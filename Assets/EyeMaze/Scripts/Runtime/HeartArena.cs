using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

namespace EyeMaze
{
    /// <summary>
    /// 미로 없이 눈 하나로 하는 리듬 판. 홍채 전체가 빈 판이 되고, 동공 한가운데의 진심에 닿으면 끝난다.
    /// 손님의 심장 박동이 곡의 박자가 되고, 공격, 동공 둘레의 고리, 소리가 모두 그 박자에 맞춰 온다.
    /// 공격은 두 가지다. 고리는 받아낼 것이다. 시선을 둘러싸고 일정한 빠르기로 좁혀져 정확히 박자에 기준 원과 겹치고, 그 박자에 Space로 받아내면 공명이 쌓인다.
    /// 가시는 피할 것이다. 박자에 맞춰 동공에서 뿜어져 나오고, 움직여서 피해야 한다. Space로는 막을 수 없다.
    /// 공명이 차면 감정이 한 단계 가라앉고, 평온과 안도에서는 박자에 맞춰 Space를 누를 때마다 동공이 열려 안으로 들어갈 수 있다.
    /// 좌표는 모두 미로 기준(로컬)이다.
    /// </summary>
    public class HeartArena : MonoBehaviour
    {
        private const int CIRCLE_SEGMENTS = 48;
        private const int SORTING_ORDER = 12;
        private const float FLASH_SECONDS = 0.3f;
        private const int SAMPLE_RATE = 44100;

        [Header("연결")]
        [SerializeField] private EyeMazeDirector _director;
        [SerializeField] private Pupil _pupil;

        [Header("판")]
        [Tooltip("켜면 미로 벽과 문을 숨기고 홍채 전체를 빈 판으로 쓴다")]
        [SerializeField] private bool _arenaMode = true;
        [Tooltip("F1~F5로 감정을 바꾸는 테스트 키를 받는다 (같은 키를 다시 누르면 2단계)")]
        [SerializeField] private bool _testKeys = true;
        [Tooltip("시선이 홍채 바깥 테두리와 닫힌 동공 가장자리에서 떨어져 있는 거리")]
        [SerializeField] private float _edgeMargin = 0.08f;
        [Tooltip("동공이 열렸을 때 진심에 닿는 거리 (동공 가운데에서)")]
        [SerializeField] private float _coreTouchRadius = 0.2f;

        [Header("박자")]
        [Tooltip("감정별 빠르기(BPM). 평온, 경계, 불안, 슬픔, 안도 순서")]
        [SerializeField] private float[] _bpm = { 80f, 112f, 124f, 66f, 92f };
        [Tooltip("2단계 감정의 빠르기 배율")]
        [SerializeField] private float _stage2BpmScale = 1.15f;
        [Tooltip("한 마디의 박 수. 마디 첫 박은 더 크게 울린다")]
        [SerializeField] private int _beatsPerBar = 4;
        [Tooltip("박자에 맞았다고 보는 앞뒤 시간(초)")]
        [SerializeField] private float _judgeWindow = 0.15f;
        [Tooltip("판정을 이만큼(밀리초) 늦춘다. 소리나 화면이 늦게 나와 늘 늦게 누르게 되면 키운다")]
        [SerializeField] private float _inputOffsetMs = 0f;
        [Tooltip("안도일 때 판정 시간 배율")]
        [SerializeField] private float _reliefWindowScale = 1.6f;
        [Tooltip("박자가 아닌 때 누르면 이 시간(초) 동안 받아내기를 받지 않는다 (마구 누르기 막기)")]
        [SerializeField] private float _missLockSeconds = 0.2f;

        [Header("공명과 동공")]
        [Tooltip("긴장한 감정이 한 단계 가라앉는 데 필요한 공명")]
        [SerializeField] private int _resonanceToCalm = 4;
        [Tooltip("평온, 안도에서 박자를 맞출 때마다 동공이 열리는 양")]
        [SerializeField, Range(0f, 1f)] private float _openPerBeat = 0.1f;
        [Tooltip("동공이 이만큼 열리면 안으로 들어갈 수 있다")]
        [SerializeField, Range(0f, 1f)] private float _openThreshold = 0.75f;

        [Header("고리: 받아낼 것")]
        [Tooltip("고리가 나타나서 기준 원에 닿기까지의 박 수")]
        [SerializeField] private float _leadBeats = 2f;
        [Tooltip("고리가 처음 나타날 때의 반지름 (시선 둘레)")]
        [SerializeField] private float _ringStartRadius = 0.9f;
        [Tooltip("시선 둘레 기준 원의 반지름. 좁혀지는 고리가 이 원과 겹치는 순간이 박이다")]
        [SerializeField] private float _ringCloseRadius = 0.14f;
        [Tooltip("경계에서 몇 박마다 고리가 오는지 (1단계, 2단계)")]
        [SerializeField] private int _guardRingEvery = 2;
        [SerializeField] private int _guardRingEvery2 = 1;
        [Tooltip("불안에서 몇 박마다 엇박 고리가 오는지 (1단계, 2단계)")]
        [SerializeField] private int _anxiousRingEvery = 2;
        [SerializeField] private int _anxiousRingEvery2 = 1;
        [Tooltip("슬픔에서 몇 박마다 누른 채 버틸 고리가 오는지 (1단계, 2단계)")]
        [SerializeField] private int _sadRingEvery = 4;
        [SerializeField] private int _sadRingEvery2 = 2;
        [Tooltip("받아내지 못한 고리에 밀려나는 거리")]
        [SerializeField] private float _ringPush = 0.4f;
        [SerializeField] private float _knockSeconds = 0.2f;
        [SerializeField] private Color _ringColor = new Color(1f, 0.97f, 0.9f, 1f);
        [SerializeField] private Color _holdRingColor = new Color(0.45f, 0.6f, 1f, 1f);

        [Header("가시: 피할 것")]
        [SerializeField] private float _spikeSpeed = 1.6f;
        [SerializeField] private float _spikeHitRadius = 0.09f;
        [SerializeField] private float _spikeLength = 0.18f;
        [Tooltip("가시에 맞은 뒤 다시 맞지 않는 시간(초)")]
        [SerializeField] private float _invulnSeconds = 0.7f;
        [SerializeField] private float _spikePush = 0.35f;
        [Tooltip("경계에서 몇 박마다 시선 쪽으로 부채꼴 가시를 뿜는지. 0이면 뿜지 않는다")]
        [SerializeField] private int _guardSpikeEvery = 2;
        [Tooltip("경계 부채꼴 가시 수 (1단계, 2단계)")]
        [SerializeField] private int _guardFan = 3;
        [SerializeField] private int _guardFan2 = 5;
        [Tooltip("부채꼴 가시 사이 각도")]
        [SerializeField] private float _guardSpreadDeg = 14f;
        [Tooltip("불안에서 몇 박마다 사방으로 가시를 뿜는지. 0이면 뿜지 않는다")]
        [SerializeField] private int _anxiousSpikeEvery = 2;
        [Tooltip("불안 사방 가시 수 (1단계, 2단계)")]
        [SerializeField] private int _anxiousBurst = 6;
        [SerializeField] private int _anxiousBurst2 = 10;
        [Tooltip("슬픔에서 몇 박마다 가시를 뿜는지. 0이면 뿜지 않는다")]
        [SerializeField] private int _sadSpikeEvery = 0;
        [SerializeField] private Color _spikeColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Header("불안: 피하는 동공")]
        [SerializeField] private float _dodgeDistance = 0.45f;
        [SerializeField] private float _dodgeDistance2 = 0.75f;
        [SerializeField] private float _dodgeSpeed = 1.2f;
        [SerializeField] private float _dodgeSpeed2 = 2.4f;
        [Tooltip("시선이 동공 가장자리에서 이 거리 안에 오면 피하기 시작한다")]
        [SerializeField] private float _dodgeSenseRange = 1.6f;
        [Tooltip("시선이 동공 가장자리에서 이 거리 안에서 박자에 누르면 안심시켜 공명이 쌓인다")]
        [SerializeField] private float _reassureRange = 0.35f;

        [Header("타격감")]
        [Tooltip("박자와 이 시간(초) 안으로 어긋나면 완벽으로 본다")]
        [SerializeField] private float _perfectWindow = 0.05f;
        [Tooltip("받아내거나 맞을 때 터지는 고리와 불꽃이 퍼지는 시간(초)")]
        [SerializeField] private float _sparkSeconds = 0.4f;
        [Tooltip("고리와 불꽃이 퍼지는 거리")]
        [SerializeField] private float _sparkRadius = 0.4f;
        [Tooltip("불꽃 줄기 수")]
        [SerializeField] private int _sparkRays = 8;
        [SerializeField] private Color _perfectColor = new Color(1f, 0.85f, 0.3f, 1f);
        [Tooltip("이 콤보마다 동공 둘레에서 큰 고리가 터진다")]
        [SerializeField] private int _comboBurstEvery = 5;
        [Tooltip("화면(눈 전체)이 흔들리는 시간(초)")]
        [SerializeField] private float _shakeSeconds = 0.25f;
        [Tooltip("맞았을 때 흔들리는 거리")]
        [SerializeField] private float _shakeHit = 0.12f;
        [Tooltip("완벽으로 받아냈을 때 흔들리는 거리")]
        [SerializeField] private float _shakePerfect = 0.03f;
        [Tooltip("감정이 가라앉을 때 흔들리는 거리")]
        [SerializeField] private float _shakeCalm = 0.08f;

        [Header("그림")]
        [SerializeField] private Color _beatColor = new Color(1f, 0.95f, 0.8f, 0.55f);
        [SerializeField] private Color _goodColor = new Color(0.55f, 1f, 0.7f, 1f);
        [SerializeField] private Color _missColor = new Color(1f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color _openColor = new Color(0.55f, 0.85f, 1f, 0.8f);

        [Header("소리")]
        [Tooltip("박자 소리를 낸다. 소리 파일 없이 코드로 만든 짧은 음을 쓴다")]
        [SerializeField] private bool _sound = true;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.35f;

        [Header("문구")]
        [Tooltip("감정 이름. 평온, 경계, 불안, 슬픔, 안도 순서. 공용 문구 emotion.0~4가 있으면 그걸 쓴다")]
        [SerializeField] private string[] _emotionNames = { "평온", "경계", "불안", "슬픔", "안도" };
        [Tooltip("2단계 이름. 평온은 비워 둔다. 공용 문구 emotion2.1~4가 있으면 그걸 쓴다")]
        [SerializeField] private string[] _stage2Names = { "", "반감", "동요", "침잠", "열망" };
        [SerializeField, TextArea(1, 3)] private string _statusMessage = "{emotion} ♪{bpm}  ·  공명 {res}/{need}  ·  동공 {open}%  {combo}\n{event}";
        [SerializeField] private string _guideEvent = "고리가 닿는 박자에 Space, 붉은 가시는 피하자.";
        [SerializeField] private string _anxiousGuideEvent = "피하는 동공에 다가가 박자에 Space로 안심시키자.";
        [SerializeField] private string _sadGuideEvent = "파란 고리는 닿기 직전 Space를 눌러 쥐고 버티자.";
        [SerializeField] private string _calmGuideEvent = "박자에 맞춰 Space를 누르면 동공이 열린다.";
        [SerializeField] private string _perfectEvent = "완벽!";
        [SerializeField] private string _earlyEvent = "좋음 · 조금 빠름";
        [SerializeField] private string _lateEvent = "좋음 · 조금 늦음";
        [SerializeField] private string _holdEvent = "버텼다.";
        [SerializeField] private string _reassureEvent = "안심시켰다.";
        [SerializeField] private string _missEvent = "박자가 어긋났다.";
        [SerializeField] private string _ringMissEvent = "고리를 놓쳤다.";
        [SerializeField] private string _spikeHitEvent = "가시에 맞았다.";
        [SerializeField] private string _calmedEvent = "마음이 조금 가라앉았다.";
        [SerializeField] private string _closedEvent = "동공이 닫혀 있다. 박자를 맞춰 열어 주자.";
        [SerializeField] private string _openedEvent = "동공이 열렸다. 안으로 들어가자.";
        [SerializeField] private string _testEmotionEvent = "(테스트) 감정: {emotion}";
        [Tooltip("{combo} = 콤보. 2 이상일 때만 상태 줄에 붙는다")]
        [SerializeField] private string _comboText = "콤보 {combo}";

        private class Ring
        {
            public float SpawnBeat;
            public float ImpactBeat;
            public bool Hold;
            public bool Resolved;
            public float ResolvedAt = -1f;
            public bool Parried;
            public LineRenderer Line;
            // 시선 둘레의 기준 원. 좁혀지는 고리가 이 원과 겹치는 순간이 박이고, 박마다 뛰며 남은 박을 센다.
            public LineRenderer Target;
        }

        private class Spike
        {
            public Vector2 Position;
            public Vector2 Direction;
            public LineRenderer Line;
        }

        private class Spark
        {
            public Vector2 Center;
            public float Start;
            public float Size;
            public float BaseRadius;
            public Color Color;
            public LineRenderer Ring;
            public LineRenderer[] Rays;
        }

        private readonly List<Ring> _rings = new List<Ring>();
        private readonly List<Spike> _spikes = new List<Spike>();
        private readonly List<Spark> _sparks = new List<Spark>();
        // 박자 소리를 정확한 때에 예약해 두는 소리 원본들. 돌아가며 쓴다.
        private readonly AudioSource[] _clickSources = new AudioSource[4];
        private int _clickIndex;
        private int _nextClickBeat;
        private AudioSource _audio;
        private AudioClip _tick;
        private AudioClip _accent;
        private AudioClip _cue;
        private AudioClip _chime;
        private AudioClip _thud;
        private AudioClip _burst;
        private IrisMaze _maze;
        private LineRenderer _beatRing;
        private Transform _eye;
        private Vector3 _eyeRest;
        private bool _arenaApplied;
        private bool _outside = true;
        private ClientEmotion _lastEmotion;
        private int _stage = 1;
        private float _beat;
        private int _lastWholeBeat;
        private int _nextRingBeat;
        private int _nextSpikeBeat;
        private int _burstCount;
        private int _lastGoodBeat = int.MinValue;
        private float _lockUntilBeat;
        // 마지막으로 Space를 누른 박. 버티는 고리에 쓴다.
        private float _pressBeat = float.MinValue;
        private int _resonance;
        private int _combo;
        private int _bestCombo;
        private float _flashT = -1f;
        private Color _flashColor;
        private Vector2 _knock;
        private float _knockT;
        private float _invulnT;
        private Vector2 _dodge;
        private float _shakeT;
        private float _shakeAmp;
        private bool _wasOpen;
        private string _event = "";

        /// <summary>미로 없이 눈 하나로 하는 판이 켜져 있는지</summary>
        public bool ArenaOn => _arenaMode;
        /// <summary>이번 판에서 놓치거나 맞은 횟수</summary>
        public int Hits { get; private set; }
        /// <summary>이번 판의 최고 콤보</summary>
        public int BestCombo => _bestCombo;

        void Start()
        {
            _maze = _director.Maze;
            _beatRing = MakeLine("박자 고리");
            _eye = _pupil.transform.parent;
            _eyeRest = _eye.localPosition;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            for (int i = 0; i < _clickSources.Length; i++)
            {
                _clickSources[i] = gameObject.AddComponent<AudioSource>();
                _clickSources[i].playOnAwake = false;
            }
            _tick = MakeTone("박", 660f, 0.05f, 0.5f);
            _accent = MakeTone("마디", 880f, 0.07f, 0.8f);
            _cue = MakeTone("고리", 990f, 0.09f, 0.9f);
            _chime = MakeTone("받아냄", 1320f, 0.18f, 0.7f);
            _thud = MakeTone("맞음", 110f, 0.2f, 1f);
            _burst = MakeTone("가시", 220f, 0.08f, 0.6f);
            ApplyArenaMode();
        }

        void Update()
        {
            if (_arenaApplied != _arenaMode) ApplyArenaMode();
            if (!_arenaMode) return;
            // 불꽃과 흔들림은 탈출이나 리셋 뒤에도 끝까지 진행한다.
            TickSparks();
            TickShake(Time.deltaTime);
            // 대화 상태가 되면 리셋된 것이므로, 다시 들여다볼 때 판을 처음 상태로 둔다.
            if (_director.CurrentMode != EyeMazeDirector.Mode.Gaze)
            {
                _outside = true;
                ClearAttacks();
                _beatRing.enabled = false;
                return;
            }
            if (_outside) ResetRound();
            _outside = false;
            // 진심에 닿아 탈출 안내가 나온 뒤에는 판을 멈추고 문구를 덮어쓰지 않는다.
            if (!_director.AcceptInput) { ClearAttacks(); return; }

            float dt = Time.deltaTime;
            TrackEmotion();
            if (_testKeys) ReadTestKeys();
            AdvanceBeat(dt);
            ReadPress();
            TickRings();
            TickSpikes(dt);
            TickDodge(dt);
            TickKnock(dt);
            CheckCore();
            DrawBeatRing();
            WriteStatus();
        }

        /// <summary>
        /// 판 모드를 켜거나 끈다. 켜면 미로 층을 숨기고 시선 이동을 판 규칙으로 바꾸며, 끄면 되돌린다.
        /// _arenaMode를 사용하며, 미로 층 오브젝트의 활성 상태, 디렉터의 ArenaMove, 동공의 회피 거리, 눈 위치를 바꾼다.
        /// </summary>
        private void ApplyArenaMode()
        {
            _arenaApplied = _arenaMode;
            foreach (MazeLayer layer in _maze.Layers) layer.gameObject.SetActive(!_arenaMode);
            _director.ArenaMove = _arenaMode ? (System.Func<Vector2, Vector2, Vector2>)MoveGaze : null;
            if (_arenaMode) return;
            ClearAttacks();
            foreach (Spark s in _sparks) DestroySpark(s);
            _sparks.Clear();
            _shakeT = 0f;
            _eye.localPosition = _eyeRest;
            _beatRing.enabled = false;
            _dodge = Vector2.zero;
            _pupil.DodgeShift = Vector2.zero;
        }

        /// <summary>
        /// 판을 처음 상태로 둔다. 시작할 때와 리셋 뒤 다시 들여다볼 때 쓴다.
        /// 입력값은 없으며, 단계, 박자, 공명, 콤보, 맞은 횟수, 공격, 회피, 문구 상태를 초기화한다.
        /// </summary>
        private void ResetRound()
        {
            _lastEmotion = _director.Emotion;
            _pressBeat = float.MinValue;
            Hits = 0;
            _beat = 0f;
            _lastWholeBeat = 0;
            _nextClickBeat = 0;
            _knockT = 0f;
            _invulnT = 0f;
            _dodge = Vector2.zero;
            _pupil.DodgeShift = Vector2.zero;
            _wasOpen = false;
            _combo = 0;
            _bestCombo = 0;
            OnEmotionChanged(1);
            _event = Guide();
        }

        /// <summary>
        /// 대화나 다른 곳에서 감정이 바뀌었으면 그 감정의 1단계로 둔다.
        /// 디렉터의 지금 감정과 _lastEmotion을 사용하며, _stage와 _lastEmotion 등 판 상태를 바꾼다.
        /// </summary>
        private void TrackEmotion()
        {
            if (_director.Emotion == _lastEmotion) return;
            _lastEmotion = _director.Emotion;
            OnEmotionChanged(1);
            _event = Guide();
        }

        /// <summary>
        /// 감정이나 단계가 바뀐 뒤 판 상태를 맞춘다. 진행 중인 공격을 지우고, 다음 마디 첫 박부터 새 공격을 시작한다.
        /// stage를 사용하며, _stage, _resonance, _nextRingBeat, _nextSpikeBeat, _lockUntilBeat를 바꾼다.
        /// </summary>
        private void OnEmotionChanged(int stage)
        {
            _stage = stage;
            _resonance = 0;
            _lockUntilBeat = 0f;
            ClearAttacks();
            int bar = Mathf.Max(1, _beatsPerBar);
            int next = (Mathf.FloorToInt(_beat) / bar + 1) * bar;
            _nextRingBeat = next;
            // 가시는 고리보다 한 박 늦게 시작해 처음 고리를 읽을 틈을 준다.
            _nextSpikeBeat = next + 1;
        }

        /// <summary>
        /// F1~F5로 감정을 바꾼다. 지금 감정과 같은 키를 다시 누르면 2단계가 된다.
        /// 키 입력과 디렉터의 지금 감정을 사용하며, 디렉터의 감정, _stage, _lastEmotion, 문구를 바꾼다.
        /// </summary>
        private void ReadTestKeys()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            ClientEmotion? pick = null;
            if (kb.f1Key.wasPressedThisFrame) pick = ClientEmotion.Calm;
            else if (kb.f2Key.wasPressedThisFrame) pick = ClientEmotion.Guarded;
            else if (kb.f3Key.wasPressedThisFrame) pick = ClientEmotion.Anxious;
            else if (kb.f4Key.wasPressedThisFrame) pick = ClientEmotion.Sad;
            else if (kb.f5Key.wasPressedThisFrame) pick = ClientEmotion.Relieved;
            if (pick == null) return;

            ClientEmotion e = pick.Value;
            bool again = e == _director.Emotion && e != ClientEmotion.Calm;
            if (!again) _director.SetEmotion(e);
            _lastEmotion = e;
            OnEmotionChanged(again ? 2 : 1);
            _event = Strings.Get("arena.test_emotion", _testEmotionEvent, ("emotion", EmotionName())) + "  " + Guide();
        }

        /// <summary>
        /// 박자를 한 프레임 진행한다. 새 박에 들어서면 정해진 박이면 고리를 보내고 가시를 뿜는다.
        /// dt와 지금 빠르기를 사용하며, _beat, _lastWholeBeat, 고리와 가시 목록을 바꾼다.
        /// </summary>
        private void AdvanceBeat(float dt)
        {
            _beat += dt * Bpm() / 60f;
            int whole = Mathf.FloorToInt(_beat);
            while (_lastWholeBeat < whole)
            {
                _lastWholeBeat++;
                if (_lastWholeBeat >= _nextRingBeat) SpawnRing(_lastWholeBeat);
                if (_lastWholeBeat >= _nextSpikeBeat) SpawnSpikes(_lastWholeBeat);
            }
            ScheduleClicks();
        }

        /// <summary>
        /// 지금 감정에 맞는 고리를 보내고 다음 고리 박을 정한다. 긴장한 감정이 아니면 보내지 않는다.
        /// beat(보내는 박), 지금 감정, _stage, 고리 설정을 사용하며, 고리 목록과 _nextRingBeat를 바꾼다.
        /// </summary>
        private void SpawnRing(int beat)
        {
            bool second = _stage >= 2;
            switch (_director.Emotion)
            {
                case ClientEmotion.Guarded:
                    AddRing(beat, beat + _leadBeats, false);
                    _nextRingBeat = beat + Mathf.Max(1, second ? _guardRingEvery2 : _guardRingEvery);
                    break;
                case ClientEmotion.Anxious:
                    // 반 박 어긋난 엇박에 닿는다.
                    AddRing(beat, beat + _leadBeats + 0.5f, false);
                    _nextRingBeat = beat + Mathf.Max(1, second ? _anxiousRingEvery2 : _anxiousRingEvery);
                    break;
                case ClientEmotion.Sad:
                    AddRing(beat, beat + _leadBeats, true);
                    _nextRingBeat = beat + Mathf.Max(1, second ? _sadRingEvery2 : _sadRingEvery);
                    break;
                default:
                    _nextRingBeat = beat + 1;
                    break;
            }
        }

        /// <summary>
        /// 시선 둘레에서 좁혀져 닿는 박에 기준 원과 겹칠 고리 하나를 보낸다. 닿는 순간의 소리를 예약한다.
        /// spawn, impact(박), hold(누른 채 버티는 고리인지)를 사용하며, 고리 목록에 더한다.
        /// </summary>
        private void AddRing(float spawn, float impact, bool hold)
        {
            var ring = new Ring { SpawnBeat = spawn, ImpactBeat = impact, Hold = hold, Line = MakeLine("고리"), Target = MakeLine("고리 닿을 자리") };
            _rings.Add(ring);
            ScheduleCue(ring.Line.gameObject, impact);
        }

        /// <summary>
        /// 지금 감정에 맞게 동공에서 가시를 뿜고 다음 가시 박을 정한다. 경계는 시선 쪽 부채꼴, 불안은 사방으로 돌아가며 뿜는다.
        /// beat(뿜는 박), 지금 감정, _stage, 가시 설정을 사용하며, 가시 목록, _nextSpikeBeat, _burstCount를 바꾼다.
        /// </summary>
        private void SpawnSpikes(int beat)
        {
            bool second = _stage >= 2;
            Vector2 from = PupilCenter();
            Vector2 aim = (Vector2)_director.Cursor.LocalPos - from;
            if (aim.sqrMagnitude < 1e-6f) aim = Vector2.right;
            aim.Normalize();
            int every;
            switch (_director.Emotion)
            {
                case ClientEmotion.Guarded:
                    every = _guardSpikeEvery;
                    if (every <= 0) break;
                    int fan = second ? _guardFan2 : _guardFan;
                    for (int k = 0; k < fan; k++) AddSpike(from, Rotate(aim, (k - (fan - 1) * 0.5f) * _guardSpreadDeg));
                    break;
                case ClientEmotion.Anxious:
                    every = _anxiousSpikeEvery;
                    if (every <= 0) break;
                    int count = Mathf.Max(1, second ? _anxiousBurst2 : _anxiousBurst);
                    // 뿜을 때마다 반 칸씩 돌려 같은 자리에 숨지 못하게 한다.
                    float offset = (_burstCount % 2) * 180f / count;
                    for (int k = 0; k < count; k++) AddSpike(from, Rotate(Vector2.right, offset + 360f * k / count));
                    break;
                case ClientEmotion.Sad:
                    every = _sadSpikeEvery;
                    if (every <= 0) break;
                    AddSpike(from, aim);
                    break;
                default:
                    every = 0;
                    break;
            }
            if (every <= 0) { _nextSpikeBeat = beat + 1; return; }
            _burstCount++;
            _nextSpikeBeat = beat + every;
            Play(_burst);
            // 뿜는 순간 동공 둘레가 붉게 번쩍인다.
            Burst(from, _spikeColor, 0.5f, PupilRadius());
        }

        /// <summary>
        /// 동공 가장자리에서 direction 쪽으로 날아가는 가시 하나를 만든다.
        /// from(동공 가운데)과 direction을 사용하며, 가시 목록에 더한다.
        /// </summary>
        private void AddSpike(Vector2 from, Vector2 direction)
        {
            LineRenderer line = MakeLine("가시");
            Shapes.SetColor(line, _spikeColor);
            _spikes.Add(new Spike { Position = from + direction * PupilRadius(), Direction = direction, Line = line });
        }

        /// <summary>
        /// 곧 올 박의 소리를 오디오 시계로 미리 예약한다. 프레임을 기다리지 않아 박과 소리가 어긋나지 않는다.
        /// 지금 박자, 빠르기, _nextClickBeat를 사용하며, 소리 원본의 예약과 _nextClickBeat, _clickIndex를 바꾼다.
        /// </summary>
        private void ScheduleClicks()
        {
            const float LOOKAHEAD = 0.1f;
            double now = AudioSettings.dspTime;
            if (_nextClickBeat <= Mathf.FloorToInt(_beat)) _nextClickBeat = Mathf.FloorToInt(_beat) + 1;
            while (true)
            {
                float seconds = (_nextClickBeat - _beat) * SecondsPerBeat();
                if (seconds > LOOKAHEAD) return;
                bool downbeat = _nextClickBeat % Mathf.Max(1, _beatsPerBar) == 0;
                if (_sound)
                {
                    AudioSource source = _clickSources[_clickIndex];
                    _clickIndex = (_clickIndex + 1) % _clickSources.Length;
                    source.clip = downbeat ? _accent : _tick;
                    source.volume = _volume;
                    source.PlayScheduled(now + seconds);
                }
                _nextClickBeat++;
            }
        }

        /// <summary>
        /// 고리가 닿는 박에 맞춰 소리를 예약한다. 엇박 고리도 닿는 순간이 들린다. 고리가 지워지면 소리도 함께 사라진다.
        /// owner(소리를 붙일 오브젝트)와 impact(박), 지금 박자, 빠르기를 사용하며, owner에 소리 원본을 붙여 예약한다.
        /// </summary>
        private void ScheduleCue(GameObject owner, float impact)
        {
            if (!_sound) return;
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = _cue;
            source.volume = _volume;
            source.PlayScheduled(AudioSettings.dspTime + (impact - _beat) * SecondsPerBeat());
        }

        /// <summary>
        /// 판정에 쓰는 박자를 계산한다. 소리와 화면이 늦게 나오는 만큼 _inputOffsetMs로 늦춰 본다.
        /// 지금 박자, 빠르기, _inputOffsetMs를 사용하며, 판정용 박자를 반환한다.
        /// </summary>
        private float JudgeBeat() => _beat - _inputOffsetMs * 0.001f / SecondsPerBeat();

        /// <summary>
        /// Space 입력을 판정한다. 긴장한 감정에서는 닿는 고리를 받아내고(불안은 동공 곁에서 안심시키기도 된다), 평온·안도에서는 박자를 맞춰 동공을 연다.
        /// 키 입력, 지금 박자, 고리 목록, 판정 시간을 사용하며, 공명, 콤보, 동공 열림, 고리 상태, 문구를 바꾼다.
        /// </summary>
        private void ReadPress()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool press = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (!press) return;
            _pressBeat = _beat;

            if (!IsTense()) { PressOnBeat(); return; }
            if (_beat < _lockUntilBeat) return;
            float judge = JudgeBeat();
            Ring target = null;
            float best = float.MaxValue;
            foreach (Ring r in _rings)
            {
                if (r.Resolved || r.Hold) continue;
                float seconds = Mathf.Abs(r.ImpactBeat - judge) * SecondsPerBeat();
                if (seconds > _judgeWindow || seconds >= best) continue;
                best = seconds;
                target = r;
            }
            if (target != null)
            {
                // 닿는 박보다 일찍 눌렀으면 양수다.
                float error = (target.ImpactBeat - judge) * SecondsPerBeat();
                ParryRing(target, JudgeText(error), error);
                Burst(_director.Cursor.LocalPos, IsPerfect(error) ? _perfectColor : _goodColor, IsPerfect(error) ? 1.3f : 1f, 0f);
                return;
            }
            if (_director.Emotion == ClientEmotion.Anxious && TryReassure(judge)) return;
            // 버티는 고리가 곧 닿으면 미리 누르는 것은 어긋남으로 보지 않는다.
            foreach (Ring r in _rings)
                if (!r.Resolved && r.Hold && r.ImpactBeat - _beat <= 1f + _judgeWindow / SecondsPerBeat()) return;
            Miss();
        }

        /// <summary>
        /// 불안일 때 시선이 동공 가까이에 있고 박자에 맞춰 눌렀으면 손님을 안심시켜 공명을 하나 쌓는다.
        /// judge(판정용 박자), 시선과 동공 위치, _reassureRange, 판정 시간을 사용하며, 안심시켰으면 true를 반환하고 공명, 콤보, 문구를 바꾼다.
        /// </summary>
        private bool TryReassure(float judge)
        {
            float gap = ((Vector2)_director.Cursor.LocalPos - PupilCenter()).magnitude - PupilRadius();
            if (gap > _reassureRange) return false;
            int nearest = Mathf.RoundToInt(judge);
            float error = (nearest - judge) * SecondsPerBeat();
            if (Mathf.Abs(error) > _judgeWindow) return false;
            // 같은 박에 두 번 누른 것은 세지 않는다.
            if (nearest == _lastGoodBeat) return true;
            _lastGoodBeat = nearest;
            Burst(PupilCenter(), IsPerfect(error) ? _perfectColor : _goodColor, IsPerfect(error) ? 1.3f : 1f, PupilRadius());
            GainResonance(Strings.Get("arena.reassure", _reassureEvent) + "  " + JudgeText(error), error);
            return true;
        }

        /// <summary>
        /// 평온·안도에서 박자에 맞춰 눌렀는지 판정한다. 맞으면 동공이 열리고 콤보가 오르며, 어긋나면 콤보가 끊긴다.
        /// 지금 박자, 판정 시간을 사용하며, 손님의 동공 열림, _lastGoodBeat, 콤보, 문구를 바꾼다.
        /// </summary>
        private void PressOnBeat()
        {
            float judge = JudgeBeat();
            int nearest = Mathf.RoundToInt(judge);
            float window = _judgeWindow * (_director.Emotion == ClientEmotion.Relieved ? _reliefWindowScale : 1f);
            if (Mathf.Abs(judge - nearest) * SecondsPerBeat() > window) { Miss(); return; }
            // 같은 박에 두 번 누른 것은 세지 않는다.
            if (nearest == _lastGoodBeat) return;
            _lastGoodBeat = nearest;
            Person person = _director.Person;
            person.Dilation = person.Dilation + _openPerBeat;
            float error = (nearest - judge) * SecondsPerBeat();
            Color color = IsPerfect(error) ? _perfectColor : _goodColor;
            Flash(color);
            Play(_chime);
            // 동공 둘레에서 고리가 퍼져 나간다.
            Burst(PupilCenter(), color, IsPerfect(error) ? 1.3f : 1f, PupilRadius());
            AddCombo(error);
            _event = JudgeText(error);
        }

        /// <summary>
        /// 고리를 받아냈을 때 처리한다.
        /// r(받아낸 고리), why(문구), error(박자와 어긋난 초)를 사용하며, 고리 상태와 공명을 바꾼다.
        /// </summary>
        private void ParryRing(Ring r, string why, float error)
        {
            r.Resolved = true;
            r.Parried = true;
            r.ResolvedAt = _beat;
            GainResonance(why, error);
        }

        /// <summary>
        /// 공명을 하나 쌓고 콤보를 올린다. 공명이 차면 감정이 한 단계 가라앉고 화면이 흔들린다.
        /// why(문구), error(박자와 어긋난 초)를 사용하며, _resonance, 콤보, _stage, 디렉터의 감정, 흔들림, 문구를 바꾼다.
        /// </summary>
        private void GainResonance(string why, float error)
        {
            _resonance++;
            AddCombo(error);
            Flash(IsPerfect(error) ? _perfectColor : _goodColor);
            Play(_chime);
            _event = why;
            if (_resonance < _resonanceToCalm) return;
            if (_stage >= 2) OnEmotionChanged(1);
            else
            {
                _director.SetEmotion(ClientEmotion.Calm);
                _lastEmotion = ClientEmotion.Calm;
                OnEmotionChanged(1);
            }
            Shake(_shakeCalm);
            Burst(PupilCenter(), _perfectColor, 2f, PupilRadius());
            _event = Strings.Get("arena.calmed", _calmedEvent);
        }

        /// <summary>
        /// 박자가 아닌 때 눌렀을 때 처리한다. 공명이 하나 줄고, 콤보가 끊기고, 잠깐 받아내기를 받지 않는다.
        /// 지금 박자와 _missLockSeconds를 사용하며, _resonance, 콤보, _lockUntilBeat, 문구를 바꾼다.
        /// </summary>
        private void Miss()
        {
            _resonance = Mathf.Max(0, _resonance - 1);
            _lockUntilBeat = _beat + _missLockSeconds / SecondsPerBeat();
            Flash(_missColor);
            Burst(_director.Cursor.LocalPos, _missColor, 0.5f, 0f);
            BreakCombo();
            _event = Strings.Get("arena.miss", _missEvent);
        }

        /// <summary>
        /// 놓치거나 맞았을 때 공통으로 처리한다. 공명이 하나 줄고, 콤보가 끊기고, 시선이 밀려나며 화면이 흔들린다.
        /// away(밀려날 방향), push(거리), why(문구)를 사용하며, 맞은 횟수, 공명, 콤보, 밀려남, 흔들림, 문구를 바꾼다.
        /// </summary>
        private void TakeHit(Vector2 away, float push, string why)
        {
            Hits++;
            _resonance = Mathf.Max(0, _resonance - 1);
            BreakCombo();
            Burst(_director.Cursor.LocalPos, _missColor, 1.2f, 0f);
            StartKnock(away * push);
            Shake(_shakeHit);
            _director.FlashGaze(1f);
            Flash(_missColor);
            Play(_thud);
            _event = why;
        }

        /// <summary>
        /// 고리를 그리고, 닿는 박이 지나면 판정한다. 받아내지 못한 고리는 놓친 것이다. 버티는 고리는 닿기 전 마지막 박 안에 눌러 쥐고 있어야 버틴다.
        /// 지금 박자, Space 누름 상태를 사용하며, 고리 목록, 공명, 콤보, 문구를 바꾼다.
        /// </summary>
        private void TickRings()
        {
            float windowBeats = _judgeWindow / SecondsPerBeat();
            bool holding = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                           || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                // 받아내서 감정이 가라앉으면 목록이 비워지므로 남은 차례를 건너뛴다.
                if (i >= _rings.Count) continue;
                Ring r = _rings[i];
                if (!r.Resolved && JudgeBeat() >= r.ImpactBeat + (r.Hold ? 0f : windowBeats)) ResolveRing(r, holding);
                if (i >= _rings.Count) continue;
                DrawRing(r);
                // 닿은 뒤 반 박 동안 잔상을 보이고 지운다.
                if (_beat < Mathf.Max(r.ImpactBeat, r.Resolved ? r.ResolvedAt : float.MaxValue) + 0.5f) continue;
                DestroyRing(r);
                _rings.RemoveAt(i);
            }
        }

        /// <summary>
        /// 닿는 박이 지난 고리를 판정한다. 버티는 고리는 마지막 박 안에 눌러 쥐었으면 버티고, 아니면 놓친 것이다.
        /// r, holding(Space를 누르고 있는지), _pressBeat를 사용하며, 고리 상태, 공명, 콤보, 밀려남, 문구를 바꾼다.
        /// </summary>
        private void ResolveRing(Ring r, bool holding)
        {
            r.Resolved = true;
            r.ResolvedAt = _beat;
            // 계속 누르고만 있으면 버티지 못한다. 고리가 닿기 전 마지막 박 안에 눌러 쥐어야 한다.
            if (r.Hold && holding && r.ImpactBeat - _pressBeat <= 1f)
            {
                r.Parried = true;
                Burst(_director.Cursor.LocalPos, _goodColor, 1f, 0f);
                // 버티기는 박자를 재지 않으므로 완벽으로 보지 않는다.
                GainResonance(Strings.Get("arena.hold", _holdEvent), _judgeWindow);
                return;
            }
            Vector2 away = (Vector2)_director.Cursor.LocalPos - PupilCenter();
            away = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.right;
            TakeHit(away, _ringPush, Strings.Get("arena.ring_miss", _ringMissEvent));
        }

        /// <summary>
        /// 고리 하나를 그린다. 고리는 시선을 둘러싸고 박자에 따라 일정한 빠르기로 좁혀져, 닿는 박에 시선 둘레의 기준 원과 딱 겹친다.
        /// 시선이 움직이면 고리도 따라가지만 좁혀지는 빠르기는 바뀌지 않는다. 기준 원은 박마다 뛰며 남은 박을 센다.
        /// 닿은 뒤에는 기준 원에서 조금 더 좁혀지며 옅어진다. 버티는 고리는 굵고 파랗다.
        /// r과 지금 박자, 시선 위치를 사용하며, 고리와 기준 원 선의 모양과 색을 바꾼다.
        /// </summary>
        private void DrawRing(Ring r)
        {
            float span = Mathf.Max(0.01f, r.ImpactBeat - r.SpawnBeat);
            float t = Mathf.Clamp01((_beat - r.SpawnBeat) / span);
            float after = _beat - r.ImpactBeat;
            float fade = after <= 0f ? 1f : Mathf.Clamp01(1f - after / 0.5f);
            Vector2 gaze = _director.Cursor.LocalPos;
            Color c = r.Parried ? _goodColor : (r.Hold ? _holdRingColor : _ringColor);

            // 박이 지나는 만큼 같은 빠르기로 좁혀진다. 닿은 뒤에도 같은 빠르기로 조금 더 좁혀지며 사라진다.
            float shrinkPerBeat = (_ringStartRadius - _ringCloseRadius) / span;
            float radius = Mathf.Max(0.01f, _ringCloseRadius + (r.ImpactBeat - _beat) * shrinkPerBeat);
            r.Line.transform.localPosition = gaze;
            Shapes.SetCircle(r.Line, radius, CIRCLE_SEGMENTS);
            r.Line.widthMultiplier = _maze.WallWidthNow * (r.Hold ? 3f + 2f * t : 1f + 1.5f * t);
            Color ring = c;
            ring.a *= Mathf.Lerp(0.3f, 1f, t) * fade;
            Shapes.SetColor(r.Line, ring);

            // 기준 원은 닿기 전까지만 보인다.
            r.Target.enabled = after < 0f && !r.Resolved;
            if (!r.Target.enabled) return;
            float pulse = Mathf.Pow(1f - (_beat - Mathf.Floor(_beat)), 3f);
            r.Target.transform.localPosition = gaze;
            Shapes.SetCircle(r.Target, _ringCloseRadius, CIRCLE_SEGMENTS);
            r.Target.widthMultiplier = _maze.WallWidthNow * (0.8f + 1.2f * pulse);
            Color mark = c;
            mark.a *= Mathf.Lerp(0.25f, 0.6f, t) + 0.25f * pulse;
            Shapes.SetColor(r.Target, mark);
        }

        /// <summary>
        /// 가시를 날리고, 시선에 닿으면 맞는다. 맞은 뒤 잠깐은 다시 맞지 않는다. 홍채 밖으로 나간 가시는 지운다.
        /// dt와 가시 설정, 시선 위치를 사용하며, 가시 목록, _invulnT, 맞음 처리를 바꾼다.
        /// </summary>
        private void TickSpikes(float dt)
        {
            if (_invulnT > 0f) _invulnT -= dt;
            Vector2 gaze = _director.Cursor.LocalPos;
            float width = _maze.WallWidthNow * 1.8f;
            for (int i = _spikes.Count - 1; i >= 0; i--)
            {
                if (i >= _spikes.Count) continue;
                Spike s = _spikes[i];
                s.Position += s.Direction * (_spikeSpeed * dt);
                Vector2 tail = s.Position - s.Direction * _spikeLength;
                Shapes.SetPoints(s.Line, new Vector3[] { tail, s.Position }, false);
                s.Line.widthMultiplier = width;
                if (_invulnT <= 0f && DistanceToSegment(gaze, tail, s.Position) <= _spikeHitRadius)
                {
                    _invulnT = _invulnSeconds;
                    TakeHit(s.Direction, _spikePush, Strings.Get("arena.spike_hit", _spikeHitEvent));
                }
                if (i >= _spikes.Count || s.Position.magnitude <= _maze.OuterRadius + 0.3f) continue;
                Destroy(s.Line.gameObject);
                _spikes.RemoveAt(i);
            }
        }

        /// <summary>
        /// 불안의 회피를 진행한다. 시선이 가까울수록 동공이 시선 반대쪽으로 비켜나고(동요는 더 멀리, 더 빨리), 불안이 아니면 제자리로 돌아온다.
        /// dt, 지금 감정, 시선 위치, 회피 설정을 사용하며, _dodge와 동공의 DodgeShift를 바꾼다.
        /// </summary>
        private void TickDodge(float dt)
        {
            Vector2 target = Vector2.zero;
            float speed = _stage >= 2 ? _dodgeSpeed2 : _dodgeSpeed;
            if (_director.Emotion == ClientEmotion.Anxious)
            {
                Vector2 gaze = _director.Cursor.LocalPos;
                float distance = gaze.magnitude;
                float closeness = Mathf.Clamp01(1f - (distance - PupilRadius()) / Mathf.Max(0.01f, _dodgeSenseRange));
                float far = _stage >= 2 ? _dodgeDistance2 : _dodgeDistance;
                if (distance > 1e-4f) target = -gaze / distance * (far * closeness);
            }
            else speed = Mathf.Max(speed, 1f);
            _dodge = Vector2.MoveTowards(_dodge, target, speed * dt);
            // 미로 기준 거리를 동공 부모 기준으로 바꿔 넘긴다. 깜빡임으로 미로가 돌아도 방향이 맞는다.
            Vector3 world = _maze.transform.TransformVector(_dodge);
            _pupil.DodgeShift = _eye.InverseTransformVector(world);
        }

        /// <summary>
        /// 시선을 offset만큼 _knockSeconds에 걸쳐 밀어내기 시작한다.
        /// offset을 사용하며, _knock과 _knockT를 바꾼다.
        /// </summary>
        private void StartKnock(Vector2 offset)
        {
            _knock = offset / Mathf.Max(0.01f, _knockSeconds);
            _knockT = _knockSeconds;
        }

        /// <summary>
        /// 밀려나는 중이면 시선을 한 프레임만큼 민다. 판의 경계와 닫힌 동공은 넘지 않는다.
        /// dt, _knock, _knockT를 사용하며, 시선 위치와 _knockT를 바꾼다.
        /// </summary>
        private void TickKnock(float dt)
        {
            if (_knockT <= 0f) return;
            _knockT -= dt;
            _director.PlaceCursor(MoveGaze(_director.Cursor.LocalPos, _knock * dt));
        }

        /// <summary>
        /// 동공이 충분히 열렸고 시선이 동공 가운데에 닿으면 진심에 닿은 것으로 처리한다. 막 열렸을 때는 문구로 알린다.
        /// 손님의 동공 열림, _openThreshold, 시선과 동공 위치를 사용하며, _wasOpen과 문구를 바꾸고 닿으면 디렉터의 TouchCore를 부른다.
        /// </summary>
        private void CheckCore()
        {
            bool open = IsOpen();
            if (open && !_wasOpen) { _event = Strings.Get("arena.opened", _openedEvent); Flash(_openColor); }
            _wasOpen = open;
            if (!open) return;
            if (((Vector2)_director.Cursor.LocalPos - PupilCenter()).magnitude <= _coreTouchRadius) _director.TouchCore();
        }

        /// <summary>
        /// 판에서 시선을 움직인다. 홍채 바깥 테두리를 넘지 않고, 동공이 닫혀 있으면 동공 안으로 들어가지 못한다.
        /// from(지금 위치)과 delta(이동량), 동공 위치와 열림을 사용하며, 새 위치를 반환한다. 디렉터의 ArenaMove로 쓴다.
        /// </summary>
        private Vector2 MoveGaze(Vector2 from, Vector2 delta)
        {
            Vector2 p = Vector2.ClampMagnitude(from + delta, _maze.OuterRadius - _edgeMargin);
            if (IsOpen()) return p;
            Vector2 center = PupilCenter();
            Vector2 d = p - center;
            float min = PupilRadius() + _edgeMargin;
            if (d.sqrMagnitude >= min * min) return p;
            // 막 닿았을 때 한 번만 알린다.
            if ((from - center).sqrMagnitude >= min * min) _event = Strings.Get("arena.closed", _closedEvent);
            Vector2 dir = d.sqrMagnitude > 1e-8f ? d.normalized : (from - center).normalized;
            return center + dir * min;
        }

        /// <summary>
        /// 동공 둘레의 박자 고리를 그린다. 박이 올 때 커지고 밝으며 마디 첫 박은 더 크다. 맞추거나 놓친 직후에는 그 색으로 빛난다.
        /// 지금 박자, 깜빡임 색, 동공 위치를 사용하며, 박자 고리의 위치, 크기, 색을 바꾼다.
        /// </summary>
        private void DrawBeatRing()
        {
            float frac = _beat - Mathf.Floor(_beat);
            float pulse = Mathf.Pow(1f - frac, 3f);
            bool downbeat = Mathf.FloorToInt(_beat) % Mathf.Max(1, _beatsPerBar) == 0;
            float size = downbeat ? 0.35f : 0.22f;
            _beatRing.enabled = true;
            _beatRing.transform.localPosition = PupilCenter();
            Shapes.SetCircle(_beatRing, PupilRadius() * (1.08f + size * pulse), CIRCLE_SEGMENTS);
            Color c = _beatColor;
            c.a *= 0.3f + 0.7f * pulse;
            float width = 1f + 1.5f * pulse;
            if (_flashT >= 0f)
            {
                _flashT += Time.deltaTime;
                float t = 1f - _flashT / FLASH_SECONDS;
                if (t <= 0f) _flashT = -1f;
                else { c = Color.Lerp(c, _flashColor, t); width += t * 2f; }
            }
            _beatRing.widthMultiplier = _maze.WallWidthNow * width;
            Shapes.SetColor(_beatRing, c);
        }

        /// <summary>
        /// 문구 줄에 지금 감정과 빠르기, 공명, 동공 열림, 콤보, 마지막 일을 쓴다.
        /// 지금 감정과 판 상태를 사용하며, 디렉터의 문구를 바꾼다.
        /// </summary>
        private void WriteStatus()
        {
            int open = Mathf.RoundToInt(_director.Person.Dilation * 100f);
            _director.Message = Strings.Get("arena.status", _statusMessage,
                ("emotion", EmotionName()), ("bpm", Mathf.RoundToInt(Bpm())), ("res", _resonance),
                ("need", IsTense() ? _resonanceToCalm : 0), ("open", open),
                ("combo", _combo >= 2 ? Strings.Get("arena.combo", _comboText, ("combo", _combo)) : ""), ("event", _event));
        }

        /// <summary>
        /// 지금 감정에 맞는 안내를 가져온다.
        /// 지금 감정을 사용하며, 안내 문구를 반환한다.
        /// </summary>
        private string Guide()
        {
            switch (_director.Emotion)
            {
                case ClientEmotion.Anxious: return Strings.Get("arena.anxious_guide", _anxiousGuideEvent);
                case ClientEmotion.Sad: return Strings.Get("arena.sad_guide", _sadGuideEvent);
                case ClientEmotion.Guarded: return Strings.Get("arena.guide", _guideEvent);
                default: return Strings.Get("arena.calm_guide", _calmGuideEvent);
            }
        }

        /// <summary>
        /// 콤보를 하나 올리고, 정해진 콤보마다 동공 둘레에서 큰 고리를 터뜨린다. 완벽이면 화면이 살짝 흔들린다.
        /// error(박자와 어긋난 초), _comboBurstEvery를 사용하며, _combo, _bestCombo, 불꽃, 흔들림을 바꾼다.
        /// </summary>
        private void AddCombo(float error)
        {
            _combo++;
            _bestCombo = Mathf.Max(_bestCombo, _combo);
            if (IsPerfect(error)) Shake(_shakePerfect);
            if (_comboBurstEvery > 0 && _combo % _comboBurstEvery == 0) Burst(PupilCenter(), _perfectColor, 1.6f + 0.1f * (_combo / _comboBurstEvery), PupilRadius());
        }

        /// <summary>
        /// 콤보를 끊는다.
        /// 입력값은 없으며, _combo를 0으로 바꾼다.
        /// </summary>
        private void BreakCombo() => _combo = 0;

        /// <summary>
        /// 박자와 어긋난 정도가 완벽으로 볼 만큼 작은지 판단한다.
        /// error(초)와 _perfectWindow를 사용하며, 완벽이면 true를 반환한다.
        /// </summary>
        private bool IsPerfect(float error) => Mathf.Abs(error) <= _perfectWindow;

        /// <summary>
        /// 박자와 어긋난 정도를 판정 문구로 바꾼다. 완벽이 아니면 빨랐는지 늦었는지 붙인다.
        /// error(초, 일찍 누르면 양수)를 사용하며, 판정 문구를 반환한다.
        /// </summary>
        private string JudgeText(float error)
        {
            if (IsPerfect(error)) return Strings.Get("arena.perfect", _perfectEvent);
            return error > 0f ? Strings.Get("arena.early", _earlyEvent) : Strings.Get("arena.late", _lateEvent);
        }

        /// <summary>
        /// 눈 전체를 strength만큼 흔들기 시작한다. 지금 흔들림보다 약하면 무시한다.
        /// strength(흔들리는 거리)와 _shakeSeconds를 사용하며, _shakeAmp와 _shakeT를 바꾼다.
        /// </summary>
        private void Shake(float strength)
        {
            float now = _shakeT > 0f ? _shakeAmp * (_shakeT / Mathf.Max(0.01f, _shakeSeconds)) : 0f;
            if (strength < now) return;
            _shakeAmp = strength;
            _shakeT = _shakeSeconds;
        }

        /// <summary>
        /// 흔들림을 한 프레임 진행한다. 눈 오브젝트를 원래 자리 둘레로 흔들고, 끝나면 원래 자리로 되돌린다. 판정은 미로 기준이라 영향을 받지 않는다.
        /// dt, _shakeAmp, _shakeT, _eyeRest를 사용하며, 눈 오브젝트의 위치와 _shakeT를 바꾼다.
        /// </summary>
        private void TickShake(float dt)
        {
            if (_shakeT <= 0f) return;
            _shakeT -= dt;
            if (_shakeT <= 0f) { _eye.localPosition = _eyeRest; return; }
            float k = _shakeT / Mathf.Max(0.01f, _shakeSeconds);
            _eye.localPosition = _eyeRest + (Vector3)(Random.insideUnitCircle * (_shakeAmp * k));
        }

        /// <summary>
        /// 받아내거나 맞거나 놓친 자리에서 고리와 불꽃을 터뜨린다.
        /// center(미로 기준), color, size(배율), baseRadius(고리가 시작하는 반지름)를 사용하며, _sparks에 더한다.
        /// </summary>
        private void Burst(Vector2 center, Color color, float size, float baseRadius)
        {
            var spark = new Spark
            {
                Center = center, Start = Time.time, Size = size, BaseRadius = baseRadius, Color = color,
                Ring = MakeLine("불꽃 고리"), Rays = new LineRenderer[Mathf.Max(0, _sparkRays)],
            };
            for (int i = 0; i < spark.Rays.Length; i++) spark.Rays[i] = MakeLine("불꽃");
            _sparks.Add(spark);
        }

        /// <summary>
        /// 터진 고리와 불꽃을 퍼뜨리고 옅게 한 뒤, 시간이 다 되면 지운다.
        /// _sparks와 _sparkSeconds, _sparkRadius를 사용하며, 불꽃 선의 모양과 색을 바꾸고 다 퍼진 것을 지운다.
        /// </summary>
        private void TickSparks()
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                Spark s = _sparks[i];
                float t = (Time.time - s.Start) / Mathf.Max(0.01f, _sparkSeconds);
                if (t >= 1f)
                {
                    DestroySpark(s);
                    _sparks.RemoveAt(i);
                    continue;
                }
                // 처음에 빠르게 퍼지고 끝으로 갈수록 느려진다.
                float ease = 1f - (1f - t) * (1f - t);
                float reach = _sparkRadius * s.Size;
                Color c = s.Color;
                c.a *= 1f - t;
                s.Ring.transform.localPosition = s.Center;
                Shapes.SetCircle(s.Ring, s.BaseRadius + reach * ease, CIRCLE_SEGMENTS);
                s.Ring.widthMultiplier = _maze.WallWidthNow * (2.5f * (1f - t) + 0.5f) * s.Size;
                Shapes.SetColor(s.Ring, c);
                for (int k = 0; k < s.Rays.Length; k++)
                {
                    Vector2 dir = Rotate(Vector2.right, 360f * k / s.Rays.Length + 22.5f);
                    float inner = s.BaseRadius + reach * (0.3f + 0.9f * ease);
                    float outer = s.BaseRadius + reach * (0.5f + 1.1f * ease);
                    Shapes.SetPoints(s.Rays[k], new Vector3[] { s.Center + dir * inner, s.Center + dir * outer }, false);
                    s.Rays[k].widthMultiplier = _maze.WallWidthNow * 1.5f * (1f - t) * s.Size;
                    Shapes.SetColor(s.Rays[k], c);
                }
            }
        }

        /// <summary>
        /// 지금 감정과 단계의 빠르기(BPM)를 계산한다.
        /// 지금 감정, _bpm, _stage2BpmScale을 사용하며, BPM을 반환한다.
        /// </summary>
        private float Bpm()
        {
            int i = Mathf.Clamp((int)_director.Emotion, 0, _bpm.Length - 1);
            return Mathf.Max(20f, _bpm[i] * (_stage >= 2 ? _stage2BpmScale : 1f));
        }

        /// <summary>
        /// 한 박의 길이(초)를 계산한다.
        /// 지금 빠르기를 사용하며, 초를 반환한다.
        /// </summary>
        private float SecondsPerBeat() => 60f / Bpm();

        /// <summary>
        /// 지금 감정의 이름을 가져온다. 2단계면 2단계 이름을 쓴다.
        /// 지금 감정, _stage, 이름 목록을 사용하며, 이름을 반환한다.
        /// </summary>
        private string EmotionName()
        {
            int i = (int)_director.Emotion;
            if (_stage >= 2 && i > 0 && i < _stage2Names.Length) return Strings.Get("emotion2." + i, _stage2Names[i]);
            return Strings.Get("emotion." + i, i < _emotionNames.Length ? _emotionNames[i] : _director.Emotion.ToString());
        }

        /// <summary>
        /// 지금 감정이 공격이 있는 긴장한 감정(경계, 불안, 슬픔)인지 판단한다.
        /// 디렉터의 지금 감정을 사용하며, 긴장한 감정이면 true를 반환한다.
        /// </summary>
        private bool IsTense()
        {
            ClientEmotion e = _director.Emotion;
            return e == ClientEmotion.Guarded || e == ClientEmotion.Anxious || e == ClientEmotion.Sad;
        }

        /// <summary>
        /// 동공이 들어갈 수 있을 만큼 열렸는지 판단한다. 긴장한 감정에서는 열려 있어도 들어갈 수 없다.
        /// 손님의 동공 열림과 _openThreshold를 사용하며, 열렸으면 true를 반환한다.
        /// </summary>
        private bool IsOpen() => !IsTense() && _director.Person.Dilation >= _openThreshold;

        /// <summary>
        /// 동공 가운데의 미로 기준 위치를 계산한다. 회피로 비켜난 만큼 함께 움직인다.
        /// 동공 Transform과 미로 Transform을 사용하며, 미로 기준 위치를 반환한다.
        /// </summary>
        private Vector2 PupilCenter() => _maze.transform.InverseTransformPoint(_pupil.transform.position);

        /// <summary>
        /// 지금 그려지는 동공 반지름을 미로 기준 거리로 계산한다.
        /// 동공의 ShownRadius와 두 Transform의 배율을 사용하며, 반지름을 반환한다.
        /// </summary>
        private float PupilRadius() => _pupil.ShownRadius * _pupil.transform.lossyScale.x / Mathf.Max(1e-4f, _maze.transform.lossyScale.x);

        /// <summary>
        /// 박자 고리를 color로 잠깐 빛나게 한다.
        /// color를 사용하며, _flashT와 _flashColor를 바꾼다.
        /// </summary>
        private void Flash(Color color)
        {
            _flashColor = color;
            _flashT = 0f;
        }

        /// <summary>
        /// 소리가 켜져 있으면 clip을 한 번 낸다.
        /// clip, _sound, _volume을 사용하며, 소리를 낸다.
        /// </summary>
        private void Play(AudioClip clip)
        {
            if (_sound) _audio.PlayOneShot(clip, _volume);
        }

        /// <summary>
        /// 날아오는 고리와 가시를 모두 지운다.
        /// 입력값은 없으며, 고리와 가시 목록을 비우고 그 선 오브젝트를 지운다.
        /// </summary>
        private void ClearAttacks()
        {
            foreach (Ring r in _rings) DestroyRing(r);
            _rings.Clear();
            foreach (Spike s in _spikes) Destroy(s.Line.gameObject);
            _spikes.Clear();
        }

        /// <summary>
        /// 고리 하나의 선 오브젝트를 지운다. 예약한 닿는 소리도 함께 사라진다.
        /// r을 사용하며, 고리와 닿을 자리 표시 오브젝트를 지운다.
        /// </summary>
        private static void DestroyRing(Ring r)
        {
            Destroy(r.Line.gameObject);
            Destroy(r.Target.gameObject);
        }

        /// <summary>
        /// 불꽃 하나의 선 오브젝트를 지운다.
        /// s를 사용하며, 고리와 불꽃 줄기 오브젝트를 지운다.
        /// </summary>
        private static void DestroySpark(Spark s)
        {
            Destroy(s.Ring.gameObject);
            foreach (LineRenderer ray in s.Rays) Destroy(ray.gameObject);
        }

        /// <summary>
        /// 미로 아래에 표시 전용 선 오브젝트를 만든다. 판정은 하지 않는다.
        /// objectName을 사용하며, 만든 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer MakeLine(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_maze.transform, false);
            LineRenderer line = Shapes.Ensure(go, _maze.LineMaterial, 2);
            line.sortingOrder = SORTING_ORDER;
            line.widthMultiplier = _maze.WallWidthNow;
            return line;
        }

        /// <summary>
        /// 짧게 울리고 사라지는 사인파 소리를 만든다. 소리 파일 없이 박자 소리로 쓴다.
        /// clipName, frequency(Hz), seconds, gain을 사용하며, 만든 AudioClip을 반환한다.
        /// </summary>
        private static AudioClip MakeTone(string clipName, float frequency, float seconds, float gain)
        {
            int length = Mathf.Max(1, Mathf.RoundToInt(SAMPLE_RATE * seconds));
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float envelope = Mathf.Exp(-t / (seconds * 0.25f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * gain;
            }
            AudioClip clip = AudioClip.Create(clipName, length, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 벡터를 degrees만큼 돌린다.
        /// v와 degrees를 사용하며, 돌린 벡터를 반환한다.
        /// </summary>
        private static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;

        /// <summary>
        /// 점 p와 선분 a-b 사이의 가장 가까운 거리를 계산한다.
        /// p, a, b를 사용하며, 거리를 반환한다.
        /// </summary>
        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
