using System.Collections.Generic;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 대사 편집기의 노드 뷰. 공통 모델을 매번 자동 배치해 그린다.
    /// Dialogue는 시작 노드부터 goto를 따라 위에서 아래로 층을 나눠 놓고, 다른 파일은 그룹 순서대로 격자에 놓는다.
    /// 화면에서 노드를 끌어 옮긴 위치와 화면 이동은 이 창에서만 쓰고 저장하지 않는다.
    /// 연결점을 끌어 다른 노드에 놓으면 goto가, 행 카드를 끌어 다른 자리에 놓으면 소속과 순서가 바뀐 새 문서를 돌려준다.
    /// </summary>
    public class DialogueEditorGraph
    {
        private const float NODE_WIDTH = 250f;
        private const float HEADER_HEIGHT = 22f;
        private const float SITUATION_HEIGHT = 34f;
        private const float ROW_HEIGHT = 20f;
        private const float NODE_PADDING = 6f;
        private const float GAP_X = 40f;
        private const float GAP_Y = 70f;
        private const float ENDING_WIDTH = 130f;
        private const float MARGIN = 20f;

        /// <summary>화면에 놓인 상자 하나 (그룹 또는 엔딩 카드)</summary>
        private class Box
        {
            public int Group = -1;
            public string EndingId;
            public Rect Rect;
            public float RowsTop;
            public int Level;
        }

        private readonly Dictionary<string, Vector2> _nodeOffsets = new Dictionary<string, Vector2>();
        private Vector2 _pan;
        private string _dragNode;
        private bool _panning;
        // 끌고 있는 연결선 또는 카드 (그룹 번호, 행 번호). 없으면 -1이다.
        private int _linkGroup = -1;
        private int _linkItem = -1;
        private int _cardGroup = -1;
        private int _cardItem = -1;
        private Vector2 _mouse;

        private static GUIStyle _rowStyle;
        private static GUIStyle _headerStyle;
        private static GUIStyle _situationStyle;

        /// <summary>
        /// 새 파일을 열 때 화면 이동과 노드 위치 옮김을 지운다.
        /// 입력값은 없으며, _pan과 _nodeOffsets를 비운다.
        /// </summary>
        public void Reset()
        {
            _pan = Vector2.zero;
            _nodeOffsets.Clear();
            _dragNode = null;
            _panning = false;
            ClearDrag();
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

            GUI.BeginClip(rect);
            Event e = Event.current;
            bool inside = new Rect(0f, 0f, rect.width, rect.height).Contains(e.mousePosition);
            DialogueEditorDocument changed = editable ? HandleEditInput(e, doc, boxes, inside) : null;
            if (!editable) ClearDrag();
            HandleViewInput(e, doc, boxes, inside);
            // 선이 상자에 가리지 않도록 상자를 먼저 그리고 선을 위에 그린다.
            foreach (Box b in boxes) DrawBox(doc, b);
            DrawEdges(doc, boxes);
            DrawDragFeedback(doc, boxes);
            if (!string.IsNullOrEmpty(notice))
            {
                var r = new Rect(8f, 6f, rect.width - 16f, 20f);
                EditorGUI.DrawRect(r, new Color(0.45f, 0.18f, 0.15f, 0.9f));
                GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width - 12f, r.height), notice, EditorStyles.whiteMiniLabel);
            }
            GUI.EndClip();
            return changed;
        }

        // ---------------------------------------------------------------- 배치

        /// <summary>
        /// 모델의 그룹과 엔딩 카드를 자동 배치한다.
        /// doc과 viewWidth를 사용하며, 화면 위치(이동 반영)가 정해진 상자 목록을 반환한다.
        /// </summary>
        private List<Box> Layout(DialogueEditorDocument doc, float viewWidth)
        {
            var boxes = doc.Kind == DialogueEditorKind.Dialogue ? LayoutTree(doc) : LayoutGrid(doc, viewWidth);
            foreach (Box b in boxes)
            {
                Vector2 offset = Vector2.zero;
                if (b.Group >= 0) _nodeOffsets.TryGetValue(doc.Groups[b.Group].Id, out offset);
                b.Rect.position += _pan + offset;
                b.RowsTop += _pan.y + offset.y;
            }
            return boxes;
        }

        /// <summary>
        /// Dialogue를 시작 노드부터 goto를 따라 층으로 나눠 위에서 아래로 놓는다. 닿지 않는 노드는 마지막 층에, 엔딩 카드는 그 아래에 놓는다.
        /// doc을 사용하며, 상자 목록을 반환한다.
        /// </summary>
        private List<Box> LayoutTree(DialogueEditorDocument doc)
        {
            var level = new Dictionary<int, int>();
            var order = new List<int>();
            var queue = new Queue<int>();
            if (doc.Groups.Count > 0) { level[0] = 0; queue.Enqueue(0); }
            while (queue.Count > 0)
            {
                int g = queue.Dequeue();
                order.Add(g);
                foreach (DialogueEditorDocument.Item i in doc.Groups[g].Items)
                {
                    int t = IndexOf(doc, i.Get(DialogueEditorFormat.GOTO));
                    if (t < 0 || level.ContainsKey(t)) continue;
                    level[t] = level[g] + 1;
                    queue.Enqueue(t);
                }
            }
            int maxLevel = 0;
            foreach (int l in level.Values) maxLevel = Mathf.Max(maxLevel, l);
            for (int g = 0; g < doc.Groups.Count; g++)
                if (!level.ContainsKey(g)) { level[g] = maxLevel + 1; order.Add(g); }

            var rows = new SortedDictionary<int, List<Box>>();
            foreach (int g in order)
            {
                var b = new Box { Group = g, Level = level[g] };
                b.Rect.size = new Vector2(NODE_WIDTH, NodeHeight(doc, doc.Groups[g]));
                if (!rows.TryGetValue(b.Level, out List<Box> list)) rows[b.Level] = list = new List<Box>();
                list.Add(b);
            }

            var endings = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
                foreach (DialogueEditorDocument.Item i in g.Items)
                {
                    string end = i.Get(DialogueEditorFormat.ENDING);
                    if (end.Length > 0 && !endings.Contains(end)) endings.Add(end);
                }
            if (endings.Count > 0)
            {
                int endLevel = rows.Count > 0 ? LastKey(rows) + 1 : 0;
                var list = new List<Box>();
                foreach (string end in endings)
                {
                    var b = new Box { EndingId = end, Level = endLevel };
                    b.Rect.size = new Vector2(ENDING_WIDTH, HEADER_HEIGHT + 4f);
                    list.Add(b);
                }
                rows[endLevel] = list;
            }

            float widest = 0f;
            foreach (List<Box> list in rows.Values) widest = Mathf.Max(widest, RowWidth(list));
            float y = MARGIN + 24f;
            var all = new List<Box>();
            foreach (List<Box> list in rows.Values)
            {
                float x = MARGIN + (widest - RowWidth(list)) * 0.5f;
                float h = 0f;
                foreach (Box b in list)
                {
                    b.Rect.position = new Vector2(x, y);
                    b.RowsTop = y + RowsOffset(doc, b);
                    x += b.Rect.width + GAP_X;
                    h = Mathf.Max(h, b.Rect.height);
                    all.Add(b);
                }
                y += h + GAP_Y;
            }
            return all;
        }

        /// <summary>
        /// Talk·Ending·Strings의 그룹을 순서대로 왼쪽에서 오른쪽, 위에서 아래로 격자에 놓는다.
        /// doc과 viewWidth를 사용하며, 상자 목록을 반환한다.
        /// </summary>
        private List<Box> LayoutGrid(DialogueEditorDocument doc, float viewWidth)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth - MARGIN) / (NODE_WIDTH + GAP_X)));
            var all = new List<Box>();
            float y = MARGIN + 24f;
            for (int start = 0; start < doc.Groups.Count; start += columns)
            {
                float h = 0f;
                for (int c = 0; c < columns && start + c < doc.Groups.Count; c++)
                {
                    int g = start + c;
                    var b = new Box { Group = g, Level = start / columns };
                    b.Rect = new Rect(MARGIN + c * (NODE_WIDTH + GAP_X), y, NODE_WIDTH, NodeHeight(doc, doc.Groups[g]));
                    b.RowsTop = y + RowsOffset(doc, b);
                    h = Mathf.Max(h, b.Rect.height);
                    all.Add(b);
                }
                y += h + GAP_X;
            }
            return all;
        }

        /// <summary>
        /// 그룹 상자의 높이를 정한다.
        /// doc과 g를 사용하며, 높이를 반환한다.
        /// </summary>
        private static float NodeHeight(DialogueEditorDocument doc, DialogueEditorDocument.Group g)
        {
            return HEADER_HEIGHT + SubHeight(doc, g) + Mathf.Max(1, g.Items.Count) * ROW_HEIGHT + NODE_PADDING;
        }

        /// <summary>
        /// 머리 아래에 그룹 칸(상황, 조건, 제목)을 보여줄 높이를 정한다.
        /// doc과 g를 사용하며, 높이를 반환한다.
        /// </summary>
        private static float SubHeight(DialogueEditorDocument doc, DialogueEditorDocument.Group g)
        {
            return GroupSummary(doc, g).Length > 0 ? SITUATION_HEIGHT : 0f;
        }

        /// <summary>
        /// 상자 위쪽에서 첫 행까지의 거리를 구한다.
        /// doc과 b를 사용하며, 거리를 반환한다.
        /// </summary>
        private static float RowsOffset(DialogueEditorDocument doc, Box b)
        {
            return b.Group < 0 ? 0f : HEADER_HEIGHT + SubHeight(doc, doc.Groups[b.Group]);
        }

        /// <summary>
        /// 한 층에 놓인 상자들의 전체 너비를 구한다.
        /// list를 사용하며, 너비를 반환한다.
        /// </summary>
        private static float RowWidth(List<Box> list)
        {
            float w = 0f;
            foreach (Box b in list) w += b.Rect.width;
            return w + Mathf.Max(0, list.Count - 1) * GAP_X;
        }

        /// <summary>
        /// 정렬된 사전의 마지막 키를 구한다.
        /// rows를 사용하며, 가장 큰 키를 반환한다.
        /// </summary>
        private static int LastKey(SortedDictionary<int, List<Box>> rows)
        {
            int last = 0;
            foreach (int k in rows.Keys) last = k;
            return last;
        }

        /// <summary>
        /// 그룹 id의 순서 번호를 찾는다.
        /// doc과 id를 사용하며, 번호 또는 -1을 반환한다.
        /// </summary>
        private static int IndexOf(DialogueEditorDocument doc, string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < doc.Groups.Count; i++) if (doc.Groups[i].Id == id) return i;
            return -1;
        }

        // ---------------------------------------------------------------- 그리기

        /// <summary>
        /// goto 화살표와 엔딩 연결선을 그린다. 앞 층이나 같은 노드로 돌아가는 선도 그대로 이어 그린다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawEdges(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (doc.Kind != DialogueEditorKind.Dialogue || Event.current.type != EventType.Repaint) return;
            var byGroup = new Dictionary<int, Box>();
            var byEnding = new Dictionary<string, Box>();
            foreach (Box b in boxes)
            {
                if (b.Group >= 0) byGroup[b.Group] = b;
                else byEnding[b.EndingId] = b;
            }
            foreach (Box b in boxes)
            {
                if (b.Group < 0) continue;
                List<DialogueEditorDocument.Item> items = doc.Groups[b.Group].Items;
                for (int k = 0; k < items.Count; k++)
                {
                    Vector2 from = PortPosition(b, k);
                    int t = IndexOf(doc, items[k].Get(DialogueEditorFormat.GOTO));
                    if (t >= 0 && byGroup.TryGetValue(t, out Box target))
                        DrawArrow(from, new Vector2(target.Rect.center.x, target.Rect.yMin), new Color(0.45f, 0.75f, 1f));
                    string end = items[k].Get(DialogueEditorFormat.ENDING);
                    if (end.Length > 0 && byEnding.TryGetValue(end, out Box endBox))
                        DrawArrow(from, new Vector2(endBox.Rect.center.x, endBox.Rect.yMin), new Color(1f, 0.6f, 0.3f));
                }
            }
        }

        /// <summary>
        /// 선택지 행 오른쪽 끝의 연결점 위치를 구한다.
        /// b와 행 번호 row를 사용하며, 위치를 반환한다.
        /// </summary>
        private static Vector2 PortPosition(Box b, int row)
        {
            return new Vector2(b.Rect.xMax - 9f, b.RowsTop + row * ROW_HEIGHT + ROW_HEIGHT * 0.5f);
        }

        /// <summary>
        /// 연결점에서 대상 위쪽까지 곡선 화살표를 그린다.
        /// from, to, color를 사용한다.
        /// </summary>
        private static void DrawArrow(Vector2 from, Vector2 to, Color color)
        {
            Vector3 a = from, b = to;
            Handles.DrawBezier(a, b, a + new Vector3(80f, 0f), b + new Vector3(0f, -80f), color, null, 2.5f);
            Color old = Handles.color;
            Handles.color = color;
            Handles.DrawAAConvexPolygon(b, b + new Vector3(-6f, -10f), b + new Vector3(6f, -10f));
            Handles.color = old;
        }

        /// <summary>
        /// 그룹 상자 또는 엔딩 카드를 그린다.
        /// doc과 b를 사용한다.
        /// </summary>
        private void DrawBox(DialogueEditorDocument doc, Box b)
        {
            if (b.Group < 0)
            {
                EditorGUI.DrawRect(b.Rect, new Color(0.45f, 0.26f, 0.12f));
                GUI.Label(new Rect(b.Rect.x + 6f, b.Rect.y + 3f, b.Rect.width - 12f, HEADER_HEIGHT), "엔딩 · " + b.EndingId, _headerStyle);
                return;
            }
            DialogueEditorDocument.Group g = doc.Groups[b.Group];
            EditorGUI.DrawRect(b.Rect, new Color(0.24f, 0.24f, 0.26f));
            var header = new Rect(b.Rect.x, b.Rect.y, b.Rect.width, HEADER_HEIGHT);
            bool start = doc.Kind == DialogueEditorKind.Dialogue && b.Group == 0;
            EditorGUI.DrawRect(header, start ? new Color(0.2f, 0.42f, 0.3f) : new Color(0.3f, 0.32f, 0.4f));
            string title = doc.Kind == DialogueEditorKind.Dialogue ? (start ? "시작 · " : "") + g.Id : $"{b.Group + 1}. {g.Id}";
            GUI.Label(new Rect(header.x + 6f, header.y + 3f, header.width - 12f, header.height), title, _headerStyle);

            string summary = GroupSummary(doc, g);
            if (summary.Length > 0)
                GUI.Label(new Rect(b.Rect.x + 6f, header.yMax + 1f, b.Rect.width - 12f, SITUATION_HEIGHT - 2f), summary, _situationStyle);

            if (g.Items.Count == 0)
                GUI.Label(new Rect(b.Rect.x + 6f, b.RowsTop, b.Rect.width - 12f, ROW_HEIGHT), "(행 없음)", _rowStyle);
            for (int k = 0; k < g.Items.Count; k++)
            {
                var row = new Rect(b.Rect.x + 4f, b.RowsTop + k * ROW_HEIGHT + 1f, b.Rect.width - 8f, ROW_HEIGHT - 2f);
                EditorGUI.DrawRect(row, new Color(0.3f, 0.3f, 0.32f));
                GUI.Label(new Rect(row.x + 4f, row.y, row.width - 26f, row.height), RowText(doc, g.Items[k], k), _rowStyle);
                if (doc.Kind == DialogueEditorKind.Dialogue) DrawPort(b, k, g.Items[k]);
            }
        }

        /// <summary>
        /// 선택지 행 오른쪽에 연결 상태 표시를 그린다. goto는 파란 점, ending은 주황 점, 둘 다 없으면 머묾 표시다.
        /// b, row, item을 사용한다.
        /// </summary>
        private static void DrawPort(Box b, int row, DialogueEditorDocument.Item item)
        {
            Vector2 p = PortPosition(b, row);
            bool hasGoto = item.Get(DialogueEditorFormat.GOTO).Length > 0;
            bool hasEnding = item.Get(DialogueEditorFormat.ENDING).Length > 0;
            if (!hasGoto && !hasEnding)
            {
                GUI.Label(new Rect(p.x - 9f, p.y - 9f, 18f, 18f), "↻", _rowStyle);
                return;
            }
            EditorGUI.DrawRect(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), hasEnding ? new Color(1f, 0.6f, 0.3f) : new Color(0.45f, 0.75f, 1f));
        }

        /// <summary>
        /// 그룹 상자 머리 아래에 보여줄 그룹 칸 요약을 만든다.
        /// doc과 g를 사용하며, 요약 글(없으면 빈 글)을 반환한다.
        /// </summary>
        private static string GroupSummary(DialogueEditorDocument doc, DialogueEditorDocument.Group g)
        {
            switch (doc.Kind)
            {
                case DialogueEditorKind.Dialogue: return Flat(g.Get("situation"));
                case DialogueEditorKind.Talk:
                    var parts = new List<string>();
                    if (g.Get("need").Length > 0) parts.Add("need " + g.Get("need"));
                    if (g.Get("set").Length > 0) parts.Add("set " + g.Get("set"));
                    return string.Join("  ·  ", parts);
                case DialogueEditorKind.Ending: return Flat(g.Get("title"));
                default: return "";
            }
        }

        /// <summary>
        /// 행 상자에 보여줄 한 줄 글을 만든다.
        /// doc, item, index를 사용하며, 글을 반환한다.
        /// </summary>
        private static string RowText(DialogueEditorDocument doc, DialogueEditorDocument.Item item, int index)
        {
            string body = Flat(item.Get(DialogueEditorFormat.SchemaOf(doc.Kind).BodyColumn));
            if (doc.Kind == DialogueEditorKind.Dialogue)
            {
                string need = item.Get("need");
                return $"{index + 1}. {body}" + (need.Length > 0 ? $"  [{need}]" : "");
            }
            if (doc.Kind == DialogueEditorKind.Talk)
            {
                string sp = item.Get("speaker");
                string who = sp.Length == 0 ? "본인" : sp == TalkScript.Narration ? "서술" : sp;
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
        /// 노드 뷰 글자 모양을 한 번 만든다.
        /// 입력값은 없으며, 정적 스타일 필드를 채운다.
        /// </summary>
        private static void EnsureStyles()
        {
            if (_rowStyle != null) return;
            _rowStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, wordWrap = false };
            _rowStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip, fontSize = 11 };
            _headerStyle.normal.textColor = Color.white;
            _situationStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, clipping = TextClipping.Clip };
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
                    if (hit == null || hit.Group < 0) return null;
                    int row = RowAt(doc, hit, e.mousePosition.y);
                    if (row < 0) return null;
                    if (doc.Kind == DialogueEditorKind.Dialogue && Vector2.Distance(PortPosition(hit, row), e.mousePosition) <= 9f)
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
        /// 끌던 연결선을 놓는다. 다른 Dialogue 노드 위면 그 노드로 goto를 바꾸고, 함께 적힌 ending은 지운다.
        /// doc, boxes, p를 사용하며, 바뀐 복사본 또는 (바뀌지 않으면) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument DropLink(DialogueEditorDocument doc, List<Box> boxes, Vector2 p)
        {
            Box target = HitBox(boxes, p);
            if (target == null || target.Group < 0 || target.Group == _linkGroup) return null;
            if (_linkItem >= doc.Groups[_linkGroup].Items.Count) return null;
            string id = doc.Groups[target.Group].Id;
            DialogueEditorDocument.Item src = doc.Groups[_linkGroup].Items[_linkItem];
            if (src.Get(DialogueEditorFormat.GOTO) == id && src.Get(DialogueEditorFormat.ENDING).Length == 0) return null;
            DialogueEditorDocument copy = doc.Clone();
            DialogueEditorDocument.Item item = copy.Groups[_linkGroup].Items[_linkItem];
            item.Fields[DialogueEditorFormat.GOTO] = id;
            item.Fields.Remove(DialogueEditorFormat.ENDING);
            return copy;
        }

        /// <summary>
        /// 끌던 행 카드를 놓는다. 놓은 그룹과 순서 위치로 그 행을 옮긴다.
        /// doc, boxes, p를 사용하며, 바뀐 복사본 또는 (바뀌지 않으면) null을 반환한다.
        /// </summary>
        private DialogueEditorDocument DropCard(DialogueEditorDocument doc, List<Box> boxes, Vector2 p)
        {
            Box target = HitBox(boxes, p);
            if (target == null || target.Group < 0 || _cardItem >= doc.Groups[_cardGroup].Items.Count) return null;
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
        private static int RowAt(DialogueEditorDocument doc, Box b, float y)
        {
            int row = Mathf.FloorToInt((y - b.RowsTop) / ROW_HEIGHT);
            return row >= 0 && row < doc.Groups[b.Group].Items.Count ? row : -1;
        }

        /// <summary>
        /// 카드를 놓을 순서 위치를 구한다. 행 경계 중 가장 가까운 곳이다.
        /// doc, b, y를 사용하며, 0부터 행 수까지의 위치를 반환한다.
        /// </summary>
        private static int InsertIndex(DialogueEditorDocument doc, Box b, float y)
        {
            int count = doc.Groups[b.Group].Items.Count;
            return Mathf.Clamp(Mathf.RoundToInt((y - b.RowsTop) / ROW_HEIGHT), 0, count);
        }

        /// <summary>
        /// 끌고 있는 연결선이나 카드, 놓일 위치 표시를 그린다.
        /// doc과 boxes를 사용한다.
        /// </summary>
        private void DrawDragFeedback(DialogueEditorDocument doc, List<Box> boxes)
        {
            if (Event.current.type != EventType.Repaint) return;
            Box hover = HitBox(boxes, _mouse);
            if (_linkGroup >= 0)
            {
                foreach (Box b in boxes)
                    if (b.Group == _linkGroup)
                    {
                        Vector3 a = PortPosition(b, _linkItem);
                        Handles.DrawBezier(a, _mouse, a + new Vector3(60f, 0f), (Vector3)_mouse + new Vector3(-30f, 0f), Color.white, null, 2f);
                    }
                if (hover != null && hover.Group >= 0 && hover.Group != _linkGroup) DrawOutline(hover.Rect, new Color(0.45f, 0.75f, 1f));
            }
            else if (_cardGroup >= 0 && _cardItem < doc.Groups[_cardGroup].Items.Count)
            {
                if (hover != null && hover.Group >= 0)
                {
                    float y = hover.RowsTop + InsertIndex(doc, hover, _mouse.y) * ROW_HEIGHT;
                    EditorGUI.DrawRect(new Rect(hover.Rect.x + 2f, y - 1.5f, hover.Rect.width - 4f, 3f), new Color(1f, 0.85f, 0.3f));
                }
                var ghost = new Rect(_mouse.x - 10f, _mouse.y - ROW_HEIGHT * 0.5f, NODE_WIDTH - 8f, ROW_HEIGHT - 2f);
                EditorGUI.DrawRect(ghost, new Color(0.4f, 0.4f, 0.48f, 0.9f));
                GUI.Label(new Rect(ghost.x + 4f, ghost.y, ghost.width - 8f, ghost.height), RowText(doc, doc.Groups[_cardGroup].Items[_cardItem], _cardItem), _rowStyle);
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
                    if (hit != null && hit.Group >= 0 && e.mousePosition.y < hit.Rect.y + HEADER_HEIGHT)
                    {
                        _dragNode = doc.Groups[hit.Group].Id;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_panning) { _pan += e.delta; e.Use(); }
                    else if (_dragNode != null)
                    {
                        _nodeOffsets.TryGetValue(_dragNode, out Vector2 o);
                        _nodeOffsets[_dragNode] = o + e.delta;
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
