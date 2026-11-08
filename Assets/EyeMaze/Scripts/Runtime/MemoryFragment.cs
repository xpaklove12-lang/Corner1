using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 미로 속 기억 조각. 층(MazeLayer) 아래에 두면 그 층과 함께 회전한다.
    /// ring / sector를 바꾸면 에디트 모드에서도 바로 그 칸으로 옮겨진다.
    /// </summary>
    [ExecuteAlways]
    public class MemoryFragment : MonoBehaviour
    {
        [Header("내용")]
        [SerializeField, FormerlySerializedAs("title")] private string _title = "기억";
        [SerializeField, FormerlySerializedAs("text"), TextArea(2, 5)] private string _text;

        [Header("위치")]
        [SerializeField, FormerlySerializedAs("ring")] private int _ring;
        [Tooltip("자기 층 기준 sector")]
        [SerializeField, FormerlySerializedAs("sector")] private int _sector;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("collected")] private bool _collected;
        [Tooltip("주우면 켜지는 플래그 id (Data/Flags.csv에 적힌 것). 대화 CSV의 need 칸에서 쓴다")]
        [SerializeField] private string _flag;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("color")] private Color _color = new Color(0.6f, 0.95f, 1f, 1f);
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;

        [NonSerialized] private int _drawnKey = int.MinValue;
        private IrisMaze _maze;

        /// <summary>파편 이름. Flags.csv에서 이 파편 플래그 줄의 title 칸이 있으면 그걸 쓴다.</summary>
        public string Title => Flags.TitleOf(_flag) ?? _title;
        /// <summary>파편을 주울 때 나오는 글. Flags.csv의 text 칸이 있으면 그걸 쓴다.</summary>
        public string Text => Flags.TextOf(_flag) ?? _text;
        public int Ring { get => _ring; set => _ring = value; }
        public int Sector { get => _sector; set => _sector = value; }
        public string Flag => _flag;

        /// <summary>주웠는지. 바꾸면 플래그도 함께 켜지고 꺼진다.</summary>
        public bool Collected
        {
            get => _collected;
            set { _collected = value; Flags.Set(_flag, value); }
        }
        public MazeLayer Layer => GetComponentInParent<MazeLayer>(true);

        /// <summary>화면 기준 sector (층 회전 반영).</summary>
        public int WorldSector
        {
            get
            {
                if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
                MazeLayer l = Layer;
                return _maze != null ? _maze.Mod(_sector + (l != null ? l.Offset : 0)) : _sector;
            }
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            int key = HashCode.Combine(_ring, _sector, _maze.GeometryKey, _collected, _color, _maze.WallAlpha, _maze.Focus);
            if (key == _drawnKey) return;
            _drawnKey = key;

            _ring = Mathf.Clamp(_ring, 0, _maze.Rings - 1);
            _sector = _maze.Mod(_sector);
            transform.localPosition = _maze.CellLocal(_ring, _sector);
            transform.localRotation = Quaternion.identity;
            Shapes.SetBlob(_line, _maze.RingWidth * 0.42f);
            Color c = _color;
            c.a *= Mathf.Lerp(0.35f, 1f, _maze.WallAlpha) * _maze.FocusAlpha;
            Shapes.SetColor(_line, c);
            _line.sortingOrder = 7;
            _line.enabled = !_collected;
        }
    }
}
