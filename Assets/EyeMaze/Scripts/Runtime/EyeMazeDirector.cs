using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 만남 화면의 흐름: 대화 → 눈 들여다보기(미로 탐색, 기억 파편 모으기) → 알게 된 것으로 대화 → 신뢰와 동공이 커짐 → 더 깊은 층 → 심연.
    /// Play 모드에서는 WASD / 방향키 / 왼쪽 스틱으로 이동하고, Space(패드 A)로 시선을 거두며, 1~4로 대화를 고르고, R로 처음부터 한다.
    /// 대화는 지금 손님의 대화 CSV(ClientCase.DialogueCsv)에서 읽는다.
    /// 에디트 모드에서는 디렉터 인스펙터의 버튼으로 같은 조작을 한다.
    /// </summary>
    [ExecuteAlways]
    public class EyeMazeDirector : MonoBehaviour
    {
        public enum Mode { Talk, Gaze, Ending }

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("maze")] private IrisMaze _maze;
        [SerializeField, FormerlySerializedAs("cursor")] private GazeCursor _cursor;
        [SerializeField, FormerlySerializedAs("person")] private Person _person;
        [Tooltip("태엽 (없어도 된다)")]
        [SerializeField, FormerlySerializedAs("intro")] private WindingIntro _intro;
        [Tooltip("동공 한가운데의 가장 반짝이는 파편")]
        [SerializeField, FormerlySerializedAs("core")] private CoreFragment _core;
        [Tooltip("키 입력을 받을지. 필드 화면에 있을 땐 GameFlow가 끈다")]
        [SerializeField, FormerlySerializedAs("acceptInput")] private bool _acceptInput = true;
        [Tooltip("플래그 목록 (Data/Flags.csv). condition이 있는 계산 플래그를 여기서 읽는다")]
        [SerializeField] private TextAsset _flagsCsv;
        [Tooltip("공용 문구 표 (Data/Strings.csv). 안내/시스템 문구를 여기서 읽는다")]
        [SerializeField] private TextAsset _stringsCsv;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("mode")] private Mode _mode = Mode.Talk;
        [Tooltip("지금 대화 장면 id (대화 CSV의 node)")]
        [SerializeField] private string _currentNodeId;
        [Tooltip("지나온 장면 id들. '다른 질문을 해볼까요?'를 고르면 하나씩 되돌아간다")]
        [SerializeField] private List<string> _nodeHistory = new List<string>();
        [SerializeField, FormerlySerializedAs("message"), TextArea(2, 5)] private string _message;
        [SerializeField, FormerlySerializedAs("inAbyss")] private bool _inAbyss;

        [Header("이야기")]
        [SerializeField, FormerlySerializedAs("introMessage"), TextArea(2, 4)] private string _introMessage = "은재는 석 달째 말을 하지 않았다. 눈을 들여다보면 마음속으로 들어갈 수 있다.";
        [SerializeField, FormerlySerializedAs("idleSituation"), TextArea(2, 4)] private string _idleSituation = "은재가 조용히 나를 바라보고 있다.";
        [SerializeField, FormerlySerializedAs("endingText"), TextArea(3, 6)] private string _endingText;
        [Tooltip("미로를 지나 동공(심연)에 들어섰을 때")]
        [SerializeField, FormerlySerializedAs("abyssMessage"), TextArea(1, 3)] private string _abyssMessage = "그녀의 마음 속 심연에 들어왔다.\n가장 깊은 곳에서 무언가가 반짝인다.";

        [Header("결말")]
        [Tooltip("이 손님의 결말들. 파편에 닿거나, 신뢰가 바닥나거나, 특정 선택지를 고르면 끝난다")]
        [SerializeField, FormerlySerializedAs("endings")] private List<EndingDef> _endings = new List<EndingDef>();
        [Tooltip("파편에 닿았을 때 이 신뢰 이상 + 열쇠 파편을 모았으면 truth, 아니면 half 엔딩")]
        [SerializeField, FormerlySerializedAs("truthTrust")] private int _truthTrust = 3;
        [Tooltip("진짜 결말에 꼭 필요한 기억 파편 (없으면 신뢰만 봄)")]
        [SerializeField, FormerlySerializedAs("keyFragment")] private MemoryFragment _keyFragment;
        [Tooltip("신뢰가 이 값 이하로 떨어지면 leave 엔딩 (그 사람이 떠남)")]
        [SerializeField, FormerlySerializedAs("leaveTrust")] private int _leaveTrust = -2;
        [Tooltip("지금 진행 중인 엔딩 id")]
        [SerializeField, FormerlySerializedAs("currentEnding")] private string _currentEnding;

        [Header("상담 케이스")]
        [Tooltip("지금 눈 속에 불러온 손님")]
        [SerializeField, FormerlySerializedAs("activeCase")] private ClientCase _activeCase;

        [Header("이동")]
        [Tooltip("초당 이동 거리")]
        [SerializeField, FormerlySerializedAs("moveSpeed")] private float _moveSpeed = 1.6f;
        [Tooltip("클수록 즉각적으로 출발/정지")]
        [SerializeField, FormerlySerializedAs("acceleration")] private float _acceleration = 10f;
        [Tooltip("벽과의 최소 간격")]
        [SerializeField, FormerlySerializedAs("wallMargin")] private float _wallMargin = 0.07f;
        [Tooltip("에디트 모드에서 버튼 한 번에 움직이는 거리")]
        [SerializeField, FormerlySerializedAs("editNudge")] private float _editNudge = 0.2f;

        [Header("눈 깜빡임 (태엽이 풀릴 때 일어남)")]
        [SerializeField, FormerlySerializedAs("blinkDuration")] private float _blinkDuration = 0.45f;
        [SerializeField, FormerlySerializedAs("blinkAmount"), Range(0f, 1f)] private float _blinkAmount;

        [Header("파편 읽은 뒤")]
        [Tooltip("파편을 다 읽고 나면 지금 층을 뺀 나머지 층의 벽을 무작위로 다시 만든다 (멈춰 있는 동안 길을 외우지 못하게)")]
        [SerializeField, FormerlySerializedAs("respawnMazeAfterReading")] private bool _respawnMazeAfterReading = true;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("talkWallAlpha"), Range(0f, 1f)] private float _talkWallAlpha = 0.22f;
        [Tooltip("에디트 모드에서 이동 한 번마다 풀리는 태엽 칸 수")]
        [SerializeField, FormerlySerializedAs("editStepUnwind")] private float _editStepUnwind = 0.5f;
        [SerializeField, FormerlySerializedAs("startRing")] private int _startRing;
        [SerializeField, FormerlySerializedAs("startSector")] private int _startSector = 4;
        [SerializeField, FormerlySerializedAs("bumpFlash")] private float _bumpFlash;
        [Tooltip("시선(플레이어) 색. 흑백 화면에서 눈에 띄도록 붉은색")]
        [SerializeField, FormerlySerializedAs("cursorColor")] private Color _cursorColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("벽에 부딪혔을 때 번쩍이는 색")]
        [SerializeField, FormerlySerializedAs("bumpColor")] private Color _bumpColor = Color.white;

        [NonSerialized] private Vector2 _velocity;
        [NonSerialized] private float _blinkPhase = -1f;
        [NonSerialized] private int _lastLayer = -1;
        [NonSerialized] private int _heartIndex;
        [NonSerialized] private List<string> _endingLines;
        [NonSerialized] private readonly HashSet<string> _usedOptions = new HashSet<string>();
        [NonSerialized] private bool _reading;
        [NonSerialized] private float _readT;
        [NonSerialized] private bool _typingPrev;
        [NonSerialized] private readonly List<ChoiceView> _choices = new List<ChoiceView>();

        /// <summary>화면에 보여줄 선택지 하나 (뒤로가기 포함)</summary>
        public struct ChoiceView
        {
            public string Text;
            public bool Available;
            public bool IsBack;
            /// <summary>아직 켜지지 않은 need 플래그들</summary>
            public string Missing;
        }

        public IrisMaze Maze => _maze;
        public GazeCursor Cursor => _cursor;
        public Person Person => _person;
        public WindingIntro Intro => _intro;
        public Mode CurrentMode => _mode;
        public bool AcceptInput { get => _acceptInput; set => _acceptInput = value; }
        public string Message { get => _message; set => _message = value; }
        public string IdleSituation => _activeCase != null && !string.IsNullOrEmpty(_activeCase.IdleSituation) ? _activeCase.IdleSituation : _idleSituation;
        /// <summary>지금 손님의 결말 목록 (엔딩 CSV가 바뀌면 바로 반영된다)</summary>
        public List<EndingDef> Endings => _activeCase != null ? _activeCase.Endings : _endings;
        private string IntroText => _activeCase != null && !string.IsNullOrEmpty(_activeCase.IntroMessage) ? _activeCase.IntroMessage : _introMessage;
        private string AbyssText => _activeCase != null && !string.IsNullOrEmpty(_activeCase.AbyssMessage) ? _activeCase.AbyssMessage : _abyssMessage;
        public ClientCase ActiveCase => _activeCase;
        public float BlinkAmount => _blinkAmount;

        /// <summary>미로를 지나 동공 안(심연)에 들어와 있는지</summary>
        public bool InAbyss => _mode == Mode.Gaze && _inAbyss;

        /// <summary>파편을 읽는 중이면 시선이 멈추고 태엽도 풀리지 않는다.</summary>
        public bool IsReading => _mode == Mode.Gaze && _reading;

        /// <summary>엔딩 대사를 끝까지 봤는지 (그 다음 입력부터 필드로 돌아갈 수 있음)</summary>
        public bool HeartFinished => _mode == Mode.Ending && _heartIndex >= EndingLines.Count - 1;

        public EndingDef CurrentEnding => FindEnding(_currentEnding);
        /// <summary>지금 손님의 대화 (CSV를 읽은 것)</summary>
        public DialogueScript Script => _activeCase != null ? _activeCase.Script : null;

        /// <summary>지금 대화 장면. id가 비었거나 CSV에 없으면 시작 장면</summary>
        public DialogueScript.Node CurrentNode
        {
            get
            {
                DialogueScript s = Script;
                if (s == null) return null;
                return s.Find(_currentNodeId) ?? s.Root;
            }
        }

        /// <summary>지금 장면에서 뒤로가기('다른 질문을 해볼까요?')를 보여줄지</summary>
        public bool CanGoBack
        {
            get
            {
                DialogueScript.Node n = CurrentNode;
                return n != null && n != Script.Root && n.BackLabel != DialogueScript.NoBack;
            }
        }

        /// <summary>지금 케이스의 파편만 (다른 손님의 파편 묶음은 꺼져 있음)</summary>
        public MemoryFragment[] Fragments => _maze != null ? _maze.GetComponentsInChildren<MemoryFragment>(false) : new MemoryFragment[0];

        public bool CanGaze => _mode == Mode.Talk && (_intro == null || _intro.CanGaze);
        public MazeLayer CursorLayer => _maze != null && _cursor != null ? _maze.GetLayer(_maze.LayerOfRing(_cursor.Ring)) : null;

        private string Name => _person != null ? _person.DisplayName : "그 사람";

        private List<string> EndingLines
        {
            get
            {
                if (_endingLines == null) _endingLines = BuildEndingLines(CurrentEnding, null);
                return _endingLines;
            }
        }

        void OnValidate()
        {
            Strings.Load(_stringsCsv);
            Flags.LoadDefinitions(_flagsCsv);
            EyeHooks.Repaint();
        }

        void Awake() => Strings.Load(_stringsCsv);

        void OnEnable()
        {
            Strings.Load(_stringsCsv);
            Flags.Values = FlagValue;
            Flags.LoadDefinitions(_flagsCsv);
        }

        /// <summary>
        /// 계산 플래그의 condition에서 쓰는 수치를 알려준다.
        /// name(trust / dilation / spring)을 사용하며, 지금 상대의 값 또는 null을 반환한다.
        /// </summary>
        private float? FlagValue(string name)
        {
            switch (name)
            {
                case "trust": return _person != null ? _person.Trust : (float?)null;
                case "dilation": return _person != null ? _person.Dilation : (float?)null;
                case "spring": return _intro != null ? _intro.Wound : (float?)null;
                default: return null;
            }
        }

        void Update()
        {
            if (_maze != null)
            {
                float a = _mode == Mode.Gaze ? 1f : _talkWallAlpha;
                if (!Mathf.Approximately(_maze.WallAlpha, a)) _maze.WallAlpha = a;
            }
            if (_cursor != null)
            {
                Color c = Color.Lerp(_cursorColor, _bumpColor, _bumpFlash);
                if (_cursor.Color != c) _cursor.Color = c;
            }

            if (!Application.isPlaying)
            {
                if (_blinkAmount != 0f) _blinkAmount = 0f;
                if (_bumpFlash != 0f) _bumpFlash = 0f;
                return;
            }

            float dt = Time.deltaTime;
            _bumpFlash = Mathf.MoveTowards(_bumpFlash, 0f, dt * 3f);

            // 깜빡임은 0에서 0.5(완전히 감김, 이때 회전)를 거쳐 1로 진행한다.
            if (_blinkPhase >= 0f)
            {
                float prev = _blinkPhase;
                _blinkPhase += dt / Mathf.Max(0.05f, _blinkDuration);
                if (prev < 0.5f && _blinkPhase >= 0.5f) ApplyBlinkRotation();
                if (_blinkPhase >= 1f) _blinkPhase = -1f;
            }
            _blinkAmount = _blinkPhase >= 0f ? Mathf.Sin(_blinkPhase * Mathf.PI) : 0f;

            // 들여다보는 동안 태엽이 풀린다. 깜빡임과 시간 제한은 태엽이 처리한다.
            if (_mode == Mode.Gaze && !_inAbyss && !_reading && _intro != null) _intro.Unwind(_intro.UnwindPerSecond * dt);
            if (_reading) _readT += dt;

            if (!_acceptInput)
            {
                // 필드 화면에서는 입력을 받지 않는다.
                _velocity = Vector2.zero;
                _typingPrev = Typewriter.AnyTyping;
                return;
            }
            HandleInput(dt);
            _typingPrev = Typewriter.AnyTyping;
        }

        // 케이스

        /// <summary>
        /// 손님 한 명의 데이터(대화/파편/결말/미로)를 불러오고 처음 상태로 되돌린다.
        /// c를 사용하며, 디렉터의 이야기 설정과 파편 묶음, 대화 루트의 활성 상태, 미로를 바꾼다.
        /// </summary>
        public void LoadCase(ClientCase c)
        {
            if (c == null) return;
            _activeCase = c;
            if (_person != null) _person.DisplayName = c.PersonName;
            _endings = c.Endings;
            _keyFragment = c.KeyFragment;
            _truthTrust = c.TruthTrust;
            _leaveTrust = c.LeaveTrust;
            if (!string.IsNullOrEmpty(c.IntroMessage)) _introMessage = c.IntroMessage;
            if (!string.IsNullOrEmpty(c.IdleSituation)) _idleSituation = c.IdleSituation;
            if (!string.IsNullOrEmpty(c.AbyssMessage)) _abyssMessage = c.AbyssMessage;

            ClientCase[] all = FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ClientCase other in all)
                foreach (GameObject g in other.FragmentGroups)
                    if (g != null && g.activeSelf != (other == c)) g.SetActive(other == c);

            if (_maze != null && _maze.Seed != c.MazeSeed) _maze.Generate(c.MazeSeed);
            ResetAll();
        }

        /// <summary>
        /// 씬에 저장된 공용 문구 속 '은재'/'그녀'를 지금 손님의 이름/대명사로 바꾼다.
        /// s와 지금 케이스를 사용하며, 바뀐 문구를 반환한다.
        /// </summary>
        public string Personalize(string s)
        {
            if (string.IsNullOrEmpty(s) || _activeCase == null) return s;
            if (_activeCase.PersonName != "은재") s = s.Replace("은재", _activeCase.PersonName);
            if (_activeCase.Pronoun != "그녀") s = s.Replace("그녀", _activeCase.Pronoun);
            return s;
        }

        /// <summary>
        /// 결말 목록에서 id로 결말을 찾는다.
        /// id를 사용하며, 찾은 EndingDef 또는 null을 반환한다.
        /// </summary>
        public EndingDef FindEnding(string id)
        {
            List<EndingDef> list = Endings;
            if (list == null || string.IsNullOrEmpty(id)) return null;
            foreach (EndingDef e in list) if (e != null && e.Id == id) return e;
            return null;
        }

        // 시선

        /// <summary>
        /// 눈을 들여다보기 시작하거나 시선을 거둔다. 심연과 엔딩 중에는 거둘 수 없다.
        /// 지금 모드와 태엽을 사용하며, 모드와 시선 표시, 문구를 바꾼다.
        /// </summary>
        public void ToggleGaze()
        {
            if (_mode == Mode.Ending) return;
            if (_mode == Mode.Gaze && _inAbyss) { _message = AbyssText; return; }
            if (_mode == Mode.Gaze) { ExitGaze(Strings.Get("msg.gaze_off", "시선을 거뒀다. 남은 태엽은 그대로다.")); return; }
            if (!CanGaze) { _message = _intro != null ? _intro.NeedWindMessage : ""; return; }
            _mode = Mode.Gaze;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = true;
            _lastLayer = _maze != null && _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            _message = Strings.Get("msg.gaze_on", "{name}의 눈을 들여다본다. 태엽이 풀리는 동안만 볼 수 있다.", ("name", Name));
        }

        /// <summary>
        /// 시선을 거두고 대화로 돌아온다.
        /// why(보여줄 문구)를 사용하며, 모드와 시선 표시, 읽기 상태를 바꾼다.
        /// </summary>
        public void ExitGaze(string why)
        {
            _reading = false;
            _mode = Mode.Talk;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = false;
            _message = why;
        }

        /// <summary>
        /// 입력 방향(화면 기준, -1~1)으로 가속/감속하며 시선을 움직인다.
        /// input, dt를 사용하며, _velocity와 시선 위치를 바꾼다.
        /// </summary>
        public void Steer(Vector2 input, float dt)
        {
            if (_mode != Mode.Gaze || _reading) { _velocity = Vector2.zero; return; }
            Vector2 target = Vector2.ClampMagnitude(input, 1f) * _moveSpeed;
            _velocity = Vector2.MoveTowards(_velocity, target, _acceleration * _moveSpeed * dt);
            if (_velocity.sqrMagnitude > 1e-8f) MoveBy(_velocity * dt);
        }

        /// <summary>
        /// 에디트 모드 버튼용으로 화면 방향으로 editNudge만큼 움직인다.
        /// dir을 사용하며, 시선 위치를 바꾸고 에디트 모드에서는 태엽을 조금 푼다.
        /// </summary>
        public void Nudge(Vector2 dir)
        {
            if (_mode != Mode.Gaze) return;
            MoveBy(dir.normalized * _editNudge);
            if (!Application.isPlaying && !_inAbyss && _intro != null) _intro.Unwind(_editStepUnwind);
        }

        /// <summary>
        /// 미로 기준 이동량만큼 시선을 옮긴다. 벽에 막히면 그 축만 막고 나머지 축으로 미끄러진다.
        /// delta를 사용하며, 시선의 반지름과 각도를 바꾼다.
        /// </summary>
        public void MoveBy(Vector2 delta)
        {
            if (_maze == null || _cursor == null) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.04f));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps && _mode == Mode.Gaze; i++)
            {
                float r = _cursor.Radius;
                float a = _cursor.Angle;
                if (_inAbyss) { MoveInAbyss(step); continue; }
                float rad = a * Mathf.Deg2Rad;
                var radialU = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var tangentU = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                float dr = Vector2.Dot(step, radialU);
                float dTan = Vector2.Dot(step, tangentU);

                if (Mathf.Abs(dr) > 1e-6f) ResolveRadial(ref r, ref a, dr);
                if (_mode != Mode.Gaze) return;
                if (!_inAbyss && Mathf.Abs(dTan) > 1e-6f) ResolveAngular(ref r, ref a, dTan);

                _cursor.Radius = r;
                _cursor.Angle = Mathf.Repeat(a, 360f);
            }
            OnMoved();
        }

        /// <summary>
        /// 다음 파편을 읽은 뒤처럼 눈을 한 번 깜빡이며, 지금 시선이 있는 층을 뺀 나머지 층의 벽을 무작위로 새로 만든다.
        /// 지금 시선의 층을 사용하며, 미로의 벽과 문구를 바꾼다.
        /// </summary>
        public void RespawnMaze()
        {
            if (!_respawnMazeAfterReading || _maze == null) return;
            int keep = _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            _maze.Generate(UnityEngine.Random.Range(1, 999999), keep);
            if (Application.isPlaying && _blinkPhase < 0f) _blinkPhase = 0f;
            _message = Strings.Get("msg.respawn", "눈을 깜빡이자, 다른 층의 길이 바뀌었다.");
        }

        /// <summary>
        /// 눈을 깜빡인다. Play 모드에서는 감겼다 뜨는 중간에 층이 돌고, 에디트 모드에서는 바로 돈다.
        /// 입력값은 없으며, 깜빡임 단계 또는 층 회전량을 바꾼다.
        /// </summary>
        public void Blink()
        {
            if (Application.isPlaying)
            {
                if (_blinkPhase < 0f) _blinkPhase = 0f;
            }
            else ApplyBlinkRotation();
        }

        // 심연과 결말

        /// <summary>
        /// 가장 반짝이는 파편에 닿아 마음이 들린다. 신뢰와 열쇠 파편에 따라 truth / half 엔딩이 된다.
        /// 상대의 신뢰와 열쇠 파편을 사용하며, 엔딩을 시작한다.
        /// </summary>
        public void TouchCore()
        {
            if (_core != null) _core.Touch();
            int trust = _person != null ? _person.Trust : 0;
            bool keyOk = _keyFragment == null || _keyFragment.Collected;
            StartEnding(trust >= _truthTrust && keyOk ? "truth" : "half", null);
        }

        /// <summary>
        /// 엔딩을 시작한다.
        /// id와 prefix(엔딩 대사 앞에 먼저 보여줄 한 줄)를 사용하며, 모드를 Ending으로 바꾸고 첫 줄을 띄운다.
        /// </summary>
        public void StartEnding(string id, string prefix)
        {
            _mode = Mode.Ending;
            _inAbyss = false;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = false;
            _currentEnding = id;
            _endingLines = BuildEndingLines(FindEnding(id), prefix);
            _heartIndex = 0;
            ShowHeartLine();
        }

        /// <summary>
        /// 엔딩 대사를 다음 줄로 넘긴다. 입력은 GameFlow가 받아서 이 메서드를 부른다.
        /// 입력값은 없으며, 엔딩 줄 번호와 문구를 바꾼다.
        /// </summary>
        public void NextHeartLine()
        {
            if (_mode != Mode.Ending || _heartIndex >= EndingLines.Count - 1) return;
            _heartIndex++;
            ShowHeartLine();
        }

        // 대화

        /// <summary>
        /// 선택지를 고를 수 있는지 판단한다.
        /// o의 need 플래그들을 사용하며, 모두 켜져 있으면 true를 반환한다.
        /// </summary>
        public bool IsAvailable(DialogueScript.Choice o) => o != null && Flags.AllSet(o.Need);

        /// <summary>
        /// 지금 장면에서 보여줄 선택지 목록을 만든다. 시작 장면이 아니면 맨 끝에 뒤로가기가 붙는다.
        /// 지금 장면과 플래그를 사용하며, 선택지 목록을 반환한다 (매번 같은 리스트를 다시 채운다).
        /// </summary>
        public IReadOnlyList<ChoiceView> CurrentChoices()
        {
            _choices.Clear();
            DialogueScript.Node node = CurrentNode;
            if (node == null) return _choices;
            foreach (DialogueScript.Choice c in node.Choices)
            {
                bool ok = IsAvailable(c);
                _choices.Add(new ChoiceView
                {
                    Text = c.Text,
                    Available = ok,
                    Missing = ok ? "" : string.Join(", ", c.Need.Where(f => !Flags.IsOn(f))),
                });
            }
            if (CanGoBack)
                _choices.Add(new ChoiceView
                {
                    Text = string.IsNullOrEmpty(node.BackLabel) ? Strings.Get("dialogue.back", DialogueScript.DefaultBackLabel) : node.BackLabel,
                    Available = true,
                    IsBack = true,
                    Missing = "",
                });
            return _choices;
        }

        /// <summary>
        /// 대화 선택지를 고른다. 좋은 말은 한 번만 통하며, 신뢰가 바닥나면 leave 엔딩이 된다.
        /// 마지막 번호가 뒤로가기면 이전 장면으로 돌아간다.
        /// index를 사용하며, 상대의 신뢰와 동공, 태엽, 플래그, 문구, 대화 장면 또는 엔딩을 바꾼다.
        /// </summary>
        public void ChooseOption(int index)
        {
            if (_mode == Mode.Ending) return;
            if (_mode == Mode.Gaze) { _message = Strings.Get("msg.no_talk_in_gaze", "들여다보는 중에는 말할 수 없다. (Space로 시선 거두기)"); return; }
            DialogueScript.Node node = CurrentNode;
            if (node == null || index < 0) return;
            if (index == node.Choices.Count && CanGoBack) { GoBack(); return; }
            if (index >= node.Choices.Count) return;
            DialogueScript.Choice o = node.Choices[index];
            if (!IsAvailable(o)) { _message = Strings.Get("msg.locked_choice", "아직 모르는 이야기다. 눈 속에서 단서를 찾아야 한다."); return; }

            // 같은 말을 반복해서 신뢰를 올릴 수 없게 좋은 말은 한 번만 통한다.
            // 다른 장면으로 넘어가는 말이면 수치는 다시 주지 않고 그 장면으로만 간다 (뒤로 갔다가 다시 들어올 때).
            string key = node.Id + ":" + index;
            bool helpful = o.Trust > 0 || o.Dilation > 0f || o.Spring > 0;
            if (helpful && _usedOptions.Contains(key))
            {
                if (!string.IsNullOrEmpty(o.Goto) && string.IsNullOrEmpty(o.Ending)) { _message = o.Response; GoTo(o.Goto); }
                else _message = Strings.Get("msg.already", "이미 한 이야기다. {name}는 다음 말을 기다리고 있다.", ("name", Name));
                return;
            }
            _usedOptions.Add(key);

            if (_person != null)
            {
                _person.Trust += o.Trust;
                _person.Dilation = Mathf.Clamp01(_person.Dilation + o.Dilation);
            }
            if (_intro != null && o.Spring != 0) _intro.AddFree(o.Spring);
            foreach (string f in o.Set) Flags.Set(f);
            if (!string.IsNullOrEmpty(o.Ending)) { StartEnding(o.Ending, o.Response); return; }
            var sb = new StringBuilder(o.Response);
            if (o.Trust > 0) sb.Append("\n" + Strings.Get("msg.trust_up", "(신뢰가 깊어졌다. 눈 속 어딘가의 문이 열렸다.)"));
            if (o.Trust < 0) sb.Append(Strings.Get("msg.trust_down", "") is string td && td.Length > 0 ? "\n" + td : "");
            if (o.Dilation > 0.05f) sb.Append("\n" + Strings.Get("msg.dilation_up", "(동공이 조금 커졌다.)"));
            if (o.Dilation < -0.05f) sb.Append("\n" + Strings.Get("msg.dilation_down", "(동공이 좁아졌다.)"));
            if (o.Spring > 0) sb.Append("\n" + Strings.Get("msg.spring_up", "(태엽이 {n}칸 감겼다.)", ("n", o.Spring)));
            if (o.Spring < 0) sb.Append("\n" + Strings.Get("msg.spring_down", "(태엽이 {n}칸 풀렸다.)", ("n", -o.Spring)));
            _message = sb.ToString();
            if (_person != null && _person.Trust <= _leaveTrust && FindEnding("leave") != null)
            {
                StartEnding("leave", o.Response);
                return;
            }
            if (!string.IsNullOrEmpty(o.Goto)) GoTo(o.Goto);
        }

        /// <summary>
        /// 다른 장면으로 간다. 이미 지나온 장면이면 그곳까지 되감는다 (뒤로가기가 꼬이지 않게).
        /// id를 사용하며, 지금 장면과 지나온 장면 목록을 바꾼다.
        /// </summary>
        public void GoTo(string id)
        {
            DialogueScript s = Script;
            if (s == null || s.Find(id) == null) return;
            DialogueScript.Node cur = CurrentNode;
            if (cur != null && cur.Id == id) return;
            int seen = _nodeHistory.LastIndexOf(id);
            if (seen >= 0) _nodeHistory.RemoveRange(seen, _nodeHistory.Count - seen);
            else if (cur != null) _nodeHistory.Add(cur.Id);
            _currentNodeId = id;
        }

        /// <summary>
        /// 바로 전 장면으로 돌아간다 ('다른 질문을 해볼까요?').
        /// 지나온 장면 목록을 사용하며, 지금 장면과 문구를 바꾼다.
        /// </summary>
        public void GoBack()
        {
            DialogueScript s = Script;
            DialogueScript.Node node = CurrentNode;
            if (s == null || node == null) return;
            _message = string.IsNullOrEmpty(node.BackLabel) ? Strings.Get("dialogue.back", DialogueScript.DefaultBackLabel) : node.BackLabel;
            if (_nodeHistory.Count > 0)
            {
                _currentNodeId = _nodeHistory[_nodeHistory.Count - 1];
                _nodeHistory.RemoveAt(_nodeHistory.Count - 1);
            }
            else _currentNodeId = s.Root != null ? s.Root.Id : "";
        }

        /// <summary>
        /// 첫 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(0)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption0() => ChooseOption(0);

        /// <summary>
        /// 두 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(1)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption1() => ChooseOption(1);

        /// <summary>
        /// 세 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(2)와 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption2() => ChooseOption(2);

        /// <summary>
        /// 네 번째 선택지(보통 뒤로가기)를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(3)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption3() => ChooseOption(3);

        /// <summary>
        /// 지금 케이스에서 모은 파편 목록을 글로 만든다.
        /// 활성화된 파편들을 사용하며, 화면에 보일 요약 글을 반환한다.
        /// </summary>
        public string CollectedSummary()
        {
            MemoryFragment[] all = Fragments;
            string[] got = all.Where(f => f.Collected).Select(f => "· " + f.Title).ToArray();
            return Strings.Get("ui.fragments", "파편들 ({got}/{all})", ("got", got.Length), ("all", all.Length)) + "\n"
                + (got.Length > 0 ? string.Join("\n", got) : Strings.Get("ui.fragments_none", "· 아직 없음"));
        }

        /// <summary>
        /// 만남을 처음 상태로 되돌린다.
        /// 입력값은 없으며, 모드와 대화 진행, 상대 상태, 태엽, 파편, 층 회전, 시선 위치를 초기화한다.
        /// </summary>
        public void ResetAll()
        {
            _mode = Mode.Talk;
            _inAbyss = false;
            _reading = false;
            _heartIndex = 0;
            _currentEnding = "";
            _endingLines = null;
            _usedOptions.Clear();
            if (_core != null) _core.ResetState();
            DialogueScript script = Script;
            _currentNodeId = script != null && script.Root != null ? script.Root.Id : "";
            _nodeHistory.Clear();
            // 이 대화의 선택지가 켜는 플래그는 만남을 다시 시작하면 끈다 (단서처럼 필드에서 켠 플래그는 유지).
            if (script != null) foreach (string f in script.FlagsSet()) Flags.Set(f, false);
            _message = IntroText;
            _blinkAmount = 0f;
            _blinkPhase = -1f;
            _velocity = Vector2.zero;
            if (_person != null) { _person.Trust = 0; _person.Dilation = 0.2f; }
            if (_intro != null) _intro.ResetForEncounter(true);
            foreach (MemoryFragment f in Fragments) f.Collected = false;
            if (_maze != null) foreach (MazeLayer l in _maze.Layers) l.Offset = 0;
            if (_cursor != null) { _cursor.SnapTo(_startRing, _startSector); _cursor.Visible = false; }
        }

        // 내부 처리

        /// <summary>
        /// 만남 화면의 키 입력을 처리한다. 파편을 읽는 중에는 넘기기 입력만 받는다.
        /// dt를 사용하며, 시선 이동과 시선 거두기, 대화 선택, 처음부터를 실행한다.
        /// </summary>
        private void HandleInput(float dt)
        {
            Vector2 move = Vector2.zero;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (_reading)
            {
                // 찍히는 중의 Z는 GameFlow가 글자를 다 보여주는 데 쓰고, 다 찍힌 뒤의 입력으로 계속한다.
                bool next = (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                if (next && _readT > 0.3f && !Typewriter.AnyTyping && !_typingPrev) { _reading = false; RespawnMaze(); }
                Steer(Vector2.zero, dt);
                return;
            }
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

                // 대화 중 Space는 태엽이 처리하고, 들여다보는 중에 누르면 시선을 거둔다.
                if (kb.spaceKey.wasPressedThisFrame && (_mode == Mode.Gaze || _intro == null)) ToggleGaze();
                if (kb.rKey.wasPressedThisFrame) ResetAll();
                if (_mode == Mode.Talk)
                {
                    if (kb.digit1Key.wasPressedThisFrame) ChooseOption(0);
                    else if (kb.digit2Key.wasPressedThisFrame) ChooseOption(1);
                    else if (kb.digit3Key.wasPressedThisFrame) ChooseOption(2);
                    else if (kb.digit4Key.wasPressedThisFrame) ChooseOption(3);
                }
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) move += stick;
                if (pad.buttonSouth.wasPressedThisFrame && (_mode == Mode.Gaze || _intro == null)) ToggleGaze();
            }
            Steer(move, dt);
        }

        /// <summary>
        /// 안쪽/바깥쪽 이동을 벽과 문에 맞춰 보정한다.
        /// r, a(참조), dr을 사용하며, 보정된 반지름과 각도를 r, a에 쓴다. 가장 안쪽을 지나면 심연에 들어간다.
        /// </summary>
        private void ResolveRadial(ref float r, ref float a, float dr)
        {
            int ring = _maze.RingAt(r);
            int s = _maze.SectorAt(a);
            float m = _wallMargin;
            float outerB = _maze.RingOuter(ring);
            float innerB = _maze.RingInner(ring);
            float target = r + dr;

            if (dr > 0f)
            {
                // 바깥쪽으로 움직인다.
                if (target <= outerB - m) { r = target; return; }
                if (ring > 0 && CanCrossArc(ring - 1, s, r, ref a)) { r = target; return; }
                r = Mathf.Min(r, outerB - m);
            }
            else
            {
                // 안쪽으로 움직인다.
                if (target >= innerB + m) { r = target; return; }
                if (CanCrossArc(ring, s, r, ref a))
                {
                    r = target;
                    if (ring == _maze.Rings - 1 && r < innerB) EnterAbyss();
                    return;
                }
                r = Mathf.Max(r, innerB + m);
            }
        }

        /// <summary>
        /// 둘레 방향 이동을 칸막이 벽에 맞춰 보정한다.
        /// r, a(참조), dTan을 사용하며, 보정된 반지름과 각도를 r, a에 쓴다.
        /// </summary>
        private void ResolveAngular(ref float r, ref float a, float dTan)
        {
            int ring = _maze.RingAt(r);
            int s = _maze.SectorAt(a);
            float d = _maze.SectorDeg;
            float m = _wallMargin;
            float rr = Mathf.Max(0.2f, r);
            float dDeg = dTan / rr * Mathf.Rad2Deg;
            float mDeg = m / rr * Mathf.Rad2Deg;
            float local = Mathf.Repeat(a, 360f) - s * d;
            float target = local + dDeg;

            if (dDeg > 0f)
            {
                // 반시계 방향으로 움직인다.
                if (target <= d - mDeg) { a += dDeg; return; }
                if (_maze.IsRadialOpen(ring, s)) { KeepInBand(ref r, ring); a += dDeg; return; }
                a = s * d + Mathf.Max(local, d - mDeg);
            }
            else
            {
                // 시계 방향으로 움직인다.
                if (target >= mDeg) { a += dDeg; return; }
                if (_maze.IsRadialOpen(ring, s - 1)) { KeepInBand(ref r, ring); a += dDeg; return; }
                a = s * d + Mathf.Min(local, mDeg);
            }
        }

        /// <summary>
        /// 원호 통로를 지날 수 있는지 판단한다. 통로 가장자리에 걸쳐 있으면 살짝 가운데로 끌어당긴다.
        /// arcRing, sector, r, a(참조)를 사용하며, 지날 수 있으면 true를 반환하고 a를 보정한다.
        /// </summary>
        private bool CanCrossArc(int arcRing, int sector, float r, ref float a)
        {
            if (!_maze.IsArcOpen(arcRing, sector, out MazeGate gate)) return false;
            if (gate != null && !gate.CanPass(_person)) { GateBlocked(gate); return false; }

            float d = _maze.SectorDeg;
            float mDeg = _wallMargin / Mathf.Max(0.2f, r) * Mathf.Rad2Deg;
            float local = Mathf.Repeat(a, 360f) - sector * d;
            if (local < mDeg) a += mDeg - local;
            else if (local > d - mDeg) a -= local - (d - mDeg);
            return true;
        }

        /// <summary>
        /// 반지름을 고리 안쪽 띠 범위로 맞춘다.
        /// r(참조), ring을 사용하며, r을 보정한다.
        /// </summary>
        private void KeepInBand(ref float r, int ring)
        {
            float m = _wallMargin;
            r = Mathf.Clamp(r, _maze.RingInner(ring) + m, _maze.RingOuter(ring) - m);
        }

        /// <summary>
        /// 잠긴 문에 막혔을 때 문구를 띄우고 시선을 번쩍이게 한다.
        /// gate를 사용하며, 문구와 _bumpFlash를 바꾼다.
        /// </summary>
        private void GateBlocked(MazeGate gate)
        {
            if (_message != gate.LockedMessage) _message = gate.LockedMessage;
            _bumpFlash = Mathf.Max(_bumpFlash, 0.6f);
        }

        /// <summary>
        /// 시선이 움직인 뒤 층이 바뀌었는지와 파편에 닿았는지 확인한다.
        /// 시선 위치를 사용하며, 문구와 마지막 층 기록을 바꾼다.
        /// </summary>
        private void OnMoved()
        {
            if (_mode != Mode.Gaze || _cursor == null) return;
            int layer = _maze.LayerOfRing(_cursor.Ring);
            if (layer != _lastLayer)
            {
                MazeLayer l = _maze.GetLayer(layer);
                if (layer > _lastLayer && l != null) _message = Strings.Get("msg.deeper", "더 깊은 곳으로 들어왔다 — {layer}", ("layer", l.DisplayName));
                _lastLayer = layer;
            }
            CheckFragment();
        }

        /// <summary>
        /// 시선이 있는 칸의 파편을 줍는다. Play 모드에서는 읽는 동안 멈춘다.
        /// 시선 위치와 파편들을 사용하며, 파편의 수집 상태와 문구, 읽기 상태를 바꾼다.
        /// </summary>
        private void CheckFragment()
        {
            Vector3 pos = _cursor.LocalPos;
            foreach (MemoryFragment f in Fragments)
            {
                if (f.Collected || f.Ring != _cursor.Ring || f.WorldSector != _cursor.Sector) continue;
                if (Vector3.Distance(pos, _maze.CellLocal(f.Ring, f.WorldSector)) > _maze.RingWidth * 0.55f) continue;
                f.Collected = true;
                _message = Strings.Get("msg.fragment", "파편 — {title}\n{text}", ("title", f.Title), ("text", f.Text));
                if (Application.isPlaying) { _reading = true; _readT = 0f; _velocity = Vector2.zero; }
            }
        }

        /// <summary>
        /// 깜빡일 때 도는 층들을 돌린다. 시선이 그 층에 있으면 함께 돈다.
        /// 각 층의 rotateOnBlink를 사용하며, 층 회전량과 시선 각도, 문구를 바꾼다.
        /// </summary>
        private void ApplyBlinkRotation()
        {
            if (_maze == null) return;
            bool rotated = false;
            foreach (MazeLayer layer in _maze.Layers)
            {
                if (layer.RotateOnBlink == 0) continue;
                bool riding = _cursor != null && _maze.LayerOfRing(_cursor.Ring) == layer.Index;
                layer.Offset += layer.RotateOnBlink;
                rotated = true;
                if (riding) _cursor.Angle = Mathf.Repeat(_cursor.Angle + layer.RotateOnBlink * _maze.SectorDeg, 360f);
            }
            if (rotated && _mode == Mode.Gaze) _message = Strings.Get("msg.blink_rotate", "눈을 깜빡였다. 무언가가 돌아갔다.");
        }

        /// <summary>
        /// 미로의 가장 안쪽을 지나 동공(심연)으로 들어선다. 여기서부턴 벽이 없고 태엽도 풀리지 않는다.
        /// 입력값은 없으며, _inAbyss와 문구를 바꾼다.
        /// </summary>
        private void EnterAbyss()
        {
            _inAbyss = true;
            _message = AbyssText;
        }

        /// <summary>
        /// 심연 안에서 벽 없이 움직인다. 동공 밖으로는 못 나가고, 한가운데 파편에 닿으면 마음이 들린다.
        /// step을 사용하며, 시선 위치를 바꾸고 닿으면 TouchCore를 부른다.
        /// </summary>
        private void MoveInAbyss(Vector2 step)
        {
            Vector2 p = (Vector2)Shapes.Polar(_cursor.Radius, _cursor.Angle) + step;
            float limit = Mathf.Max(0.1f, _maze.InnerRadius - _wallMargin);
            if (p.magnitude > limit) p = p.normalized * limit;
            _cursor.Radius = p.magnitude;
            if (p.sqrMagnitude > 1e-8f) _cursor.Angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
            float touch = _core != null ? _core.TouchRadius : 0.22f;
            if (_cursor.Radius <= touch) TouchCore();
        }

        /// <summary>
        /// 엔딩에서 보여줄 줄 목록을 만든다.
        /// e와 prefix를 사용하며, 앞줄 + 엔딩 대사 + 제목 줄 목록을 반환한다.
        /// </summary>
        private List<string> BuildEndingLines(EndingDef e, string prefix)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(prefix)) list.Add(prefix);
            if (e != null)
            {
                if (e.Lines != null) foreach (string l in e.Lines) if (!string.IsNullOrEmpty(l)) list.Add(l);
                list.Add($"— {e.Title} —");
            }
            else if (!string.IsNullOrEmpty(_endingText)) list.Add(_endingText);
            if (list.Count == 0) list.Add("…");
            return list;
        }

        /// <summary>
        /// 지금 엔딩 줄을 문구로 띄운다.
        /// _heartIndex를 사용하며, _message를 바꾼다.
        /// </summary>
        private void ShowHeartLine()
        {
            List<string> l = EndingLines;
            _message = l[Mathf.Clamp(_heartIndex, 0, l.Count - 1)];
        }
    }
}
