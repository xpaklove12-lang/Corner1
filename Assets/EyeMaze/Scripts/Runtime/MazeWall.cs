using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>벽 하나. 오브젝트를 끄면(비활성) 그 자리는 지나갈 수 있다.</summary>
    [ExecuteAlways]
    public class MazeWall : MonoBehaviour
    {
        public enum Kind { Arc, Radial }

        [Header("위치")]
        [Tooltip("Arc: ring과 ring+1 사이 / Radial: ring 안에서 sector와 sector+1 사이")]
        [SerializeField, FormerlySerializedAs("kind")] private Kind _kind;
        [SerializeField, FormerlySerializedAs("ring")] private int _ring;
        [SerializeField, FormerlySerializedAs("sector")] private int _sector;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 스펙 008: 연출 중 벽 색에 곱할 투명도 추가
        [NonSerialized] private float _visualAlpha = 1f;
        private IrisMaze _maze;

        public Kind WallKind => _kind;
        public int Ring => _ring;
        public int Sector => _sector;
        /// <summary>연출 중 벽 색에 곱하는 투명도. 벽 판정에는 쓰지 않는다.</summary>
        public float VisualAlpha { get => _visualAlpha; set => _visualAlpha = value; }

        /// <summary>
        /// 새로 만든 벽의 위치와 그림을 한 번에 설정한다.
        /// kind, ring, sector, line을 사용하며, 이 벽의 위치 정보와 LineRenderer 참조를 바꾼다.
        /// </summary>
        public void Setup(Kind kind, int ring, int sector, LineRenderer line)
        {
            _kind = kind;
            _ring = ring;
            _sector = sector;
            _line = line;
            _drawnKey = int.MinValue;
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            // 스펙 008: 표시 투명도가 바뀌어도 다시 칠하도록 키에 포함
            int key = HashCode.Combine(_kind, _ring, _sector, _maze.GeometryKey, _maze.WallColor, _maze.WallWidthNow, _visualAlpha);
            if (key == _drawnKey) return;
            _drawnKey = key;

            float d = _maze.SectorDeg;
            if (_kind == Kind.Arc)
                Shapes.SetArc(_line, _maze.RingInner(_ring), _sector * d, (_sector + 1) * d, 6);
            else
            {
                float a = (_sector + 1) * d;
                Shapes.SetPoints(_line, new[] { Shapes.Polar(_maze.RingOuter(_ring), a), Shapes.Polar(_maze.RingInner(_ring), a) }, false);
            }
            _line.widthMultiplier = _maze.WallWidthNow;
            // 스펙 008: 벽 색에 표시 투명도를 곱함
            Color color = _maze.WallColor;
            color.a *= _visualAlpha;
            Shapes.SetColor(_line, color);
            _line.sortingOrder = 5;
        }
    }
}
