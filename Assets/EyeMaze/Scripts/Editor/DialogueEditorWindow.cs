using System.Collections.Generic;
using System.Reflection;
using System.Text;

using UnityEngine;

using UnityEditor;
using UnityEditor.ShortcutManagement;

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
        private const string TEXT_CONTROL = "DialogueEditorText";

        // 에디터 텍스트 상자가 지금 쓰는 편집기. Tab 들여쓰기에서 커서 위치를 읽고 바꾸는 데 쓴다.
        private static readonly FieldInfo ActiveEditorField = typeof(EditorGUI).GetField("activeEditor", BindingFlags.NonPublic | BindingFlags.Static);

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

        /// <summary>실행 취소 기록 한 칸. 유효하게 저장된 모델과 그 CSV 글이다.</summary>
        private class Snapshot
        {
            public DialogueEditorDocument Doc;
            public string Csv;
        }

        // 이 파일의 유효한 편집 순서. _historyIndex가 지금 상태를 가리킨다.
        private readonly List<Snapshot> _history = new List<Snapshot>();
        private int _historyIndex = -1;

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
            HandleHistoryKeys();
            DrawToolbar();
            if (_doc == null)
            {
                EditorGUILayout.HelpBox("편집할 CSV를 위에서 고르세요. 인물·회차 파일(Data/Dialogue/인물_DayN.csv)과 공용 Data/Strings.csv를 지원합니다.", MessageType.Info);
                return;
            }
            Rect body = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f);
            var textRect = new Rect(body.x, body.y, Mathf.Floor(body.width * TEXT_WIDTH_RATIO), body.height);
            var graphRect = new Rect(textRect.xMax + 4f, body.y, body.width - textRect.width - 4f, body.height);
            // 노드 뷰가 먼저 입력을 받아야 텍스트 뷰 위에서 놓은 끌기도 노드 뷰가 끝낼 수 있다. 두 영역은 겹치지 않는다.
            DrawGraphView(graphRect);
            DrawTextView(textRect);
            EditorGUI.DrawRect(new Rect(textRect.xMax + 1f, body.y, 2f, body.height), new Color(0f, 0f, 0f, 0.3f));
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
            HandleTabKey();
            // 입력 상자의 글자는 투명하게 두고, 같은 글자 모양으로 문법별 색을 입힌 글을 그 위에 겹쳐 그린다.
            // 두 글의 글자 위치가 같아야 하므로 같은 스타일에서 글자색과 richText만 다르게 쓴다.
            GUIStyle style = InputStyle();
            float width = textRect.width - 16f;
            float height = Mathf.Max(textRect.height, style.CalcHeight(new GUIContent(_draft), width));
            _textScroll = GUI.BeginScrollView(textRect, _textScroll, new Rect(0f, 0f, width, height), false, true);
            var area = new Rect(0f, 0f, width, height);
            GUI.SetNextControlName(TEXT_CONTROL);
            string next = EditorGUI.TextArea(area, _draft, style);
            if (Event.current.type == EventType.Repaint) HighlightStyle(style).Draw(area, new GUIContent(Highlight(next)), false, false, false, false);
            GUI.EndScrollView();
            SetDraftFromInput(next);

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
        /// 텍스트 입력 상자 스타일을 만든다. 글자는 투명하게 두고 커서와 선택 영역만 보이게 한다.
        /// 입력값은 없으며, 새 스타일을 반환한다.
        /// </summary>
        private static GUIStyle InputStyle()
        {
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true, richText = false };
            Color clear = new Color(0f, 0f, 0f, 0f);
            style.normal.textColor = style.focused.textColor = style.hover.textColor = style.active.textColor = clear;
            style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = clear;
            return style;
        }

        /// <summary>
        /// 문법 색을 입힌 글을 그릴 스타일을 만든다. 입력 상자와 같은 글자 모양과 여백을 쓰고 배경은 그리지 않는다.
        /// input을 사용하며, 새 스타일을 반환한다.
        /// </summary>
        private static GUIStyle HighlightStyle(GUIStyle input)
        {
            var style = new GUIStyle(input) { richText = true };
            style.normal.background = style.focused.background = style.hover.background = style.active.background = null;
            style.normal.textColor = EditorStyles.textArea.normal.textColor;
            return style;
        }

        /// <summary>
        /// 텍스트 뷰의 각 줄에 문법별 색 태그를 붙인다. 라벨은 청록, 선택지는 노랑, Line: 대사는 기본색,
        /// [eye]는 보라, [goto]는 파랑, 오류가 난 줄은 빨강이다. 글자 자체는 바꾸지 않는다.
        /// text를 사용하며, 색 태그를 붙인 글을 반환한다.
        /// </summary>
        private string Highlight(string text)
        {
            bool pro = EditorGUIUtility.isProSkin;
            string label = pro ? "#4FD1C5" : "#007A72";
            string choice = pro ? "#E6C547" : "#8A6A00";
            string eye = pro ? "#C792EA" : "#7A3EA8";
            string go = pro ? "#6CB6FF" : "#1F5FBF";
            string error = pro ? "#FF6B6B" : "#C62828";
            var errorLines = new HashSet<int>();
            foreach (DialogueEditorFormat.Issue i in _issues) if (i.Error && i.Line > 0) errorLines.Add(i.Line);

            var sb = new StringBuilder();
            string[] lines = text.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                if (n > 0) sb.Append('\n');
                string line = lines[n];
                string t = line.TrimStart();
                string color = null;
                if (errorLines.Contains(n + 1)) color = error;
                else if (t.StartsWith(":")) color = label;
                else if (t.StartsWith("*")) color = choice;
                else if (t.StartsWith("[goto")) color = go;
                else if (t.StartsWith("[eye")) color = eye;
                string safe = EscapeRichText(line);
                if (color == null || safe.Length == 0) sb.Append(safe);
                else sb.Append("<color=").Append(color).Append('>').Append(safe).Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 글 안의 꺾쇠가 색 태그로 읽히지 않도록 폭이 없는 문자를 끼워 넣는다. 글자 위치는 바뀌지 않는다.
        /// s를 사용하며, 바꾼 글을 반환한다.
        /// </summary>
        private static string EscapeRichText(string s) => s.IndexOf('<') < 0 ? s : s.Replace("<", "<\u200B");

        /// <summary>
        /// 텍스트 입력 중 Tab과 Shift+Tab을 받아 커서가 있는 줄 맨 앞의 탭 문자를 하나 넣거나 뺀다.
        /// 기본 동작(포커스 이동)은 막는다. 이벤트와 에디터 텍스트 편집기를 사용하며, _draft를 바꾼다.
        /// </summary>
        private void HandleTabKey()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || GUI.GetNameOfFocusedControl() != TEXT_CONTROL) return;
            bool tabKey = e.keyCode == KeyCode.Tab;
            bool tabChar = e.character == '\t' || e.character == (char)25;
            if (!tabKey && !tabChar) return;
            // Tab은 키 이벤트와 글자 이벤트가 따로 오므로 키 이벤트에서만 처리하고 둘 다 막는다.
            if (tabKey) Indent(!e.shift);
            e.Use();
        }

        /// <summary>
        /// 커서가 있는 줄의 들여쓰기를 탭 문자 한 단계 늘리거나 줄인다.
        /// add(늘릴지)를 사용하며, _draft와 텍스트 편집기의 글·커서를 바꾼다.
        /// </summary>
        private void Indent(bool add)
        {
            var editor = ActiveEditorField?.GetValue(null) as TextEditor;
            if (editor == null) return;
            string text = editor.text ?? _draft;
            int cursor = Mathf.Clamp(editor.cursorIndex, 0, text.Length);
            int select = Mathf.Clamp(editor.selectIndex, 0, text.Length);
            int lineStart = cursor == 0 ? 0 : text.LastIndexOf('\n', cursor - 1) + 1;
            if (add)
            {
                text = text.Insert(lineStart, "\t");
                cursor++;
                if (select >= lineStart) select++;
            }
            else
            {
                if (lineStart >= text.Length || text[lineStart] != '\t') return;
                text = text.Remove(lineStart, 1);
                if (cursor > lineStart) cursor--;
                if (select > lineStart) select--;
            }
            editor.text = text;
            editor.cursorIndex = cursor;
            editor.selectIndex = select;
            SetDraftFromInput(text);
            GUI.changed = true;
        }

        /// <summary>
        /// 입력으로 바뀐 텍스트 초안을 받고 검사 대기를 시작한다.
        /// text를 사용하며, _draft, _textPending, _editedAt을 바꾼다.
        /// </summary>
        private void SetDraftFromInput(string text)
        {
            if (text == _draft) return;
            _draft = text;
            _textPending = true;
            _editedAt = EditorApplication.timeSinceStartup;
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
        /// 검사를 통과한 모델을 현재 모델로 삼고 CSV로 저장한다. 내용이 바뀌었으면 실행 취소 기록에 한 칸 더하고 그 뒤의 다시 실행 기록을 지운다.
        /// doc과 csv를 사용하며, _doc, _savedAt, _history를 바꾼다.
        /// </summary>
        private void Commit(DialogueEditorDocument doc, string csv)
        {
            _doc = doc;
            if (DialogueEditorFormat.Save(doc.AssetPath, csv)) _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
            if (_historyIndex >= 0 && _history[_historyIndex].Csv == csv) return;
            if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(new Snapshot { Doc = doc.Clone(), Csv = csv });
            _historyIndex = _history.Count - 1;
        }

        /// <summary>
        /// 실행 취소 기록을 지금 모델 하나로 새로 시작한다. 파일을 열거나 다시 열 때 부른다.
        /// _doc을 사용하며, _history와 _historyIndex를 바꾼다.
        /// </summary>
        private void ResetHistory()
        {
            _history.Clear();
            var rowLines = new List<int>();
            _history.Add(new Snapshot { Doc = _doc.Clone(), Csv = DialogueEditorFormat.WriteCsv(_doc, rowLines) });
            _historyIndex = 0;
        }

        /// <summary>
        /// 창 안에서 Ctrl+Z, Ctrl+Y(Mac은 Cmd)를 받아 실행 취소·다시 실행한다. Unity 단축키가 먼저 받으면 아래 단축키 메서드가 같은 일을 한다.
        /// 이벤트를 사용하며, 처리하면 이벤트를 사용 처리한다.
        /// </summary>
        private void HandleHistoryKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || !(e.control || e.command) || _doc == null) return;
            if (e.keyCode == KeyCode.Z && !e.shift) { UndoEdit(); e.Use(); }
            else if (e.keyCode == KeyCode.Y) { RedoEdit(); e.Use(); }
        }

        [Shortcut("EyeMaze/대사 편집기/실행 취소", typeof(DialogueEditorWindow), KeyCode.Z, ShortcutModifiers.Action)]
        private static void UndoShortcut(ShortcutArguments args) => (args.context as DialogueEditorWindow)?.UndoEdit();

        [Shortcut("EyeMaze/대사 편집기/다시 실행", typeof(DialogueEditorWindow), KeyCode.Y, ShortcutModifiers.Action)]
        private static void RedoShortcut(ShortcutArguments args) => (args.context as DialogueEditorWindow)?.RedoEdit();

        /// <summary>
        /// 실행 취소한다. 검사 전이거나 오류가 있는 텍스트 초안이 있으면 먼저 그 초안만 마지막 정상 모델의 글로 되돌리고,
        /// 아니면 기록의 한 칸 앞 상태로 두 뷰와 CSV를 되돌린다.
        /// _history와 _draft를 사용하며, 모델·텍스트·CSV를 바꾼다.
        /// </summary>
        public void UndoEdit()
        {
            if (_doc == null) return;
            if (_textPending || _draftHasErrors)
            {
                SetDraftText(DialogueEditorFormat.WriteText(_doc));
                _textPending = false;
                _draftHasErrors = false;
                _issues.Clear();
                Repaint();
                return;
            }
            if (_historyIndex <= 0) return;
            RestoreSnapshot(_historyIndex - 1);
        }

        /// <summary>
        /// 다시 실행한다. 기록의 한 칸 뒤 상태가 있으면 두 뷰와 CSV를 그 상태로 바꾼다.
        /// _history를 사용하며, 모델·텍스트·CSV를 바꾼다.
        /// </summary>
        public void RedoEdit()
        {
            if (_doc == null || _textPending || _draftHasErrors || _historyIndex + 1 >= _history.Count) return;
            RestoreSnapshot(_historyIndex + 1);
        }

        /// <summary>
        /// 기록의 한 칸으로 모델·텍스트를 바꾸고 같은 저장·재임포트 경로로 CSV를 다시 쓴다.
        /// index를 사용하며, _historyIndex, _doc, _draft, _issues를 바꾼다.
        /// </summary>
        private void RestoreSnapshot(int index)
        {
            _historyIndex = index;
            DialogueEditorDocument doc = _history[index].Doc.Clone();
            SetDraftText(DialogueEditorFormat.WriteText(doc));
            var issues = new List<DialogueEditorFormat.Issue>();
            string csv = DialogueEditorFormat.Validate(doc, issues);
            _issues.Clear();
            _issues.AddRange(issues);
            _doc = doc;
            if (DialogueEditorFormat.Save(doc.AssetPath, csv)) _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
            Repaint();
        }

        /// <summary>
        /// 텍스트 초안을 코드에서 바꾼다. 입력 중인 텍스트 상자가 새 글을 보이도록 포커스를 푼다.
        /// text를 사용하며, _draft를 바꾼다.
        /// </summary>
        private void SetDraftText(string text)
        {
            _draft = text;
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
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
            SetDraftText(text);
            _issues.AddRange(issues);
            Commit(parsed, csv);
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
            foreach (string folder in new[] { DialogueData.DialogueFolder })
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
            ResetHistory();
            _openPath = path;
            _selected = System.Array.IndexOf(_paths, path);
            GUI.FocusControl(null);
            Repaint();
        }
    }
}
