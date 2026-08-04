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
            if (rawImage == null) return;

            if (rawImage.texture == null)
            {
                EditorGUILayout.HelpBox(
                    "RawImage 의 Texture 에 스프라이트 시트를 지정하세요.",
                    MessageType.Warning);
            }

            // 마스크 병용 경고 (전용 셰이더는 클리핑/스텐실 미지원)
            if (flow.GetComponentInParent<Mask>() != null || flow.GetComponentInParent<RectMask2D>() != null)
            {
                EditorGUILayout.HelpBox(
                    "UVSheetGridFlow 셰이더는 Mask / RectMask2D / SoftMask 계열을 지원하지 않습니다. 마스크 밖에서 사용하세요.",
                    MessageType.Warning);
            }

            Rect uvRect = rawImage.uvRect;
            if (uvRect != new Rect(0f, 0f, 1f, 1f))
            {
                EditorGUILayout.HelpBox(
                    "RawImage 의 UV Rect 는 기본값(0,0,1,1)을 권장합니다. 그리드/스크롤은 이 컴포넌트가 제어합니다.",
                    MessageType.Info);
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
