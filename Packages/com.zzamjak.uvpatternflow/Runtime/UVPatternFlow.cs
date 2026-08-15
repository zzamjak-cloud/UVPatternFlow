using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// 패턴 텍스처 UV를 스크롤/회전시키는 컴포넌트 (RawImage / Image / SpriteRenderer 겸용).
    ///
    /// [모드 자동 감지]
    /// - RawImage 존재 → RawImage 모드: IMeshModifier 로 메시 UV(uv0) 를 직접 변환.
    ///   Material 을 건드리지 않으므로 SoftMask / SoftMaskLight 와 자동 호환된다.
    /// - Image 존재 → Image 모드: IMeshModifier 로 uv1(패턴 UV)/uv2(외곽 UV Rect) 채널에 데이터를 싣고,
    ///   전용 UI 셰이더(CAT/Effects/UVPatternFlow (UI))가 프래그먼트에서 frac() 으로 서브영역 내 반복 샘플링.
    ///   → 아틀라스에 포함된 스프라이트도 동작, Wrap Mode 무관. Canvas 의
    ///   Additional Shader Channels(TexCoord1/2) 는 자동 활성화된다. UGUI Mask/RectMask2D 호환.
    /// - SpriteRenderer 존재 → Sprite 모드: 전용 셰이더(CAT/Effects/UVPatternFlow (Sprite)) +
    ///   MaterialPropertyBlock 으로 UV 변환. 공유 material 1개를 모든 인스턴스가 사용.
    ///
    /// [UV 변환 순서]
    /// 회전(피벗 0.5,0.5, aspect 보정) → 타일링(UV Rect W/H) → 오프셋(UV Rect X/Y + 스크롤)
    /// 스크롤은 회전된 패턴 축을 따라 흐른다.
    ///
    /// [성능]
    /// - UI 모드는 스크롤/회전 중 매 프레임 메시를 갱신한다. 다른 UI 와 같은 Canvas 에 있으면
    ///   전체 배칭이 매 프레임 재계산되므로 전용 하위 Canvas 분리 필수 (부착 시 자동 추가됨).
    /// - Sprite 모드는 MaterialPropertyBlock 사용으로 인스턴스별 드로우콜이 된다 (다수 배치 시 주의).
    ///
    /// [제약]
    /// - RawImage/Sprite 모드: 텍스처 Wrap Mode = Repeat 필수
    /// - RawImage 모드: RawImage 의 uvRect 는 (0,0,1,1) 로 두고 이 컴포넌트의 UV Rect 를 사용 권장
    /// - Image 모드: Image Type = Simple + Use Sprite Mesh OFF 필수.
    ///   아틀라스는 Tight Packing / Rotation 비활성 필요. 밉맵 사용 시 반복 경계에 미세한 심이 보일 수 있음.
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

        // Image 모드: 공유 material 로 교체하기 전의 원본 material (기본 material 이었다면 null)
        [SerializeField, HideInInspector]
        private Material _imageOriginalMaterial;

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
        private Image _image;
        private SpriteRenderer _spriteRenderer;
        private bool _isUIMode;
        private MaterialPropertyBlock _mpb;

        private bool _canvasChannelsEnsured; // Image 모드: Canvas 채널 활성화 완료 여부
        private Sprite _outerUVSprite;       // Image 모드: 외곽 UV 캐시 기준 스프라이트
        private Vector4 _outerUVRect = new Vector4(0f, 0f, 1f, 1f); // (min.xy, size.zw)

        private Vector2 _offset;     // 스크롤 누적 오프셋
        private float _animAngle;    // 회전 속도 누적 각도 (도)
        private bool _isPlaying;

        // Sprite 모드 공유 material (Resources 에셋, 개별 값은 MPB 로 주입)
        private static Material s_spriteSharedMaterial;
        private const string SpriteMaterialResourceName = "UVPatternFlowSprite";

        // Image 모드 공유 material (개별 값은 정점 채널 uv1/uv2 로 주입 → 인스턴스 불필요)
        private static Material s_imageSharedMaterial;
        private static bool s_imageMaterialErrorLogged; // 재시도 중 에러 로그 중복 방지
        private const string ImageMaterialResourceName = "UVPatternFlowUI";

        private static readonly int PropRendererColor = Shader.PropertyToID("_RendererColor");
        private static readonly int PropUVFlowMat = Shader.PropertyToID("_UVFlowMat");
        private static readonly int PropUVFlowST = Shader.PropertyToID("_UVFlowST");
        private static readonly int PropUVFlowUI = Shader.PropertyToID("_UVFlowUI");

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
            _canvasChannelsEnsured = false;
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
            RestoreImageMaterial();
            // UI 모드: 비활성화 시 원래 UV 로 복원되도록 리빌드 트리거
            if (_rawImage != null) _rawImage.SetVerticesDirty();
            if (_image != null) _image.SetVerticesDirty();
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
        /// <summary>
        /// 에디터에서 컴포넌트 부착/리셋 시 1회 호출.
        /// UI 모드는 스크롤 중 매 프레임 메시를 갱신하므로, 부모 Canvas 전체의 배칭 재계산을
        /// 막기 위해 전용 하위 Canvas 를 자동 부착한다 (안전장치).
        /// </summary>
        private void Reset()
        {
            bool isUI = GetComponent<RawImage>() != null || GetComponent<Image>() != null;
            if (isUI && GetComponent<Canvas>() == null && GetComponentInParent<Canvas>() != null)
                UnityEditor.Undo.AddComponent<Canvas>(gameObject);
        }

        private void OnValidate()
        {
            // OnValidate 중 material 교체/SetVerticesDirty 는 경고 발생 → delayCall 로 지연
            // 플레이 모드에서도 실행해야 인스펙터 변경(UV Rect 등)이 즉시 반영된다
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                CacheTargets();
                ApplyToTarget();
                if (!Application.isPlaying)
                    UnityEditor.SceneView.RepaintAll();
            };
        }
#endif

        #endregion

        #region 대상 캐싱 / Material 관리

        private void CacheTargets()
        {
            _rawImage = GetComponent<RawImage>();
            _image = _rawImage != null ? null : GetComponent<Image>();
            _isUIMode = _rawImage != null || _image != null;
            _spriteRenderer = _isUIMode ? null : GetComponent<SpriteRenderer>();

            if (_image != null && isActiveAndEnabled)
                EnsureImageMaterial();
            if (_spriteRenderer != null && isActiveAndEnabled)
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

        /// <summary>
        /// Image 모드: Image 의 material 을 공유 UVPatternFlow UI material 로 교체한다.
        /// 기본 UI 셰이더는 uv1/uv2 채널을 사용하지 않으므로 전용 셰이더가 필요.
        /// 사용자가 호환 프로퍼티(_UVFlowUI)를 가진 커스텀 material 을 지정했으면 그대로 사용.
        /// </summary>
        private void EnsureImageMaterial()
        {
            EnsureCanvasChannels();

            Material cur = _image.material;
            if (cur != null && cur.HasProperty(PropUVFlowUI)) return;

            if (s_imageSharedMaterial == null)
            {
                s_imageSharedMaterial = Resources.Load<Material>(ImageMaterialResourceName);
                if (s_imageSharedMaterial == null)
                {
                    // 에셋 임포트가 도메인 리로드보다 늦을 수 있음 → ApplyToTarget 에서 재시도되므로 로그는 1회만
                    if (!s_imageMaterialErrorLogged)
                    {
                        s_imageMaterialErrorLogged = true;
                        Debug.LogError("[UVPatternFlow] Resources 에서 UVPatternFlowUI.mat 을 찾을 수 없습니다. Image 모드가 동작하지 않습니다.");
                    }
                    return;
                }
            }

            // 기본 material 이었다면 null 로 백업 (null 재할당 = 기본 material 복귀)
            if (_imageOriginalMaterial == null && cur != _image.defaultMaterial)
                _imageOriginalMaterial = cur;
            _image.material = s_imageSharedMaterial;
        }

        /// <summary>Image 모드: 비활성화 시 원본 material 복구</summary>
        private void RestoreImageMaterial()
        {
            if (_image == null) return;
            if (_image.material == s_imageSharedMaterial)
                _image.material = _imageOriginalMaterial;
        }

        /// <summary>
        /// Image 모드: 셰이더가 uv1(패턴 UV)/uv2(외곽 UV Rect) 를 받도록
        /// Canvas 의 Additional Shader Channels 를 자동 활성화한다.
        /// </summary>
        private void EnsureCanvasChannels()
        {
            Canvas canvas = _image.canvas;
            if (canvas == null) return;

            const AdditionalCanvasShaderChannels required =
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
            if ((canvas.additionalShaderChannels & required) != required)
                canvas.additionalShaderChannels |= required;
            _canvasChannelsEnsured = true;
        }

        #endregion

        #region UV 변환 적용

        private void ApplyToTarget()
        {
            if (_isUIMode)
            {
                // 메시 리빌드 트리거 → ModifyMesh 에서 현재 상태로 UV 변환
                if (_rawImage != null) _rawImage.SetVerticesDirty();
                else if (_image != null)
                {
                    // 캔버스 초기화/에셋 임포트가 늦을 수 있으므로 성공할 때까지 재시도
                    if (!_canvasChannelsEnsured) EnsureCanvasChannels();
                    if (s_imageSharedMaterial == null) EnsureImageMaterial();
                    _image.SetVerticesDirty();
                }
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
                RectTransform rt = _rawImage != null ? _rawImage.rectTransform : _image.rectTransform;
                Rect r = rt.rect;
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
        /// UI 모드: 메시 UV 를 변환한다 (회전 → 타일링 → 오프셋).
        /// - RawImage: uv0 직접 변환. Material 을 건드리지 않으므로 SoftMask 체인과 자동 호환.
        /// - Image: uv1 에 변환된 패턴 UV, uv2 에 스프라이트 외곽 UV Rect 를 실어
        ///   전용 UI 셰이더가 프래그먼트에서 서브영역 내 반복 샘플링 (아틀라스 지원).
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

            if (_rawImage != null)
            {
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
                return;
            }

            // Image 모드: 패턴 좌표는 정점 위치(rect 내 0~1)에서 계산 → 스프라이트 UV 레이아웃과 무관
            Rect rect = _image.rectTransform.rect;
            float invW = rect.width  > 0.0001f ? 1f / rect.width  : 0f;
            float invH = rect.height > 0.0001f ? 1f / rect.height : 0f;

            // 스프라이트의 아틀라스 내 외곽 UV (독립 텍스처면 0,0~1,1). 스프라이트 변경 시에만 재계산
            Sprite sprite = _image.sprite;
            if (sprite != _outerUVSprite)
            {
                _outerUVSprite = sprite;
                if (sprite != null)
                {
                    Vector4 o = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                    _outerUVRect = new Vector4(o.x, o.y, o.z - o.x, o.w - o.y);
                }
                else
                {
                    _outerUVRect = new Vector4(0f, 0f, 1f, 1f);
                }
            }
            Vector4 outerRect = _outerUVRect;

            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float px = (vert.position.x - rect.xMin) * invW - 0.5f;
                float py = (vert.position.y - rect.yMin) * invH - 0.5f;
                float rx = m.x * px + m.y * py + 0.5f;
                float ry = m.z * px + m.w * py + 0.5f;
                vert.uv1 = new Vector4(
                    rx * _uvRect.width + offX,
                    ry * _uvRect.height + offY, 0f, 0f);
                vert.uv2 = outerRect;
                vh.SetUIVertex(vert, i);
            }
        }

        #endregion
    }
}
