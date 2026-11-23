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
        // 스펙 046: 열쇠 플래그 조건 추가
        [Tooltip("이 문을 여는 열쇠 플래그. 채우면 신뢰와 동공 조건 대신 이 플래그만 본다")]
        [SerializeField] private string _keyFlag = "";

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;
        [SerializeField, FormerlySerializedAs("lockedColor")] private Color _lockedColor = new Color(1f, 0.72f, 0.2f, 1f);
        [SerializeField, FormerlySerializedAs("openColor")] private Color _openColor = new Color(0.6f, 1f, 0.7f, 0.45f);
        // 스펙 046: 희망에서 열쇠가 맞는 문의 맥동 색과 주기 추가
        [Tooltip("희망과 열망에서 열쇠가 맞고 아직 지나가지 않은 문이 맥동하는 색")]
        [SerializeField] private Color _gateGlowColor = new Color(0.75f, 0.9f, 1f, 1f);
        [Tooltip("맥동 한 주기(초)")]
        [SerializeField, Min(0.1f)] private float _glowSeconds = 1.2f;

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 스펙 010: 연출 중 문 색에 곱할 투명도 추가
        [NonSerialized] private float _visualAlpha = 1f;
        private IrisMaze _maze;

        public int Ring => _ring;
        public int Sector => _sector;
        /// <summary>연출 중 문 색에 곱하는 투명도. 통과 판정에는 쓰지 않는다.</summary>
        public float VisualAlpha { get => _visualAlpha; set => _visualAlpha = value; }
        /// <summary>
        /// 막혔을 때 문구. Strings.csv의 gate.pupil(동공 문) / gate.first(첫 문) / gate.deep(그 안쪽 문)이 있으면 그걸 쓴다.
        /// </summary>
        public string LockedMessage => HasKey
            ? Strings.Get("gate.key", "잠겨 있다. 눈 속에서 찾은 말을 건네야 열린다.")
            : Strings.Get(_requiredDilation >= 0f ? "gate.pupil" : _requiredTrust <= 1 ? "gate.first" : "gate.deep", _lockedMessage);
        /// <summary>스펙 046: 이 문을 여는 열쇠 플래그. 비어 있으면 신뢰와 동공 조건을 쓴다.</summary>
        public string KeyFlag
        {
            get => _keyFlag;
            set
            {
                _keyFlag = value ?? "";
                _drawnKey = int.MinValue;
            }
        }
        private bool HasKey => !string.IsNullOrEmpty(_keyFlag);

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
            // 스펙 046: 열쇠 플래그가 정해진 문은 그 플래그만 본다.
            if (HasKey) return Flags.IsOn(_keyFlag);
            if (p == null) return true;
            // 스펙 004: 안 좋은 말의 감점이 문을 막지 않도록 GateTrust로 판단
            bool trustOk = _requiredTrust < 0 || p.GateTrust >= _requiredTrust;
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
            // 스펙 046 FR-037: 희망과 열망에서 열쇠가 맞고 아직 지나가지 않은 문은 맥동한다.
            Person person = _maze.Person;
            bool glow = Application.isPlaying && open && HasKey && person != null && person.Emotion == Emotion.Hope && !_maze.IsGatePassed(_ring);
            // 스펙 010: 표시 투명도가 바뀌어도 다시 칠하도록 키에 포함
            int key = HashCode.Combine(_ring, _sector, _maze.GeometryKey, open, _maze.WallAlpha, _maze.Focus, HashCode.Combine(_lockedColor, _openColor), _visualAlpha);
            if (key == _drawnKey && !glow) return;
            // 맥동이 끝난 다음 프레임에 원래 색으로 다시 칠하도록 맥동 중에는 키를 비워 둔다.
            _drawnKey = glow ? int.MinValue : key;

            float d = _maze.SectorDeg;
            Shapes.SetArc(_line, _maze.RingInner(_ring), _sector * d + d * 0.12f, (_sector + 1) * d - d * 0.12f, 6);
            float wave = glow ? 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / _glowSeconds) : 0f;
            _line.widthMultiplier = _maze.WallWidthNow * (open ? Mathf.Lerp(1.2f, 2.4f, wave) : 2.4f);
            Color c = open ? Color.Lerp(_openColor, _gateGlowColor, wave) : _lockedColor;
            // 스펙 010: 문 색에 표시 투명도를 곱함
            c.a *= Mathf.Lerp(0.5f, 1f, _maze.WallAlpha) * _maze.FocusAlpha * _visualAlpha;
            Shapes.SetColor(_line, c);
            _line.sortingOrder = 6;
        }
    }
}
