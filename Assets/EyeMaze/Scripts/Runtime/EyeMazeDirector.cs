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
    /// Play 모드에서는 WASD / 방향키 / 왼쪽 스틱으로 이동하고, Space(패드 A)로 시선을 거두며, 1 / 2 / 3으로 대화를 고르고, R로 처음부터 한다.
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
        [Tooltip("자식 DialogueNode들이 순서대로 진행된다")]
        [SerializeField, FormerlySerializedAs("dialogue")] private Transform _dialogue;
        [Tooltip("동공 한가운데의 가장 반짝이는 파편")]
        [SerializeField, FormerlySerializedAs("core")] private CoreFragment _core;
        [Tooltip("키 입력을 받을지. 필드 화면에 있을 땐 GameFlow가 끈다")]
        [SerializeField, FormerlySerializedAs("acceptInput")] private bool _acceptInput = true;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("mode")] private Mode _mode = Mode.Talk;
        [SerializeField, FormerlySerializedAs("currentNode")] private int _currentNode;
        [SerializeField, FormerlySerializedAs("message"), TextArea(2, 5)] private string _message;
        [SerializeField, FormerlySerializedAs("inAbyss")] private bool _inAbyss;

        [Header("이야기")]
        [SerializeField, FormerlySerializedAs("introMessage"), TextArea(2, 4)] private string _introMessage = "은재는 석 달째 말을 하지 않았다. 눈을 들여다보면 마음속으로 들어갈 수 있다.";
        [SerializeField, TextArea(2, 4)] private string _firstGuide = "Space를 누르면 은재의 눈을 들여다본다.";
        [SerializeField, TextArea(2, 4)] private string _againGuide = "다시 마주 앉았다. Space를 누르면 눈을 들여다본다.";
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

        [Header("눈 깜빡임")]
        [Tooltip("들여다보는 동안 몇 초마다 눈을 깜빡이는지 (0이면 깜빡이지 않음)")]
        [SerializeField] private float _blinkInterval = 4f;
        [SerializeField, FormerlySerializedAs("blinkDuration")] private float _blinkDuration = 0.45f;
        [SerializeField, FormerlySerializedAs("blinkAmount"), Range(0f, 1f)] private float _blinkAmount;

        [Header("파편 읽은 뒤")]
        [Tooltip("파편을 다 읽고 나면 지금 층을 뺀 나머지 층의 벽을 무작위로 다시 만든다 (멈춰 있는 동안 길을 외우지 못하게)")]
        [SerializeField, FormerlySerializedAs("respawnMazeAfterReading")] private bool _respawnMazeAfterReading = true;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("talkWallAlpha"), Range(0f, 1f)] private float _talkWallAlpha = 0.22f;
        [SerializeField, FormerlySerializedAs("startRing")] private int _startRing;
        [SerializeField, FormerlySerializedAs("startSector")] private int _startSector = 4;
        [SerializeField, FormerlySerializedAs("bumpFlash")] private float _bumpFlash;
        [Tooltip("시선(플레이어) 색. 흑백 화면에서 눈에 띄도록 붉은색")]
        [SerializeField, FormerlySerializedAs("cursorColor")] private Color _cursorColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("벽에 부딪혔을 때 번쩍이는 색")]
        [SerializeField, FormerlySerializedAs("bumpColor")] private Color _bumpColor = Color.white;

        [NonSerialized] private Vector2 _velocity;
        [NonSerialized] private float _blinkPhase = -1f;
        [NonSerialized] private float _blinkElapsed;
        [NonSerialized] private int _lastLayer = -1;
        [NonSerialized] private int _heartIndex;
        [NonSerialized] private List<string> _endingLines;
        [NonSerialized] private readonly HashSet<string> _usedOptions = new HashSet<string>();
        [NonSerialized] private bool _reading;
        [NonSerialized] private float _readT;
        [NonSerialized] private bool _typingPrev;
        [NonSerialized] private bool _acceptedLastFrame;

        public IrisMaze Maze => _maze;
        public GazeCursor Cursor => _cursor;
        public Person Person => _person;
        public Transform Dialogue => _dialogue;
        public Mode CurrentMode => _mode;
        public bool AcceptInput { get => _acceptInput; set => _acceptInput = value; }
        public string Message { get => _message; set => _message = value; }
        public string IdleSituation => _idleSituation;
        public List<EndingDef> Endings => _endings;
        public ClientCase ActiveCase => _activeCase;
        public float BlinkAmount => _blinkAmount;

        /// <summary>미로를 지나 동공 안(심연)에 들어와 있는지</summary>
        public bool InAbyss => _mode == Mode.Gaze && _inAbyss;

        /// <summary>파편을 읽는 중이면 시선이 멈춘다.</summary>
        public bool IsReading => _mode == Mode.Gaze && _reading;

        /// <summary>엔딩 대사를 끝까지 봤는지 (그 다음 입력부터 필드로 돌아갈 수 있음)</summary>
        public bool HeartFinished => _mode == Mode.Ending && _heartIndex >= EndingLines.Count - 1;

        public EndingDef CurrentEnding => FindEnding(_currentEnding);
        public DialogueNode[] Nodes => _dialogue != null ? _dialogue.GetComponentsInChildren<DialogueNode>(false) : new DialogueNode[0];

        public DialogueNode CurrentNode
        {
            get
            {
                DialogueNode[] n = Nodes;
                return _currentNode >= 0 && _currentNode < n.Length ? n[_currentNode] : null;
            }
        }

        /// <summary>지금 케이스의 파편만 (다른 손님의 파편 묶음은 꺼져 있음)</summary>
        public MemoryFragment[] Fragments => _maze != null ? _maze.GetComponentsInChildren<MemoryFragment>(false) : new MemoryFragment[0];

        public bool CanGaze => _mode == Mode.Talk;
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

        void OnValidate() => EyeHooks.Repaint();

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

            // 들여다보는 동안 정해진 시간마다 눈을 깜빡인다.
            if (_mode == Mode.Gaze && !_inAbyss && !_reading) TickBlink(dt);
            if (_reading) _readT += dt;

            if (!_acceptInput)
            {
                // 필드 화면에서는 입력을 받지 않는다.
                _velocity = Vector2.zero;
                _typingPrev = Typewriter.AnyTyping;
                _acceptedLastFrame = false;
                return;
            }
            HandleInput(dt);
            _typingPrev = Typewriter.AnyTyping;
            _acceptedLastFrame = true;
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
            _dialogue = c.Dialogue;
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
            foreach (ClientCase other in all)
                if (other.Dialogue != null && other.Dialogue.gameObject.activeSelf != (other == c))
                    other.Dialogue.gameObject.SetActive(other == c);

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
            if (_endings == null || string.IsNullOrEmpty(id)) return null;
            foreach (EndingDef e in _endings) if (e != null && e.Id == id) return e;
            return null;
        }

        // 시선

        /// <summary>
        /// 눈을 들여다보기 시작하거나 시선을 거둔다. 심연과 엔딩 중에는 거둘 수 없다.
        /// 지금 모드를 사용하며, 모드와 시선 표시, 문구를 바꾼다.
        /// </summary>
        public void ToggleGaze()
        {
            if (_mode == Mode.Ending) return;
            if (_mode == Mode.Gaze && _inAbyss) { _message = _abyssMessage; return; }
            if (_mode == Mode.Gaze) { ExitGaze("시선을 거뒀다."); return; }
            if (!CanGaze) return;
            _mode = Mode.Gaze;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = true;
            _lastLayer = _maze != null && _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            _message = $"{Name}의 눈을 들여다본다.";
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
        /// dir을 사용하며, 시선 위치를 바꾼다.
        /// </summary>
        public void Nudge(Vector2 dir)
        {
            if (_mode != Mode.Gaze) return;
            MoveBy(dir.normalized * _editNudge);
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
        /// 파편을 읽은 뒤 지금 시선이 있는 층을 뺀 나머지 층의 벽을 무작위로 새로 만든다.
        /// 지금 시선의 층을 사용하며, 미로의 벽과 문구를 바꾼다.
        /// </summary>
        public void RespawnMaze()
        {
            if (!_respawnMazeAfterReading || _maze == null) return;
            int keep = _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            _maze.Generate(UnityEngine.Random.Range(1, 999999), keep);
            _message = "다른 층의 길이 바뀌었다.";
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

        /// <summary>
        /// 들여다본 시간을 쌓아 _blinkInterval초마다 눈을 깜빡인다.
        /// dt를 사용하며, _blinkElapsed와 깜빡임 단계를 바꾼다.
        /// </summary>
        private void TickBlink(float dt)
        {
            if (_blinkInterval <= 0f) return;
            _blinkElapsed += dt;
            if (_blinkElapsed < _blinkInterval) return;
            _blinkElapsed -= _blinkInterval;
            Blink();
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
        /// o의 필요 파편과 필요 단서를 사용하며, 고를 수 있으면 true를 반환한다.
        /// </summary>
        public bool IsAvailable(DialogueOption o) =>
            o != null && (o.Requires == null || o.Requires.Collected) && (o.RequiresClue == null || o.RequiresClue.Examined);

        /// <summary>
        /// 대화 선택지를 고른다. 좋은 말은 한 번만 통하며, 신뢰가 바닥나면 leave 엔딩이 된다.
        /// index를 사용하며, 상대의 신뢰와 동공, 문구, 대화 진행 또는 엔딩을 바꾼다.
        /// </summary>
        public void ChooseOption(int index)
        {
            if (_mode == Mode.Ending) return;
            if (_mode == Mode.Gaze) { _message = "들여다보는 중에는 말할 수 없다. (Space로 시선 거두기)"; return; }
            DialogueNode node = CurrentNode;
            if (node == null || index < 0 || index >= node.Options.Count) return;
            DialogueOption o = node.Options[index];
            if (!IsAvailable(o)) { _message = "아직 모르는 이야기다. 눈 속에서 단서를 찾아야 한다."; return; }

            // 같은 말을 반복해서 신뢰를 올릴 수 없게 좋은 말은 한 번만 통한다.
            string key = _currentNode + ":" + index;
            bool helpful = o.Trust > 0 || o.Dilation > 0f;
            if (helpful && _usedOptions.Contains(key)) { _message = $"이미 한 이야기다. {Name}는 다음 말을 기다리고 있다."; return; }
            _usedOptions.Add(key);

            if (_person != null)
            {
                _person.Trust += o.Trust;
                _person.Dilation = Mathf.Clamp01(_person.Dilation + o.Dilation);
            }
            if (!string.IsNullOrEmpty(o.Ending)) { StartEnding(o.Ending, o.Response); return; }
            var sb = new StringBuilder(o.Response);
            if (o.Trust > 0) sb.Append("\n(신뢰가 깊어졌다. 눈 속 어딘가의 문이 열렸다.)");
            if (o.Dilation > 0.05f) sb.Append("\n(동공이 조금 커졌다.)");
            if (o.Dilation < -0.05f) sb.Append("\n(동공이 좁아졌다.)");
            _message = sb.ToString();
            if (_person != null && _person.Trust <= _leaveTrust && FindEnding("leave") != null)
            {
                StartEnding("leave", o.Response);
                return;
            }
            if (o.Advance) _currentNode++;
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
        /// 지금 케이스에서 모은 파편 목록을 글로 만든다.
        /// 활성화된 파편들을 사용하며, 화면에 보일 요약 글을 반환한다.
        /// </summary>
        public string CollectedSummary()
        {
            MemoryFragment[] all = Fragments;
            string[] got = all.Where(f => f.Collected).Select(f => "· " + f.Title).ToArray();
            return $"파편들 ({got.Length}/{all.Length})\n" + (got.Length > 0 ? string.Join("\n", got) : "· 아직 없음");
        }

        /// <summary>
        /// 만남을 시작할 때 안내 문구를 보여준다.
        /// first(처음 만나는지)를 사용하며, 문구를 바꾼다.
        /// </summary>
        public void ShowEncounterGuide(bool first)
        {
            _message = first ? _firstGuide : _againGuide;
        }

        /// <summary>
        /// 만남을 처음 상태로 되돌린다.
        /// 입력값은 없으며, 모드와 대화 진행, 상대 상태, 안내 문구, 파편, 층 회전, 시선 위치를 초기화한다.
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
            _currentNode = 0;
            _message = _introMessage;
            _blinkAmount = 0f;
            _blinkPhase = -1f;
            _blinkElapsed = 0f;
            _velocity = Vector2.zero;
            if (_person != null) { _person.Trust = 0; _person.Dilation = 0.2f; }
            ShowEncounterGuide(true);
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

                // Space를 누르면 들여다보거나 시선을 거둔다. 만남 화면에 막 들어온 프레임의 누름은 무시한다.
                if (kb.spaceKey.wasPressedThisFrame && _acceptedLastFrame) ToggleGaze();
                if (kb.rKey.wasPressedThisFrame) ResetAll();
                if (_mode == Mode.Talk)
                {
                    if (kb.digit1Key.wasPressedThisFrame) ChooseOption(0);
                    else if (kb.digit2Key.wasPressedThisFrame) ChooseOption(1);
                    else if (kb.digit3Key.wasPressedThisFrame) ChooseOption(2);
                }
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) move += stick;
                if (pad.buttonSouth.wasPressedThisFrame && _acceptedLastFrame) ToggleGaze();
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
                if (layer > _lastLayer && l != null) _message = $"더 깊은 곳으로 들어왔다 — {l.DisplayName}";
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
                _message = $"파편 — {f.Title}\n{f.Text}";
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
            if (rotated && _mode == Mode.Gaze) _message = "눈을 깜빡였다. 무언가가 돌아갔다.";
        }

        /// <summary>
        /// 미로의 가장 안쪽을 지나 동공(심연)으로 들어선다. 여기서부턴 벽이 없다.
        /// 입력값은 없으며, _inAbyss와 문구를 바꾼다.
        /// </summary>
        private void EnterAbyss()
        {
            _inAbyss = true;
            _message = _abyssMessage;
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
