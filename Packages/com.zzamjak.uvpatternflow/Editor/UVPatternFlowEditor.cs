using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// UVPatternFlow 인스펙터.
    /// - 모드(UI/Sprite) 표시, 스크롤/회전/UV Rect 필드
    /// - 대상 렌더러/텍스처 설정 오류 시 경고 출력
    /// - 에디터 미리보기 / 플레이 모드 제어 버튼
    /// </summary>
    [CustomEditor(typeof(UVPatternFlow))]
    [CanEditMultipleObjects]
    public class UVPatternFlowEditor : Editor
    {
        private SerializedProperty _scrollSpeed;
        private SerializedProperty _uvRect;
        private SerializedProperty _rotation;
        private SerializedProperty _rotationSpeed;
        private SerializedProperty _aspectCompensation;
        private SerializedProperty _playOnEnable;

        private bool _editorPreviewRunning;
        private double _editorPreviewLastTime;

        private void OnEnable()
        {
            _scrollSpeed        = serializedObject.FindProperty("_scrollSpeed");
            _uvRect             = serializedObject.FindProperty("_uvRect");
            _rotation           = serializedObject.FindProperty("_rotation");
            _rotationSpeed      = serializedObject.FindProperty("_rotationSpeed");
            _aspectCompensation = serializedObject.FindProperty("_aspectCompensation");
            _playOnEnable       = serializedObject.FindProperty("_playOnEnable");
        }

        private void OnDisable()
        {
            StopEditorPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("스크롤", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_scrollSpeed, new GUIContent("스크롤 속도 (X/Y)"));
            EditorGUILayout.PropertyField(_uvRect,      new GUIContent("UV Rect", "타일링(W/H) + 기본 오프셋(X/Y). RawImage.uvRect 대신 이 값을 사용하세요"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("회전", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_rotation,           new GUIContent("회전 각도 (도)", "양수 = 화면상 반시계"));
            EditorGUILayout.PropertyField(_rotationSpeed,      new GUIContent("회전 속도 (도/초)"));
            EditorGUILayout.PropertyField(_aspectCompensation, new GUIContent("비율 왜곡 보정", "비정사각 영역에서 회전 시 패턴이 찌그러지지 않도록 보정"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_playOnEnable, new GUIContent("활성화 시 자동 재생"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);

            var flow = (UVPatternFlow)target;
            DrawWarnings(flow);

            EditorGUILayout.Space(6);

            if (Application.isPlaying)
                DrawPlayModeButtons(flow);
            else
                DrawEditModePreview(flow);
        }

        private void DrawWarnings(UVPatternFlow flow)
        {
            var rawImage = flow.GetComponent<RawImage>();
            var image = rawImage != null ? null : flow.GetComponent<Image>();
            var spriteRenderer = (rawImage != null || image != null) ? null : flow.GetComponent<SpriteRenderer>();

            // 대상 렌더러 검사
            if (rawImage == null && image == null && spriteRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "RawImage, Image 또는 SpriteRenderer 컴포넌트가 필요합니다.",
                    MessageType.Warning);
                return;
            }

            string mode = rawImage != null ? "UI (RawImage)" : image != null ? "UI (Image)" : "Sprite (SpriteRenderer)";
            EditorGUILayout.LabelField("모드", mode);

            // 텍스처 Wrap Mode 검사 — Image 모드는 셰이더 frac 반복이라 Wrap Mode 무관
            Texture tex = null;
            if (rawImage != null) tex = rawImage.texture;
            else if (spriteRenderer != null && spriteRenderer.sprite != null) tex = spriteRenderer.sprite.texture;

            if (tex != null && tex.wrapMode != TextureWrapMode.Repeat
                && tex.wrapMode != TextureWrapMode.Mirror && tex.wrapMode != TextureWrapMode.MirrorOnce)
            {
                EditorGUILayout.HelpBox(
                    "텍스처의 Wrap Mode 가 Repeat 이 아니면 스크롤/타일링이 끊어집니다. Texture Import 설정에서 Wrap Mode = Repeat 로 변경하세요.",
                    MessageType.Warning);
            }

            // UI 모드 공통: 전용 하위 Canvas 분리 안내 (매 프레임 메시 갱신 → 부모 Canvas 배칭 보호)
            if ((rawImage != null || image != null) && flow.GetComponent<Canvas>() == null)
            {
                EditorGUILayout.HelpBox(
                    "UI 모드는 스크롤/회전 중 매 프레임 메시를 갱신합니다. 다른 UI 와 같은 Canvas 에 있으면 " +
                    "전체 배칭이 매 프레임 재계산되므로, 이 오브젝트에 전용 Canvas 를 추가해 분리하는 것을 권장합니다.",
                    MessageType.Warning);
                if (GUILayout.Button("전용 Canvas 추가"))
                    Undo.AddComponent<Canvas>(flow.gameObject);
            }

            // Image 모드 전용 검사
            if (image != null)
                DrawImageWarnings(image);

            // Sprite 모드 전용 검사
            if (spriteRenderer != null)
            {
                if (spriteRenderer.sprite != null && spriteRenderer.sprite.packed)
                {
                    EditorGUILayout.HelpBox(
                        "Sprite 모드는 아틀라스 스프라이트를 지원하지 않습니다. 독립 텍스처로 Import 하거나 Image 컴포넌트를 사용하세요.",
                        MessageType.Warning);
                }
                DrawTightMeshWarning(spriteRenderer.sprite);
                if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
                {
                    EditorGUILayout.HelpBox(
                        "Draw Mode = Simple 을 권장합니다. Tiled/Sliced 모드는 타일별 UV 로 인해 회전/스크롤이 의도대로 표시되지 않을 수 있습니다.",
                        MessageType.Info);
                }
            }
        }

        /// <summary>Image 모드 설정 검사 (Simple 타입, 스프라이트 메시, 아틀라스 패킹 방식)</summary>
        private void DrawImageWarnings(Image image)
        {
            if (image.type != Image.Type.Simple)
            {
                EditorGUILayout.HelpBox(
                    "Image Type = Simple 이어야 합니다. Sliced/Tiled/Filled 는 패치별 UV 로 인해 패턴이 의도대로 표시되지 않습니다.",
                    MessageType.Warning);
            }

            if (image.useSpriteMesh)
            {
                EditorGUILayout.HelpBox(
                    "Use Sprite Mesh 가 켜져 있으면 알파 외곽선 메시가 패턴을 마스킹합니다. 옵션을 끄세요.",
                    MessageType.Warning);
            }

            Sprite sprite = image.sprite;
            if (sprite != null && sprite.packed)
            {
                if (sprite.packingMode != SpritePackingMode.Rectangle)
                {
                    EditorGUILayout.HelpBox(
                        "아틀라스가 Tight Packing 이면 이웃 스프라이트가 패턴 영역을 침범합니다. SpriteAtlas 의 Tight Packing 을 끄세요.",
                        MessageType.Warning);
                }
                if (sprite.packingRotation != SpritePackingRotation.None)
                {
                    EditorGUILayout.HelpBox(
                        "아틀라스에서 회전 패킹된 스프라이트는 UV 방향이 달라져 사용할 수 없습니다. SpriteAtlas 의 Allow Rotation 을 끄세요.",
                        MessageType.Warning);
                }
            }
        }

        /// <summary>
        /// Sprite Mesh Type = Tight 검사.
        /// Tight 메시는 알파 외곽선을 따라 지오메트리를 생성하므로 UV 스크롤 시
        /// 고정된 실루엣이 마스크처럼 패턴을 잘라낸다 → Full Rect 필수.
        /// </summary>
        private void DrawTightMeshWarning(Sprite sprite)
        {
            if (sprite == null) return;

            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path)) return;
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.Tight) return;

            EditorGUILayout.HelpBox(
                "Sprite Mesh Type 이 Tight 입니다. Tight 메시는 알파 외곽선을 따라 잘리므로 " +
                "UV 스크롤 시 패턴이 실루엣에 마스킹됩니다. Full Rect 로 변경하세요.",
                MessageType.Warning);

            if (GUILayout.Button("Mesh Type 을 Full Rect 로 변경"))
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private void DrawPlayModeButtons(UVPatternFlow flow)
        {
            EditorGUILayout.LabelField("플레이 모드 제어", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !flow.IsPlaying;
                if (GUILayout.Button("▶ Play"))
                    flow.Play();

                GUI.enabled = flow.IsPlaying;
                if (GUILayout.Button("⏸ Pause"))
                    flow.Pause();

                GUI.enabled = true;
                if (GUILayout.Button("■ Stop"))
                    flow.Stop();
            }
        }

        private void DrawEditModePreview(UVPatternFlow flow)
        {
            EditorGUILayout.LabelField("에디터 미리보기", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!_editorPreviewRunning)
                {
                    if (GUILayout.Button("▶ 미리보기 시작"))
                        StartEditorPreview();
                }
                else
                {
                    GUI.color = new Color(1f, 0.6f, 0.6f);
                    if (GUILayout.Button("■ 미리보기 중지"))
                        StopEditorPreview();
                    GUI.color = Color.white;
                }

                if (GUILayout.Button("오프셋 초기화", GUILayout.Width(90)))
                {
                    flow.ResetOffset();
                    SceneView.RepaintAll();
                }
            }
        }

        private void StartEditorPreview()
        {
            if (_editorPreviewRunning) return;
            _editorPreviewRunning = true;
            _editorPreviewLastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorPreviewUpdate;
        }

        private void StopEditorPreview()
        {
            if (!_editorPreviewRunning) return;
            _editorPreviewRunning = false;
            EditorApplication.update -= EditorPreviewUpdate;
            (target as UVPatternFlow)?.ResetOffset();
            SceneView.RepaintAll();
        }

        private void EditorPreviewUpdate()
        {
            if (!_editorPreviewRunning) return;

            var flow = target as UVPatternFlow;
            if (flow == null) { StopEditorPreview(); return; }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _editorPreviewLastTime);
            _editorPreviewLastTime = now;

            flow.EditorAdvance(dt);
            // 편집 모드에서 캔버스 dirty 플래그를 즉시 처리하여 UV 변경이 씬에 반영되도록 강제
            if (flow.IsUIMode) Canvas.ForceUpdateCanvases();
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
