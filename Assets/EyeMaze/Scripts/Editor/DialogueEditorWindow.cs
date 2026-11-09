using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 대사 CSV를 SUDS식 텍스트와 노드 뷰에서 함께 편집하는 창 (EyeMaze/대사 편집기).
    /// CSV가 유일한 원본이며, 창은 파일을 열 때마다 공통 모델을 새로 만든다.
    /// </summary>
    public class DialogueEditorWindow : EditorWindow
    {
        private const float DEFAULT_SPLIT = 0.42f;
        private const float MIN_VIEW_WIDTH = 250f;
        private const float SPLIT_HANDLE = 6f;
        private const string SPLIT_PREF = "EyeMaze.DialogueEditor.SplitRatio";
        private const string SIZE_PREF = "EyeMaze.DialogueEditor.Size.";
        // 입력이 이만큼(초) 멈추면 텍스트를 검사한다.
        private const double TEXT_APPLY_DELAY = 0.75;
        private const string TEXT_CONTROL = "DialogueEditorText";

        // 에디터 텍스트 상자가 지금 쓰는 편집기. Tab 들여쓰기에서 커서 위치를 읽고 바꾸는 데 쓴다.
        private static readonly FieldInfo ActiveEditorField = typeof(EditorGUI).GetField("activeEditor", BindingFlags.NonPublic | BindingFlags.Static);
        // 에디터 텍스트 상자는 처음 클릭해 포커스를 줄 때 글 전체를 선택한다. 문서 전체가 한 번의 입력으로 바뀌지 않도록 끈다.
        // 에디터 텍스트 상자들이 함께 쓰는 편집기. 초안을 코드에서 바꿀 때 예전 글을 들고 편집 중으로 남지 않게 비운다.
        private static readonly PropertyInfo RecycledEditorProperty = typeof(EditorGUI).GetProperty("s_RecycledEditor", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo SelectAllOnMouseUpField = typeof(EditorGUI).GetField("s_SelectAllOnMouseUp", BindingFlags.NonPublic | BindingFlags.Static);

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
            public string FlagsCsv;
            /// <summary>이 상태로 오게 한 편집이 그룹 ID 변경이면 그 이전·새 그룹 키. 실행 취소·다시 실행 때 크기 설정 키를 옮긴다.</summary>
            public string RenamedFrom;
            public string RenamedTo;
        }

        // 이 파일의 유효한 편집 순서. _historyIndex가 지금 상태를 가리킨다.
        private readonly List<Snapshot> _history = new List<Snapshot>();
        private int _historyIndex = -1;

        private float _split = DEFAULT_SPLIT;
        private bool _splitDragging;
        // 노드 선택과 맞춰 텍스트 뷰에서 강조할 줄과, 다음 그리기에서 그 줄로 스크롤할지
        private int _highlightLine = -1;
        private bool _scrollToHighlight;

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
            _split = EditorPrefs.GetFloat(SPLIT_PREF, DEFAULT_SPLIT);
            _graph.SizeLoader = LoadNodeSize;
            _graph.SizeSaver = SaveNodeSize;
            _graph.FragmentTextSaver = SaveFragmentText;
            _graph.FragmentCreator = CreateFragment;
            _graph.FlagCreator = AddFlag;
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
            float textWidth = TextWidth(body.width);
            var handle = new Rect(body.x + textWidth - 1f, body.y, SPLIT_HANDLE, body.height);
            HandleSplit(handle, body);
            textWidth = TextWidth(body.width);
            var textRect = new Rect(body.x, body.y, textWidth, body.height);
            var graphRect = new Rect(textRect.xMax + 4f, body.y, body.width - textRect.width - 4f, body.height);
            // 노드 뷰가 먼저 입력을 받아야 텍스트 뷰 위에서 놓은 끌기도 노드 뷰가 끝낼 수 있다. 두 영역은 겹치지 않는다.
            DrawGraphView(graphRect);
            DrawTextView(textRect);
            EditorGUI.DrawRect(new Rect(textRect.xMax + 1f, body.y, 2f, body.height), _splitDragging ? new Color(0.35f, 0.6f, 1f, 0.8f) : new Color(0f, 0f, 0f, 0.3f));
        }

        /// <summary>
        /// 텍스트 뷰 너비를 정한다. 창이 500px 이상이면 두 뷰가 각각 250px 이상인 범위에서 저장된 비율을 따르고, 더 좁으면 반씩 나눈다.
        /// width를 사용하며, 텍스트 뷰 너비를 반환한다.
        /// </summary>
        private float TextWidth(float width)
        {
            if (width < MIN_VIEW_WIDTH * 2f) return Mathf.Floor(width * 0.5f);
            return Mathf.Floor(Mathf.Clamp(width * _split, MIN_VIEW_WIDTH, width - MIN_VIEW_WIDTH - 4f));
        }

        /// <summary>
        /// 두 뷰 사이 분할선을 끌어 너비 비율을 바꾸고, 놓을 때 에디터 설정에 저장한다.
        /// handle과 body를 사용하며, _split을 바꾼다.
        /// </summary>
        private void HandleSplit(Rect handle, Rect body)
        {
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !handle.Contains(e.mousePosition)) return;
                    GUIUtility.hotControl = id;
                    _splitDragging = true;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) return;
                    if (body.width >= MIN_VIEW_WIDTH * 2f) _split = Mathf.Clamp01((e.mousePosition.x - body.x) / body.width);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) return;
                    GUIUtility.hotControl = 0;
                    _splitDragging = false;
                    // 저장할 때는 실제로 보이는 너비의 비율로 맞춘다.
                    if (body.width >= MIN_VIEW_WIDTH * 2f) _split = TextWidth(body.width) / body.width;
                    EditorPrefs.SetFloat(SPLIT_PREF, _split);
                    e.Use();
                    break;
            }
        }

        /// <summary>
        /// 노드 크기 설정 키를 만든다. 파일 GUID와 그룹 키(section/actor/id)로 나눈다.
        /// groupKey를 사용하며, EditorPrefs 키를 반환한다.
        /// </summary>
        private string SizeKey(string groupKey) => SIZE_PREF + AssetDatabase.AssetPathToGUID(_doc != null ? _doc.AssetPath : "") + "." + groupKey;

        /// <summary>
        /// 저장된 노드 크기를 읽는다.
        /// groupKey를 사용하며, 크기 또는 (없으면) null을 반환한다.
        /// </summary>
        private Vector2? LoadNodeSize(string groupKey)
        {
            string v = EditorPrefs.GetString(SizeKey(groupKey), "");
            string[] p = v.Split(',');
            if (p.Length != 2) return null;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(p[0], System.Globalization.NumberStyles.Float, inv, out float w) || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out float h)) return null;
            return new Vector2(w, h);
        }

        /// <summary>
        /// 노드 크기를 저장한다.
        /// groupKey와 size를 사용하며, EditorPrefs에 쓴다.
        /// </summary>
        private void SaveNodeSize(string groupKey, Vector2 size)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            EditorPrefs.SetString(SizeKey(groupKey), size.x.ToString("0.##", inv) + "," + size.y.ToString("0.##", inv));
        }

        /// <summary>
        /// ID를 바꾼 그룹의 크기 설정을 새 키로 옮긴다.
        /// from과 to(그룹 키)를 사용하며, EditorPrefs를 바꾼다.
        /// </summary>
        private void MoveNodeSize(string from, string to)
        {
            string old = SizeKey(from);
            if (!EditorPrefs.HasKey(old)) return;
            EditorPrefs.SetString(SizeKey(to), EditorPrefs.GetString(old));
            EditorPrefs.DeleteKey(old);
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
            if (_scrollToHighlight && _highlightLine > 0)
            {
                _textScroll.y = Mathf.Max(0f, LineTop(style, width, _highlightLine) - textRect.height * 0.3f);
                _scrollToHighlight = false;
            }
            // 클릭으로 포커스를 줄 때 전체 선택하지 않고 클릭한 자리에 커서를 둔다.
            if (Event.current.type == EventType.MouseUp && textRect.Contains(Event.current.mousePosition)) SelectAllOnMouseUpField?.SetValue(null, false);
            _textScroll = GUI.BeginScrollView(textRect, _textScroll, new Rect(0f, 0f, width, height), false, true);
            var area = new Rect(0f, 0f, width, height);
            GUI.SetNextControlName(TEXT_CONTROL);
            string next = EditorGUI.TextArea(area, _draft, style);
            if (Event.current.type == EventType.Repaint && _highlightLine > 0)
            {
                float top = LineTop(style, width, _highlightLine);
                float lineHeight = style.CalcHeight(new GUIContent(LineText(_highlightLine)), width) - style.padding.vertical;
                EditorGUI.DrawRect(new Rect(0f, top, width, lineHeight), new Color(1f, 0.82f, 0.25f, 0.35f));
                EditorGUI.DrawRect(new Rect(0f, top, 3f, lineHeight), new Color(1f, 0.82f, 0.25f, 1f));
            }
            if (Event.current.type == EventType.Repaint) HighlightStyle(style).Draw(area, new GUIContent(Highlight(next)), false, false, false, false);
            GUI.EndScrollView();
            // 에디터의 텍스트 입력은 편집기 하나를 여러 입력창이 함께 쓴다. 다른 입력창(노드 뷰의 인라인 편집)의 글이
            // 이 상자의 반환값으로 넘어와 초안을 덮지 않도록, 이 상자가 키보드 포커스를 가진 때만 받는다.
            if (GUI.GetNameOfFocusedControl() == TEXT_CONTROL) SetDraftFromInput(next);

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
        /// 텍스트 초안의 한 줄을 꺼낸다.
        /// line(1부터)을 사용하며, 그 줄 글 또는 빈 글을 반환한다.
        /// </summary>
        private string LineText(int line)
        {
            string[] lines = _draft.Split('\n');
            return line >= 1 && line <= lines.Length ? lines[line - 1] : "";
        }

        /// <summary>
        /// 텍스트 상자 안에서 한 줄이 시작하는 세로 위치를 구한다. 줄바꿈되는 긴 줄도 같은 스타일로 높이를 잰다.
        /// style, width, line을 사용하며, 위치를 반환한다.
        /// </summary>
        private float LineTop(GUIStyle style, float width, int line)
        {
            if (line <= 1) return style.padding.top;
            string[] lines = _draft.Split('\n');
            string prefix = string.Join("\n", lines, 0, Mathf.Min(line - 1, lines.Length));
            return style.CalcHeight(new GUIContent(prefix), width) - style.padding.bottom;
        }

        /// <summary>
        /// 노드 뷰에서 고른 항목의 텍스트 줄을 강조하고 그 줄로 스크롤한다. 대사 행이면 [eye ...] 다음의 Line: 줄을 고른다.
        /// line을 사용하며, _highlightLine과 _scrollToHighlight를 바꾼다.
        /// </summary>
        private void ShowTextLine(int line)
        {
            string[] lines = _draft.Split('\n');
            if (_graph.SelectedRow >= 0)
                while (line < lines.Length && lines[line - 1].TrimStart().StartsWith("[eye")) line++;
            _highlightLine = line;
            _scrollToHighlight = true;
            // 노드 뷰에서 누른 F2·Enter가 텍스트 상자로 가지 않도록 텍스트 입력을 끝낸다.
            EndTextInput();
            Repaint();
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
            // 입력 상자 스타일을 복사하면 배경 이미지까지 따라와 아래의 줄 강조를 덮으므로, 글자 배치에 쓰는 값만 옮긴 빈 스타일을 만든다.
            var style = new GUIStyle
            {
                font = input.font,
                fontSize = input.fontSize,
                fontStyle = input.fontStyle,
                alignment = input.alignment,
                padding = new RectOffset(input.padding.left, input.padding.right, input.padding.top, input.padding.bottom),
                contentOffset = input.contentOffset,
                clipping = input.clipping,
                wordWrap = true,
                richText = true,
            };
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
            if (Event.current.type == EventType.Used || _graph.WantsRepaint) Repaint();
            if (_graph.TakeSelectionLine(out int line))
            {
                if (line > 0) ShowTextLine(line);
                else _highlightLine = -1;
            }
            if (changed == null) return;
            bool ok = ApplyGraphEdit(changed, out string error);
            _graph.CompleteInline(ok, error);
            _graph.CompleteCreate(ok, _doc);
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
        private void Commit(DialogueEditorDocument doc, string csv, string renamedFrom = null, string renamedTo = null)
        {
            _doc = doc;
            if (DialogueEditorFormat.Save(doc.AssetPath, csv)) _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
            string flagsCsv = ReadFlagsSource();
            if (_historyIndex >= 0 && _history[_historyIndex].Csv == csv && _history[_historyIndex].FlagsCsv == flagsCsv) return;
            if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(new Snapshot { Doc = doc.Clone(), Csv = csv, FlagsCsv = flagsCsv, RenamedFrom = renamedFrom, RenamedTo = renamedTo });
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
            _history.Add(new Snapshot { Doc = _doc.Clone(), Csv = DialogueEditorFormat.WriteCsv(_doc, rowLines), FlagsCsv = ReadFlagsSource() });
            _historyIndex = 0;
        }

        /// <summary>
        /// 새 기억 파편을 만든다. Flags.csv에 파편 플래그 줄을 넣고, 열린 씬의 group(그 층 파편 묶음)에 기존 파편을 복제한 오브젝트를 둔다.
        /// client(이 회차 손님)와 group을 사용하며, 성공 여부와 오류, 새 파편을 반환하고 Flags.csv·실행 취소 기록·씬을 바꾼다.
        /// </summary>
        private (bool ok, string error, MemoryFragment fragment) CreateFragment(ClientCase client, GameObject group)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return (false, "Play Mode 중에는 파편을 추가할 수 없다.", null);
            MemoryFragment template = group.GetComponentInChildren<MemoryFragment>(true);
            if (template == null) template = client.Fragments().Find(f => f != null);
            if (template == null) return (false, $"'{client.PersonName}' 케이스에 복제할 기존 파편이 없다.", null);
            IrisMaze maze = group.GetComponentInParent<IrisMaze>(true);
            MazeLayer layer = group.GetComponentInParent<MazeLayer>(true);
            if (maze == null || layer == null) return (false, $"'{group.name}' 묶음이 미로 층 아래에 있지 않다.", null);

            string before = ReadFlagsSource();
            if (before == null) return (false, "Flags.csv를 UTF-8로 읽지 못했다.", null);
            if (_historyIndex >= 0 && _history[_historyIndex].FlagsCsv != before)
                return (false, "Flags.csv가 창 밖에서 바뀌었다. 파일을 다시 열고 추가한다.", null);

            // Flag ID는 파일 이름의 인물 부분을 소문자로 쓰고, Flags.csv에 없는 가장 작은 번호를 붙인다.
            var ids = new HashSet<string>();
            foreach (List<CellRange> row in ReadCellRanges(before, out _)) if (row.Count > 0) ids.Add(row[0].Value.Trim());
            string person = Path.GetFileNameWithoutExtension(_doc.AssetPath);
            int day = person.IndexOf("_Day", System.StringComparison.Ordinal);
            if (day > 0) person = person.Substring(0, day);
            string prefix = person.ToLowerInvariant() + "_fragment_";
            int n = 1;
            while (ids.Contains(prefix + n)) n++;
            string flag = prefix + n;

            string newline = before.Contains("\r\n") ? "\r\n" : "\n";
            string after = before + (before.Length == 0 || before.EndsWith("\n") ? "" : newline)
                + $"{flag},{client.PersonName} · 새 기억,파편,,새 기억,새 기억 글,{newline}";
            if (!WriteFlagsSource(after, out string error)) return (false, error, null);
            if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(new Snapshot { Doc = _doc.Clone(), Csv = DialogueEditorFormat.WriteCsv(_doc, new List<int>()), FlagsCsv = after });
            _historyIndex = _history.Count - 1;

            // 같은 층에서 이 묶음 파편이 쓰지 않는 첫 칸을 바깥 ring, sector 0부터 찾는다.
            var used = new HashSet<(int, int)>();
            foreach (MemoryFragment f in group.GetComponentsInChildren<MemoryFragment>(true)) used.Add((f.Ring, maze.Mod(f.Sector)));
            int ring = -1, sector = 0;
            for (int r = 0; r < maze.Rings && ring < 0; r++)
            {
                if (maze.LayerOfRing(r) != layer.Index) continue;
                for (int s = 0; s < maze.Sectors; s++)
                    if (!used.Contains((r, s))) { ring = r; sector = s; break; }
            }
            if (ring < 0) { ring = template.Ring; sector = template.Sector; }

            GameObject go = Instantiate(template.gameObject, group.transform);
            go.name = "기억 조각 · 새 기억";
            Undo.RegisterCreatedObjectUndo(go, "기억 파편 추가");
            MemoryFragment created = go.GetComponent<MemoryFragment>();
            var so = new SerializedObject(created);
            so.FindProperty("_flag").stringValue = flag;
            so.FindProperty("_title").stringValue = "새 기억";
            so.FindProperty("_text").stringValue = "새 기억 글";
            so.FindProperty("_collected").boolValue = false;
            so.FindProperty("_ring").intValue = ring;
            so.FindProperty("_sector").intValue = sector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(go.scene);
            _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
            Repaint();
            return (true, "", created);
        }

        /// <summary>
        /// Flags.csv 끝에 플래그 줄을 넣고 실행 취소 기록에 더한다. title·text·memo 칸은 비운다.
        /// id, description, source, condition을 사용하며, 성공 여부와 오류를 반환하고 Flags.csv와 기록을 바꾼다.
        /// </summary>
        private (bool ok, string error) AddFlag(string id, string description, string source, string condition)
        {
            string before = ReadFlagsSource();
            if (before == null) return (false, "Flags.csv를 UTF-8로 읽지 못했다.");
            if (_historyIndex >= 0 && _history[_historyIndex].FlagsCsv != before)
                return (false, "Flags.csv가 창 밖에서 바뀌었다. 파일을 다시 열고 추가한다.");
            foreach (List<CellRange> row in ReadCellRanges(before, out _))
                if (row.Count > 0 && row[0].Value.Trim() == id) return (false, $"'{id}'는 Flags.csv에 이미 있다.");
            string newline = before.Contains("\r\n") ? "\r\n" : "\n";
            string after = before + (before.Length == 0 || before.EndsWith("\n") ? "" : newline) + $"{id},{description},{source},{condition},,,{newline}";
            if (!WriteFlagsSource(after, out string error)) return (false, error);
            if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(new Snapshot { Doc = _doc.Clone(), Csv = DialogueEditorFormat.WriteCsv(_doc, new List<int>()), FlagsCsv = after });
            _historyIndex = _history.Count - 1;
            return (true, "");
        }

        /// <summary>
        /// Flags.csv의 UTF-8 본문을 BOM 없이 읽는다. 파일이 없거나 형식이 틀리면 null이다.
        /// </summary>
        private static string ReadFlagsSource()
        {
            try
            {
                string path = Path.GetFullPath(DialogueData.FlagsPath);
                return File.Exists(path) && Csv.TryDecodeUtf8(File.ReadAllBytes(path), out string source) ? source : null;
            }
            catch (IOException) { return null; }
        }

        /// <summary>
        /// Flags.csv를 저장·재임포트하고 게임에서 읽는 플래그 문구를 즉시 갱신한다.
        /// </summary>
        private static bool WriteFlagsSource(string source, out string error)
        {
            error = "";
            try
            {
                DialogueEditorFormat.Save(DialogueData.FlagsPath, source);
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(DialogueData.FlagsPath);
                if (asset == null) { error = "저장 후 Flags.csv를 다시 읽지 못했다."; return false; }
                Flags.LoadDefinitions(asset, true);
                return true;
            }
            catch (IOException ex) { error = "Flags.csv 저장 실패: " + ex.Message; return false; }
        }

        /// <summary>
        /// 파편 Flag ID의 획득 문구를 Flags.csv에 저장하고 대화 편집과 같은 기록에 추가한다.
        /// </summary>
        private (bool ok, string error) SaveFragmentText(string flag, string text)
        {
            string before = ReadFlagsSource();
            if (before == null) return (false, "Flags.csv를 UTF-8로 읽지 못했다.");
            if (_historyIndex >= 0 && _history[_historyIndex].FlagsCsv != before)
                return (false, "Flags.csv가 창 밖에서 바뀌었다. 파일을 다시 열고 수정한다.");
            if (!TryReplaceFlagText(before, flag, DialogueEditorFormat.Body(text), out string after, out string error)) return (false, error);
            if (before == after) return (true, "");
            if (!WriteFlagsSource(after, out error)) return (false, error);
            if (_historyIndex + 1 < _history.Count) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            var rowLines = new List<int>();
            _history.Add(new Snapshot { Doc = _doc.Clone(), Csv = DialogueEditorFormat.WriteCsv(_doc, rowLines), FlagsCsv = after });
            _historyIndex = _history.Count - 1;
            _savedAt = System.DateTime.Now.ToString("HH:mm:ss");
            Repaint();
            return (true, "");
        }

        /// <summary>CSV 칸의 원문 시작·끝과 따옴표를 해석한 값을 보관한다.</summary>
        private struct CellRange
        {
            public int Start;
            public int End;
            public string Value;
        }

        /// <summary>
        /// 따옴표 안의 쉼표·줄바꿈을 고려해 CSV 칸의 원문 위치를 찾는다.
        /// </summary>
        private static List<List<CellRange>> ReadCellRanges(string source, out bool closed)
        {
            var rows = new List<List<CellRange>>();
            var cells = new List<CellRange>();
            var value = new StringBuilder();
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < source.Length && source[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                    continue;
                }
                if (!quoted && (c == ',' || c == '\n'))
                {
                    cells.Add(new CellRange { Start = start, End = i, Value = value.ToString() });
                    value.Clear();
                    start = i + 1;
                    if (c == '\n') { rows.Add(cells); cells = new List<CellRange>(); }
                    continue;
                }
                if (c != '\r') value.Append(c);
            }
            closed = !quoted;
            if (start < source.Length || cells.Count > 0)
            {
                cells.Add(new CellRange { Start = start, End = source.Length, Value = value.ToString() });
                rows.Add(cells);
            }
            return rows;
        }

        /// <summary>
        /// 지정한 Flag ID의 text 칸 원문만 바꾸고 다른 행과 열을 그대로 둔다.
        /// </summary>
        private static bool TryReplaceFlagText(string source, string flag, string text, out string changed, out string error)
        {
            changed = source;
            error = "";
            List<List<CellRange>> rows = ReadCellRanges(source, out bool closed);
            if (!closed) { error = "Flags.csv의 따옴표가 닫히지 않았다."; return false; }
            int idColumn = -1, textColumn = -1;
            bool header = false;
            CellRange target = new CellRange();
            int matches = 0;
            foreach (List<CellRange> row in rows)
            {
                if (row.Count == 0 || row[0].Value.Trim().Length == 0 || row[0].Value.TrimStart().StartsWith("#")) continue;
                if (!header)
                {
                    header = true;
                    for (int i = 0; i < row.Count; i++)
                    {
                        if (row[i].Value.Trim() == "id") idColumn = i;
                        if (row[i].Value.Trim() == "text") textColumn = i;
                    }
                    continue;
                }
                if (idColumn < 0 || textColumn < 0) break;
                if (idColumn >= row.Count || row[idColumn].Value.Trim() != flag) continue;
                if (textColumn >= row.Count) { error = "Flags.csv의 '" + flag + "' 행에 text 칸이 없다."; return false; }
                target = row[textColumn];
                matches++;
            }
            if (idColumn < 0 || textColumn < 0) { error = "Flags.csv에 id 또는 text 열이 없다."; return false; }
            if (matches != 1) { error = matches == 0 ? "Flags.csv에 파편 플래그 '" + flag + "'가 없다." : "Flags.csv에 파편 플래그 '" + flag + "'가 중복돼 있다."; return false; }
            if (target.Value.Trim() == text) return true;
            string encoded = "\"" + text.Replace("\"", "\"\"") + "\"";
            changed = source.Substring(0, target.Start) + encoded + source.Substring(target.End);
            var errors = new List<string>();
            Csv.Parse(changed, errors);
            if (errors.Count > 0) { changed = source; error = errors[0]; return false; }
            return true;
        }

        /// <summary>
        /// 창 안에서 Ctrl+Z, Ctrl+Y(Mac은 Cmd)를 받아 실행 취소·다시 실행한다. Unity 단축키가 먼저 받으면 아래 단축키 메서드가 같은 일을 한다.
        /// 이벤트를 사용하며, 처리하면 이벤트를 사용 처리한다.
        /// </summary>
        private void HandleHistoryKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || !(e.control || e.command) || _doc == null || _graph.IsInlineEditing) return;
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
            string currentFlags = ReadFlagsSource();
            if (_historyIndex >= 0 && currentFlags != _history[_historyIndex].FlagsCsv)
            {
                _issues.Add(new DialogueEditorFormat.Issue { Message = "Flags.csv가 창 밖에서 바뀌었다. 파일을 다시 열어 실행 취소 기록을 갱신한다." });
                Repaint();
                return;
            }
            string nextFlags = _history[index].FlagsCsv;
            if (nextFlags != null && nextFlags != currentFlags && !WriteFlagsSource(nextFlags, out string flagsError))
            {
                _issues.Add(new DialogueEditorFormat.Issue { Message = flagsError });
                Repaint();
                return;
            }
            // 되돌리거나 다시 하는 편집이 ID 변경이면 노드 크기 설정 키도 따라 옮긴다.
            Snapshot moving = index < _historyIndex ? _history[_historyIndex] : _history[index];
            if (moving.RenamedFrom != null && moving.RenamedTo != null)
            {
                if (index < _historyIndex) MoveNodeSize(moving.RenamedTo, moving.RenamedFrom);
                else MoveNodeSize(moving.RenamedFrom, moving.RenamedTo);
            }
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
            EndTextInput();
        }

        /// <summary>
        /// 텍스트 상자 입력을 끝낸다. 키보드 포커스를 비우고, 공용 편집기가 예전 글을 들고 텍스트 상자를 계속 편집 중으로 여기지 않게 연결을 끊는다.
        /// OnGUI 밖(단축키 실행 취소 등)에서 불러도 같은 결과가 되도록 정적 상태를 직접 바꾼다.
        /// 입력값은 없으며, 키보드 포커스와 공용 편집기 상태를 바꾼다.
        /// </summary>
        private static void EndTextInput()
        {
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            if (RecycledEditorProperty?.GetValue(null) is TextEditor editor)
            {
                editor.controlID = 0;
                editor.text = "";
            }
        }

        /// <summary>
        /// 노드 뷰에서 바뀐 모델을 검사해 통과하면 텍스트로 다시 써서 두 뷰를 함께 바꾸고 CSV에 저장한다.
        /// 통과하지 못하면 모델, 텍스트, CSV를 그대로 두고 이유만 보여준다.
        /// changed를 사용하며, _doc, _draft, _issues를 바꾸고, 반영했으면 true를, 아니면 false와 첫 오류 이유를 반환한다.
        /// </summary>
        private bool ApplyGraphEdit(DialogueEditorDocument changed, out string error)
        {
            error = "";
            string text = DialogueEditorFormat.WriteText(changed);
            var issues = new List<DialogueEditorFormat.Issue>();
            DialogueEditorDocument parsed = DialogueEditorFormat.ParseText(text, changed.Kind, changed.AssetPath, issues);
            string csv = HasError(issues) ? null : DialogueEditorFormat.Validate(parsed, issues);
            _issues.Clear();
            if (HasError(issues))
            {
                // 거절된 편집의 줄 번호는 화면에 없는 글을 가리키므로 빼고 보여준다.
                foreach (DialogueEditorFormat.Issue i in issues)
                {
                    if (!i.Error) continue;
                    if (error.Length == 0) error = i.Message;
                    _issues.Add(new DialogueEditorFormat.Issue { Message = "노드 편집을 되돌렸다: " + i.Message });
                }
                Repaint();
                return false;
            }
            SetDraftText(text);
            _issues.AddRange(issues);
            Commit(parsed, csv, _graph.RenamedFrom, _graph.RenamedTo);
            if (_graph.RenamedFrom != null && _graph.RenamedTo != null) MoveNodeSize(_graph.RenamedFrom, _graph.RenamedTo);
            Repaint();
            return true;
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
            _graph.ClearSelection();
            _highlightLine = -1;
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
            EndTextInput();
            Repaint();
        }
    }
}
