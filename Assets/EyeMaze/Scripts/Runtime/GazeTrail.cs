using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 슬픔과 침잠일 때 시선이 지나온 칸을 잇는 흔적 선을 그린다.
    /// 칸은 층 기준으로 기억해 층과 함께 돌고, 두 칸 사이가 지금 벽으로 막혀 있으면 그 구간은 그리지 않는다.
    /// 미로 오브젝트 아래에 둔다.
    /// </summary>
    public class GazeTrail : MonoBehaviour
    {
        private const float WALL_CHECK_INTERVAL = 0.2f;
        private const float WIDTH_RATIO = 0.8f;
        // 스펙 046 FR-046: 슬픔의 안개(17) 위에 그려 흔적이 길잡이가 되게 한다.
        private const int SORTING_ORDER = 18;

        [Header("연결")]
        [Tooltip("시선, 미로, 감정을 읽을 디렉터")]
        [SerializeField] private EyeMazeDirector _director;

        [Header("흔적")]
        [Tooltip("슬픔에서 남는 흔적 칸 수")]
        [SerializeField, Min(2)] private int _sadTrailCells = 6;
        [Tooltip("침잠에서 남는 흔적 칸 수")]
        [SerializeField, Min(2)] private int _deepTrailCells = 18;
        [Tooltip("흔적 선 색")]
        [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.55f);

        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<bool> _open = new List<bool>();
        private MazeLayer[] _layers;
        private float _wallCheckT;

        /// <summary>지나온 칸 하나. 섹터는 그 칸이 속한 층 기준이다.</summary>
        private struct Cell
        {
            public int Ring { get; set; }
            public int Sector { get; set; }
        }

        private int Capacity => Mathf.Max(_sadTrailCells, _deepTrailCells);

        void Start() => _layers = _director.Maze.Layers;

        void Update()
        {
            EmotionControl emotion = _director.EmotionControl;
            if (emotion.Current != Emotion.Sad)
            {
                // 슬픔을 벗어나면 흔적을 지운다.
                if (_cells.Count > 0) _cells.Clear();
                Hide(0);
                return;
            }

            bool gazing = _director.CurrentMode == EyeMazeDirector.Mode.Gaze && !_director.InAbyss;
            bool changed = gazing && Record();
            _wallCheckT -= Time.deltaTime;
            if (changed || _wallCheckT <= 0f)
            {
                _wallCheckT = WALL_CHECK_INTERVAL;
                CheckWalls();
            }
            Draw(gazing, emotion.Stage >= 2 ? _deepTrailCells : _sadTrailCells);
        }

        /// <summary>
        /// 흔적을 모두 지운다. 만남을 처음부터 다시 시작할 때 부른다.
        /// 입력값은 없으며, _cells와 선의 표시를 바꾼다.
        /// </summary>
        public void Clear()
        {
            _cells.Clear();
            Hide(0);
        }

        /// <summary>
        /// 시선이 새 칸에 들어왔으면 그 칸을 흔적 끝에 더한다. 가장 긴 흔적 칸 수를 넘으면 오래된 칸부터 버린다.
        /// 시선의 고리와 섹터, 층 회전량을 사용하며, _cells를 바꾸고 칸이 더해졌으면 true를 반환한다.
        /// </summary>
        private bool Record()
        {
            IrisMaze maze = _director.Maze;
            GazeCursor cursor = _director.Cursor;
            int ring = cursor.Ring;
            int sector = maze.Mod(cursor.Sector - maze.Offset(maze.LayerOfRing(ring)));
            if (_cells.Count > 0 && _cells[_cells.Count - 1].Ring == ring && _cells[_cells.Count - 1].Sector == sector) return false;
            _cells.Add(new Cell { Ring = ring, Sector = sector });
            while (_cells.Count > Capacity) _cells.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// 이어진 두 칸 사이가 지금 열려 있는지 구간마다 다시 확인한다.
        /// _cells와 미로의 벽 상태를 사용하며, _open을 다시 채운다.
        /// </summary>
        private void CheckWalls()
        {
            IrisMaze maze = _director.Maze;
            _open.Clear();
            for (int i = 0; i + 1 < _cells.Count; i++)
            {
                Cell from = _cells[i];
                Cell to = _cells[i + 1];
                _open.Add(maze.IsStepOpen(from.Ring, WorldSector(maze, from), to.Ring, WorldSector(maze, to)));
            }
        }

        /// <summary>
        /// 최근 칸들을 잇는 구간 선을 그린다. 막힌 구간과 남는 선은 숨긴다.
        /// visible(들여다보는 중인지), cellCount(보여 줄 칸 수)를 사용하며, 구간 선의 위치, 색, 표시를 바꾼다.
        /// </summary>
        private void Draw(bool visible, int cellCount)
        {
            if (!visible) { Hide(0); return; }
            IrisMaze maze = _director.Maze;
            int first = Mathf.Max(0, _cells.Count - cellCount);
            int used = 0;
            for (int i = first; i + 1 < _cells.Count && i < _open.Count; i++)
            {
                if (!_open[i]) continue;
                LineRenderer line = LineAt(used++);
                line.SetPosition(0, CellPosition(maze, _cells[i]));
                line.SetPosition(1, CellPosition(maze, _cells[i + 1]));
                line.widthMultiplier = maze.WallWidthNow * WIDTH_RATIO;
                Shapes.SetColor(line, _color);
                line.enabled = true;
            }
            Hide(used);
        }

        /// <summary>
        /// 구간 선을 번호로 가져온다. 모자라면 이 오브젝트 아래에 새로 만든다.
        /// index와 미로의 선 재질을 사용하며, 구간 선을 반환한다.
        /// </summary>
        private LineRenderer LineAt(int index)
        {
            while (_lines.Count <= index)
            {
                var segment = new GameObject($"흔적 {_lines.Count + 1}");
                segment.transform.SetParent(transform, false);
                LineRenderer line = Shapes.Ensure(segment, _director.Maze.LineMaterial, 2);
                line.positionCount = 2;
                line.sortingOrder = SORTING_ORDER;
                _lines.Add(line);
            }
            return _lines[index];
        }

        /// <summary>
        /// from번부터의 구간 선을 숨긴다.
        /// from을 사용하며, 구간 선의 표시를 바꾼다.
        /// </summary>
        private void Hide(int from)
        {
            for (int i = from; i < _lines.Count; i++)
                if (_lines[i].enabled) _lines[i].enabled = false;
        }

        /// <summary>
        /// 칸 한가운데의 위치를 이 오브젝트 기준 좌표로 구한다. 칸이 속한 층의 회전을 따른다.
        /// maze와 cell을 사용하며, 로컬 좌표를 반환한다.
        /// </summary>
        private Vector3 CellPosition(IrisMaze maze, Cell cell)
        {
            Transform layer = _layers[maze.LayerOfRing(cell.Ring)].transform;
            return transform.InverseTransformPoint(layer.TransformPoint(maze.CellLocal(cell.Ring, cell.Sector)));
        }

        /// <summary>
        /// 층 기준 섹터를 화면 기준 섹터로 바꾼다.
        /// maze와 cell을 사용하며, 화면 기준 섹터를 반환한다.
        /// </summary>
        private static int WorldSector(IrisMaze maze, Cell cell)
        {
            return maze.Mod(cell.Sector + maze.Offset(maze.LayerOfRing(cell.Ring)));
        }
    }
}
