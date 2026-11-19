using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    public class PlayModeValuesWindow : EditorWindow
    {
        [SerializeField] private bool _includeChildren;
        [SerializeField] private Vector2 _scroll;
        private readonly List<Component> _components = new List<Component>();
        private readonly HashSet<int> _unchecked = new HashSet<int>();

        /// <summary>입력 없이 플레이 값 보관 창을 열어 선택한 컴포넌트와 세션 보관 목록을 표시한다.</summary>
        [MenuItem("EyeMaze/플레이 값 보관")]
        public static void Open()
        {
            var window = GetWindow<PlayModeValuesWindow>("플레이 값 보관");
            window.minSize = new Vector2(480f, 360f);
            window.Show();
        }

        void OnEnable()
        {
            Selection.selectionChanged += RefreshSelection;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            RefreshSelection();
        }

        void OnDisable()
        {
            Selection.selectionChanged -= RefreshSelection;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        /// <summary>state의 Play Mode 전환이 완료되면 컴포넌트 목록을 갱신한다.</summary>
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) RefreshSelection();
        }

        /// <summary>현재 선택 오브젝트와 자식 포함 설정을 읽어 중복 없는 컴포넌트 목록을 갱신한다.</summary>
        private void RefreshSelection()
        {
            var previous = new HashSet<int>();
            foreach (Component component in _components) if (component != null) previous.Add(component.GetInstanceID());
            _components.Clear();
            var added = new HashSet<int>();
            foreach (GameObject go in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
                Component[] components = _includeChildren ? go.GetComponentsInChildren<Component>(true) : go.GetComponents<Component>();
                foreach (Component component in components)
                {
                    if (component == null || !added.Add(component.GetInstanceID())) continue;
                    _components.Add(component);
                    if (component is Transform && !previous.Contains(component.GetInstanceID())) _unchecked.Add(component.GetInstanceID());
                }
            }
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Play Mode에서 대상 체크 → 선택 값 보관 → Play Mode 종료 → 저장 값 일괄 적용 → Ctrl+S\n여러 번 보관하면 누적되며 같은 컴포넌트는 최신 값으로 교체됩니다. 보관본은 현재 에디터 세션 동안 유지됩니다.", MessageType.Info);
            bool include = EditorGUILayout.ToggleLeft("선택 오브젝트의 자식 포함", _includeChildren);
            if (include != _includeChildren) { _includeChildren = include; RefreshSelection(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("모두 선택")) foreach (Component c in _components) if (c != null) _unchecked.Remove(c.GetInstanceID());
                if (GUILayout.Button("모두 해제")) foreach (Component c in _components) if (c != null) _unchecked.Add(c.GetInstanceID());
                if (GUILayout.Button("목록 새로고침")) RefreshSelection();
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("현재 선택 대상", EditorStyles.boldLabel);
            if (_components.Count == 0) EditorGUILayout.LabelField("Hierarchy에서 오브젝트를 선택하세요.");
            var selected = new List<Component>();
            foreach (Component component in _components)
            {
                if (component == null) continue;
                int id = component.GetInstanceID();
                bool check = EditorGUILayout.ToggleLeft(PlayModeValuesStore.GetLabel(component), !_unchecked.Contains(id));
                if (check) { _unchecked.Remove(id); selected.Add(component); }
                else _unchecked.Add(id);
            }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || EditorApplication.isCompiling || selected.Count == 0))
                if (GUILayout.Button($"선택 값 보관 ({selected.Count}개)", GUILayout.Height(28f))) PlayModeValuesStore.Capture(selected);

            PlayModeValuesStore.Snapshot snapshot = PlayModeValuesStore.Load();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"보관 목록 ({snapshot.Entries.Count}개)", EditorStyles.boldLabel);
            foreach (PlayModeValuesStore.Entry entry in snapshot.Entries)
                EditorGUILayout.LabelField(entry.Label + $" · {entry.Fields.Count}개 값", EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || snapshot.Entries.Count == 0))
                    if (GUILayout.Button("저장 값 일괄 적용", GUILayout.Height(28f))) PlayModeValuesStore.Apply();
                using (new EditorGUI.DisabledScope(snapshot.Entries.Count == 0))
                    if (GUILayout.Button("전체 비우기", GUILayout.Width(95f), GUILayout.Height(28f))) PlayModeValuesStore.Clear();
            }
            EditorGUILayout.HelpBox("체크한 컴포넌트의 지원되는 Inspector 설정을 보관합니다. 실행 중 상태도 직렬화 필드이면 포함됩니다. 오브젝트 참조·커브·그라데이션은 제외됩니다. Transform은 기본 선택에서 제외됩니다.", MessageType.None);
            string report = PlayModeValuesStore.Load().Report;
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
