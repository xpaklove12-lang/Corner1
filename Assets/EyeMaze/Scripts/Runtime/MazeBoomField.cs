using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로. 들여다보는 동안 순서표의 무늬대로 미로 칸이 경고한 뒤 터지고, 시선이 터진 칸에 있으면 층 입구로 돌려보낸다.
    /// 미로(IrisMaze) 아래에 두어 미로가 돌면 함께 돈다. 칸 그림은 Play 중 이 오브젝트 아래에 만든다.
    /// 순서표는 한 바퀴 끝나면 처음부터 다시 돌고, 바퀴마다 조금씩 빨라진다.
    /// </summary>
    public class MazeBoomField : MonoBehaviour
    {
        private const string CELL_CONTAINER_NAME = "폭발 칸";
        private const string GRID_CONTAINER_NAME = "격자";
        private const int GRID_CIRCLE_SEGMENTS = 96;
        private const int ARC_SEGMENTS = 8;
        // 칸 상태 배열에서 경고나 폭발이 없는 칸의 값
        private const float NONE = -1f;

        [Header("연결")]
        [SerializeField] private EyeMazeDirector _director;
        [Tooltip("미로를 그리는 카메라. 격자선을 화면에서 최소 몇 픽셀 굵기로 그릴지 계산하는 데 쓴다")]
        [SerializeField] private Camera _camera;

        [Header("진행")]
        [Tooltip("켜면 폭발 미로를 쓰는 동안 눈이 깜빡이지 않아 미로가 돌지 않는다")]
        [SerializeField] private bool _pauseBlink = true;

        [Header("이동")]
        [Tooltip("폭발 미로를 쓰는 동안의 칸 단위 이동 설정. 디렉터 Inspector의 같은 설정 대신 이 값을 쓴다")]
        [SerializeField] private GridMoveSettings _move = new GridMoveSettings(true, GridControl.Ring);
        [Tooltip("순서표가 한 바퀴 끝난 뒤 다시 돌기까지 쉬는 시간(초)")]
        [SerializeField, Min(0f)] private float _loopGapSeconds = 1f;
        [Tooltip("한 바퀴 돌 때마다 더해지는 배속")]
        [SerializeField, Min(0f)] private float _speedUpPerLoop = 0.1f;
        [Tooltip("가장 빠를 때의 배속")]
        [SerializeField, Min(1f)] private float _maxSpeed = 1.6f;
        [Tooltip("폭발 순서표. 시작 시각은 한 바퀴 안에서의 시각이다")]
        [SerializeField] private List<BoomWave> _waves = CreateDefaultWaves();

        [Header("휩쓸림")]
        [Tooltip("켜면 가장 최근에 밟은 안전지대로 돌아간다. 끄면 언제나 시작 칸으로 돌아간다")]
        [SerializeField] private bool _respawnAtLastSafe = true;
        [Tooltip("돌아간 뒤 다시 휩쓸리지 않는 시간(초)")]
        [SerializeField, Min(0f)] private float _hitGraceSeconds = 1f;
        [Tooltip("공용 문구 파일에 msg.boom_hit가 없을 때 쓰는 글. {hits} = 휩쓸린 횟수")]
        [SerializeField, TextArea(1, 2)] private string _hitMessage = "폭발에 휩쓸렸다. ({hits}번째)";

        [Header("안전지대")]
        // 안전지대 칸은 이 오브젝트 아래의 BoomSafeZone 오브젝트로 놓는다. 씬 뷰에서 끌어 옮긴다.
        [Tooltip("켜면 휩쓸렸을 때 돌아가는 칸(시작 칸 또는 마지막 안전지대)도 터지지 않는다")]
        [SerializeField] private bool _checkpointSafe = true;
        [Tooltip("안전지대 칸의 색")]
        [SerializeField] private Color _safeColor = new Color(0.35f, 0.95f, 0.75f, 0.22f);

        [Header("그림")]
        [Tooltip("경고가 막 뜰 때의 색. 검은 홍채 위에서 겨우 보이는 어두운 붉은빛으로 시작한다")]
        [SerializeField] private Color _warnColor = new Color(0.35f, 0.08f, 0.05f, 0.1f);
        [Tooltip("터지기 직전의 경고 색. 미로가 덮여 보이지 않도록 짙은 붉은빛까지만 올린다")]
        [SerializeField] private Color _warnEndColor = new Color(0.6f, 0.12f, 0.06f, 0.45f);
        [Tooltip("경고 색이 바뀌는 빠르기 곡선. 1이면 고르게, 클수록 처음엔 천천히 달아오르다 터지기 직전에 확 밝아진다")]
        [SerializeField, Range(1f, 4f)] private float _warnEase = 2f;
        [Tooltip("터지는 순간의 색")]
        [SerializeField] private Color _burstColor = new Color(1f, 0.62f, 0.18f, 1f);
        [Tooltip("터진 뒤 가라앉으며 바뀌는 색. 투명도 0이면 검은 배경으로 꺼지듯 사라진다")]
        [SerializeField] private Color _burstEndColor = new Color(0.9f, 0.1f, 0.05f, 0f);
        [Tooltip("터지는 순간 칸이 부풀어 오르는 배율. 칸 폭을 넘지 않는다")]
        [SerializeField, Min(1f)] private float _popScale = 1.16f;
        [Tooltip("폭발 시간 중 부풀었다 돌아오는 앞부분의 비율. 나머지 동안 색이 가라앉는다")]
        [SerializeField, Range(0.05f, 1f)] private float _popPortion = 0.35f;
        [Tooltip("칸 폭 대비 채우는 비율")]
        [SerializeField, Range(0.1f, 1f)] private float _fillScale = 0.86f;
        [Tooltip("칸 그림의 정렬 순서. 격자선과 벽(5)보다 낮게 두어 칸 위로 격자와 벽이 보이게 한다")]
        [SerializeField] private int _sortingOrder = 3;

        [Header("격자")]
        [Tooltip("켜면 들여다보는 동안 칸을 나누는 격자선을 그린다")]
        [SerializeField] private bool _showGrid = true;
        [Tooltip("격자선 색. 흰 벽보다 훨씬 어두운 반투명 회색으로 두어 벽이 먼저 보이게 한다")]
        [SerializeField] private Color _gridColor = new Color(0.5f, 0.5f, 0.5f, 0.22f);
        [Tooltip("격자선 굵기. 픽셀 카메라에서 한 픽셀(이 씬은 약 0.05)보다 가늘면 끊기거나 사라진다")]
        [SerializeField, Min(0.001f)] private float _gridWidth = 0.03f;
        [Tooltip("격자선이 화면에서 적어도 이 픽셀 수만큼 굵게 보이도록 한다. 굵기는 늘 픽셀 정수배로 맞춘다. 벽(약 1픽셀)보다 덜 보이도록 1로 둔다")]
        [SerializeField, Min(1)] private int _gridMinPixels = 1;
        [Tooltip("격자선의 정렬 순서. 칸 그림보다 높고 벽(5)보다 낮게 두어, 칸이 터져도 격자가 보이고 벽은 격자 위에 보인다")]
        [SerializeField] private int _gridSortingOrder = 4;

        private LineRenderer[,] _cells;
        private int _cellsKey = int.MinValue;
        private float _cellWidth;
        // 칸마다 경고 진행(0~1)과 폭발 뒤 남은 비율(1~0). 없으면 NONE이다.
        private float[,] _warn;
        // 고리 경계 원과 칸 경계 선
        private readonly List<LineRenderer> _gridLines = new List<LineRenderer>();
        private float[,] _burst;
        private bool _running;
        private float _time;
        private int _loop;
        private float _graceT;
        private int _hits;
        private int _checkpointRing;
        private int _checkpointSector;
        // 칸마다 안전지대 오브젝트가 놓였는지. 매 프레임 다시 모아 Play 중에 옮겨도 바로 반영한다.
        private bool[,] _safeMap;

        /// <summary>이번 판에서 휩쓸린 횟수</summary>
        public int Hits => _hits;

        private IrisMaze Maze => _director.Maze;

        // 깜빡임, 읽기, 회상, 창, 탈출 뒤에는 무늬 시간이 멈춘다.
        private bool IsPaused => !_director.AcceptInput || _director.IsReading || _director.IsBlinking
                                 || FlashbackPlayer.IsPlaying || DialogueLog.BlocksInput || FragmentRecapUI.BlocksInput;

        void Update()
        {
            _director.BlinkPaused = _pauseBlink;
            _director.GridOverride = _move;
            if (_director.CurrentMode != EyeMazeDirector.Mode.Gaze)
            {
                if (_running) StopRun();
                return;
            }
            if (!_running) BeginRun();

            CollectSafeZones();
            TrackCheckpoint();
            if (!IsPaused) Advance(Time.deltaTime);
            EvaluateCells();
            DrawCells();
            DrawGrid();
            if (!IsPaused) CheckHit();
        }

        void OnDisable()
        {
            // 씬을 닫는 중이면 디렉터가 먼저 사라질 수 있다.
            if (_director != null)
            {
                _director.BlinkPaused = false;
                _director.GridOverride = null;
            }
            if (_running) StopRun();
        }

        /// <summary>
        /// 기본 폭발 순서표를 만든다. 체크 무늬, 도는 나선, 흩뿌림, 빠른 체크 무늬 순서로 이어진다.
        /// 입력값은 없으며, 새 무늬 목록을 반환한다.
        /// </summary>
        private static List<BoomWave> CreateDefaultWaves()
        {
            return new List<BoomWave>
            {
                new BoomWave("체크 무늬", BoomShape.Checker, 1f, 4, 1.8f, 1.4f, 0.4f),
                new BoomWave("도는 나선", BoomShape.Spiral, 8.6f, 8, 0.55f, 1.2f, 0.4f, sectorStep: 1, arms: 2, twist: 1),
                new BoomWave("흩뿌림", BoomShape.Scatter, 14.6f, 4, 1.5f, 1.4f, 0.4f, density: 0.3f, seed: 1),
                new BoomWave("빠른 체크 무늬", BoomShape.Checker, 21.3f, 6, 1.2f, 1f, 0.35f)
            };
        }

        /// <summary>
        /// 들여다보기가 시작될 때 순서표를 처음부터 돌리고, 지금 시선 칸을 첫 입구로 정한다.
        /// 시선 위치를 사용하며, _running, _time, _loop, _hits, _graceT, 입구 칸과 칸 그림을 바꾼다.
        /// </summary>
        private void BeginRun()
        {
            _running = true;
            _time = 0f;
            _loop = 0;
            _hits = 0;
            _graceT = 0f;
            GazeCursor cursor = _director.Cursor;
            SetCheckpoint(cursor.Ring, cursor.Sector);
            EnsureCells();
        }

        /// <summary>
        /// 들여다보기가 끝나면 순서표를 멈추고 칸 그림을 모두 숨긴다.
        /// 입력값은 없으며, _running과 칸 그림의 표시 상태를 바꾼다.
        /// </summary>
        private void StopRun()
        {
            _running = false;
            if (_cells == null) return;
            foreach (LineRenderer lr in _cells) if (lr != null) lr.enabled = false;
            foreach (LineRenderer lr in _gridLines) if (lr != null) lr.enabled = false;
        }

        /// <summary>
        /// 휩쓸렸을 때 돌아갈 칸을 정한다.
        /// ring, sector(미로 기준)를 사용하며, _checkpointRing과 _checkpointSector를 바꾼다.
        /// </summary>
        private void SetCheckpoint(int ring, int sector)
        {
            _checkpointRing = ring;
            _checkpointSector = sector;
        }

        /// <summary>
        /// 시선이 안전지대 칸에 서면 그 칸을 돌아갈 칸으로 정한다. _respawnAtLastSafe가 꺼져 있으면 시작 칸을 유지한다.
        /// 시선 위치와 안전지대 배치를 사용하며, 돌아갈 칸을 바꾼다.
        /// </summary>
        private void TrackCheckpoint()
        {
            if (!_respawnAtLastSafe || _director.InAbyss) return;
            GazeCursor cursor = _director.Cursor;
            if (IsSafeZone(cursor.Ring, cursor.Sector)) SetCheckpoint(cursor.Ring, cursor.Sector);
        }

        /// <summary>
        /// 무늬 시간을 배속만큼 진행하고, 한 바퀴가 끝나면 처음으로 돌리며 바퀴 수를 올린다. 휩쓸림 유예 시간도 줄인다.
        /// dt, _loop, _speedUpPerLoop, _maxSpeed, 순서표 길이를 사용하며, _time, _loop, _graceT를 바꾼다.
        /// </summary>
        private void Advance(float dt)
        {
            float speed = Mathf.Min(_maxSpeed, 1f + _loop * _speedUpPerLoop);
            _time += dt * speed;
            float length = LoopLength();
            if (length > 0f && _time >= length)
            {
                _time -= length;
                _loop++;
            }
            if (_graceT > 0f) _graceT -= dt;
        }

        /// <summary>
        /// 순서표 한 바퀴의 길이를 계산한다. 가장 늦게 끝나는 무늬에 쉬는 시간을 더한다.
        /// _waves와 _loopGapSeconds를 사용하며, 초 단위 길이를 반환한다.
        /// </summary>
        private float LoopLength()
        {
            float end = 0f;
            foreach (BoomWave w in _waves) if (w != null) end = Mathf.Max(end, w.End);
            return end > 0f ? end + _loopGapSeconds : 0f;
        }

        /// <summary>
        /// 지금 무늬 시간에서 칸마다 경고 진행과 폭발 뒤 남은 비율을 계산한다. 여러 무늬가 겹치면 큰 값을 쓴다.
        /// _time, _waves, 미로의 고리·칸 수를 사용하며, _warn과 _burst를 바꾼다.
        /// </summary>
        private void EvaluateCells()
        {
            int rings = Maze.Rings;
            int sectors = Maze.Sectors;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sectors; s++) { _warn[r, s] = NONE; _burst[r, s] = NONE; }

            foreach (BoomWave w in _waves)
            {
                if (w == null) continue;
                for (int k = 0; k < w.Repeat; k++)
                {
                    float local = _time - (w.Start + k * w.Interval);
                    if (local < 0f || local >= w.WarnSeconds + w.BurstSeconds) continue;
                    bool bursting = local >= w.WarnSeconds;
                    // 경고는 0에서 1로 차오르고, 폭발은 1에서 0으로 가라앉는다.
                    float value = bursting ? 1f - (local - w.WarnSeconds) / w.BurstSeconds : local / w.WarnSeconds;
                    float[,] target = bursting ? _burst : _warn;
                    for (int r = 0; r < rings; r++)
                        for (int s = 0; s < sectors; s++)
                            if (!IsSafe(r, s) && w.Covers(r, s, k, rings, sectors) && value > target[r, s]) target[r, s] = value;
                }
            }
        }

        /// <summary>
        /// 칸 상태에 맞춰 칸 그림을 켜고 색을 칠한다. 폭발이 경고보다 먼저 보인다.
        /// _warn, _burst, 그림 색 설정, 미로의 모드 투명도를 사용하며, 칸 LineRenderer의 표시 상태와 색을 바꾼다.
        /// </summary>
        private void DrawCells()
        {
            float modeAlpha = Maze.WallAlpha;
            for (int r = 0; r < Maze.Rings; r++)
            {
                for (int s = 0; s < Maze.Sectors; s++)
                {
                    LineRenderer lr = _cells[r, s];
                    float burst = _burst[r, s];
                    float warn = _warn[r, s];
                    bool safe = IsSafe(r, s);
                    if (burst < 0f && warn < 0f && !safe) { if (lr.enabled) lr.enabled = false; continue; }

                    Color c;
                    float scale = 1f;
                    if (safe) c = _safeColor;
                    else if (burst >= 0f)
                    {
                        // 앞부분에 하얗게 부풀었다 돌아오고, 나머지 동안 불씨 색으로 꺼지듯 가라앉는다.
                        float p = 1f - burst;
                        if (p < _popPortion)
                        {
                            c = _burstColor;
                            scale = 1f + (_popScale - 1f) * Mathf.Sin(p / _popPortion * Mathf.PI);
                        }
                        else c = Color.Lerp(_burstColor, _burstEndColor, (p - _popPortion) / Mathf.Max(0.01f, 1f - _popPortion));
                    }
                    else
                    {
                        // 깜빡이지 않고 어두운 불씨에서 금빛으로 천천히 달아오른다.
                        c = Color.Lerp(_warnColor, _warnEndColor, Mathf.Pow(warn, _warnEase));
                    }
                    c.a *= modeAlpha;
                    Shapes.SetColor(lr, c);
                    // 부풀어도 칸 폭을 넘지 않아 옆 칸을 덮지 않는다.
                    lr.widthMultiplier = Mathf.Min(_cellWidth * scale, Maze.RingWidth);
                    if (!lr.enabled) lr.enabled = true;
                }
            }
        }

        /// <summary>
        /// 격자선을 켜고 끄고, 미로의 모드 투명도를 곱한 격자 색으로 칠한다.
        /// _showGrid, _gridColor, 미로의 모드 투명도를 사용하며, 격자 LineRenderer의 표시 상태와 색을 바꾼다.
        /// </summary>
        private void DrawGrid()
        {
            Color c = _gridColor;
            c.a *= Maze.WallAlpha;
            float width = GridLineWidth();
            foreach (LineRenderer lr in _gridLines)
            {
                if (lr.enabled != _showGrid) lr.enabled = _showGrid;
                if (!_showGrid) continue;
                Shapes.SetColor(lr, c);
                lr.widthMultiplier = width;
            }
        }

        /// <summary>
        /// 격자선 굵기를 계산한다. 카메라가 그리는 화면(픽셀 카메라면 낮은 해상도 텍스처)에서 한 픽셀이 미로 기준으로 얼마인지 구해,
        /// _gridMinPixels픽셀보다 가늘지 않게 하고 픽셀 정수배로 맞춘다.
        /// _gridWidth, _gridMinPixels, _camera의 직교 크기와 그리는 높이, 미로의 크기 배율을 사용하며, 미로 기준 굵기를 반환한다.
        /// </summary>
        private float GridLineWidth()
        {
            int pixels = _camera.targetTexture != null ? _camera.targetTexture.height : _camera.pixelHeight;
            float worldPerPixel = 2f * _camera.orthographicSize / Mathf.Max(1, pixels);
            float localPerPixel = worldPerPixel / Mathf.Max(1e-4f, Mathf.Abs(Maze.transform.lossyScale.y));
            // 픽셀 정수배로 맞춰 화면 크기나 확대가 바뀌어도 선마다 굵기가 덜 들쭉날쭉하게 한다.
            int px = Mathf.Max(_gridMinPixels, Mathf.RoundToInt(_gridWidth / localPerPixel));
            return px * localPerPixel;
        }

        /// <summary>
        /// 칸이 터지지 않는 칸인지 판단한다. 안전지대이거나, _checkpointSafe가 켜져 있으면 돌아갈 칸도 포함한다.
        /// ring, sector(미로 기준)와 돌아갈 칸을 사용하며, 터지지 않으면 true를 반환한다.
        /// </summary>
        private bool IsSafe(int ring, int sector)
        {
            if (_checkpointSafe && ring == _checkpointRing && sector == _checkpointSector) return true;
            return IsSafeZone(ring, sector);
        }

        /// <summary>
        /// 칸에 안전지대 오브젝트가 놓였는지 판단한다.
        /// ring, sector(미로 기준)와 _safeMap을 사용하며, 안전지대면 true를 반환한다.
        /// </summary>
        private bool IsSafeZone(int ring, int sector) => _safeMap[ring, sector];

        /// <summary>
        /// 이 오브젝트 아래의 켜진 안전지대 오브젝트를 모아 칸마다 안전지대인지 기록한다.
        /// 자식 BoomSafeZone의 고리와 칸 번호를 사용하며, _safeMap을 바꾼다.
        /// </summary>
        private void CollectSafeZones()
        {
            Array.Clear(_safeMap, 0, _safeMap.Length);
            foreach (BoomSafeZone z in GetComponentsInChildren<BoomSafeZone>())
                _safeMap[Mathf.Clamp(z.Ring, 0, Maze.Rings - 1), Maze.Mod(z.Sector)] = true;
        }

        /// <summary>
        /// 시선이 터진 칸에 있으면 휩쓸림을 처리한다. 유예 중이거나 동공 안이면 넘어간다.
        /// 시선 위치, _burst, _graceT를 사용하며, 휩쓸리면 Hit을 부른다.
        /// </summary>
        private void CheckHit()
        {
            if (_graceT > 0f || _director.InAbyss) return;
            GazeCursor cursor = _director.Cursor;
            if (_burst[cursor.Ring, cursor.Sector] >= 0f) Hit();
        }

        /// <summary>
        /// 휩쓸린 횟수를 올리고 시선을 입구 칸으로 돌려보낸 뒤 안내를 띄운다.
        /// 입구 칸, _hitGraceSeconds, _hitMessage를 사용하며, _hits, _graceT, 시선 위치, 디렉터 문구를 바꾼다.
        /// </summary>
        private void Hit()
        {
            _hits++;
            _graceT = _hitGraceSeconds;
            _director.WarpGaze(_checkpointRing, _checkpointSector);
            _director.Message = Strings.Get("msg.boom_hit", _hitMessage, ("hits", _hits));
        }

        /// <summary>
        /// 칸마다 원호 그림을 만든다. 미로 모양이 바뀌었으면 옛 그림을 지우고 다시 만든다.
        /// 미로의 고리·칸 수, 반지름, 선 재질과 _fillScale, _sortingOrder를 사용하며, _cells, _warn, _burst와 자식 오브젝트를 바꾼다.
        /// </summary>
        private void EnsureCells()
        {
            IrisMaze maze = Maze;
            int key = HashCode.Combine(maze.GeometryKey, _fillScale);
            if (_cells != null && key == _cellsKey) return;
            _cellsKey = key;

            Transform old = transform.Find(CELL_CONTAINER_NAME);
            if (old != null) Destroy(old.gameObject);
            Transform container = new GameObject(CELL_CONTAINER_NAME).transform;
            container.SetParent(transform, false);

            int rings = maze.Rings;
            int sectors = maze.Sectors;
            _cells = new LineRenderer[rings, sectors];
            _warn = new float[rings, sectors];
            _burst = new float[rings, sectors];
            _safeMap = new bool[rings, sectors];
            float width = maze.RingWidth * _fillScale;
            _cellWidth = width;
            for (int r = 0; r < rings; r++)
            {
                float mid = maze.RingMid(r);
                // 이웃 칸과 붙지 않도록 고리 사이 틈과 같은 길이만큼 양끝을 줄인다.
                float insetDeg = (maze.RingWidth - width) * 0.5f / mid * Mathf.Rad2Deg;
                for (int s = 0; s < sectors; s++)
                {
                    var go = new GameObject($"칸 {r}-{s}");
                    go.transform.SetParent(container, false);
                    LineRenderer lr = Shapes.Ensure(go, maze.LineMaterial, 0);
                    Shapes.SetArc(lr, mid, s * maze.SectorDeg + insetDeg, (s + 1) * maze.SectorDeg - insetDeg, ARC_SEGMENTS);
                    lr.widthMultiplier = width;
                    lr.sortingOrder = _sortingOrder;
                    lr.enabled = false;
                    _cells[r, s] = lr;
                }
            }
            BuildGrid(maze);
        }

        /// <summary>
        /// 고리 경계마다 원을, 칸 경계마다 안쪽에서 바깥까지 이어지는 선을 만든다. 옛 격자는 지운다.
        /// maze의 고리·칸 수와 반지름, _gridWidth, _gridSortingOrder를 사용하며, _gridLines와 자식 오브젝트를 바꾼다.
        /// </summary>
        private void BuildGrid(IrisMaze maze)
        {
            Transform old = transform.Find(GRID_CONTAINER_NAME);
            if (old != null) Destroy(old.gameObject);
            _gridLines.Clear();
            Transform container = new GameObject(GRID_CONTAINER_NAME).transform;
            container.SetParent(transform, false);

            // 가장 바깥 원부터 동공 둘레까지 고리 경계를 그린다.
            for (int r = 0; r <= maze.Rings; r++)
            {
                LineRenderer lr = MakeGridLine(container, $"원 {r}", maze);
                Shapes.SetCircle(lr, r < maze.Rings ? maze.RingOuter(r) : maze.RingInner(maze.Rings - 1), GRID_CIRCLE_SEGMENTS);
            }
            for (int s = 0; s < maze.Sectors; s++)
            {
                LineRenderer lr = MakeGridLine(container, $"선 {s}", maze);
                float deg = s * maze.SectorDeg;
                Shapes.SetPoints(lr, new[] { Shapes.Polar(maze.InnerRadius, deg), Shapes.Polar(maze.OuterRadius, deg) }, false);
            }
        }

        /// <summary>
        /// 격자선 하나를 만들어 목록에 넣는다. 처음에는 꺼 둔다.
        /// container, name, maze의 선 재질, _gridWidth, _gridSortingOrder를 사용하며, 만든 LineRenderer를 반환하고 _gridLines를 바꾼다.
        /// </summary>
        private LineRenderer MakeGridLine(Transform container, string name, IrisMaze maze)
        {
            var go = new GameObject(name);
            go.transform.SetParent(container, false);
            LineRenderer lr = Shapes.Ensure(go, maze.LineMaterial, 0);
            lr.widthMultiplier = _gridWidth;
            lr.sortingOrder = _gridSortingOrder;
            lr.enabled = false;
            _gridLines.Add(lr);
            return lr;
        }
    }
}
