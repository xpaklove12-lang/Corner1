using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 대사 CSV를 SUDS식 텍스트와 노드 뷰에서 함께 편집하는 창 (EyeMaze/대사 편집기).
    /// CSV가 유일한 원본이며, 창은 파일을 열 때마다 공통 모델을 새로 만든다.
    /// </summary>
    public class DialogueEditorWindow : EditorWindow
    {
        private const float TEXT_WIDTH_RATIO = 0.42f;
        // 입력이 이만큼(초) 멈추면 텍스트를 검사한다.
        private const double TEXT_APPLY_DELAY = 0.75;

        // 스크립트가 다시 컴파일돼도 열던 파일을 다시 열려고 경로만 저장한다.
        [SerializeField] private string _openPath = "";

        private string[] _paths = new string[0];
        private int _selected = -1;
        private DialogueEditorDocument _doc;
        private string _draft = "";
        private Vector2 _textScroll;
        private Vector2 _issueScroll;
        private readonly List<DialogueEditorFormat.Issue> _issues = new List<DialogueEditorFormat.Issue>();
        private bool _textPending;
        private double _editedAt;
        private bool _draftHasErrors;
        private string _savedAt = "";
        private readonly DialogueEditorGraph _graph = new DialogueEditorGraph();

        [MenuItem("EyeMaze/대사 편집기")]
        private static void Open()
        {
            var w = GetWindow<DialogueEditorWindow>("대사 편집기");
            string selectedPath = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            w.RefreshPaths();
            if (selectedPath != null && DialogueEditorFormat.TryGetKind(selectedPath, out _)) w.OpenPath(selectedPath);
        }

        void OnEnable()
        {
            RefreshPaths();
            if (!string.IsNullOrEmpty(_openPath)) OpenPath(_openPath);
        }

        void Update()
        {
            if (_textPending && EditorApplication.timeSinceStartup - _editedAt >= TEXT_APPLY_DELAY)
            {
                _textPending = false;
                ApplyDraft();
            }
        }

        void OnGUI()
        {
            DrawToolbar();
            if (_doc == null)
            {
                EditorGUILayout.HelpBox("편집할 CSV를 위에서 고르세요. Data/Dialogue, Data/Talk, Data/Ending, Data/Strings.csv를 지원합니다.", MessageType.Info);
                return;
            }
            Rect body = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f);
            var textRect = new Rect(body.x, body.y, Mathf.Floor(body.width * TEXT_WIDTH_RATIO), body.height);
            var graphRect = new Rect(textRect.xMax + 4f, body.y, body.width - textRect.width - 4f, body.height);
            DrawTextView(textRect);
            EditorGUI.DrawRect(new Rect(textRect.xMax + 1f, body.y, 2f, body.height), new Color(0f, 0f, 0f, 0.3f));
            DrawGraphView(graphRect);
        }

        /// <summary>
        /// 파일 선택 줄과 다시 열기 버튼을 그린다.
        /// _paths와 _selected를 사용하며, 고른 파일을 연다.
        /// </summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var labels = new string[_paths.Length];
                for (int i = 0; i < _paths.Length; i++) labels[i] = _paths[i].Substring(DialogueData.DataFolder.Length + 1);
                int next = EditorGUILayout.Popup(_selected, labels, EditorStyles.toolbarPopup, GUILayout.Width(220f));
                if (next != _selected && next >= 0) OpenPath(_paths[next]);
                if (GUILayout.Button("다시 열기", EditorStyles.toolbarButton, GUILayout.Width(70f)) && _doc != null) OpenPath(_doc.AssetPath);
                if (GUILayout.Button("목록 새로고침", EditorStyles.toolbarButton, GUILayout.Width(90f))) RefreshPaths();
                GUILayout.FlexibleSpace();
                GUILayout.Label(StatusText(), EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// 도구 막대 오른쪽에 보일 상태 글을 만든다.
        /// _issues를 사용하며, 상태 글을 반환한다.
        /// </summary>
        private string StatusText()
        {
            if (_doc == null) return "";
            int errors = 0;
            foreach (DialogueEditorFormat.Issue i in _issues) if (i.Error) errors++;
            if (errors > 0) return $"오류 {errors}개 · 저장 안 함";
            return _savedAt.Length > 0 ? $"정상 · {_savedAt} 저장" : "정상";
        }

        /// <summary>
        /// 텍스트 뷰와 그 아래 오류 목록을 그린다.
        /// rect와 _draft를 사용하며, 입력한 글을 _draft에 쓴다.
        /// </summary>
        private void DrawTextView(Rect rect)
        {
            float issueHeight = _issues.Count > 0 ? Mathf.Min(120f, 22f + _issues.Count * 18f) : 0f;
            var textRect = new Rect(rect.x, rect.y, rect.width, rect.height - issueHeight);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true, richText = false };
            // 줄바꿈된 높이를 직접 계산해 가로 스크롤 없이 창 너비에 맞춘다.
            float width = textRect.width - 16f;
            float height = Mathf.Max(textRect.height, style.CalcHeight(new GUIContent(_draft), width));
            _textScroll = GUI.BeginScrollView(textRect, _textScroll, new Rect(0f, 0f, width, height), false, true);
            string next = EditorGUI.TextArea(new Rect(0f, 0f, width, height), _draft, style);
            GUI.EndScrollView();
            if (next != _draft)
            {
                _draft = next;
                _textPending = true;
                _editedAt = EditorApplication.timeSinceStartup;
            }

            if (issueHeight <= 0f) return;
            var listRect = new Rect(rect.x, textRect.yMax, rect.width, issueHeight);
            EditorGUI.DrawRect(listRect, new Color(0.12f, 0.12f, 0.13f));
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            float labelWidth = listRect.width - 24f;
            var labels = new List<string>();
            float contentHeight = 4f;
            foreach (DialogueEditorFormat.Issue i in _issues)
            {
                string where = i.Line > 0 ? $"{i.Line}줄: " : "";
                string label = (i.Error ? "오류 " : "경고 ") + where + i.Message;
                labels.Add(label);
                contentHeight += labelStyle.CalcHeight(new GUIContent(label), labelWidth) + 2f;
            }
            _issueScroll = GUI.BeginScrollView(listRect, _issueScroll, new Rect(0f, 0f, labelWidth, contentHeight));
            float y = 4f;
            for (int k = 0; k < labels.Count; k++)
            {
                labelStyle.normal.textColor = _issues[k].Error ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.8f, 0.3f);
                float h = labelStyle.CalcHeight(new GUIContent(labels[k]), labelWidth);
                GUI.Label(new Rect(6f, y, labelWidth, h), labels[k], labelStyle);
                y += h + 2f;
            }
            GUI.EndScrollView();
        }

        /// <summary>
        /// 노드 뷰를 그린다.
        /// rect와 _doc을 사용한다.
        /// </summary>
        private void DrawGraphView(Rect rect)
        {
            string notice = _draftHasErrors ? "텍스트에 오류가 있어 마지막 정상 상태를 보여줍니다. 오류를 고치기 전에는 노드를 편집할 수 없습니다." : null;
            DialogueEditorDocument changed = _graph.Draw(rect, _doc, !_draftHasErrors && !_textPending, notice);
            if (Event.current.type == EventType.Used) Repaint();
            if (changed != null) ApplyGraphEdit(changed);
        }

        /// <summary>
        /// 텍스트 초안을 해석·검사해 문제가 없으면 공통 모델을 바꾸고 CSV에 저장한다.
        /// 문제가 있으면 마지막 정상 모델과 CSV를 그대로 두고 초안도 저장하지 않는다.
        /// _draft를 사용하며, _doc, _issues, _draftHasErrors를 바꾼다.
        /// </summary>
        private void ApplyDraft()
        {
            var issues = new List<DialogueEditorFormat.Issue>();
            DialogueEditorDocument parsed = DialogueEditorFormat.ParseText(_draft, _doc.Kind, _doc.AssetPath, issues);
            string csv = HasError(issues) ? null : DialogueEditorFormat.Validate(parsed, issues);
            _issues.Clear();
            _issues.AddRange(issues);
            _draftHasErrors = HasError(issues);
            if (!_draftHasErrors) Commit(parsed, csv);
            Repaint();
        }

        /// <summary>
        /// 검사를 통과한 모델을 현재 모델로 삼고 CSV로 저장한다.
        /// doc과 csv를 사용하며, _doc과 _savedAt을 바꾼다.
        /// </summary>
        private void Commit(DialogueEditorDocument doc, string csv)
        {
            _doc = doc;
            if (DialogueEditorFormat.Save(doc.AssetPath, csv)) _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
        }

        /// <summary>
        /// 노드 뷰에서 바뀐 모델을 검사해 통과하면 텍스트로 다시 써서 두 뷰를 함께 바꾸고 CSV에 저장한다.
        /// 통과하지 못하면 모델, 텍스트, CSV를 그대로 두고 이유만 보여준다.
        /// changed를 사용하며, _doc, _draft, _issues를 바꾼다.
        /// </summary>
        private void ApplyGraphEdit(DialogueEditorDocument changed)
        {
            string text = DialogueEditorFormat.WriteText(changed);
            var issues = new List<DialogueEditorFormat.Issue>();
            DialogueEditorDocument parsed = DialogueEditorFormat.ParseText(text, changed.Kind, changed.AssetPath, issues);
            string csv = HasError(issues) ? null : DialogueEditorFormat.Validate(parsed, issues);
            _issues.Clear();
            if (HasError(issues))
            {
                // 거절된 편집의 줄 번호는 화면에 없는 글을 가리키므로 빼고 보여준다.
                foreach (DialogueEditorFormat.Issue i in issues)
                    if (i.Error) _issues.Add(new DialogueEditorFormat.Issue { Message = "노드 편집을 되돌렸다: " + i.Message });
                Repaint();
                return;
            }
            _draft = text;
            _issues.AddRange(issues);
            Commit(parsed, csv);
            // 입력 중인 텍스트 상자가 새 글을 보이도록 포커스를 푼다.
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        /// <summary>
        /// 검사 결과에 오류가 하나라도 있는지 본다.
        /// issues를 사용하며, 오류가 있으면 true를 반환한다.
        /// </summary>
        private static bool HasError(List<DialogueEditorFormat.Issue> issues)
        {
            foreach (DialogueEditorFormat.Issue i in issues) if (i.Error) return true;
            return false;
        }

        /// <summary>
        /// 지원하는 CSV 목록을 다시 찾는다.
        /// 입력값은 없으며, _paths와 _selected를 바꾼다.
        /// </summary>
        private void RefreshPaths()
        {
            var list = new List<string>();
            foreach (string folder in new[] { DialogueData.DialogueFolder, DialogueData.TalkFolder, DialogueData.EndingFolder })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                var found = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { folder }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (DialogueEditorFormat.TryGetKind(p, out _)) found.Add(p);
                }
                found.Sort(System.StringComparer.Ordinal);
                list.AddRange(found);
            }
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(Strings.DefaultPath) != null) list.Add(Strings.DefaultPath);
            _paths = list.ToArray();
            _selected = _doc != null ? System.Array.IndexOf(_paths, _doc.AssetPath) : -1;
        }

        /// <summary>
        /// CSV를 열어 공통 모델과 텍스트 초안을 새로 만든다.
        /// path를 사용하며, _doc, _draft, _issues를 바꾼다.
        /// </summary>
        private void OpenPath(string path)
        {
            if (!DialogueEditorFormat.TryGetKind(path, out DialogueEditorKind kind)) return;
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) return;
            var errors = new List<string>();
            if (_doc == null || _doc.AssetPath != path) _graph.Reset();
            _doc = DialogueEditorFormat.ReadCsv(asset, kind, path, errors);
            _draft = DialogueEditorFormat.WriteText(_doc);
            _issues.Clear();
            _textPending = false;
            _draftHasErrors = false;
            _savedAt = "";
            foreach (string e in errors) _issues.Add(new DialogueEditorFormat.Issue { Message = e });
            _openPath = path;
            _selected = System.Array.IndexOf(_paths, path);
            GUI.FocusControl(null);
            Repaint();
        }
    }
}
