using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 플레이어의 시선(미로 속 점). 위치는 반지름 + 각도(화면 기준)로 연속적으로 움직인다.
    /// 인스펙터에서 radius / angle을 바꾸거나, 씬 뷰에서 이동 툴(W)로 직접 끌어도 된다. (벽을 무시하는 디버그용 조작)
    /// </summary>
    [ExecuteAlways]
    public class GazeCursor : MonoBehaviour
    {
        [Header("위치")]
        [Tooltip("홍채 중심에서의 거리")]
        [SerializeField, FormerlySerializedAs("radius")] private float _radius = 2.8f;
        [Tooltip("각도 (도, 반시계 방향)")]
        [SerializeField, FormerlySerializedAs("angle")] private float _angle = 101.25f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("visible")] private bool _visible;
        [SerializeField, FormerlySerializedAs("color")] private Color _color = Color.white;
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;
        [SerializeField, FormerlySerializedAs("appliedPos"), HideInInspector] private Vector3 _appliedPos;
        [SerializeField, FormerlySerializedAs("applied"), HideInInspector] private bool _applied;

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 스펙 007: 깜빡임 회전 때 화면에만 더하는 각도 추가
        [NonSerialized] private float _blinkVisualDeg;
        private IrisMaze _maze;

        public float Radius { get => _radius; set => _radius = value; }
        public float Angle { get => _angle; set => _angle = value; }
        public bool Visible { get => _visible; set => _visible = value; }
        public Color Color { get => _color; set => _color = value; }
        /// <summary>깜빡임 회전 때 화면에만 더하는 각도(도). 벽 판정에는 쓰지 않는다.</summary>
        public float BlinkVisualDeg { get => _blinkVisualDeg; set => _blinkVisualDeg = value; }

        /// <summary>현재 고리 (0 = 가장 바깥)</summary>
        public int Ring => Maze != null ? Maze.RingAt(_radius) : 0;
        /// <summary>현재 칸 (화면 기준)</summary>
        public int Sector => Maze != null ? Maze.SectorAt(_angle) : 0;
        public Vector3 LocalPos => Shapes.Polar(_radius, _angle);

        private IrisMaze Maze => _maze != null ? _maze : (_maze = GetComponentInParent<IrisMaze>());

        /// <summary>
        /// 시선을 칸 한가운데로 옮긴다.
        /// ring, sector를 사용하며, 반지름과 각도를 바꾸고 위치를 바로 반영한다.
        /// </summary>
        public void SnapTo(int ring, int sector)
        {
            if (Maze == null) return;
            _radius = Maze.RingMid(Mathf.Clamp(ring, 0, Maze.Rings - 1));
            _angle = (Maze.Mod(sector) + 0.5f) * Maze.SectorDeg;
            Apply();
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            if (Maze == null) return;

            // 씬 뷰에서 사용자가 직접 끌어 옮겼으면 그 위치를 받아들인다.
            if (_applied && (transform.localPosition - _appliedPos).sqrMagnitude > 1e-6f)
            {
                Vector3 p = transform.localPosition;
                _radius = new Vector2(p.x, p.y).magnitude;
                _angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            }
            Apply();

            int key = HashCode.Combine(_visible, _color, Maze.RingWidth);
            if (_line == null || key == _drawnKey) return;
            _drawnKey = key;
            Shapes.SetBlob(_line, Maze.RingWidth * 0.5f);
            Shapes.SetColor(_line, _color);
            _line.sortingOrder = 12;
            _line.enabled = _visible;
        }

        /// <summary>
        /// 반지름과 각도를 범위 안으로 맞추고 Transform 위치에 반영한다.
        /// _radius, _angle을 사용하며, transform.localPosition과 적용 기록을 바꾼다.
        /// </summary>
        private void Apply()
        {
            if (Maze == null) return;
            _radius = Mathf.Clamp(_radius, 0f, Maze.OuterRadius); // 0은 동공 한가운데(심연)다.
            _angle = Mathf.Repeat(_angle, 360f);
            // 스펙 007: 표시 위치에만 깜빡임 회전 각도를 더함
            Vector3 pos = Shapes.Polar(_radius, _angle + _blinkVisualDeg);
            if (transform.localPosition != pos) transform.localPosition = pos;
            _appliedPos = pos;
            _applied = true;
        }
    }
}
