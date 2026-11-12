using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 든 실의 시작 기억을 보관하고, 시작 자리에서 시선이 있는 칸까지 지금 미로의 열린 통로를 따라 이어지는 길을 찾아 실 하나를 그린다.
    /// 길은 칸 단위로 찾고, 통로 가운데(고리의 가운데 원호와 열린 칸막이)를 따라 그린다. 그릴 때마다 층의 지금 회전을 반영해 깜빡임 중에도 통로에 붙어 있다.
    /// 길이 없으면 실을 숨긴다. 이은 쌍마다 _linkedLines의 렌더러 하나로 두 자리 사이의 길에 실을 남기고, 미로가 바뀌면 다시 찾는다.
    /// 파편과 시선이 위치를 정한 뒤에 그리도록 늦게 실행한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MemoryThread : MonoBehaviour
    {
        private const int ARC_STEPS = 4;

        [Header("든 실")]
        [Tooltip("든 실을 그릴 렌더러 (이 오브젝트의 LineRenderer)")]
        [SerializeField] private LineRenderer _line;
        [Tooltip("실의 끝이 따라갈 시선")]
        [SerializeField] private GazeCursor _cursor;
        [Tooltip("실이 따라갈 미로. 열린 통로로 길을 찾을 때 쓴다")]
        [SerializeField] private IrisMaze _maze;
        [Tooltip("미로가 바뀌었는지 보고 길을 다시 찾는 간격(초)")]
        [SerializeField] private float _repathSeconds = 0.25f;
        [Tooltip("실의 색")]
        [SerializeField] private Color _threadColor = new Color(0.6f, 0.95f, 1f, 0.5f);
        [Tooltip("실의 굵기")]
        [SerializeField] private float _threadWidth = 0.03f;

        [Header("이은 실")]
        [Tooltip("이은 두 자리 사이에 실을 남길지")]
        [SerializeField] private bool _keepLinkedThreads = true;
        [Tooltip("이은 뒤 남는 실의 색")]
        [SerializeField] private Color _linkedThreadColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("이은 뒤 남는 실을 그릴 렌더러들 (이 오브젝트의 자식). 이은 쌍마다 하나씩 쓴다")]
        [SerializeField] private LineRenderer[] _linkedLines = new LineRenderer[0];

        private MemoryFragment _from;
        // 든 실이 지나는 칸 목록. 비어 있으면 길이 없다.
        private readonly List<Vector2Int> _heldCells = new List<Vector2Int>();
        private Vector2Int _heldTarget = new Vector2Int(-1, -1);
        private float _repathT;
        private readonly List<(MemoryFragment a, MemoryFragment b, List<Vector2Int> cells)> _linked =
            new List<(MemoryFragment, MemoryFragment, List<Vector2Int>)>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly Dictionary<Vector2Int, Vector2Int> _cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        private readonly Queue<Vector2Int> _open = new Queue<Vector2Int>();

        /// <summary>든 실의 시작 기억. 없으면 실을 들지 않은 상태</summary>
        public MemoryFragment From => _from;

        void LateUpdate()
        {
            _repathT -= Time.deltaTime;
            bool repath = _repathT <= 0f;
            if (repath) _repathT = _repathSeconds;

            // 손님이 바뀌어 파편 묶음이 꺼져 있는 동안에는 그 파편의 실을 그리지 않는다.
            bool held = _from != null && _from.gameObject.activeInHierarchy;
            if (held)
            {
                Vector2Int target = new Vector2Int(_cursor.Ring, _cursor.Sector);
                if (repath || target != _heldTarget)
                {
                    _heldTarget = target;
                    FindPath(new Vector2Int(_from.Ring, _from.WorldSector), target, _heldCells);
                }
                held = _heldCells.Count > 0;
                if (held) DrawPath(_line, _from.transform.position, _heldCells, _cursor.transform.position, _threadColor, true);
            }
            _line.enabled = held;

            for (int i = 0; i < _linkedLines.Length; i++)
            {
                bool on = i < _linked.Count && _linked[i].a.gameObject.activeInHierarchy && _linked[i].b.gameObject.activeInHierarchy;
                if (on)
                {
                    (MemoryFragment a, MemoryFragment b, List<Vector2Int> cells) l = _linked[i];
                    if (repath) FindPath(new Vector2Int(l.a.Ring, l.a.WorldSector), new Vector2Int(l.b.Ring, l.b.WorldSector), l.cells);
                    on = l.cells.Count > 0;
                    if (on) DrawPath(_linkedLines[i], l.a.transform.position, l.cells, l.b.transform.position, _linkedThreadColor, false);
                }
                _linkedLines[i].enabled = on;
            }
        }

        /// <summary>
        /// 기억으로 실을 든다. 이미 든 실이 있으면 그 실을 없애고 새로 든다. 다음 프레임에 길을 바로 찾는다.
        /// from(시작 기억)을 사용하며, _from, _heldCells, 길 찾기 시간을 바꾼다.
        /// </summary>
        public void Hold(MemoryFragment from)
        {
            _from = from;
            _heldCells.Clear();
            _heldTarget = new Vector2Int(-1, -1);
            _repathT = 0f;
        }

        /// <summary>
        /// 든 실을 없앤다.
        /// 입력값은 없으며, _from과 _heldCells를 비운다.
        /// </summary>
        public void Release()
        {
            _from = null;
            _heldCells.Clear();
        }

        /// <summary>
        /// 이은 두 자리 사이에 남길 실을 더한다. 길은 다음 프레임부터 지금 미로 기준으로 찾는다. _keepLinkedThreads가 꺼져 있으면 남기지 않는다.
        /// a와 b(이은 두 기억)를 사용하며, _linked에 쌍을 더하고 길 찾기 시간을 당긴다.
        /// </summary>
        public void AddLinked(MemoryFragment a, MemoryFragment b)
        {
            if (!_keepLinkedThreads) return;
            _linked.Add((a, b, new List<Vector2Int>()));
            _repathT = 0f;
        }

        /// <summary>
        /// fragments 중 하나라도 들어 있는 이은 실을 지운다.
        /// fragments(지울 파편 목록)를 사용하며, _linked에서 그 쌍들을 뺀다.
        /// </summary>
        public void ClearLinked(ICollection<MemoryFragment> fragments)
        {
            _linked.RemoveAll(l => fragments.Contains(l.a) || fragments.Contains(l.b));
        }

        /// <summary>
        /// 지금 미로의 열린 통로로 start 칸에서 goal 칸까지 가장 짧은 칸 목록을 찾는다. 칸은 (고리, 월드 칸 번호)다.
        /// start, goal과 _maze의 벽 상태를 사용하며, cells를 start부터 goal까지의 칸으로 채운다. 길이 없으면 cells를 비운다.
        /// </summary>
        private void FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> cells)
        {
            cells.Clear();
            _cameFrom.Clear();
            _open.Clear();
            _open.Enqueue(start);
            _cameFrom[start] = start;
            while (_open.Count > 0)
            {
                Vector2Int c = _open.Dequeue();
                if (c == goal) break;
                TryStep(c, new Vector2Int(c.x, _maze.Mod(c.y + 1)), _maze.IsRadialOpen(c.x, c.y));
                TryStep(c, new Vector2Int(c.x, _maze.Mod(c.y - 1)), _maze.IsRadialOpen(c.x, _maze.Mod(c.y - 1)));
                if (c.x + 1 < _maze.Rings) TryStep(c, new Vector2Int(c.x + 1, c.y), _maze.IsArcOpen(c.x, c.y, out _));
                if (c.x > 0) TryStep(c, new Vector2Int(c.x - 1, c.y), _maze.IsArcOpen(c.x - 1, c.y, out _));
            }
            if (!_cameFrom.ContainsKey(goal)) return;
            for (Vector2Int c = goal; ; c = _cameFrom[c])
            {
                cells.Add(c);
                if (c == start) break;
            }
            cells.Reverse();
        }

        /// <summary>
        /// 열린 면이면 이웃 칸을 탐색 목록에 넣는다.
        /// from, to(이웃 칸), open(그 면이 열렸는지)을 사용하며, _cameFrom과 _open을 바꾼다.
        /// </summary>
        private void TryStep(Vector2Int from, Vector2Int to, bool open)
        {
            if (!open || _cameFrom.ContainsKey(to)) return;
            _cameFrom[to] = from;
            _open.Enqueue(to);
        }

        /// <summary>
        /// 시작 위치에서 칸 목록의 통로 가운데를 따라 끝 위치까지 실을 그린다. 같은 고리의 옆 칸은 원호로, 고리를 넘을 때는 곧게 잇는다.
        /// stopAtEnd가 켜져 있으면 마지막 칸의 가운데를 거치지 않고, 그 칸에 들어선 경계에서 끝 위치로 바로 이어 끝보다 앞에 실이 생기지 않는다.
        /// line, start, cells, end, color, stopAtEnd와 _threadWidth를 사용하며, line의 점 목록, 굵기, 색을 바꾼다.
        /// </summary>
        private void DrawPath(LineRenderer line, Vector3 start, List<Vector2Int> cells, Vector3 end, Color color, bool stopAtEnd)
        {
            _points.Clear();
            _points.Add(ToRenderer(line, start));
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                // 수정(017): 시선이 있는 칸은 가운데까지 그리지 않아 실이 시선보다 앞서 나가지 않게 했다.
                bool last = stopAtEnd && i == cells.Count - 1;
                if (i > 0 && cells[i - 1].x == c.x)
                {
                    // 같은 고리의 옆 칸으로 갈 때는 고리 가운데 원호를 따라간다. 마지막 칸이면 두 칸의 경계까지만 간다.
                    int step = _maze.Mod(c.y - cells[i - 1].y) == 1 ? 1 : -1;
                    int steps = last ? ARC_STEPS / 2 : ARC_STEPS - 1;
                    for (int k = 1; k <= steps; k++)
                        _points.Add(ToRenderer(line, CellPoint(c.x, cells[i - 1].y, 0.5f + step * k / (float)ARC_STEPS)));
                }
                if (!last) _points.Add(ToRenderer(line, CellPoint(c.x, c.y, 0.5f)));
            }
            _points.Add(ToRenderer(line, end));
            Shapes.SetPoints(line, _points.ToArray(), false);
            line.widthMultiplier = _threadWidth;
            Shapes.SetColor(line, color);
        }

        /// <summary>
        /// 고리 가운데 원 위의 한 점을 그 고리가 속한 층의 지금 회전을 반영한 월드 위치로 구한다.
        /// ring, worldSector, t(칸 안에서의 각도 비율, 0.5가 칸 가운데)를 사용하며, 월드 위치를 반환한다.
        /// </summary>
        private Vector3 CellPoint(int ring, int worldSector, float t)
        {
            MazeLayer layer = _maze.GetLayer(_maze.LayerOfRing(ring));
            int offset = layer != null ? layer.Offset : 0;
            Vector3 local = Shapes.Polar(_maze.RingMid(ring), (worldSector - offset + t) * _maze.SectorDeg);
            return layer != null ? layer.transform.TransformPoint(local) : _maze.transform.TransformPoint(local);
        }

        /// <summary>
        /// 월드 위치를 렌더러가 쓰는 좌표로 바꾼다.
        /// line과 world를 사용하며, 렌더러 좌표를 반환한다.
        /// </summary>
        private static Vector3 ToRenderer(LineRenderer line, Vector3 world) => line.useWorldSpace ? world : line.transform.InverseTransformPoint(world);
    }
}
