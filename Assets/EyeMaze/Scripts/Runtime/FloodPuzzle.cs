using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 047: 파편을 열 때 삼각형 칸 판의 한 영역에 색을 부으면 이어진 같은 색 영역 전체가 그 색으로 퍼지는 채우기 퍼즐.
    /// 제한 수 안에 판 전체를 파편 감정색으로 채우면 파편을 준다.
    /// </summary>
    public class FloodPuzzle : MonoBehaviour
    {
        private const int COLOR_COUNT = 4;
        private const int WHITE = 0;
        private const int GREEN = 1;
        private const int RED = 2;
        private const int BLUE = 3;
        private const int MIN_ROWS = 2;
        private const int MIN_COLUMNS = 3;
        private const int MIN_REGIONS = 2;
        private const int GENERATE_TRIES = 2000;
        private const int MAX_GRAPH_REGIONS = 64;

        [Header("퍼즐")]
        [Tooltip("판 줄 수")]
        [SerializeField] private int _rows = 7;
        [Tooltip("한 줄의 삼각형 수")]
        [SerializeField] private int _columns = 12;
        [Tooltip("층별 처음 영역 시작 칸 수 (0번이 1층). 층이 더 많으면 마지막 값을 쓴다")]
        [SerializeField] private int[] _regionCounts = { 10, 10, 12 };
        [Tooltip("최소 수가 이보다 적은 판은 다시 만든다")]
        [SerializeField, Min(1)] private int _minMoves = 3;
        [Tooltip("최소 수가 이보다 많은 판은 다시 만든다. 클수록 판 생성 탐색이 느려진다")]
        [SerializeField, Min(1)] private int _maxMoves = 6;
        [Tooltip("최소 수에 더해 주는 여유 수. 0이면 최소 수와 정확히 같은 수로만 풀린다")]
        [SerializeField, Min(0)] private int _extraMoves;
        [Tooltip("시작 칸 중 정답색으로 정해 두는 비율. 나머지는 4색 중 무작위다")]
        [SerializeField, Range(0f, 1f)] private float _targetSeedShare = 0.3f;

        [Header("움직임")]
        [Tooltip("색이 한 칸 거리만큼 퍼지는 간격(초)")]
        [SerializeField, Min(0f)] private float _fillStepSeconds = 0.07f;
        [Tooltip("색이 바뀐 칸 하나가 번쩍이며 커졌다 돌아오는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _popSeconds = 0.3f;
        [Tooltip("색이 바뀔 때 칸이 시작하는 크기 배율")]
        [SerializeField, Range(0f, 1f)] private float _popStartScale = 0.4f;
        [Tooltip("원래 크기보다 더 커졌다 돌아오는 정도")]
        [SerializeField, Range(0f, 1f)] private float _popOvershoot = 0.18f;
        [Tooltip("색이 바뀌는 순간 번쩍이는 색")]
        [SerializeField] private Color _flashColor = Color.white;
        [Tooltip("정답색으로 가득 찬 판을 보여 준 뒤 닫힐 때까지의 시간(초)")]
        [SerializeField, Min(0f)] private float _solveDelay = 0.8f;
        [Tooltip("수를 다 쓴 판을 보여 준 뒤 처음으로 되돌릴 때까지의 시간(초)")]
        [SerializeField, Min(0f)] private float _failDelay = 0.8f;

        [Header("모양")]
        [Tooltip("칸 재질. 정점 색을 쓰는 재질이어야 한다")]
        [SerializeField] private Material _fillMaterial;
        [SerializeField] private Material _lineMaterial;
        [Tooltip("판 가로 길이(월드)")]
        [SerializeField, Min(0.1f)] private float _boardWidth = 4f;
        [Tooltip("칸 사이 틈 비율. 틈으로 뒤 원판 색이 보여 칸 경계가 된다")]
        [SerializeField, Range(0f, 0.5f)] private float _cellGap = 0.06f;
        [Tooltip("판 가장자리에서 색 견본 줄, 남은 수 점 줄까지의 거리(월드)")]
        [SerializeField, Min(0f)] private float _uiGap = 0.55f;
        [Tooltip("뒤에 까는 어두운 원판 지름. 홍채 원판 지름과 같게 두면 뒤의 미로가 모두 가려진다")]
        [SerializeField, Min(0.1f)] private float _backdropDiameter = 6.4f;
        [SerializeField] private Color _whiteColor = new Color(0.92f, 0.92f, 0.9f, 1f);
        [SerializeField] private Color _greenColor = new Color(0.35f, 0.75f, 0.45f, 1f);
        [SerializeField] private Color _redColor = new Color(0.9f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color _blueColor = new Color(0.3f, 0.5f, 0.95f, 1f);
        [SerializeField] private Color _backdropColor = Color.black;
        [SerializeField] private Color _cursorColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _moveDotColor = Color.white;
        [SerializeField, Min(0.01f)] private float _swatchSize = 0.4f;
        [SerializeField, Min(0.001f)] private float _frameWidth = 0.04f;
        [SerializeField, Min(0.01f)] private float _moveDotSize = 0.1f;
        [Tooltip("판 둘레에 두르는 정답색 테두리 두께")]
        [SerializeField, Min(0.001f)] private float _targetFrameWidth = 0.1f;
        [Tooltip("판 가장자리와 정답색 테두리 사이 간격")]
        [SerializeField, Min(0f)] private float _targetFrameMargin = 0.15f;
        [Tooltip("그리기 순서. 눈꺼풀(22)보다 낮게 둔다")]
        [SerializeField] private int _sortingOrder = 20;

        private int[] _colors = Array.Empty<int>();
        private int[] _startColors = Array.Empty<int>();
        private int _target;
        private int _moveLimit;
        private int _movesLeft;
        private int _cursor;
        private int _paint;
        private readonly List<(int cell, int dist)> _fillQueue = new List<(int cell, int dist)>();
        private float _fillT;
        private bool _filling;
        private float[] _popT = Array.Empty<float>();
        private int _popping;
        private Vector3[] _baseVertices = Array.Empty<Vector3>();
        private Vector3[] _cellCenters = Array.Empty<Vector3>();
        private Vector3[] _vertices = Array.Empty<Vector3>();
        private float _resultT = -1f;
        private bool _resultSolved;
        private bool _isOpen;
        private Transform _drawRoot;
        private Mesh _mesh;
        private Color[] _vertexColors = Array.Empty<Color>();
        private LineRenderer _cursorFrame;
        private LineRenderer _swatchFrame;
        private LineRenderer[] _moveDots = Array.Empty<LineRenderer>();

        public event Action Solved;
        public event Action Cancelled;
        // 스펙 047: 판이 정답색이 아닌 한 색으로 모였을 때 알린다.
        public event Action WrongColor;

        public bool IsOpen => _isOpen;
        private int CellCount => _rows * _columns;
        private float Side => _boardWidth / ((_columns + 1) * 0.5f);
        private float TriHeight => Side * Mathf.Sqrt(3f) * 0.5f;
        private float BoardHeight => _rows * TriHeight;

        void Update()
        {
            if (!_isOpen) return;
            float dt = Time.deltaTime;
            AdvancePops(dt);
            // 성공·실패 뒤에는 입력을 받지 않고 결과를 잠깐 보여 준다.
            if (_resultT >= 0f)
            {
                _resultT += dt;
                if (_resultSolved)
                {
                    if (_resultT < _solveDelay) return;
                    Close();
                    Solved?.Invoke();
                    return;
                }
                if (_resultT < _failDelay) return;
                ResetBoard();
                return;
            }
            if (_filling)
            {
                AdvanceFill(dt);
                return;
            }
            if (DialogueLog.BlocksInput) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                Cancelled?.Invoke();
                return;
            }
            HandleInput(kb);
        }

        void OnDisable()
        {
            if (_isOpen) Close();
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void OnValidate()
        {
            _rows = Mathf.Max(MIN_ROWS, _rows);
            _columns = Mathf.Max(MIN_COLUMNS, _columns);
        }

        /// <summary>
        /// layerIndex에 맞는 영역 수로 제한 수 안에 풀 수 있는 판을 만들어 그린다.
        /// layerIndex, seed, emotion을 사용하며, 칸 색, 정답색, 제한 수, 선택 상태와 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Open(int layerIndex, int seed, MemoryEmotion emotion)
        {
            int countIndex = Mathf.Clamp(layerIndex, 0, _regionCounts.Length - 1);
            int regions = _regionCounts.Length > 0 ? _regionCounts[countIndex] : 10;
            regions = Mathf.Clamp(regions, MIN_REGIONS, CellCount);

            _target = TargetOf(emotion);
            _isOpen = true;
            Generate(new System.Random(seed), regions);
            BuildDrawing();
            ResetBoard();
        }

        /// <summary>
        /// 퍼즐을 닫고 그리기 오브젝트를 지운다.
        /// 입력값은 없으며, 열림 상태, 진행 중인 채우기와 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            _fillQueue.Clear();
            _filling = false;
            _popping = 0;
            _resultT = -1f;
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _drawRoot = null;
            _cursorFrame = null;
            _swatchFrame = null;
            _moveDots = Array.Empty<LineRenderer>();
        }

        /// <summary>
        /// 파편 감정을 정답색 번호로 바꾼다.
        /// emotion을 사용하며, 색 번호(초록·빨강·파랑)를 반환한다.
        /// </summary>
        private static int TargetOf(MemoryEmotion emotion)
        {
            switch (emotion)
            {
                case MemoryEmotion.Calm: return GREEN;
                case MemoryEmotion.Intense: return RED;
                default: return BLUE;
            }
        }

        /// <summary>
        /// 처음 색과 제한 수로 판을 되돌린다.
        /// _startColors와 _moveLimit을 사용하며, 칸 색, 남은 수, 선택 틀, 고른 색, 결과 상태를 바꾼다.
        /// </summary>
        private void ResetBoard()
        {
            _colors = (int[])_startColors.Clone();
            _movesLeft = _moveLimit;
            _cursor = (_rows / 2) * _columns + _columns / 2;
            _paint = _target;
            _fillQueue.Clear();
            _filling = false;
            for (int i = 0; i < _popT.Length; i++) _popT[i] = -1f;
            _popping = 0;
            _resultT = -1f;
            Refresh();
        }

        /// <summary>
        /// 시작 칸에서 영역을 넓혀 판을 만들고, 최소 수에 여유 수를 더해 제한 수를 정한다.
        /// 최소 수가 범위 밖이거나 정답색이 아닌 색도 그 수 안에 판 전체를 덮을 수 있으면 다시 만든다.
        /// rng와 regions를 사용하며, _startColors와 _moveLimit을 바꾼다.
        /// </summary>
        private void Generate(System.Random rng, int regions)
        {
            int[] fallback = null;
            int fallbackMoves = 0;
            for (int attempt = 0; attempt < GENERATE_TRIES; attempt++)
            {
                int[] board = GrowBoard(rng, regions);
                int moves = MinMoves(board, _target, _maxMoves);
                if (moves < _minMoves) continue;
                if (fallback == null)
                {
                    fallback = board;
                    fallbackMoves = moves;
                }
                if (!OnlyTargetFits(board, moves)) continue;
                _startColors = board;
                _moveLimit = moves + _extraMoves;
                return;
            }
            // 조건에 맞는 판을 끝내 못 찾으면 정답색으로는 풀리는 판을 쓴다.
            Debug.LogWarning("FloodPuzzle: 정답색만 맞는 판을 찾지 못해 다른 색도 맞을 수 있는 판을 씁니다.", this);
            _startColors = fallback ?? GrowBoard(rng, MIN_REGIONS);
            _moveLimit = Mathf.Max(1, fallback != null ? fallbackMoves : MinMoves(_startColors, _target, CellCount)) + _extraMoves;
        }

        /// <summary>
        /// 정답색이 아닌 모든 색이 moves 수 안에는 판 전체를 덮을 수 없는지 확인한다.
        /// board, moves, _target을 사용하며, 정답색만 moves 수 안에 맞으면 true를 반환한다.
        /// </summary>
        private bool OnlyTargetFits(int[] board, int moves)
        {
            for (int c = 0; c < COLOR_COUNT; c++)
            {
                if (c == _target) continue;
                if (MinMoves(board, c, moves) >= 0) return false;
            }
            return true;
        }

        /// <summary>
        /// 무작위 시작 칸에 무작위 색을 주고, 이미 색이 정해진 이웃의 색으로 남은 칸을 무작위 순서로 채운다.
        /// rng와 regions를 사용하며, 칸별 색 번호 배열을 반환한다.
        /// </summary>
        private int[] GrowBoard(System.Random rng, int regions)
        {
            var colors = new int[CellCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = -1;
            var frontier = new List<int>();
            var inFrontier = new bool[CellCount];
            var neighbors = new List<int>(3);

            for (int placed = 0; placed < regions; placed++)
            {
                int cell;
                do cell = rng.Next(CellCount); while (colors[cell] >= 0);
                // 정답색을 일정 비율 이상 넣어 정답색만 제한 수에 맞는 판이 잘 나오게 한다.
                colors[cell] = rng.NextDouble() < _targetSeedShare ? _target : rng.Next(COLOR_COUNT);
                AddFrontier(cell, colors, frontier, inFrontier, neighbors);
            }

            while (frontier.Count > 0)
            {
                int pick = rng.Next(frontier.Count);
                int cell = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (colors[cell] >= 0) continue;

                GetNeighbors(cell, neighbors);
                var owners = new List<int>(3);
                foreach (int n in neighbors) if (colors[n] >= 0) owners.Add(colors[n]);
                colors[cell] = owners[rng.Next(owners.Count)];
                AddFrontier(cell, colors, frontier, inFrontier, neighbors);
            }
            return colors;
        }

        /// <summary>
        /// cell의 색이 정해지지 않은 이웃을 넓혀 갈 칸 목록에 넣는다.
        /// cell, colors, frontier, inFrontier, neighbors를 사용하며, frontier와 inFrontier를 바꾼다.
        /// </summary>
        private void AddFrontier(int cell, int[] colors, List<int> frontier, bool[] inFrontier, List<int> neighbors)
        {
            GetNeighbors(cell, neighbors);
            foreach (int n in neighbors)
            {
                if (colors[n] >= 0 || inFrontier[n]) continue;
                inFrontier[n] = true;
                frontier.Add(n);
            }
        }

        /// <summary>
        /// 판 전체를 goal 색으로 만드는 최소 수를 반복 깊이 늘리기 탐색으로 구한다.
        /// start, goal, maxDepth를 사용하며, 최소 수를 반환한다. 처음부터 다 goal 색이면 0, maxDepth 안에 못 풀면 -1이다.
        /// </summary>
        private int MinMoves(int[] start, int goal, int maxDepth)
        {
            int[] region = LabelRegions(start, out int count);
            if (count > MAX_GRAPH_REGIONS) return -1;

            // 영역을 점, 맞닿은 영역을 선으로 하는 그래프를 만든다.
            var colors = new int[count];
            var adj = new ulong[count];
            var neighbors = new List<int>(3);
            for (int cell = 0; cell < CellCount; cell++)
            {
                int a = region[cell];
                colors[a] = start[cell];
                GetNeighbors(cell, neighbors);
                foreach (int n in neighbors)
                {
                    int b = region[n];
                    if (a == b) continue;
                    adj[a] |= 1UL << b;
                    adj[b] |= 1UL << a;
                }
            }
            ulong alive = count == 64 ? ulong.MaxValue : (1UL << count) - 1;

            var failed = new Dictionary<string, int>();
            for (int depth = LowerBound(colors, adj, alive, goal); depth <= maxDepth; depth++)
            {
                if (Search(colors, adj, alive, depth, goal, failed)) return depth;
            }
            return -1;
        }

        /// <summary>
        /// 남은 수 depthLeft 안에 그래프 전체를 goal 색 한 영역으로 만들 수 있는지 깊이 우선으로 찾는다.
        /// colors, adj, alive, depthLeft, goal, failed를 사용하며, 풀 수 있으면 true를 반환하고 실패한 상태를 failed에 기록한다.
        /// </summary>
        private bool Search(int[] colors, ulong[] adj, ulong alive, int depthLeft, int goal, Dictionary<string, int> failed)
        {
            if (CountBits(alive) == 1)
            {
                // 한 영역만 남았으면 goal 색이거나 한 수로 goal 색으로 바꿀 수 있어야 한다.
                return colors[LowestBit(alive)] == goal || depthLeft >= 1;
            }
            if (LowerBound(colors, adj, alive, goal) > depthLeft) return false;
            string key = StateKey(colors, adj, alive);
            if (failed.TryGetValue(key, out int triedDepth) && triedDepth >= depthLeft) return false;

            for (ulong rest = alive; rest != 0; rest &= rest - 1)
            {
                int v = LowestBit(rest);
                // 맞닿은 영역에 있는 색으로만 바꾼다. 아무것도 합치지 못하는 수는 최소 수에 필요 없다.
                int neighborColors = 0;
                for (ulong ns = adj[v]; ns != 0; ns &= ns - 1) neighborColors |= 1 << colors[LowestBit(ns)];
                for (int c = 0; c < COLOR_COUNT; c++)
                {
                    if ((neighborColors & (1 << c)) == 0) continue;
                    var nextColors = (int[])colors.Clone();
                    var nextAdj = (ulong[])adj.Clone();
                    ulong nextAlive = Merge(nextColors, nextAdj, alive, v, c);
                    if (Search(nextColors, nextAdj, nextAlive, depthLeft - 1, goal, failed)) return true;
                }
            }
            failed[key] = depthLeft;
            return false;
        }

        /// <summary>
        /// 영역 v를 색 c로 바꾸고 맞닿은 같은 색 영역을 v에 합친다.
        /// colors, adj, alive, v, c를 사용하며, colors와 adj를 바꾸고 합친 뒤 남은 영역 집합을 반환한다.
        /// </summary>
        private static ulong Merge(int[] colors, ulong[] adj, ulong alive, int v, int c)
        {
            colors[v] = c;
            ulong bitV = 1UL << v;
            ulong merged = 0;
            for (ulong ns = adj[v]; ns != 0; ns &= ns - 1)
            {
                int u = LowestBit(ns);
                if (colors[u] == c) merged |= 1UL << u;
            }
            ulong joined = adj[v];
            for (ulong ms = merged; ms != 0; ms &= ms - 1) joined |= adj[LowestBit(ms)];
            joined &= ~(merged | bitV);
            for (ulong ws = joined; ws != 0; ws &= ws - 1)
            {
                int w = LowestBit(ws);
                adj[w] = (adj[w] & ~merged) | bitV;
            }
            adj[v] = joined;
            return alive & ~merged;
        }

        /// <summary>
        /// 남은 수의 아래 한계를 구한다. 한 수에 색 하나만 없앨 수 있고, 그래프 지름은 한 수에 2까지만 줄어든다.
        /// colors, adj, alive, goal을 사용하며, 필요한 최소 수의 아래 한계를 반환한다.
        /// </summary>
        private static int LowerBound(int[] colors, ulong[] adj, ulong alive, int goal)
        {
            int colorMask = 0;
            for (ulong rest = alive; rest != 0; rest &= rest - 1) colorMask |= 1 << colors[LowestBit(rest)];
            int colorCount = CountBits((ulong)colorMask);
            int byColor = colorCount - 1 + ((colorMask & (1 << goal)) == 0 ? 1 : 0);

            int diameter = 0;
            for (ulong rest = alive; rest != 0; rest &= rest - 1)
            {
                // 한 영역에서 너비 우선으로 퍼져 가장 먼 영역까지의 거리를 잰다.
                ulong seen = 1UL << LowestBit(rest), frontier = seen;
                int dist = 0;
                while (true)
                {
                    ulong next = 0;
                    for (ulong fs = frontier; fs != 0; fs &= fs - 1) next |= adj[LowestBit(fs)];
                    next &= alive & ~seen;
                    if (next == 0) break;
                    seen |= next;
                    frontier = next;
                    dist++;
                }
                if (dist > diameter) diameter = dist;
            }
            return Mathf.Max(byColor, (diameter + 1) / 2);
        }

        /// <summary>
        /// 실패 기록에 쓸 그래프 상태 키를 만든다.
        /// colors, adj, alive를 사용하며, 남은 영역의 색과 이웃을 담은 문자열을 반환한다.
        /// </summary>
        private static string StateKey(int[] colors, ulong[] adj, ulong alive)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(alive).Append(':');
            for (ulong rest = alive; rest != 0; rest &= rest - 1)
            {
                int v = LowestBit(rest);
                sb.Append(colors[v]).Append(',').Append(adj[v]).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 켜진 비트 수를 센다.
        /// bits를 사용하며, 켜진 비트 수를 반환한다.
        /// </summary>
        private static int CountBits(ulong bits)
        {
            int count = 0;
            for (; bits != 0; bits &= bits - 1) count++;
            return count;
        }

        /// <summary>
        /// 가장 낮은 켜진 비트의 번호를 구한다.
        /// bits를 사용하며, 비트 번호를 반환한다. bits가 0이면 64다.
        /// </summary>
        private static int LowestBit(ulong bits)
        {
            if (bits == 0) return 64;
            int index = 0;
            while ((bits & 1UL) == 0)
            {
                bits >>= 1;
                index++;
            }
            return index;
        }

        /// <summary>
        /// 이웃끼리 같은 색인 칸을 묶어 영역 번호를 매긴다.
        /// colors를 사용하며, 칸별 영역 번호 배열과 영역 수를 반환한다.
        /// </summary>
        private int[] LabelRegions(int[] colors, out int regionCount)
        {
            var region = new int[CellCount];
            for (int i = 0; i < region.Length; i++) region[i] = -1;
            var stack = new Stack<int>();
            var neighbors = new List<int>(3);
            regionCount = 0;
            for (int cell = 0; cell < CellCount; cell++)
            {
                if (region[cell] >= 0) continue;
                region[cell] = regionCount;
                stack.Push(cell);
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    GetNeighbors(cur, neighbors);
                    foreach (int n in neighbors)
                    {
                        if (region[n] >= 0 || colors[n] != colors[cell]) continue;
                        region[n] = regionCount;
                        stack.Push(n);
                    }
                }
                regionCount++;
            }
            return region;
        }

        /// <summary>
        /// cell이 속한 영역 전체를 color로 바꾼다.
        /// colors, cell, color를 사용하며, colors를 바꾼다.
        /// </summary>
        private void FillRegion(int[] colors, int cell, int color)
        {
            foreach ((int c, int _) in RegionByDistance(colors, cell)) colors[c] = color;
        }

        /// <summary>
        /// cell이 속한 영역의 칸을 cell에서 영역 안을 따라 잰 칸 거리 순서로 모은다.
        /// colors와 cell을 사용하며, (칸, 거리) 목록을 반환한다.
        /// </summary>
        private List<(int cell, int dist)> RegionByDistance(int[] colors, int cell)
        {
            var result = new List<(int cell, int dist)>();
            var visited = new bool[CellCount];
            var queue = new Queue<(int cell, int dist)>();
            var neighbors = new List<int>(3);
            int color = colors[cell];
            visited[cell] = true;
            queue.Enqueue((cell, 0));
            while (queue.Count > 0)
            {
                (int cur, int dist) = queue.Dequeue();
                result.Add((cur, dist));
                GetNeighbors(cur, neighbors);
                foreach (int n in neighbors)
                {
                    if (visited[n] || colors[n] != color) continue;
                    visited[n] = true;
                    queue.Enqueue((n, dist + 1));
                }
            }
            return result;
        }

        /// <summary>
        /// 삼각형 칸의 이웃을 구한다. 좌우 칸과, 위를 향한 칸은 아래 줄, 아래를 향한 칸은 위 줄 칸이 이웃이다.
        /// cell을 사용하며, neighbors를 이웃 칸 번호로 채운다.
        /// </summary>
        private void GetNeighbors(int cell, List<int> neighbors)
        {
            neighbors.Clear();
            int r = cell / _columns, c = cell % _columns;
            if (c > 0) neighbors.Add(cell - 1);
            if (c < _columns - 1) neighbors.Add(cell + 1);
            if (IsUp(r, c)) { if (r > 0) neighbors.Add(cell - _columns); }
            else if (r < _rows - 1) neighbors.Add(cell + _columns);
        }

        /// <summary>
        /// 칸이 위를 향한 삼각형인지 확인한다.
        /// r과 c를 사용하며, (줄 + 칸 번호)가 짝수면 true를 반환한다.
        /// </summary>
        private static bool IsUp(int r, int c) => ((r + c) & 1) == 0;

        /// <summary>
        /// WASD·방향키로 선택 틀을 옮기고, 1~4·Q/E로 색을 고르고, Space로 붓고, R로 처음으로 되돌린다.
        /// kb를 사용하며, _cursor, _paint, 칸 색, 남은 수를 바꾼다.
        /// </summary>
        private void HandleInput(Keyboard kb)
        {
            int r = _cursor / _columns, c = _cursor % _columns;
            int nr = r, nc = c;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) nr = r + 1;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) nr = r - 1;
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) nc = c + 1;
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) nc = c - 1;
            nr = Mathf.Clamp(nr, 0, _rows - 1);
            nc = Mathf.Clamp(nc, 0, _columns - 1);
            if (nr != r || nc != c)
            {
                _cursor = nr * _columns + nc;
                Refresh();
            }

            int paint = _paint;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) paint = WHITE;
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) paint = GREEN;
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) paint = RED;
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) paint = BLUE;
            else if (kb.qKey.wasPressedThisFrame) paint = (_paint + COLOR_COUNT - 1) % COLOR_COUNT;
            else if (kb.eKey.wasPressedThisFrame) paint = (_paint + 1) % COLOR_COUNT;
            if (paint != _paint)
            {
                _paint = paint;
                Refresh();
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                ResetBoard();
                return;
            }
            if (kb.spaceKey.wasPressedThisFrame && _colors[_cursor] != _paint) StartFill();
        }

        /// <summary>
        /// 선택한 칸의 영역에 고른 색을 붓기 시작하고 남은 수를 하나 줄인다.
        /// _cursor, _paint, _colors를 사용하며, _fillQueue, _fillT, _movesLeft를 바꾼다.
        /// </summary>
        private void StartFill()
        {
            _fillQueue.Clear();
            _fillQueue.AddRange(RegionByDistance(_colors, _cursor));
            _fillT = 0f;
            _filling = true;
            _movesLeft--;
            AdvanceFill(0f);
        }

        /// <summary>
        /// 시간에 따라 부은 칸에서 가까운 칸부터 색을 바꾸고 번쩍임을 시작한다. 다 퍼지고 번쩍임이 끝나면 성공·실패를 판정한다.
        /// dt, _fillStepSeconds, _paint를 사용하며, 칸 색, _fillQueue, 칸 연출, 결과 상태를 바꾼다.
        /// </summary>
        private void AdvanceFill(float dt)
        {
            _fillT += dt;
            int reach = _fillStepSeconds > 0f ? Mathf.FloorToInt(_fillT / _fillStepSeconds) : int.MaxValue;
            // 목록은 거리 순서라 앞에서부터 닿은 칸만 바꾼다.
            int done = 0;
            while (done < _fillQueue.Count && _fillQueue[done].dist <= reach)
            {
                int cell = _fillQueue[done].cell;
                _colors[cell] = _paint;
                if (_popT[cell] < 0f) _popping++;
                _popT[cell] = 0f;
                done++;
            }
            _fillQueue.RemoveRange(0, done);
            if (_fillQueue.Count > 0 || _popping > 0)
            {
                Refresh();
                return;
            }
            _filling = false;

            if (Array.TrueForAll(_colors, color => color == _target))
            {
                _resultSolved = true;
                _resultT = 0f;
            }
            else if (Array.TrueForAll(_colors, color => color == _colors[0]))
            {
                WrongColor?.Invoke();
                if (_movesLeft <= 0)
                {
                    _resultSolved = false;
                    _resultT = 0f;
                }
            }
            else if (_movesLeft <= 0)
            {
                _resultSolved = false;
                _resultT = 0f;
            }
            Refresh();
        }

        /// <summary>
        /// 색 번호를 Inspector 색으로 바꾼다.
        /// index를 사용하며, 색을 반환한다.
        /// </summary>
        private Color ColorOf(int index)
        {
            switch (index)
            {
                case GREEN: return _greenColor;
                case RED: return _redColor;
                case BLUE: return _blueColor;
                default: return _whiteColor;
            }
        }

        /// <summary>
        /// 칸의 세 꼭짓점을 판 로컬 좌표로 구한다. 0줄이 맨 아래다.
        /// cell, Side, TriHeight를 사용하며, 세 꼭짓점과 무게중심을 반환한다.
        /// </summary>
        private (Vector3 a, Vector3 b, Vector3 apex, Vector3 center) CellCorners(int cell)
        {
            int r = cell / _columns, c = cell % _columns;
            float s = Side, h = TriHeight;
            float x = c * s * 0.5f - (_columns - 1) * s * 0.25f;
            float y0 = r * h - BoardHeight * 0.5f;
            bool up = IsUp(r, c);
            float baseY = up ? y0 : y0 + h;
            float apexY = up ? y0 + h : y0;
            var a = new Vector3(x - s * 0.5f, baseY, 0f);
            var b = new Vector3(x + s * 0.5f, baseY, 0f);
            var apex = new Vector3(x, apexY, 0f);
            return (a, b, apex, (a + b + apex) / 3f);
        }

        /// <summary>
        /// 원판, 칸 메시, 선택 틀, 색 견본과 테두리, 정답 표시, 남은 수 점을 새로 만든다.
        /// 판 크기와 모양 설정을 사용하며, _drawRoot 아래 그리기 오브젝트와 _mesh를 바꾼다.
        /// </summary>
        private void BuildDrawing()
        {
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _drawRoot = new GameObject("색 채우기 판").transform;
            _drawRoot.SetParent(transform, false);

            LineRenderer backdrop = CreateLine("원판", _drawRoot, 0);
            Shapes.SetBlob(backdrop, _backdropDiameter);
            Shapes.SetColor(backdrop, _backdropColor);

            BuildBoardMesh();

            // 판 둘레에 정답색 테두리를 둘러 어떤 색으로 채워야 하는지 보여 준다.
            LineRenderer targetFrame = CreateLine("정답색 테두리", _drawRoot, 2);
            float fw = _boardWidth * 0.5f + _targetFrameMargin;
            float fh = BoardHeight * 0.5f + _targetFrameMargin;
            Shapes.SetPoints(targetFrame, new[]
            {
                new Vector3(-fw, -fh, 0f), new Vector3(fw, -fh, 0f),
                new Vector3(fw, fh, 0f), new Vector3(-fw, fh, 0f),
            }, true);
            targetFrame.widthMultiplier = _targetFrameWidth;
            Shapes.SetColor(targetFrame, ColorOf(_target));

            _cursorFrame = CreateLine("선택", _drawRoot, 3);
            _cursorFrame.widthMultiplier = _frameWidth;
            Shapes.SetColor(_cursorFrame, _cursorColor);

            // 판 아래에 4색 견본을 한 줄로 놓는다.
            float swatchY = -BoardHeight * 0.5f - _uiGap;
            float spacing = _swatchSize * 1.5f;
            for (int i = 0; i < COLOR_COUNT; i++)
            {
                LineRenderer swatch = CreateLine("견본", _drawRoot, 2);
                Shapes.SetFilledRect(swatch, _swatchSize, _swatchSize);
                Shapes.SetColor(swatch, ColorOf(i));
                swatch.transform.localPosition = SwatchPos(i, swatchY, spacing);
            }

            float frameHalf = _swatchSize * 0.5f + _frameWidth * 1.5f;
            _swatchFrame = CreateLine("고른 색", _drawRoot, 3);
            Shapes.SetPoints(_swatchFrame, new[]
            {
                new Vector3(-frameHalf, -frameHalf, 0f), new Vector3(frameHalf, -frameHalf, 0f),
                new Vector3(frameHalf, frameHalf, 0f), new Vector3(-frameHalf, frameHalf, 0f),
            }, true);
            _swatchFrame.widthMultiplier = _frameWidth;
            Shapes.SetColor(_swatchFrame, _cursorColor);

            LineRenderer targetMark = CreateLine("정답색", _drawRoot, 2);
            MemoryFragment.DrawDiamond(targetMark, _swatchSize * 0.45f);
            Shapes.SetColor(targetMark, ColorOf(_target));
            targetMark.transform.localPosition = SwatchPos(_target, swatchY, spacing) + new Vector3(0f, -_swatchSize * 0.85f, 0f);

            // 판 위에 제한 수만큼 점을 놓고 남은 수만큼만 보이게 한다.
            float dotY = BoardHeight * 0.5f + _uiGap * 0.6f;
            float dotSpacing = _moveDotSize * 2f;
            _moveDots = new LineRenderer[_moveLimit];
            for (int i = 0; i < _moveLimit; i++)
            {
                LineRenderer dot = CreateLine("남은 수", _drawRoot, 2);
                Shapes.SetBlob(dot, _moveDotSize);
                Shapes.SetColor(dot, _moveDotColor);
                dot.transform.localPosition = new Vector3((i - (_moveLimit - 1) * 0.5f) * dotSpacing, dotY, 0f);
                _moveDots[i] = dot;
            }
        }

        /// <summary>
        /// 색 견본 index의 판 로컬 위치를 구한다.
        /// index, y, spacing을 사용하며, 로컬 위치를 반환한다.
        /// </summary>
        private static Vector3 SwatchPos(int index, float y, float spacing)
        {
            return new Vector3((index - (COLOR_COUNT - 1) * 0.5f) * spacing, y, 0f);
        }

        /// <summary>
        /// 칸마다 틈만큼 줄인 삼각형 세 꼭짓점으로 판 메시를 만든다. 칸 색은 정점 색으로 칠한다.
        /// 판 크기, _cellGap, _fillMaterial을 사용하며, _mesh와 _vertexColors를 바꾼다.
        /// </summary>
        private void BuildBoardMesh()
        {
            var go = new GameObject("칸");
            go.transform.SetParent(_drawRoot, false);
            var filter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _fillMaterial;
            meshRenderer.sortingOrder = _sortingOrder + 1;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            var vertices = new Vector3[CellCount * 3];
            var triangles = new int[CellCount * 3];
            _cellCenters = new Vector3[CellCount];
            float keep = 1f - _cellGap;
            for (int cell = 0; cell < CellCount; cell++)
            {
                (Vector3 a, Vector3 b, Vector3 apex, Vector3 center) = CellCorners(cell);
                int v = cell * 3;
                _cellCenters[cell] = center;
                vertices[v] = center + (a - center) * keep;
                vertices[v + 1] = center + (apex - center) * keep;
                vertices[v + 2] = center + (b - center) * keep;
                // 칸 재질은 양면을 그리는 재질을 쓰므로 꼭짓점 순서는 맞추지 않는다.
                triangles[v] = v;
                triangles[v + 1] = v + 1;
                triangles[v + 2] = v + 2;
            }

            if (_mesh == null) _mesh = new Mesh { name = "색 채우기 판" };
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _baseVertices = vertices;
            _vertices = (Vector3[])vertices.Clone();
            _popT = new float[CellCount];
            for (int i = 0; i < _popT.Length; i++) _popT[i] = -1f;
            _popping = 0;
            _vertexColors = new Color[vertices.Length];
            _mesh.colors = _vertexColors;
            _mesh.RecalculateBounds();
            filter.sharedMesh = _mesh;
        }

        /// <summary>
        /// parent 아래에 선 그리기 오브젝트를 만든다.
        /// objectName, parent, orderOffset을 사용하며, 설정된 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer CreateLine(string objectName, Transform parent, int orderOffset)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            LineRenderer line = Shapes.Ensure(go, _lineMaterial, 4);
            line.sortingOrder = _sortingOrder + orderOffset;
            return line;
        }

        /// <summary>
        /// 칸 색, 선택 틀, 고른 색 테두리, 남은 수 점을 현재 상태로 다시 그린다.
        /// _colors, _cursor, _paint, _movesLeft, 결과 상태를 사용하며, 그리기 오브젝트의 색, 위치, 표시 여부를 바꾼다.
        /// </summary>
        private void Refresh()
        {
            if (_drawRoot == null) return;
            DrawCells();

            (Vector3 a, Vector3 b, Vector3 apex, Vector3 center) = CellCorners(_cursor);
            const float inset = 0.8f;
            Shapes.SetPoints(_cursorFrame, new[]
            {
                center + (a - center) * inset, center + (apex - center) * inset, center + (b - center) * inset,
            }, true);
            _cursorFrame.enabled = _resultT < 0f;

            float swatchY = -BoardHeight * 0.5f - _uiGap;
            _swatchFrame.transform.localPosition = SwatchPos(_paint, swatchY, _swatchSize * 1.5f);

            for (int i = 0; i < _moveDots.Length; i++) _moveDots[i].enabled = i < _movesLeft;
        }

        /// <summary>
        /// 색이 바뀐 칸의 번쩍임 시간을 진행하고, 진행 중인 칸이 있으면 칸을 다시 그린다.
        /// dt와 _popSeconds를 사용하며, _popT, _popping과 판 메시를 바꾼다.
        /// </summary>
        private void AdvancePops(float dt)
        {
            if (_popping == 0) return;
            for (int cell = 0; cell < _popT.Length; cell++)
            {
                if (_popT[cell] < 0f) continue;
                _popT[cell] += dt / _popSeconds;
                if (_popT[cell] < 1f) continue;
                _popT[cell] = -1f;
                _popping--;
            }
            DrawCells();
        }

        /// <summary>
        /// 모든 칸의 정점 위치와 색을 그린다. 번쩍이는 칸은 작게 시작해 원래보다 조금 커졌다 돌아오고, 번쩍임 색에서 칸 색으로 바뀐다.
        /// _colors, _popT, _baseVertices, _cellCenters를 사용하며, 판 메시의 정점과 색을 바꾼다.
        /// </summary>
        private void DrawCells()
        {
            // 선형 색 공간에서는 정점 색이 변환 없이 쓰여 밝게 보이므로, 선과 같은 색으로 보이게 미리 바꾼다.
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            for (int cell = 0; cell < CellCount; cell++)
            {
                Color color = ColorOf(_colors[cell]);
                float scale = 1f;
                float t = _popT[cell];
                if (t >= 0f)
                {
                    // 앞 60%는 작게 시작해 원래보다 커지고, 뒤 40%는 원래 크기로 돌아온다.
                    scale = t < 0.6f
                        ? Mathf.Lerp(_popStartScale, 1f + _popOvershoot, Mathf.SmoothStep(0f, 1f, t / 0.6f))
                        : Mathf.Lerp(1f + _popOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f));
                    color = Color.Lerp(_flashColor, color, Mathf.SmoothStep(0f, 1f, t));
                }
                if (linear) color = color.linear;

                Vector3 center = _cellCenters[cell];
                for (int k = 0; k < 3; k++)
                {
                    int v = cell * 3 + k;
                    _vertices[v] = center + (_baseVertices[v] - center) * scale;
                    _vertexColors[v] = color;
                }
            }
            _mesh.vertices = _vertices;
            _mesh.colors = _vertexColors;
        }
    }
}
