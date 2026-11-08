using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 손님 입장 컷신 (Play 모드에서만).
    /// 오프닝 독백이 끝나면 어머니가 문으로 들어와 대화하고 나가며, 은재가 들어와 창가 의자에 앉는다.
    /// 상담이 끝나면 그 손님이 나가고 다음 손님(ClientCase 순서)이 들어와 앉아 첫마디를 한다.
    /// 컷신 동안 플레이어는 움직일 수 없고 대사만 넘긴다. 끄면 어머니가 소파에 앉아 있고 말을 걸면 대화한다.
    /// </summary>
    public class FieldCutscene : MonoBehaviour
    {
        public enum Step
        {
            WaitOpening, Delay, MotherIn, Talk, Talking, MotherOut, Between, DaughterIn, Sitting, Reveal, Done,
            WaitResolved, ClientOut, ClientBetween, ClientIn, ClientSitting, ClientGreet, AllDone
        }

        private static FieldCutscene _current;

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("flow")] private GameFlow _flow;
        [SerializeField, FormerlySerializedAs("mother")] private FieldNPC _mother;
        [SerializeField, FormerlySerializedAs("daughter")] private FieldNPC _daughter;

        [Header("연출")]
        [Tooltip("문 위치 (x). 여기서 들어오고 나간다")]
        [SerializeField, FormerlySerializedAs("doorX")] private float _doorX = 6.4f;
        [Tooltip("어머니가 멈춰 서는 곳: 플레이어 오른쪽으로 이만큼")]
        [SerializeField, FormerlySerializedAs("talkDistance")] private float _talkDistance = 1.1f;
        [Tooltip("오프닝이 끝난 뒤 문이 열리기까지 (초)")]
        [SerializeField, FormerlySerializedAs("startDelay")] private float _startDelay = 0.8f;
        [Tooltip("앞사람이 나간 뒤 다음 사람이 들어오기까지 (초)")]
        [SerializeField, FormerlySerializedAs("betweenDelay")] private float _betweenDelay = 1.0f;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("step")] private Step _step = Step.WaitOpening;

        [NonSerialized] private float _timer;
        [NonSerialized] private bool _hidInitial;
        [NonSerialized] private FieldNPC _seated;
        [NonSerialized] private ClientCase _nextCase;

        /// <summary>컷신 중이면 플레이어 이동/상호작용을 막는다 (대사 넘기기는 가능).</summary>
        public static bool IsPlaying => _current != null && _current.isActiveAndEnabled && Application.isPlaying
                                        && _current._step != Step.WaitResolved && _current._step != Step.AllDone;

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
                    if (!OpeningMonologue.IsPlaying) { _step = Step.Delay; _timer = _startDelay; }
                    break;

                case Step.Delay:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f)
                    {
                        _step = Step.MotherIn;
                        _mother.StandAt(_doorX);
                        float px = _flow.Player != null ? _flow.Player.Position.x : 0f;
                        _mother.WalkTo(px + _talkDistance, () => _step = Step.Talk);
                    }
                    break;

                case Step.Talk:
                    // 대화가 끝나면 콜백에서 다음 단계로 넘어간다.
                    _step = Step.Talking;
                    _flow.ShowConversation(_mother.DisplayName, _mother.Portrait, _mother.TalkLines, () =>
                    {
                        _step = Step.MotherOut;
                        _mother.WalkTo(_doorX, () => { _mother.Present = false; _step = Step.Between; _timer = _betweenDelay; });
                    });
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
                                if (!string.IsNullOrEmpty(_mother.RevealLine))
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
                                if (!string.IsNullOrEmpty(_nextCase.EntryLine))
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
