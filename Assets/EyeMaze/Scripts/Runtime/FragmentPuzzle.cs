using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 037: 파편을 열 때 길이 그려진 조각들의 자리를 바꿔 시작에서 단서까지 길을 잇고, 말을 걸려 단서에 닿는 조각 맞추기 미로.
    /// </summary>
    public class FragmentPuzzle : MonoBehaviour
    {
        private const int MIN_SIZE = 2;
        private const int CIRCLE_SEGMENTS = 24;
        private const int DIR_COUNT = 4;
        private const int SHUFFLE_TRIES = 30;

        // 방향 순서: 위, 오른쪽, 아래, 왼쪽. 비트마스크의 비트 번호와 같다.
        private static readonly int[] DIR_DR = { 1, 0, -1, 0 };
        private static readonly int[] DIR_DC = { 0, 1, 0, -1 };

        [Header("퍼즐")]
        [Tooltip("층별 판 한 변의 칸 수 (0번이 1층). 층이 더 많으면 마지막 값을 쓴다")]
        [SerializeField] private int[] _gridSizes = { 3, 3, 4 };
        [Tooltip("정답 경로 밖 조각이 가지는 길 수의 최솟값")]
        [SerializeField, Range(1, 4)] private int _decoyMinPaths = 1;
        [Tooltip("정답 경로 밖 조각이 가지는 길 수의 최댓값")]
        [SerializeField, Range(1, 4)] private int _decoyMaxPaths = 2;

        [Header("움직임")]
        [Tooltip("두 조각이 미끄러지며 자리를 바꾸는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _swapSeconds = 0.15f;
        [Tooltip("조각이 시계 방향으로 90도 도는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _rotateSeconds = 0.12f;
        [Tooltip("말이 한 칸 걷는 시간(초). 키를 누르고 있으면 이 간격으로 계속 걷는다")]
        [SerializeField, Min(0.01f)] private float _moveSeconds = 0.12f;

        [Header("적")]
        [Tooltip("적이 나오기 시작하는 층 (1층부터 센다). 0이면 나오지 않는다")]
        [SerializeField, Min(0)] private int _enemyFromFloor = 3;
        [Tooltip("한 퍼즐에 나오는 적의 수")]
        [SerializeField, Min(1)] private int _enemyCount = 2;
        [Tooltip("퍼즐이 열린 뒤 적이 움직이기 시작할 때까지 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float _enemyStartDelay = 1.5f;
        [Tooltip("적이 한 칸 걷고 다음 칸으로 가기 전까지 쉬는 시간(초)")]
        [SerializeField, Min(0f)] private float _enemyPauseSeconds = 0.5f;
        [Tooltip("적이 한 칸 걷는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _enemyMoveSeconds = 0.3f;
        [SerializeField] private Color _enemyColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField, Min(0.001f)] private float _enemySize = 0.24f;

        [Header("바위")]
        [Tooltip("적이 나오는 퍼즐에서 정답 길 위에 놓는 바위 수. 적의 수보다 많으면 적의 수만큼만 놓는다")]
        [SerializeField, Min(0)] private int _rockCount = 1;
        [SerializeField] private Color _rockColor = new Color(0.6f, 0.55f, 0.5f, 1f);
        [Tooltip("바위 한 변 길이 (칸 크기에 대한 비율)")]
        [SerializeField, Range(0.1f, 1f)] private float _rockSize = 0.5f;

        [Header("모양")]
        [SerializeField] private Material _lineMaterial;
        [Tooltip("퍼즐판 한 변 길이(월드)")]
        [SerializeField, Min(0.1f)] private float _boardSize = 3.2f;
        [Tooltip("뒤에 까는 어두운 원판 지름")]
        [SerializeField, Min(0.1f)] private float _backdropDiameter = 4.8f;
        [SerializeField] private Color _backdropColor = Color.black;
        [SerializeField] private Color _frameColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color _pathColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("시작 조각과 이어진 길의 색")]
        [SerializeField] private Color _linkedColor = Color.white;
        [SerializeField] private Color _walkerColor = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] private Color _cursorColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _pickedColor = new Color(1f, 0.55f, 0.2f, 1f);
        [SerializeField, Min(0.001f)] private float _frameWidth = 0.03f;
        [SerializeField, Min(0.001f)] private float _pathWidth = 0.14f;
        [SerializeField, Min(0.001f)] private float _walkerSize = 0.22f;
        [Tooltip("그리기 순서. 눈꺼풀(22)보다 낮게 둔다")]
        [SerializeField] private int _sortingOrder = 20;

        /// <summary>판의 한 칸에 놓이는 조각. 열린 쪽 비트마스크와 그리기 오브젝트를 가진다.</summary>
        private class Tile
        {
            public int Mask;
            public int Turns;
            public Transform Root;
            public LineRenderer Frame;
            public readonly List<LineRenderer> Arms = new List<LineRenderer>();
        }

        /// <summary>판 위를 걸어 말에게 다가가는 적 하나의 위치와 걷기 상태.</summary>
        private class Enemy
        {
            public int Cell;
            public int From;
            public float MoveT = 1f;
            public float WaitT;
            public LineRenderer Dot;
        }

        private Tile[] _slots = Array.Empty<Tile>();
        private bool[] _linked = Array.Empty<bool>();
        private int _size;
        private int _start;
        private int _clue;
        private int _cursor;
        private int _picked = -1;
        private int _walker;
        private int _walkFrom;
        private float _moveT = 1f;
        private int _queuedDr;
        private int _queuedDc;
        private int _swapA = -1;
        private int _swapB = -1;
        private float _swapT = 1f;
        private int _rotateCell = -1;
        private float _rotateT = 1f;
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private bool _hasEnemy;
        private readonly Dictionary<int, LineRenderer> _rocks = new Dictionary<int, LineRenderer>();
        private bool _isOpen;
        private Transform _drawRoot;
        private LineRenderer _cursorFrame;
        private LineRenderer _walkerDot;

        public event Action Solved;
        public event Action Cancelled;
        public event Action Caught;

        public bool IsOpen => _isOpen;
        private int CellCount => _size * _size;
        private float CellSize => _boardSize / _size;

        void Update()
        {
            if (!_isOpen) return;
            if (DialogueLog.BlocksInput) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Close();
                Cancelled?.Invoke();
                return;
            }
            float dt = Time.deltaTime;
            if (_swapT < 1f)
            {
                AdvanceSwap(dt);
                return;
            }
            if (_rotateT < 1f)
            {
                AdvanceRotate(dt);
                return;
            }
            HandleTileInput(kb);
            HandleWalk(kb, dt);
            if (!_isOpen || !_hasEnemy) return;
            // 바위를 부순 적은 목록에서 빠지므로 뒤에서부터 돈다.
            for (int i = _enemies.Count - 1; i >= 0; i--) AdvanceEnemy(_enemies[i], dt);
            if (IsCaught())
            {
                Close();
                Caught?.Invoke();
            }
        }

        void OnDisable()
        {
            if (_isOpen) Close();
        }

        /// <summary>
        /// layerIndex에 맞는 크기로 항상 풀 수 있는 섞인 조각판을 만들고 그린다.
        /// layerIndex와 seed를 사용하며, 조각 배치, 선택 틀, 말 위치와 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Open(int layerIndex, int seed)
        {
            int sizeIndex = Mathf.Clamp(layerIndex, 0, _gridSizes.Length - 1);
            _size = Mathf.Max(MIN_SIZE, _gridSizes.Length > 0 ? _gridSizes[sizeIndex] : 3);
            _start = 0;
            _clue = CellCount - 1;
            _walker = _start;
            _walkFrom = _start;
            _cursor = _start;
            _picked = -1;
            _moveT = 1f;
            _swapT = 1f;
            _swapA = _swapB = -1;
            _rotateCell = -1;
            _rotateT = 1f;
            _queuedDr = _queuedDc = 0;
            _hasEnemy = _enemyFromFloor > 0 && layerIndex + 1 >= _enemyFromFloor;
            _enemies.Clear();
            _rocks.Clear();
            _isOpen = true;

            var rng = new System.Random(seed);
            var route = new List<int>();
            int[] masks = Generate(rng, route);
            BuildDrawing(masks);
            if (_hasEnemy) PlaceRocks(route, rng);
            Shuffle(rng);
            if (_hasEnemy) SpawnEnemies(rng);
            Refresh();
        }

        /// <summary>
        /// 퍼즐을 닫고 그리기 오브젝트를 지운다.
        /// 입력값은 없으며, 열림 상태와 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Close()
        {
            _isOpen = false;
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _drawRoot = null;
            _cursorFrame = null;
            _walkerDot = null;
            _enemies.Clear();
            _rocks.Clear();
            _slots = Array.Empty<Tile>();
        }

        /// <summary>
        /// 시작 칸에서 단서 칸까지 무작위 경로를 만들고, 칸마다 열린 쪽 비트마스크를 정한다.
        /// rng와 길 수 설정을 사용하며, route에 정답 경로를 채우고 정답 배치의 칸별 비트마스크 배열을 반환한다.
        /// </summary>
        private int[] Generate(System.Random rng, List<int> route)
        {
            var masks = new int[CellCount];
            route.Clear();
            route.Add(_start);
            var used = new bool[CellCount];
            used[_start] = true;
            FindRoute(route, used, rng);
            var onRoute = new bool[CellCount];
            foreach (int cell in route) onRoute[cell] = true;
            for (int i = 1; i < route.Count; i++)
            {
                int d = DirTo(route[i - 1], route[i]);
                masks[route[i - 1]] |= 1 << d;
                masks[route[i]] |= 1 << ((d + 2) % DIR_COUNT);
            }

            // 정답 경로 밖 조각은 판 안쪽으로 난 길 몇 개를 무작위로 가진다.
            int lo = Mathf.Min(_decoyMinPaths, _decoyMaxPaths), hi = Mathf.Max(_decoyMinPaths, _decoyMaxPaths);
            for (int cell = 0; cell < CellCount; cell++)
            {
                if (onRoute[cell]) continue;
                var dirs = new List<int>();
                for (int d = 0; d < DIR_COUNT; d++) if (Neighbor(cell, d) >= 0) dirs.Add(d);
                ShuffleList(dirs, rng);
                int count = Mathf.Min(dirs.Count, rng.Next(lo, hi + 1));
                for (int i = 0; i < count; i++) masks[cell] |= 1 << dirs[i];
            }
            return masks;
        }

        /// <summary>
        /// 무작위 순서의 깊이 우선 탐색으로 route를 단서 칸까지 늘린다.
        /// route, used, rng를 사용하며, 단서 칸에 닿았으면 true를 반환한다.
        /// </summary>
        private bool FindRoute(List<int> route, bool[] used, System.Random rng)
        {
            int current = route[route.Count - 1];
            if (current == _clue) return true;
            var dirs = new List<int> { 0, 1, 2, 3 };
            ShuffleList(dirs, rng);
            foreach (int d in dirs)
            {
                int n = Neighbor(current, d);
                if (n < 0 || used[n]) continue;
                used[n] = true;
                route.Add(n);
                if (FindRoute(route, used, rng)) return true;
                route.RemoveAt(route.Count - 1);
            }
            return false;
        }

        /// <summary>
        /// 시작·단서·바위 조각을 뺀 조각들의 자리를 섞는다. 바위를 빼고 봐도 이미 이어져 있으면 다시 섞는다.
        /// rng를 사용하며, _slots와 조각 오브젝트 위치를 바꾼다.
        /// </summary>
        private void Shuffle(System.Random rng)
        {
            var movable = new List<int>();
            for (int cell = 0; cell < CellCount; cell++)
                if (cell != _start && cell != _clue && !_rocks.ContainsKey(cell)) movable.Add(cell);
            for (int attempt = 0; attempt < SHUFFLE_TRIES; attempt++)
            {
                for (int i = movable.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (_slots[movable[i]], _slots[movable[j]]) = (_slots[movable[j]], _slots[movable[i]]);
                }
                UpdateLinked(false);
                if (!_linked[_clue]) break;
            }
            for (int cell = 0; cell < CellCount; cell++) _slots[cell].Root.localPosition = CellPos(cell);
        }

        /// <summary>
        /// list의 순서를 rng로 섞는다.
        /// list와 rng를 사용하며, list의 순서를 바꾼다.
        /// </summary>
        private static void ShuffleList(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// cell에서 dir 방향의 이웃 칸을 구한다.
        /// cell과 dir을 사용하며, 판 안이면 이웃 칸 번호, 밖이면 -1을 반환한다.
        /// </summary>
        private int Neighbor(int cell, int dir)
        {
            int r = cell / _size + DIR_DR[dir], c = cell % _size + DIR_DC[dir];
            if (r < 0 || r >= _size || c < 0 || c >= _size) return -1;
            return r * _size + c;
        }

        /// <summary>
        /// 이웃한 두 칸 a에서 b로 가는 방향을 구한다.
        /// a와 b를 사용하며, 방향 번호(위 0, 오른쪽 1, 아래 2, 왼쪽 3)를 반환한다.
        /// </summary>
        private int DirTo(int a, int b)
        {
            for (int d = 0; d < DIR_COUNT; d++) if (Neighbor(a, d) == b) return d;
            return 0;
        }

        /// <summary>
        /// 지금 배치에서 cell과 dir 방향 이웃이 서로 마주 보는 길로 이어져 있는지 확인한다.
        /// cell, dir과 _slots를 사용하며, 이어져 있으면 true를 반환한다.
        /// </summary>
        private bool IsConnected(int cell, int dir)
        {
            int n = Neighbor(cell, dir);
            if (n < 0) return false;
            return (_slots[cell].Mask & (1 << dir)) != 0 && (_slots[n].Mask & (1 << ((dir + 2) % DIR_COUNT))) != 0;
        }

        /// <summary>
        /// 시작 칸에서 이어진 길로 닿을 수 있는 칸을 표시한다. rocksBlock이면 바위 칸에서 길이 막힌다.
        /// _slots, _rocks와 rocksBlock을 사용하며, _linked를 바꾼다.
        /// </summary>
        private void UpdateLinked(bool rocksBlock)
        {
            _linked = new bool[CellCount];
            var queue = new Queue<int>();
            queue.Enqueue(_start);
            _linked[_start] = true;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int d = 0; d < DIR_COUNT; d++)
                {
                    if (!IsConnected(cell, d)) continue;
                    int n = Neighbor(cell, d);
                    if (_linked[n] || (rocksBlock && _rocks.ContainsKey(n))) continue;
                    _linked[n] = true;
                    queue.Enqueue(n);
                }
            }
        }

        /// <summary>
        /// cell이 집거나 바꿀 수 없는 고정 칸인지 확인한다.
        /// cell과 시작·단서·말 위치를 사용하며, 고정이면 true를 반환한다.
        /// </summary>
        private bool IsLocked(int cell)
        {
            if (cell == _start || cell == _clue || cell == _walker || (_moveT < 1f && cell == _walkFrom)) return true;
            // 적이 있는 조각은 적이 걷는 중이어도 옮기거나 돌릴 수 있다. 걷던 적은 StopEnemiesOn에서 멈춘다.
            return _rocks.ContainsKey(cell);
        }

        /// <summary>
        /// 방향키로 선택 틀을 옮기고, R로 선택한 조각을 돌리고, Space로 조각을 집거나 두 조각의 자리를 바꾼다.
        /// kb를 사용하며, _cursor, _picked와 회전·자리 바꾸기 상태를 바꾼다.
        /// </summary>
        private void HandleTileInput(Keyboard kb)
        {
            int dr = 0, dc = 0;
            if (kb.upArrowKey.wasPressedThisFrame) dr = 1;
            else if (kb.downArrowKey.wasPressedThisFrame) dr = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame) dc = 1;
            else if (kb.leftArrowKey.wasPressedThisFrame) dc = -1;
            if (dr != 0 || dc != 0)
            {
                int r = Mathf.Clamp(_cursor / _size + dr, 0, _size - 1);
                int c = Mathf.Clamp(_cursor % _size + dc, 0, _size - 1);
                _cursor = r * _size + c;
                Refresh();
            }

            if (kb.rKey.wasPressedThisFrame && !IsLocked(_cursor))
            {
                StartRotate(_cursor);
                return;
            }

            if (!kb.spaceKey.wasPressedThisFrame) return;
            if (_picked < 0)
            {
                if (!IsLocked(_cursor)) _picked = _cursor;
            }
            else if (_picked == _cursor) _picked = -1;
            else if (!IsLocked(_cursor) && !IsLocked(_picked))
            {
                StartSwap(_picked, _cursor);
                _picked = -1;
            }
            Refresh();
        }

        /// <summary>
        /// 두 칸 a, b의 조각을 맞바꾸고 미끄러지는 연출을 시작한다. 조각 위에 서 있던 적도 조각과 함께 옮긴다.
        /// a와 b를 사용하며, _slots, 적 위치와 자리 바꾸기 상태를 바꾼다.
        /// </summary>
        private void StartSwap(int a, int b)
        {
            StopEnemiesOn(a, b);
            (_slots[a], _slots[b]) = (_slots[b], _slots[a]);
            foreach (Enemy enemy in _enemies)
            {
                if (enemy.Cell == a) enemy.Cell = enemy.From = b;
                else if (enemy.Cell == b) enemy.Cell = enemy.From = a;
            }
            _swapA = a;
            _swapB = b;
            _swapT = 0f;
        }

        /// <summary>
        /// 칸 a나 b를 출발하거나 그쪽으로 걷던 적을, 더 가까운 칸에 멈춰 세운다. 멈춘 칸에 바위가 있으면 바위를 부순다.
        /// a, b와 적들의 걷기 진행도를 사용하며, 적의 칸, 걷기 진행도, 쉬는 시간과 _rocks, _enemies를 바꾼다.
        /// </summary>
        private void StopEnemiesOn(int a, int b)
        {
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = _enemies[i];
                if (enemy.MoveT >= 1f) continue;
                if (enemy.Cell != a && enemy.Cell != b && enemy.From != a && enemy.From != b) continue;
                // 반 넘게 걸었으면 도착 칸에, 아니면 출발 칸에 세운다. 세운 칸의 조각과 함께 움직인다.
                int cell = enemy.MoveT >= 0.5f ? enemy.Cell : enemy.From;
                enemy.Cell = enemy.From = cell;
                enemy.MoveT = 1f;
                enemy.WaitT = _enemyPauseSeconds;
                if (_rocks.ContainsKey(cell)) BreakRock(enemy);
            }
        }

        /// <summary>
        /// cell의 조각을 시계 방향으로 90도 돌리고 도는 연출을 시작한다. 열린 쪽은 즉시 한 칸씩 돌려 둔다.
        /// cell을 사용하며, 조각의 Mask, Turns와 회전 상태를 바꾼다.
        /// </summary>
        private void StartRotate(int cell)
        {
            StopEnemiesOn(cell, cell);
            Tile tile = _slots[cell];
            // 위→오른쪽→아래→왼쪽 순서로 비트를 한 칸씩 민다.
            tile.Mask = ((tile.Mask << 1) | (tile.Mask >> (DIR_COUNT - 1))) & ((1 << DIR_COUNT) - 1);
            tile.Turns++;
            _rotateCell = cell;
            _rotateT = 0f;
        }

        /// <summary>
        /// 도는 조각을 한 프레임 돌리고, 끝나면 이어진 길을 다시 계산한다.
        /// dt와 _rotateSeconds를 사용하며, _rotateT와 조각 오브젝트 회전을 바꾼다.
        /// </summary>
        private void AdvanceRotate(float dt)
        {
            _rotateT = Mathf.MoveTowards(_rotateT, 1f, dt / _rotateSeconds);
            Tile tile = _slots[_rotateCell];
            float e = Mathf.SmoothStep(0f, 1f, _rotateT);
            // 시계 방향은 z축 음의 방향이다.
            float angle = -90f * Mathf.Lerp(tile.Turns - 1, tile.Turns, e);
            tile.Root.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (_rotateT < 1f) return;
            _rotateCell = -1;
            Refresh();
        }

        /// <summary>
        /// 자리를 바꾸는 두 조각을 한 프레임 미끄러지게 하고, 끝나면 이어진 길을 다시 계산한다.
        /// dt와 _swapSeconds를 사용하며, _swapT와 조각 오브젝트 위치를 바꾼다.
        /// </summary>
        private void AdvanceSwap(float dt)
        {
            _swapT = Mathf.MoveTowards(_swapT, 1f, dt / _swapSeconds);
            float e = Mathf.SmoothStep(0f, 1f, _swapT);
            _slots[_swapA].Root.localPosition = Vector3.Lerp(CellPos(_swapB), CellPos(_swapA), e);
            _slots[_swapB].Root.localPosition = Vector3.Lerp(CellPos(_swapA), CellPos(_swapB), e);
            PlaceEnemies();
            if (_swapT < 1f) return;
            _swapA = _swapB = -1;
            Refresh();
        }

        /// <summary>
        /// 말을 한 프레임 걷게 하고, 도착하면 단서인지 확인한 뒤 저장한 방향이나 누르고 있는 WASD 방향으로 이어 간다.
        /// kb와 dt를 사용하며, 말 위치, 걷기 진행도, 저장한 방향과 성공 이벤트를 바꾼다.
        /// </summary>
        private void HandleWalk(Keyboard kb, float dt)
        {
            // 걷는 중에 누른 방향도 빠지지 않도록 가장 최근 것 하나를 저장한다.
            if (ReadWalk(kb, false, out int pressedDr, out int pressedDc))
            {
                _queuedDr = pressedDr;
                _queuedDc = pressedDc;
            }

            if (_moveT < 1f)
            {
                _moveT = Mathf.MoveTowards(_moveT, 1f, dt / _moveSeconds);
                PlaceWalker();
                if (_moveT < 1f) return;
                _walkFrom = _walker;
                if (_walker == _clue)
                {
                    Close();
                    Solved?.Invoke();
                    return;
                }
                Refresh();
            }

            int dr = _queuedDr, dc = _queuedDc;
            _queuedDr = _queuedDc = 0;
            if (dr == 0 && dc == 0 && !ReadWalk(kb, true, out dr, out dc)) return;
            int dir = dr > 0 ? 0 : dc > 0 ? 1 : dr < 0 ? 2 : 3;
            if (!IsConnected(_walker, dir) || _rocks.ContainsKey(Neighbor(_walker, dir))) return;

            _walkFrom = _walker;
            _walker = Neighbor(_walker, dir);
            _moveT = 0f;
            if (_picked == _walker) _picked = -1;
            Refresh();
        }

        /// <summary>
        /// WASD 한 방향을 읽는다. held가 true면 누르고 있는 키, false면 이번 프레임에 누른 키를 본다.
        /// kb와 held를 사용하며, 방향이 있으면 dr·dc에 담고 true를 반환한다.
        /// </summary>
        private static bool ReadWalk(Keyboard kb, bool held, out int dr, out int dc)
        {
            dr = 0;
            dc = 0;
            if (IsDown(kb.wKey, held)) dr = 1;
            else if (IsDown(kb.sKey, held)) dr = -1;
            else if (IsDown(kb.dKey, held)) dc = 1;
            else if (IsDown(kb.aKey, held)) dc = -1;
            else return false;
            return true;
        }

        /// <summary>
        /// 키가 눌렸는지 확인한다.
        /// key와 held를 사용하며, held면 누르고 있는지, 아니면 이번 프레임에 눌렀는지를 반환한다.
        /// </summary>
        private static bool IsDown(KeyControl key, bool held)
        {
            return held ? key.isPressed : key.wasPressedThisFrame;
        }

        /// <summary>
        /// 칸 번호를 퍼즐판 로컬 좌표(칸 가운데)로 바꾼다.
        /// cell과 _boardSize를 사용하며, 로컬 위치를 반환한다.
        /// </summary>
        private Vector3 CellPos(int cell)
        {
            float half = _boardSize * 0.5f;
            return new Vector3((cell % _size + 0.5f) * CellSize - half, (cell / _size + 0.5f) * CellSize - half, 0f);
        }

        /// <summary>
        /// 원판, 조각, 시작·단서 표시, 선택 틀, 말을 새로 만든다. 조각은 정답 배치 순서로 _slots에 넣는다.
        /// masks와 모양 설정을 사용하며, _drawRoot 아래 그리기 오브젝트와 _slots를 바꾼다.
        /// </summary>
        private void BuildDrawing(int[] masks)
        {
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _drawRoot = new GameObject("퍼즐판").transform;
            _drawRoot.SetParent(transform, false);

            LineRenderer backdrop = CreateLine("원판", _drawRoot, 0);
            Shapes.SetBlob(backdrop, _backdropDiameter);
            Shapes.SetColor(backdrop, _backdropColor);

            float half = CellSize * 0.5f;
            float inset = half * 0.94f;
            _slots = new Tile[CellCount];
            for (int cell = 0; cell < CellCount; cell++)
            {
                var tile = new Tile { Mask = masks[cell] };
                tile.Root = new GameObject("조각").transform;
                tile.Root.SetParent(_drawRoot, false);
                tile.Frame = CreateLine("테두리", tile.Root, 1);
                Shapes.SetPoints(tile.Frame, Square(inset), true);
                tile.Frame.widthMultiplier = _frameWidth;
                for (int d = 0; d < DIR_COUNT; d++)
                {
                    if ((tile.Mask & (1 << d)) == 0) continue;
                    LineRenderer arm = CreateLine("길", tile.Root, 2);
                    Shapes.SetPoints(arm, new[] { Vector3.zero, new Vector3(DIR_DC[d], DIR_DR[d], 0f) * half }, false);
                    arm.widthMultiplier = _pathWidth;
                    tile.Arms.Add(arm);
                }
                _slots[cell] = tile;
            }

            // 시작과 단서 조각은 섞지 않으므로 표시를 그 조각에 붙인다.
            LineRenderer startDot = CreateLine("시작", _slots[_start].Root, 3);
            Shapes.SetBlob(startDot, _pathWidth * 2.2f);
            Shapes.SetColor(startDot, _linkedColor);
            LineRenderer clueRing = CreateLine("단서", _slots[_clue].Root, 3);
            Shapes.SetCircle(clueRing, _pathWidth * 1.6f, CIRCLE_SEGMENTS);
            clueRing.widthMultiplier = _frameWidth * 2f;
            Shapes.SetColor(clueRing, _cursorColor);

            _cursorFrame = CreateLine("선택", _drawRoot, 4);
            Shapes.SetPoints(_cursorFrame, Square(inset * 0.9f), true);
            _cursorFrame.widthMultiplier = _frameWidth * 2f;

            _walkerDot = CreateLine("말", _drawRoot, 5);
            Shapes.SetBlob(_walkerDot, _walkerSize);
            Shapes.SetColor(_walkerDot, _walkerColor);
        }

        /// <summary>
        /// 시작 칸에서 먼 칸 중 시작·단서 칸을 뺀 곳에 적을 _enemyCount명 겹치지 않게 놓고 그린다.
        /// rng와 적 설정을 사용하며, _enemies와 적 그리기 오브젝트를 바꾼다.
        /// </summary>
        private void SpawnEnemies(System.Random rng)
        {
            var cells = new List<int>();
            for (int cell = 0; cell < CellCount; cell++)
                if (cell != _start && cell != _clue && !_rocks.ContainsKey(cell)) cells.Add(cell);
            // 시작 칸에서 판 한 변 이상 떨어진 칸 중에서 무작위로 고른다. 그런 칸이 모자라면 가까운 칸도 쓴다.
            ShuffleList(cells, rng);
            var far = cells.FindAll(c => Distance(c) >= _size - 1);
            if (far.Count >= _enemyCount) cells = far;
            int count = Mathf.Min(_enemyCount, cells.Count);
            float r = _enemySize * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var enemy = new Enemy { Cell = cells[i], From = cells[i], WaitT = _enemyStartDelay };
                enemy.Dot = CreateLine("적", _drawRoot, 5);
                // 적은 말과 구별되게 마름모로 그린다.
                Shapes.SetPoints(enemy.Dot, new[] { new Vector3(0f, r, 0f), new Vector3(r, 0f, 0f), new Vector3(0f, -r, 0f), new Vector3(-r, 0f, 0f) }, true);
                enemy.Dot.widthMultiplier = _frameWidth * 3f;
                Shapes.SetColor(enemy.Dot, _enemyColor);
                _enemies.Add(enemy);
            }
        }

        /// <summary>
        /// 정답 경로 가운데쯤의 칸에 바위를 놓고 그린다. 바위 칸 조각은 정답 방향 그대로 고정된다.
        /// route, rng와 바위 설정을 사용하며, _rocks와 바위 그리기 오브젝트를 바꾼다.
        /// </summary>
        private void PlaceRocks(List<int> route, System.Random rng)
        {
            // 시작·단서 바로 옆은 피하고, 경로가 짧으면 시작·단서만 뺀다.
            var cells = new List<int>();
            for (int i = 2; i < route.Count - 2; i++) cells.Add(route[i]);
            if (cells.Count == 0) for (int i = 1; i < route.Count - 1; i++) cells.Add(route[i]);
            ShuffleList(cells, rng);
            int count = Mathf.Min(Mathf.Min(_rockCount, _enemyCount), cells.Count);
            float side = CellSize * _rockSize;
            for (int i = 0; i < count; i++)
            {
                LineRenderer rock = CreateLine("바위", _drawRoot, 3);
                Shapes.SetFilledRect(rock, side, side);
                Shapes.SetColor(rock, _rockColor);
                rock.transform.localPosition = CellPos(cells[i]);
                _rocks.Add(cells[i], rock);
            }
        }

        /// <summary>
        /// enemy가 들어간 칸의 바위를 부수고, 부딪힌 적도 함께 없앤다.
        /// enemy와 _rocks를 사용하며, _rocks, _enemies와 그리기 오브젝트를 바꾼다.
        /// </summary>
        private void BreakRock(Enemy enemy)
        {
            Destroy(_rocks[enemy.Cell].gameObject);
            _rocks.Remove(enemy.Cell);
            Destroy(enemy.Dot.gameObject);
            _enemies.Remove(enemy);
        }

        /// <summary>
        /// 시작 칸에서 cell까지의 가로·세로 칸 수 합을 구한다.
        /// cell과 _start를 사용하며, 거리를 반환한다.
        /// </summary>
        private int Distance(int cell)
        {
            return Mathf.Abs(cell % _size - _start % _size) + Mathf.Abs(cell / _size - _start / _size);
        }

        /// <summary>
        /// 가운데가 원점인 정사각형 꼭짓점을 만든다.
        /// half(한 변의 절반)를 사용하며, 네 꼭짓점 배열을 반환한다.
        /// </summary>
        private static Vector3[] Square(float half)
        {
            return new[]
            {
                new Vector3(-half, -half, 0f), new Vector3(half, -half, 0f),
                new Vector3(half, half, 0f), new Vector3(-half, half, 0f),
            };
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
        /// 이어진 길을 다시 계산하고 조각 색, 선택 틀, 말 위치를 지금 상태에 맞게 다시 그린다.
        /// _slots, _linked, _cursor, _picked를 사용하며, 그리기 오브젝트의 색과 위치를 바꾼다.
        /// </summary>
        private void Refresh()
        {
            if (_drawRoot == null) return;
            UpdateLinked(true);
            for (int cell = 0; cell < CellCount; cell++)
            {
                Tile tile = _slots[cell];
                Color frame = cell == _picked ? _pickedColor : _frameColor;
                if (IsLocked(cell) && cell != _picked) frame.a *= 0.5f;
                Shapes.SetColor(tile.Frame, frame);
                foreach (LineRenderer arm in tile.Arms) Shapes.SetColor(arm, _linked[cell] ? _linkedColor : _pathColor);
            }
            _cursorFrame.transform.localPosition = CellPos(_cursor);
            Shapes.SetColor(_cursorFrame, _picked >= 0 ? _pickedColor : _cursorColor);
            PlaceWalker();
            PlaceEnemies();
        }

        /// <summary>
        /// 말을 걷기 진행도에 맞춰 출발 칸과 도착 칸 사이에 놓는다.
        /// _walkFrom, _walker, _moveT를 사용하며, 말 오브젝트 위치를 바꾼다.
        /// </summary>
        private void PlaceWalker()
        {
            if (_walkerDot == null) return;
            _walkerDot.transform.localPosition = Vector3.Lerp(CellPos(_walkFrom), CellPos(_walker), _moveT);
        }

        /// <summary>
        /// 적 하나를 한 프레임 움직인다. 쉬는 시간이 끝나면 이어진 길로 말에게 가는 첫 칸으로 걷고, 길이 없으면 제자리에서 기다린다.
        /// enemy, dt와 적 움직임 설정을 사용하며, enemy의 칸, 걷기 진행도, 쉬는 시간과 적 위치를 바꾼다.
        /// </summary>
        private void AdvanceEnemy(Enemy enemy, float dt)
        {
            if (enemy.MoveT < 1f)
            {
                enemy.MoveT = Mathf.MoveTowards(enemy.MoveT, 1f, dt / _enemyMoveSeconds);
                PlaceEnemies();
                if (enemy.MoveT < 1f) return;
                enemy.From = enemy.Cell;
                enemy.WaitT = _enemyPauseSeconds;
                if (_rocks.ContainsKey(enemy.Cell)) BreakRock(enemy);
                Refresh();
                return;
            }

            enemy.WaitT -= dt;
            if (enemy.WaitT > 0f) return;
            int next = NextStepToward(enemy, _walker);
            if (next < 0) return;
            enemy.From = enemy.Cell;
            enemy.Cell = next;
            enemy.MoveT = 0f;
            Refresh();
        }

        /// <summary>
        /// 이어진 길만 따라 enemy에서 target까지 가는 가장 짧은 길의 첫 칸을 찾는다. 다른 적이 있는 칸은 지나가지 않는다.
        /// enemy, target과 지금 조각 배치, 다른 적 위치를 사용하며, 첫 칸 번호를 반환하고 길이 없으면 -1을 반환한다.
        /// </summary>
        private int NextStepToward(Enemy enemy, int target)
        {
            int from = enemy.Cell;
            if (from == target) return -1;
            var prev = new int[CellCount];
            for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            prev[from] = -1;
            foreach (Enemy other in _enemies)
            {
                if (other == enemy) continue;
                // 다른 적이 서 있거나 들어가는 칸은 막힌 칸으로 본다.
                if (other.Cell != target) prev[other.Cell] = -3;
                if (other.From != target && other.From != from) prev[other.From] = -3;
            }
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                if (cell == target) break;
                for (int d = 0; d < DIR_COUNT; d++)
                {
                    if (!IsConnected(cell, d)) continue;
                    int n = Neighbor(cell, d);
                    if (prev[n] != -2) continue;
                    prev[n] = cell;
                    queue.Enqueue(n);
                }
            }
            if (prev[target] < 0) return -1;
            int step = target;
            while (prev[step] != from) step = prev[step];
            return step;
        }

        /// <summary>
        /// 적들을 화면에 놓는다. 걷는 적은 출발 칸과 도착 칸 사이에, 서 있는 적은 자기 조각 위에 놓아 조각이 미끄러질 때 함께 움직이게 한다.
        /// _enemies와 조각 위치를 사용하며, 적 오브젝트 위치를 바꾼다.
        /// </summary>
        private void PlaceEnemies()
        {
            foreach (Enemy enemy in _enemies)
            {
                if (enemy.Dot == null) continue;
                enemy.Dot.transform.localPosition = enemy.MoveT < 1f
                    ? Vector3.Lerp(CellPos(enemy.From), CellPos(enemy.Cell), enemy.MoveT)
                    : _slots[enemy.Cell].Root.localPosition;
            }
        }

        /// <summary>
        /// 말과 적 중 하나라도 닿았는지 화면 위치로 확인한다. 서로 엇갈려 지나가는 경우도 잡는다.
        /// 말과 적 오브젝트 위치와 칸 크기를 사용하며, 닿았으면 true를 반환한다.
        /// </summary>
        private bool IsCaught()
        {
            if (_walkerDot == null) return false;
            Vector3 walker = _walkerDot.transform.localPosition;
            foreach (Enemy enemy in _enemies)
                if (enemy.Dot != null && Vector3.Distance(walker, enemy.Dot.transform.localPosition) < CellSize * 0.45f) return true;
            return false;
        }
    }
}
