using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 파편을 열 때 점 사이 칸마다 놓인 V자 조각을 돌려, 모든 조각을 하나의 선 그물로 이으면 파편을 주는 선 잇기 퍼즐.
    /// 플레이어가 판 위를 걸어 다니지 않고, 다 이어진 상태 자체가 성공 조건이다.
    /// </summary>
    public class TracePuzzle : MonoBehaviour
    {
        private const int MIN_SIZE = 3;
        private const int SIDE_COUNT = 4;
        private const int GENERATE_TRIES = 50;
        private const int SCRAMBLE_TRIES = 30;

        [Header("퍼즐")]
        [Tooltip("층별 점 격자 한 변의 점 수 (0번이 1층). 조각은 점 사이 칸마다 하나씩 놓인다. 층이 더 많으면 마지막 값을 쓴다")]
        [SerializeField] private int[] _gridSizes = { 4, 4, 4 };

        [Header("움직임")]
        [Tooltip("조각이 시계 방향으로 90도 도는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _rotateSeconds = 0.12f;
        [Tooltip("모두 이어진 뒤 퍼즐이 닫힐 때까지 파랗게 빛나는 모습을 보여 주는 시간(초)")]
        [SerializeField, Min(0f)] private float _solveDelay = 1.8f;

        [Header("완성 빛")]
        [Tooltip("모두 이어졌을 때 선과 점이 바뀌는 색")]
        [SerializeField] private Color _solvedColor = new Color(0.45f, 0.85f, 1f, 1f);
        [Tooltip("선 뒤에 번지는 빛의 색. 알파가 빛의 진하기다")]
        [SerializeField] private Color _glowColor = new Color(0.25f, 0.65f, 1f, 0.45f);
        [Tooltip("빛 두께 (선 두께에 대한 배수)")]
        [SerializeField, Min(1f)] private float _glowWidthScale = 4f;
        [Tooltip("빛이 차오르는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _glowFadeSeconds = 0.35f;
        [Tooltip("빛이 숨 쉬듯 밝아졌다 어두워지는 빠르기(초당 횟수)")]
        [SerializeField, Min(0f)] private float _glowPulseSpeed = 1.5f;

        [Header("모양")]
        [SerializeField] private Material _lineMaterial;
        [Tooltip("V자 꼭짓점이 칸 가장자리에서 칸 가운데 쪽으로 들어간 정도 (0이면 직선, 1이면 칸 가운데)")]
        [SerializeField, Range(0f, 1f)] private float _vDepth = 0.7f;
        [Tooltip("판 한 변 길이(월드)")]
        [SerializeField, Min(0.1f)] private float _boardSize = 4.4f;
        [Tooltip("뒤에 까는 어두운 원판 지름. 홍채 원판 지름과 같게 두면 뒤의 미로가 모두 가려진다")]
        [SerializeField, Min(0.1f)] private float _backdropDiameter = 6.4f;
        [SerializeField] private Color _backdropColor = Color.black;
        [Tooltip("시작 조각과 아직 이어지지 않은 조각의 색")]
        [SerializeField] private Color _lineColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("시작 조각과 이어진 조각의 색")]
        [SerializeField] private Color _linkedColor = Color.white;
        [SerializeField] private Color _pointColor = new Color(0.55f, 0.85f, 1f, 1f);
        [Tooltip("돌릴 수 없는 시작 조각 표시 색")]
        [SerializeField] private Color _anchorColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color _cursorColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.07f;
        [SerializeField, Min(0.001f)] private float _pointSize = 0.12f;
        [SerializeField, Min(0.001f)] private float _frameWidth = 0.03f;
        [Tooltip("그리기 순서. 눈꺼풀(22)보다 낮게 둔다")]
        [SerializeField] private int _sortingOrder = 20;

        /// <summary>점 사이 칸 하나에 놓인 V자 조각. 어느 변 쪽 두 점을 잇는지와 그리기 오브젝트를 가진다.</summary>
        private class Piece
        {
            public int Side;
            public int Turns;
            public Transform Root;
            public LineRenderer Line;
        }

        private Piece[] _pieces = Array.Empty<Piece>();
        private bool[] _linked = Array.Empty<bool>();
        private int _size;
        private int _anchor;
        private int _cursor;
        private int _rotating = -1;
        private float _rotateT = 1f;
        private float _solveT = -1f;
        private bool _isOpen;
        private Transform _drawRoot;
        private LineRenderer _cursorFrame;
        private readonly List<LineRenderer> _dots = new List<LineRenderer>();
        private readonly List<LineRenderer> _glows = new List<LineRenderer>();

        public event Action Solved;
        public event Action Cancelled;

        public bool IsOpen => _isOpen;
        private int Cells => _size - 1;
        private int PieceCount => Cells * Cells;
        private float Spacing => _boardSize / _size;

        void Update()
        {
            if (!_isOpen) return;
            float dt = Time.deltaTime;
            // 다 이어진 뒤에는 입력을 받지 않고 이어진 모습을 잠깐 보여 준 다음 닫는다.
            if (_solveT >= 0f)
            {
                _solveT += dt;
                AdvanceGlow();
                if (_solveT < _solveDelay) return;
                Close();
                Solved?.Invoke();
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
            if (_rotateT < 1f)
            {
                AdvanceRotate(dt);
                return;
            }
            HandleInput(kb);
        }

        void OnDisable()
        {
            if (_isOpen) Close();
        }

        /// <summary>
        /// layerIndex에 맞는 크기로 항상 이을 수 있는 조각판을 만들고, 시작 조각을 뺀 조각 방향을 섞어 그린다.
        /// layerIndex와 seed를 사용하며, 조각 방향, 시작 조각, 선택 틀과 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Open(int layerIndex, int seed)
        {
            int sizeIndex = Mathf.Clamp(layerIndex, 0, _gridSizes.Length - 1);
            _size = Mathf.Max(MIN_SIZE, _gridSizes.Length > 0 ? _gridSizes[sizeIndex] : 4);
            _anchor = (Cells / 2) * Cells + Cells / 2;
            _cursor = _anchor;
            _rotating = -1;
            _rotateT = 1f;
            _solveT = -1f;
            _isOpen = true;

            var rng = new System.Random(seed);
            int[] solution = Generate(rng);
            BuildDrawing(solution);
            Scramble(rng);
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
            _pieces = Array.Empty<Piece>();
            _dots.Clear();
            _glows.Clear();
        }

        /// <summary>
        /// 시작 조각에서 출발해, 이미 이어진 점에 닿는 칸의 조각을 그 점을 지나는 변으로 하나씩 정하며 모든 조각이 이어진 정답 배치를 만든다.
        /// rng를 사용하며, 칸별 정답 변 번호 배열을 반환한다.
        /// </summary>
        private int[] Generate(System.Random rng)
        {
            var sides = new int[PieceCount];
            for (int attempt = 0; attempt < GENERATE_TRIES; attempt++)
            {
                if (TryGenerate(rng, sides)) return sides;
            }
            // 만들기에 계속 실패하면 마지막 시도 배치를 그대로 쓴다. 칸이 비어 있는 경우는 없어 판은 그려진다.
            return sides;
        }

        /// <summary>
        /// 정답 배치를 한 번 만들어 본다. 이어진 점에 닿는 칸 중 하나를 무작위로 골라, 그 점을 지나고 아직 쓰지 않은 변으로 조각을 정한다.
        /// rng와 sides를 사용하며, sides를 채우고 모든 칸을 정했으면 true를 반환한다.
        /// </summary>
        private bool TryGenerate(System.Random rng, int[] sides)
        {
            var reached = new bool[_size * _size];
            var usedEdges = new HashSet<long>();
            var assigned = new bool[PieceCount];
            sides[_anchor] = rng.Next(SIDE_COUNT);
            assigned[_anchor] = true;
            MarkPiece(_anchor, sides[_anchor], reached, usedEdges);

            var candidates = new List<(int cell, int side)>();
            for (int placed = 1; placed < PieceCount; placed++)
            {
                candidates.Clear();
                for (int cell = 0; cell < PieceCount; cell++)
                {
                    if (assigned[cell]) continue;
                    for (int side = 0; side < SIDE_COUNT; side++)
                    {
                        (int a, int b) = SidePoints(cell, side);
                        if (!reached[a] && !reached[b]) continue;
                        if (usedEdges.Contains(Key(a, b))) continue;
                        candidates.Add((cell, side));
                    }
                }
                if (candidates.Count == 0) return false;
                (int pick, int pickSide) = candidates[rng.Next(candidates.Count)];
                sides[pick] = pickSide;
                assigned[pick] = true;
                MarkPiece(pick, pickSide, reached, usedEdges);
            }
            return true;
        }

        /// <summary>
        /// 조각이 잇는 두 점을 이어진 점으로 표시하고, 그 변을 쓴 변으로 기록한다.
        /// cell, side, reached, usedEdges를 사용하며, reached와 usedEdges를 바꾼다.
        /// </summary>
        private void MarkPiece(int cell, int side, bool[] reached, HashSet<long> usedEdges)
        {
            (int a, int b) = SidePoints(cell, side);
            reached[a] = true;
            reached[b] = true;
            usedEdges.Add(Key(a, b));
        }

        /// <summary>
        /// 칸 cell의 side 변이 잇는 두 점을 구한다. 변 번호는 위 0, 오른쪽 1, 아래 2, 왼쪽 3이다.
        /// cell과 side를 사용하며, 두 점 번호를 반환한다.
        /// </summary>
        private (int a, int b) SidePoints(int cell, int side)
        {
            int r = cell / Cells, c = cell % Cells;
            int bl = r * _size + c, br = bl + 1, tl = bl + _size, tr = tl + 1;
            switch (side)
            {
                case 0: return (tl, tr);
                case 1: return (tr, br);
                case 2: return (br, bl);
                default: return (bl, tl);
            }
        }

        /// <summary>
        /// 두 점 번호로 순서와 상관없는 변 키를 만든다.
        /// a와 b를 사용하며, 키를 반환한다.
        /// </summary>
        private static long Key(int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        /// <summary>
        /// 시작 조각을 뺀 조각들을 무작위로 0~3번 돌려 둔다. 섞은 결과가 이미 모두 이어져 있으면 다시 섞는다.
        /// rng를 사용하며, 조각의 Side, Turns와 조각 오브젝트 회전을 바꾼다.
        /// </summary>
        private void Scramble(System.Random rng)
        {
            for (int attempt = 0; attempt < SCRAMBLE_TRIES; attempt++)
            {
                for (int cell = 0; cell < PieceCount; cell++)
                {
                    if (cell == _anchor) continue;
                    int turns = rng.Next(SIDE_COUNT);
                    _pieces[cell].Side = (_pieces[cell].Side + turns) % SIDE_COUNT;
                    _pieces[cell].Turns += turns;
                }
                if (!UpdateLinked()) break;
            }
            foreach (Piece piece in _pieces) piece.Root.localRotation = Quaternion.Euler(0f, 0f, -90f * piece.Turns);
        }

        /// <summary>
        /// 모든 조각의 선으로 점들을 묶고, 시작 조각과 이어진 조각을 표시한다.
        /// _pieces와 _anchor를 사용하며, _linked를 바꾸고 모든 조각이 이어졌으면 true를 반환한다.
        /// </summary>
        private bool UpdateLinked()
        {
            var parent = new int[_size * _size];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int cell = 0; cell < PieceCount; cell++)
            {
                (int a, int b) = SidePoints(cell, _pieces[cell].Side);
                parent[Root(parent, a)] = Root(parent, b);
            }
            int anchorRoot = Root(parent, SidePoints(_anchor, _pieces[_anchor].Side).a);
            _linked = new bool[PieceCount];
            bool all = true;
            for (int cell = 0; cell < PieceCount; cell++)
            {
                _linked[cell] = Root(parent, SidePoints(cell, _pieces[cell].Side).a) == anchorRoot;
                all &= _linked[cell];
            }
            return all;
        }

        /// <summary>
        /// 점 묶음에서 point가 속한 묶음의 대표 점을 찾고 경로를 줄인다.
        /// parent와 point를 사용하며, 대표 점 번호를 반환한다.
        /// </summary>
        private static int Root(int[] parent, int point)
        {
            while (parent[point] != point)
            {
                parent[point] = parent[parent[point]];
                point = parent[point];
            }
            return point;
        }

        /// <summary>
        /// WASD·방향키로 선택 틀을 옮기고, Space로 선택한 조각을 시계 방향으로 돌린다. 시작 조각은 돌리지 않는다.
        /// kb를 사용하며, _cursor와 회전 상태를 바꾼다.
        /// </summary>
        private void HandleInput(Keyboard kb)
        {
            int dr = 0, dc = 0;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) dr = 1;
            else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) dr = -1;
            else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) dc = 1;
            else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) dc = -1;
            if (dr != 0 || dc != 0)
            {
                int r = Mathf.Clamp(_cursor / Cells + dr, 0, Cells - 1);
                int c = Mathf.Clamp(_cursor % Cells + dc, 0, Cells - 1);
                _cursor = r * Cells + c;
                Refresh();
            }
            if (kb.spaceKey.wasPressedThisFrame && _cursor != _anchor) StartRotate(_cursor);
        }

        /// <summary>
        /// cell의 조각을 시계 방향으로 90도 돌리고 도는 연출을 시작한다. 잇는 변은 즉시 다음 변으로 바꿔 둔다.
        /// cell을 사용하며, 조각의 Side, Turns와 회전 상태를 바꾼다.
        /// </summary>
        private void StartRotate(int cell)
        {
            Piece piece = _pieces[cell];
            piece.Side = (piece.Side + 1) % SIDE_COUNT;
            piece.Turns++;
            _rotating = cell;
            _rotateT = 0f;
        }

        /// <summary>
        /// 도는 조각을 한 프레임 돌리고, 끝나면 이어진 조각을 다시 계산해 모두 이어졌으면 성공 대기를 시작한다.
        /// dt와 _rotateSeconds를 사용하며, _rotateT, 조각 회전, _solveT를 바꾼다.
        /// </summary>
        private void AdvanceRotate(float dt)
        {
            _rotateT = Mathf.MoveTowards(_rotateT, 1f, dt / _rotateSeconds);
            Piece piece = _pieces[_rotating];
            float e = Mathf.SmoothStep(0f, 1f, _rotateT);
            // 시계 방향은 z축 음의 방향이다.
            piece.Root.localRotation = Quaternion.Euler(0f, 0f, -90f * Mathf.Lerp(piece.Turns - 1, piece.Turns, e));
            if (_rotateT < 1f) return;
            _rotating = -1;
            if (Refresh()) StartGlow();
        }

        /// <summary>
        /// 모두 이어진 순간 조각마다 선 뒤에 번지는 빛 선을 만들고 성공 대기를 시작한다.
        /// _pieces와 완성 빛 설정을 사용하며, _glows와 _solveT를 바꾼다.
        /// </summary>
        private void StartGlow()
        {
            _solveT = 0f;
            _glows.Clear();
            foreach (Piece piece in _pieces)
            {
                LineRenderer glow = CreateLine("빛", piece.Root, 1);
                var points = new Vector3[piece.Line.positionCount];
                piece.Line.GetPositions(points);
                Shapes.SetPoints(glow, points, false);
                glow.widthMultiplier = _lineWidth * _glowWidthScale;
                _glows.Add(glow);
            }
            AdvanceGlow();
        }

        /// <summary>
        /// 완성 빛을 한 프레임 진행한다. 선과 점은 파란색으로 바뀌고, 뒤의 빛은 차오른 뒤 숨 쉬듯 밝아졌다 어두워진다.
        /// _solveT와 완성 빛 설정을 사용하며, 선·점·빛의 색과 빛 두께를 바꾼다.
        /// </summary>
        private void AdvanceGlow()
        {
            float fill = Mathf.SmoothStep(0f, 1f, _solveT / _glowFadeSeconds);
            float pulse = 0.75f + 0.25f * Mathf.Cos(_solveT * _glowPulseSpeed * Mathf.PI * 2f);
            Color line = Color.Lerp(_linkedColor, _solvedColor, fill);
            Color glow = _glowColor;
            glow.a *= fill * pulse;
            foreach (Piece piece in _pieces) Shapes.SetColor(piece.Line, line);
            foreach (LineRenderer dot in _dots) Shapes.SetColor(dot, Color.Lerp(_pointColor, _solvedColor, fill));
            foreach (LineRenderer g in _glows)
            {
                Shapes.SetColor(g, glow);
                g.widthMultiplier = _lineWidth * _glowWidthScale * (0.8f + 0.2f * pulse);
            }
        }

        /// <summary>
        /// 칸 번호를 판 로컬 좌표(칸 가운데)로 바꾼다.
        /// cell과 _boardSize를 사용하며, 로컬 위치를 반환한다.
        /// </summary>
        private Vector3 CellPos(int cell)
        {
            float half = (Cells - 1) * Spacing * 0.5f;
            return new Vector3(cell % Cells * Spacing - half, cell / Cells * Spacing - half, 0f);
        }

        /// <summary>
        /// 점 번호를 판 로컬 좌표로 바꾼다.
        /// point와 _boardSize를 사용하며, 로컬 위치를 반환한다.
        /// </summary>
        private Vector3 PointPos(int point)
        {
            float half = (_size - 1) * Spacing * 0.5f;
            return new Vector3(point % _size * Spacing - half, point / _size * Spacing - half, 0f);
        }

        /// <summary>
        /// 원판, 점, V자 조각, 시작 조각 표시, 선택 틀을 새로 만든다. 조각은 정답 방향으로 만든 뒤 섞는다.
        /// solution과 모양 설정을 사용하며, _drawRoot 아래 그리기 오브젝트와 _pieces를 바꾼다.
        /// </summary>
        private void BuildDrawing(int[] solution)
        {
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _dots.Clear();
            _glows.Clear();
            _drawRoot = new GameObject("선 잇기 판").transform;
            _drawRoot.SetParent(transform, false);

            LineRenderer backdrop = CreateLine("원판", _drawRoot, 0);
            Shapes.SetBlob(backdrop, _backdropDiameter);
            Shapes.SetColor(backdrop, _backdropColor);

            for (int point = 0; point < _size * _size; point++)
            {
                LineRenderer dot = CreateLine("점", _drawRoot, 3);
                Shapes.SetBlob(dot, _pointSize);
                Shapes.SetColor(dot, _pointColor);
                dot.transform.localPosition = PointPos(point);
                _dots.Add(dot);
            }

            // 조각은 위쪽 변을 잇는 V로 그리고, 칸 가운데를 축으로 돌려 다른 변을 잇게 한다.
            float half = Spacing * 0.5f;
            Vector3[] vShape =
            {
                new Vector3(-half, half, 0f),
                new Vector3(0f, half * (1f - _vDepth), 0f),
                new Vector3(half, half, 0f),
            };
            _pieces = new Piece[PieceCount];
            for (int cell = 0; cell < PieceCount; cell++)
            {
                var piece = new Piece { Side = solution[cell], Turns = solution[cell] };
                piece.Root = new GameObject("조각").transform;
                piece.Root.SetParent(_drawRoot, false);
                piece.Root.localPosition = CellPos(cell);
                piece.Line = CreateLine("V", piece.Root, 2);
                Shapes.SetPoints(piece.Line, vShape, false);
                piece.Line.widthMultiplier = _lineWidth;
                _pieces[cell] = piece;
            }

            LineRenderer anchorMark = CreateLine("시작 조각", _drawRoot, 1);
            Shapes.SetBlob(anchorMark, Spacing * 0.35f);
            Shapes.SetColor(anchorMark, _anchorColor);
            anchorMark.transform.localPosition = CellPos(_anchor);

            float inset = half * 0.92f;
            _cursorFrame = CreateLine("선택", _drawRoot, 4);
            Shapes.SetPoints(_cursorFrame, new[]
            {
                new Vector3(-inset, -inset, 0f), new Vector3(inset, -inset, 0f),
                new Vector3(inset, inset, 0f), new Vector3(-inset, inset, 0f),
            }, true);
            _cursorFrame.widthMultiplier = _frameWidth;
            Shapes.SetColor(_cursorFrame, _cursorColor);
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
        /// 이어진 조각을 다시 계산하고 조각 색과 선택 틀 위치를 다시 그린다.
        /// _pieces, _linked, _cursor를 사용하며, 그리기 오브젝트의 색과 위치를 바꾸고 모든 조각이 이어졌으면 true를 반환한다.
        /// </summary>
        private bool Refresh()
        {
            if (_drawRoot == null) return false;
            bool all = UpdateLinked();
            for (int cell = 0; cell < PieceCount; cell++)
                Shapes.SetColor(_pieces[cell].Line, _linked[cell] ? _linkedColor : _lineColor);
            _cursorFrame.transform.localPosition = CellPos(_cursor);
            _cursorFrame.enabled = !all;
            return all;
        }
    }
}
