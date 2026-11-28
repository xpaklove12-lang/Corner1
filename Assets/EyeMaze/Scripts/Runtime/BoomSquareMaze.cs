using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로의 사각 미로. 홍채 원 안을 가로세로 칸이 바둑판처럼 채우고, 벽은 칸 경계를 따라 곧은 선으로 그려진다.
    /// 칸 전체가 바깥 반지름 안에 드는 칸만 미로 칸이고, 일부라도 안쪽 반지름(동공) 안에 드는 칸은 동공 자리다.
    /// 동공 자리를 두른 벽 하나를 입구로 연다. 칸 좌표는 열(0 = 왼쪽)과 행(0 = 아래)이고, 이 오브젝트의 로컬 원점이 원의 한가운데다.
    /// 켜져 있는 동안 원형 미로 층의 벽과 문 그림을 숨긴다. 원형 벽의 판정은 건드리지 않는다.
    /// </summary>
    public class BoomSquareMaze : MonoBehaviour
    {
        private const string WALL_CONTAINER_NAME = "사각 벽";
        private const int WALL_SORTING_ORDER = 5;
        private const float FIT_EPSILON = 1e-4f;

        [Header("모양")]
        [Tooltip("칸 한 변의 길이 (미로 기준)")]
        [SerializeField, Min(0.05f)] private float _cellSize = 0.4f;
        [Tooltip("미로를 채울 원의 바깥 반지름. 칸 전체가 이 원 안에 드는 칸만 쓴다. 홍채 반지름에 맞춘다")]
        [SerializeField, Min(0.1f)] private float _outerRadius = 3f;
        [Tooltip("동공 자리 반지름. 칸의 일부라도 이 원 안에 들면 동공 자리다. 동공 반지름에 맞춘다")]
        [SerializeField, Min(0f)] private float _innerRadius = 0.9f;
        [Tooltip("시선이 시작하는 칸 (열, 행). 휩쓸렸을 때 안전지대를 아직 밟지 않았으면 여기로 돌아간다. 미로 칸이 아니면 같은 열의 가장 아래 미로 칸을 쓴다")]
        [SerializeField] private Vector2Int _startCell = new Vector2Int(7, 1);

        [Header("생성")]
        [Tooltip("미로를 만드는 시드. 같은 시드면 언제나 같은 미로다")]
        [SerializeField] private int _seed = 7;
        [Tooltip("막다른 길을 줄이기 위해 추가로 뚫는 벽 비율")]
        [SerializeField, Range(0f, 0.4f)] private float _loopChance = 0.08f;

        [Header("씬 뷰 표시")]
        [SerializeField] private Color _gizmoColor = new Color(1f, 1f, 1f, 0.25f);

        // 세로 경계(열 x의 왼쪽, x = 0 ~ 열 수)와 가로 경계(행 y의 아래쪽, y = 0 ~ 행 수)가 열려 있는지
        private bool[,] _openV;
        private bool[,] _openH;
        // 그려진 벽. 경계 하나에 벽 하나다. 열린 경계와 미로 칸에 닿지 않는 경계는 없다.
        private LineRenderer[,] _wallV;
        private LineRenderer[,] _wallH;
        // 벽이 빛나기 시작한 시각(빛나지 않으면 -1), 빛나는 시간과 색
        private float[,] _glowStartV;
        private float[,] _glowStartH;
        private float _glowSeconds = 0.4f;
        private Color _glowColor = Color.white;
        private bool _built;
        private bool _irisWallsHidden;
        private IrisMaze _maze;

        /// <summary>바둑판의 가로 칸 수. 바깥 원을 덮는 가장 작은 홀수라 원의 한가운데가 가운데 칸의 중심이다.</summary>
        public int Columns => 2 * Mathf.CeilToInt(_outerRadius / _cellSize - 0.5f) + 1;
        /// <summary>바둑판의 세로 칸 수. 가로 칸 수와 같다.</summary>
        public int Rows => Columns;
        public float CellSize => _cellSize;
        /// <summary>시선이 시작하는 칸. 지정한 칸이 미로 칸이 아니면 같은 열의 가장 아래 미로 칸이다.</summary>
        public Vector2Int StartCell => ResolveStart();
        /// <summary>미로 모양 설정이 바뀌었는지 알아보는 값</summary>
        public int GeometryKey => HashCode.Combine(_cellSize, _outerRadius, _innerRadius);
        /// <summary>켜져 있으면 사각 벽을 그리고 원형 미로의 벽 그림을 숨긴다. 폭발 미로가 들여다보는 동안 켠다.</summary>
        public bool Active { get; set; }

        private IrisMaze Maze => _maze != null ? _maze : (_maze = GetComponentInParent<IrisMaze>());

        void OnValidate()
        {
            _innerRadius = Mathf.Clamp(_innerRadius, 0f, _outerRadius - _cellSize);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (Active && _built) DrawWalls();
            SetWallsVisible(Active && _built);
            HideIrisWalls(Active);
        }

        void OnDisable()
        {
            if (Maze != null) HideIrisWalls(false);
        }

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
            return Mathf.Sqrt(fx * fx + fy * fy) <= _outerRadius + FIT_EPSILON;
        }

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

        /// <summary>
        /// 칸에서 dir 쪽 경계가 열려 있는지 판단한다. 원 테두리와 쓰지 않는 칸 쪽은 늘 막혀 있다.
        /// cell(열, 행), dir(칸 방향), 경계 열림 기록을 사용하며, 지나갈 수 있으면 true를 반환한다.
        /// </summary>
        public bool IsOpen(Vector2Int cell, Vector2Int dir)
        {
            return _built && IsOpenFlag(cell, dir);
        }

        /// <summary>
        /// 경계 열림 기록에서 칸의 dir 쪽 경계가 열려 있는지 읽는다. 미로를 만드는 중에도 쓴다.
        /// cell(열, 행), dir(칸 방향), _openV와 _openH를 사용하며, 열려 있으면 true를 반환한다.
        /// </summary>
        private bool IsOpenFlag(Vector2Int cell, Vector2Int dir)
        {
            if (_openV == null || !InBounds(cell) || !InBounds(cell + dir)) return false;
            if (dir.x != 0) return _openV[dir.x > 0 ? cell.x + 1 : cell.x, cell.y];
            return _openH[cell.x, dir.y > 0 ? cell.y + 1 : cell.y];
        }

        /// <summary>
        /// 미로 칸에서 dir 쪽으로 가면 동공 자리로 들어서는지 판단한다.
        /// cell(열, 행), dir(칸 방향)을 사용하며, 옆 칸이 동공 자리면 true를 반환한다.
        /// </summary>
        public bool LeadsToHole(Vector2Int cell, Vector2Int dir) => IsMazeCell(cell) && IsHole(cell + dir);

        /// <summary>
        /// 칸의 dir 쪽 벽을 color로 빛나게 했다가 seconds 동안 원래 색으로 돌아오게 한다. 원 테두리 벽도 빛난다.
        /// cell(열, 행), dir(칸 방향), color, seconds를 사용하며, 그 벽의 빛남 시작 시각과 빛남 색, 시간을 바꾼다.
        /// </summary>
        public void GlowWall(Vector2Int cell, Vector2Int dir, Color color, float seconds)
        {
            if (!_built || !InBounds(cell)) return;
            _glowColor = color;
            _glowSeconds = Mathf.Max(0.01f, seconds);
            if (dir.x != 0) _glowStartV[dir.x > 0 ? cell.x + 1 : cell.x, cell.y] = Time.time;
            else _glowStartH[cell.x, dir.y > 0 ? cell.y + 1 : cell.y] = Time.time;
        }

        /// <summary>
        /// 시드로 미로를 새로 만든다. 시작 칸에서 무작위 깊이 우선 탐색으로 모든 미로 칸을 잇고,
        /// 벽 일부를 더 뚫은 뒤 동공 자리를 두른 벽 하나를 입구로 연다. 옛 벽 그림은 지우고 새로 만든다.
        /// _seed, _loopChance, 모양 설정을 사용하며, 경계 열림 기록, 벽 그림과 빛남 기록을 바꾼다.
        /// </summary>
        public void Generate()
        {
            int columns = Columns;
            int rows = Rows;
            _openV = new bool[columns + 1, rows];
            _openH = new bool[columns, rows + 1];
            var rng = new System.Random(_seed);
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            Vector2Int start = StartCell;
            bool[,] visited = new bool[columns, rows];
            var stack = new Stack<Vector2Int>();
            visited[start.x, start.y] = true;
            stack.Push(start);
            var next = new List<Vector2Int>();
            while (stack.Count > 0)
            {
                Vector2Int cur = stack.Peek();
                next.Clear();
                foreach (Vector2Int d in dirs)
                {
                    Vector2Int n = cur + d;
                    if (IsMazeCell(n) && !visited[n.x, n.y]) next.Add(d);
                }
                if (next.Count == 0) { stack.Pop(); continue; }
                Vector2Int pick = next[rng.Next(next.Count)];
                SetOpen(cur, pick);
                Vector2Int to = cur + pick;
                visited[to.x, to.y] = true;
                stack.Push(to);
            }

            // 막다른 길을 줄이도록 미로 칸 사이 경계를 조금 더 뚫고, 동공 자리로 이어질 수 있는 경계를 모은다.
            var entrances = new List<(Vector2Int cell, Vector2Int dir)>();
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!IsMazeCell(cell)) continue;
                    foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.up })
                    {
                        if (!IsMazeCell(cell + d) || IsOpenFlag(cell, d)) continue;
                        if (rng.NextDouble() < _loopChance) SetOpen(cell, d);
                    }
                    foreach (Vector2Int d in dirs) if (LeadsToHole(cell, d)) entrances.Add((cell, d));
                }
            }
            if (entrances.Count > 0)
            {
                (Vector2Int cell, Vector2Int dir) e = entrances[rng.Next(entrances.Count)];
                SetOpen(e.cell, e.dir);
            }
            BuildWalls();
            _built = true;
        }

        /// <summary>
        /// 지정한 시작 칸이 미로 칸이면 그대로 쓰고, 아니면 같은 열에서 가장 아래 미로 칸을 찾는다. 그 열에 없으면 가운데 열에서 찾는다.
        /// _startCell과 미로 모양을 사용하며, 시작 칸(열, 행)을 반환한다.
        /// </summary>
        private Vector2Int ResolveStart()
        {
            if (IsMazeCell(_startCell)) return _startCell;
            foreach (int x in new[] { Mathf.Clamp(_startCell.x, 0, Columns - 1), Columns / 2 })
                for (int y = 0; y < Rows; y++)
                    if (IsMazeCell(new Vector2Int(x, y))) return new Vector2Int(x, y);
            return new Vector2Int(Columns / 2, 0);
        }

        /// <summary>
        /// 칸의 dir 쪽 경계를 연다.
        /// cell(열, 행), dir(칸 방향)을 사용하며, 경계 열림 기록을 바꾼다.
        /// </summary>
        private void SetOpen(Vector2Int cell, Vector2Int dir)
        {
            if (dir.x != 0) _openV[dir.x > 0 ? cell.x + 1 : cell.x, cell.y] = true;
            else _openH[cell.x, dir.y > 0 ? cell.y + 1 : cell.y] = true;
        }

        /// <summary>
        /// 세로 경계가 미로 칸에 닿아 벽이나 격자선을 그릴 경계인지 판단한다. 동공 자리 안쪽과 원 밖의 경계는 그리지 않는다.
        /// x(경계 열), y(행)를 사용하며, 그릴 경계면 true를 반환한다.
        /// </summary>
        public bool HasEdgeV(int x, int y) => IsMazeCell(new Vector2Int(x - 1, y)) || IsMazeCell(new Vector2Int(x, y));

        /// <summary>
        /// 가로 경계가 미로 칸에 닿아 벽이나 격자선을 그릴 경계인지 판단한다. 동공 자리 안쪽과 원 밖의 경계는 그리지 않는다.
        /// x(열), y(경계 행)를 사용하며, 그릴 경계면 true를 반환한다.
        /// </summary>
        public bool HasEdgeH(int x, int y) => IsMazeCell(new Vector2Int(x, y - 1)) || IsMazeCell(new Vector2Int(x, y));

        /// <summary>
        /// 막힌 경계마다 곧은 벽 선을 만든다. 옛 벽 그림은 지운다.
        /// 경계 열림 기록, 모양 설정, 원형 미로의 선 재질을 사용하며, 벽 LineRenderer와 빛남 기록, 자식 오브젝트를 바꾼다.
        /// </summary>
        private void BuildWalls()
        {
            Transform old = transform.Find(WALL_CONTAINER_NAME);
            if (old != null)
            {
                // Destroy는 프레임 끝에 일어나므로 같은 이름으로 다시 찾히지 않게 먼저 떼어 낸다.
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }
            Transform container = new GameObject(WALL_CONTAINER_NAME).transform;
            container.SetParent(transform, false);

            int columns = Columns;
            int rows = Rows;
            _wallV = new LineRenderer[columns + 1, rows];
            _wallH = new LineRenderer[columns, rows + 1];
            _glowStartV = new float[columns + 1, rows];
            _glowStartH = new float[columns, rows + 1];
            float h = _cellSize * 0.5f;
            for (int x = 0; x <= columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    _glowStartV[x, y] = -1f;
                    if (_openV[x, y] || !HasEdgeV(x, y)) continue;
                    Vector3 c = CellCenterLocal(new Vector2Int(x, y));
                    float ex = c.x - h;
                    _wallV[x, y] = MakeWall(container, $"세로 {x}-{y}", new Vector3(ex, c.y - h, 0f), new Vector3(ex, c.y + h, 0f));
                }
            }
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y <= rows; y++)
                {
                    _glowStartH[x, y] = -1f;
                    if (_openH[x, y] || !HasEdgeH(x, y)) continue;
                    Vector3 c = CellCenterLocal(new Vector2Int(x, y));
                    float ey = c.y - h;
                    _wallH[x, y] = MakeWall(container, $"가로 {x}-{y}", new Vector3(c.x - h, ey, 0f), new Vector3(c.x + h, ey, 0f));
                }
            }
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
        /// 벽을 원형 미로의 지금 벽 색과 굵기로 칠한다. 빛나는 벽은 빛남 색으로 물들고 굵어졌다가 돌아온다.
        /// 원형 미로의 WallColor, WallWidthNow, 빛남 기록을 사용하며, 벽 LineRenderer의 색과 굵기를 바꾼다.
        /// </summary>
        private void DrawWalls()
        {
            Color baseColor = Maze.WallColor;
            float width = Maze.WallWidthNow;
            for (int x = 0; x < _wallV.GetLength(0); x++)
                for (int y = 0; y < _wallV.GetLength(1); y++)
                    PaintWall(_wallV[x, y], ref _glowStartV[x, y], baseColor, width);
            for (int x = 0; x < _wallH.GetLength(0); x++)
                for (int y = 0; y < _wallH.GetLength(1); y++)
                    PaintWall(_wallH[x, y], ref _glowStartH[x, y], baseColor, width);
        }

        /// <summary>
        /// 벽 하나를 칠한다. 빛남이 끝나면 빛남 시작 시각을 지운다.
        /// lr, glowStart(참조), baseColor, width, 빛남 색과 시간을 사용하며, lr의 색과 굵기, glowStart를 바꾼다.
        /// </summary>
        private void PaintWall(LineRenderer lr, ref float glowStart, Color baseColor, float width)
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
        }

        /// <summary>
        /// 사각 벽 그림을 모두 켜거나 끈다.
        /// visible을 사용하며, 벽 LineRenderer의 표시 상태를 바꾼다.
        /// </summary>
        private void SetWallsVisible(bool visible)
        {
            if (_wallV == null) return;
            foreach (LineRenderer lr in _wallV) if (lr != null && lr.enabled != visible) lr.enabled = visible;
            foreach (LineRenderer lr in _wallH) if (lr != null && lr.enabled != visible) lr.enabled = visible;
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
            // 쓰는 미로 칸과 원, 시작 칸을 씬 뷰에 그려 안전지대를 놓을 자리를 보여 준다.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            var size = new Vector3(_cellSize, _cellSize, 0f);
            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (IsMazeCell(cell)) Gizmos.DrawWireCube(CellCenterLocal(cell), size);
                }
            }
            Gizmos.DrawSphere(CellCenterLocal(StartCell), _cellSize * 0.15f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
