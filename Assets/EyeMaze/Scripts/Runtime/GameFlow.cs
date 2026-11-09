using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>
    /// 필드 ↔ 만남(눈동자 미로) 흐름.
    /// 필드에서는 이동하고 Space / Enter로 말을 걸거나 살펴본다.
    /// 말을 걸면 그 사람에게 줌인한 뒤 암전되고 눈 화면으로 들어간다. 만남에서 Q는 자리 떠나기(진행 유지)다.
    /// 에디트 모드에서는 GameFlow 인스펙터의 버튼으로 필드 화면과 만남 화면을 오갈 수 있다.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        public enum State { Field, ZoomIn, Encounter, ZoomOut }

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("cam")] private Camera _cam;
        [SerializeField, FormerlySerializedAs("room")] private FieldRoom _room;
        [SerializeField, FormerlySerializedAs("player")] private FieldPlayer _player;
        [SerializeField, FormerlySerializedAs("director")] private EyeMazeDirector _director;
        [Tooltip("눈 화면의 기준 (눈 오브젝트)")]
        [SerializeField, FormerlySerializedAs("eyeAnchor")] private Transform _eyeAnchor;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("state")] private State _state = State.Field;
        [SerializeField, FormerlySerializedAs("currentNpc")] private FieldNPC _currentNpc;
        [SerializeField, FormerlySerializedAs("fieldText"), TextArea(2, 4)] private string _fieldText;
        [SerializeField, FormerlySerializedAs("showingText")] private bool _showingText;

        [Header("카메라")]
        [SerializeField, FormerlySerializedAs("fieldCamSize")] private float _fieldCamSize = 4.5f;
        [SerializeField, FormerlySerializedAs("followAmount"), Range(0f, 1f)] private float _followAmount = 0.25f;
        [Tooltip("눈 화면 카메라 크기 (눈이 화면 가운데 위쪽, 아래는 대화 칸)")]
        [SerializeField, FormerlySerializedAs("eyeCamSize")] private float _eyeCamSize = 6.6f;
        [SerializeField, FormerlySerializedAs("eyeCamOffset")] private Vector2 _eyeCamOffset = new Vector2(0f, -2.1f);
        [SerializeField, FormerlySerializedAs("fieldBackground")] private Color _fieldBackground = Color.black;
        [SerializeField, FormerlySerializedAs("encounterBackground")] private Color _encounterBackground = Color.black;
        [SerializeField, FormerlySerializedAs("zoomInSize")] private float _zoomInSize = 1.1f;
        [SerializeField, FormerlySerializedAs("zoomDuration")] private float _zoomDuration = 0.7f;
        [SerializeField, FormerlySerializedAs("fadeSpeed")] private float _fadeSpeed = 3f;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("fade"), Range(0f, 1f)] private float _fade;

        [Header("대화 상자")]
        [Tooltip("지금 텍스트 상자에 보여줄 초상화 (없으면 글만)")]
        [SerializeField, FormerlySerializedAs("fieldPortrait")] private Sprite _fieldPortrait;

        [NonSerialized] private float _timer;
        [NonSerialized] private float _fadeTarget;
        [NonSerialized] private Vector3 _zoomFrom;
        [NonSerialized] private float _zoomFromSize;
        [NonSerialized] private bool _returnResolved;
        [NonSerialized] private List<string> _convLines;
        [NonSerialized] private string _convSpeaker;
        [NonSerialized] private List<string> _convSpeakers;
        [NonSerialized] private List<Sprite> _convPortraits;
        [NonSerialized] private int _convIndex;
        [NonSerialized] private Action _convDone;

        public State CurrentState { get => _state; set => _state = value; }
        public Camera Cam => _cam;
        public FieldPlayer Player => _player;
        public EyeMazeDirector Director => _director;
        public FieldNPC CurrentNpc => _currentNpc;
        public string FieldText => _fieldText;
        public bool ShowingText => _showingText;
        public Sprite FieldPortrait => _fieldPortrait;
        public float Fade => _fade;
        public FieldInteractable Nearest { get; private set; }

        private Vector3 EyeCamPos => _eyeAnchor != null ? _eyeAnchor.position + (Vector3)_eyeCamOffset : Vector3.zero;

        private Vector3 FieldCamTarget
        {
            get
            {
                Vector2 c = _room != null ? (Vector2)_room.transform.position : Vector2.zero;
                Vector2 p = _player != null ? _player.Position : c;
                return Vector2.Lerp(c, p, _followAmount);
            }
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            if (_director != null) _director.AcceptInput = _state == State.Encounter;
            if (_state == State.Field) PlaceCamera(FieldCamTarget, _fieldCamSize);
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            float dt = Time.deltaTime;
            if (OpeningMonologue.IsPlaying)
            {
                // 시작 독백 중에는 아무것도 움직이지 않는다.
                if (_player != null) _player.Stop();
                return;
            }

            bool interact = false;
            bool leave = false;
            Vector2 move = Vector2.zero;
            // 스펙 007: 대화 기록 창이 열려 있으면 키 입력을 처리하지 않는다.
            Keyboard kb = DialogueLog.BlocksInput ? null : Keyboard.current;
            Gamepad pad = DialogueLog.BlocksInput ? null : Gamepad.current;
            if (kb != null)
            {
                // 상호작용과 대사 넘기기는 Space로 한다. 만남 화면의 Space는 시선 조작이라 엔딩 대사와 파편 읽기에서만 넘기기로 쓴다.
                bool endingLines = _state == State.Encounter && _director != null && _director.CurrentMode == EyeMazeDirector.Mode.Ending;
                bool readingLines = _state == State.Encounter && _director != null && _director.IsReading;
                bool spaceAdvance = kb.spaceKey.wasPressedThisFrame && (_state == State.Field || endingLines);
                interact = kb.enterKey.wasPressedThisFrame || spaceAdvance;
                leave = kb.qKey.wasPressedThisFrame;
                bool skip = kb.xKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || spaceAdvance
                            || (readingLines && kb.spaceKey.wasPressedThisFrame);
                if (skip && Typewriter.AnyTyping) { Typewriter.SkipAll(); interact = false; }
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            }
            if (pad != null)
            {
                Vector2 s = pad.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) move += s;
                if (_state == State.Field && pad.buttonSouth.wasPressedThisFrame) interact = true;
                if (pad.buttonEast.wasPressedThisFrame) leave = true;
            }

            switch (_state)
            {
                case State.Field:
                    if (_director != null && _director.AcceptInput) _director.AcceptInput = false;
                    if (_showingText)
                    {
                        Nearest = null;
                        if (interact) AdvanceText();
                    }
                    else if (FieldCutscene.IsPlaying)
                    {
                        // 컷신 중에는 움직이지 않고 지켜본다.
                        Nearest = null;
                        if (_player != null) _player.Stop();
                    }
                    else
                    {
                        if (_player != null) _player.Steer(move, dt);
                        Nearest = FindNearest();
                        if (interact && Nearest != null) Nearest.Interact(this);
                    }
                    if (_cam != null)
                    {
                        Vector3 t = FieldCamTarget;
                        Vector3 cp = Vector3.Lerp(_cam.transform.position, new Vector3(t.x, t.y, _cam.transform.position.z), 1f - Mathf.Exp(-6f * dt));
                        _cam.transform.position = cp;
                        _cam.orthographicSize = _fieldCamSize;
                    }
                    break;

                case State.ZoomIn:
                    _timer += dt / Mathf.Max(0.05f, _zoomDuration);
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_timer));
                    if (_cam != null && _currentNpc != null)
                    {
                        Vector3 target = _currentNpc.transform.position;
                        _cam.transform.position = Vector3.Lerp(_zoomFrom, new Vector3(target.x, target.y, _zoomFrom.z), e);
                        _cam.orthographicSize = Mathf.Lerp(_zoomFromSize, _zoomInSize, e);
                    }
                    _fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, _timer));
                    if (_timer >= 1f) { _fade = 1f; EnterEncounterNow(); }
                    break;

                case State.Encounter:
                    _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);
                    if (_director != null)
                    {
                        if (_director.CurrentMode == EyeMazeDirector.Mode.Ending && interact)
                        {
                            if (_director.HeartFinished) BeginReturn(true);
                            else _director.NextHeartLine();
                        }
                        // 스펙 009: 대화 중 Q는 휴식 신호가 있으면 승낙을 기록하고 바로 나간다.
                        else if (_director.CurrentMode == EyeMazeDirector.Mode.Talk && leave) LeaveTalk();
                    }
                    break;

                case State.ZoomOut:
                    _fade = Mathf.MoveTowards(_fade, 1f, _fadeSpeed * dt);
                    if (_fade >= 1f) ReturnToFieldNow();
                    break;
            }

            if (_state == State.Field) _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);
        }

        /// <summary>
        /// 대화 중 자리를 뜬다. 휴식 신호가 있으면 쉬어 주기로 기록한 뒤 나간다.
        /// 디렉터의 휴식 신호를 사용하며, 디렉터의 휴식 상태와 화면 상태를 바꾼다.
        /// </summary>
        private void LeaveTalk()
        {
            if (_director.RestRequested) _director.AcceptRest();
            BeginReturn(false);
        }

        // 필드 글 상자

        /// <summary>
        /// 한 줄짜리 글(단서 등)을 글 상자에 띄운다.
        /// text를 사용하며, 대화 상태를 한 줄짜리로 연다.
        /// </summary>
        public void ShowText(string text) => ShowConversation(null, null, new List<string> { text }, null);

        /// <summary>
        /// 여러 줄 대화를 연다. Space로 한 줄씩 넘기고, 마지막 줄 다음 Space에서 닫히며 done이 불린다.
        /// speaker, portrait, lines, done을 사용하며, 글 상자 상태와 초상화를 바꾸고 플레이어를 멈춘다.
        /// </summary>
        public void ShowConversation(string speaker, Sprite portrait, List<string> lines, Action done)
        {
            _convLines = lines != null && lines.Count > 0 ? lines : new List<string> { "…" };
            _convSpeaker = speaker;
            _convSpeakers = null;
            _convPortraits = null;
            _convIndex = 0;
            _convDone = done;
            _fieldPortrait = portrait;
            _showingText = true;
            ShowConvLine();
            if (_player != null) _player.Stop();
        }

        /// <summary>
        /// 줄마다 말하는 사람과 초상화가 다른 대화를 연다 (필드 대화 CSV용).
        /// speakers, portraits, lines(같은 길이), done을 사용하며, 글 상자 상태를 바꾸고 플레이어를 멈춘다.
        /// </summary>
        public void ShowConversationLines(List<string> speakers, List<Sprite> portraits, List<string> lines, Action done)
        {
            ShowConversation(speakers != null && speakers.Count > 0 ? speakers[0] : null,
                portraits != null && portraits.Count > 0 ? portraits[0] : null, lines, done);
            _convSpeakers = speakers;
            _convPortraits = portraits;
            ShowConvLine();
        }

        /// <summary>
        /// 대화를 다음 줄로 넘기고, 마지막 줄이면 닫는다.
        /// 입력값은 없으며, 닫힐 때 완료 콜백을 부른다.
        /// </summary>
        public void AdvanceText()
        {
            if (_convLines != null && _convIndex < _convLines.Count - 1)
            {
                _convIndex++;
                ShowConvLine();
                return;
            }
            Action done = _convDone;
            CloseText();
            done?.Invoke();
        }

        /// <summary>
        /// 글 상자를 닫는다.
        /// 입력값은 없으며, 글과 초상화, 대화 상태를 비운다.
        /// </summary>
        public void CloseText()
        {
            _showingText = false;
            _fieldText = "";
            _fieldPortrait = null;
            _convLines = null;
            _convSpeakers = null;
            _convPortraits = null;
            _convDone = null;
        }

        /// <summary>
        /// 플레이어가 상호작용할 수 있는 가장 가까운 대상을 찾는다.
        /// 플레이어 위치와 각 대상의 상호작용 거리를 사용하며, 찾은 대상 또는 null을 반환한다.
        /// </summary>
        public FieldInteractable FindNearest()
        {
            if (_player == null) return null;
            Transform scope = _player.transform.parent;
            FieldInteractable[] all = scope != null ? scope.GetComponentsInChildren<FieldInteractable>(false)
                                                    : Object.FindObjectsByType<FieldInteractable>(FindObjectsSortMode.None);
            FieldInteractable best = null;
            float bestD = float.MaxValue;
            foreach (FieldInteractable it in all)
            {
                if (!it.Available) continue;
                float d = it.DistanceTo(_player.Position, _player.Radius, _player.SideView);
                if (d > it.InteractRange || d >= bestD) continue;
                best = it;
                bestD = d;
            }
            return best;
        }

        // 화면 전환

        /// <summary>
        /// 사람의 눈 속으로 들어가기 시작한다. Play 모드에서는 줌인 연출을 거치고, 에디트 모드에서는 바로 들어간다.
        /// npc를 사용하며, 현재 상대와 흐름 상태를 바꾼다.
        /// </summary>
        public void BeginEncounter(FieldNPC npc)
        {
            if (_state != State.Field) return;
            _currentNpc = npc;
            if (_player != null) _player.Stop();
            CloseText();
            if (!Application.isPlaying) { EnterEncounterNow(); return; }
            _state = State.ZoomIn;
            _timer = 0f;
            _zoomFrom = _cam != null ? _cam.transform.position : Vector3.zero;
            _zoomFromSize = _cam != null ? _cam.orthographicSize : _fieldCamSize;
        }

        /// <summary>
        /// 바로 만남 화면으로 바꾼다. 에디트 모드 버튼과 줌인 끝에서 쓴다.
        /// 현재 상대를 사용하며, 카메라와 디렉터, 상담 케이스, 안내 문구를 바꾼다.
        /// </summary>
        public void EnterEncounterNow()
        {
            _state = State.Encounter;
            PlaceCamera(EyeCamPos, _eyeCamSize);
            if (_cam != null) _cam.backgroundColor = _encounterBackground;
            if (_director != null)
            {
                _director.AcceptInput = true;
                if (_director.CurrentMode == EyeMazeDirector.Mode.Gaze) _director.ExitGaze("");
            }
            // 이 사람의 상담 케이스를 불러온다. 이미 불러온 사람이면 진행 상황을 유지한다.
            if (_director != null && _currentNpc != null)
            {
                ClientCase c = FindCase(_currentNpc);
                if (c != null && _director.ActiveCase != c) _director.LoadCase(c);
            }
            bool first = _currentNpc == null || !_currentNpc.Met;
            if (_currentNpc != null && Application.isPlaying) _currentNpc.Met = true;
            if (_director != null && Application.isPlaying) _director.ShowEncounterGuide(first);
            _fadeTarget = 0f;
        }

        /// <summary>
        /// 필드의 사람에 해당하는 상담 케이스를 찾는다.
        /// npc를 사용하며, 찾은 ClientCase 또는 null을 반환한다.
        /// </summary>
        public static ClientCase FindCase(FieldNPC npc)
        {
            if (npc == null) return null;
            foreach (ClientCase c in FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.Npc == npc) return c;
            return null;
        }

        /// <summary>
        /// 필드로 돌아가기 시작한다. Play 모드에서는 암전 연출을 거친다.
        /// resolved(상담이 끝났는지)를 사용하며, 흐름 상태와 디렉터 입력을 바꾼다.
        /// </summary>
        public void BeginReturn(bool resolved)
        {
            if (_state != State.Encounter) return;
            _returnResolved = resolved;
            if (!Application.isPlaying) { ReturnToFieldNow(); return; }
            _state = State.ZoomOut;
            _fadeTarget = 1f;
            if (_director != null) _director.AcceptInput = false;
        }

        /// <summary>
        /// 바로 필드 화면으로 바꾼다. 에디트 모드 버튼과 줌아웃 끝에서 쓴다.
        /// 상담이 끝났으면 상대를 해결 상태로 바꾸고 결말 뒤 대사를 띄운다.
        /// </summary>
        public void ReturnToFieldNow()
        {
            _state = State.Field;
            if (_director != null)
            {
                _director.AcceptInput = false;
                if (_director.CurrentMode == EyeMazeDirector.Mode.Gaze) _director.ExitGaze("");
            }
            PlaceCamera(FieldCamTarget, _fieldCamSize);
            if (_cam != null) _cam.backgroundColor = _fieldBackground;
            _fadeTarget = 0f;
            if (_returnResolved && _currentNpc != null)
            {
                _currentNpc.Resolved = true;
                EndingDef ending = _director != null ? _director.CurrentEnding : null;
                if (ending != null && !string.IsNullOrEmpty(ending.AfterLine)) _currentNpc.LineAfterResolved = ending.AfterLine;
                ShowConversation(_currentNpc.DisplayName, _currentNpc.Portrait, new List<string> { _currentNpc.LineAfterResolved }, null);
            }
            // 스펙 009: 쉬어 주고 돌아오면 상담실에서 내담자의 리액션을 보여 준다.
            else if (_director != null && _director.RestAccepted && _currentNpc != null) _currentNpc.TryTalk(this, TalkScript.Rest, null, null);
            _returnResolved = false;
        }

        /// <summary>
        /// 지금 대화 줄을 글 상자 문자열로 만든다.
        /// _convLines, _convIndex, _convSpeaker를 사용하며, _fieldText를 바꾼다.
        /// </summary>
        private void ShowConvLine()
        {
            int i = Mathf.Clamp(_convIndex, 0, _convLines.Count - 1);
            string line = _convLines[i];
            if (_convSpeakers != null && i < _convSpeakers.Count) _convSpeaker = _convSpeakers[i];
            if (_convPortraits != null && i < _convPortraits.Count) _fieldPortrait = _convPortraits[i];
            _fieldText = string.IsNullOrEmpty(_convSpeaker) ? line : $"{_convSpeaker}\n{line}";
            // 스펙 007: 글 상자에 나온 줄을 얼굴과 함께 대화 기록에 남긴다. 같은 줄을 다시 보여줘도 한 번만 남는다.
            DialogueLog.Add(_convSpeaker, line, false, string.IsNullOrEmpty(_convSpeaker) ? null : _fieldPortrait);
        }

        /// <summary>
        /// 카메라 위치와 크기를 정한다.
        /// pos, size를 사용하며, 카메라 Transform과 orthographicSize를 바꾼다.
        /// </summary>
        private void PlaceCamera(Vector3 pos, float size)
        {
            if (_cam == null) return;
            _cam.transform.position = new Vector3(pos.x, pos.y, _cam.transform.position.z);
            _cam.orthographicSize = size;
        }
    }
}
