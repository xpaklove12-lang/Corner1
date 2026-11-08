using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>층 사이의 문. 조건(신뢰, 동공 크기)을 채워야 지나갈 수 있다.</summary>
    [ExecuteAlways]
    public class MazeGate : MonoBehaviour
    {
        [Header("위치")]
        [Tooltip("이 문이 있는 원호 자리 (ring과 ring+1 사이, 자기 층 기준 sector)")]
        [SerializeField, FormerlySerializedAs("ring")] private int _ring;
        [SerializeField, FormerlySerializedAs("sector")] private int _sector;

        [Header("조건")]
        [Tooltip("필요한 신뢰 (-1 = 조건 없음)")]
        [SerializeField, FormerlySerializedAs("requiredTrust")] private int _requiredTrust = 1;
        [Tooltip("필요한 동공 열림 0~1 (-1 = 조건 없음)")]
        [SerializeField, FormerlySerializedAs("requiredDilation")] private float _requiredDilation = -1f;
        [SerializeField, FormerlySerializedAs("lockedMessage"), TextArea] private string _lockedMessage = "잠겨 있다.";

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;
        [SerializeField, FormerlySerializedAs("lockedColor")] private Color _lockedColor = new Color(1f, 0.72f, 0.2f, 1f);
        [SerializeField, FormerlySerializedAs("openColor")] private Color _openColor = new Color(0.6f, 1f, 0.7f, 0.45f);

        [NonSerialized] private int _drawnKey = int.MinValue;
        private IrisMaze _maze;

        public int Ring => _ring;
        public int Sector => _sector;
        /// <summary>
        /// 막혔을 때 문구. Strings.csv의 gate.pupil(동공 문) / gate.first(첫 문) / gate.deep(그 안쪽 문)이 있으면 그걸 쓴다.
        /// </summary>
        public string LockedMessage => Strings.Get(_requiredDilation >= 0f ? "gate.pupil" : _requiredTrust <= 1 ? "gate.first" : "gate.deep", _lockedMessage);

        /// <summary>
        /// 새로 만든 문의 위치, 조건, 그림을 한 번에 설정한다.
        /// ring, sector, requiredTrust, requiredDilation, lockedMessage, line을 사용하며, 이 문의 설정값을 바꾼다.
        /// </summary>
        public void Setup(int ring, int sector, int requiredTrust, float requiredDilation, string lockedMessage, LineRenderer line)
        {
            _ring = ring;
            _sector = sector;
            _requiredTrust = requiredTrust;
            _requiredDilation = requiredDilation;
            _lockedMessage = lockedMessage;
            _line = line;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// 상대의 지금 상태로 이 문을 지나갈 수 있는지 판단한다.
        /// p의 신뢰와 동공 열림을 사용하며, 지나갈 수 있으면 true를 반환한다.
        /// </summary>
        public bool CanPass(Person p)
        {
            if (p == null) return true;
            bool trustOk = _requiredTrust < 0 || p.Trust >= _requiredTrust;
            bool dilOk = _requiredDilation < 0f || p.Dilation >= _requiredDilation;
            return trustOk && dilOk;
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            bool open = CanPass(_maze.Person);
            int key = HashCode.Combine(_ring, _sector, _maze.GeometryKey, open, _maze.WallAlpha, _maze.Focus, _lockedColor, _openColor);
            if (key == _drawnKey) return;
            _drawnKey = key;

            float d = _maze.SectorDeg;
            Shapes.SetArc(_line, _maze.RingInner(_ring), _sector * d + d * 0.12f, (_sector + 1) * d - d * 0.12f, 6);
            _line.widthMultiplier = _maze.WallWidthNow * (open ? 1.2f : 2.4f);
            Color c = open ? _openColor : _lockedColor;
            c.a *= Mathf.Lerp(0.5f, 1f, _maze.WallAlpha) * _maze.FocusAlpha;
            Shapes.SetColor(_line, c);
            _line.sortingOrder = 6;
        }
    }
}
