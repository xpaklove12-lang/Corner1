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
        // 스펙 010: 연출 중 벽 색에 곱할 투명도 추가
        [NonSerialized] private float _visualAlpha = 1f;
        // 스펙 015: 어지러움 연출로 벽을 흔드는 폭과 빠르기 추가
        [NonSerialized] private float _wobble;
        [NonSerialized] private float _wobbleSpeed;
        private IrisMaze _maze;

        public Kind WallKind => _kind;
        public int Ring => _ring;
        public int Sector => _sector;
        /// <summary>연출 중 벽 색에 곱하는 투명도. 벽 판정에는 쓰지 않는다.</summary>
        public float VisualAlpha { get => _visualAlpha; set => _visualAlpha = value; }

        /// <summary>
        /// 스펙 015: 벽 가운데의 층 기준 위치. 원호 벽은 두 고리 사이 원 위, 방사 벽은 고리 가운데다.
        /// 위치 정보와 미로 모양을 사용하며, 층 기준 위치를 반환한다. 미로가 없으면 원점이다.
        /// </summary>
        public Vector3 MidLocal
        {
            get
            {
                if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
                if (_maze == null) return Vector3.zero;
                float d = _maze.SectorDeg;
                return _kind == Kind.Arc ? Shapes.Polar(_maze.RingInner(_ring), (_sector + 0.5f) * d) : Shapes.Polar(_maze.RingMid(_ring), (_sector + 1) * d);
            }
        }

        /// <summary>
        /// 스펙 015: 벽을 흔들리게 그린다. 그림만 바꾸고 벽 판정은 바꾸지 않는다.
        /// amplitude(흔들림 폭, 0이면 멈춤)와 speed를 사용하며, _wobble과 _wobbleSpeed를 바꾼다.
        /// </summary>
        public void SetWobble(float amplitude, float speed)
        {
            _wobble = Mathf.Max(0f, amplitude);
            _wobbleSpeed = speed;
        }

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
            // 스펙 010: 표시 투명도가 바뀌어도 다시 칠하도록 키에 포함
            // 스펙 015: 흔들림 폭도 키에 넣고, 흔들리는 동안에는 매 프레임 다시 그린다.
            int key = HashCode.Combine(_kind, _ring, _sector, _maze.GeometryKey, _maze.WallColor, _maze.WallWidthNow, _visualAlpha, _wobble);
            if (key == _drawnKey && _wobble <= 0f) return;
            _drawnKey = key;

            float d = _maze.SectorDeg;
            if (_wobble > 0f) DrawWobbling(d);
            else if (_kind == Kind.Arc)
                Shapes.SetArc(_line, _maze.RingInner(_ring), _sector * d, (_sector + 1) * d, 6);
            else
            {
                float a = (_sector + 1) * d;
                Shapes.SetPoints(_line, new[] { Shapes.Polar(_maze.RingOuter(_ring), a), Shapes.Polar(_maze.RingInner(_ring), a) }, false);
            }
            _line.widthMultiplier = _maze.WallWidthNow;
            // 스펙 010: 벽 색에 표시 투명도를 곱함
            Color color = _maze.WallColor;
            color.a *= _visualAlpha;
            Shapes.SetColor(_line, color);
            _line.sortingOrder = 5;
        }

        /// <summary>
        /// 스펙 015: 흔들리는 벽을 그린다. 원호 벽은 점마다 반지름 방향으로, 방사 벽은 선에 수직 방향으로 시간에 따라 물결치듯 밀어 그린다.
        /// d(칸 각도), _wobble, _wobbleSpeed와 시간을 사용하며, _line의 점 목록을 바꾼다.
        /// </summary>
        private void DrawWobbling(float d)
        {
            float time = Application.isPlaying ? Time.time : 0f;
            if (_kind == Kind.Arc)
            {
                const int seg = 8;
                float r = _maze.RingInner(_ring);
                var pts = new Vector3[seg + 1];
                for (int i = 0; i <= seg; i++)
                {
                    float wave = Mathf.Sin(time * _wobbleSpeed + i * 1.3f + _sector * 2.1f) * _wobble;
                    pts[i] = Shapes.Polar(r + wave, Mathf.Lerp(_sector * d, (_sector + 1) * d, i / (float)seg));
                }
                Shapes.SetPoints(_line, pts, false);
                return;
            }
            const int steps = 5;
            float a = (_sector + 1) * d;
            Vector3 side = Shapes.Polar(1f, a + 90f);
            var line = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float wave = Mathf.Sin(time * _wobbleSpeed + i * 1.7f + _ring * 2.3f) * _wobble;
                line[i] = Shapes.Polar(Mathf.Lerp(_maze.RingOuter(_ring), _maze.RingInner(_ring), i / (float)steps), a) + side * wave;
            }
            Shapes.SetPoints(_line, line, false);
        }
    }
}
