using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 패턴 텍스처 UV를 스크롤/회전시키는 컴포넌트 (RawImage / SpriteRenderer 양용).
    ///
    /// [모드 자동 감지]
    /// - RawImage 존재 → UI 모드: IMeshModifier 로 메시 UV 를 직접 변환.
    ///   Material 을 건드리지 않으므로 SoftMask / SoftMaskLight 와 자동 호환된다.
    /// - SpriteRenderer 존재 → Sprite 모드: 전용 셰이더(CAT/Effects/UVPatternFlow (Sprite)) +
    ///   MaterialPropertyBlock 으로 UV 변환. 공유 material 1개를 모든 인스턴스가 사용.
    ///
    /// [UV 변환 순서]
    /// 회전(피벗 0.5,0.5, aspect 보정) → 타일링(UV Rect W/H) → 오프셋(UV Rect X/Y + 스크롤)
    /// 스크롤은 회전된 패턴 축을 따라 흐른다.
    ///
    /// [제약]
    /// - 텍스처 Wrap Mode = Repeat 필수
    /// - UI 모드: RawImage 의 uvRect 는 (0,0,1,1) 로 두고 이 컴포넌트의 UV Rect 를 사용 권장
    /// - Sprite 모드: 아틀라스 불가, Mesh Type = Full Rect, Draw Mode = Simple 권장
    /// </summary>
    [AddComponentMenu("CAT/Effects/UVPatternFlow")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UVPatternFlow : MonoBehaviour, IMeshModifier
    {
        #region 직렬화 필드

        [SerializeField, Tooltip("초당 UV 스크롤 속도 (X/Y축)")]
        private Vector2 _scrollSpeed = new Vector2(0.1f, 0f);

        [SerializeField, Tooltip("UV Rect — 타일링(W/H)과 기본 오프셋(X/Y). RawImage.uvRect 대신 이 값을 사용하세요")]
        private Rect _uvRect = new Rect(0f, 0f, 1f, 1f);

        [SerializeField, Tooltip("패턴 회전 각도 (도). 양수 = 화면상 반시계")]
        private float _rotation = 0f;

        [SerializeField, Tooltip("회전 속도 (도/초). 0 = 회전 애니메이션 없음")]
        private float _rotationSpeed = 0f;

        [SerializeField, Tooltip("비정사각 영역에서 회전 시 패턴이 찌그러지지 않도록 가로세로 비율 보정")]
        private bool _aspectCompensation = true;

        [SerializeField, Tooltip("컴포넌트 활성화 시 자동 재생")]
        private bool _playOnEnable = true;

        // Sprite 모드: 공유 material 로 교체하기 전의 원본 material (비활성화 시 복구용, 씬에 직렬화)
        [SerializeField, HideInInspector]
        private Material _spriteOriginalMaterial;

        #endregion

        #region 공개 프로퍼티

        public Vector2 ScrollSpeed
        {
            get => _scrollSpeed;
            set => _scrollSpeed = value;
        }

        /// <summary>타일링(W/H) + 기본 오프셋(X/Y)</summary>
        public Rect UVRect
        {
            get => _uvRect;
            set { _uvRect = value; ApplyToTarget(); }
        }

        /// <summary>패턴 회전 각도 (도). 양수 = 화면상 반시계</summary>
        public float Rotation
        {
            get => _rotation;
            set { _rotation = value; ApplyToTarget(); }
        }

        /// <summary>회전 속도 (도/초)</summary>
        public float RotationSpeed
        {
            get => _rotationSpeed;
            set => _rotationSpeed = value;
        }

        /// <summary>비정사각 영역 회전 왜곡 보정</summary>
        public bool AspectCompensation
        {
            get => _aspectCompensation;
            set { _aspectCompensation = value; ApplyToTarget(); }
        }

        public bool IsPlaying => _isPlaying;

        /// <summary>RawImage 대상으로 동작 중인지 여부</summary>
        public bool IsUIMode => _isUIMode;

        #endregion

        #region 내부 상태

        private RawImage _rawImage;
        private SpriteRenderer _spriteRenderer;
        private bool _isUIMode;
        private MaterialPropertyBlock _mpb;

        private Vector2 _offset;     // 스크롤 누적 오프셋
        private float _animAngle;    // 회전 속도 누적 각도 (도)
        private bool _isPlaying;

        // Sprite 모드 공유 material (Resources 에셋, 개별 값은 MPB 로 주입)
        private static Material s_spriteSharedMaterial;
        private const string SpriteMaterialResourceName = "UVPatternFlowSprite";

        private static readonly int PropRendererColor = Shader.PropertyToID("_RendererColor");
        private static readonly int PropUVFlowMat = Shader.PropertyToID("_UVFlowMat");
        private static readonly int PropUVFlowST = Shader.PropertyToID("_UVFlowST");

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
            _offset = Vector2.zero;
            _animAngle = 0f;
            ApplyToTarget();
        }

        public void SetOffset(Vector2 offset)
        {
            _offset = offset;
            ApplyToTarget();
        }

        public void ResetOffset()
        {
            _offset = Vector2.zero;
            _animAngle = 0f;
            ApplyToTarget();
        }

        /// <summary>에디터 전용: 외부에서 deltaTime을 전달하여 스크롤/회전을 진행시킨다.</summary>
        public void EditorAdvance(float dt)
        {
            _offset += _scrollSpeed * dt;
            _animAngle += _rotationSpeed * dt;
            WrapOffset();
            WrapAngle();
            ApplyToTarget();
        }

        #endregion

        #region Unity 생명주기

        private void Awake()
        {
            CacheTargets();
        }

        private void OnEnable()
        {
            CacheTargets();
            _offset = Vector2.zero;
            _animAngle = 0f;
            ApplyToTarget();

            if (_playOnEnable && Application.isPlaying)
                Play();
        }

        private void OnDisable()
        {
            RestoreSpriteMaterial();
            // UI 모드: 비활성화 시 원래 UV 로 복원되도록 리빌드 트리거
            if (_rawImage != null) _rawImage.SetVerticesDirty();
        }

        private void Update()
        {
            if (!Application.isPlaying || !_isPlaying) return;

            bool scrolling = _scrollSpeed.x != 0f || _scrollSpeed.y != 0f;
            bool rotating = _rotationSpeed != 0f;
            if (!scrolling && !rotating) return;

            float dt = Time.deltaTime;
            if (scrolling)
            {
                _offset += _scrollSpeed * dt;
                WrapOffset();
            }
            if (rotating)
            {
                _animAngle += _rotationSpeed * dt;
                WrapAngle();
            }
            ApplyToTarget();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            // OnValidate 중 material 교체/SetVerticesDirty 는 경고 발생 → delayCall 로 지연
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                CacheTargets();
                ApplyToTarget();
            };
        }
#endif

        #endregion

        #region 대상 캐싱 / Material 관리

        private void CacheTargets()
        {
            _rawImage = GetComponent<RawImage>();
            _isUIMode = _rawImage != null;
            _spriteRenderer = _isUIMode ? null : GetComponent<SpriteRenderer>();

            if (!_isUIMode && _spriteRenderer != null && isActiveAndEnabled)
                EnsureSpriteMaterial();
        }

        /// <summary>
        /// Sprite 모드: SpriteRenderer 의 material 을 공유 UVPatternFlow material 로 교체한다.
        /// URP 기본 스프라이트 셰이더는 UV 오프셋/회전 파라미터가 없으므로 전용 셰이더가 필요.
        /// 사용자가 호환 프로퍼티(_UVFlowST)를 가진 커스텀 material 을 지정했으면 그대로 사용.
        /// </summary>
        private void EnsureSpriteMaterial()
        {
            Material cur = _spriteRenderer.sharedMaterial;
            if (cur != null && cur.HasProperty(PropUVFlowST)) return;

            if (s_spriteSharedMaterial == null)
            {
                s_spriteSharedMaterial = Resources.Load<Material>(SpriteMaterialResourceName);
                if (s_spriteSharedMaterial == null)
                {
                    Debug.LogError("[UVPatternFlow] Resources 에서 UVPatternFlowSprite.mat 을 찾을 수 없습니다. Sprite 모드가 동작하지 않습니다.");
                    return;
                }
            }

            if (_spriteOriginalMaterial == null) _spriteOriginalMaterial = cur; // 최초 1회 백업
            _spriteRenderer.sharedMaterial = s_spriteSharedMaterial;
        }

        /// <summary>Sprite 모드: 비활성화 시 원본 material 복구</summary>
        private void RestoreSpriteMaterial()
        {
            if (_spriteRenderer == null) return;
            if (_spriteRenderer.sharedMaterial == s_spriteSharedMaterial && _spriteOriginalMaterial != null)
                _spriteRenderer.sharedMaterial = _spriteOriginalMaterial;
        }

        #endregion

        #region UV 변환 적용

        private void ApplyToTarget()
        {
            if (_isUIMode)
            {
                // 메시 리빌드 트리거 → ModifyMesh 에서 현재 상태로 UV 변환
                if (_rawImage != null) _rawImage.SetVerticesDirty();
            }
            else if (_spriteRenderer != null)
            {
                ApplySpriteProperties();
            }
        }

        /// <summary>Sprite 모드: MaterialPropertyBlock 으로 UV 변환 파라미터 주입 (material 인스턴스 생성 없음)</summary>
        private void ApplySpriteProperties()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _spriteRenderer.GetPropertyBlock(_mpb);

            // flipX/Y 는 커스텀 셰이더에서 처리되지 않으므로 타일링 부호로 흡수
            float tileX = _uvRect.width * (_spriteRenderer.flipX ? -1f : 1f);
            float tileY = _uvRect.height * (_spriteRenderer.flipY ? -1f : 1f);

            _mpb.SetVector(PropUVFlowMat, ComputeUVMatrix());
            _mpb.SetVector(PropUVFlowST, new Vector4(tileX, tileY, _uvRect.x + _offset.x, _uvRect.y + _offset.y));
            // Unity 6: SpriteRenderer 색상은 정점 컬러에 실리지 않음 (unity_SpriteColor) → MPB 로 전달
            _mpb.SetColor(PropRendererColor, _spriteRenderer.color);
            _spriteRenderer.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// UV 회전 행렬(2×2)을 계산한다. M = S(1/a)·R(-θ)·S(a)
        /// - 샘플링 좌표를 역방향(-θ) 회전 → 화면상 패턴은 양수 = 반시계 회전
        /// - a = 표시 영역 가로/세로 비 (aspect 보정 ON 시) → 회전해도 패턴 모양 유지
        /// 반환: (m00, m01, m10, m11)
        /// </summary>
        private Vector4 ComputeUVMatrix()
        {
            float rad = (_rotation + _animAngle) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            float a = _aspectCompensation ? Mathf.Max(0.001f, GetDisplayAspect()) : 1f;
            return new Vector4(c, s / a, -s * a, c);
        }

        /// <summary>표시 영역의 가로/세로 비율 (aspect 보정용)</summary>
        private float GetDisplayAspect()
        {
            if (_isUIMode)
            {
                Rect r = _rawImage.rectTransform.rect;
                return r.height > 0.0001f ? r.width / r.height : 1f;
            }

            Sprite sp = _spriteRenderer.sprite;
            Vector2 size;
            if (_spriteRenderer.drawMode != SpriteDrawMode.Simple)
                size = _spriteRenderer.size;
            else
                size = sp != null ? (Vector2)sp.bounds.size : Vector2.one;
            return size.y > 0.0001f ? size.x / size.y : 1f;
        }

        private void WrapOffset()
        {
            // 부동소수점 정밀도 유지를 위해 [0, 1) 범위로 래핑
            _offset.x -= Mathf.Floor(_offset.x);
            _offset.y -= Mathf.Floor(_offset.y);
        }

        private void WrapAngle()
        {
            // [0, 360) 범위로 래핑
            _animAngle -= Mathf.Floor(_animAngle / 360f) * 360f;
        }

        #endregion

        #region IMeshModifier (UI 모드 전용)

        /// <summary>레거시 시그니처 (미사용)</summary>
        public void ModifyMesh(Mesh mesh) { }

        /// <summary>
        /// UI 모드: RawImage 메시의 UV 를 직접 변환한다 (회전 → 타일링 → 오프셋).
        /// Material 을 건드리지 않으므로 SoftMask / SoftMaskLight 체인과 자동 호환.
        /// </summary>
        public void ModifyMesh(VertexHelper vh)
        {
            if (!isActiveAndEnabled || !_isUIMode) return;

            int count = vh.currentVertCount;
            if (count == 0) return;

            Vector4 m = ComputeUVMatrix();
            float offX = _uvRect.x + _offset.x;
            float offY = _uvRect.y + _offset.y;

            UIVertex vert = default;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float px = vert.uv0.x - 0.5f;
                float py = vert.uv0.y - 0.5f;
                float rx = m.x * px + m.y * py + 0.5f;
                float ry = m.z * px + m.w * py + 0.5f;
                vert.uv0 = new Vector4(
                    rx * _uvRect.width + offX,
                    ry * _uvRect.height + offY,
                    vert.uv0.z, vert.uv0.w);
                vh.SetUIVertex(vert, i);
            }
        }

        #endregion
    }
}
