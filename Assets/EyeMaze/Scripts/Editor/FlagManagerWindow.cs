using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 플래그 관리자 창 (메뉴 EyeMaze/플래그 관리자).
    /// Flags.csv의 플래그마다 켜짐 여부, 켜는 곳, 쓰는 곳을 보여주고, 체크로 바로 켜고 끌 수 있다 (Play 중 테스트용).
    /// 위쪽에는 기획 데이터 검사 결과가 나온다.
    /// </summary>
    public class FlagManagerWindow : EditorWindow
    {
        private List<DialogueData.FlagDef> _defs = new List<DialogueData.FlagDef>();
        private Dictionary<string, List<DialogueData.Ref>> _setters = new Dictionary<string, List<DialogueData.Ref>>();
        private Dictionary<string, List<DialogueData.Ref>> _users = new Dictionary<string, List<DialogueData.Ref>>();
        private List<DialogueData.Issue> _issues = new List<DialogueData.Issue>();
        private string _search = "";
        private bool _onlyOn;
        private bool _showIssues = true;
        private Vector2 _scroll;

        [MenuItem("EyeMaze/플래그 관리자")]
        public static void Open() => GetWindow<FlagManagerWindow>("플래그 관리자").Reload();

        /// <summary>
        /// 열려 있는 플래그 관리자 창을 모두 다시 읽고 다시 그린다.
        /// </summary>
        public static void RepaintAll()
        {
            foreach (FlagManagerWindow w in Resources.FindObjectsOfTypeAll<FlagManagerWindow>()) { w.Reload(); w.Repaint(); }
        }

        void OnEnable()
        {
            Flags.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayMode;
            Reload();
        }

        void OnDisable()
        {
            Flags.Changed -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayMode;
        }

        // Play 중에는 신뢰 같은 수치가 바뀌면 계산 플래그도 바뀌므로 자주 다시 그린다.
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

        void OnHierarchyChange() { if (!Application.isPlaying) Reload(); }

        private void OnPlayMode(PlayModeStateChange _) { Reload(); Repaint(); }

        /// <summary>
        /// Flags.csv와 대화 CSV, 씬을 다시 읽어 표와 검사 결과를 만든다.
        /// </summary>
        private void Reload()
        {
            var errors = new List<string>();
            _defs = DialogueData.LoadFlags(errors);
            _setters = new Dictionary<string, List<DialogueData.Ref>>();
            _users = new Dictionary<string, List<DialogueData.Ref>>();
            DialogueData.CollectRefs(DialogueData.LoadDialogues(), _setters, _users);
            _issues = DialogueData.Validate();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(70))) Reload();
                if (GUILayout.Button("Flags.csv 열기", EditorStyles.toolbarButton, GUILayout.Width(95)))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(DialogueData.FlagsPath));
                if (GUILayout.Button("Data 폴더", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    EditorUtility.RevealInFinder(DialogueData.FlagsPath);
                if (GUILayout.Button("씬 상태로 맞추기", EditorStyles.toolbarButton, GUILayout.Width(105))) Flags.ResetAll();
                GUILayout.FlexibleSpace();
                _onlyOn = GUILayout.Toggle(_onlyOn, "켜진 것만", EditorStyles.toolbarButton, GUILayout.Width(70));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }

            int errorCount = _issues.Count(i => i.Error);
            int warnCount = _issues.Count - errorCount;
            _showIssues = EditorGUILayout.Foldout(_showIssues, $"검사 결과 — 오류 {errorCount} · 경고 {warnCount}", true);
            if (_showIssues)
            {
                if (_issues.Count == 0) EditorGUILayout.HelpBox("문제 없음", MessageType.Info);
                foreach (DialogueData.Issue i in _issues)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(i.Text, i.Error ? MessageType.Error : MessageType.Warning);
                        if (i.Context != null && GUILayout.Button("가기", GUILayout.Width(40), GUILayout.Height(38))) Ping(i.Context);
                    }
                }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("켜짐", EditorStyles.miniBoldLabel, GUILayout.Width(32));
                GUILayout.Label("id / 설명", EditorStyles.miniBoldLabel, GUILayout.Width(220));
                GUILayout.Label("켜는 곳", EditorStyles.miniBoldLabel, GUILayout.Width(200));
                GUILayout.Label("쓰는 곳 (need)", EditorStyles.miniBoldLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (DialogueData.FlagDef d in _defs)
            {
                if (string.IsNullOrEmpty(d.Id)) continue;
                bool on = Flags.IsOn(d.Id);
                if (_onlyOn && !on) continue;
                if (_search.Length > 0 && d.Id.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0
                    && (d.Description ?? "").IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    bool computed = !string.IsNullOrEmpty(d.Condition);
                    using (new EditorGUI.DisabledScope(computed))
                    {
                        bool next = EditorGUILayout.Toggle(on, GUILayout.Width(32));
                        if (next != on && !computed) Flags.Set(d.Id, next);
                    }
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(220)))
                    {
                        EditorGUILayout.SelectableLabel(d.Id, EditorStyles.boldLabel, GUILayout.Height(16));
                        GUILayout.Label(d.Description, EditorStyles.wordWrappedMiniLabel);
                    }
                    if (!string.IsNullOrEmpty(d.Condition))
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(200)))
                            GUILayout.Label("계산: " + d.Condition, EditorStyles.wordWrappedMiniLabel);
                    else DrawRefs(_setters, d.Id, 200, "없음 — 열리지 않는다");
                    DrawRefs(_users, d.Id, -1, "아무 데서도 안 씀");
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("체크는 지금 상태만 바꾼다 (테스트용, 저장되지 않음). 플래그를 늘리려면 Flags.csv에 한 줄 추가한다.\n계산 플래그(condition)는 체크할 수 없고, 신뢰 등 지금 값으로 자동으로 켜지고 꺼진다.", MessageType.None);
        }

        /// <summary>
        /// 플래그를 켜는 곳/쓰는 곳 목록을 버튼으로 그린다. 누르면 그 오브젝트나 CSV로 간다.
        /// </summary>
        private static void DrawRefs(Dictionary<string, List<DialogueData.Ref>> refs, string id, float width, string empty)
        {
            using (width > 0 ? new EditorGUILayout.VerticalScope(GUILayout.Width(width)) : new EditorGUILayout.VerticalScope())
            {
                if (!refs.TryGetValue(id, out List<DialogueData.Ref> list) || list.Count == 0)
                {
                    GUILayout.Label(empty, EditorStyles.centeredGreyMiniLabel);
                    return;
                }
                foreach (DialogueData.Ref r in list)
                    if (GUILayout.Button(r.Label, EditorStyles.miniButton)) Ping(r.Target);
            }
        }

        private static void Ping(Object o)
        {
            if (o == null) return;
            Selection.activeObject = o;
            EditorGUIUtility.PingObject(o);
        }
    }
}
