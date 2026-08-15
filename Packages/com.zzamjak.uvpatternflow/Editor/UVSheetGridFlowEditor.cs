using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CAT.Effects
{
    /// <summary>
    /// UVSheetGridFlow 인스펙터.
    /// - 시트/그리드/스크롤 설정 필드
    /// - 텍스처 미지정, 마스크 병용 등 설정 경고
    /// - 에디터 미리보기 / 플레이 모드 제어 버튼
    /// </summary>
    [CustomEditor(typeof(UVSheetGridFlow))]
    [CanEditMultipleObjects]
    public class UVSheetGridFlowEditor : Editor
    {
        private SerializedProperty _sheetTiles;
        private SerializedProperty _gridCount;
        private SerializedProperty _cellGap;
        private SerializedProperty _scrollSpeed;
        private SerializedProperty _switchDuration;
        private SerializedProperty _frameInset;
        private SerializedProperty _playOnEnable;

        private bool _editorPreviewRunning;
        private double _editorPreviewLastTime;

        private void OnEnable()
        {
            _sheetTiles     = serializedObject.FindProperty("_sheetTiles");
            _gridCount      = serializedObject.FindProperty("_gridCount");
            _cellGap        = serializedObject.FindProperty("_cellGap");
            _scrollSpeed    = serializedObject.FindProperty("_scrollSpeed");
            _switchDuration = serializedObject.FindProperty("_switchDuration");
            _frameInset     = serializedObject.FindProperty("_frameInset");
            _playOnEnable   = serializedObject.FindProperty("_playOnEnable");
        }

        private void OnDisable()
        {
            StopEditorPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("스프라이트 시트", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sheetTiles, new GUIContent("시트 분할 (X×Y)", "예: 3×3 = 9프레임. 파티클 Texture Sheet Animation 과 동일 방식"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("그리드", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_gridCount, new GUIContent("그리드 셀 수 (X×Y)"));
            EditorGUILayout.PropertyField(_cellGap,   new GUIContent("셀 간격 (비율)", "셀 크기 대비 0~0.9. 간격 부분은 투명"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("애니메이션", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_scrollSpeed,    new GUIContent("스크롤 속도 (셀/초)"));
            EditorGUILayout.PropertyField(_switchDuration, new GUIContent("스위칭 주기 (초)"));
            EditorGUILayout.PropertyField(_frameInset,     new GUIContent("프레임 인셋", "인접 프레임 블리딩 방지"));

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(_playOnEnable, new GUIContent("활성화 시 자동 재생"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6);

            var flow = (UVSheetGridFlow)target;
            DrawWarnings(flow);

            EditorGUILayout.Space(6);

            if (Application.isPlaying)
                DrawPlayModeButtons(flow);
            else
                DrawEditModePreview(flow);
        }

        private void DrawWarnings(UVSheetGridFlow flow)
        {
            var rawImage = flow.GetComponent<RawImage>();
            var image = rawImage != null ? null : flow.GetComponent<Image>();

            if (rawImage == null && image == null)
            {
                EditorGUILayout.HelpBox(
                    "RawImage 또는 Image 컴포넌트가 필요합니다.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("모드", rawImage != null ? "UI (RawImage)" : "UI (Image)");

            if (rawImage != null && rawImage.texture == null)
            {
                EditorGUILayout.HelpBox(
                    "RawImage 의 Texture 에 스프라이트 시트를 지정하세요.",
                    MessageType.Warning);
            }
            if (image != null && image.sprite == null)
            {
                EditorGUILayout.HelpBox(
                    "Image 의 Source Image 에 스프라이트 시트를 지정하세요.",
                    MessageType.Warning);
            }

            if (rawImage != null && rawImage.uvRect != new Rect(0f, 0f, 1f, 1f))
            {
                EditorGUILayout.HelpBox(
                    "RawImage 의 UV Rect 는 기본값(0,0,1,1)을 권장합니다. 그리드/스크롤은 이 컴포넌트가 제어합니다.",
                    MessageType.Info);
            }

            // Image 모드 전용 검사
            if (image != null)
            {
                if (image.type != Image.Type.Simple)
                {
                    EditorGUILayout.HelpBox(
                        "Image Type = Simple 이어야 합니다. Sliced/Tiled/Filled 는 패치별 UV 로 인해 그리드가 의도대로 표시되지 않습니다.",
                        MessageType.Warning);
                }
                if (image.useSpriteMesh)
                {
                    EditorGUILayout.HelpBox(
                        "Use Sprite Mesh 가 켜져 있으면 알파 외곽선 메시가 그리드를 마스킹합니다. 옵션을 끄세요.",
                        MessageType.Warning);
                }

                Sprite sprite = image.sprite;
                if (sprite != null && sprite.packed)
                {
                    if (sprite.packingMode != SpritePackingMode.Rectangle)
                    {
                        EditorGUILayout.HelpBox(
                            "아틀라스가 Tight Packing 이면 이웃 스프라이트가 시트 영역을 침범합니다. SpriteAtlas 의 Tight Packing 을 끄세요.",
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
        }

        private void DrawPlayModeButtons(UVSheetGridFlow flow)
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

        private void DrawEditModePreview(UVSheetGridFlow flow)
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

                if (GUILayout.Button("초기화", GUILayout.Width(60)))
                {
                    flow.Stop();
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
            (target as UVSheetGridFlow)?.Stop();
            SceneView.RepaintAll();
        }

        private void EditorPreviewUpdate()
        {
            if (!_editorPreviewRunning) return;

            var flow = target as UVSheetGridFlow;
            if (flow == null) { StopEditorPreview(); return; }

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _editorPreviewLastTime);
            _editorPreviewLastTime = now;

            flow.EditorAdvance(dt);
            SceneView.RepaintAll();
            Repaint();
        }
    }
}
