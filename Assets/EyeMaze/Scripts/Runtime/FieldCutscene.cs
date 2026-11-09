using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 손님 입장 컷신 (Play 모드에서만).
    /// 오프닝 독백이 끝나면 문에 호출 글씨가 뜨고 주인공의 혼잣말 두 줄이 이어진다. 플레이어가 문을 열어 주면 어머니가 들어와 대화하고 나가며,
    /// 은재가 들어와 창가 의자에 앉는다. 상담이 끝나면 그 손님이 나가고 다음 손님(ClientCase 순서)이 들어와 앉아 첫마디를 한다.
    /// 문을 기다리는 동안을 빼고, 컷신 동안 플레이어는 움직일 수 없고 대사만 넘긴다. 끄면 어머니가 소파에 앉아 있고 말을 걸면 대화한다.
    /// </summary>
    public class FieldCutscene : MonoBehaviour
    {
        public enum Step
        {
            WaitOpening, CallFirst, CallTalking, CallSecond, WaitDoor, Arrive, Pause, Talk, Talking, MotherOut, Between, DaughterIn, Sitting, Reveal, Done,
            WaitResolved, ClientOut, ClientBetween, ClientIn, ClientSitting, ClientGreet, AllDone
        }

        private static FieldCutscene _current;

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("flow")] private GameFlow _flow;
        [SerializeField, FormerlySerializedAs("mother")] private FieldNPC _mother;
        [SerializeField, FormerlySerializedAs("daughter")] private FieldNPC _daughter;
        [Tooltip("독백 뒤 울리는 상담실 문. 플레이어가 열어 주면 어머니가 들어온다")]
        [SerializeField] private FieldDoor _door;

        [Header("연출")]
        [Tooltip("문 위치 (x). 여기서 들어오고 나간다")]
        [SerializeField, FormerlySerializedAs("doorX")] private float _doorX = 6.4f;
        [Tooltip("앞사람이 나간 뒤 다음 사람이 들어오기까지 (초)")]
        [SerializeField, FormerlySerializedAs("betweenDelay")] private float _betweenDelay = 1.0f;

        [Header("문 앞 호출")]
        [Tooltip("독백이 걷히고 띵-동이 뜬 뒤 첫 혼잣말이 나오기까지 (초)")]
        [SerializeField] private float _firstCallDelay = 2f;
        [Tooltip("첫 혼잣말을 넘긴 뒤 띵-동이 다시 뜨고, 넘김 입력(Z·Enter·Space)을 받기 시작하기까지 (초). 입력이 있어야 두 번째 혼잣말이 열린다")]
        [SerializeField] private float _secondCallDelay = 0.5f;
        [Tooltip("첫 혼잣말. Strings.csv의 field.call_first가 있으면 그걸 쓴다")]
        [SerializeField, TextArea(1, 3)] private string _firstCallLine = "뭐야? 한창 분위기 잡고있는데.";
        [Tooltip("두 번째 혼잣말. Strings.csv의 field.call_second가 있으면 그걸 쓴다")]
        [SerializeField, TextArea(1, 3)] private string _secondCallLine = "아직 첫 상담까지는 시간이 남았을 텐데...";
        [Tooltip("문을 열어 줄 때까지 필드 조작 안내 자리에 띄우는 문구. Strings.csv의 hint.door가 있으면 그걸 쓴다")]
        [SerializeField] private string _doorGuide = "문을 열어서 손님을 맞이하세요.";

        [Header("첫 손님 맞이")]
        [Tooltip("출입구에서 방 안쪽을 향하는 x 방향 (-1: 왼쪽, 1: 오른쪽)")]
        [SerializeField] private float _inwardSign = -1f;
        [Tooltip("어머니가 출입구에서 방 안쪽으로 걸어 들어오는 한 걸음 거리")]
        [SerializeField] private float _motherStep = 0.6f;
        [Tooltip("어머니가 출입구에 나타난 뒤 걷기 시작할 때까지 서 있는 시간 (초)")]
        [SerializeField] private float _motherWaitSeconds = 1f;
        [Tooltip("어머니가 한 걸음을 출발해서 도착하기까지 걸리는 시간 (초)")]
        [SerializeField] private float _motherWalkSeconds = 1f;
        [Tooltip("플레이어가 출입구에서 멀어지며 물러서는 한 걸음 거리")]
        [SerializeField] private float _playerStep = 0.6f;
        [Tooltip("플레이어가 한 걸음 물러서는 데 걸리는 시간 (초)")]
        [SerializeField] private float _playerStepSeconds = 0.35f;
        [Tooltip("두 사람 사이 최소 간격. 플레이어가 문에 너무 붙어 있으면 이만큼 떨어지도록 더 물러선다")]
        [SerializeField, FormerlySerializedAs("talkDistance")] private float _talkDistance = 1.1f;
        [Tooltip("두 사람의 이동이 모두 끝난 뒤 대화가 시작되기까지 (초)")]
        [SerializeField] private float _talkDelay = 2f;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("step")] private Step _step = Step.WaitOpening;

        [NonSerialized] private float _timer;
        [NonSerialized] private bool _hidInitial;
        [NonSerialized] private FieldNPC _seated;
        [NonSerialized] private ClientCase _nextCase;
        [NonSerialized] private bool _motherArrived;
        [NonSerialized] private float _motherWaitT;
        [NonSerialized] private float _motherToX;
        [NonSerialized] private float _playerFromX;
        [NonSerialized] private float _playerToX;
        [NonSerialized] private float _playerStepT;

        /// <summary>컷신 중이면 플레이어 이동/상호작용을 막는다 (대사 넘기기는 가능). 문을 기다리는 동안은 막지 않는다.</summary>
        public static bool IsPlaying => _current != null && _current.isActiveAndEnabled && Application.isPlaying
                                        && _current._step != Step.WaitDoor
                                        && _current._step != Step.WaitResolved && _current._step != Step.AllDone;

        /// <summary>문을 열어 주기를 기다리는 동안의 안내 문구. 그 밖에는 null.</summary>
        public static string DoorGuide => _current != null && _current.isActiveAndEnabled && Application.isPlaying
                                          && _current._step == Step.WaitDoor ? Strings.Get("hint.door", _current._doorGuide) : null;

        void OnEnable() => _current = this;

        void Start()
        {
            if (!Application.isPlaying) return;
            _step = Step.WaitOpening;
        }

        void Update()
        {
            if (!Application.isPlaying || _flow == null || _mother == null || _daughter == null) return;

            // 시작하자마자 손님들을 모두 방 밖으로 내보낸다.
            if (!_hidInitial)
            {
                _hidInitial = true;
                _mother.Present = false;
                _daughter.Present = false;
                foreach (ClientCase c in Cases()) if (c.Npc != null) c.Npc.Present = false;
            }

            switch (_step)
            {
                case Step.WaitOpening:
                    if (!OpeningMonologue.IsPlaying) { _step = Step.CallFirst; _timer = _firstCallDelay; _door.ShowBell(true); }
                    break;

                case Step.CallFirst:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) ShowCallLine(Strings.Get("field.call_first", _firstCallLine), () => { _step = Step.CallSecond; _timer = _secondCallDelay; _door.ShowBell(true); });
                    break;

                case Step.CallSecond:
                    // 대기 시간이 지나도 플레이어가 넘겨야 두 번째 혼잣말이 열린다. 대기는 A를 닫은 입력이 바로 B를 여는 것도 막는다.
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f && AdvancePressed()) ShowCallLine(Strings.Get("field.call_second", _secondCallLine), () => { _step = Step.WaitDoor; _door.Ring(); });
                    break;

                case Step.WaitDoor:
                    // 플레이어가 문을 열어 줄 때까지 자동으로 진행하지 않는다.
                    if (_door.Answered) BeginArrival();
                    break;

                case Step.Arrive:
                    StepPlayer(Time.deltaTime);
                    // 어머니는 출입구에 잠시 서 있다가 걷기 시작한다. 걷기 시작은 한 번만 한다.
                    if (_motherWaitT > 0f)
                    {
                        _motherWaitT -= Time.deltaTime;
                        if (_motherWaitT <= 0f) _mother.WalkToIn(_motherToX, _motherWalkSeconds, () => _motherArrived = true);
                    }
                    if (_motherArrived && _playerStepT >= 1f) { _step = Step.Pause; _timer = _talkDelay; }
                    break;

                case Step.Pause:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) _step = Step.Talk;
                    break;

                case Step.Talk:
                    // 대화가 끝나면 콜백에서 다음 단계로 넘어간다.
                    _step = Step.Talking;
                    Action motherDone = () =>
                    {
                        _step = Step.MotherOut;
                        _mother.WalkTo(_doorX, () => { _mother.Present = false; _step = Step.Between; _timer = _betweenDelay; });
                    };
                    // 필드 대화 CSV의 enter 대화가 있으면 그걸, 없으면 인스펙터의 talkLines를 쓴다.
                    if (!_mother.TryTalk(_flow, TalkScript.Enter, null, motherDone))
                        _flow.ShowConversation(_mother.DisplayName, _mother.Portrait, _mother.TalkLines, motherDone);
                    break;

                case Step.Between:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f)
                    {
                        _step = Step.DaughterIn;
                        float seatX = _daughter.SeatPosition.x;
                        _daughter.StandAt(_doorX);
                        _daughter.WalkTo(seatX, () =>
                        {
                            _step = Step.Sitting;
                            _daughter.SitDown(() =>
                            {
                                _step = Step.Reveal;
                                if (_mother.TryTalk(_flow, TalkScript.Reveal, _daughter.Portrait, () => { _seated = _daughter; _step = Step.WaitResolved; })) { }
                                else if (!string.IsNullOrEmpty(_mother.RevealLine))
                                    _flow.ShowConversation(null, _daughter.Portrait, new List<string> { _mother.RevealLine }, () => { _seated = _daughter; _step = Step.WaitResolved; });
                                else { _seated = _daughter; _step = Step.WaitResolved; }
                            });
                        });
                    }
                    break;

                case Step.WaitResolved:
                    if (_seated != null && _seated.Resolved && _flow.CurrentState == GameFlow.State.Field && !_flow.ShowingText)
                    {
                        _nextCase = NextCase(_seated);
                        if (_nextCase == null || _nextCase.Npc == null) { _step = Step.AllDone; break; }
                        _step = Step.ClientOut;
                        FieldNPC leaving = _seated;
                        leaving.WalkTo(_doorX, () => { leaving.Present = false; _step = Step.ClientBetween; _timer = _betweenDelay; });
                    }
                    break;

                case Step.ClientBetween:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f)
                    {
                        _step = Step.ClientIn;
                        FieldNPC npc = _nextCase.Npc;
                        float seatX = npc.SeatPosition.x;
                        npc.StandAt(_doorX);
                        npc.WalkTo(seatX, () =>
                        {
                            _step = Step.ClientSitting;
                            npc.SitDown(() =>
                            {
                                _seated = npc;
                                if (npc.TryTalk(_flow, TalkScript.Enter, null, () => _step = Step.WaitResolved)) _step = Step.ClientGreet;
                                else if (!string.IsNullOrEmpty(_nextCase.EntryLine))
                                {
                                    _step = Step.ClientGreet;
                                    _flow.ShowConversation(_nextCase.PersonName, npc.Portrait, new List<string> { _nextCase.EntryLine }, () => _step = Step.WaitResolved);
                                }
                                else _step = Step.WaitResolved;
                            });
                        });
                    }
                    break;
            }
        }

        /// <summary>
        /// 문 앞 혼잣말 한 줄을 화자 이름과 초상화 없이 띄운다. 대사창이 열려 있는 동안 호출 글씨는 숨긴다.
        /// line과 닫힌 뒤 부를 done을 사용하며, _step을 CallTalking으로 바꾸고 글 상자를 연다.
        /// </summary>
        private void ShowCallLine(string line, Action done)
        {
            _step = Step.CallTalking;
            _door.ShowBell(false);
            _flow.ShowConversation(null, null, new List<string> { line }, done);
        }

        /// <summary>
        /// 이번 프레임에 대사 넘김 입력이 들어왔는지 확인한다.
        /// 키보드 Z·Enter·Space와 게임패드 아래 버튼을 사용하며, 눌렸으면 true를 반환한다.
        /// </summary>
        private static bool AdvancePressed()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (kb != null && (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>
        /// 문을 열어 준 직후 어머니를 출입구에 세우고 플레이어의 후퇴를 시작한다. 어머니의 전진은 _motherWaitSeconds 뒤에 시작한다.
        /// _doorX, _inwardSign, 한 걸음 거리와 _talkDistance, 플레이어 위치를 사용하며, _step과 두 사람의 이동 상태를 바꾼다.
        /// </summary>
        private void BeginArrival()
        {
            _step = Step.Arrive;
            float motherX = _doorX + _inwardSign * _motherStep;
            _motherArrived = false;
            _motherToX = motherX;
            _motherWaitT = Mathf.Max(0.0001f, _motherWaitSeconds);
            _mother.StandAt(_doorX);

            FieldPlayer player = _flow.Player;
            _playerFromX = player.Position.x;
            _playerToX = _playerFromX + _inwardSign * _playerStep;
            // 문에 바짝 붙어 있었다면 어머니와 겹치지 않도록 최소 간격까지 더 물러선다.
            float limitX = motherX + _inwardSign * _talkDistance;
            if ((_playerToX - limitX) * _inwardSign < 0f) _playerToX = limitX;
            _playerStepT = 0f;
            // 물러서면서도 들어오는 손님 쪽을 바라본다.
            player.Facing = new Vector2(-Mathf.Sign(_inwardSign), 0f);
        }

        /// <summary>
        /// 플레이어의 한 걸음 후퇴를 한 프레임 진행한다.
        /// dt와 _playerStepSeconds를 사용하며, _playerStepT와 플레이어 위치를 바꾼다.
        /// </summary>
        private void StepPlayer(float dt)
        {
            if (_playerStepT >= 1f) return;
            _playerStepT = Mathf.Min(1f, _playerStepT + dt / Mathf.Max(0.01f, _playerStepSeconds));
            float x = Mathf.Lerp(_playerFromX, _playerToX, Mathf.SmoothStep(0f, 1f, _playerStepT));
            FieldPlayer player = _flow.Player;
            player.MoveBy(new Vector2(x - player.Position.x, 0f));
        }

        /// <summary>
        /// 씬의 상담 케이스를 순서대로 모은다.
        /// 각 케이스의 Order를 사용하며, 정렬된 목록을 반환한다.
        /// </summary>
        private static List<ClientCase> Cases()
        {
            var list = new List<ClientCase>(FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            list.Sort((a, b) => a.Order.CompareTo(b.Order));
            return list;
        }

        /// <summary>
        /// 지금 손님 다음 순서의 케이스를 찾는다.
        /// after(지금 앉아 있는 사람)를 사용하며, 다음 ClientCase 또는 null을 반환한다.
        /// </summary>
        private static ClientCase NextCase(FieldNPC after)
        {
            List<ClientCase> list = Cases();
            int i = list.FindIndex(c => c.Npc == after);
            return i >= 0 && i + 1 < list.Count ? list[i + 1] : null;
        }
    }
}
