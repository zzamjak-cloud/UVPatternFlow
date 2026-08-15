using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 스프라이트 시트(예: 3×3 아틀라스) 기반 그리드 플로우 효과 (RawImage / Image 겸용).
    ///
    /// 시트를 그리드 셀로 반복 배열하고, 각 셀이 지정 주기(switchDuration)마다
    /// 시트 내 프레임을 랜덤으로 스위칭하면서 전체 그리드가 지정 방향으로 무한 스크롤한다.
    /// 파티클 시스템의 Texture Sheet Animation 처럼 Tiles X/Y 로 시트를 분할한다.
    ///
    /// [구현 방식]
    /// - 셀 분할/간격/프레임 선택은 전부 프래그먼트 셰이더에서 처리 → 쿼드 1개, Canvas rebuild 없음
    /// - material 은 컴포넌트당 인스턴스 1개 (HideFlags.DontSave), 시트 텍스처는
    ///   RawImage.texture / Image.sprite 텍스처가 CanvasRenderer 로 자동 주입
    /// - Image 모드: 시트 스프라이트가 스프라이트 아틀라스에 포함돼 있어도 동작.
    ///   아틀라스 내 외곽 UV(_OuterUV)를 material 로 넘겨 셰이더가 서브영역 안에서만 샘플링
    /// - 스크롤 오프셋/시간은 C# 에서 누적하여 주입 → Play/Pause/속도 변경 시 위치 튐 없음
    ///
    /// [제약]
    /// - UGUI Mask(스텐실) / RectMask2D(_ClipRect) 호환. SoftMask 계열은 미대응 (전용 셰이더 변형 필요)
    /// - Wrap Mode 무관 (셰이더 내부 frac 처리)
    /// - RawImage 모드: uvRect 는 기본값(0,0,1,1) 권장
    /// - Image 모드: Image Type = Simple + Use Sprite Mesh OFF 필수.
    ///   아틀라스는 Tight Packing / Rotation 비활성 필요
    /// </summary>
    [AddComponentMenu("CAT/Effects/UVSheetGridFlow")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UVSheetGridFlow : MonoBehaviour
    {
        #region 직렬화 필드

        [SerializeField, Tooltip("시트 분할 수 (가로×세로). 예: 3×3 = 9프레임")]
        private Vector2Int _sheetTiles = new Vector2Int(3, 3);

        [SerializeField, Tooltip("화면에 반복할 그리드 셀 수 (가로×세로)")]
        private Vector2 _gridCount = new Vector2(4f, 4f);

        [SerializeField, Tooltip("셀 간격 (셀 크기 대비 비율 0~0.9). 간격 부분은 투명")]
        private Vector2 _cellGap = new Vector2(0.1f, 0.1f);

        [SerializeField, Tooltip("초당 스크롤 속도 (그리드 셀 단위)")]
        private Vector2 _scrollSpeed = new Vector2(0.5f, 0f);

        [SerializeField, Min(0.05f), Tooltip("이미지 스위칭 주기 (초). 셀마다 위상이 달라 자연스럽게 전환됨")]
        private float _switchDuration = 0.5f;

        [SerializeField, Range(0f, 0.05f), Tooltip("프레임 가장자리 인셋 — 인접 프레임 블리딩 방지")]
        private float _frameInset = 0.005f;

        [SerializeField, Tooltip("컴포넌트 활성화 시 자동 재생")]
        private bool _playOnEnable = true;

        #endregion

        #region 공개 프로퍼티

        /// <summary>시트 분할 수 (가로×세로)</summary>
        public Vector2Int SheetTiles
        {
            get => _sheetTiles;
            set { _sheetTiles = new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y)); ApplyStaticProperties(); }
        }

        /// <summary>화면에 반복할 그리드 셀 수</summary>
        public Vector2 GridCount
        {
            get => _gridCount;
            set { _gridCount = value; ApplyStaticProperties(); }
        }

        /// <summary>셀 간격 (셀 크기 대비 비율 0~0.9)</summary>
        public Vector2 CellGap
        {
            get => _cellGap;
            set { _cellGap = new Vector2(Mathf.Clamp(value.x, 0f, 0.9f), Mathf.Clamp(value.y, 0f, 0.9f)); ApplyStaticProperties(); }
        }

        /// <summary>초당 스크롤 속도 (그리드 셀 단위)</summary>
        public Vector2 ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = value;
        }

        /// <summary>이미지 스위칭 주기 (초)</summary>
        public float SwitchDuration
        {
            get => _switchDuration;
            set { _switchDuration = Mathf.Max(0.05f, value); ApplyStaticProperties(); }
        }

        public bool IsPlaying => _isPlaying;

        #endregion

        #region 내부 상태

        private RawImage _rawImage;
        private Image _image;
        private Graphic _graphic;        // material 연결 대상 (RawImage 또는 Image)
        private Material _material;      // 컴포넌트당 인스턴스 (DontSave)
        private Sprite _outerUVSprite;   // 외곽 UV 캐시 기준 스프라이트 (변경 시에만 재계산)
        private Vector2 _flowOffset;     // 스크롤 누적 (그리드 셀 단위)
        private float _flowTime;         // 스위칭 슬롯용 누적 시간
        private bool _isPlaying;

        private const string ShaderResourceName = "UVSheetGridFlow";
        // 스크롤 오프셋 래핑 주기 (셀 단위). 래핑 순간 셀 해시가 이웃으로 이동해 전체가 1회 스위칭되므로
        // 충분히 크게, 단 fp32 frac 정밀도(그리드 좌표 = 오프셋 + 셀 수)가 유지되는 범위로 제한.
        private const float OffsetWrapPeriod = 2048f;

        private static readonly int PropOuterUV = Shader.PropertyToID("_OuterUV");
        private static readonly int PropTiles = Shader.PropertyToID("_Tiles");
        private static readonly int PropGridCount = Shader.PropertyToID("_GridCount");
        private static readonly int PropGap = Shader.PropertyToID("_Gap");
        private static readonly int PropFrameInset = Shader.PropertyToID("_FrameInset");
        private static readonly int PropSwitchDuration = Shader.PropertyToID("_SwitchDuration");
        private static readonly int PropFlowOffset = Shader.PropertyToID("_FlowOffset");
        private static readonly int PropFlowTime = Shader.PropertyToID("_FlowTime");

        #endregion

        #region 공개 API

        public void Play()
        {
            _isPlaying = true;
        }

        public void Pause()
        {
            _isPlaying = false;
        }

        public void Stop()
        {
            _isPlaying = false;
            _flowOffset = Vector2.zero;
            _flowTime = 0f;
            ApplyDynamicProperties();
        }

        /// <summary>에디터 전용: 외부에서 deltaTime을 전달하여 흐름을 진행시킨다.</summary>
        public void EditorAdvance(float dt)
        {
            Advance(dt);
        }

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            CacheTarget();
        }

        private void OnEnable()
        {
            CacheTarget();
            EnsureMaterial();
            _flowOffset = Vector2.zero;
            _flowTime = 0f;
            ApplyStaticProperties();
            ApplyDynamicProperties();

            if (_playOnEnable && Application.isPlaying)
                Play();
        }

        private void OnDisable()
        {
            // 원래 렌더링(기본 UI material)으로 복구
            if (_graphic != null) _graphic.material = null;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || !_isPlaying) return;
            Advance(Time.deltaTime);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _sheetTiles = new Vector2Int(Mathf.Max(1, _sheetTiles.x), Mathf.Max(1, _sheetTiles.y));
            _cellGap = new Vector2(Mathf.Clamp(_cellGap.x, 0f, 0.9f), Mathf.Clamp(_cellGap.y, 0f, 0.9f));

            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                EnsureMaterial();
                ApplyStaticProperties();
                ApplyDynamicProperties();
            };
        }
#endif

        #endregion

        #region 내부 구현

        /// <summary>시간을 진행시키고 material 에 반영한다.</summary>
        private void Advance(float dt)
        {
            _flowOffset += _scrollSpeed * dt;
            // 부동소수점 정밀도 유지 (래핑 주기는 충분히 커서 시각적 팝은 실사용에서 발생하지 않음)
            _flowOffset.x -= Mathf.Floor(_flowOffset.x / OffsetWrapPeriod) * OffsetWrapPeriod;
            _flowOffset.y -= Mathf.Floor(_flowOffset.y / OffsetWrapPeriod) * OffsetWrapPeriod;

            _flowTime += dt;
            float timeWrap = _switchDuration * OffsetWrapPeriod;
            _flowTime -= Mathf.Floor(_flowTime / timeWrap) * timeWrap;

            ApplyDynamicProperties();
        }

        /// <summary>대상 Graphic(RawImage 우선, 없으면 Image)을 캐싱한다.</summary>
        private void CacheTarget()
        {
            _rawImage = GetComponent<RawImage>();
            _image = _rawImage != null ? null : GetComponent<Image>();
            _graphic = _rawImage != null ? (Graphic)_rawImage : _image;
        }

        /// <summary>전용 셰이더의 material 인스턴스를 생성하고 대상 Graphic 에 연결한다.</summary>
        private void EnsureMaterial()
        {
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourceName);
                if (shader == null)
                {
                    Debug.LogError("[UVSheetGridFlow] Resources 에서 UVSheetGridFlow.shader 를 찾을 수 없습니다.");
                    return;
                }
                _material = new Material(shader)
                {
                    name = "UVSheetGridFlow (Instance)",
                    hideFlags = HideFlags.DontSave
                };
            }

            if (_graphic != null && _graphic.material != _material)
                _graphic.material = _material;
        }

        /// <summary>
        /// Image 모드: 시트 스프라이트의 아틀라스 내 외곽 UV (min.xy, size.zw)를 material 에 반영한다.
        /// 스프라이트가 바뀐 경우에만 재계산 (RawImage 모드/스프라이트 미지정 시 전체 텍스처 0,0,1,1).
        /// </summary>
        private void RefreshOuterUV(bool force)
        {
            Sprite cur = _image != null ? _image.sprite : null;
            if (!force && cur == _outerUVSprite) return;
            _outerUVSprite = cur;

            Vector4 outer = new Vector4(0f, 0f, 1f, 1f);
            if (cur != null)
            {
                Vector4 o = UnityEngine.Sprites.DataUtility.GetOuterUV(cur);
                outer = new Vector4(o.x, o.y, o.z - o.x, o.w - o.y);
            }
            _material.SetVector(PropOuterUV, outer);
        }

        /// <summary>설정성 프로퍼티 반영 (값 변경 시에만 호출)</summary>
        private void ApplyStaticProperties()
        {
            if (_material == null) return;
            RefreshOuterUV(true);
            _material.SetVector(PropTiles, new Vector4(_sheetTiles.x, _sheetTiles.y, 0f, 0f));
            _material.SetVector(PropGridCount, new Vector4(_gridCount.x, _gridCount.y, 0f, 0f));
            _material.SetVector(PropGap, new Vector4(_cellGap.x, _cellGap.y, 0f, 0f));
            _material.SetFloat(PropFrameInset, _frameInset);
            _material.SetFloat(PropSwitchDuration, _switchDuration);
        }

        /// <summary>매 프레임 갱신 프로퍼티 반영 (오프셋/시간)</summary>
        private void ApplyDynamicProperties()
        {
            if (_material == null) return;
            _material.SetVector(PropFlowOffset, new Vector4(_flowOffset.x, _flowOffset.y, 0f, 0f));
            _material.SetFloat(PropFlowTime, _flowTime);
            // Image 모드: 재생 중 스프라이트 교체 감지 (동일 스프라이트면 no-op)
            RefreshOuterUV(false);
        }

        #endregion
    }
}
