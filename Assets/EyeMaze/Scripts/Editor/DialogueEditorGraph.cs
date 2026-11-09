using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 대사 편집기의 노드 뷰. 공통 모델을 매번 자동 배치해 그린다.
    /// 인물·회차 파일은 위에서부터 등장 Talk(enter, reveal), 상담 Dialogue 트리, Ending과 after 카드 순서로 놓고,
    /// again 같은 조건부 재대화와 @intro·@idle·@abyss는 해당 단계 옆의 보조 카드로 놓는다. Strings는 key 순서대로 격자에 놓는다.
    /// 화면에서 노드를 끌어 옮긴 위치와 화면 이동은 이 창에서만 쓰고 저장하지 않는다.
    /// 연결점을 끌어 같은 상담 actor의 다른 노드에 놓으면 goto가, 행 카드를 끌어 같은 단계의 다른 자리에 놓으면 소속과 순서가 바뀐 새 문서를 돌려준다.
    /// 그룹이나 행을 우클릭해 추가를 고르면 새 행을 넣은 새 문서를 돌려준다.
    /// 노드를 클릭하면 선택하고, F2로 그룹 ID를, Enter로 선택한 행의 본문을 노드 위에서 바로 고친다. 노드 오른쪽 아래 모서리를 끌면 크기를 바꾼다.
    /// Ctrl+휠로 포인터 아래를 기준으로 확대·축소한다. 배치는 확대 전 좌표계에서 하고, 그리기와 입력 판정은 화면 좌표(배치 × 확대율 + 화면 이동)로 한다.
    /// </summary>
    public class DialogueEditorGraph
    {
        private const float NODE_WIDTH = 250f;
        private const float HEADER_HEIGHT = 22f;
        private const float SITUATION_HEIGHT = 34f;
        private const float ROW_HEIGHT = 20f;
        private const float NODE_PADDING = 6f;
        private const float GAP_X = 40f;
        private const float GAP_Y = 60f;
        private const float SIDE_GAP = 16f;
        private const float AFTER_HEIGHT = 46f;
        private const float MARGIN = 20f;
        private const float TOP = 44f;
        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 2f;
        private const float ZOOM_STEP = 0.1f;
        private const float MIN_NODE_WIDTH = 180f;
        private const float MAX_NODE_WIDTH = 600f;
        private const float MAX_NODE_HEIGHT = 800f;
        private const float RESIZE_HANDLE = 12f;
        private const string INLINE_CONTROL = "DialogueGraphInline";
        private const double MESSAGE_SECONDS = 3.0;

        /// <summary>노드 위에서 바로 고치는 중인 항목</summary>
        private enum InlineKind
        {
            None,
            Rename,
            Body,
        }

        private static readonly Color GOTO_COLOR = new Color(0.45f, 0.75f, 1f);
        private static readonly Color ENDING_COLOR = new Color(1f, 0.6f, 0.3f);
        private static readonly Color GUIDE_COLOR = new Color(0.65f, 0.65f, 0.7f);

        /// <summary>상자 종류</summary>
        private enum BoxKind
        {
            Group,
            After,
        }

        /// <summary>화면에 놓인 상자 하나 (그룹 노드 또는 엔딩의 after 카드)</summary>
        private class Box
        {
            public BoxKind Kind;
            public int Group = -1;
            /// <summary>자동 배치 좌표계의 사각형 (화면 이동·노드 옮김 반영 전)</summary>
            public Rect Canvas;
            /// <summary>화면 좌표계의 사각형</summary>
            public Rect Rect;
            /// <summary>화면 좌표계에서 첫 행의 위쪽</summary>
            public float RowsTop;
            /// <summary>보조 카드인지 (again, @intro 등)</summary>
            public bool Side;
            /// <summary>상자 안에서 보이는 첫 행 (행이 넘치면 상자 안에서 스크롤한다)</summary>
            public int FirstRow;
            /// <summary>상자 안에 보이는 행 수</summary>
            public int VisibleRows = 1;
        }

        private readonly Dictionary<string, Vector2> _nodeOffsets = new Dictionary<string, Vector2>();
        // 그룹 상자 안의 행 스크롤 위치 (화면 전용)
        private readonly Dictionary<string, int> _rowScroll = new Dictionary<string, int>();
        // 모서리를 끌어 크기를 바꾸는 중인 그룹과 그 크기 (배치 좌표계)
        private string _resizeKey;
        private Vector2 _resizeSize;
        // 선택한 그룹(키)과 행 (-1이면 그룹 자체). 파일을 바꾸거나 다시 열면 지운다.
        private string _selKey;
        private int _selRow = -1;
        private int _selectionLine = -1;
        // 마지막 클릭이 노드 뷰 안이었는지. F2와 Enter는 이때만 받는다.
        private bool _focused;
        private InlineKind _inline;
        private string _inlineKey;
        private int _inlineRow = -1;
        private string _inlineText = "";
        private string _inlineError = "";
        private bool _inlineFocus;
        // 인라인 편집을 확정해 창의 검사를 기다리는 중인지
        private bool _inlineAwait;
        private string _message = "";
        private double _messageUntil;
        // 마지막으로 그린 배치. 휠 스크롤과 Enter 편집에서 상자를 찾을 때 쓴다.
        private List<Box> _lastBoxes = new List<Box>();
        private DialogueEditorDocument _lastDoc;
        private Vector2 _pan;
        private float _zoom = 1f;
        private int _control;
        private string _dragNode;
        private bool _panning;
        // 끌고 있는 연결선 또는 카드 (그룹 번호, 행 번호). 없으면 -1이다.
        private int _linkGroup = -1;
        private int _linkItem = -1;
        private int _cardGroup = -1;
        private int _cardItem = -1;
        private Vector2 _mouse;
        // 우클릭 메뉴처럼 나중에 확정되는 편집. 다음 Draw에서 돌려준다.
        private DialogueEditorDocument _pending;

        private GUIStyle _rowStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _situationStyle;
        private float _styleZoom = -1f;

        /// <summary>노드 뷰 확대율 (0.5~2.0, 파일에 저장하지 않음)</summary>
        public float Zoom => _zoom;

        /// <summary>그룹 키로 저장된 노드 크기를 읽는다. 없으면 null이다. 창이 EditorPrefs로 채운다.</summary>
        public System.Func<string, Vector2?> SizeLoader { get; set; }

        /// <summary>그룹 키의 노드 크기를 저장한다. 창이 EditorPrefs로 채운다.</summary>
        public System.Action<string, Vector2> SizeSaver { get; set; }

        /// <summary>마지막으로 확정한 ID 변경의 이전·새 그룹 키 (없으면 null). 창이 크기 설정 키를 옮길 때 쓴다.</summary>
        public string RenamedFrom { get; private set; }
        public string RenamedTo { get; private set; }

        /// <summary>인라인 편집 중인지</summary>
        public bool IsInlineEditing => _inline != InlineKind.None;

        /// <summary>선택한 행 번호 (그룹만 선택했거나 선택이 없으면 -1)</summary>
        public int SelectedRow => _selKey == null ? -1 : _selRow;

        /// <summary>
        /// 새 파일을 열 때 화면 이동과 노드 위치 옮김을 지운다.
        /// 입력값은 없으며, _pan과 _nodeOffsets를 비운다.
        /// </summary>
        public void Reset()
        {
            _pan = Vector2.zero;
            _zoom = 1f;
            _nodeOffsets.Clear();
            _rowScroll.Clear();
            _resizeKey = null;
            ClearSelection();
            _dragNode = null;
            _panning = false;
            ClearDrag();
        }

        /// <summary>
        /// 선택과 인라인 편집을 지운다. 파일을 바꾸거나 다시 열 때 부른다.
        /// 입력값은 없으며, 선택 상태를 비운다.
        /// </summary>
        public void ClearSelection()
        {
            _selKey = null;
            _selRow = -1;
            _selectionLine = -1;
            CancelInline();
        }

        /// <summary>
        /// 선택이 바뀌어 텍스트 뷰에서 보여줄 줄이 있으면 꺼낸다. 선택이 풀렸으면 줄 번호 0을 준다.
        /// line에 줄 번호를 쓰며, 바뀌었으면 true를 반환하고 한 번 꺼내면 지운다.
        /// </summary>
        public bool TakeSelectionLine(out int line)
        {
            line = _selectionLine;
            _selectionLine = -1;
            return line >= 0;
        }

        /// <summary>
        /// 창이 인라인 편집 결과를 검사한 뒤 알려준다. 통과하면 편집을 닫고, 아니면 입력창을 그대로 두고 이유를 보인다.
        /// ok와 error를 사용하며, 인라인 편집 상태를 바꾼다.
        /// </summary>
        public void CompleteInline(bool ok, string error)
        {
            if (!_inlineAwait) return;
            _inlineAwait = false;
            if (!ok)
            {
                _inlineError = error;
                _inlineFocus = true;
                RenamedFrom = RenamedTo = null;
                return;
            }
            if (_inline == InlineKind.Rename && RenamedTo != null) _selKey = RenamedTo;
            _inline = InlineKind.None;
            _inlineError = "";
            // 창이 크기 설정 키를 옮긴 뒤이므로 다음 편집이 이 값을 다시 쓰지 않게 지운다.
            RenamedFrom = RenamedTo = null;
        }

        /// <summary>
        /// 인라인 편집을 취소한다.
        /// 입력값은 없으며, 인라인 상태를 비운다.
        /// </summary>
        private void CancelInline()
        {
            _inline = InlineKind.None;
            _inlineAwait = false;
            _inlineError = "";
            RenamedFrom = RenamedTo = null;
        }

        /// <summary>
        /// 노드 뷰 아래쪽에 잠깐 보일 안내를 남긴다.
        /// text를 사용하며, _message를 바꾼다.
        /// </summary>
        private void Notify(string text)
        {
            _message = text;
            _messageUntil = EditorApplication.timeSinceStartup + MESSAGE_SECONDS;
        }

        /// <summary>
        /// 끌던 연결선과 카드를 놓는다.
        /// 입력값은 없으며, 끌기 상태를 비운다.
        /// </summary>
        private void ClearDrag()
        {
            _linkGroup = _linkItem = _cardGroup = _cardItem = -1;
        }

        /// <summary>
        /// 노드 뷰를 그리고 마우스 입력을 처리한다.
        /// rect, doc, editable(연결·카드 편집 허용), notice(위에 띄울 안내)를 사용하며,
        /// 연결이나 카드를 놓아 모델이 바뀌면 바뀐 복사본을, 아니면 null을 반환한다.
        /// </summary>
        public DialogueEditorDocument Draw(Rect rect, DialogueEditorDocument doc, bool editable, string notice)
        {
            EnsureStyles();
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.16f));
            List<Box> boxes = Layout(doc, rect.width);
            _lastBoxes = boxes;
            _lastDoc = doc;

            // 입력은 클립 밖에서 노드 뷰 좌표로 바꿔 처리한다. 클립 안에서는 뷰 밖에서 놓은 마우스 이벤트를 받지 못한다.
            Event e = Event.current;
            Vector2 windowMouse = e.mousePosition;
            e.mousePosition = windowMouse - rect.position;
            _control = GUIUtility.GetControlID(FocusType.Passive);
            bool inside = new Rect(0f, 0f, rect.width, rect.height).Contains(e.mousePosition);
            DialogueEditorDocument changed = HandleKeys(e, doc, editable);
            HandleResize(e, doc, boxes, inside);
            HandleSelection(e, doc, boxes, inside);
            bool blocked = !editable || _inline != InlineKind.None;
            if (changed == null && !blocked) changed = HandleEditInput(e, doc, boxes, inside);
            if (blocked) ClearDrag();
            if (_inline == InlineKind.None) HandleContextMenu(e, doc, boxes, inside, editable, windowMouse);
            if (changed == null && _pending != null)
            {
                changed = editable ? _pending : null;
                _pending = null;
            }
            HandleViewInput(e, doc, boxes, inside);
            HandleZoom(e, inside);
            // 끄는 동안 마우스를 이 뷰가 붙잡아, 뷰 밖에서 버튼을 놓아도 놓기 이벤트를 받아 끌기를 끝낸다.
            bool dragging = _linkGroup >= 0 || _cardGroup >= 0 || _panning || _dragNode != null || _resizeKey != null;
            if (dragging) GUIUtility.hotControl = _control;
            else if (GUIUtility.hotControl == _control) GUIUtility.hotControl = 0;
            e.mousePosition = windowMouse;
            if (_styleZoom != _zoom) EnsureStyles();

            GUI.BeginClip(rect);
            // 선이 상자에 가리지 않도록 상자를 먼저 그리고 선을 위에 그린다.
            foreach (Box b in boxes) DrawBox(doc, b);
            DrawEdges(doc, boxes);
            DrawDragFeedback(doc, boxes);
            DrawSelection(doc, boxes);
            DrawInline(doc, boxes);
            if (_message.Length > 0 && EditorApplication.timeSinceStartup < _messageUntil)
            {
                var m = new Rect(8f, rect.height - 24f, rect.width - 90f, 20f);
                EditorGUI.DrawRect(m, new Color(0.1f, 0.1f, 0.12f, 0.92f));
                GUI.Label(new Rect(m.x + 6f, m.y + 2f, m.width - 12f, m.height), _message, EditorStyles.whiteMiniLabel);
            }
            if (!string.IsNullOrEmpty(notice))
            {
                var r = new Rect(8f, 6f, rect.width - 16f, 20f);
                EditorGUI.DrawRect(r, new Color(0.45f, 0.18f, 0.15f, 0.9f));
                GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, r.height), notice, EditorStyles.whiteMiniLabel);
            }
            if (!Mathf.Approximately(_zoom, 1f))
                GUI.Label(new Rect(rect.width - 70f, rect.height - 20f, 64f, 18f), $"{Mathf.RoundToInt(_zoom * 100f)}%", EditorStyles.miniLabel);
            GUI.EndClip();
            return changed;
        }

        // ---------------------------------------------------------------- 배치

        /// <summary>
        /// 그룹 키를 만든다. 노드 옮김과 크기 설정을 그룹마다 기억할 때 쓴다.
        /// g를 사용하며, section/actor/id 글을 반환한다.
        /// </summary>
        public static string KeyOf(DialogueEditorDocument.Group g) => g.Section + "/" + g.Actor + "/" + g.Id;

        /// <summary>
        /// 배치 좌표계의 길이를 화면 길이로 바꾼다.
        /// v를 사용하며, 확대율을 곱한 값을 반환한다.
        /// </summary>
        private float Z(float v) => v * _zoom;

        /// <summary>
        /// 모델의 그룹과 after 카드를 자동 배치하고 화면 좌표를 정한다.
        /// doc과 viewWidth를 사용하며, 상자 목록을 반환한다.
        /// </summary>
        private List<Box> Layout(DialogueEditorDocument doc, float viewWidth)
        {
            List<Box> boxes = doc.Kind == DialogueEditorKind.Day ? LayoutDay(doc) : LayoutGrid(doc, viewWidth);
            foreach (Box b in boxes)
            {
                Vector2 offset = Vector2.zero;
                if (b.Group >= 0) _nodeOffsets.TryGetValue(KeyOf(doc.Groups[b.Group]) + (b.Kind == BoxKind.After ? "#after" : ""), out offset);
                b.Rect = new Rect((b.Canvas.position + offset) * _zoom + _pan, b.Canvas.size * _zoom);
                if (b.Kind != BoxKind.Group) continue;
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                float rowsArea = b.Canvas.height - HEADER_HEIGHT - SubHeight(g) - NODE_PADDING;
                b.RowsTop = b.Rect.y + Z(HEADER_HEIGHT + SubHeight(g));
                b.VisibleRows = Mathf.Max(1, Mathf.FloorToInt(rowsArea / ROW_HEIGHT + 0.01f));
                _rowScroll.TryGetValue(KeyOf(g), out int first);
                b.FirstRow = Mathf.Clamp(first, 0, Mathf.Max(0, g.Items.Count - b.VisibleRows));
            }
            return boxes;
        }

        /// <summary>
        /// 그룹 상자의 배치 크기를 정한다. 끄는 중이거나 저장된 크기가 있으면 그 크기를, 없으면 자동 크기를 쓴다.
        /// g를 사용하며, 허용 범위로 자른 크기를 반환한다.
        /// </summary>
        private Vector2 SizeOf(DialogueEditorDocument.Group g)
        {
            string key = KeyOf(g);
            Vector2? saved = key == _resizeKey ? _resizeSize : SizeLoader?.Invoke(key);
            return saved.HasValue ? ClampSize(g, saved.Value) : new Vector2(NODE_WIDTH, NodeHeight(g));
        }

        /// <summary>
        /// 노드 크기를 허용 범위로 자른다. 너비는 180~600, 높이는 머리와 한 행이 보이는 높이부터 800까지다.
        /// g와 size를 사용하며, 자른 크기를 반환한다.
        /// </summary>
        private static Vector2 ClampSize(DialogueEditorDocument.Group g, Vector2 size)
        {
            float minHeight = HEADER_HEIGHT + SubHeight(g) + ROW_HEIGHT + NODE_PADDING;
            return new Vector2(Mathf.Clamp(size.x, MIN_NODE_WIDTH, MAX_NODE_WIDTH), Mathf.Clamp(size.y, minHeight, MAX_NODE_HEIGHT));
        }

        /// <summary>
        /// 인물·회차 파일을 등장 Talk, 상담 트리, 엔딩 순서로 위에서 아래로 놓는다.
        /// doc을 사용하며, 배치 좌표가 정해진 상자 목록을 반환한다.
        /// </summary>
        private List<Box> LayoutDay(DialogueEditorDocument doc)
        {
            var boxes = new List<Box>();
            Box Add(int g, float x, float y, bool side)
            {
                var b = new Box { Kind = BoxKind.Group, Group = g, Side = side, Canvas = new Rect(new Vector2(x, y), SizeOf(doc.Groups[g])) };
                boxes.Add(b);
                return b;
            }
            float mainX = -NODE_WIDTH * 0.5f;
            float y = 0f;

            // 1. 등장 Talk: actor마다 enter, reveal을 가운데 줄에, 나머지 대화는 오른쪽 보조 카드로 놓는다.
            foreach (string actor in Actors(doc, Csv.SECTION_TALK))
            {
                float chainWidth = NODE_WIDTH;
                foreach (DialogueEditorDocument.Group grp in doc.Groups)
                    if (grp.Section == Csv.SECTION_TALK && grp.Actor == actor && IsChainTalk(grp)) chainWidth = Mathf.Max(chainWidth, SizeOf(grp).x);
                float sideX = mainX + chainWidth + GAP_X;
                float startY = y, sideY = y;
                for (int g = 0; g < doc.Groups.Count; g++)
                {
                    DialogueEditorDocument.Group grp = doc.Groups[g];
                    if (grp.Section != Csv.SECTION_TALK || grp.Actor != actor) continue;
                    if (IsChainTalk(grp))
                    {
                        Box b = Add(g, mainX, y, false);
                        y += b.Canvas.height + GAP_Y;
                    }
                    else
                    {
                        Box b = Add(g, sideX, sideY, true);
                        sideY += b.Canvas.height + SIDE_GAP;
                    }
                }
                y = Mathf.Max(y, sideY + GAP_Y - SIDE_GAP);
                if (y == startY) y += GAP_Y;
            }

            // 2. 상담: 시작 노드부터 goto를 따라 층을 나눠 놓는다. 닿지 않는 노드는 마지막 층에 놓는다.
            var dialogue = new List<int>();
            for (int g = 0; g < doc.Groups.Count; g++) if (doc.Groups[g].Section == Csv.SECTION_DIALOGUE) dialogue.Add(g);
            float firstRowRight = mainX + NODE_WIDTH, firstRowY = y;
            if (dialogue.Count > 0)
            {
                var level = new Dictionary<int, int> { [dialogue[0]] = 0 };
                var order = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(dialogue[0]);
                while (queue.Count > 0)
                {
                    int g = queue.Dequeue();
                    order.Add(g);
                    DialogueEditorDocument.Group grp = doc.Groups[g];
                    foreach (DialogueEditorDocument.Item i in grp.Items)
                    {
                        int t = doc.IndexOf(Csv.SECTION_DIALOGUE, grp.Actor, i.Get(DialogueEditorFormat.GOTO));
                        if (t < 0 || level.ContainsKey(t)) continue;
                        level[t] = level[g] + 1;
                        queue.Enqueue(t);
                    }
                }
                int maxLevel = 0;
                foreach (int l in level.Values) maxLevel = Mathf.Max(maxLevel, l);
                foreach (int g in dialogue) if (!level.ContainsKey(g)) { level[g] = maxLevel + 1; order.Add(g); }

                var rows = new SortedDictionary<int, List<int>>();
                foreach (int g in order)
                {
                    if (!rows.TryGetValue(level[g], out List<int> list)) rows[level[g]] = list = new List<int>();
                    list.Add(g);
                }
                bool first = true;
                foreach (List<int> list in rows.Values)
                {
                    float width = (list.Count - 1) * GAP_X;
                    foreach (int g in list) width += SizeOf(doc.Groups[g]).x;
                    float x = -width * 0.5f, h = 0f;
                    foreach (int g in list)
                    {
                        Box b = Add(g, x, y, false);
                        x += b.Canvas.width + GAP_X;
                        h = Mathf.Max(h, b.Canvas.height);
                    }
                    if (first) { firstRowRight = x - GAP_X; firstRowY = y; first = false; }
                    y += h + GAP_Y;
                }
            }

            // 3. 사람별 문구(@intro, @idle, @abyss)는 상담 첫 줄 오른쪽에 보조 카드로 놓는다.
            float noteY = firstRowY;
            for (int g = 0; g < doc.Groups.Count; g++)
            {
                DialogueEditorDocument.Group grp = doc.Groups[g];
                if (grp.Section != Csv.SECTION_ENDING || !grp.Id.StartsWith("@")) continue;
                Box b = Add(g, firstRowRight + GAP_X, noteY, true);
                noteY += b.Canvas.height + SIDE_GAP;
            }
            if (dialogue.Count == 0) y = Mathf.Max(y, noteY + GAP_Y);

            // 4. 엔딩: 한 줄로 놓고 각 엔딩 아래에 after 카드를 붙인다.
            var endings = new List<int>();
            for (int g = 0; g < doc.Groups.Count; g++)
                if (doc.Groups[g].Section == Csv.SECTION_ENDING && !doc.Groups[g].Id.StartsWith("@")) endings.Add(g);
            if (endings.Count > 0)
            {
                float width = (endings.Count - 1) * GAP_X;
                foreach (int g in endings) width += SizeOf(doc.Groups[g]).x;
                float x = -width * 0.5f;
                foreach (int g in endings)
                {
                    Box b = Add(g, x, y, false);
                    if (doc.Groups[g].Get("after").Length > 0)
                        boxes.Add(new Box { Kind = BoxKind.After, Group = g, Canvas = new Rect(x, b.Canvas.yMax + 30f, b.Canvas.width, AFTER_HEIGHT) });
                    x += b.Canvas.width + GAP_X;
                }
            }

            // 가장 왼쪽 위 상자가 여백 안쪽에 오도록 옮긴다.
            float minX = float.MaxValue;
            foreach (Box b in boxes) minX = Mathf.Min(minX, b.Canvas.x);
            Vector2 shift = new Vector2(MARGIN - (boxes.Count > 0 ? minX : 0f), TOP);
            foreach (Box b in boxes) b.Canvas.position += shift;
            return boxes;
        }

        /// <summary>
        /// Strings의 key를 순서대로 왼쪽에서 오른쪽, 위에서 아래로 격자에 놓는다.
        /// doc과 viewWidth를 사용하며, 상자 목록을 반환한다.
        /// </summary>
        private List<Box> LayoutGrid(DialogueEditorDocument doc, float viewWidth)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth - MARGIN) / (NODE_WIDTH + GAP_X)));
            var all = new List<Box>();
            float y = TOP;
            for (int start = 0; start < doc.Groups.Count; start += columns)
            {
                float h = 0f, x = MARGIN;
                for (int c = 0; c < columns && start + c < doc.Groups.Count; c++)
                {
                    int g = start + c;
                    var b = new Box { Kind = BoxKind.Group, Group = g, Canvas = new Rect(new Vector2(x, y), SizeOf(doc.Groups[g])) };
                    x += b.Canvas.width + GAP_X;
                    h = Mathf.Max(h, b.Canvas.height);
                    all.Add(b);
                }
                y += h + GAP_X;
            }
            return all;
        }

        /// <summary>
        /// 한 단계에 나오는 actor를 처음 나온 순서로 모은다.
        /// doc과 section을 사용하며, actor 목록을 반환한다.
        /// </summary>
        private static List<string> Actors(DialogueEditorDocument doc, string section)
        {
            var list = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
                if (g.Section == section && !list.Contains(g.Actor)) list.Add(g.Actor);
            return list;
        }

        /// <summary>
        /// 등장 흐름(enter, reveal)에 놓을 Talk 그룹인지 본다. 나머지 Talk는 조건부 재대화 보조 카드다.
        /// g를 사용하며, 등장 흐름이면 true를 반환한다.
        /// </summary>
        private static bool IsChainTalk(DialogueEditorDocument.Group g) => g.Id == TalkScript.Enter || g.Id == TalkScript.Reveal;

        /// <summary>
        /// 그룹 상자의 높이를 정한다.
        /// g를 사용하며, 높이를 반환한다.
        /// </summary>
        private static float NodeHeight(DialogueEditorDocument.Group g)
        {
            return HEADER_HEIGHT + SubHeight(g) + Mathf.Max(1, g.Items.Count) * ROW_HEIGHT + NODE_PADDING;
        }

        /// <summary>
        /// 머리 아래에 그룹 칸(상황, 조건, 제목)을 보여줄 높이를 정한다.
        /// g를 사용하며, 높이를 반환한다.
        /// </summary>
        private static float SubHeight(DialogueEditorDocument.Group g) => GroupSummary(g).Length > 0 ? SITUATION_HEIGHT : 0f;

        // ---------------------------------------------------------------- 그리기

        /// <summary>
        /// 단계 안내선, goto 화살표, 엔딩 연결선, after 연결선을 그린다. 앞 층이나 같은 노드로 돌아가는 선도 그대로 이어 그린다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawEdges(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (Event.current.type != EventType.Repaint || doc.Kind != DialogueEditorKind.Day) return;
            var byGroup = new Dictionary<int, Box>();
            foreach (Box b in boxes) if (b.Kind == BoxKind.Group) byGroup[b.Group] = b;

            // 등장 흐름: Talk의 enter·reveal을 차례로 잇고 마지막에서 상담 시작 노드로 안내선을 긋는다.
            // 실제로는 필드에서 말을 걸어 넘어가므로 CSV의 goto가 아니다.
            Box prev = null;
            Box firstDialogue = null;
            foreach (Box b in boxes)
            {
                if (b.Kind != BoxKind.Group || b.Side) continue;
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                if (g.Section == Csv.SECTION_TALK)
                {
                    if (prev != null) DrawGuide(BottomCenter(prev.Rect), TopCenter(b.Rect));
                    prev = b;
                }
                else if (g.Section == Csv.SECTION_DIALOGUE && firstDialogue == null) firstDialogue = b;
            }
            if (prev != null && firstDialogue != null) DrawGuide(BottomCenter(prev.Rect), TopCenter(firstDialogue.Rect));

            foreach (Box b in boxes)
            {
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                if (b.Kind == BoxKind.After)
                {
                    if (byGroup.TryGetValue(b.Group, out Box end)) DrawArrow(BottomCenter(end.Rect), TopCenter(b.Rect), GUIDE_COLOR, false);
                    continue;
                }
                if (b.Side)
                {
                    // 보조 카드는 옆 단계에 속한다는 것만 점선으로 잇는다.
                    Box owner = SideOwner(doc, boxes, b);
                    if (owner != null) DrawGuide(new Vector2(owner.Rect.xMax, owner.Rect.y + Z(HEADER_HEIGHT) * 0.5f), new Vector2(b.Rect.x, b.Rect.y + Z(HEADER_HEIGHT) * 0.5f));
                    continue;
                }
                if (g.Section != Csv.SECTION_DIALOGUE) continue;
                for (int k = 0; k < g.Items.Count; k++)
                {
                    Vector2 from = PortPosition(b, k);
                    int t = doc.IndexOf(Csv.SECTION_DIALOGUE, g.Actor, g.Items[k].Get(DialogueEditorFormat.GOTO));
                    if (t >= 0 && byGroup.TryGetValue(t, out Box target)) DrawArrow(from, TopCenter(target.Rect), GOTO_COLOR, true);
                    int e = doc.IndexOf(Csv.SECTION_ENDING, g.Actor, g.Items[k].Get(DialogueEditorFormat.ENDING));
                    if (e >= 0 && byGroup.TryGetValue(e, out Box endBox)) DrawArrow(from, TopCenter(endBox.Rect), ENDING_COLOR, true);
                }
            }
        }

        /// <summary>
        /// 보조 카드가 속한 단계의 상자를 찾는다. Talk는 같은 actor의 첫 등장 상자, 사람별 문구는 상담 시작 노드다.
        /// doc, boxes, side를 사용하며, 상자 또는 null을 반환한다.
        /// </summary>
        private static Box SideOwner(DialogueEditorDocument doc, List<Box> boxes, Box side)
        {
            DialogueEditorDocument.Group sg = doc.Groups[side.Group];
            foreach (Box b in boxes)
            {
                if (b.Kind != BoxKind.Group || b.Side) continue;
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                if (sg.Section == Csv.SECTION_TALK && g.Section == Csv.SECTION_TALK && g.Actor == sg.Actor) return b;
                if (sg.Section == Csv.SECTION_ENDING && g.Section == Csv.SECTION_DIALOGUE) return b;
            }
            return null;
        }

        private static Vector2 TopCenter(Rect r) => new Vector2(r.center.x, r.yMin);
        private static Vector2 BottomCenter(Rect r) => new Vector2(r.center.x, r.yMax);

        /// <summary>
        /// 선택지 행 오른쪽 끝의 연결점 위치를 구한다.
        /// b와 행 번호 row를 사용하며, 위치를 반환한다.
        /// </summary>
        private Vector2 PortPosition(Box b, int row)
        {
            // 상자 안에서 스크롤되어 보이지 않는 행의 선은 보이는 행 영역의 위나 아래 끝에서 나간다.
            float shown = Mathf.Clamp(row - b.FirstRow, 0, b.VisibleRows - 1);
            return new Vector2(b.Rect.xMax - Z(9f), b.RowsTop + Z(shown * ROW_HEIGHT + ROW_HEIGHT * 0.5f));
        }

        /// <summary>
        /// 연결점에서 대상 위쪽까지 곡선 화살표를 그린다.
        /// from, to, color, sideways(오른쪽으로 나가는 선인지)를 사용한다.
        /// </summary>
        private void DrawArrow(Vector2 from, Vector2 to, Color color, bool sideways)
        {
            Vector3 a = from, b = to;
            Vector3 startTangent = a + (sideways ? new Vector3(Z(80f), 0f) : new Vector3(0f, Z(40f)));
            Handles.DrawBezier(a, b, startTangent, b + new Vector3(0f, -Z(60f)), color, null, Mathf.Max(1.5f, Z(2.5f)));
            Color old = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(b, b + new Vector3(-Z(6f), -Z(10f)), b + new Vector3(Z(6f), -Z(10f)));
            Handles.color = old;
        }

        /// <summary>
        /// 실제 연결이 아닌 단계 흐름을 점선 곡선으로 그린다.
        /// from과 to를 사용한다.
        /// </summary>
        private void DrawGuide(Vector2 from, Vector2 to)
        {
            bool vertical = Mathf.Abs(to.y - from.y) > Mathf.Abs(to.x - from.x);
            Vector3 a = from, b = to;
            Vector3 t1 = a + (vertical ? new Vector3(0f, Z(40f)) : new Vector3(Z(30f), 0f));
            Vector3 t2 = b + (vertical ? new Vector3(0f, -Z(40f)) : new Vector3(-Z(30f), 0f));
            Vector3[] points = Handles.MakeBezierPoints(a, b, t1, t2, 24);
            Color old = Handles.color;
            Handles.color = GUIDE_COLOR;
            Handles.DrawDottedLines(SegmentPairs(points), 4f);
            if (vertical) Handles.DrawAAConvexPolygon(b, b + new Vector3(-Z(5f), -Z(9f)), b + new Vector3(Z(5f), -Z(9f)));
            Handles.color = old;
        }

        /// <summary>
        /// 연속한 점 목록을 선분 쌍 목록으로 바꾼다.
        /// points를 사용하며, (시작, 끝) 쌍을 이어 붙인 배열을 반환한다.
        /// </summary>
        private static Vector3[] SegmentPairs(Vector3[] points)
        {
            var pairs = new Vector3[(points.Length - 1) * 2];
            for (int i = 0; i < points.Length - 1; i++) { pairs[i * 2] = points[i]; pairs[i * 2 + 1] = points[i + 1]; }
            return pairs;
        }

        /// <summary>
        /// 그룹 상자 또는 after 카드를 그린다.
        /// doc과 b를 사용한다.
        /// </summary>
        private void DrawBox(DialogueEditorDocument doc, Box b)
        {
            DialogueEditorDocument.Group g = doc.Groups[b.Group];
            if (b.Kind == BoxKind.After)
            {
                EditorGUI.DrawRect(b.Rect, new Color(0.32f, 0.24f, 0.18f));
                GUI.Label(new Rect(b.Rect.x + Z(6f), b.Rect.y + Z(3f), b.Rect.width - Z(12f), b.Rect.height - Z(6f)), "after · " + Flat(g.Get("after")), _situationStyle);
                return;
            }
            EditorGUI.DrawRect(b.Rect, b.Side ? new Color(0.21f, 0.21f, 0.23f) : new Color(0.24f, 0.24f, 0.26f));
            var header = new Rect(b.Rect.x, b.Rect.y, b.Rect.width, Z(HEADER_HEIGHT));
            EditorGUI.DrawRect(header, HeaderColor(doc, g, b));
            GUI.Label(new Rect(header.x + Z(6f), header.y + Z(3f), header.width - Z(12f), header.height), Title(doc, g, b), _headerStyle);

            string summary = GroupSummary(g);
            if (summary.Length > 0)
                GUI.Label(new Rect(b.Rect.x + Z(6f), header.yMax + Z(1f), b.Rect.width - Z(12f), Z(SITUATION_HEIGHT - 2f)), summary, _situationStyle);

            if (g.Items.Count == 0)
                GUI.Label(new Rect(b.Rect.x + Z(6f), b.RowsTop, b.Rect.width - Z(12f), Z(ROW_HEIGHT)), "(행 없음)", _rowStyle);
            int last = Mathf.Min(g.Items.Count, b.FirstRow + b.VisibleRows);
            for (int k = b.FirstRow; k < last; k++)
            {
                Rect row = RowRect(b, k);
                EditorGUI.DrawRect(row, new Color(0.3f, 0.3f, 0.32f));
                GUI.Label(new Rect(row.x + Z(4f), row.y, row.width - Z(26f), row.height), RowText(g, g.Items[k], k), _rowStyle);
                if (g.Section == Csv.SECTION_DIALOGUE) DrawPort(b, k, g.Items[k]);
            }
            if (g.Items.Count > b.VisibleRows)
                GUI.Label(new Rect(b.Rect.xMax - Z(90f), b.Rect.yMax - Z(15f), Z(70f), Z(14f)), $"{b.FirstRow + 1}-{last}/{g.Items.Count} ↕", _rowStyle);
            Rect handle = ResizeHandle(b);
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawLine(new Vector2(handle.xMax - 2f, handle.y + 2f), new Vector2(handle.x + 2f, handle.yMax - 2f));
            Handles.DrawLine(new Vector2(handle.xMax - 2f, handle.center.y), new Vector2(handle.center.x, handle.yMax - 2f));
            Handles.color = Color.white;
        }

        /// <summary>
        /// 그룹 상자 오른쪽 아래의 크기 조절 손잡이 사각형을 구한다.
        /// b를 사용하며, 화면 사각형을 반환한다.
        /// </summary>
        private Rect ResizeHandle(Box b) => new Rect(b.Rect.xMax - Z(RESIZE_HANDLE), b.Rect.yMax - Z(RESIZE_HANDLE), Z(RESIZE_HANDLE), Z(RESIZE_HANDLE));

        /// <summary>
        /// 행 카드 하나의 화면 사각형을 구한다.
        /// b와 행 번호 k를 사용하며, 사각형을 반환한다.
        /// </summary>
        private Rect RowRect(Box b, int k)
        {
            return new Rect(b.Rect.x + Z(4f), b.RowsTop + Z((k - b.FirstRow) * ROW_HEIGHT + 1f), b.Rect.width - Z(8f), Z(ROW_HEIGHT - 2f));
        }

        /// <summary>
        /// 상자 머리 색을 정한다. 단계마다 다르고, 상담 시작 노드와 보조 카드는 따로 구분한다.
        /// doc, g, b를 사용하며, 색을 반환한다.
        /// </summary>
        private static Color HeaderColor(DialogueEditorDocument doc, DialogueEditorDocument.Group g, Box b)
        {
            if (b.Side) return new Color(0.33f, 0.33f, 0.36f);
            switch (g.Section)
            {
                case Csv.SECTION_TALK: return new Color(0.38f, 0.3f, 0.45f);
                case Csv.SECTION_DIALOGUE: return IsStart(doc, b.Group) ? new Color(0.2f, 0.42f, 0.3f) : new Color(0.3f, 0.32f, 0.4f);
                case Csv.SECTION_ENDING: return new Color(0.48f, 0.28f, 0.13f);
                default: return new Color(0.3f, 0.32f, 0.4f);
            }
        }

        /// <summary>
        /// 상담 시작 노드(첫 dialogue 그룹)인지 본다.
        /// doc과 그룹 번호 g를 사용하며, 시작 노드면 true를 반환한다.
        /// </summary>
        private static bool IsStart(DialogueEditorDocument doc, int g)
        {
            for (int i = 0; i < doc.Groups.Count; i++) if (doc.Groups[i].Section == Csv.SECTION_DIALOGUE) return i == g;
            return false;
        }

        /// <summary>
        /// 상자 머리 글을 만든다. 단계와 actor, id를 함께 보인다.
        /// doc, g, b를 사용하며, 머리 글을 반환한다.
        /// </summary>
        private static string Title(DialogueEditorDocument doc, DialogueEditorDocument.Group g, Box b)
        {
            if (doc.Kind == DialogueEditorKind.Strings) return $"{b.Group + 1}. {g.Id}";
            switch (g.Section)
            {
                case Csv.SECTION_TALK: return $"Talk · {g.Actor} · {g.Id}";
                case Csv.SECTION_DIALOGUE: return (IsStart(doc, b.Group) ? "상담 시작 · " : "") + g.Id;
                default: return (g.Id.StartsWith("@") ? "문구 · " : "엔딩 · ") + g.Id;
            }
        }

        /// <summary>
        /// 선택지 행 오른쪽에 연결 상태 표시를 그린다. goto는 파란 점, ending은 주황 점, 둘 다 없으면 머묾 표시다.
        /// b, row, item을 사용한다.
        /// </summary>
        private void DrawPort(Box b, int row, DialogueEditorDocument.Item item)
        {
            Vector2 p = PortPosition(b, row);
            bool hasGoto = item.Get(DialogueEditorFormat.GOTO).Length > 0;
            bool hasEnding = item.Get(DialogueEditorFormat.ENDING).Length > 0;
            if (!hasGoto && !hasEnding)
            {
                GUI.Label(new Rect(p.x - Z(9f), p.y - Z(9f), Z(18f), Z(18f)), "↻", _rowStyle);
                return;
            }
            EditorGUI.DrawRect(new Rect(p.x - Z(4f), p.y - Z(4f), Z(8f), Z(8f)), hasEnding ? ENDING_COLOR : GOTO_COLOR);
        }

        /// <summary>
        /// 그룹 상자 머리 아래에 보여줄 그룹 칸 요약을 만든다.
        /// g를 사용하며, 요약 글(없으면 빈 글)을 반환한다.
        /// </summary>
        private static string GroupSummary(DialogueEditorDocument.Group g)
        {
            switch (g.Section)
            {
                case Csv.SECTION_DIALOGUE: return Flat(g.Get("situation"));
                case Csv.SECTION_TALK:
                    var parts = new List<string>();
                    if (g.Get("need").Length > 0) parts.Add("need " + g.Get("need"));
                    if (g.Get("set").Length > 0) parts.Add("set " + g.Get("set"));
                    return string.Join("  ·  ", parts);
                case Csv.SECTION_ENDING: return Flat(g.Get("title"));
                default: return "";
            }
        }

        /// <summary>
        /// 행 상자에 보여줄 한 줄 글을 만든다.
        /// g, item, index를 사용하며, 글을 반환한다.
        /// </summary>
        private static string RowText(DialogueEditorDocument.Group g, DialogueEditorDocument.Item item, int index)
        {
            string body = Flat(item.Get(DialogueEditorFormat.TEXT));
            if (g.Section == Csv.SECTION_DIALOGUE)
            {
                string need = item.Get("need");
                return $"{index + 1}. {body}" + (need.Length > 0 ? $"  [{need}]" : "");
            }
            if (g.Section == Csv.SECTION_TALK)
            {
                string sp = item.Get("speaker");
                string who = sp.Length == 0 ? g.Actor : sp == TalkScript.Narration ? "서술" : sp;
                return $"{who}: {body}";
            }
            return body;
        }

        /// <summary>
        /// 칸 안의 줄바꿈 표기를 공백으로 바꿔 한 줄로 만든다.
        /// s를 사용하며, 한 줄 글을 반환한다.
        /// </summary>
        private static string Flat(string s) => s.Replace("\\n", " ").Replace("\n", " ");

        /// <summary>
        /// 노드 뷰 글자 모양을 지금 확대율에 맞춰 만든다. 확대율이 바뀔 때만 다시 만든다.
        /// _zoom을 사용하며, 스타일 필드를 채운다.
        /// </summary>
        private void EnsureStyles()
        {
            if (_rowStyle != null && Mathf.Approximately(_styleZoom, _zoom)) return;
            _styleZoom = _zoom;
            int mini = Mathf.Max(5, Mathf.RoundToInt(10f * _zoom));
            _rowStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, wordWrap = false, fontSize = mini };
            _rowStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip, fontSize = Mathf.Max(5, Mathf.RoundToInt(11f * _zoom)) };
            _headerStyle.normal.textColor = Color.white;
            _situationStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, clipping = TextClipping.Clip, fontSize = mini };
            _situationStyle.normal.textColor = new Color(0.7f, 0.72f, 0.75f);
        }

        // ---------------------------------------------------------------- 연결·카드 편집

        /// <summary>
        /// 연결점이나 행 카드를 끌고 놓는 입력을 처리한다. 놓을 때 바뀐 내용을 복사본에 반영한다.
        /// e, doc, boxes, inside를 사용하며, 바뀐 복사본 또는 null을 반환한다.
        /// </summary>
        private DialogueEditorDocument HandleEditInput(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside)
        {
            if (e.isMouse) _mouse = e.mousePosition;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!inside || e.button != 0) return null;
                    Box hit = HitBox(boxes, e.mousePosition);
                    if (hit == null || hit.Kind != BoxKind.Group) return null;
                    int row = RowAt(doc, hit, e.mousePosition.y);
                    if (row < 0) return null;
                    if (doc.Groups[hit.Group].Section == Csv.SECTION_DIALOGUE && Vector2.Distance(PortPosition(hit, row), e.mousePosition) <= Z(9f))
                    {
                        _linkGroup = hit.Group;
                        _linkItem = row;
                    }
                    else
                    {
                        _cardGroup = hit.Group;
                        _cardItem = row;
                    }
                    e.Use();
                    return null;
                case EventType.MouseDrag:
                    if (_linkGroup >= 0 || _cardGroup >= 0) e.Use();
                    return null;
                case EventType.MouseUp:
                    if (_linkGroup < 0 && _cardGroup < 0) return null;
                    DialogueEditorDocument result = _linkGroup >= 0 ? DropLink(doc, boxes, e.mousePosition) : DropCard(doc, boxes, e.mousePosition);
                    ClearDrag();
                    e.Use();
                    return result;
            }
            return null;
        }

        /// <summary>
        /// 끌던 연결선을 놓는다. 같은 상담 actor의 다른 Dialogue 노드 위면 그 노드로 goto를 바꾸고, 함께 적힌 ending은 지운다.
        /// doc, boxes, p를 사용하며, 바뀐 복사본 또는 (바뀌지 않거나 거절하면) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument DropLink(DialogueEditorDocument doc, List<Box> boxes, Vector2 p)
        {
            Box target = HitBox(boxes, p);
            if (target == null || target.Kind != BoxKind.Group || target.Group == _linkGroup) return null;
            DialogueEditorDocument.Group from = doc.Groups[_linkGroup];
            DialogueEditorDocument.Group to = doc.Groups[target.Group];
            if (!from.SameStage(to) || _linkItem >= from.Items.Count) return null;
            DialogueEditorDocument.Item src = from.Items[_linkItem];
            if (src.Get(DialogueEditorFormat.GOTO) == to.Id && src.Get(DialogueEditorFormat.ENDING).Length == 0) return null;
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Item item = copy.Groups[_linkGroup].Items[_linkItem];
            item.Fields[DialogueEditorFormat.GOTO] = to.Id;
            item.Fields.Remove(DialogueEditorFormat.ENDING);
            return copy;
        }

        /// <summary>
        /// 끌던 행 카드를 놓는다. 같은 section·actor의 그룹이면 놓은 순서 위치로 그 행을 옮긴다.
        /// doc, boxes, p를 사용하며, 바뀐 복사본 또는 (바뀌지 않거나 거절하면) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument DropCard(DialogueEditorDocument doc, List<Box> boxes, Vector2 p)
        {
            Box target = HitBox(boxes, p);
            if (target == null || target.Kind != BoxKind.Group || _cardItem >= doc.Groups[_cardGroup].Items.Count) return null;
            if (!doc.Groups[_cardGroup].SameStage(doc.Groups[target.Group])) return null;
            int index = InsertIndex(doc, target, p.y);
            if (target.Group == _cardGroup)
            {
                if (index > _cardItem) index--;
                if (index == _cardItem) return null;
            }
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Item item = copy.Groups[_cardGroup].Items[_cardItem];
            copy.Groups[_cardGroup].Items.RemoveAt(_cardItem);
            List<DialogueEditorDocument.Item> to = copy.Groups[target.Group].Items;
            to.Insert(Mathf.Clamp(index, 0, to.Count), item);
            return copy;
        }

        /// <summary>
        /// 상자 안 y 위치에 있는 행 번호를 찾는다.
        /// doc, b, y를 사용하며, 행 번호 또는 -1을 반환한다.
        /// </summary>
        private int RowAt(DialogueEditorDocument doc, Box b, float y)
        {
            int shown = Mathf.FloorToInt((y - b.RowsTop) / Z(ROW_HEIGHT));
            if (shown < 0 || shown >= b.VisibleRows) return -1;
            int row = shown + b.FirstRow;
            return row < doc.Groups[b.Group].Items.Count ? row : -1;
        }

        /// <summary>
        /// 카드를 놓을 순서 위치를 구한다. 행 경계 중 가장 가까운 곳이다.
        /// doc, b, y를 사용하며, 0부터 행 수까지의 위치를 반환한다.
        /// </summary>
        private int InsertIndex(DialogueEditorDocument doc, Box b, float y)
        {
            int count = doc.Groups[b.Group].Items.Count;
            int shown = Mathf.Clamp(Mathf.RoundToInt((y - b.RowsTop) / Z(ROW_HEIGHT)), 0, b.VisibleRows);
            return Mathf.Clamp(shown + b.FirstRow, 0, count);
        }

        /// <summary>
        /// 끌고 있는 연결선이나 카드, 놓일 위치 표시를 그린다. 놓을 수 없는 상자에는 표시하지 않는다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawDragFeedback(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (Event.current.type != EventType.Repaint) return;
            Box hover = HitBox(boxes, _mouse);
            bool hoverGroup = hover != null && hover.Kind == BoxKind.Group;
            if (_linkGroup >= 0)
            {
                foreach (Box b in boxes)
                    if (b.Kind == BoxKind.Group && b.Group == _linkGroup)
                    {
                        Vector3 a = PortPosition(b, _linkItem);
                        Handles.DrawBezier(a, _mouse, a + new Vector3(Z(60f), 0f), (Vector3)_mouse + new Vector3(-Z(30f), 0f), Color.white, null, 2f);
                    }
                if (hoverGroup && hover.Group != _linkGroup && doc.Groups[_linkGroup].SameStage(doc.Groups[hover.Group])) DrawOutline(hover.Rect, GOTO_COLOR);
            }
            else if (_cardGroup >= 0 && _cardItem < doc.Groups[_cardGroup].Items.Count)
            {
                if (hoverGroup && doc.Groups[_cardGroup].SameStage(doc.Groups[hover.Group]))
                {
                    float y = hover.RowsTop + Z((InsertIndex(doc, hover, _mouse.y) - hover.FirstRow) * ROW_HEIGHT);
                    EditorGUI.DrawRect(new Rect(hover.Rect.x + 2f, y - 1.5f, hover.Rect.width - 4f, 3f), new Color(1f, 0.85f, 0.3f));
                }
                var ghost = new Rect(_mouse.x - Z(10f), _mouse.y - Z(ROW_HEIGHT) * 0.5f, Z(NODE_WIDTH - 8f), Z(ROW_HEIGHT - 2f));
                EditorGUI.DrawRect(ghost, new Color(0.4f, 0.4f, 0.48f, 0.9f));
                DialogueEditorDocument.Group g = doc.Groups[_cardGroup];
                GUI.Label(new Rect(ghost.x + Z(4f), ghost.y, ghost.width - Z(8f), ghost.height), RowText(g, g.Items[_cardItem], _cardItem), _rowStyle);
            }
        }

        /// <summary>
        /// 사각형 테두리를 그린다.
        /// r과 color를 사용한다.
        /// </summary>
        private static void DrawOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, 2f), color);
            EditorGUI.DrawRect(new Rect(r.x - 2f, r.yMax, r.width + 4f, 2f), color);
            EditorGUI.DrawRect(new Rect(r.x - 2f, r.y, 2f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax, r.y, 2f, r.height), color);
        }

        // ---------------------------------------------------------------- 선택·크기·인라인 편집

        /// <summary>
        /// 그룹 키로 그룹 순서 번호를 찾는다.
        /// doc과 key를 사용하며, 번호 또는 -1을 반환한다.
        /// </summary>
        private static int GroupIndex(DialogueEditorDocument doc, string key)
        {
            if (key == null) return -1;
            for (int i = 0; i < doc.Groups.Count; i++) if (KeyOf(doc.Groups[i]) == key) return i;
            return -1;
        }

        /// <summary>
        /// 그룹 번호의 그룹 상자를 찾는다.
        /// boxes와 group을 사용하며, 상자 또는 null을 반환한다.
        /// </summary>
        private static Box BoxOf(List<Box> boxes, int group)
        {
            foreach (Box b in boxes) if (b.Kind == BoxKind.Group && b.Group == group) return b;
            return null;
        }

        /// <summary>
        /// 노드 뷰 안을 클릭하면 노드·엔딩 카드·행을 선택하고, 빈 곳을 클릭하면 선택을 푼다. 뷰 밖을 클릭하면 키 입력을 받지 않는다.
        /// 이벤트는 사용 처리하지 않아 끌기·연결과 함께 동작한다. e, doc, boxes, inside를 사용하며, 선택 상태를 바꾼다.
        /// </summary>
        private void HandleSelection(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside)
        {
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (!inside)
            {
                _focused = false;
                if (!_inlineAwait) CancelInline();
                return;
            }
            _focused = true;
            if (_inline != InlineKind.None)
            {
                Rect field = InlineRect(doc, boxes);
                if (field.Contains(e.mousePosition)) return;
                if (!_inlineAwait) CancelInline();
            }
            Box hit = HitBox(boxes, e.mousePosition);
            if (hit == null)
            {
                // 0은 선택이 풀려 텍스트 강조도 지운다는 뜻이다.
                if (_selKey != null) _selectionLine = 0;
                _selKey = null;
                _selRow = -1;
                return;
            }
            DialogueEditorDocument.Group g = doc.Groups[hit.Group];
            int row = hit.Kind == BoxKind.Group ? RowAt(doc, hit, e.mousePosition.y) : -1;
            _selKey = KeyOf(g);
            _selRow = row;
            _selectionLine = row >= 0 ? g.Items[row].TextLine : g.TextLine;
        }

        /// <summary>
        /// 그룹 상자 오른쪽 아래 모서리를 끌어 크기를 바꾸고, 놓을 때 저장한다. 크기는 배치 좌표계로 기억해 확대율과 상관없다.
        /// e, doc, boxes, inside를 사용하며, _resizeKey와 _resizeSize를 바꾼다.
        /// </summary>
        private void HandleResize(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!inside || e.button != 0 || _inline != InlineKind.None) return;
                    Box hit = HitBox(boxes, e.mousePosition);
                    if (hit == null || hit.Kind != BoxKind.Group || !ResizeHandle(hit).Contains(e.mousePosition)) return;
                    _resizeKey = KeyOf(doc.Groups[hit.Group]);
                    _resizeSize = hit.Canvas.size;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (_resizeKey == null) return;
                    _resizeSize += e.delta / _zoom;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (_resizeKey == null) return;
                    int g = GroupIndex(doc, _resizeKey);
                    if (g >= 0) SizeSaver?.Invoke(_resizeKey, ClampSize(doc.Groups[g], _resizeSize));
                    _resizeKey = null;
                    e.Use();
                    break;
            }
        }

        /// <summary>
        /// 키 입력을 처리한다. 인라인 편집 중에는 Enter로 확정하고 Escape로 취소하며, 아니면 노드 뷰가 선택된 상태에서 F2로 ID를, Enter로 행 본문을 고치기 시작한다.
        /// e, doc, editable을 사용하며, 확정해서 바뀐 복사본이 있으면 반환한다.
        /// </summary>
        private DialogueEditorDocument HandleKeys(Event e, DialogueEditorDocument doc, bool editable)
        {
            if (e.type != EventType.KeyDown) return null;
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (_inline != InlineKind.None)
            {
                if (e.character == '\n' || e.character == '\r') { e.Use(); return null; }
                if (_inlineAwait) { if (enter || e.keyCode == KeyCode.Escape) e.Use(); return null; }
                if (e.keyCode == KeyCode.Escape) { CancelInline(); e.Use(); return null; }
                if (enter) { e.Use(); return ConfirmInline(doc); }
                return null;
            }
            if (!_focused || _selKey == null) return null;
            if (e.keyCode == KeyCode.F2) { e.Use(); StartRename(doc, editable); }
            else if (enter) { e.Use(); StartBodyEdit(doc, editable); }
            return null;
        }

        /// <summary>
        /// 선택한 그룹의 ID 인라인 편집을 시작한다. 공용 Strings의 key, 런타임 예약 ID, 텍스트 오류·검사 대기 중에는 시작하지 않고 이유를 보인다.
        /// doc과 editable을 사용하며, 인라인 상태를 바꾼다.
        /// </summary>
        private void StartRename(DialogueEditorDocument doc, bool editable)
        {
            int idx = GroupIndex(doc, _selKey);
            if (idx < 0) return;
            DialogueEditorDocument.Group g = doc.Groups[idx];
            if (doc.Kind == DialogueEditorKind.Strings) { Notify("공용 Strings의 key는 코드가 참조하므로 F2로 바꿀 수 없다."); return; }
            if (IsReservedId(g)) { Notify($"'{g.Id}'는 게임이 직접 부르는 예약 ID라 바꿀 수 없다."); return; }
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 바꿀 수 있다."); return; }
            _inline = InlineKind.Rename;
            _inlineKey = _selKey;
            _inlineRow = -1;
            _inlineText = g.Id;
            _inlineError = "";
            _inlineFocus = true;
        }

        /// <summary>
        /// 선택한 행의 본문 인라인 편집을 시작한다. 행을 고르지 않았거나 텍스트 오류·검사 대기 중이면 시작하지 않고 이유를 보인다.
        /// doc과 editable을 사용하며, 인라인 상태와 행 스크롤을 바꾼다.
        /// </summary>
        private void StartBodyEdit(DialogueEditorDocument doc, bool editable)
        {
            int idx = GroupIndex(doc, _selKey);
            if (idx < 0) return;
            if (_selRow < 0 || _selRow >= doc.Groups[idx].Items.Count) { Notify("대사 행을 클릭해 고른 뒤 Enter로 본문을 고친다."); return; }
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 고칠 수 있다."); return; }
            _inline = InlineKind.Body;
            _inlineKey = _selKey;
            _inlineRow = _selRow;
            _inlineText = DialogueEditorFormat.Body(doc.Groups[idx].Items[_selRow].Get(DialogueEditorFormat.TEXT));
            _inlineError = "";
            _inlineFocus = true;
            // 고치는 행이 상자 안에서 보이도록 스크롤한다.
            Box b = BoxOf(_lastBoxes, idx);
            if (b != null && (_selRow < b.FirstRow || _selRow >= b.FirstRow + b.VisibleRows)) _rowScroll[_selKey] = _selRow;
        }

        /// <summary>
        /// 게임 코드가 이름으로 부르는 예약 그룹인지 본다. Talk의 enter·reveal, Ending의 truth·half·leave와 @로 시작하는 문구다.
        /// g를 사용하며, 예약 ID면 true를 반환한다.
        /// </summary>
        private static bool IsReservedId(DialogueEditorDocument.Group g)
        {
            if (g.Section == Csv.SECTION_TALK) return g.Id == TalkScript.Enter || g.Id == TalkScript.Reveal;
            if (g.Section == Csv.SECTION_ENDING) return g.Id == "truth" || g.Id == "half" || g.Id == "leave" || g.Id.StartsWith("@");
            return false;
        }

        /// <summary>
        /// 인라인 입력을 확정한다. 입력 규칙에 맞지 않으면 입력창을 그대로 두고 이유를 보인다.
        /// 맞으면 바뀐 복사본을 만들어 창의 검사를 기다린다. doc을 사용하며, 복사본 또는 null을 반환한다.
        /// </summary>
        private DialogueEditorDocument ConfirmInline(DialogueEditorDocument doc)
        {
            int idx = GroupIndex(doc, _inlineKey);
            if (idx < 0) { CancelInline(); return null; }
            DialogueEditorDocument.Group g = doc.Groups[idx];
            string text = _inlineText.Trim();
            if (_inline == InlineKind.Rename)
            {
                if (text.Length == 0) { _inlineError = "ID가 비어 있다."; return null; }
                if (!DialogueEditorFormat.IsValidId(text)) { _inlineError = "ID에 공백, 쉼표, 큰따옴표를 쓸 수 없다."; return null; }
                if (text == g.Id) { CancelInline(); return null; }
                if (doc.Find(g.Section, g.Actor, text) != null) { _inlineError = $"같은 단계에 '{text}'가 이미 있다."; return null; }
                DialogueEditorDocument renamed = RenameGroup(doc, idx, text);
                RenamedFrom = KeyOf(g);
                RenamedTo = KeyOf(renamed.Groups[idx]);
                _inlineAwait = true;
                return renamed;
            }
            if (_inlineRow < 0 || _inlineRow >= g.Items.Count) { CancelInline(); return null; }
            if (text.Length == 0 && doc.Kind != DialogueEditorKind.Strings)
            {
                _inlineError = g.Section == Csv.SECTION_DIALOGUE ? "빈 선택지는 저장할 수 없다." : "빈 대사는 저장할 수 없다.";
                return null;
            }
            if (text == g.Items[_inlineRow].Get(DialogueEditorFormat.TEXT)) { CancelInline(); return null; }
            DialogueEditorDocument copy = doc.Clone();
            copy.Groups[idx].Items[_inlineRow].Fields[DialogueEditorFormat.TEXT] = text;
            RenamedFrom = RenamedTo = null;
            _inlineAwait = true;
            return copy;
        }

        /// <summary>
        /// 그룹 ID를 바꾼 복사본을 만든다. Dialogue 장면이면 같은 actor의 모든 goto를, 엔딩이면 같은 actor 선택지의 모든 ending을 함께 바꾼다.
        /// doc, group, newId를 사용하며, 새 문서를 반환한다.
        /// </summary>
        public static DialogueEditorDocument RenameGroup(DialogueEditorDocument doc, int group, string newId)
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Group g = copy.Groups[group];
            string old = g.Id;
            g.Id = newId;
            string refColumn = g.Section == Csv.SECTION_DIALOGUE ? DialogueEditorFormat.GOTO : g.Section == Csv.SECTION_ENDING ? DialogueEditorFormat.ENDING : null;
            if (refColumn == null) return copy;
            foreach (DialogueEditorDocument.Group other in copy.Groups)
            {
                if (other.Section != Csv.SECTION_DIALOGUE || other.Actor != g.Actor) continue;
                foreach (DialogueEditorDocument.Item i in other.Items)
                    if (i.Get(refColumn) == old) i.Fields[refColumn] = newId;
            }
            return copy;
        }

        /// <summary>
        /// 인라인 입력창이 놓일 화면 사각형을 구한다. ID는 상자 머리, 본문은 그 행 자리다.
        /// doc과 boxes를 사용하며, 사각형(없으면 크기 0)을 반환한다.
        /// </summary>
        private Rect InlineRect(DialogueEditorDocument doc, List<Box> boxes)
        {
            Box b = BoxOf(boxes, GroupIndex(doc, _inlineKey));
            if (b == null) return Rect.zero;
            if (_inline == InlineKind.Rename) return new Rect(b.Rect.x + Z(2f), b.Rect.y + Z(2f), b.Rect.width - Z(4f), Z(HEADER_HEIGHT - 4f));
            return RowRect(b, _inlineRow);
        }

        /// <summary>
        /// 선택한 그룹 상자와 행에 선택 테두리를 그린다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawSelection(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (Event.current.type != EventType.Repaint || _selKey == null) return;
            Box b = BoxOf(boxes, GroupIndex(doc, _selKey));
            if (b == null) return;
            Color c = _focused ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.82f, 0.25f, 0.45f);
            DrawOutline(b.Rect, c);
            if (_selRow >= b.FirstRow && _selRow < b.FirstRow + b.VisibleRows)
            {
                Rect r = RowRect(b, _selRow);
                EditorGUI.DrawRect(new Rect(r.x, r.y, Z(3f), r.height), c);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            }
        }

        /// <summary>
        /// 인라인 입력창과 오류 이유를 그린다. 입력은 Enter로 확정하고 Escape로 취소한다.
        /// doc과 boxes를 사용하며, _inlineText를 바꾼다.
        /// </summary>
        private void DrawInline(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (_inline == InlineKind.None) return;
            Rect r = InlineRect(doc, boxes);
            if (r.width <= 0f) return;
            var style = new GUIStyle(EditorStyles.textField) { fontSize = Mathf.Max(6, Mathf.RoundToInt(11f * _zoom)) };
            GUI.SetNextControlName(INLINE_CONTROL);
            _inlineText = EditorGUI.TextField(r, _inlineText, style);
            if (_inlineFocus)
            {
                EditorGUI.FocusTextInControl(INLINE_CONTROL);
                _inlineFocus = false;
            }
            string hint = _inlineError.Length > 0 ? _inlineError : "Enter 확정 · Esc 취소";
            var hr = new Rect(r.x, r.yMax + 2f, Mathf.Max(r.width, 220f), 18f);
            EditorGUI.DrawRect(hr, _inlineError.Length > 0 ? new Color(0.55f, 0.15f, 0.12f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.9f));
            GUI.Label(new Rect(hr.x + 4f, hr.y + 1f, hr.width - 8f, hr.height), hint, EditorStyles.whiteMiniLabel);
        }

        // ---------------------------------------------------------------- 우클릭 추가

        /// <summary>
        /// 그룹 노드나 그 안의 행을 우클릭하면 추가 메뉴를 띄운다. 행 위면 그 행 뒤에, 그 밖이면 그룹 끝에 넣는다.
        /// 편집할 수 없을 때와 선택지가 3개인 Dialogue 노드에서는 메뉴 항목을 비활성화한다.
        /// e, doc, boxes, inside, editable, windowMouse(창 좌표의 마우스 위치, 메뉴를 띄울 곳)를 사용하며, 고른 결과를 _pending에 둔다.
        /// </summary>
        private void HandleContextMenu(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside, bool editable, Vector2 windowMouse)
        {
            if (e.type != EventType.ContextClick || !inside) return;
            Box hit = HitBox(boxes, e.mousePosition);
            if (hit == null || hit.Kind != BoxKind.Group) return;
            BuildAddMenu(doc, hit.Group, RowAt(doc, hit, e.mousePosition.y), editable).DropDown(new Rect(windowMouse, Vector2.zero));
            e.Use();
        }

        /// <summary>
        /// 그룹 또는 행의 추가 메뉴를 만든다. 행이 있으면 그 행 뒤에, 없으면(-1) 그룹 끝에 넣는 항목이다.
        /// doc, group, row, editable을 사용하며, 메뉴를 반환한다. 항목을 고르면 결과를 _pending에 둔다.
        /// </summary>
        private GenericMenu BuildAddMenu(DialogueEditorDocument doc, int group, int row, bool editable)
        {
            DialogueEditorDocument.Group g = doc.Groups[group];
            int index = row >= 0 ? row + 1 : g.Items.Count;
            string what = AddLabel(doc, g);
            string label = row >= 0 ? $"{row + 1}번 행 뒤에 {what} 추가" : $"'{g.Id}' 끝에 {what} 추가";
            bool full = g.Section == Csv.SECTION_DIALOGUE && g.Items.Count >= 3;
            var menu = new GenericMenu();
            if (!editable || full) menu.AddDisabledItem(new GUIContent(label + (full ? " (선택지 3개)" : " (텍스트 오류 또는 검사 대기 중)")));
            else menu.AddItem(new GUIContent(label), false, () => _pending = InsertRow(doc, group, index));
            return menu;
        }

        /// <summary>
        /// 그룹에 넣을 새 행의 이름을 정한다.
        /// doc과 g를 사용하며, 선택지·대사·문구 중 하나를 반환한다.
        /// </summary>
        private static string AddLabel(DialogueEditorDocument doc, DialogueEditorDocument.Group g)
        {
            if (g.Section == Csv.SECTION_DIALOGUE) return "선택지";
            return doc.Kind == DialogueEditorKind.Strings ? "문구" : "대사";
        }

        /// <summary>
        /// 그룹의 지정 위치에 새 행을 넣은 복사본을 만든다. 새 선택지는 글 '새 선택지'와 수치 0, 다른 행은 '새 대사' 또는 '새 문구'다.
        /// doc, group, index를 사용하며, 새 문서를 반환한다.
        /// </summary>
        public static DialogueEditorDocument InsertRow(DialogueEditorDocument doc, int group, int index)
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Group g = copy.Groups[group];
            var item = new DialogueEditorDocument.Item();
            if (g.Section == Csv.SECTION_DIALOGUE)
            {
                item.Fields[DialogueEditorFormat.TEXT] = "새 선택지";
                foreach (string c in new[] { "trust", "dilation", "spring" }) item.Fields[c] = "0";
            }
            else item.Fields[DialogueEditorFormat.TEXT] = doc.Kind == DialogueEditorKind.Strings ? "새 문구" : "새 대사";
            g.Items.Insert(Mathf.Clamp(index, 0, g.Items.Count), item);
            return copy;
        }

        // ---------------------------------------------------------------- 화면 조작

        /// <summary>
        /// 빈 곳을 끌면 화면을 옮기고, 상자 머리를 끌면 그 상자만 화면에서 옮긴다. 둘 다 저장하지 않는다.
        /// e, doc, boxes, inside(마우스가 뷰 안에 있는지)를 사용하며, _pan과 _nodeOffsets를 바꾼다.
        /// </summary>
        private void HandleViewInput(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (!inside) break;
                    if (e.button == 2 || (e.button == 0 && HitBox(boxes, e.mousePosition) == null)) { _panning = true; e.Use(); break; }
                    if (e.button != 0) break;
                    Box hit = HitBox(boxes, e.mousePosition);
                    if (hit != null && _inline == InlineKind.None && (hit.Kind == BoxKind.After || e.mousePosition.y < hit.Rect.y + Z(HEADER_HEIGHT)))
                    {
                        _dragNode = KeyOf(doc.Groups[hit.Group]) + (hit.Kind == BoxKind.After ? "#after" : "");
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_panning) { _pan += e.delta; e.Use(); }
                    else if (_dragNode != null)
                    {
                        // 노드 옮김은 배치 좌표계에 기억하므로 화면에서 움직인 거리를 확대율로 나눈다.
                        _nodeOffsets.TryGetValue(_dragNode, out Vector2 o);
                        _nodeOffsets[_dragNode] = o + e.delta / _zoom;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_panning || _dragNode != null) e.Use();
                    _panning = false;
                    _dragNode = null;
                    break;
            }
        }

        /// <summary>
        /// 행이 넘치는 그룹 상자 위에서 휠을 돌리면 상자 안의 행을 스크롤한다.
        /// e를 사용하며, 스크롤했으면 true를 반환하고 _rowScroll을 바꾼다.
        /// </summary>
        private bool ScrollRows(Event e)
        {
            Box hit = HitBox(_lastBoxes, e.mousePosition);
            if (hit == null || hit.Kind != BoxKind.Group || _lastDoc == null || hit.Group >= _lastDoc.Groups.Count) return false;
            DialogueEditorDocument.Group g = _lastDoc.Groups[hit.Group];
            int max = g.Items.Count - hit.VisibleRows;
            if (max <= 0) return false;
            _rowScroll[KeyOf(g)] = Mathf.Clamp(hit.FirstRow + (e.delta.y > 0f ? 1 : -1), 0, max);
            return true;
        }

        /// <summary>
        /// Ctrl(또는 Cmd)+휠로 확대율을 0.1씩 바꾸고, 포인터 아래 배치 좌표가 같은 화면 위치에 머물도록 화면 이동을 고친다.
        /// 일반 휠은 화면을 위아래로 옮긴다. e와 inside를 사용하며, _zoom과 _pan을 바꾼다.
        /// </summary>
        private void HandleZoom(Event e, bool inside)
        {
            if (e.type != EventType.ScrollWheel || !inside) return;
            if (!(e.control || e.command))
            {
                if (!ScrollRows(e)) _pan -= new Vector2(0f, e.delta.y * 20f);
                e.Use();
                return;
            }
            float next = Mathf.Clamp(Mathf.Round((_zoom + (e.delta.y < 0f ? ZOOM_STEP : -ZOOM_STEP)) * 10f) / 10f, MIN_ZOOM, MAX_ZOOM);
            if (!Mathf.Approximately(next, _zoom))
            {
                Vector2 canvas = (e.mousePosition - _pan) / _zoom;
                _zoom = next;
                _pan = e.mousePosition - canvas * _zoom;
            }
            e.Use();
        }

        /// <summary>
        /// 마우스 위치의 상자를 찾는다. 나중에 그린(위에 보이는) 상자를 먼저 본다.
        /// boxes와 p를 사용하며, 상자 또는 null을 반환한다.
        /// </summary>
        private static Box HitBox(List<Box> boxes, Vector2 p)
        {
            for (int i = boxes.Count - 1; i >= 0; i--) if (boxes[i].Rect.Contains(p)) return boxes[i];
            return null;
        }
    }
}
