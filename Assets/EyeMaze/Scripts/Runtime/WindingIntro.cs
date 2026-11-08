using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 태엽 = 눈을 들여다볼 수 있는 시간. 이 게임의 유일한 자원이다.
    /// 대화 중 Space(패드 A)를 누르고 있으면 막대가 차오르고, 떼는 순간 바로 눈을 들여다본다.
    /// 뗀 지점이 파란 목표선에 가까울수록 길게 감기고, 끝까지 찬 뒤에도 누르고 있으면 미끄러져 전부 풀린다.
    /// 상대가 불안할수록 막대가 빠르고 들쭉날쭉하게 차고, 들여다보는 동안에는 초당 unwindPerSecond칸씩 풀린다.
    /// 신뢰가 깊어질수록 더 많이 감을 수 있다. 에디트 모드에서는 인스펙터 버튼이나 wound 값을 직접 바꾼다.
    /// </summary>
    [ExecuteAlways]
    public class WindingIntro : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("director")] private EyeMazeDirector _director;
        [SerializeField, FormerlySerializedAs("maze")] private IrisMaze _maze;
        [SerializeField, FormerlySerializedAs("eye")] private EyeVisual _eye;
        [SerializeField, FormerlySerializedAs("pupil")] private Pupil _pupil;
        [Tooltip("홍채 둘레의 톱니 (돌아가는 태엽)")]
        [SerializeField, FormerlySerializedAs("crown")] private LineRenderer _crown;
        [Tooltip("눈 둘레에 톱니 링을 그릴지 (끄면 왼쪽 톱니 아이콘만으로 보여줘요)")]
        [SerializeField, FormerlySerializedAs("showCrown")] private bool _showCrown = false;
        [Tooltip("톱니 바깥의 원호 = 남은 태엽 게이지")]
        [SerializeField, FormerlySerializedAs("gauge")] private LineRenderer _gauge;
        [SerializeField, FormerlySerializedAs("cam")] private Camera _cam;

        [Header("태엽")]
        [Tooltip("지금 감긴 칸 수")]
        [SerializeField, FormerlySerializedAs("wound")] private float _wound;
        [Tooltip("신뢰 0일 때 감을 수 있는 칸 수")]
        [SerializeField, FormerlySerializedAs("baseCapacity")] private int _baseCapacity = 14;
        [Tooltip("신뢰 1당 늘어나는 칸 수")]
        [SerializeField, FormerlySerializedAs("capacityPerTrust")] private int _capacityPerTrust = 5;
        [Tooltip("들여다보는 동안 초당 풀리는 칸 수")]
        [SerializeField, FormerlySerializedAs("unwindPerSecond")] private float _unwindPerSecond = 1f;
        [Tooltip("몇 칸 풀릴 때마다 눈을 깜빡이는지 (그때 2층이 돎)")]
        [SerializeField, FormerlySerializedAs("ticksPerBlink")] private int _ticksPerBlink = 4;
        [Tooltip("초점이 완전히 맞기까지 필요한 칸 수")]
        [SerializeField, FormerlySerializedAs("focusTicks")] private float _focusTicks = 3f;

        [Header("과하게 감기")]
        [Tooltip("가득 찬 뒤에도 감으면 쌓이는 긴장. 1이 되면 미끄러져 다 풀림")]
        [SerializeField, FormerlySerializedAs("strain"), Range(0f, 1f)] private float _strain;
        [SerializeField, FormerlySerializedAs("strainPerTick")] private float _strainPerTick = 0.45f;
        [Tooltip("손을 떼면 초당 풀리는 긴장")]
        [SerializeField, FormerlySerializedAs("strainRecovery")] private float _strainRecovery = 0.9f;
        [Tooltip("미끄러졌을 때 동공 변화")]
        [SerializeField, FormerlySerializedAs("slipDilation")] private float _slipDilation = -0.05f;

        [Header("누르고 있다가 떼기")]
        [Tooltip("끝으로 갈수록 뻑뻑해지는 정도")]
        [SerializeField, FormerlySerializedAs("resistance"), Range(0f, 0.9f)] private float _resistance = 0.4f;
        [Tooltip("목표선 위치 (막대 높이 비율)")]
        [SerializeField, FormerlySerializedAs("targetPoint"), Range(0.1f, 1f)] private float _targetPoint = 0.85f;
        [Tooltip("목표선에서 이만큼 멀어지면 가장 짧게 감김")]
        [SerializeField, FormerlySerializedAs("falloff"), Range(0.05f, 1f)] private float _falloff = 0.45f;
        [Tooltip("가장 못 맞췄을 때 감기는 비율")]
        [SerializeField, FormerlySerializedAs("minQuality"), Range(0f, 1f)] private float _minQuality = 0.15f;
        [Tooltip("불안할 때 막대가 0→끝까지 차는 시간(초) — 빠름")]
        [SerializeField, FormerlySerializedAs("anxiousFillSeconds")] private float _anxiousFillSeconds = 0.9f;
        [Tooltip("마음이 열렸을 때 막대가 차는 시간(초) — 느림")]
        [SerializeField, FormerlySerializedAs("calmFillSeconds")] private float _calmFillSeconds = 1.9f;
        [Tooltip("불안할 때 차오르는 속도가 들쭉날쭉한 정도")]
        [SerializeField, FormerlySerializedAs("anxiousJitter"), Range(0f, 1f)] private float _anxiousJitter = 0.6f;
        [Tooltip("이 이상 맞추면 '딱 맞았다'")]
        [SerializeField, FormerlySerializedAs("perfectQuality"), Range(0f, 1f)] private float _perfectQuality = 0.9f;
        [Tooltip("불안할 때 철컥 뒤 버틸 수 있는 시간(초)")]
        [SerializeField, FormerlySerializedAs("anxiousGrace")] private float _anxiousGrace = 0.18f;
        [Tooltip("마음이 열렸을 때 철컥 뒤 버틸 수 있는 시간(초)")]
        [SerializeField, FormerlySerializedAs("calmGrace")] private float _calmGrace = 0.6f;
        [Tooltip("이 시간보다 짧게 톡 누르면 감지 않고 바로 들여다봐요")]
        [SerializeField, FormerlySerializedAs("tapThreshold")] private float _tapThreshold = 0.15f;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("teeth")] private int _teeth = 40;
        [SerializeField, FormerlySerializedAs("crownGap")] private float _crownGap = 0.22f;
        [SerializeField, FormerlySerializedAs("toothDepth")] private float _toothDepth = 0.12f;
        [SerializeField, FormerlySerializedAs("crownColor")] private Color _crownColor = new Color(0.86f, 0.76f, 0.56f, 1f);
        [SerializeField, FormerlySerializedAs("strainColor")] private Color _strainColor = new Color(1f, 0.3f, 0.25f, 1f);
        [SerializeField, FormerlySerializedAs("gaugeColor")] private Color _gaugeColor = new Color(1f, 0.85f, 0.5f, 0.9f);
        [SerializeField, FormerlySerializedAs("shake")] private float _shake = 0.025f;

        [Header("문구")]
        [SerializeField, FormerlySerializedAs("firstPrompt"), TextArea(2, 4)] private string _firstPrompt = "Space를 누르고 있으면 태엽이 감긴다. 막대가 파란 선에 닿을 때 손을 떼면 가장 길게 감긴다. 떼는 순간 은재의 눈을 들여다본다.";
        [SerializeField, FormerlySerializedAs("againPrompt"), TextArea(2, 4)] private string _againPrompt = "다시 마주 앉았다. Space를 누르고 있다가 파란 선에서 뗀다.";
        [SerializeField, FormerlySerializedAs("fullMessage"), TextArea(1, 3)] private string _fullMessage = "막대가 끝까지 찼다. 지금 떼지 않으면 미끄러진다.";
        [SerializeField, FormerlySerializedAs("slipMessage"), TextArea(2, 3)] private string _slipMessage = "너무 세게 감았다. 태엽이 미끄러져 풀리고, 은재가 움찔했다.";
        [SerializeField, FormerlySerializedAs("emptyMessage"), TextArea(2, 3)] private string _emptyMessage = "태엽이 다 풀렸다. 은재가 눈을 감았다.";
        [SerializeField, FormerlySerializedAs("needWindMessage"), TextArea(1, 2)] private string _needWindMessage = "태엽이 풀려 있다. 먼저 감아야 한다.";
        [SerializeField, FormerlySerializedAs("perfectMessage"), TextArea(1, 2)] private string _perfectMessage = "딱 맞았다. 태엽이 단단히 감겼다.";
        [SerializeField, FormerlySerializedAs("goodMessage"), TextArea(1, 2)] private string _goodMessage = "조금 어긋났다. 태엽이 덜 감겼다.";
        [SerializeField, FormerlySerializedAs("poorMessage"), TextArea(1, 2)] private string _poorMessage = "많이 어긋났다. 태엽이 헐겁게 감겼다.";

        [NonSerialized] private int _lastWhole;
        [NonSerialized] private float _unwoundSinceBlink;
        [NonSerialized] private bool _wasFull;
        [NonSerialized] private bool _windingNow;
        [NonSerialized] private bool _holding;
        [NonSerialized] private bool _slippedThisHold;
        [NonSerialized] private float _holdTime;
        [NonSerialized] private bool _couldWindLastFrame;
        [NonSerialized] private float _holdProgress;
        [NonSerialized] private float _jitterPhase;
        [NonSerialized] private bool _passedTarget;
        [NonSerialized] private float _visualAngle;
        [NonSerialized] private float _prevMouseAngle;
        [NonSerialized] private bool _hasPrevMouse;
        [NonSerialized] private float _shakeAmt;
        [NonSerialized] private Vector3 _camBase;
        [NonSerialized] private bool _camBaseSet;
        [NonSerialized] private int _crownKey = int.MinValue;

        public EyeMazeDirector Director => _director;
        public IrisMaze Maze => _maze;
        public EyeVisual Eye => _eye;
        public float Wound { get => _wound; set => _wound = value; }
        public float Strain { get => _strain; set => _strain = value; }
        public string NeedWindMessage => Strings.Get("wind.need", _needWindMessage);
        public float TargetPoint => _targetPoint;
        public float UnwindPerSecond => _unwindPerSecond;
        public Color GaugeColor => _gaugeColor;
        public Color StrainColor => _strainColor;
        /// <summary>누르는 중의 막대 높이 (0~1)</summary>
        public float HoldProgress => _holdProgress;
        /// <summary>UI 막대에 보여줄 값: 누르는 중엔 차오르는 높이, 아닐 땐 남은 태엽</summary>
        public float Gauge01 => _holding && _holdTime >= _tapThreshold ? _holdProgress : Fill;

        public int Capacity
        {
            get
            {
                int trust = _director != null && _director.Person != null ? Mathf.Max(0, _director.Person.Trust) : 0;
                return Mathf.Max(1, _baseCapacity + _capacityPerTrust * trust);
            }
        }

        public bool IsFull => _wound >= Capacity - 1e-3f;

        /// <summary>0 = 불안, 1 = 마음이 열림. 동공 열림과 신뢰로 정해진다.</summary>
        public float Calm
        {
            get
            {
                Person p = _director != null ? _director.Person : null;
                if (p == null) return 0.5f;
                return Mathf.Clamp01(p.Dilation * 0.7f + p.Trust * 0.15f);
            }
        }

        /// <summary>막대가 차오르는 속도(초당 비율). 불안할수록 빠르고 들쭉날쭉.</summary>
        public float FillSpeed
        {
            get
            {
                float c = Calm;
                float baseSpeed = 1f / Mathf.Max(0.1f, Mathf.Lerp(_anxiousFillSeconds, _calmFillSeconds, c));
                float wobble = 1f + _anxiousJitter * (1f - c) * Mathf.Sin(_jitterPhase);
                return baseSpeed * Mathf.Max(0.2f, wobble);
            }
        }

        /// <summary>
        /// 뗀 지점의 정확도로 감기는 비율을 계산한다. 목표선에 가까울수록 1이다.
        /// p(뗀 높이 0~1)를 사용하며, 감기는 비율을 반환한다.
        /// </summary>
        public float QualityAt(float p)
        {
            float q = Mathf.Clamp01(1f - Mathf.Abs(p - _targetPoint) / Mathf.Max(0.01f, _falloff));
            return Mathf.Lerp(_minQuality, 1f, q);
        }
        public float Grace => Mathf.Lerp(_anxiousGrace, _calmGrace, Calm);
        public bool IsHolding => _holding;
        public bool CanGaze => _wound >= 1f;
        public float Fill => Mathf.Clamp01(_wound / Capacity);
        public float Focus
        {
            get
            {
                float w = _holding ? Mathf.Max(_wound, _holdProgress * Capacity) : _wound;
                float t = Mathf.Clamp01(w / Mathf.Max(0.1f, _focusTicks));
                return t * t * (3f - 2f * t);
            }
        }
        private float TickDeg => 360f / _teeth * 2f; // 한 칸은 톱니 두 개다.

        void OnValidate() => EyeHooks.Repaint();

        void Start()
        {
            if (!Application.isPlaying) return;
            _lastWhole = Mathf.FloorToInt(_wound);
        }

        /// <summary>
        /// 만남을 시작할 때 태엽이 풀린 상태(흐릿한 눈)로 되돌린다.
        /// first(처음 만나는지)를 사용하며, 태엽 상태를 초기화하고 안내 문구를 바꾼다.
        /// </summary>
        public void ResetForEncounter(bool first)
        {
            _wound = 0f;
            _strain = 0f;
            _wasFull = false;
            _unwoundSinceBlink = 0f;
            _hasPrevMouse = false;
            _lastWhole = 0;
            if (_cam != null) { _camBase = _cam.transform.position; _camBaseSet = true; }
            if (_director != null) _director.Message = first ? Strings.Get("wind.first", _firstPrompt) : Strings.Get("wind.again", _againPrompt);
        }

        /// <summary>
        /// 태엽을 ticks칸 감는다. 가득 찬 뒤에는 긴장이 쌓이다가 미끄러진다.
        /// ticks를 사용하며, _wound와 _strain을 바꾼다.
        /// </summary>
        public void Wind(float ticks)
        {
            if (ticks <= 0f) return;
            _windingNow = true;
            if (IsFull)
            {
                _strain += ticks * _strainPerTick;
                if (_strain >= 1f) Slip();
                return;
            }
            float cap = Capacity;
            float t = _wound / cap;
            _wound = Mathf.Min(cap, _wound + ticks * (1f - _resistance * t * t));
            if (IsFull && !_wasFull) OnFull();
        }

        /// <summary>
        /// 대화 결과 등으로 태엽을 더하거나(+) 푼다(-). 긴장은 쌓이지 않는다.
        /// ticks를 사용하며, _wound를 바꾼다.
        /// </summary>
        public void AddFree(float ticks)
        {
            _wound = Mathf.Clamp(_wound + ticks, 0f, Capacity);
            if (!IsFull) _wasFull = false;
        }

        /// <summary>
        /// 들여다보는 동안 태엽을 푼다. 정해진 칸마다 눈을 깜빡이고, 다 풀리면 시선을 거둔다.
        /// ticks를 사용하며, _wound와 깜빡임 누적량을 바꾼다.
        /// </summary>
        public void Unwind(float ticks)
        {
            if (ticks <= 0f || _wound <= 0f) return;
            float used = Mathf.Min(_wound, ticks);
            _wound -= used;
            _wasFull = false;
            _unwoundSinceBlink += used;
            if (_ticksPerBlink > 0 && _unwoundSinceBlink >= _ticksPerBlink)
            {
                _unwoundSinceBlink -= _ticksPerBlink;
                if (_director != null) _director.Blink();
            }
            if (_wound <= 1e-4f)
            {
                _wound = 0f;
                if (_director != null && _director.CurrentMode == EyeMazeDirector.Mode.Gaze) _director.ExitGaze(Strings.Get("wind.empty", _emptyMessage));
            }
        }

        /// <summary>
        /// 태엽을 가득 감는다. 에디트 모드 미리보기에서 쓴다.
        /// 입력값은 없으며, _wound를 용량만큼 채우고 긴장을 없앤다.
        /// </summary>
        public void FillUp()
        {
            _wound = Capacity;
            _wasFull = true;
            _strain = 0f;
        }

        /// <summary>
        /// 태엽이 가득 찬 순간의 반응을 낸다.
        /// 입력값은 없으며, 동공을 움찔하게 하고 안내 문구와 흔들림을 바꾼다.
        /// </summary>
        private void OnFull()
        {
            _wasFull = true;
            if (_pupil != null) _pupil.Pulse = 1f;
            if (_director != null) _director.Message = Strings.Get("wind.full", _fullMessage);
            _shakeAmt = _shake * 2f;
        }

        /// <summary>
        /// 너무 세게 감아 태엽이 미끄러진다.
        /// 입력값은 없으며, 태엽을 모두 풀고 상대의 동공을 줄이며 문구를 바꾼다.
        /// </summary>
        private void Slip()
        {
            _wound = 0f;
            _strain = 0f;
            _wasFull = false;
            _lastWhole = 0;
            if (_director != null)
            {
                if (_director.Person != null) _director.Person.Dilation = Mathf.Clamp01(_director.Person.Dilation + _slipDilation);
                _director.Message = Strings.Get("wind.slip", _slipMessage);
            }
            _shakeAmt = _shake * 3f;
        }

        void Update()
        {
            bool playing = Application.isPlaying;
            float dt = playing ? Time.deltaTime : 0f;
            _windingNow = false;

            bool canWind = _director != null && _director.AcceptInput && _director.CurrentMode == EyeMazeDirector.Mode.Talk;
            if (playing && canWind) HandleInput(dt);
            else if (_holding) { _holding = false; _slippedThisHold = false; } // 화면이 바뀌면 누르던 것은 취소한다.
            _couldWindLastFrame = canWind;

            if (playing && !_windingNow) _strain = Mathf.MoveTowards(_strain, 0f, _strainRecovery * dt);

            // 감기는 쪽으로 칸이 넘어갈 때 살짝 흔든다.
            int whole = Mathf.FloorToInt(_wound + 1e-4f);
            if (playing && whole != _lastWhole && !IsFull && !_holding)
            {
                if (whole > _lastWhole) _shakeAmt = Mathf.Max(_shakeAmt, _shake);
            }
            _lastWhole = whole;

            ApplyVisuals(dt, playing);
        }

        /// <summary>
        /// Space(패드 A)를 누르고 떼는 입력을 처리한다.
        /// dt를 사용하며, 누르는 중이면 Hold, 떼면 Release를 부른다.
        /// </summary>
        private void HandleInput(float dt)
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool down = (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed);

            // 시선을 거두려고 누른 Space가 감기로 이어지지 않게, 새로 누른 경우에만 시작한다.
            if (!_holding && pressed && _couldWindLastFrame)
            {
                _holding = true;
                _slippedThisHold = false;
                _holdTime = 0f;
            }

            if (_holding && down) Hold(dt);
            else if (_holding && !down) Release();
        }

        /// <summary>
        /// 누르고 있는 동안 막대를 채운다. 끝까지 차면 긴장이 쌓이다가 미끄러진다.
        /// dt를 사용하며, _holdProgress와 _strain을 바꾼다.
        /// </summary>
        private void Hold(float dt)
        {
            _holdTime += dt;
            if (_slippedThisHold || _holdTime < _tapThreshold) return;
            _windingNow = true;
            _jitterPhase += dt * 9f;

            if (_holdProgress < 1f)
            {
                float before = _holdProgress;
                _holdProgress = Mathf.Min(1f, _holdProgress + FillSpeed * dt);
                // 칸을 넘을 때 살짝 흔들고, 목표선을 지날 때 동공을 움찔하게 한다.
                int stepsBefore = Mathf.FloorToInt(before * 12f);
                int stepsNow = Mathf.FloorToInt(_holdProgress * 12f);
                if (stepsNow > stepsBefore) { _shakeAmt = Mathf.Max(_shakeAmt, _shake * 0.5f); }
                if (!_passedTarget && _holdProgress >= _targetPoint) { _passedTarget = true; if (_pupil != null) _pupil.Pulse = 0.6f; }
                if (_holdProgress >= 1f && _director != null) _director.Message = Strings.Get("wind.full", _fullMessage);
            }
            else
            {
                _strain += dt / Mathf.Max(0.05f, Grace);
                if (_strain >= 1f)
                {
                    Slip();
                    _slippedThisHold = true;
                    _holdProgress = 0f;
                }
            }
        }

        /// <summary>
        /// 뗐을 때 뗀 높이로 감긴 양을 정하고 바로 들여다본다. 톡 눌렀으면 남은 태엽으로 들여다본다.
        /// _holdProgress와 _holdTime을 사용하며, _wound와 디렉터의 모드, 문구를 바꾼다.
        /// </summary>
        private void Release()
        {
            _holding = false;
            bool slipped = _slippedThisHold;
            bool tapped = _holdTime < _tapThreshold;
            float p = _holdProgress;
            _slippedThisHold = false;
            _holdProgress = 0f;
            _passedTarget = false;
            _strain = 0f;
            if (slipped || _director == null) return;

            if (!tapped)
            {
                float q = QualityAt(p);
                _wound = Capacity * q;
                _wasFull = false;
                _director.Message = q >= _perfectQuality ? Strings.Get("wind.perfect", _perfectMessage) : (q >= 0.55f ? Strings.Get("wind.good", _goodMessage) : Strings.Get("wind.poor", _poorMessage));
                if (q >= _perfectQuality && _pupil != null) _pupil.Pulse = 1f;
            }

            if (CanGaze)
            {
                string keep = _director.Message;
                _director.ToggleGaze();
                if (!tapped) _director.Message = keep;
            }
            else _director.Message = Strings.Get("wind.need", _needWindMessage);
        }

        /// <summary>
        /// 태엽 상태를 눈의 초점, 톱니, 게이지, 카메라 흔들림에 반영한다.
        /// dt, playing을 사용하며, 미로와 눈의 초점, 톱니 회전, 게이지 그림, 카메라 위치를 바꾼다.
        /// </summary>
        private void ApplyVisuals(float dt, bool playing)
        {
            float focus = Focus;
            if (_maze != null && !Mathf.Approximately(_maze.Focus, focus)) _maze.Focus = focus;
            if (_eye != null && !Mathf.Approximately(_eye.Focus, focus)) _eye.Focus = focus;

            // 톱니는 한 칸 사이에서는 30%만 따라오다가 칸을 넘으면 넘어간다. 긴장이 쌓이면 떨린다.
            float shown = _holding ? _holdProgress * Capacity : _wound;
            int t = Mathf.FloorToInt(shown + 1e-4f);
            float frac = shown - t;
            float target = -(t + Mathf.Clamp01(frac) * 0.3f) * TickDeg;
            if (_strain > 0f && playing) target += Mathf.Sin(Time.time * 60f) * _strain * 1.5f;
            _visualAngle = playing ? Mathf.Lerp(_visualAngle, target, 1f - Mathf.Exp(-30f * dt)) : target;

            float irisRadius = _maze != null ? _maze.OuterRadius : 3f;
            if (_crown != null)
            {
                int key = HashCode.Combine(_teeth, _crownGap, _toothDepth, irisRadius);
                if (key != _crownKey)
                {
                    _crownKey = key;
                    int n = _teeth * 2;
                    var pts = new Vector3[n];
                    for (int i = 0; i < n; i++)
                        pts[i] = Shapes.Polar(irisRadius + _crownGap + (i % 2 == 0 ? _toothDepth : 0f), 360f * i / n);
                    Shapes.SetPoints(_crown, pts, true);
                    _crown.widthMultiplier = 0.05f;
                    _crown.sortingOrder = -2;
                }
                var c = Color.Lerp(_crownColor, _strainColor, _strain);
                c.a *= Mathf.Lerp(0.45f, 1f, Mathf.Max(focus, 0.3f));
                Shapes.SetColor(_crown, c);
                if (_crown.enabled != _showCrown) _crown.enabled = _showCrown;
                var rot = Quaternion.Euler(0f, 0f, _visualAngle);
                if (_crown.transform.localRotation != rot) _crown.transform.localRotation = rot;
            }

            // 남은 태엽 게이지는 12시 방향에서 시계 방향으로 줄어드는 원호다.
            if (_gauge != null)
            {
                float f = Fill;
                bool show = f > 0.001f;
                if (_gauge.enabled != show) _gauge.enabled = show;
                if (show)
                {
                    int seg = Mathf.Max(2, Mathf.CeilToInt(64 * f));
                    Shapes.SetArc(_gauge, irisRadius + _crownGap + _toothDepth + 0.14f, 90f, 90f - 360f * f * 0.999f, seg);
                    _gauge.widthMultiplier = 0.07f;
                    var gc = Color.Lerp(_gaugeColor, _strainColor, _strain);
                    Shapes.SetColor(_gauge, gc);
                    _gauge.sortingOrder = -1;
                }
            }

            // 흔들림이 있을 때만 카메라를 건드린다. 평소에는 GameFlow가 카메라를 움직인다.
            if (playing && _cam != null && _camBaseSet && _shakeAmt > 0f)
            {
                _shakeAmt = Mathf.MoveTowards(_shakeAmt, 0f, dt * 0.25f);
                _cam.transform.position = _camBase + (Vector3)(UnityEngine.Random.insideUnitCircle * _shakeAmt);
            }
        }
    }
}
