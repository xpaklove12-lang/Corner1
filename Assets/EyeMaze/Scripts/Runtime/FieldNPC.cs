using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>필드의 사람. 말을 걸면 그 사람의 눈 속(만남 화면)으로 들어간다. 사이드뷰에서는 흰 테두리 캡슐로 그린다.</summary>
    [ExecuteAlways]
    public class FieldNPC : FieldInteractable
    {
        [Header("사람")]
        [Tooltip("키 (0이면 동그라미로 그림, 탑뷰용)")]
        [SerializeField, FormerlySerializedAs("height")] private float _height = 0.95f;
        [SerializeField, FormerlySerializedAs("bodyColor")] private Color _bodyColor = Color.white;
        [SerializeField, FormerlySerializedAs("lineBefore"), TextArea(2, 4)] private string _lineBefore = "…";
        [SerializeField, FormerlySerializedAs("lineAfterResolved"), TextArea(2, 4)] private string _lineAfterResolved = "\"…고마워요. 오늘은 조금 말하고 싶어졌어요.\"";
        [Tooltip("한 번이라도 눈을 들여다봤는지")]
        [SerializeField, FormerlySerializedAs("met")] private bool _met;
        [Tooltip("상담이 끝났는지")]
        [SerializeField, FormerlySerializedAs("resolved")] private bool _resolved;

        [Header("필드 대화 CSV")]
        [Tooltip("말을 걸었을 때 나오는 대사 CSV (Data/Talk/이름.csv, UTF-8). 비우거나 맞는 대화가 없으면 아래 칸들의 대사를 쓴다")]
        [SerializeField] private TextAsset _talkCsv;

        [Header("초상화 / 대화")]
        [Tooltip("말할 때 텍스트 상자 왼쪽에 뜨는 초상화")]
        [SerializeField, FormerlySerializedAs("portrait")] private Sprite _portrait;
        [Tooltip("켜면 눈을 들여다보지 않고 대화만 한다 (예: 보호자)")]
        [SerializeField, FormerlySerializedAs("talkOnly")] private bool _talkOnly;
        [Tooltip("talkOnly일 때 Z로 한 줄씩 넘기는 대사")]
        [SerializeField, FormerlySerializedAs("talkLines"), TextArea(1, 3)] private List<string> _talkLines = new List<string>();
        [Tooltip("대화를 마친 뒤 다시 말을 걸면 나오는 말")]
        [SerializeField, FormerlySerializedAs("lineAfterTalk"), TextArea(1, 2)] private string _lineAfterTalk = "…";
        [Tooltip("대화를 마치면 방을 나간다")]
        [SerializeField, FormerlySerializedAs("leavesAfterTalk")] private bool _leavesAfterTalk;
        [Tooltip("대화를 마치면 이 사람이 들어온다")]
        [SerializeField, FormerlySerializedAs("revealAfterTalk")] private FieldNPC _revealAfterTalk;
        [Tooltip("그 사람이 들어올 때 나오는 서술 (그 사람의 초상화와 함께)")]
        [SerializeField, FormerlySerializedAs("revealLine"), TextArea(1, 3)] private string _revealLine;

        [Header("등장")]
        [Tooltip("켜면 Play 시작 때는 방에 없고, 누군가와의 대화 뒤에 들어온다 (에디트 모드에선 항상 보임)")]
        [SerializeField, FormerlySerializedAs("appearsLater")] private bool _appearsLater;

        [Header("걷기 (컷신)")]
        [SerializeField, FormerlySerializedAs("walkSpeed")] private float _walkSpeed = 2.6f;
        [Tooltip("서 있을 때 키 (앉아 있을 때는 height)")]
        [SerializeField, FormerlySerializedAs("standHeight")] private float _standHeight = 1.5f;
        [Tooltip("바닥 높이 (서 있을 때 발이 닿는 곳)")]
        [SerializeField, FormerlySerializedAs("floorY")] private float _floorY = -3f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("body")] private LineRenderer _body;
        [Tooltip("몸 안쪽을 검게 칠해 테두리만 남김")]
        [SerializeField, FormerlySerializedAs("inner")] private LineRenderer _inner;
        [SerializeField, FormerlySerializedAs("marker")] private LineRenderer _marker;

        [NonSerialized] private int _drawnKey = int.MinValue;
        [NonSerialized] private bool _present = true;
        [NonSerialized] private bool _talked;
        [NonSerialized] private bool _seatSaved;
        [NonSerialized] private Vector3 _seatPos;
        [NonSerialized] private float _seatHeight;
        [NonSerialized] private bool _walking;
        [NonSerialized] private float _walkTargetX;
        [NonSerialized] private Action _walkDone;
        [NonSerialized] private float _walkSpeedNow;
        [NonSerialized] private float _bobT;
        [NonSerialized] private float _sitT = -1f;
        [NonSerialized] private Vector3 _sitFrom;
        [NonSerialized] private float _sitFromHeight;
        [NonSerialized] private Action _sitDone;

        public bool Met { get => _met; set => _met = value; }
        public bool Resolved { get => _resolved; set => _resolved = value; }
        public string LineAfterResolved { get => _lineAfterResolved; set => _lineAfterResolved = value; }
        public Sprite Portrait => _portrait;
        public bool TalkOnly => _talkOnly;
        public List<string> TalkLines => _talkLines;
        public string RevealLine => _revealLine;
        public bool IsMoving => _walking || _sitT >= 0f;
        public TextAsset TalkCsv => _talkCsv;
        public TalkScript Talk => TalkScript.Get(_talkCsv);

        /// <summary>씬에 놓인 원래 자리 (의자 위)</summary>
        public Vector3 SeatPosition { get { SaveSeat(); return _seatPos; } }

        /// <summary>지금 방에 있는지 (에디트 모드에선 항상 true)</summary>
        public bool Present
        {
            get => !Application.isPlaying || _present;
            set { _present = value; _drawnKey = int.MinValue; }
        }

        public override bool Available => Present;
        public override string Verb => _talkOnly || _resolved ? Strings.Get("verb.talk", "이야기하기") : Strings.Get("verb.gaze", "눈을 마주하기");

        void OnEnable() { _drawnKey = int.MinValue; }
        protected override void OnValidate() { _drawnKey = int.MinValue; base.OnValidate(); }

        void Start()
        {
            if (Application.isPlaying) Present = !_appearsLater;
        }

        void Update()
        {
            if (Application.isPlaying) Animate(Time.deltaTime);
            int key = HashCode.Combine(Radius, _height, _bodyColor, _resolved, Present, _talkOnly);
            if (key == _drawnKey) return;
            _drawnKey = key;
            bool show = Present;
            if (_body != null && _body.enabled != show) _body.enabled = show;
            if (_inner != null && _inner.enabled != show) _inner.enabled = show;
            if (_marker != null && _marker.enabled != show) _marker.enabled = show;
            float w = Radius * 2f;
            float half = Mathf.Max(0.001f, _height * 0.5f - w * 0.5f);
            if (_body != null)
            {
                if (_height > 0f)
                {
                    _body.numCapVertices = 6;
                    Shapes.SetPoints(_body, new[] { new Vector3(0f, -half, 0f), new Vector3(0f, half, 0f) }, false);
                    _body.widthMultiplier = w;
                }
                else Shapes.SetBlob(_body, w);
                Shapes.SetColor(_body, _bodyColor);
                _body.sortingOrder = 0;
            }
            if (_inner != null)
            {
                float t = 0.07f;
                _inner.numCapVertices = 6;
                if (_height > 0f)
                    Shapes.SetPoints(_inner, new[] { new Vector3(0f, -half, 0f), new Vector3(0f, half, 0f) }, false);
                else
                    Shapes.SetPoints(_inner, new[] { Vector3.zero, new Vector3(0f, 0.001f, 0f) }, false);
                _inner.widthMultiplier = Mathf.Max(0.01f, w - t * 2f);
                Shapes.SetColor(_inner, Color.black);
                _inner.sortingOrder = 1;
            }
            if (_marker != null)
            {
                float top = _height > 0f ? _height * 0.5f : Radius;
                _marker.transform.localPosition = new Vector3(0f, top + 0.25f, 0f);
                Shapes.SetBlob(_marker, 0.12f);
                Shapes.SetColor(_marker, _resolved || _talkOnly ? Color.white : new Color(0.55f, 0.95f, 1f, 1f));
                _marker.sortingOrder = 2;
            }
        }

        /// <summary>
        /// 말을 걸었을 때의 반응을 실행한다. 대화만 하는 사람은 대사를 띄우고, 그 밖에는 눈 속으로 들어간다.
        /// flow를 사용하며, 글 상자 또는 만남 상태를 바꾼다.
        /// </summary>
        public override void Interact(GameFlow flow)
        {
            if (_talkOnly)
            {
                bool first = !_talked;
                if (TryTalk(flow, null, null, first ? () => FinishTalk(flow) : (Action)null)) return;
                if (_talked) { flow.ShowConversation(DisplayName, _portrait, new List<string> { _lineAfterTalk }, null); return; }
                flow.ShowConversation(DisplayName, _portrait, _talkLines, () => FinishTalk(flow));
                return;
            }
            if (_resolved)
            {
                if (TryTalk(flow, null, null, null)) return;
                flow.ShowConversation(DisplayName, _portrait, new List<string> { _lineAfterResolved }, null);
            }
            else flow.BeginEncounter(this);
        }

        /// <summary>
        /// 필드 대화 CSV에서 대화를 골라 보여준다. 다 보면 그 대화의 set 플래그를 켜고 done을 부른다.
        /// flow, groupId(null이면 need가 맞는 첫 대화), narrationPortrait(서술 줄의 초상화), done을 사용하며,
        /// 보여줄 대화가 있으면 true를 반환한다 (없으면 호출한 쪽이 원래 대사를 쓴다).
        /// </summary>
        public bool TryTalk(GameFlow flow, string groupId, Sprite narrationPortrait, Action done)
        {
            TalkScript t = Talk;
            TalkScript.Group g = t == null ? null : groupId == null ? t.Pick() : t.Find(groupId);
            if (g == null || g.Lines.Count == 0 || !Flags.AllSet(g.Need)) return false;

            var speakers = new List<string>();
            var portraits = new List<Sprite>();
            var lines = new List<string>();
            foreach (TalkScript.Line l in g.Lines)
            {
                if (string.IsNullOrEmpty(l.Speaker)) { speakers.Add(DisplayName); portraits.Add(_portrait); }
                else if (l.Speaker == TalkScript.Narration) { speakers.Add(null); portraits.Add(narrationPortrait); }
                else if (l.Speaker == TalkScript.Unknown) { speakers.Add(TalkScript.Unknown); portraits.Add(_portrait); }
                else { speakers.Add(l.Speaker); portraits.Add(PortraitOf(l.Speaker)); }
                lines.Add(l.Text);
            }
            string[] set = g.Set;
            flow.ShowConversationLines(speakers, portraits, lines, () =>
            {
                foreach (string f in set) Flags.Set(f);
                done?.Invoke();
            });
            return true;
        }

        /// <summary>
        /// 이름으로 씬의 다른 사람 초상화를 찾는다 (speaker 칸에 다른 사람 이름을 쓴 줄용).
        /// name을 사용하며, 찾은 초상화 또는 null을 반환한다.
        /// </summary>
        private static Sprite PortraitOf(string name)
        {
            foreach (FieldNPC n in FindObjectsByType<FieldNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (n.DisplayName == name) return n.Portrait;
            return null;
        }

        /// <summary>
        /// x 위치에 서 있는 모습으로 나타난다.
        /// x를 사용하며, 키를 서 있는 키로 바꾸고 위치와 등장 상태를 바꾼다.
        /// </summary>
        public void StandAt(float x)
        {
            SaveSeat();
            _height = _standHeight;
            transform.position = new Vector3(x, _floorY + _standHeight * 0.5f, _seatPos.z);
            Present = true;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// x까지 걸어가기 시작한다.
        /// x와 도착 콜백 done을 사용하며, 걷기 상태를 켠다.
        /// </summary>
        public void WalkTo(float x, Action done)
        {
            SaveSeat();
            if (!Mathf.Approximately(_height, _standHeight)) StandAt(transform.position.x);
            _walking = true;
            _walkTargetX = x;
            _walkDone = done;
            _walkSpeedNow = _walkSpeed;
        }

        /// <summary>
        /// x까지 정해진 시간 동안 걸어간다. 걷는 속도 설정 대신 거리와 시간으로 속도를 정한다.
        /// x, 걸리는 시간 seconds, 도착 콜백 done을 사용하며, 걷기 상태와 이번 걸음의 속도를 바꾼다.
        /// </summary>
        public void WalkToIn(float x, float seconds, Action done)
        {
            WalkTo(x, done);
            _walkSpeedNow = Mathf.Abs(x - transform.position.x) / Mathf.Max(0.01f, seconds);
        }

        /// <summary>
        /// 씬에 놓인 자리(의자 등)로 앉기 시작한다.
        /// 완료 콜백 done을 사용하며, 앉기 보간 상태를 켠다.
        /// </summary>
        public void SitDown(Action done)
        {
            SaveSeat();
            _sitFrom = transform.position;
            _sitFromHeight = _height;
            _sitT = 0f;
            _sitDone = done;
        }

        /// <summary>
        /// 처음 한 번, 씬에 놓인 위치와 키를 앉는 자리로 기억한다.
        /// transform.position과 _height를 사용하며, _seatPos와 _seatHeight를 저장한다.
        /// </summary>
        private void SaveSeat()
        {
            if (_seatSaved) return;
            _seatSaved = true;
            _seatPos = transform.position;
            _seatHeight = _height;
        }

        /// <summary>
        /// 걷기와 앉기 움직임을 한 프레임 진행한다.
        /// dt를 사용하며, 위치와 키를 바꾸고 끝나면 콜백을 부른다.
        /// </summary>
        private void Animate(float dt)
        {
            if (_walking)
            {
                Vector3 p = transform.position;
                p.x = Mathf.MoveTowards(p.x, _walkTargetX, _walkSpeedNow * dt);
                _bobT += dt * 9f;
                p.y = _floorY + _height * 0.5f + Mathf.Abs(Mathf.Sin(_bobT)) * 0.06f;
                transform.position = p;
                if (Mathf.Approximately(p.x, _walkTargetX))
                {
                    _walking = false;
                    p.y = _floorY + _height * 0.5f;
                    transform.position = p;
                    Action d = _walkDone;
                    _walkDone = null;
                    d?.Invoke();
                }
            }
            if (_sitT >= 0f)
            {
                _sitT = Mathf.Min(1f, _sitT + dt / 0.4f);
                float e = Mathf.SmoothStep(0f, 1f, _sitT);
                transform.position = Vector3.Lerp(_sitFrom, _seatPos, e);
                float h = Mathf.Lerp(_sitFromHeight, _seatHeight, e);
                if (!Mathf.Approximately(h, _height)) _height = h;
                if (_sitT >= 1f)
                {
                    _sitT = -1f;
                    Action d = _sitDone;
                    _sitDone = null;
                    d?.Invoke();
                }
            }
        }

        /// <summary>
        /// 대화를 마친 뒤의 일을 처리한다. 나가야 하면 사라지고, 다음 사람이 있으면 들여보낸다.
        /// flow를 사용하며, _talked와 등장 상태를 바꾸고 필요하면 서술을 띄운다.
        /// </summary>
        private void FinishTalk(GameFlow flow)
        {
            _talked = true;
            if (_leavesAfterTalk) Present = false;
            if (_revealAfterTalk != null)
            {
                _revealAfterTalk.Present = true;
                if (TryTalk(flow, TalkScript.Reveal, _revealAfterTalk.Portrait, null)) return;
                if (!string.IsNullOrEmpty(_revealLine))
                    flow.ShowConversation(null, _revealAfterTalk.Portrait, new List<string> { _revealLine }, null);
            }
        }
    }
}
