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
            var spriteRenderer = flow.GetComponent<SpriteRenderer>();

            // 대상 렌더러 검사
            if (rawImage == null && spriteRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "RawImage 또는 SpriteRenderer 컴포넌트가 필요합니다.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("모드", rawImage != null ? "UI (RawImage)" : "Sprite (SpriteRenderer)");

            // 텍스처 Wrap Mode 검사
            Texture tex = null;
            if (rawImage != null) tex = rawImage.texture;
            else if (spriteRenderer.sprite != null) tex = spriteRenderer.sprite.texture;

            if (tex != null && tex.wrapMode != TextureWrapMode.Repeat
                && tex.wrapMode != TextureWrapMode.Mirror && tex.wrapMode != TextureWrapMode.MirrorOnce)
            {
                EditorGUILayout.HelpBox(
                    "텍스처의 Wrap Mode 가 Repeat 이 아니면 스크롤/타일링이 끊어집니다. Texture Import 설정에서 Wrap Mode = Repeat 로 변경하세요.",
                    MessageType.Warning);
            }

            // Sprite 모드 전용 검사
            if (spriteRenderer != null)
            {
                if (spriteRenderer.sprite != null && spriteRenderer.sprite.packed)
                {
                    EditorGUILayout.HelpBox(
                        "아틀라스에 포함된 스프라이트는 UV 가 서브영역이라 사용할 수 없습니다. 독립 텍스처로 Import 하세요.",
                        MessageType.Warning);
                }
                if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
                {
                    EditorGUILayout.HelpBox(
                        "Draw Mode = Simple 을 권장합니다. Tiled/Sliced 모드는 타일별 UV 로 인해 회전/스크롤이 의도대로 표시되지 않을 수 있습니다.",
                        MessageType.Info);
                }
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
