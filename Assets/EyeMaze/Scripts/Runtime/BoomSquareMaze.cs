using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로의 사각 미로. 홍채 원 안을 가로세로 칸이 바둑판처럼 채운다. 칸 전체가 원 안에 드는 칸만 시선이 다니는 미로 칸이고,
    /// 원에 걸친 바깥 칸은 쓰지 않고 그리지도 않는다. 미로를 두르는 테두리 선은 없다. 벽은 칸 경계를 따라 곧은 선이다.
    /// 미로는 바깥에서 안으로 원을 따라 층(외각, 내각)으로 나뉘고, 층 경계와 동공 자리 둘레에 문이 하나씩 있다.
    /// 층마다 조각이 하나 있어 먹으면 그 층의 안쪽 문이 열린다. 칸 좌표는 열(0 = 왼쪽)과 행(0 = 아래)이고, 이 오브젝트의 로컬 원점이 원의 한가운데다.
    /// 켜져 있는 동안 원형 미로 층의 벽과 문 그림을 숨긴다. 원형 벽의 판정은 건드리지 않는다.
    /// </summary>
    public class BoomSquareMaze : MonoBehaviour
    {
        private const string WALL_CONTAINER_NAME = "사각 벽";
        private const int WALL_SORTING_ORDER = 5;
        private const int SHARD_SORTING_ORDER = 7;
        private const float FIT_EPSILON = 1e-4f;

        /// <summary>층 사이(또는 가장 안쪽 층과 동공 자리 사이)의 문 하나</summary>
        private class Gate
        {
            public Vector2Int Cell;
            public Vector2Int Dir;
            public bool Open;
            public float OpenStart;
        }

        [Header("모양")]
        [Tooltip("칸 한 변의 길이 (미로 기준)")]
        [SerializeField, Min(0.05f)] private float _cellSize = 0.3f;
        [Tooltip("미로 바깥 테두리 원의 반지름. 칸 한가운데가 이 원 안에 있는 칸을 모두 쓴다. 홍채 반지름에 맞춘다")]
        [SerializeField, Min(0.1f)] private float _outerRadius = 3f;
        [Tooltip("동공 자리 반지름. 칸의 일부라도 이 원 안에 들면 동공 자리다. 동공 반지름에 맞춘다")]
        [SerializeField, Min(0f)] private float _innerRadius = 0.9f;
        [Tooltip("시선이 시작하는 칸 (열, 행). 휩쓸렸을 때 안전지대를 아직 밟지 않았으면 여기로 돌아간다. 외각 칸이 아니면 같은 열의 가장 아래 외각 칸을 쓴다")]
        [SerializeField] private Vector2Int _startCell = new Vector2Int(10, 0);

        [Header("층")]
        [Tooltip("층 경계 원의 반지름. 바깥부터 적는다. 칸 한가운데가 이 원보다 안쪽이면 안쪽 층이다. 하나면 외각, 내각 두 층이 된다")]
        [SerializeField] private List<float> _layerRadii = new List<float> { 1.9f };

        [Header("생성")]
        [Tooltip("미로를 만드는 시드. 같은 시드면 언제나 같은 미로다")]
        [SerializeField] private int _seed = 7;
        [Tooltip("막다른 길을 줄이기 위해 층 안에서 추가로 뚫는 벽 비율")]
        [SerializeField, Range(0f, 0.4f)] private float _loopChance = 0.08f;

        [Header("문")]
        [Tooltip("닫힌 문의 색. 보통 벽과 구분되게 칠한다")]
        [SerializeField] private Color _gateColor = new Color(1f, 0.78f, 0.3f, 1f);
        [Tooltip("층 경계 벽과 문의 굵기 배율 (보통 벽 = 1). 층이 구분되어 보이게 굵게 그린다")]
        [SerializeField, Min(1f)] private float _layerWallWidthScale = 2f;
        [Tooltip("문이 열릴 때 번쩍이며 사라지는 시간(초)")]
        [SerializeField, Min(0.05f)] private float _gateOpenSeconds = 0.6f;
        [Tooltip("공용 문구 파일에 msg.boom_gate_open이 없을 때 쓰는 글")]
        [SerializeField, TextArea(1, 2)] private string _gateOpenMessage = "문이 열렸다.";
        [Tooltip("공용 문구 파일에 msg.boom_gate_locked가 없을 때 쓰는 글")]
        [SerializeField, TextArea(1, 2)] private string _gateLockedMessage = "조각을 찾아야 열린다.";

        [Header("조각")]
        [Tooltip("조각(마름모)의 색")]
        [SerializeField] private Color _shardColor = new Color(0.55f, 0.95f, 1f, 1f);
        [Tooltip("조각 마름모 한 변의 길이 (칸 크기 대비)")]
        [SerializeField, Range(0.2f, 1f)] private float _shardScale = 0.55f;
        [Tooltip("조각이 반짝이는 빠르기 (초당 맥박 수)")]
        [SerializeField, Min(0f)] private float _shardPulseRate = 1.5f;

        [Header("씬 뷰 표시")]
        [SerializeField] private Color _gizmoColor = new Color(1f, 1f, 1f, 0.25f);

        // 세로 경계(열 x의 왼쪽, x = 0 ~ 열 수)와 가로 경계(행 y의 아래쪽, y = 0 ~ 행 수)가 열려 있는지. 문은 따로 관리한다.
        private bool[,] _openV;
        private bool[,] _openH;
        // 경계마다 문 번호(층 번호). 문이 아니면 -1이다.
        private int[,] _gateV;
        private int[,] _gateH;
        private readonly List<Gate> _gates = new List<Gate>();
        // 층마다 조각 칸과 먹었는지, 조각 그림
        private Vector2Int[] _shards = new Vector2Int[0];
        private bool[] _shardTaken = new bool[0];
        private LineRenderer[] _shardLines = new LineRenderer[0];
        // 그려진 벽과 문. 경계 하나에 선 하나다. 열린 경계와 원 테두리 쪽 경계는 없다.
        private LineRenderer[,] _wallV;
        private LineRenderer[,] _wallH;
        // 층 경계(층이 다른 두 칸이나 가장 안쪽 층과 동공 자리 사이) 경계인지. 굵게 그린다.
        private bool[,] _layerEdgeV;
        private bool[,] _layerEdgeH;
        // 벽과 테두리가 빛나기 시작한 시각(빛나지 않으면 -1), 빛나는 시간과 색
        private float[,] _glowStartV;
        private float[,] _glowStartH;
        // 미로 칸과 바깥(원에 걸친 칸이나 원 밖) 사이 경계인지. 평소에는 꺼 두고 부딪혀 빛나는 동안만 보인다.
        private bool[,] _rimEdgeV;
        private bool[,] _rimEdgeH;
        private float _glowSeconds = 0.4f;
        private Color _glowColor = Color.white;
        private bool _built;
        private bool _shown;
        private bool _irisWallsHidden;
        private IrisMaze _maze;

        /// <summary>바둑판의 가로 칸 수. 원의 한가운데가 가운데 칸의 중심이 되도록 홀수다.</summary>
        public int Columns => 2 * Mathf.FloorToInt(_outerRadius / _cellSize + FIT_EPSILON) + 1;
        /// <summary>바둑판의 세로 칸 수. 가로 칸 수와 같다.</summary>
        public int Rows => Columns;
        public float CellSize => _cellSize;
        /// <summary>층 수. 층 경계 원 수보다 하나 많다. 0이 가장 바깥(외각)이다.</summary>
        public int LayerCount => _layerRadii.Count + 1;
        /// <summary>시선이 시작하는 칸. 지정한 칸이 외각 칸이 아니면 같은 열의 가장 아래 외각 칸이다.</summary>
        public Vector2Int StartCell => ResolveStart();
        /// <summary>
        /// 화면 한 픽셀이 이 미로 기준으로 얼마인지. 폭발 미로가 카메라로 구해 넘긴다. 0보다 크면 벽과 문 굵기를 이 값의 정수배로 맞춘다.
        /// </summary>
        public float PixelSize { get; set; }
        /// <summary>미로 모양 설정이 바뀌었는지 알아보는 값</summary>
        public int GeometryKey => HashCode.Combine(_cellSize, _outerRadius, _innerRadius);
        /// <summary>켜져 있으면 사각 벽을 그리고 원형 미로의 벽 그림을 숨긴다. 폭발 미로가 들여다보는 동안 켠다.</summary>
        public bool Active { get; set; }
        /// <summary>문이 열릴 때 띄우는 안내</summary>
        public string GateOpenMessage => Strings.Get("msg.boom_gate_open", _gateOpenMessage);
        /// <summary>닫힌 문에 부딪혔을 때 띄우는 안내</summary>
        public string GateLockedMessage => Strings.Get("msg.boom_gate_locked", _gateLockedMessage);

        private IrisMaze Maze => _maze != null ? _maze : (_maze = GetComponentInParent<IrisMaze>());

        void OnValidate()
        {
            _innerRadius = Mathf.Clamp(_innerRadius, 0f, _outerRadius - _cellSize);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            bool show = Active && _built;
            if (show) { DrawWalls(); DrawShards(); }
            else if (_shown) HideAll();
            _shown = show;
            HideIrisWalls(Active);
        }

        void OnDisable()
        {
            if (Maze != null) HideIrisWalls(false);
        }

        // 칸 판정

        /// <summary>
        /// 칸이 바둑판 안에 있는지 판단한다.
        /// cell(열, 행)을 사용하며, 안에 있으면 true를 반환한다.
        /// </summary>
        public bool InBounds(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows;

        /// <summary>
        /// 칸이 가운데 동공 자리인지 판단한다. 칸에서 원의 한가운데에 가장 가까운 점이 안쪽 반지름 안에 있으면 동공 자리다.
        /// cell(열, 행), _innerRadius, 칸 크기를 사용하며, 동공 자리면 true를 반환한다.
        /// </summary>
        public bool IsHole(Vector2Int cell)
        {
            if (!InBounds(cell)) return false;
            Vector2 c = CellCenterLocal(cell);
            float h = _cellSize * 0.5f;
            float nx = Mathf.Max(0f, Mathf.Abs(c.x) - h);
            float ny = Mathf.Max(0f, Mathf.Abs(c.y) - h);
            return nx * nx + ny * ny < _innerRadius * _innerRadius;
        }

        /// <summary>
        /// 칸이 시선이 다닐 수 있는 미로 칸인지 판단한다. 칸 전체(가장 먼 모서리)가 바깥 반지름 안에 들고 동공 자리가 아니면 미로 칸이다.
        /// cell(열, 행), _outerRadius, 칸 크기를 사용하며, 미로 칸이면 true를 반환한다.
        /// </summary>
        public bool IsMazeCell(Vector2Int cell)
        {
            if (!InBounds(cell) || IsHole(cell)) return false;
            Vector2 c = CellCenterLocal(cell);
            float h = _cellSize * 0.5f;
            float fx = Mathf.Abs(c.x) + h;
            float fy = Mathf.Abs(c.y) + h;
            return fx * fx + fy * fy <= (_outerRadius + FIT_EPSILON) * (_outerRadius + FIT_EPSILON);
        }

        /// <summary>
        /// 미로 칸이 몇 번째 층인지 계산한다. 칸 한가운데가 층 경계 원보다 안쪽일수록 안쪽 층이다.
        /// cell(열, 행), _layerRadii를 사용하며, 층 번호(0 = 외각)를 반환한다. 미로 칸이 아니면 -1이다.
        /// </summary>
        public int LayerOf(Vector2Int cell)
        {
            if (!IsMazeCell(cell)) return -1;
            float d = ((Vector2)CellCenterLocal(cell)).magnitude;
            int layer = 0;
            foreach (float r in _layerRadii) if (d < r) layer++;
            return layer;
        }

        // 좌표 변환

        /// <summary>
        /// 칸 한가운데의 이 오브젝트 기준 로컬 좌표를 계산한다. 원의 한가운데가 원점이다.
        /// cell(열, 행), 칸 수와 칸 크기를 사용하며, 로컬 좌표를 반환한다.
        /// </summary>
        public Vector3 CellCenterLocal(Vector2Int cell)
        {
            int half = Columns / 2;
            return new Vector3((cell.x - half) * _cellSize, (cell.y - half) * _cellSize, 0f);
        }

        /// <summary>
        /// 이 오브젝트 기준 로컬 좌표가 어느 칸에 있는지 계산한다. 바둑판 밖이면 가장 가까운 칸으로 맞춘다.
        /// local, 칸 수와 칸 크기를 사용하며, 칸(열, 행)을 반환한다.
        /// </summary>
        public Vector2Int CellAtLocal(Vector3 local)
        {
            int half = Columns / 2;
            int x = Mathf.RoundToInt(local.x / _cellSize) + half;
            int y = Mathf.RoundToInt(local.y / _cellSize) + half;
            return new Vector2Int(Mathf.Clamp(x, 0, Columns - 1), Mathf.Clamp(y, 0, Rows - 1));
        }

        /// <summary>
        /// 칸 한가운데를 시선과 같은 원형 미로 기준 로컬 좌표로 계산한다.
        /// cell(열, 행), 이 오브젝트와 원형 미로의 Transform을 사용하며, 원형 미로 기준 좌표를 반환한다.
        /// </summary>
        public Vector2 CellCenterMaze(Vector2Int cell)
        {
            return Maze.transform.InverseTransformPoint(transform.TransformPoint(CellCenterLocal(cell)));
        }

        /// <summary>
        /// 원형 미로 기준 로컬 좌표(시선 위치)가 어느 칸에 있는지 계산한다.
        /// mazeLocal, 이 오브젝트와 원형 미로의 Transform을 사용하며, 칸(열, 행)을 반환한다.
        /// </summary>
        public Vector2Int CellAtMaze(Vector3 mazeLocal)
        {
            return CellAtLocal(transform.InverseTransformPoint(Maze.transform.TransformPoint(mazeLocal)));
        }

        /// <summary>
        /// 월드(화면) 방향을 이 미로의 가로세로 한 칸 방향으로 바꾼다. 더 크게 향한 축 하나만 쓴다.
        /// worldDir, 이 오브젝트의 회전을 사용하며, 칸 방향(가로 또는 세로 단위 벡터)을 반환한다.
        /// </summary>
        public Vector2Int GridDirection(Vector2 worldDir)
        {
            Vector3 d = transform.InverseTransformDirection(worldDir);
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) return new Vector2Int(d.x >= 0f ? 1 : -1, 0);
            return new Vector2Int(0, d.y >= 0f ? 1 : -1);
        }

        /// <summary>
        /// 칸 방향을 원형 미로 기준 단위 벡터로 바꾼다. 시선의 튕김과 늘어남 방향에 쓴다.
        /// dir(칸 방향), 이 오브젝트와 원형 미로의 Transform을 사용하며, 원형 미로 기준 단위 벡터를 반환한다.
        /// </summary>
        public Vector2 DirectionMaze(Vector2Int dir)
        {
            Vector3 world = transform.TransformDirection(new Vector3(dir.x, dir.y, 0f));
            return ((Vector2)Maze.transform.InverseTransformDirection(world)).normalized;
        }

        /// <summary>
        /// 칸 한 변의 길이를 원형 미로 기준 길이로 바꾼다.
        /// _cellSize와 두 Transform의 크기 배율을 사용하며, 원형 미로 기준 길이를 반환한다.
        /// </summary>
        public float CellSizeMaze()
        {
            return _cellSize * Mathf.Abs(transform.lossyScale.x) / Mathf.Max(1e-4f, Mathf.Abs(Maze.transform.lossyScale.x));
        }

        // 길과 문

        /// <summary>
        /// 칸에서 dir 쪽 경계로 지나갈 수 있는지 판단한다. 문이면 열렸을 때만, 원 테두리 쪽은 늘 막혀 있다.
        /// cell(열, 행), dir(칸 방향), 경계 열림 기록과 문 상태를 사용하며, 지나갈 수 있으면 true를 반환한다.
        /// </summary>
        public bool IsOpen(Vector2Int cell, Vector2Int dir)
        {
            if (!_built) return false;
            int gate = GateIndex(cell, dir);
            return gate >= 0 ? _gates[gate].Open : IsOpenFlag(cell, dir);
        }

        /// <summary>
        /// 칸의 dir 쪽 경계가 아직 닫힌 문인지 판단한다.
        /// cell(열, 행), dir(칸 방향), 문 상태를 사용하며, 닫힌 문이면 true를 반환한다.
        /// </summary>
        public bool IsClosedGate(Vector2Int cell, Vector2Int dir)
        {
            int gate = _built ? GateIndex(cell, dir) : -1;
            return gate >= 0 && !_gates[gate].Open;
        }

        /// <summary>
        /// 미로 칸에서 dir 쪽으로 가면 동공 자리로 들어서는지 판단한다.
        /// cell(열, 행), dir(칸 방향)을 사용하며, 옆 칸이 동공 자리면 true를 반환한다.
        /// </summary>
        public bool LeadsToHole(Vector2Int cell, Vector2Int dir) => IsMazeCell(cell) && IsHole(cell + dir);

        /// <summary>
        /// 칸이 터지지 않게 지켜야 하는 칸인지 판단한다. 아직 먹지 않은 조각 칸과 문 양쪽 칸이다.
        /// cell(열, 행), 조각과 문 목록을 사용하며, 지켜야 하면 true를 반환한다.
        /// </summary>
        public bool IsProtected(Vector2Int cell)
        {
            if (!_built) return false;
            for (int i = 0; i < _shards.Length; i++) if (!_shardTaken[i] && _shards[i] == cell) return true;
            foreach (Gate g in _gates) if (g.Cell == cell || g.Cell + g.Dir == cell) return true;
            return false;
        }

        /// <summary>
        /// 칸에 아직 먹지 않은 조각이 있으면 먹고, 그 층의 안쪽 문을 연다. 문은 번쩍이며 사라진다.
        /// cell(열, 행), 조각과 문 목록, 지금 시각을 사용하며, 조각을 먹었으면 true를 반환하고 조각과 문 상태를 바꾼다.
        /// </summary>
        public bool TryTakeShard(Vector2Int cell)
        {
            if (!_built) return false;
            for (int i = 0; i < _shards.Length; i++)
            {
                if (_shardTaken[i] || _shards[i] != cell) continue;
                _shardTaken[i] = true;
                if (i < _gates.Count)
                {
                    _gates[i].Open = true;
                    _gates[i].OpenStart = Time.time;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 칸의 dir 쪽 벽을 color로 빛나게 했다가 seconds 동안 원래 색으로 돌아오게 한다. 바깥 쪽 경계는 평소 안 보이다가 빛나는 동안만 보인다.
        /// cell(열, 행), dir(칸 방향), color, seconds를 사용하며, 그 경계의 빛남 시작 시각과 빛남 색, 시간을 바꾼다.
        /// </summary>
        public void GlowWall(Vector2Int cell, Vector2Int dir, Color color, float seconds)
        {
            if (!_built || !InBounds(cell) || !InBounds(cell + dir)) return;
            _glowColor = color;
            _glowSeconds = Mathf.Max(0.01f, seconds);
            EdgeIndex(cell, dir, out bool vertical, out int x, out int y);
            if (vertical) _glowStartV[x, y] = Time.time;
            else _glowStartH[x, y] = Time.time;
        }

        // 생성

        /// <summary>
        /// 시드로 미로를 새로 만든다. 바깥 층부터 층에 들어온 자리에서 무작위 깊이 우선 탐색으로 층 안의 칸을 잇고 벽 일부를 더 뚫는다.
        /// 들어온 자리에서 가장 먼 칸에 조각을 두고, 안쪽 층(가장 안쪽 층은 동공 자리)으로 가는 경계 하나를 닫힌 문으로 둔다. 다음 층은 그 문 안쪽 칸에서 시작한다.
        /// _seed, _loopChance, 모양과 층 설정을 사용하며, 경계 열림 기록, 문과 조각, 벽과 조각 그림, 빛남 기록을 바꾼다.
        /// </summary>
        public void Generate()
        {
            int n = Columns;
            _openV = new bool[n + 1, n];
            _openH = new bool[n, n + 1];
            _gateV = NewGateMap(n + 1, n);
            _gateH = NewGateMap(n, n + 1);
            _gates.Clear();
            int layers = LayerCount;
            _shards = new Vector2Int[layers];
            _shardTaken = new bool[layers];
            var rng = new System.Random(_seed);

            Vector2Int entry = StartCell;
            for (int layer = 0; layer < layers; layer++)
            {
                if (LayerOf(entry) != layer && !FindAnyCell(layer, out entry))
                {
                    Debug.LogWarning($"[폭발 미로] {layer}번째 층에 칸이 없다. 층 경계 반지름을 확인한다.", this);
                    break;
                }
                CarveLayer(layer, entry, rng);
                _shards[layer] = FarthestCell(layer, entry);
                if (!PickGate(layer, rng, out Gate gate))
                {
                    Debug.LogWarning($"[폭발 미로] {layer}번째 층에서 안쪽으로 가는 문 자리를 찾지 못했다.", this);
                    break;
                }
                EdgeIndex(gate.Cell, gate.Dir, out bool vertical, out int x, out int y);
                if (vertical) _gateV[x, y] = _gates.Count;
                else _gateH[x, y] = _gates.Count;
                _gates.Add(gate);
                entry = gate.Cell + gate.Dir;
            }
            BuildWalls();
            BuildShards();
            _built = true;
        }

        /// <summary>
        /// 층 하나를 들어온 자리에서 무작위 깊이 우선 탐색으로 잇고, 층 안의 벽 일부를 더 뚫는다. 닿지 못한 칸이 있으면 경고를 남긴다.
        /// layer, entry(들어온 칸), rng, _loopChance를 사용하며, 경계 열림 기록을 바꾼다.
        /// </summary>
        private void CarveLayer(int layer, Vector2Int entry, System.Random rng)
        {
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            int n = Columns;
            bool[,] visited = new bool[n, n];
            var stack = new Stack<Vector2Int>();
            visited[entry.x, entry.y] = true;
            stack.Push(entry);
            var next = new List<Vector2Int>();
            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                next.Clear();
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int c = cur + d;
                    if (LayerOf(c) == layer && !visited[c.x, c.y]) next.Add(d);
                }
                if (next.Count == 0) { stack.Pop(); continue; }
                Vector2Int pick = next[rng.Next(next.Count)];
                SetOpen(cur, pick);
                Vector2Int to = cur + pick;
                visited[to.x, to.y] = true;
                stack.Push(to);
            }

            int unreached = 0;
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (LayerOf(cell) != layer) continue;
                    if (!visited[x, y]) unreached++;
                    foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.up })
                    {
                        if (LayerOf(cell + d) != layer || IsOpenFlag(cell, d)) continue;
                        if (rng.NextDouble() < _loopChance) SetOpen(cell, d);
                    }
                }
            }
            if (unreached > 0) Debug.LogWarning($"[폭발 미로] {layer}번째 층에서 {unreached}칸이 끊겨 닿지 못한다. 층 경계 반지름이나 칸 크기를 확인한다.", this);
        }

        /// <summary>
        /// 층 안에서 들어온 자리로부터 열린 길로 가장 먼 칸을 찾는다. 조각을 둘 자리다.
        /// layer, entry를 사용하며, 가장 먼 칸(열, 행)을 반환한다.
        /// </summary>
        private Vector2Int FarthestCell(int layer, Vector2Int entry)
        {
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            var seen = new HashSet<Vector2Int> { entry };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(entry);
            Vector2Int last = entry;
            while (queue.Count > 0)
            {
                last = queue.Dequeue();
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int c = last + d;
                    if (LayerOf(c) != layer || !IsOpenFlag(last, d) || !seen.Add(c)) continue;
                    queue.Enqueue(c);
                }
            }
            return last;
        }

        /// <summary>
        /// 층에서 안쪽 층(가장 안쪽 층은 동공 자리)으로 가는 경계를 모아 하나를 무작위로 골라 문으로 만든다.
        /// layer, rng를 사용하며, 고른 문을 gate로 내보내고 찾았으면 true를 반환한다.
        /// </summary>
        private bool PickGate(int layer, System.Random rng, out Gate gate)
        {
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            bool last = layer == LayerCount - 1;
            var candidates = new List<(Vector2Int cell, Vector2Int dir)>();
            int n = Columns;
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (LayerOf(cell) != layer) continue;
                    foreach (Vector2Int d in dirs)
                    {
                        bool inward = last ? IsHole(cell + d) : LayerOf(cell + d) == layer + 1;
                        if (inward) candidates.Add((cell, d));
                    }
                }
            }
            gate = null;
            if (candidates.Count == 0) return false;
            (Vector2Int cell, Vector2Int dir) pick = candidates[rng.Next(candidates.Count)];
            gate = new Gate { Cell = pick.cell, Dir = pick.dir };
            return true;
        }

        /// <summary>
        /// 층에서 아무 칸 하나를 찾는다. 들어온 자리가 그 층이 아닐 때 대신 쓴다.
        /// layer를 사용하며, 찾은 칸을 cell로 내보내고 찾았으면 true를 반환한다.
        /// </summary>
        private bool FindAnyCell(int layer, out Vector2Int cell)
        {
            int n = Columns;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    cell = new Vector2Int(x, y);
                    if (LayerOf(cell) == layer) return true;
                }
            }
            cell = Vector2Int.zero;
            return false;
        }

        /// <summary>
        /// 지정한 시작 칸이 외각 칸이면 그대로 쓰고, 아니면 같은 열에서 가장 아래 외각 칸을 찾는다. 그 열에 없으면 가운데 열에서 찾는다.
        /// _startCell과 미로 모양을 사용하며, 시작 칸(열, 행)을 반환한다.
        /// </summary>
        private Vector2Int ResolveStart()
        {
            if (LayerOf(_startCell) == 0) return _startCell;
            foreach (int x in new[] { Mathf.Clamp(_startCell.x, 0, Columns - 1), Columns / 2 })
                for (int y = 0; y < Rows; y++)
                    if (LayerOf(new Vector2Int(x, y)) == 0) return new Vector2Int(x, y);
            return new Vector2Int(Columns / 2, 0);
        }

        /// <summary>
        /// 경계 수만큼의 문 번호 표를 만들고 모두 -1(문 아님)로 채운다.
        /// w, h를 사용하며, 새 표를 반환한다.
        /// </summary>
        private static int[,] NewGateMap(int w, int h)
        {
            var map = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) map[x, y] = -1;
            return map;
        }

        /// <summary>
        /// 칸과 방향으로 경계 하나의 표 위치를 계산한다. 세로 경계는 열 x의 왼쪽, 가로 경계는 행 y의 아래쪽이다.
        /// cell(열, 행), dir(칸 방향)을 사용하며, 세로 경계인지와 표 위치 x, y를 내보낸다.
        /// </summary>
        private static void EdgeIndex(Vector2Int cell, Vector2Int dir, out bool vertical, out int x, out int y)
        {
            vertical = dir.x != 0;
            x = vertical ? (dir.x > 0 ? cell.x + 1 : cell.x) : cell.x;
            y = vertical ? cell.y : (dir.y > 0 ? cell.y + 1 : cell.y);
        }

        /// <summary>
        /// 칸의 dir 쪽 경계가 문이면 문 번호를 찾는다.
        /// cell(열, 행), dir(칸 방향), 문 번호 표를 사용하며, 문 번호 또는 문이 아니면 -1을 반환한다.
        /// </summary>
        private int GateIndex(Vector2Int cell, Vector2Int dir)
        {
            if (_gateV == null || !InBounds(cell) || !InBounds(cell + dir)) return -1;
            EdgeIndex(cell, dir, out bool vertical, out int x, out int y);
            return vertical ? _gateV[x, y] : _gateH[x, y];
        }

        /// <summary>
        /// 경계 열림 기록에서 칸의 dir 쪽 경계가 열려 있는지 읽는다. 문은 포함하지 않는다. 미로를 만드는 중에도 쓴다.
        /// cell(열, 행), dir(칸 방향), _openV와 _openH를 사용하며, 열려 있으면 true를 반환한다.
        /// </summary>
        private bool IsOpenFlag(Vector2Int cell, Vector2Int dir)
        {
            if (_openV == null || !InBounds(cell) || !InBounds(cell + dir)) return false;
            EdgeIndex(cell, dir, out bool vertical, out int x, out int y);
            return vertical ? _openV[x, y] : _openH[x, y];
        }

        /// <summary>
        /// 칸의 dir 쪽 경계를 연다.
        /// cell(열, 행), dir(칸 방향)을 사용하며, 경계 열림 기록을 바꾼다.
        /// </summary>
        private void SetOpen(Vector2Int cell, Vector2Int dir)
        {
            EdgeIndex(cell, dir, out bool vertical, out int x, out int y);
            if (vertical) _openV[x, y] = true;
            else _openH[x, y] = true;
        }

        /// <summary>
        /// 세로 경계가 미로 칸에 닿아 격자선을 그릴 경계인지 판단한다. 동공 자리 안쪽과 미로 바깥의 경계는 그리지 않는다.
        /// x(경계 열), y(행)를 사용하며, 그릴 경계면 true를 반환한다.
        /// </summary>
        public bool HasEdgeV(int x, int y) => IsMazeCell(new Vector2Int(x - 1, y)) || IsMazeCell(new Vector2Int(x, y));

        /// <summary>
        /// 가로 경계가 미로 칸에 닿아 격자선을 그릴 경계인지 판단한다. 동공 자리 안쪽과 미로 바깥의 경계는 그리지 않는다.
        /// x(열), y(경계 행)를 사용하며, 그릴 경계면 true를 반환한다.
        /// </summary>
        public bool HasEdgeH(int x, int y) => IsMazeCell(new Vector2Int(x, y - 1)) || IsMazeCell(new Vector2Int(x, y));

        /// <summary>
        /// 경계가 미로 칸과 그 바깥(원에 걸친 칸이나 원 밖) 사이인지 판단한다. 이런 경계는 늘 막혀 있고, 평소에는 선 없이 부딪힐 때만 빛난다.
        /// a, b(경계 양쪽 칸)를 사용하며, 바깥 경계면 true를 반환한다.
        /// </summary>
        private bool IsRimEdge(Vector2Int a, Vector2Int b)
        {
            bool aOut = !IsMazeCell(a) && !IsHole(a);
            bool bOut = !IsMazeCell(b) && !IsHole(b);
            return (IsMazeCell(a) && bOut) || (IsMazeCell(b) && aOut);
        }

        /// <summary>
        /// 경계 양쪽이 미로 칸이나 동공 자리여서 벽을 곧은 선으로 그릴 경계인지 판단한다. 바깥 경계는 여기에 들지 않는다.
        /// a, b(경계 양쪽 칸)를 사용하며, 그릴 경계면 true를 반환한다.
        /// </summary>
        private bool IsInnerEdge(Vector2Int a, Vector2Int b)
        {
            bool aIn = IsMazeCell(a) || IsHole(a);
            bool bIn = IsMazeCell(b) || IsHole(b);
            return aIn && bIn && (IsMazeCell(a) || IsMazeCell(b));
        }

        /// <summary>
        /// 경계가 층 경계인지 판단한다. 양쪽 칸의 층이 다르거나 한쪽이 동공 자리면 층 경계다.
        /// a, b(경계 양쪽 칸)를 사용하며, 층 경계면 true를 반환한다.
        /// </summary>
        private bool IsLayerEdge(Vector2Int a, Vector2Int b) => LayerOf(a) != LayerOf(b);

        // 그림

        /// <summary>
        /// 막힌 안쪽 경계마다 곧은 벽(문 포함)을 만들고, 바깥 경계에는 부딪힐 때만 보이는 선을 만든다. 옛 벽 그림은 지운다.
        /// 경계 열림 기록, 문 번호 표, 모양 설정, 원형 미로의 선 재질을 사용하며, 벽 LineRenderer, 층·바깥 경계 표시, 빛남 기록, 자식 오브젝트를 바꾼다.
        /// </summary>
        private void BuildWalls()
        {
            Transform container = NewContainer();
            int n = Columns;
            _wallV = new LineRenderer[n + 1, n];
            _wallH = new LineRenderer[n, n + 1];
            _glowStartV = new float[n + 1, n];
            _glowStartH = new float[n, n + 1];
            _layerEdgeV = new bool[n + 1, n];
            _layerEdgeH = new bool[n, n + 1];
            _rimEdgeV = new bool[n + 1, n];
            _rimEdgeH = new bool[n, n + 1];
            float h = _cellSize * 0.5f;
            for (int x = 0; x <= n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    _glowStartV[x, y] = -1f;
                    var right = new Vector2Int(x, y);
                    var left = new Vector2Int(x - 1, y);
                    _rimEdgeV[x, y] = IsRimEdge(left, right);
                    if (_openV[x, y] || (!IsInnerEdge(left, right) && !_rimEdgeV[x, y])) continue;
                    _layerEdgeV[x, y] = !_rimEdgeV[x, y] && IsLayerEdge(left, right);
                    Vector3 c = CellCenterLocal(right);
                    _wallV[x, y] = MakeWall(container, $"세로 {x}-{y}", new Vector3(c.x - h, c.y - h, 0f), new Vector3(c.x - h, c.y + h, 0f));
                }
            }
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y <= n; y++)
                {
                    _glowStartH[x, y] = -1f;
                    var up = new Vector2Int(x, y);
                    var down = new Vector2Int(x, y - 1);
                    _rimEdgeH[x, y] = IsRimEdge(down, up);
                    if (_openH[x, y] || (!IsInnerEdge(down, up) && !_rimEdgeH[x, y])) continue;
                    _layerEdgeH[x, y] = !_rimEdgeH[x, y] && IsLayerEdge(down, up);
                    Vector3 c = CellCenterLocal(up);
                    _wallH[x, y] = MakeWall(container, $"가로 {x}-{y}", new Vector3(c.x - h, c.y - h, 0f), new Vector3(c.x + h, c.y - h, 0f));
                }
            }
        }

        /// <summary>
        /// 벽 묶음 오브젝트를 새로 만든다. 옛 묶음은 같은 이름으로 다시 찾히지 않게 떼어 낸 뒤 지운다.
        /// 입력값은 없으며, 새 묶음 Transform을 반환하고 자식 오브젝트를 바꾼다.
        /// </summary>
        private Transform NewContainer()
        {
            Transform old = transform.Find(WALL_CONTAINER_NAME);
            if (old != null)
            {
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }
            Transform container = new GameObject(WALL_CONTAINER_NAME).transform;
            container.SetParent(transform, false);
            return container;
        }

        /// <summary>
        /// 벽 선 하나를 만든다. 둥근 끝으로 모서리가 이어져 보이게 하고, 처음에는 꺼 둔다.
        /// container, name, 양 끝 a와 b, 원형 미로의 선 재질을 사용하며, 만든 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer MakeWall(Transform container, string name, Vector3 a, Vector3 b)
        {
            var go = new GameObject(name);
            go.transform.SetParent(container, false);
            LineRenderer lr = Shapes.Ensure(go, Maze.LineMaterial, 2);
            Shapes.SetPoints(lr, new[] { a, b }, false);
            lr.sortingOrder = WALL_SORTING_ORDER;
            lr.enabled = false;
            return lr;
        }

        /// <summary>
        /// 층마다 조각 칸 한가운데에 마름모 그림을 만든다. 꽉 찬 사각형을 45도 돌려 마름모로 그린다.
        /// 조각 칸, 칸 크기, _shardScale, 원형 미로의 선 재질을 사용하며, _shardLines와 자식 오브젝트를 바꾼다.
        /// </summary>
        private void BuildShards()
        {
            Transform container = transform.Find(WALL_CONTAINER_NAME);
            _shardLines = new LineRenderer[_shards.Length];
            float side = _cellSize * _shardScale;
            for (int i = 0; i < _shards.Length; i++)
            {
                var go = new GameObject($"조각 {i}");
                go.transform.SetParent(container, false);
                go.transform.localPosition = CellCenterLocal(_shards[i]);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                LineRenderer lr = Shapes.Ensure(go, Maze.LineMaterial, 0);
                Shapes.SetFilledRect(lr, side, side);
                lr.sortingOrder = SHARD_SORTING_ORDER;
                lr.enabled = false;
                _shardLines[i] = lr;
            }
        }

        /// <summary>
        /// 벽과 문을 원형 미로의 지금 벽 색으로 칠하고 켠다. 굵기는 화면 픽셀의 정수배로 맞춰 어디서나 같게 보인다.
        /// 빛나는 벽은 빛남 색으로 물들고 굵어졌다가 돌아오며, 닫힌 문은 문 색으로, 열리는 문은 번쩍이며 사라졌다가 꺼진다. 바깥 경계는 빛나는 동안만 보인다.
        /// 원형 미로의 WallColor, WallWidthNow, PixelSize, 빛남 기록, 문 상태를 사용하며, LineRenderer의 표시 상태, 색, 굵기를 바꾼다.
        /// </summary>
        private void DrawWalls()
        {
            Color wallColor = Maze.WallColor;
            Color gateColor = new Color(_gateColor.r, _gateColor.g, _gateColor.b, _gateColor.a * wallColor.a);
            float width = SnapWidth(Maze.WallWidthNow);
            float layerWidth = SnapWidth(Maze.WallWidthNow * _layerWallWidthScale);
            for (int x = 0; x < _wallV.GetLength(0); x++)
                for (int y = 0; y < _wallV.GetLength(1); y++)
                    PaintEdge(_wallV[x, y], _gateV[x, y], _rimEdgeV[x, y], ref _glowStartV[x, y], wallColor, gateColor, _layerEdgeV[x, y] ? layerWidth : width);
            for (int x = 0; x < _wallH.GetLength(0); x++)
                for (int y = 0; y < _wallH.GetLength(1); y++)
                    PaintEdge(_wallH[x, y], _gateH[x, y], _rimEdgeH[x, y], ref _glowStartH[x, y], wallColor, gateColor, _layerEdgeH[x, y] ? layerWidth : width);
        }

        /// <summary>
        /// 굵기를 화면 픽셀의 정수배(최소 1픽셀)로 맞춘다. 픽셀 크기를 모르면 그대로 둔다.
        /// width와 PixelSize를 사용하며, 맞춘 굵기를 반환한다.
        /// </summary>
        private float SnapWidth(float width)
        {
            if (PixelSize <= 0f) return width;
            return Mathf.Max(1, Mathf.RoundToInt(width / PixelSize)) * PixelSize;
        }

        /// <summary>
        /// 경계 하나를 칠한다. 문이면 닫혔을 때 문 색으로, 열리는 동안 흰빛으로 번쩍이며 투명해지다가 꺼진다. 바깥 경계는 빛나는 동안만 빛남 색으로 보인다.
        /// lr, gate(문 번호 또는 -1), rim(바깥 경계인지), glowStart(참조), 벽 색, 문 색, 굵기, _gateOpenSeconds를 사용하며, lr의 표시 상태, 색, 굵기를 바꾼다.
        /// </summary>
        private void PaintEdge(LineRenderer lr, int gate, bool rim, ref float glowStart, Color wallColor, Color gateColor, float width)
        {
            if (lr == null) return;
            if (rim)
            {
                // 평소 선이 없는 바깥 경계는 부딪혀 빛나는 동안만 빛남 색으로 나타났다 사라진다.
                float glow = glowStart >= 0f ? 1f - (Time.time - glowStart) / _glowSeconds : 0f;
                if (glow <= 0f) { glowStart = -1f; if (lr.enabled) lr.enabled = false; return; }
                Shapes.SetColor(lr, new Color(_glowColor.r, _glowColor.g, _glowColor.b, wallColor.a * glow * glow));
                lr.widthMultiplier = width;
                if (!lr.enabled) lr.enabled = true;
                return;
            }
            if (gate < 0 || !_gates[gate].Open) { PaintLine(lr, ref glowStart, gate < 0 ? wallColor : gateColor, width); return; }
            float t = (Time.time - _gates[gate].OpenStart) / _gateOpenSeconds;
            if (t >= 1f) { if (lr.enabled) lr.enabled = false; return; }
            // 열리는 순간 흰빛으로 굵게 번쩍였다가 투명하게 사라진다.
            float k = 1f - t;
            Color c = Color.Lerp(gateColor, new Color(1f, 1f, 1f, gateColor.a), k);
            c.a *= k;
            Shapes.SetColor(lr, c);
            lr.widthMultiplier = width * (1f + 1.5f * k);
            if (!lr.enabled) lr.enabled = true;
        }

        /// <summary>
        /// 선 하나를 바탕 색으로 칠하고 켠다. 빛나는 동안 빛남 색으로 물들고 굵어지며, 빛남이 끝나면 빛남 시작 시각을 지운다.
        /// lr, glowStart(참조), baseColor, width, 빛남 색과 시간을 사용하며, lr의 표시 상태, 색, 굵기, glowStart를 바꾼다.
        /// </summary>
        private void PaintLine(LineRenderer lr, ref float glowStart, Color baseColor, float width)
        {
            if (lr == null) return;
            float glow = 0f;
            if (glowStart >= 0f)
            {
                float k = 1f - (Time.time - glowStart) / _glowSeconds;
                if (k > 0f) glow = k * k;
                else glowStart = -1f;
            }
            Color c = glow > 0f ? Color.Lerp(baseColor, new Color(_glowColor.r, _glowColor.g, _glowColor.b, baseColor.a), glow) : baseColor;
            Shapes.SetColor(lr, c);
            lr.widthMultiplier = width * (1f + glow);
            if (!lr.enabled) lr.enabled = true;
        }

        /// <summary>
        /// 아직 먹지 않은 조각을 맥박치듯 반짝이게 그리고, 먹은 조각은 끈다.
        /// 조각 상태, _shardColor, _shardScale, _shardPulseRate, 원형 미로의 모드 투명도를 사용하며, 조각 LineRenderer의 표시 상태, 색, 굵기를 바꾼다.
        /// </summary>
        private void DrawShards()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _shardPulseRate * Mathf.PI * 2f);
            Color c = Color.Lerp(_shardColor, Color.white, 0.35f * pulse);
            c.a *= Maze.WallColor.a;
            for (int i = 0; i < _shardLines.Length; i++)
            {
                LineRenderer lr = _shardLines[i];
                bool visible = !_shardTaken[i];
                if (lr.enabled != visible) lr.enabled = visible;
                if (!visible) continue;
                Shapes.SetColor(lr, c);
                float side = _cellSize * _shardScale * (0.9f + 0.15f * pulse);
                Shapes.SetFilledRect(lr, side, side);
            }
        }

        /// <summary>
        /// 사각 벽, 문, 바깥 경계, 조각 그림을 모두 끈다.
        /// 입력값은 없으며, LineRenderer의 표시 상태를 바꾼다.
        /// </summary>
        private void HideAll()
        {
            if (_wallV == null) return;
            foreach (LineRenderer lr in _wallV) if (lr != null) lr.enabled = false;
            foreach (LineRenderer lr in _wallH) if (lr != null) lr.enabled = false;
            foreach (LineRenderer lr in _shardLines) if (lr != null) lr.enabled = false;
        }

        /// <summary>
        /// 원형 미로 층의 벽과 문 그림을 숨기거나 다시 보이게 한다. 층을 다시 만들면 새 벽이 보이므로 켜져 있는 동안 매 프레임 숨긴다.
        /// hide와 원형 미로 층의 벽 묶음을 사용하며, 그 아래 LineRenderer의 표시 상태와 _irisWallsHidden을 바꾼다.
        /// </summary>
        private void HideIrisWalls(bool hide)
        {
            if (!hide && !_irisWallsHidden) return;
            _irisWallsHidden = hide;
            foreach (MazeLayer layer in Maze.Layers)
            {
                Transform walls = layer.transform.Find(MazeLayer.WALL_CONTAINER_NAME);
                if (walls == null) continue;
                foreach (LineRenderer lr in walls.GetComponentsInChildren<LineRenderer>(true)) lr.enabled = !hide;
            }
        }

        void OnDrawGizmos()
        {
            // 쓰는 미로 칸을 층마다 밝기를 달리해 그리고, 테두리 원과 시작 칸을 씬 뷰에 보여 준다.
            Gizmos.matrix = transform.localToWorldMatrix;
            var size = new Vector3(_cellSize, _cellSize, 0f);
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    int layer = LayerOf(new Vector2Int(x, y));
                    if (layer < 0) continue;
                    Color c = _gizmoColor;
                    c.a *= 1f - 0.25f * layer;
                    Gizmos.color = c;
                    Gizmos.DrawWireCube(CellCenterLocal(new Vector2Int(x, y)), size);
                }
            }
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(CellCenterLocal(StartCell), _cellSize * 0.15f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
