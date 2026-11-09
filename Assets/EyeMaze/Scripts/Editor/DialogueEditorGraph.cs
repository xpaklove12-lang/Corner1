using System.Collections.Generic;
using System.Globalization;

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
        // Dialogue 선택지는 본문과 조건·수치 배지를 표시하고 결과는 별도 자식 노드에 둔다.
        private const float CHOICE_ROW_HEIGHT = 50f;
        private const float RESPONSE_HEIGHT = 70f;
        private const float RESPONSE_LINE_HEIGHT = 34f;
        private const float FRAGMENT_HEIGHT = 82f;
        private const float NODE_PADDING = 6f;
        private const float JUDGE_WIDTH = 170f;
        private const float JUDGE_HEIGHT = 26f;
        private const float JUDGE_GAP = 90f;
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
            Response,
            After,
            Fragment,
            /// <summary>엔딩 조건의 기준값 하나 (신뢰 기준, 부담 기준)</summary>
            Value,
            /// <summary>선택지의 신뢰·동공 변화량 배지</summary>
            Badge,
            /// <summary>직접 엔딩 조건에 더할 새 조건 변수(Flag ID)</summary>
            NewFlag,
            /// <summary>직접 엔딩 조건에 든 수치 계산 플래그의 비교와 수치</summary>
            Compare,
        }

        /// <summary>트리에서 눌러 고칠 수 있는 조건·수치 항목</summary>
        private enum EditTarget
        {
            None,
            TruthTrust,
            KeyFlag,
            LeaveTrust,
            BurdenLimit,
            Need,
        }

        /// <summary>진입선 조건이나 수치 배지 글의 한 조각. 고칠 수 있는 조각은 눌러서 편집한다.</summary>
        private class Segment
        {
            public string Text;
            public EditTarget Target;
            /// <summary>고칠 그룹 번호 (조건 기준값은 truth·leave 그룹, 선택지 항목은 그 Dialogue 그룹)</summary>
            public int Group = -1;
            /// <summary>고칠 선택지 행 번호 (선택지 항목이 아니면 -1)</summary>
            public int Item = -1;
            public Rect Rect;
        }

        private static readonly Color GOTO_COLOR = new Color(0.45f, 0.75f, 1f);
        private static readonly Color ENDING_COLOR = new Color(1f, 0.6f, 0.3f);
        private static readonly Color GUIDE_COLOR = new Color(0.65f, 0.65f, 0.7f);
        private static readonly Color FRAGMENT_COLOR = new Color(0.42f, 0.78f, 0.86f);

        /// <summary>상자 종류</summary>
        private enum BoxKind
        {
            Group,
            After,
            Response,
            Fragment,
            /// <summary>심연 핵심 접촉 판정 (truth·half로 갈라짐). CSV 그룹이 아닌 표시용 카드다.</summary>
            Core,
            /// <summary>상담 중 판정 (낮은 신뢰·부담으로 leave). CSV 그룹이 아닌 표시용 카드다.</summary>
            Gaze,
        }

        /// <summary>화면에 놓인 상자 하나 (그룹 노드 또는 엔딩의 after 카드)</summary>
        private class Box
        {
            public BoxKind Kind;
            public int Group = -1;
            public int Item = -1;
            public MemoryFragment Fragment;
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
            /// <summary>행 하나의 높이 (배치 좌표계)</summary>
            public float RowH = ROW_HEIGHT;
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
        private bool _selResponse;
        // 결과 카드에서 고른 결과 줄 (_selResponse일 때)
        private int _selLine;
        // 고치는 중인 결과 줄 (InlineKind.Response일 때)
        private int _inlineLine;
        private bool _selAfter;
        private MemoryFragment _selFragment;
        private int _selectionLine = -1;
        // 마지막 클릭이 노드 뷰 안이었는지. F2와 Enter는 이때만 받는다.
        private bool _focused;
        private InlineKind _inline;
        private string _inlineKey;
        private MemoryFragment _inlineFragment;
        private int _inlineRow = -1;
        private string _inlineText = "";
        private string _inlineColumn;
        // 빈 곳 우클릭으로 추가해 저장 결과를 기다리는 그룹 키
        private string _createdKey;
        // 고치는 중인 엔딩 기준값 (InlineKind.Value일 때)
        private EditTarget _inlineTarget;
        // 비교·수치를 고치는 중인 원래 계산 플래그 (InlineKind.Compare일 때)
        private string _inlineFlag;
        // 이번 그리기의 엔딩 진입선 조건 조각. 입력 판정과 그리기에 함께 쓴다.
        private List<Segment> _segments = new List<Segment>();
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
        private GUIStyle _responseStyle;
        private GUIStyle _scoreStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _situationStyle;
        private float _styleZoom = -1f;

        /// <summary>노드 뷰 확대율 (0.5~2.0, 파일에 저장하지 않음)</summary>
        public float Zoom => _zoom;

        /// <summary>그룹 키로 저장된 노드 크기를 읽는다. 없으면 null이다. 창이 EditorPrefs로 채운다.</summary>
        public System.Func<string, Vector2?> SizeLoader { get; set; }

        /// <summary>그룹 키의 노드 크기를 저장한다. 창이 EditorPrefs로 채운다.</summary>
        public System.Action<string, Vector2> SizeSaver { get; set; }

        /// <summary>파편 Flag ID의 획득 문구를 Flags.csv에 저장한다.</summary>
        public System.Func<string, string, (bool ok, string error)> FragmentTextSaver { get; set; }
        /// <summary>손님과 층 파편 묶음을 받아 새 기억 파편을 만드는 창의 함수. 결과로 성공 여부, 오류, 새 파편을 돌려준다.</summary>
        public System.Func<ClientCase, GameObject, (bool ok, string error, MemoryFragment fragment)> FragmentCreator { get; set; }
        /// <summary>Flag ID를 받아 Flags.csv에 새 플래그 줄을 넣는 창의 함수. 결과로 성공 여부와 오류를 돌려준다.</summary>
        public System.Func<string, string, string, string, (bool ok, string error)> FlagCreator { get; set; }

        /// <summary>마지막으로 확정한 ID 변경의 이전·새 그룹 키 (없으면 null). 창이 크기 설정 키를 옮길 때 쓴다.</summary>
        public string RenamedFrom { get; private set; }
        public string RenamedTo { get; private set; }

        /// <summary>인라인 편집 중인지</summary>
        public bool IsInlineEditing => _inline != InlineKind.None;

        /// <summary>인라인 입력창에 포커스를 주는 중이라 한 번 더 그려야 하는지</summary>
        public bool WantsRepaint => _inlineFocus;

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
            _selResponse = false;
            _selAfter = false;
            _selFragment = null;
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
            ReleaseInlineFocus();
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
            if (_inline != InlineKind.None) ReleaseInlineFocus();
            _inline = InlineKind.None;
            _inlineAwait = false;
            _inlineError = "";
            RenamedFrom = RenamedTo = null;
        }

        /// <summary>
        /// 닫는 인라인 입력창의 키보드 포커스와 입력 상태를 끝낸다. 남겨 두면 그 글이 다른 입력창으로 넘어갈 수 있다.
        /// 입력값은 없으며, 키보드 포커스를 비운다.
        /// </summary>
        private void ReleaseInlineFocus()
        {
            _inlineFocus = false;
            GUIUtility.keyboardControl = 0;
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
            BuildSegments(doc, boxes);

            // 입력은 클립 밖에서 노드 뷰 좌표로 바꿔 처리한다. 클립 안에서는 뷰 밖에서 놓은 마우스 이벤트를 받지 못한다.
            Event e = Event.current;
            Vector2 windowMouse = e.mousePosition;
            e.mousePosition = windowMouse - rect.position;
            _control = GUIUtility.GetControlID(FocusType.Passive);
            bool inside = new Rect(0f, 0f, rect.width, rect.height).Contains(e.mousePosition);
            DialogueEditorDocument changed = HandleKeys(e, doc, editable);
            HandleSegmentInput(e, doc, inside, editable, windowMouse);
            HandleScoreInput(e, doc, boxes, inside, editable, windowMouse);
            HandleResize(e, doc, boxes, inside);
            HandleSelection(e, doc, boxes, inside, editable);
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
            DrawSegments();
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
        /// 그룹과 보조 카드의 위치를 서로 다른 키로 보관한다.
        /// </summary>
        private static string OffsetKey(DialogueEditorDocument doc, Box box)
        {
            if (box.Kind == BoxKind.Fragment) return "fragment/" + box.Fragment.GetInstanceID();
            string key = KeyOf(doc.Groups[box.Group]);
            return box.Kind == BoxKind.Group ? key : key + "#" + box.Kind.ToString().ToLowerInvariant() + (box.Kind == BoxKind.Response ? "/" + box.Item : "");
        }

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
                _nodeOffsets.TryGetValue(OffsetKey(doc, b), out offset);
                b.Rect = new Rect((b.Canvas.position + offset) * _zoom + _pan, b.Canvas.size * _zoom);
                if (b.Kind != BoxKind.Group) continue;
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                float rowsArea = b.Canvas.height - HEADER_HEIGHT - SubHeight(g) - NODE_PADDING;
                b.RowsTop = b.Rect.y + Z(HEADER_HEIGHT + SubHeight(g));
                b.RowH = RowHeightOf(g);
                b.VisibleRows = Mathf.Max(1, Mathf.FloorToInt(rowsArea / b.RowH + 0.01f));
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
            float minHeight = HEADER_HEIGHT + SubHeight(g) + RowHeightOf(g) + NODE_PADDING;
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
                    foreach (int g in list)
                    {
                        int responses = 0;
                        foreach (DialogueEditorDocument.Item item in doc.Groups[g].Items)
                            if (item.Get("response").Length > 0) responses++;
                        float childrenWidth = responses > 0 ? responses * NODE_WIDTH + (responses - 1) * GAP_X : 0f;
                        width += Mathf.Max(SizeOf(doc.Groups[g]).x, childrenWidth);
                    }
                    float x = -width * 0.5f, h = 0f;
                    var placed = new List<Box>();
                    foreach (int g in list)
                    {
                        int responses = 0;
                        foreach (DialogueEditorDocument.Item item in doc.Groups[g].Items)
                            if (item.Get("response").Length > 0) responses++;
                        float childrenWidth = responses > 0 ? responses * NODE_WIDTH + (responses - 1) * GAP_X : 0f;
                        Vector2 size = SizeOf(doc.Groups[g]);
                        float span = Mathf.Max(size.x, childrenWidth);
                        Box b = Add(g, x + (span - size.x) * 0.5f, y, false);
                        placed.Add(b);
                        x += span + GAP_X;
                        h = Mathf.Max(h, b.Canvas.height);
                    }
                    if (first) { firstRowRight = x - GAP_X; firstRowY = y; first = false; }
                    float responseH = 0f;
                    foreach (Box parent in placed)
                    {
                        DialogueEditorDocument.Group group = doc.Groups[parent.Group];
                        var responseItems = new List<int>();
                        for (int item = 0; item < group.Items.Count; item++)
                            if (group.Items[item].Get("response").Length > 0) responseItems.Add(item);
                        if (responseItems.Count == 0) continue;
                        float childrenWidth = responseItems.Count * NODE_WIDTH + (responseItems.Count - 1) * GAP_X;
                        float childX = parent.Canvas.center.x - childrenWidth * 0.5f;
                        foreach (int item in responseItems)
                        {
                            float cardH = ResponseHeight(group.Items[item]);
                            boxes.Add(new Box { Kind = BoxKind.Response, Group = parent.Group, Item = item,
                                Canvas = new Rect(childX, y + h + GAP_Y * 0.5f, NODE_WIDTH, cardH) });
                            responseH = Mathf.Max(responseH, cardH);
                            childX += NODE_WIDTH + GAP_X;
                        }
                    }
                    y += h + (responseH > 0f ? GAP_Y * 0.5f + responseH : 0f) + GAP_Y;
                }
            }

            // 씬의 이 회차 손님이 가진 기억 파편을 상담 트리 왼쪽에 놓는다.
            ClientCase client = DialogueEditorFormat.CaseOf(doc.AssetPath);
            if (client != null)
            {
                float dialogueLeft = mainX;
                foreach (Box box in boxes)
                    if (box.Kind == BoxKind.Response || (box.Kind == BoxKind.Group && doc.Groups[box.Group].Section == Csv.SECTION_DIALOGUE))
                        dialogueLeft = Mathf.Min(dialogueLeft, box.Canvas.xMin);
                float fragmentX = dialogueLeft - NODE_WIDTH - GAP_X * 2f;
                float fragmentY = firstRowY;
                foreach (MemoryFragment fragment in client.Fragments())
                {
                    if (fragment == null) continue;
                    boxes.Add(new Box { Kind = BoxKind.Fragment, Fragment = fragment,
                        Canvas = new Rect(fragmentX, fragmentY, NODE_WIDTH, FRAGMENT_HEIGHT) });
                    fragmentY += FRAGMENT_HEIGHT + SIDE_GAP;
                }
                if (fragmentY > firstRowY) y = Mathf.Max(y, fragmentY + GAP_Y - SIDE_GAP);
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
                // 판정 카드(심연 핵심 접촉, 상담 중 판정)를 엔딩 줄 위에 두고 그 아래에 엔딩을 놓는다.
                int truth = doc.IndexOf(Csv.SECTION_ENDING, doc.Groups[endings[0]].Actor, EndingScript.TRUTH);
                int half = doc.IndexOf(Csv.SECTION_ENDING, doc.Groups[endings[0]].Actor, EndingScript.HALF);
                int leave = doc.IndexOf(Csv.SECTION_ENDING, doc.Groups[endings[0]].Actor, EndingScript.LEAVE);
                bool judge = truth >= 0 || half >= 0 || leave >= 0;
                float endY = judge ? y + JUDGE_HEIGHT + JUDGE_GAP : y;
                float width = (endings.Count - 1) * GAP_X;
                foreach (int g in endings) width += SizeOf(doc.Groups[g]).x;
                float x = -width * 0.5f;
                var centers = new Dictionary<int, float>();
                foreach (int g in endings)
                {
                    Box b = Add(g, x, endY, false);
                    centers[g] = b.Canvas.center.x;
                    if (doc.Groups[g].Get("after").Length > 0)
                        boxes.Add(new Box { Kind = BoxKind.After, Group = g, Canvas = new Rect(x, b.Canvas.yMax + 30f, b.Canvas.width, AFTER_HEIGHT) });
                    x += b.Canvas.width + GAP_X;
                }
                if (truth >= 0 || half >= 0)
                {
                    float cx = truth >= 0 && half >= 0 ? (centers[truth] + centers[half]) * 0.5f : centers[truth >= 0 ? truth : half];
                    boxes.Add(new Box { Kind = BoxKind.Core, Group = truth >= 0 ? truth : half, Canvas = new Rect(cx - JUDGE_WIDTH * 0.5f, y, JUDGE_WIDTH, JUDGE_HEIGHT) });
                }
                if (leave >= 0)
                    boxes.Add(new Box { Kind = BoxKind.Gaze, Group = leave, Canvas = new Rect(centers[leave] - JUDGE_WIDTH * 0.5f, y, JUDGE_WIDTH, JUDGE_HEIGHT) });
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
            return HEADER_HEIGHT + SubHeight(g) + Mathf.Max(1, g.Items.Count) * RowHeightOf(g) + NODE_PADDING;
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
                if (b.Kind == BoxKind.Response || b.Kind == BoxKind.Fragment) continue;
                if (b.Kind == BoxKind.Core || b.Kind == BoxKind.Gaze)
                {
                    // 판정 카드에서 엔딩으로 가는 진입선. 조건 글은 DrawSegments가 선 위에 그린다.
                    string actor = doc.Groups[b.Group].Actor;
                    string[] targets = b.Kind == BoxKind.Core ? new[] { EndingScript.TRUTH, EndingScript.HALF } : new[] { EndingScript.LEAVE, EndingScript.LEAVE };
                    for (int lane = 0; lane < targets.Length; lane++)
                    {
                        int t = doc.IndexOf(Csv.SECTION_ENDING, actor, targets[lane]);
                        if (t < 0 || !byGroup.TryGetValue(t, out Box target)) continue;
                        (Vector2 from, Vector2 to) = JudgeLane(b, target, lane);
                        DrawArrow(from, to, ENDING_COLOR, false);
                    }
                    continue;
                }
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
                    Box response = boxes.Find(candidate => candidate.Kind == BoxKind.Response && candidate.Group == b.Group && candidate.Item == k);
                    if (response != null)
                    {
                        DrawArrow(from, TopCenter(response.Rect), GUIDE_COLOR, true);
                        from = BottomCenter(response.Rect);
                    }
                    int t = doc.IndexOf(Csv.SECTION_DIALOGUE, g.Actor, g.Items[k].Get(DialogueEditorFormat.GOTO));
                    if (t >= 0 && byGroup.TryGetValue(t, out Box target)) DrawArrow(from, TopCenter(target.Rect), GOTO_COLOR, true);
                    int e = doc.IndexOf(Csv.SECTION_ENDING, g.Actor, g.Items[k].Get(DialogueEditorFormat.ENDING));
                    if (e >= 0 && byGroup.TryGetValue(e, out Box endBox)) DrawArrow(from, TopCenter(endBox.Rect), ENDING_COLOR, true);
                }
            }

            // 파편의 Flag가 선택지 need 또는 truth의 key_flag에 쓰일 때 의존선을 그린다.
            foreach (Box fragment in boxes)
            {
                if (fragment.Kind != BoxKind.Fragment || fragment.Fragment == null || string.IsNullOrEmpty(fragment.Fragment.Flag)) continue;
                string flag = fragment.Fragment.Flag;
                Vector2 from = new Vector2(fragment.Rect.xMax, fragment.Rect.center.y);
                foreach (Box target in boxes)
                {
                    if (target.Kind != BoxKind.Group) continue;
                    DialogueEditorDocument.Group group = doc.Groups[target.Group];
                    if (group.Section == Csv.SECTION_DIALOGUE)
                    {
                        for (int item = 0; item < group.Items.Count; item++)
                        {
                            if (!HasFlag(group.Items[item].Get("need"), flag)) continue;
                            Vector2 to = new Vector2(target.Rect.xMin, PortPosition(target, item).y);
                            DrawArrow(from, to, FRAGMENT_COLOR, true);
                        }
                    }
                    else if (group.Section == Csv.SECTION_ENDING && group.Id == EndingScript.TRUTH && group.Get("key_flag") == flag)
                        DrawArrow(from, new Vector2(target.Rect.xMin, target.Rect.center.y), FRAGMENT_COLOR, true);
                }
            }
        }

        /// <summary>
        /// 파이프로 구분한 플래그 목록에 지정한 ID가 있는지 확인한다.
        /// </summary>
        private static bool HasFlag(string list, string flag)
        {
            foreach (string part in (list ?? "").Split('|'))
                if (part.Trim() == flag) return true;
            return false;
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
            return new Vector2(b.Rect.xMax - Z(9f), b.RowsTop + Z(shown * b.RowH + b.RowH * 0.5f));
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
            if (b.Kind == BoxKind.Fragment)
            {
                MemoryFragment fragment = b.Fragment;
                EditorGUI.DrawRect(b.Rect, new Color(0.16f, 0.27f, 0.30f));
                Rect title = new Rect(b.Rect.x, b.Rect.y, b.Rect.width, Z(HEADER_HEIGHT));
                EditorGUI.DrawRect(title, new Color(0.20f, 0.43f, 0.47f));
                GUI.Label(new Rect(title.x + Z(6f), title.y + Z(3f), title.width - Z(12f), title.height),
                    "기억 파편 · " + fragment.Title, _headerStyle);
                GUI.Label(new Rect(b.Rect.x + Z(6f), title.yMax + Z(3f), b.Rect.width - Z(12f), Z(16f)), fragment.Flag, _responseStyle);
                GUI.Label(new Rect(b.Rect.x + Z(6f), title.yMax + Z(21f), b.Rect.width - Z(12f), b.Rect.height - Z(HEADER_HEIGHT + 25f)),
                    new GUIContent(Flat(fragment.Text), fragment.Text), _responseStyle);
                return;
            }
            if (b.Kind == BoxKind.Core || b.Kind == BoxKind.Gaze)
            {
                // 판정 카드는 CSV 그룹이 아니라 엔딩으로 갈라지는 지점을 보여주는 표시용 카드다.
                EditorGUI.DrawRect(b.Rect, new Color(0.36f, 0.20f, 0.30f));
                string judgeTitle = b.Kind == BoxKind.Core ? "심연 핵심 접촉 · 판정" : "상담 중 판정";
                GUI.Label(new Rect(b.Rect.x + Z(6f), b.Rect.y + Z(5f), b.Rect.width - Z(12f), b.Rect.height - Z(6f)), judgeTitle, _headerStyle);
                return;
            }
            DialogueEditorDocument.Group g = doc.Groups[b.Group];
            if (b.Kind == BoxKind.Response)
            {
                EditorGUI.DrawRect(b.Rect, new Color(0.19f, 0.29f, 0.34f));
                Rect title = new Rect(b.Rect.x, b.Rect.y, b.Rect.width, Z(HEADER_HEIGHT));
                EditorGUI.DrawRect(title, new Color(0.22f, 0.40f, 0.48f));
                List<string> lines = DialogueEditorFormat.ResponseLines(g.Items[b.Item].Get("response"));
                GUI.Label(new Rect(title.x + Z(6f), title.y + Z(3f), title.width - Z(12f), title.height),
                    lines.Count > 1 ? $"결과 · 선택지 {b.Item + 1} · {lines.Count}줄" : $"결과 · 선택지 {b.Item + 1}", _headerStyle);
                for (int line = 0; line < lines.Count; line++)
                {
                    Rect r = ResponseLineRect(b, line);
                    // 줄 사이에 구분선을 그어 게임에서 한 번에 한 줄씩 나온다는 것을 보인다.
                    if (line > 0) EditorGUI.DrawRect(new Rect(r.x + Z(4f), r.y, r.width - Z(8f), 1f), new Color(1f, 1f, 1f, 0.12f));
                    GUI.Label(new Rect(r.x + Z(6f), r.y + Z(2f), r.width - Z(12f), r.height - Z(2f)), new GUIContent(lines[line], lines[line]), _responseStyle);
                }
                return;
            }
            if (b.Kind == BoxKind.After)
            {
                EditorGUI.DrawRect(b.Rect, new Color(0.32f, 0.24f, 0.18f));
                string after = Flat(g.Get("after"));
                GUI.Label(new Rect(b.Rect.x + Z(6f), b.Rect.y + Z(3f), b.Rect.width - Z(12f), b.Rect.height - Z(6f)),
                    new GUIContent("after · " + after, after), _situationStyle);
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
                if (g.Section == Csv.SECTION_DIALOGUE)
                {
                    float textWidth = row.width - Z(26f);
                    GUI.Label(new Rect(row.x + Z(4f), row.y, textWidth, Z(17f)), RowText(g, g.Items[k], k), _rowStyle);
                    string score = ScoreText(g.Items[k]);
                    GUI.Label(ScoreRect(row), new GUIContent(score, score.Replace("\n", " · ")), _scoreStyle);
                }
                else
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
            return new Rect(b.Rect.x + Z(4f), b.RowsTop + Z((k - b.FirstRow) * b.RowH + 1f), b.Rect.width - Z(8f), Z(b.RowH - 2f));
        }

        /// <summary>
        /// 선택지 행 아래쪽의 수치 표시 및 클릭 영역을 구한다.
        /// </summary>
        private Rect ScoreRect(Rect row)
        {
            return new Rect(row.x + Z(4f), row.y + Z(16f), row.width - Z(26f), Z(32f));
        }

        /// <summary>
        /// 선택지의 0이 아닌 수치 변화량을 한 줄로 만든다. 모두 0이면 편집 안내를 표시한다.
        /// </summary>
        private static string ScoreText(DialogueEditorDocument.Item item)
        {
            var parts = new List<string>();
            string trust = item.Get("trust");
            string dilation = item.Get("dilation");
            string spring = item.Get("spring");
            if (trust.Length > 0 && trust != "0") parts.Add("신뢰 " + Signed(trust));
            if (dilation.Length > 0 && float.TryParse(dilation, NumberStyles.Float, CultureInfo.InvariantCulture, out float d) && d != 0f)
                parts.Add("동공 " + Signed(dilation));
            string springText = spring.Length > 0 && spring != "0" ? "spring " + Signed(spring) + " (미적용)" : "";
            if (parts.Count == 0 && springText.Length == 0) return "수치 변화 없음 · 클릭해서 편집";
            string primary = string.Join(" · ", parts);
            return primary.Length == 0 ? springText + "  ✎" : springText.Length == 0 ? primary + "  ✎" : primary + "\n" + springText + "  ✎";
        }

        /// <summary>
        /// 양수 변화량 앞에 더하기 기호를 붙인다.
        /// </summary>
        private static string Signed(string value) => value.StartsWith("-") || value.StartsWith("+") ? value : "+" + value;

        /// <summary>
        /// 그룹 종류에 따라 한 행의 높이를 반환한다.
        /// </summary>
        private static float RowHeightOf(DialogueEditorDocument.Group group)
        {
            return group.Section == Csv.SECTION_DIALOGUE ? CHOICE_ROW_HEIGHT : ROW_HEIGHT;
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
            _responseStyle = new GUIStyle(_rowStyle) { wordWrap = true };
            _responseStyle.normal.textColor = new Color(0.7f, 0.78f, 0.85f);
            _scoreStyle = new GUIStyle(_rowStyle) { wordWrap = true };
            _scoreStyle.normal.textColor = new Color(0.98f, 0.79f, 0.40f);
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip, fontSize = Mathf.Max(5, Mathf.RoundToInt(11f * _zoom)) };
            _headerStyle.normal.textColor = Color.white;
            _situationStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, clipping = TextClipping.Clip, fontSize = mini };
            _situationStyle.normal.textColor = new Color(0.7f, 0.72f, 0.75f);
        }

        /// <summary>
        /// 선택지 아래 수치 줄을 누르면 편집할 수치를 고르는 메뉴를 연다.
        /// </summary>
        private void HandleScoreInput(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside, bool editable, Vector2 windowMouse)
        {
            if (!inside || e.type != EventType.MouseDown || e.button != 0 || _inline != InlineKind.None) return;
            Box hit = HitBox(boxes, e.mousePosition);
            if (hit == null || hit.Kind != BoxKind.Group || doc.Groups[hit.Group].Section != Csv.SECTION_DIALOGUE) return;
            int row = RowAt(doc, hit, e.mousePosition.y);
            if (row < 0 || !ScoreRect(RowRect(hit, row)).Contains(e.mousePosition)) return;
            _focused = true;
            _selKey = KeyOf(doc.Groups[hit.Group]);
            _selRow = row;
            _selResponse = false;
            _selAfter = false;
            _selFragment = null;
            _selectionLine = doc.Groups[hit.Group].Items[row].TextLine;
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 수치를 바꿀 수 있다."); e.Use(); return; }
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("신뢰 (trust)"), false, () => StartScoreEdit(doc, hit.Group, row, "trust"));
            menu.AddItem(new GUIContent("동공 (dilation)"), false, () => StartScoreEdit(doc, hit.Group, row, "dilation"));
            menu.AddItem(new GUIContent("spring (현재 실행 미적용)"), false, () => StartScoreEdit(doc, hit.Group, row, "spring"));
            menu.DropDown(new Rect(windowMouse, Vector2.zero));
            e.Use();
        }

        /// <summary>
        /// 선택한 수치 열의 현재 값을 선택지 아래에서 인라인으로 고친다.
        /// </summary>
        private void StartScoreEdit(DialogueEditorDocument doc, int group, int row, string column)
        {
            if (group < 0 || group >= doc.Groups.Count || row < 0 || row >= doc.Groups[group].Items.Count) return;
            _inline = InlineKind.Badge;
            _inlineKey = KeyOf(doc.Groups[group]);
            _inlineRow = row;
            _inlineColumn = column;
            _inlineText = doc.Groups[group].Items[row].Get(column);
            if (_inlineText.Length == 0) _inlineText = "0";
            _inlineError = "";
            _inlineFocus = true;
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
            int shown = Mathf.FloorToInt((y - b.RowsTop) / Z(b.RowH));
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
            int shown = Mathf.Clamp(Mathf.RoundToInt((y - b.RowsTop) / Z(b.RowH)), 0, b.VisibleRows);
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
                    float y = hover.RowsTop + Z((InsertIndex(doc, hover, _mouse.y) - hover.FirstRow) * hover.RowH);
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
        /// 이벤트는 사용 처리하지 않아 끌기·연결과 함께 동작한다. 대사 행을 더블 클릭하면 Enter처럼 본문 편집을 시작하고 그 클릭은 사용 처리한다.
        /// e, doc, boxes, inside, editable을 사용하며, 선택 상태를 바꾼다.
        /// </summary>
        private void HandleSelection(Event e, DialogueEditorDocument doc, List<Box> boxes, bool inside, bool editable)
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
                _selResponse = false;
                _selAfter = false;
                _selFragment = null;
                return;
            }
            if (hit.Kind == BoxKind.Fragment)
            {
                _selKey = null;
                _selRow = -1;
                _selResponse = false;
                _selAfter = false;
                _selFragment = hit.Fragment;
                _selectionLine = 0;
                if (e.clickCount == 2) { StartFragmentEdit(editable); e.Use(); }
                return;
            }
            _selFragment = null;
            DialogueEditorDocument.Group g = doc.Groups[hit.Group];
            int row = hit.Kind == BoxKind.Response ? hit.Item : hit.Kind == BoxKind.Group ? RowAt(doc, hit, e.mousePosition.y) : -1;
            _selKey = KeyOf(g);
            _selRow = row;
            _selResponse = hit.Kind == BoxKind.Response;
            _selLine = _selResponse ? ResponseLineAt(doc, hit, e.mousePosition.y) : -1;
            _selAfter = hit.Kind == BoxKind.After;
            _selectionLine = row >= 0 ? g.Items[row].TextLine : g.TextLine;
            if (e.clickCount == 2 && (row >= 0 || _selAfter))
            {
                if (_selAfter) StartAfterEdit(doc, editable);
                else if (_selResponse) StartResponseEdit(doc, editable);
                else StartBodyEdit(doc, editable);
                // 두 번째 클릭으로 카드 끌기나 화면 이동이 시작되지 않게 한다.
                e.Use();
            }
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
            // 에디터는 Delete 키를 Delete·SoftDelete 명령으로도 보낸다. 노드 뷰가 노드를 골랐을 때만 받는다.
            bool deleteCommand = (e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand)
                && (e.commandName == "Delete" || e.commandName == "SoftDelete");
            if (deleteCommand && _inline == InlineKind.None && _focused && _selKey != null)
            {
                bool execute = e.type == EventType.ExecuteCommand;
                e.Use();
                return execute ? DeleteSelected(doc, editable) : null;
            }
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
            if (!_focused) return null;
            if (_selFragment != null)
            {
                if (enter) { e.Use(); StartFragmentEdit(editable); }
                return null;
            }
            if (_selKey == null) return null;
            if (e.keyCode == KeyCode.Delete) { e.Use(); return DeleteSelected(doc, editable); }
            if (e.keyCode == KeyCode.F2 && !_selResponse) { e.Use(); StartRename(doc, editable); }
            else if (enter) { e.Use(); if (_selAfter) StartAfterEdit(doc, editable); else if (_selResponse) StartResponseEdit(doc, editable); else StartBodyEdit(doc, editable); }
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
        /// 선택한 결과 카드의 결과 줄 하나를 인라인으로 고친다.
        /// doc, editable, _selRow, _selLine을 사용하며, 인라인 상태를 바꾼다.
        /// </summary>
        private void StartResponseEdit(DialogueEditorDocument doc, bool editable)
        {
            int idx = GroupIndex(doc, _selKey);
            if (idx < 0 || _selRow < 0 || _selRow >= doc.Groups[idx].Items.Count) return;
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 고칠 수 있다."); return; }
            List<string> lines = DialogueEditorFormat.ResponseLines(doc.Groups[idx].Items[_selRow].Get("response"));
            if (lines.Count == 0) return;
            _inline = InlineKind.Response;
            _inlineKey = _selKey;
            _inlineRow = _selRow;
            _inlineLine = Mathf.Clamp(_selLine, 0, lines.Count - 1);
            _inlineText = lines[_inlineLine];
            _inlineError = "";
            _inlineFocus = true;
        }

        /// <summary>
        /// 선택한 엔딩의 after 문구를 카드 위에서 편집한다.
        /// </summary>
        private void StartAfterEdit(DialogueEditorDocument doc, bool editable)
        {
            int idx = GroupIndex(doc, _selKey);
            if (idx < 0 || doc.Groups[idx].Section != Csv.SECTION_ENDING) return;
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 고칠 수 있다."); return; }
            _inline = InlineKind.After;
            _inlineKey = _selKey;
            _inlineRow = -1;
            _inlineText = DialogueEditorFormat.Body(doc.Groups[idx].Get("after"));
            _inlineError = "";
            _inlineFocus = true;
        }

        /// <summary>
        /// 선택한 파편의 획득 문구를 카드에서 편집한다. 문구의 원본은 Flags.csv다.
        /// </summary>
        private void StartFragmentEdit(bool editable)
        {
            if (_selFragment == null || string.IsNullOrEmpty(_selFragment.Flag)) { Notify("파편에 Flag ID를 먼저 설정한다."); return; }
            if (!editable || FragmentTextSaver == null) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 고칠 수 있다."); return; }
            _inline = InlineKind.Fragment;
            _inlineFragment = _selFragment;
            _inlineText = DialogueEditorFormat.Body(_selFragment.Text);
            _inlineError = "";
            _inlineFocus = true;
        }

        /// <summary>
        /// 선택한 노드를 지운 복사본을 만든다. 결과 카드는 response를, after 카드는 after를, 그룹 노드는 그룹 전체를 지운다.
        /// 예약 ID, 공용 Strings key, 텍스트 오류·검사 대기 중이면 지우지 않고 이유를 보인다.
        /// doc과 editable을 사용하며, 새 문서(지우지 않으면 null)를 반환하고 선택을 푼다.
        /// </summary>
        private DialogueEditorDocument DeleteSelected(DialogueEditorDocument doc, bool editable)
        {
            int idx = GroupIndex(doc, _selKey);
            if (idx < 0) return null;
            DialogueEditorDocument.Group g = doc.Groups[idx];
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 지울 수 있다."); return null; }
            DialogueEditorDocument copy;
            if (_selResponse)
            {
                if (_selRow < 0 || _selRow >= g.Items.Count) return null;
                copy = WithResponse(doc, idx, _selRow, new List<string>());
                _selResponse = false;
                return copy;
            }
            if (_selAfter)
            {
                copy = WithGroupField(doc, idx, "after", "");
                _selAfter = false;
                return copy;
            }
            if (doc.Kind == DialogueEditorKind.Strings) { Notify("공용 Strings의 key는 코드가 참조하므로 지울 수 없다."); return null; }
            if (IsReservedId(g)) { Notify($"'{g.Id}'는 게임이 직접 부르는 예약 ID라 지울 수 없다."); return null; }
            copy = RemoveGroup(doc, idx);
            _selKey = null;
            _selRow = -1;
            _selectionLine = 0;
            return copy;
        }

        /// <summary>
        /// 그룹을 지운 복사본을 만든다. 같은 actor 선택지가 지운 장면을 가리키는 goto나 지운 엔딩을 가리키는 ending은 비운다.
        /// doc과 group을 사용하며, 새 문서를 반환한다.
        /// </summary>
        public static DialogueEditorDocument RemoveGroup(DialogueEditorDocument doc, int group)
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Group g = copy.Groups[group];
            copy.Groups.RemoveAt(group);
            string refColumn = g.Section == Csv.SECTION_DIALOGUE ? DialogueEditorFormat.GOTO : g.Section == Csv.SECTION_ENDING ? DialogueEditorFormat.ENDING : null;
            if (refColumn != null)
                foreach (DialogueEditorDocument.Group other in copy.Groups)
                {
                    if (other.Section != Csv.SECTION_DIALOGUE || other.Actor != g.Actor) continue;
                    foreach (DialogueEditorDocument.Item i in other.Items)
                        if (i.Get(refColumn) == g.Id) i.Fields.Remove(refColumn);
                }
            DialogueEditorFormat.Normalize(copy);
            return copy;
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
            if (_inline == InlineKind.Fragment)
            {
                if (_inlineFragment == null) { CancelInline(); return null; }
                string fragmentText = _inlineText.Trim();
                if (fragmentText.Length == 0) { _inlineError = "파편 문구를 입력한다."; return null; }
                if (fragmentText == DialogueEditorFormat.Body(_inlineFragment.Text)) { CancelInline(); return null; }
                (bool ok, string error) = FragmentTextSaver(_inlineFragment.Flag, fragmentText);
                if (ok) CancelInline();
                else { _inlineError = error; _inlineFocus = true; }
                return null;
            }
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
            if (_inline == InlineKind.After)
            {
                if (text == g.Get("after")) { CancelInline(); return null; }
                DialogueEditorDocument afterCopy = doc.Clone();
                if (text.Length == 0) afterCopy.Groups[idx].Fields.Remove("after");
                else afterCopy.Groups[idx].Fields["after"] = text;
                RenamedFrom = RenamedTo = null;
                _inlineAwait = true;
                return afterCopy;
            }
            if (_inline == InlineKind.Value) return ConfirmValue(doc, idx);
            if (_inlineRow < 0 || _inlineRow >= g.Items.Count) { CancelInline(); return null; }
            if (_inline == InlineKind.NewFlag) return ConfirmNewFlag(doc, idx, text);
            if (_inline == InlineKind.Compare) return ConfirmCompare(doc, idx, text);
            if (_inline == InlineKind.Badge)
            {
                bool valid = _inlineColumn == "dilation"
                    ? float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float d) && !float.IsNaN(d) && !float.IsInfinity(d)
                    : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                if (!valid) { _inlineError = _inlineColumn == "dilation" ? "동공은 0.15처럼 소수점으로 입력한다." : "정수를 입력한다."; return null; }
                if (text == g.Items[_inlineRow].Get(_inlineColumn)) { CancelInline(); return null; }
                DialogueEditorDocument scoreCopy = doc.Clone();
                scoreCopy.Groups[idx].Items[_inlineRow].Fields[_inlineColumn] = text;
                RenamedFrom = RenamedTo = null;
                _inlineAwait = true;
                return scoreCopy;
            }
            if (_inline == InlineKind.Response)
            {
                // 고친 줄만 바꾸고 다른 결과 줄은 그대로 둔다.
                if (text.Length == 0) { _inlineError = "빈 결과 줄은 저장할 수 없다. 우클릭으로 줄을 삭제한다."; return null; }
                List<string> lines = DialogueEditorFormat.ResponseLines(g.Items[_inlineRow].Get("response"));
                if (_inlineLine < 0 || _inlineLine >= lines.Count) { CancelInline(); return null; }
                if (text == lines[_inlineLine]) { CancelInline(); return null; }
                lines[_inlineLine] = text;
                DialogueEditorDocument responseCopy = WithResponse(doc, idx, _inlineRow, lines);
                RenamedFrom = RenamedTo = null;
                _inlineAwait = true;
                return responseCopy;
            }
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
            if (_inline == InlineKind.Fragment)
            {
                Box fragment = boxes.Find(box => box.Kind == BoxKind.Fragment && box.Fragment == _inlineFragment);
                return fragment == null ? Rect.zero : new Rect(fragment.Rect.x + Z(4f), fragment.Rect.y + Z(HEADER_HEIGHT + 20f), fragment.Rect.width - Z(8f), Z(20f));
            }
            if (_inline == InlineKind.After)
            {
                int idx = GroupIndex(doc, _inlineKey);
                Box after = boxes.Find(box => box.Kind == BoxKind.After && box.Group == idx);
                return after == null ? Rect.zero : new Rect(after.Rect.x + Z(4f), after.Rect.y + Z(4f), after.Rect.width - Z(8f), Z(20f));
            }
            if (_inline == InlineKind.Response)
            {
                int idx = GroupIndex(doc, _inlineKey);
                Box response = boxes.Find(box => box.Kind == BoxKind.Response && box.Group == idx && box.Item == _inlineRow);
                if (response == null) return Rect.zero;
                Rect line = ResponseLineRect(response, _inlineLine);
                return new Rect(line.x + Z(4f), line.y + Z(2f), line.width - Z(8f), Z(20f));
            }
            if (_inline == InlineKind.Compare)
            {
                int idx = GroupIndex(doc, _inlineKey);
                Segment s = _segments.Find(seg => seg.Target == EditTarget.Need && seg.Group == idx && seg.Item == _inlineRow && seg.Text == _inlineFlag);
                return s == null ? Rect.zero : new Rect(s.Rect.x, s.Rect.y - Z(2f), Mathf.Max(s.Rect.width, Z(90f)), s.Rect.height + Z(4f));
            }
            if (_inline == InlineKind.NewFlag)
            {
                int idx = GroupIndex(doc, _inlineKey);
                Segment s = _segments.Find(seg => seg.Target == EditTarget.Need && seg.Group == idx && seg.Item == _inlineRow);
                return s == null ? Rect.zero : new Rect(s.Rect.x, s.Rect.y - Z(2f), Mathf.Max(s.Rect.width, Z(160f)), s.Rect.height + Z(4f));
            }
            if (_inline == InlineKind.Value)
            {
                int idx = GroupIndex(doc, _inlineKey);
                Segment s = _segments.Find(seg => seg.Target == _inlineTarget && seg.Group == idx);
                return s == null ? Rect.zero : new Rect(s.Rect.x, s.Rect.y - Z(2f), Mathf.Max(s.Rect.width, Z(70f)), s.Rect.height + Z(4f));
            }
            Box b = BoxOf(boxes, GroupIndex(doc, _inlineKey));
            if (b == null) return Rect.zero;
            if (_inline == InlineKind.Rename) return new Rect(b.Rect.x + Z(2f), b.Rect.y + Z(2f), b.Rect.width - Z(4f), Z(HEADER_HEIGHT - 4f));
            if (_inline == InlineKind.Badge) return ScoreRect(RowRect(b, _inlineRow));
            return RowRect(b, _inlineRow);
        }

        /// <summary>
        /// 선택한 그룹 상자와 행에 선택 테두리를 그린다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawSelection(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_selFragment != null)
            {
                Box fragment = boxes.Find(box => box.Kind == BoxKind.Fragment && box.Fragment == _selFragment);
                if (fragment != null) DrawOutline(fragment.Rect, _focused ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.82f, 0.25f, 0.45f));
                return;
            }
            if (_selKey == null) return;
            if (_selAfter)
            {
                int idx = GroupIndex(doc, _selKey);
                Box after = boxes.Find(box => box.Kind == BoxKind.After && box.Group == idx);
                if (after != null) DrawOutline(after.Rect, _focused ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.82f, 0.25f, 0.45f));
                return;
            }
            if (_selResponse)
            {
                int idx = GroupIndex(doc, _selKey);
                Box response = boxes.Find(box => box.Kind == BoxKind.Response && box.Group == idx && box.Item == _selRow);
                if (response == null) return;
                Color rc = _focused ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 0.82f, 0.25f, 0.45f);
                DrawOutline(response.Rect, rc);
                if (_selLine >= 0)
                {
                    Rect r = ResponseLineRect(response, _selLine);
                    EditorGUI.DrawRect(new Rect(r.x, r.y, Z(3f), r.height), rc);
                }
                return;
            }
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
            // 에디터 공용 텍스트 편집기(EditorGUI)를 쓰면 입력창이 생기고 사라질 때 컨트롤 번호가 밀려 텍스트 뷰가 이 글을 가져갈 수 있다.
            // 그래서 자기 상태를 따로 갖는 GUI.TextField를 쓴다.
            string typed = GUI.TextField(r, _inlineText, style);
            // 입력창이 실제로 포커스를 가진 때만 받아, 다른 입력창의 글이 섞이지 않게 한다.
            if (GUI.GetNameOfFocusedControl() == INLINE_CONTROL) _inlineText = typed;
            if (_inlineFocus)
            {
                // 포커스는 다음 그리기에서 적용되므로, 입력창이 실제로 입력 중이 될 때까지 다시 요청한다.
                // 잡히면 전체 선택 대신 커서를 글 끝에 두어 바로 이어서 입력할 수 있게 한다.
                if (GUI.GetNameOfFocusedControl() == INLINE_CONTROL)
                {
                    _inlineFocus = false;
                    var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    editor.text = _inlineText;
                    editor.MoveTextEnd();
                }
                else GUI.FocusControl(INLINE_CONTROL);
            }
            string hint = _inlineError.Length > 0 ? _inlineError : "Enter 확정 · Esc 취소";
            var hr = new Rect(r.x, r.yMax + 2f, Mathf.Max(r.width, 220f), 18f);
            EditorGUI.DrawRect(hr, _inlineError.Length > 0 ? new Color(0.55f, 0.15f, 0.12f, 0.95f) : new Color(0.1f, 0.1f, 0.12f, 0.9f));
            GUI.Label(new Rect(hr.x + 4f, hr.y + 1f, hr.width - 8f, hr.height), hint, EditorStyles.whiteMiniLabel);
        }

        // ---------------------------------------------------------------- 엔딩 진입 조건

        /// <summary>
        /// 판정 카드에서 엔딩으로 가는 진입선의 시작·끝 위치를 구한다. 심연 핵심 접촉은 왼쪽 선이 truth, 오른쪽 선이 half이고,
        /// 상담 중 판정은 같은 leave로 두 선(낮은 신뢰, 부담)을 나란히 긋는다.
        /// judge, target, lane(선 번호 0·1)을 사용하며, (시작, 끝)을 반환한다.
        /// </summary>
        private (Vector2 from, Vector2 to) JudgeLane(Box judge, Box target, int lane)
        {
            if (judge.Kind == BoxKind.Core)
                return (new Vector2(judge.Rect.center.x + (lane == 0 ? -Z(30f) : Z(30f)), judge.Rect.yMax), TopCenter(target.Rect));
            float f = (lane + 1) / 3f;
            return (new Vector2(judge.Rect.x + judge.Rect.width * f, judge.Rect.yMax), new Vector2(target.Rect.x + target.Rect.width * f, target.Rect.yMin));
        }

        /// <summary>
        /// 화살표 곡선 위의 한 점을 구한다. DrawArrow와 같은 접선을 쓴다.
        /// from, to, sideways, t(0~1)를 사용하며, 점을 반환한다.
        /// </summary>
        private Vector2 CurvePoint(Vector2 from, Vector2 to, bool sideways, float t)
        {
            Vector2 t1 = from + (sideways ? new Vector2(Z(80f), 0f) : new Vector2(0f, Z(40f)));
            Vector2 t2 = to + new Vector2(0f, -Z(60f));
            float u = 1f - t;
            return u * u * u * from + 3f * u * u * t * t1 + 3f * u * t * t * t2 + t * t * t * to;
        }

        /// <summary>
        /// 선택지 행에서 엔딩·goto 선이 출발하는 위치를 구한다. 결과 카드가 있으면 그 아래에서 출발한다.
        /// boxes, b, k를 사용하며, 위치를 반환한다.
        /// </summary>
        private Vector2 ChoiceLineStart(List<Box> boxes, Box b, int k)
        {
            Box response = boxes.Find(candidate => candidate.Kind == BoxKind.Response && candidate.Group == b.Group && candidate.Item == k);
            return response != null ? BottomCenter(response.Rect) : PortPosition(b, k);
        }

        /// <summary>
        /// 엔딩 진입선에 붙일 조건 글을 조각으로 만든다. truth·half는 신뢰 기준과 열쇠 파편, leave는 낮은 신뢰와 부담 기준,
        /// 선택지에서 바로 가는 엔딩은 그 선택지의 need 플래그(없으면 '선택 시 즉시')다. 고칠 수 있는 조각에는 편집 대상을 붙인다.
        /// doc과 boxes를 사용하며, _segments를 새로 채운다.
        /// </summary>
        private void BuildSegments(DialogueEditorDocument doc, List<Box> boxes)
        {
            _segments.Clear();
            if (doc.Kind != DialogueEditorKind.Day) return;
            var byGroup = new Dictionary<int, Box>();
            foreach (Box b in boxes) if (b.Kind == BoxKind.Group) byGroup[b.Group] = b;

            foreach (Box judge in boxes)
            {
                if (judge.Kind != BoxKind.Core && judge.Kind != BoxKind.Gaze) continue;
                string actor = doc.Groups[judge.Group].Actor;
                if (judge.Kind == BoxKind.Core)
                {
                    int truth = doc.IndexOf(Csv.SECTION_ENDING, actor, EndingScript.TRUTH);
                    int half = doc.IndexOf(Csv.SECTION_ENDING, actor, EndingScript.HALF);
                    string trust = truth >= 0 ? doc.Groups[truth].Get(EndingScript.TRUTH_TRUST) : "";
                    string key = truth >= 0 ? doc.Groups[truth].Get(EndingScript.KEY_FLAG) : "";
                    EditTarget trustTarget = truth >= 0 ? EditTarget.TruthTrust : EditTarget.None;
                    EditTarget keyTarget = truth >= 0 ? EditTarget.KeyFlag : EditTarget.None;
                    string keyText = key.Length > 0 ? key : "없음";
                    if (truth >= 0 && byGroup.TryGetValue(truth, out Box tb))
                    {
                        (Vector2 from, Vector2 to) = JudgeLane(judge, tb, 0);
                        AddLabel(CurvePoint(from, to, false, 0.35f), ("신뢰 ≥ ", EditTarget.None, -1, -1), (Value(trust), trustTarget, truth, -1),
                            (" · 파편 ", EditTarget.None, -1, -1), (keyText, keyTarget, truth, -1), (key.Length > 0 ? " 획득" : " (조건 없음)", EditTarget.None, -1, -1));
                    }
                    if (half >= 0 && byGroup.TryGetValue(half, out Box hb))
                    {
                        (Vector2 from, Vector2 to) = JudgeLane(judge, hb, 1);
                        if (key.Length > 0)
                            AddLabel(CurvePoint(from, to, false, 0.7f), ("신뢰 < ", EditTarget.None, -1, -1), (Value(trust), trustTarget, truth, -1),
                                (" 또는 파편 ", EditTarget.None, -1, -1), (keyText, keyTarget, truth, -1), (" 미획득", EditTarget.None, -1, -1));
                        else
                            AddLabel(CurvePoint(from, to, false, 0.7f), ("신뢰 < ", EditTarget.None, -1, -1), (Value(trust), trustTarget, truth, -1),
                                (" · 파편 ", EditTarget.None, -1, -1), (keyText, keyTarget, truth, -1), (" (조건 없음)", EditTarget.None, -1, -1));
                    }
                }
                else if (byGroup.TryGetValue(judge.Group, out Box lb))
                {
                    DialogueEditorDocument.Group leave = doc.Groups[judge.Group];
                    (Vector2 f0, Vector2 t0) = JudgeLane(judge, lb, 0);
                    AddLabel(CurvePoint(f0, t0, false, 0.35f), ("선택 후 신뢰 ≤ ", EditTarget.None, -1, -1), (Value(leave.Get(EndingScript.LEAVE_TRUST)), EditTarget.LeaveTrust, judge.Group, -1));
                    (Vector2 f1, Vector2 t1) = JudgeLane(judge, lb, 1);
                    AddLabel(CurvePoint(f1, t1, false, 0.7f), ("들여다보는 중 부담 ≥ ", EditTarget.None, -1, -1), (Percent(leave.Get(EndingScript.BURDEN_LIMIT)), EditTarget.BurdenLimit, judge.Group, -1));
                }
            }

            // 선택지에서 바로 가는 엔딩: 그 선택지의 need가 조건이다.
            foreach (Box b in boxes)
            {
                if (b.Kind != BoxKind.Group || b.Side) continue;
                DialogueEditorDocument.Group g = doc.Groups[b.Group];
                if (g.Section != Csv.SECTION_DIALOGUE) continue;
                for (int k = 0; k < g.Items.Count; k++)
                {
                    int e = doc.IndexOf(Csv.SECTION_ENDING, g.Actor, g.Items[k].Get(DialogueEditorFormat.ENDING));
                    if (e < 0 || !byGroup.TryGetValue(e, out Box endBox)) continue;
                    string need = g.Items[k].Get("need");
                    Vector2 mid = CurvePoint(ChoiceLineStart(boxes, b, k), TopCenter(endBox.Rect), true, 0.55f);
                    if (need.Length > 0)
                    {
                        // 플래그마다 따로 클릭할 수 있게 조각을 나눈다.
                        var parts = new List<(string, EditTarget, int, int)> { ("조건 ", EditTarget.None, -1, -1) };
                        foreach (string flag in need.Split('|'))
                        {
                            if (flag.Trim().Length == 0) continue;
                            if (parts.Count > 1) parts.Add((", ", EditTarget.None, -1, -1));
                            parts.Add((flag.Trim(), EditTarget.Need, b.Group, k));
                        }
                        AddLabel(mid, parts.ToArray());
                    }
                    else AddLabel(mid, ("선택 시 즉시", EditTarget.Need, b.Group, k));
                }
            }
        }

        /// <summary>
        /// 기준값을 화면에 보일 글로 바꾼다. 비어 있으면 '?'다.
        /// v를 사용하며, 글을 반환한다.
        /// </summary>
        private static string Value(string v) => v.Length > 0 ? v : "?";

        /// <summary>
        /// 0~1 부담 기준을 백분율 글로 바꾼다. 비었거나 수가 아니면 '?'다.
        /// v를 사용하며, 'NN%' 글을 반환한다.
        /// </summary>
        private static string Percent(string v)
        {
            if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return "?";
            return (f * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// 조건 조각들을 한 줄로 이어 center에 가운데 맞춰 놓는다.
        /// center와 parts(글, 편집 대상, 그룹, 행)를 사용하며, _segments에 조각을 더한다.
        /// </summary>
        private void AddLabel(Vector2 center, params (string text, EditTarget target, int group, int item)[] parts)
        {
            float h = Z(16f), pad = Z(4f), total = 0f;
            var widths = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                widths[i] = _rowStyle.CalcSize(new GUIContent(parts[i].text)).x + (parts[i].target != EditTarget.None ? pad * 2f : 0f);
                total += widths[i];
            }
            float x = center.x - total * 0.5f;
            for (int i = 0; i < parts.Length; i++)
            {
                _segments.Add(new Segment { Text = parts[i].text, Target = parts[i].target, Group = parts[i].group, Item = parts[i].item, Rect = new Rect(x, center.y - h * 0.5f, widths[i], h) });
                x += widths[i];
            }
        }

        /// <summary>
        /// 진입선 조건 조각을 그린다. 고칠 수 있는 조각은 다른 바탕과 밑줄로 구분한다.
        /// 입력값은 없으며, _segments를 그린다.
        /// </summary>
        private void DrawSegments()
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (Segment s in _segments)
            {
                bool editable = s.Target != EditTarget.None;
                EditorGUI.DrawRect(s.Rect, editable ? new Color(0.30f, 0.22f, 0.10f, 0.95f) : new Color(0.10f, 0.10f, 0.12f, 0.92f));
                if (editable) EditorGUI.DrawRect(new Rect(s.Rect.x + Z(3f), s.Rect.yMax - 1f, s.Rect.width - Z(6f), 1f), new Color(1f, 0.78f, 0.35f));
                var r = new Rect(s.Rect.x + (editable ? Z(4f) : 0f), s.Rect.y + Z(1f), s.Rect.width, s.Rect.height);
                _scoreStyle.wordWrap = false;
                GUI.Label(r, s.Text, editable ? _scoreStyle : _rowStyle);
                _scoreStyle.wordWrap = true;
            }
        }

        /// <summary>
        /// 진입선 조건의 고칠 수 있는 조각을 누르면 편집을 시작한다. 기준값은 인라인 입력, 열쇠 파편과 need는 메뉴로 고른다.
        /// 텍스트 오류·검사 대기 중에는 이유만 보인다. e, doc, inside, editable, windowMouse를 사용하며, 처리하면 이벤트를 사용 처리한다.
        /// </summary>
        private void HandleSegmentInput(Event e, DialogueEditorDocument doc, bool inside, bool editable, Vector2 windowMouse)
        {
            if (!inside || e.type != EventType.MouseDown || e.button != 0 || _inline != InlineKind.None) return;
            Segment hit = null;
            for (int i = _segments.Count - 1; i >= 0; i--)
                if (_segments[i].Target != EditTarget.None && _segments[i].Rect.Contains(e.mousePosition)) { hit = _segments[i]; break; }
            if (hit == null) return;
            _focused = true;
            e.Use();
            if (!editable) { Notify("텍스트 오류를 고치거나 검사가 끝난 뒤 조건을 바꿀 수 있다."); return; }
            if (hit.Group < 0 || hit.Group >= doc.Groups.Count) return;
            switch (hit.Target)
            {
                case EditTarget.KeyFlag:
                    KeyFlagMenu(doc, hit.Group).DropDown(new Rect(windowMouse, Vector2.zero));
                    break;
                case EditTarget.Need:
                    // 수치 계산 플래그면 비교·수치를 고치고, 아니면 조건 켜고 끄기 메뉴를 띄운다.
                    if (TryCompare(ConditionOf(hit.Text), out _, out _, out _)) StartCompareEdit(doc, hit.Group, hit.Item, hit.Text);
                    else NeedMenu(doc, hit.Group, hit.Item).DropDown(new Rect(windowMouse, Vector2.zero));
                    break;
                default:
                    StartValueEdit(doc, hit.Target, hit.Group);
                    break;
            }
        }

        /// <summary>
        /// 엔딩 기준값의 인라인 편집을 시작한다. 부담 기준은 백분율 숫자로 고친다.
        /// doc, target, group을 사용하며, 인라인 상태를 바꾼다.
        /// </summary>
        private void StartValueEdit(DialogueEditorDocument doc, EditTarget target, int group)
        {
            DialogueEditorDocument.Group g = doc.Groups[group];
            _inline = InlineKind.Value;
            _inlineTarget = target;
            _inlineKey = KeyOf(g);
            _inlineRow = -1;
            string v = g.Get(ColumnOf(target));
            _inlineText = target == EditTarget.BurdenLimit ? Percent(v).TrimEnd('%').Replace("?", "") : v;
            _inlineError = "";
            _inlineFocus = true;
        }

        /// <summary>
        /// 편집 대상의 CSV 열 이름을 구한다.
        /// target을 사용하며, 열 이름을 반환한다.
        /// </summary>
        private static string ColumnOf(EditTarget target)
        {
            switch (target)
            {
                case EditTarget.TruthTrust: return EndingScript.TRUTH_TRUST;
                case EditTarget.KeyFlag: return EndingScript.KEY_FLAG;
                case EditTarget.LeaveTrust: return EndingScript.LEAVE_TRUST;
                case EditTarget.BurdenLimit: return EndingScript.BURDEN_LIMIT;
                default: return "need";
            }
        }

        /// <summary>
        /// 엔딩 기준값 입력을 확정한다. 신뢰 기준은 정수, 부담 기준은 0 초과 100 이하의 백분율이며 CSV에는 0~1 실수로 쓴다.
        /// doc과 idx(그룹 번호)를 사용하며, 바뀐 복사본 또는 (오류·변화 없음이면) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument ConfirmValue(DialogueEditorDocument doc, int idx)
        {
            string text = _inlineText.Trim();
            string column = ColumnOf(_inlineTarget);
            string value;
            if (_inlineTarget == EditTarget.BurdenLimit)
            {
                if (!float.TryParse(text.TrimEnd('%').Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float p)) { _inlineError = "부담 기준은 100처럼 백분율 숫자로 입력한다."; return null; }
                if (p <= 0f || p > 100f) { _inlineError = "부담 기준은 0% 초과 100% 이하여야 한다."; return null; }
                value = (p / 100f).ToString("0.####", CultureInfo.InvariantCulture);
            }
            else
            {
                if (!int.TryParse(text.TrimStart('+'), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { _inlineError = "신뢰 기준은 정수로 입력한다."; return null; }
                value = v.ToString(CultureInfo.InvariantCulture);
            }
            if (value == doc.Groups[idx].Get(column)) { CancelInline(); return null; }
            DialogueEditorDocument copy = doc.Clone();
            copy.Groups[idx].Fields[column] = value;
            RenamedFrom = RenamedTo = null;
            _inlineAwait = true;
            return copy;
        }

        /// <summary>
        /// 열쇠 파편 대상을 고르는 메뉴를 만든다. '없음'과 이 회차 손님의 파편 Flag(하나뿐인 것)만 고를 수 있다.
        /// doc과 truth(그룹 번호)를 사용하며, 메뉴를 반환한다. 고르면 key_flag를 바꾼 복사본을 _pending에 둔다.
        /// </summary>
        private GenericMenu KeyFlagMenu(DialogueEditorDocument doc, int truth)
        {
            string current = doc.Groups[truth].Get(EndingScript.KEY_FLAG);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("없음 (신뢰만 판정)"), current.Length == 0, () => _pending = WithGroupField(doc, truth, EndingScript.KEY_FLAG, ""));
            (ClientCase owner, List<string> flags) = DialogueEditorFormat.CaseFragmentFlags(doc.AssetPath);
            if (owner == null) { menu.AddDisabledItem(new GUIContent("열린 씬에서 이 파일의 손님을 찾지 못했다")); return menu; }
            menu.AddSeparator("");
            foreach (string flag in flags)
            {
                string f = flag;
                menu.AddItem(new GUIContent(f), f == current, () => _pending = WithGroupField(doc, truth, EndingScript.KEY_FLAG, f));
            }
            return menu;
        }

        /// <summary>
        /// 직접 엔딩 선택지의 need 플래그를 켜고 끄는 메뉴를 만든다. Flags.csv의 플래그마다 한 항목이고, 고를 때마다 하나를 더하거나 뺀다.
        /// doc, group, item을 사용하며, 메뉴를 반환한다. 고르면 need를 바꾼 복사본을 _pending에 둔다.
        /// </summary>
        private GenericMenu NeedMenu(DialogueEditorDocument doc, int group, int item)
        {
            var current = new List<string>();
            foreach (string part in doc.Groups[group].Items[item].Get("need").Split('|'))
                if (part.Trim().Length > 0) current.Add(part.Trim());
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("조건 모두 지우기 (선택 시 즉시)"), current.Count == 0, () => _pending = WithNeed(doc, group, item, new List<string>()));
            menu.AddSeparator("");
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(new List<string>()))
            {
                if (d.Id.Length == 0) continue;
                string id = d.Id;
                bool on = current.Contains(id);
                menu.AddItem(new GUIContent(id), on, () =>
                {
                    var next = new List<string>(current);
                    if (on) next.Remove(id);
                    else next.Add(id);
                    _pending = WithNeed(doc, group, item, next);
                });
            }
            return menu;
        }

        /// <summary>
        /// 그룹 칸 하나를 바꾼 복사본을 만든다. 값이 비면 칸을 지운다.
        /// doc, group, column, value를 사용하며, 새 문서를 반환한다.
        /// </summary>
        private static DialogueEditorDocument WithGroupField(DialogueEditorDocument doc, int group, string column, string value)
        {
            DialogueEditorDocument copy = doc.Clone();
            if (value.Length == 0) copy.Groups[group].Fields.Remove(column);
            else copy.Groups[group].Fields[column] = value;
            return copy;
        }

        /// <summary>
        /// 위치에 있는 직접 엔딩 조건 조각을 찾는다. 앞에 붙은 '조건' 글자 위도 그 조건으로 본다.
        /// position(그래프 좌표)을 사용하며, need 조각(없으면 null)을 반환한다.
        /// </summary>
        private Segment NeedSegmentAt(Vector2 position)
        {
            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                Segment seg = _segments[i];
                if (seg.Target != EditTarget.Need) continue;
                if (seg.Rect.Contains(position)) return seg;
                if (i > 0 && _segments[i - 1].Target == EditTarget.None && _segments[i - 1].Rect.Contains(position)) return seg;
            }
            return null;
        }

        /// <summary>
        /// 직접 엔딩 조건의 우클릭 메뉴를 만든다. 조건에 없는 플래그를 source별로 더하고, 조건의 플래그를 빼고, 새 조건 변수를 만든다.
        /// doc, group, item(선택지 행), editable을 사용하며, 메뉴를 반환한다. 고르면 need를 바꾼 복사본을 _pending에 두거나 ID 입력을 연다.
        /// </summary>
        private GenericMenu NeedContextMenu(DialogueEditorDocument doc, int group, int item, bool editable)
        {
            var menu = new GenericMenu();
            if (!editable)
            {
                menu.AddDisabledItem(new GUIContent("조건 변수 편집 (텍스트 오류 또는 검사 대기 중)"));
                return menu;
            }
            var current = new List<string>();
            foreach (string part in doc.Groups[group].Items[item].Get("need").Split('|'))
                if (part.Trim().Length > 0) current.Add(part.Trim());
            bool any = false;
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(new List<string>()))
            {
                if (d.Id.Length == 0 || current.Contains(d.Id)) continue;
                string id = d.Id;
                string source = !string.IsNullOrEmpty(d.Condition) ? "계산" : !string.IsNullOrWhiteSpace(d.Source) ? d.Source.Trim() : "기타";
                menu.AddItem(new GUIContent($"조건 추가/{source.Replace("/", "∕")}/{id}"), false,
                    () => _pending = WithNeed(doc, group, item, new List<string>(current) { id }));
                any = true;
            }
            if (!any) menu.AddDisabledItem(new GUIContent("조건 추가 (더할 플래그가 없음)"));
            foreach (string flag in current)
            {
                string id = flag;
                menu.AddItem(new GUIContent("조건 빼기/" + id), false, () =>
                {
                    var next = new List<string>(current);
                    next.Remove(id);
                    _pending = WithNeed(doc, group, item, next);
                });
            }
            // 수치 계산 플래그는 수치를 그대로 두고 비교만 바꿀 수 있다.
            foreach (string flag in current)
            {
                if (!TryCompare(ConditionOf(flag), out string variable, out string op, out string value) || FlagCreator == null) continue;
                foreach (string o in COMPARE_OPS)
                {
                    string old = flag, nextOp = o;
                    menu.AddItem(new GUIContent($"{flag} 비교/{nextOp} ({OpWord(nextOp)})"), o == op, () =>
                    {
                        DialogueEditorDocument changed = ReplaceCompare(doc, group, item, old, variable, nextOp, value, out string error);
                        if (changed == null) { if (error.Length > 0) Notify(error); return; }
                        _pending = changed;
                    });
                }
            }
            menu.AddSeparator("");
            if (FlagCreator == null) menu.AddDisabledItem(new GUIContent("새 조건 변수 만들기…"));
            else menu.AddItem(new GUIContent("새 조건 변수 만들기…"), false, () => StartNewFlagEdit(doc, group, item));
            return menu;
        }

        private static readonly string[] COMPARE_OPS = { ">=", "<=", ">", "<", "==", "!=" };

        /// <summary>
        /// 계산 플래그의 condition을 Flags.csv에서 찾는다.
        /// flag를 사용하며, condition(없거나 일반 플래그면 빈 글)을 반환한다.
        /// </summary>
        private static string ConditionOf(string flag)
        {
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(new List<string>()))
                if (d.Id == flag) return d.Condition ?? "";
            return "";
        }

        /// <summary>
        /// condition이 수치 하나와 숫자의 비교 하나인지 본다 (예: trust>=3).
        /// condition을 사용하며, 맞으면 true를 반환하고 변수·비교·수치(InvariantCulture 글)를 내보낸다.
        /// </summary>
        private static bool TryCompare(string condition, out string variable, out string op, out string value)
        {
            variable = op = value = "";
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(condition ?? "",
                @"^\s*(trust|dilation|spring)\s*(>=|<=|==|!=|>|<)\s*(-?\d+(?:\.\d+)?)\s*$");
            if (!m.Success) return false;
            variable = m.Groups[1].Value;
            op = m.Groups[2].Value;
            value = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>
        /// 비교를 설명 글로 바꾼다.
        /// op를 사용하며, 이상·이하 같은 말을 반환한다.
        /// </summary>
        private static string OpWord(string op)
        {
            switch (op)
            {
                case ">=": return "이상";
                case "<=": return "이하";
                case ">": return "초과";
                case "<": return "미만";
                case "==": return "와 같음";
                default: return "이 아님";
            }
        }

        /// <summary>
        /// 직접 엔딩 조건에 든 수치 계산 플래그의 비교·수치 입력을 시작한다. 입력창에는 '>=3'처럼 비교와 수치를 함께 보인다.
        /// doc, group, item, flag를 사용하며, 인라인 상태를 바꾼다.
        /// </summary>
        private void StartCompareEdit(DialogueEditorDocument doc, int group, int item, string flag)
        {
            if (FlagCreator == null || !TryCompare(ConditionOf(flag), out _, out string op, out string value)) return;
            _inline = InlineKind.Compare;
            _inlineKey = KeyOf(doc.Groups[group]);
            _inlineRow = item;
            _inlineFlag = flag;
            _inlineText = op + value;
            _inlineError = "";
            _inlineFocus = true;
            _focused = true;
        }

        /// <summary>
        /// 비교·수치 입력을 확정한다. 수치만 쓰면 원래 비교를 유지하고, '=', '≥', '≤', '≠'도 받는다.
        /// doc, idx(그룹), text(입력)를 사용하며, 조건을 바꾼 복사본 또는 (입력창을 유지할 때) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument ConfirmCompare(DialogueEditorDocument doc, int idx, string text)
        {
            if (!TryCompare(ConditionOf(_inlineFlag), out string variable, out string oldOp, out _)) { CancelInline(); return null; }
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(text,
                @"^(>=|<=|==|!=|>|<|=|≥|≤|≠)?\s*(-?\d+(?:\.\d+)?)$");
            if (!m.Success) { _inlineError = "'>=5', '<=2', '==0'처럼 비교와 수치를 쓴다. 수치만 쓰면 비교를 유지한다."; return null; }
            string op = m.Groups[1].Value;
            op = op.Length == 0 ? oldOp : op == "=" ? "==" : op == "≥" ? ">=" : op == "≤" ? "<=" : op == "≠" ? "!=" : op;
            string value = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            DialogueEditorDocument changed = ReplaceCompare(doc, idx, _inlineRow, _inlineFlag, variable, op, value, out string error);
            if (changed == null)
            {
                if (error.Length > 0) { _inlineError = error; return null; }
                CancelInline();
                return null;
            }
            RenamedFrom = RenamedTo = null;
            _inlineAwait = true;
            return changed;
        }

        /// <summary>
        /// 선택지 조건의 수치 계산 플래그 하나를 같은 변수·비교·수치의 계산 플래그로 바꾼다.
        /// Flags.csv에 같은 조건의 플래그가 있으면 그걸 쓰고, 없으면 규칙에 맞는 ID로 새 줄을 넣는다.
        /// doc, group, item, oldFlag, variable, op, value를 사용하며, 새 문서(바뀌지 않거나 실패하면 null)를 반환하고 실패 이유를 error에 쓴다.
        /// </summary>
        private DialogueEditorDocument ReplaceCompare(DialogueEditorDocument doc, int group, int item, string oldFlag, string variable, string op, string value, out string error)
        {
            error = "";
            string target = null;
            var ids = new HashSet<string>();
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(new List<string>()))
            {
                ids.Add(d.Id);
                if (target == null && TryCompare(d.Condition, out string v, out string o, out string n) && v == variable && o == op && n == value) target = d.Id;
            }
            if (target == oldFlag) return null;
            if (target == null)
            {
                string opName = op == ">=" ? "" : op == "<=" ? "le_" : op == "<" ? "lt_" : op == ">" ? "gt_" : op == "==" ? "eq_" : "ne_";
                target = $"{variable}_{opName}{value.Replace("-", "m").Replace(".", "_")}";
                if (ids.Contains(target)) { error = $"'{target}'가 Flags.csv에 다른 조건으로 이미 있다. Flags.csv에서 조건을 확인한다."; return null; }
                string name = variable == "trust" ? "신뢰" : variable == "dilation" ? "동공" : "태엽";
                (bool ok, string createError) = FlagCreator(target, $"{name} {value} {OpWord(op)}", "계산", $"{variable}{op}{value}");
                if (!ok) { error = createError; return null; }
            }
            var flags = new List<string>();
            foreach (string part in doc.Groups[group].Items[item].Get("need").Split('|'))
            {
                string f = part.Trim();
                if (f.Length == 0) continue;
                string next = f == oldFlag ? target : f;
                if (!flags.Contains(next)) flags.Add(next);
            }
            return WithNeed(doc, group, item, flags);
        }

        /// <summary>
        /// 새 조건 변수의 ID 입력을 조건 자리에서 시작한다.
        /// doc, group, item을 사용하며, 인라인 상태를 바꾼다.
        /// </summary>
        private void StartNewFlagEdit(DialogueEditorDocument doc, int group, int item)
        {
            _inline = InlineKind.NewFlag;
            _inlineKey = KeyOf(doc.Groups[group]);
            _inlineRow = item;
            _inlineText = "";
            _inlineError = "";
            _inlineFocus = true;
            _focused = true;
        }

        /// <summary>
        /// 새 조건 변수 입력을 확정한다. ID 규칙에 맞으면 Flags.csv에 줄을 넣고 그 ID를 조건에 더한 복사본을 만든다.
        /// doc, idx(그룹), text(입력한 ID)를 사용하며, 복사본 또는 (입력창을 유지할 때) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument ConfirmNewFlag(DialogueEditorDocument doc, int idx, string text)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(text, "^[a-z0-9_]+$")) { _inlineError = "ID는 영문 소문자, 숫자, _만 쓴다."; return null; }
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(new List<string>()))
                if (d.Id == text) { _inlineError = $"'{text}'는 Flags.csv에 이미 있다. 조건 추가 메뉴에서 고른다."; return null; }
            (bool ok, string error) = FlagCreator(text, "", "", "");
            if (!ok) { _inlineError = error; return null; }
            var flags = new List<string>();
            foreach (string part in doc.Groups[idx].Items[_inlineRow].Get("need").Split('|'))
                if (part.Trim().Length > 0) flags.Add(part.Trim());
            flags.Add(text);
            RenamedFrom = RenamedTo = null;
            _inlineAwait = true;
            return WithNeed(doc, idx, _inlineRow, flags);
        }

        /// <summary>
        /// 선택지의 need 목록을 바꾼 복사본을 만든다.
        /// doc, group, item, flags를 사용하며, 새 문서를 반환한다.
        /// </summary>
        private static DialogueEditorDocument WithNeed(DialogueEditorDocument doc, int group, int item, List<string> flags)
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Item i = copy.Groups[group].Items[item];
            if (flags.Count == 0) i.Fields.Remove("need");
            else i.Fields["need"] = string.Join("|", flags);
            return copy;
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
            Segment need = NeedSegmentAt(e.mousePosition);
            if (need != null && need.Group < doc.Groups.Count)
            {
                // 진입선 조건 위: 조건 변수를 더하고 빼거나 새로 만드는 메뉴
                NeedContextMenu(doc, need.Group, need.Item, editable).DropDown(new Rect(windowMouse, Vector2.zero));
                e.Use();
                return;
            }
            Box hit = HitBox(boxes, e.mousePosition);
            if (hit == null)
            {
                // 빈 곳: 새 장면·대화·엔딩을 만드는 메뉴
                BuildCreateMenu(doc, editable).DropDown(new Rect(windowMouse, Vector2.zero));
                e.Use();
                return;
            }
            if (hit.Kind == BoxKind.Response)
            {
                BuildResponseMenu(doc, hit.Group, hit.Item, ResponseLineAt(doc, hit, e.mousePosition.y), editable).DropDown(new Rect(windowMouse, Vector2.zero));
                e.Use();
                return;
            }
            if (hit.Kind != BoxKind.Group) return;
            BuildAddMenu(doc, hit.Group, RowAt(doc, hit, e.mousePosition.y), editable).DropDown(new Rect(windowMouse, Vector2.zero));
            e.Use();
        }

        /// <summary>
        /// 결과 카드의 우클릭 메뉴를 만든다. 고른 줄 뒤에 결과 줄을 넣거나 그 줄을 지운다. 마지막 한 줄을 지우면 결과 카드가 사라진다.
        /// doc, group, item(선택지 행), line(우클릭한 결과 줄), editable을 사용하며, 메뉴를 반환한다. 항목을 고르면 결과를 _pending에 둔다.
        /// </summary>
        private GenericMenu BuildResponseMenu(DialogueEditorDocument doc, int group, int item, int line, bool editable)
        {
            var menu = new GenericMenu();
            List<string> lines = DialogueEditorFormat.ResponseLines(doc.Groups[group].Items[item].Get("response"));
            string addLabel = $"{line + 1}번 줄 뒤에 결과 줄 추가";
            string removeLabel = lines.Count > 1 ? $"{line + 1}번 결과 줄 삭제" : "결과 삭제";
            if (!editable)
            {
                menu.AddDisabledItem(new GUIContent(addLabel + " (텍스트 오류 또는 검사 대기 중)"));
                menu.AddDisabledItem(new GUIContent(removeLabel + " (텍스트 오류 또는 검사 대기 중)"));
                return menu;
            }
            string key = KeyOf(doc.Groups[group]);
            menu.AddItem(new GUIContent(addLabel), false, () =>
            {
                var added = new List<string>(lines);
                added.Insert(line + 1, "새 결과 줄");
                _pending = WithResponse(doc, group, item, added);
                SelectResponse(key, item, line + 1);
            });
            menu.AddItem(new GUIContent(removeLabel), false, () =>
            {
                var removed = new List<string>(lines);
                removed.RemoveAt(line);
                _pending = WithResponse(doc, group, item, removed);
                if (removed.Count > 0) SelectResponse(key, item, Mathf.Min(line, removed.Count - 1));
                else { _selResponse = false; _selRow = item; }
            });
            return menu;
        }

        /// <summary>
        /// 선택지 결과를 결과 줄 목록으로 바꾼 복사본을 만든다. 목록이 비면 response 칸을 비운다.
        /// doc, group, item, lines를 사용하며, 새 문서를 반환한다.
        /// </summary>
        private static DialogueEditorDocument WithResponse(DialogueEditorDocument doc, int group, int item, List<string> lines)
        {
            DialogueEditorDocument copy = doc.Clone();
            Dictionary<string, string> fields = copy.Groups[group].Items[item].Fields;
            if (lines.Count == 0) fields.Remove("response");
            else fields["response"] = DialogueEditorFormat.JoinResponse(lines);
            return copy;
        }

        /// <summary>
        /// 결과 카드의 한 줄을 선택한다. 메뉴로 줄을 넣거나 지운 뒤 그 줄을 바로 Enter로 고칠 수 있게 한다.
        /// key(그룹 키), item, line을 사용하며, 선택 상태를 바꾼다.
        /// </summary>
        private void SelectResponse(string key, int item, int line)
        {
            _selKey = key;
            _selRow = item;
            _selResponse = true;
            _selAfter = false;
            _selFragment = null;
            _selLine = line;
            _focused = true;
        }

        /// <summary>
        /// 결과 카드 높이를 구한다. 머리와 결과 줄 수만큼의 줄 칸이며, 한 줄일 때는 기존 카드 높이를 쓴다.
        /// item(선택지 행)을 사용하며, 배치 좌표계 높이를 반환한다.
        /// </summary>
        private static float ResponseHeight(DialogueEditorDocument.Item item)
        {
            int count = DialogueEditorFormat.ResponseLines(item.Get("response")).Count;
            return Mathf.Max(RESPONSE_HEIGHT, HEADER_HEIGHT + 8f + count * RESPONSE_LINE_HEIGHT);
        }

        /// <summary>
        /// 결과 카드 안 결과 줄의 화면 사각형을 구한다.
        /// b(결과 카드)와 line을 사용하며, 사각형을 반환한다.
        /// </summary>
        private Rect ResponseLineRect(Box b, int line)
        {
            float top = b.Rect.y + Z(HEADER_HEIGHT + 4f);
            return new Rect(b.Rect.x, top + Z(RESPONSE_LINE_HEIGHT) * line, b.Rect.width, Z(RESPONSE_LINE_HEIGHT));
        }

        /// <summary>
        /// 화면 y 위치에 있는 결과 줄 번호를 구한다. 머리 위는 첫 줄, 마지막 줄 아래는 마지막 줄로 본다.
        /// doc, b(결과 카드), y를 사용하며, 줄 번호(결과가 없으면 -1)를 반환한다.
        /// </summary>
        private int ResponseLineAt(DialogueEditorDocument doc, Box b, float y)
        {
            int count = DialogueEditorFormat.ResponseLines(doc.Groups[b.Group].Items[b.Item].Get("response")).Count;
            if (count == 0) return -1;
            int line = Mathf.FloorToInt((y - (b.Rect.y + Z(HEADER_HEIGHT + 4f))) / Z(RESPONSE_LINE_HEIGHT));
            return Mathf.Clamp(line, 0, count - 1);
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
            else if (g.Section == Csv.SECTION_TALK)
            {
                menu.AddItem(new GUIContent(label + "/기본 화자 (" + g.Actor + ")"), false, () => _pending = InsertRow(doc, group, index, ""));
                menu.AddItem(new GUIContent(label + "/서술"), false, () => _pending = InsertRow(doc, group, index, TalkScript.Narration));
                menu.AddItem(new GUIContent(label + "/???"), false, () => _pending = InsertRow(doc, group, index, TalkScript.Unknown));
                foreach (string speaker in SpeakerNames(doc))
                {
                    string selected = speaker;
                    menu.AddItem(new GUIContent(label + "/" + selected), false, () => _pending = InsertRow(doc, group, index, selected));
                }
            }
            else menu.AddItem(new GUIContent(label), false, () => _pending = InsertRow(doc, group, index));
            // Talk 대사 행 위에서 우클릭하면 그 행의 화자를 바꾸는 항목도 보인다. 지금 화자에 체크 표시를 한다.
            if (g.Section == Csv.SECTION_TALK && row >= 0 && row < g.Items.Count)
            {
                menu.AddSeparator("");
                string current = g.Items[row].Get("speaker");
                string changeLabel = $"{row + 1}번 행 화자 바꾸기";
                if (!editable) menu.AddDisabledItem(new GUIContent(changeLabel + " (텍스트 오류 또는 검사 대기 중)"));
                else
                {
                    menu.AddItem(new GUIContent(changeLabel + "/기본 화자 (" + g.Actor + ")"), current.Length == 0, () => _pending = WithSpeaker(doc, group, row, ""));
                    menu.AddItem(new GUIContent(changeLabel + "/서술"), current == TalkScript.Narration, () => _pending = WithSpeaker(doc, group, row, TalkScript.Narration));
                    menu.AddItem(new GUIContent(changeLabel + "/???"), current == TalkScript.Unknown, () => _pending = WithSpeaker(doc, group, row, TalkScript.Unknown));
                    foreach (string speaker in SpeakerNames(doc))
                    {
                        string selected = speaker;
                        menu.AddItem(new GUIContent(changeLabel + "/" + selected), current == selected, () => _pending = WithSpeaker(doc, group, row, selected));
                    }
                }
            }
            // 결과가 없는 선택지 행이면 결과 카드를 만드는 항목을 보인다.
            if (g.Section == Csv.SECTION_DIALOGUE && row >= 0 && row < g.Items.Count && g.Items[row].Get("response").Length == 0)
            {
                menu.AddSeparator("");
                string responseLabel = $"{row + 1}번 선택지에 결과 추가";
                if (!editable) menu.AddDisabledItem(new GUIContent(responseLabel + " (텍스트 오류 또는 검사 대기 중)"));
                else
                {
                    string key = KeyOf(g);
                    menu.AddItem(new GUIContent(responseLabel), false, () =>
                    {
                        _pending = WithResponse(doc, group, row, new List<string> { "새 결과" });
                        SelectResponse(key, row, 0);
                    });
                }
            }
            if (g.Section == Csv.SECTION_ENDING && !g.Id.StartsWith("@") && g.Get("after").Length == 0)
            {
                menu.AddSeparator("");
                if (editable) menu.AddItem(new GUIContent("after 추가"), false, () => _pending = AddAfter(doc, group));
                else menu.AddDisabledItem(new GUIContent("after 추가 (텍스트 오류 또는 검사 대기 중)"));
            }
            return menu;
        }

        /// <summary>
        /// 빈 곳 우클릭 메뉴를 만든다. 인물·회차 파일이면 상담 장면, actor별 Talk 대화, 엔딩, 층별 기억 파편을 추가할 수 있다.
        /// 텍스트 오류·검사 대기 중에는 항목을 비활성화한다. doc과 editable을 사용하며, 메뉴를 반환한다.
        /// </summary>
        private GenericMenu BuildCreateMenu(DialogueEditorDocument doc, bool editable)
        {
            var menu = new GenericMenu();
            if (doc.Kind != DialogueEditorKind.Day)
            {
                menu.AddDisabledItem(new GUIContent("새 그룹 추가는 인물·회차 파일에서 쓴다"));
                return menu;
            }
            string disabled = " (텍스트 오류 또는 검사 대기 중)";
            string dialogueActor = FirstActor(doc, Csv.SECTION_DIALOGUE) ?? FirstActor(doc, Csv.SECTION_ENDING);
            if (dialogueActor == null) menu.AddDisabledItem(new GUIContent("상담 장면 추가 (상담 actor가 없음)"));
            else if (!editable) menu.AddDisabledItem(new GUIContent("상담 장면 추가" + disabled));
            else menu.AddItem(new GUIContent("상담 장면 추가"), false, () => QueueCreate(doc, Csv.SECTION_DIALOGUE, dialogueActor));

            List<string> talkActors = TalkActorCandidates(doc);
            if (talkActors.Count == 0) menu.AddDisabledItem(new GUIContent("Talk 대화 추가 (actor가 없음)"));
            foreach (string actor in talkActors)
            {
                string a = actor;
                if (!editable) menu.AddDisabledItem(new GUIContent("Talk 대화 추가/" + a + disabled));
                else menu.AddItem(new GUIContent("Talk 대화 추가/" + a), false, () => QueueCreate(doc, Csv.SECTION_TALK, a));
            }

            if (dialogueActor == null) menu.AddDisabledItem(new GUIContent("엔딩 추가 (상담 actor가 없음)"));
            else if (!editable) menu.AddDisabledItem(new GUIContent("엔딩 추가" + disabled));
            else menu.AddItem(new GUIContent("엔딩 추가"), false, () => QueueCreate(doc, Csv.SECTION_ENDING, dialogueActor));

            // 기억 파편은 씬의 이 회차 손님 파편 묶음(층마다 하나)에 넣는다.
            menu.AddSeparator("");
            ClientCase client = DialogueEditorFormat.CaseOf(doc.AssetPath);
            var groups = new List<GameObject>();
            if (client != null) foreach (GameObject go in client.FragmentGroups) if (go != null) groups.Add(go);
            if (groups.Count == 0) menu.AddDisabledItem(new GUIContent("기억 파편 추가 (씬에 이 손님의 파편 묶음이 없음)"));
            else if (EditorApplication.isPlayingOrWillChangePlaymode) menu.AddDisabledItem(new GUIContent("기억 파편 추가 (Play Mode 중)"));
            else if (!editable || FragmentCreator == null) menu.AddDisabledItem(new GUIContent("기억 파편 추가" + disabled));
            else
                foreach (GameObject go in groups)
                {
                    GameObject group = go;
                    MazeLayer layer = group.GetComponentInParent<MazeLayer>(true);
                    // 메뉴 경로 구분자와 겹치지 않게 '/'를 바꾼다.
                    string name = (layer != null ? layer.name : group.name).Replace("/", "∕");
                    menu.AddItem(new GUIContent("기억 파편 추가/" + name), false, () => CreateFragment(client, group));
                }
            return menu;
        }

        /// <summary>
        /// 창에 새 기억 파편을 만들게 하고, 성공하면 그 파편 카드를 선택해 획득 문구 편집을 연다. 실패하면 이유를 보인다.
        /// client와 group(층 파편 묶음)을 사용하며, 선택과 인라인 상태를 바꾼다.
        /// </summary>
        private void CreateFragment(ClientCase client, GameObject group)
        {
            (bool ok, string error, MemoryFragment fragment) = FragmentCreator(client, group);
            if (!ok) { Notify(error); return; }
            _selKey = null;
            _selRow = -1;
            _selResponse = _selAfter = false;
            _selFragment = fragment;
            _selectionLine = 0;
            _focused = true;
            StartFragmentEdit(true);
        }

        /// <summary>
        /// 한 단계에 처음 나오는 actor를 찾는다.
        /// doc과 section을 사용하며, actor 또는 (없으면) null을 반환한다.
        /// </summary>
        private static string FirstActor(DialogueEditorDocument doc, string section)
        {
            foreach (DialogueEditorDocument.Group g in doc.Groups) if (g.Section == section) return g.Actor;
            return null;
        }

        /// <summary>
        /// 새 Talk 대화를 만들 actor 후보를 모은다. 파일에 이미 있는 talk actor와, 열린 씬에서 이 파일을 쓰는 필드 NPC의 actor ID다.
        /// doc을 사용하며, 겹치지 않는 actor 목록을 반환한다.
        /// </summary>
        private static List<string> TalkActorCandidates(DialogueEditorDocument doc)
        {
            var list = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
                if (g.Section == Csv.SECTION_TALK && !list.Contains(g.Actor)) list.Add(g.Actor);
            foreach (FieldNPC npc in Object.FindObjectsByType<FieldNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (npc.TalkCsv == null || AssetDatabase.GetAssetPath(npc.TalkCsv) != doc.AssetPath) continue;
                if (!string.IsNullOrEmpty(npc.DialogueActorId) && !list.Contains(npc.DialogueActorId)) list.Add(npc.DialogueActorId);
            }
            return list;
        }

        /// <summary>
        /// 새 그룹을 넣은 복사본을 만들어 다음 그리기에서 돌려주도록 둔다. 저장에 성공하면 CompleteCreate가 그 그룹을 선택하고 ID 편집을 연다.
        /// doc, section, actor를 사용하며, _pending과 _createdKey를 바꾼다.
        /// </summary>
        private void QueueCreate(DialogueEditorDocument doc, string section, string actor)
        {
            DialogueEditorDocument copy = CreateGroup(doc, section, actor, out string key);
            _pending = copy;
            _createdKey = key;
        }

        /// <summary>
        /// 새 그룹을 그 section·actor의 마지막 그룹 뒤에 넣은 복사본을 만든다. 기본 내용은 상담 장면이 상황과 선택지 한 줄,
        /// Talk 대화가 대사 한 줄, 엔딩이 title과 대사 한 줄이다. ID는 겹치지 않게 뒤에 번호를 붙인다.
        /// doc, section, actor를 사용하며, 새 문서를 반환하고 새 그룹 키를 key에 쓴다.
        /// </summary>
        public static DialogueEditorDocument CreateGroup(DialogueEditorDocument doc, string section, string actor, out string key)
        {
            string baseId = section == Csv.SECTION_DIALOGUE ? "new_scene" : section == Csv.SECTION_TALK ? "new_talk" : "new_ending";
            string id = baseId;
            for (int n = 2; doc.Find(section, actor, id) != null; n++) id = baseId + "_" + n;
            var g = new DialogueEditorDocument.Group { Section = section, Actor = actor, Id = id };
            var item = new DialogueEditorDocument.Item();
            item.Fields[DialogueEditorFormat.TEXT] = section == Csv.SECTION_DIALOGUE ? "새 선택지" : "새 대사";
            if (section == Csv.SECTION_DIALOGUE)
            {
                g.Fields["situation"] = "새 상황";
                foreach (string c in new[] { "trust", "dilation", "spring" }) item.Fields[c] = "0";
            }
            if (section == Csv.SECTION_ENDING) g.Fields["title"] = "새 엔딩";
            g.Items.Add(item);

            DialogueEditorDocument copy = doc.Clone();
            int at = copy.Groups.Count;
            for (int i = copy.Groups.Count - 1; i >= 0; i--)
                if (copy.Groups[i].Section == section && copy.Groups[i].Actor == actor) { at = i + 1; break; }
            copy.Groups.Insert(at, g);
            DialogueEditorFormat.Normalize(copy);
            key = KeyOf(g);
            return copy;
        }

        /// <summary>
        /// 창이 새 그룹 추가의 저장 결과를 알려준다. 성공하면 그 그룹을 선택하고 ID 인라인 편집을 연다.
        /// ok와 doc(저장된 현재 문서)을 사용하며, 선택과 인라인 상태를 바꾼다.
        /// </summary>
        public void CompleteCreate(bool ok, DialogueEditorDocument doc)
        {
            if (_createdKey == null) return;
            string key = _createdKey;
            _createdKey = null;
            if (!ok || doc == null || GroupIndex(doc, key) < 0) return;
            _selKey = key;
            _selRow = -1;
            _selResponse = _selAfter = false;
            _selFragment = null;
            _focused = true;
            _selectionLine = doc.Groups[GroupIndex(doc, key)].TextLine;
            StartRename(doc, true);
        }

        /// <summary>
        /// Talk 화자로 고를 수 있는 이름을 모은다. 파일에 이미 쓰인 화자, 열린 씬의 필드 인물 이름, 손님 이름 순서다.
        /// doc을 사용하며, 겹치지 않는 이름 목록을 반환한다.
        /// </summary>
        private static List<string> SpeakerNames(DialogueEditorDocument doc)
        {
            var speakers = new List<string>();
            foreach (DialogueEditorDocument.Group groupEntry in doc.Groups)
                foreach (DialogueEditorDocument.Item line in groupEntry.Items)
                {
                    string speaker = line.Get("speaker");
                    if (speaker.Length > 0 && speaker != TalkScript.Narration && speaker != TalkScript.Unknown && !speakers.Contains(speaker)) speakers.Add(speaker);
                }
            foreach (FieldNPC npc in Object.FindObjectsByType<FieldNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(npc.DisplayName) && !speakers.Contains(npc.DisplayName)) speakers.Add(npc.DisplayName);
            foreach (ClientCase client in Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(client.PersonName) && !speakers.Contains(client.PersonName)) speakers.Add(client.PersonName);
            return speakers;
        }

        /// <summary>
        /// Talk 대사 한 행의 화자를 바꾼 복사본을 만든다. 빈 값이면 speaker 칸을 지워 기본 화자(그 actor)가 말한다.
        /// doc, group, row, speaker를 사용하며, 새 문서를 반환한다.
        /// </summary>
        private static DialogueEditorDocument WithSpeaker(DialogueEditorDocument doc, int group, int row, string speaker)
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Item item = copy.Groups[group].Items[row];
            if (speaker.Length == 0) item.Fields.Remove("speaker");
            else item.Fields["speaker"] = speaker;
            return copy;
        }

        /// <summary>
        /// after가 없는 엔딩에 편집 가능한 새 문구를 넣은 복사본을 만든다.
        /// </summary>
        private static DialogueEditorDocument AddAfter(DialogueEditorDocument doc, int group)
        {
            DialogueEditorDocument copy = doc.Clone();
            copy.Groups[group].Fields["after"] = "새 after";
            return copy;
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
        public static DialogueEditorDocument InsertRow(DialogueEditorDocument doc, int group, int index, string speaker = "")
        {
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Group g = copy.Groups[group];
            var item = new DialogueEditorDocument.Item();
            if (g.Section == Csv.SECTION_DIALOGUE)
            {
                item.Fields[DialogueEditorFormat.TEXT] = "새 선택지";
                foreach (string c in new[] { "trust", "dilation", "spring" }) item.Fields[c] = "0";
            }
            else
            {
                item.Fields[DialogueEditorFormat.TEXT] = doc.Kind == DialogueEditorKind.Strings ? "새 문구" : "새 대사";
                if (g.Section == Csv.SECTION_TALK && !string.IsNullOrEmpty(speaker)) item.Fields["speaker"] = speaker;
            }
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
                    if (hit != null && _inline == InlineKind.None && (hit.Kind != BoxKind.Group || e.mousePosition.y < hit.Rect.y + Z(HEADER_HEIGHT)))
                    {
                        _dragNode = OffsetKey(doc, hit);
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
