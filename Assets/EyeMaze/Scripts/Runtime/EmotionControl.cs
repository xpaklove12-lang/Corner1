using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 상대의 감정을 바꾸는 규칙을 맡는다. 대화 선택지와 미로 안의 화제 짚기로 감정을 바꾸고,
    /// 쉼터에 머무는 동안 한 단계씩 가라앉힌다. 감정 값은 Person에 둔다.
    /// </summary>
    public class EmotionControl : MonoBehaviour
    {
        /// <summary>미로 안에서 숫자키로 짚을 수 있는 화제 수</summary>
        public const int VISIBLE_TOPICS = 4;

        private static readonly string[] STAGE1_NAMES = { "평온", "경계", "동요", "슬픔", "희망" };
        private static readonly string[] STAGE2_NAMES = { "평온", "반감", "격동", "침잠", "열망" };
        private static readonly string[] CSV_NAMES = { "", "guard", "stir", "sad", "hope" };

        [Header("연결")]
        [Tooltip("감정 값을 가진 상대")]
        [SerializeField] private Person _person;
        [Tooltip("경계와 반감일 때 경계 애니메이션을 켤 초상화")]
        [SerializeField] private WaryStance _waryStance;
        [Tooltip("감정별 표정 그림을 보여 줄 초상화")]
        [SerializeField] private EmotionPortrait _portrait;

        [Header("수치")]
        [Tooltip("쉼터에서 한 단계 내려가는 시간(초)")]
        [SerializeField, Min(0.1f)] private float _restStepSeconds = 2.5f;
        [Tooltip("평온일 때 부담감 누적 속도 배율")]
        [SerializeField, Min(0f)] private float _calmBurdenScale = 0.6f;
        [Tooltip("2단계일 때 부담감 누적 속도 배율")]
        [SerializeField, Min(0f)] private float _stage2BurdenScale = 1.5f;
        [Tooltip("슬픔과 침잠일 때 시선 속도 배율")]
        [SerializeField, Range(0.05f, 1f)] private float _sadGazeScale = 0.8f;
        // 스펙 046 FR-047: 희망과 열망에서 장애물에 맞았을 때의 값
        [Tooltip("희망과 열망에서 장애물에 맞을 때 오르는 부담감에 곱하는 배율")]
        [SerializeField, Min(1f)] private float _hopeHitBurdenScale = 2f;
        [Tooltip("희망과 열망에서 이만큼 맞으면 희망이 꺾여 경계가 된다")]
        [SerializeField, Min(1)] private int _hopeBreakHits = 3;
        [Tooltip("쉼터 안에서 초당 줄어드는 부담감 (0~1)")]
        [SerializeField, Min(0f)] private float _restBurdenReliefPerSecond = 0.04f;
        private readonly List<Topic> _topics = new List<Topic>();
        private float _restT;
        private int _hopeHits;

        [Header("문구")]
        [Tooltip("2단계에서 같은 감정의 화제를 또 짚었을 때")]
        [SerializeField, TextArea(1, 2)] private string _alreadyMaxMessage = "더는 밀어붙일 수 없다.";
        [Tooltip("반감이 되었는데 그 층에 드러낼 숨긴 파편이 없을 때")]
        [SerializeField, TextArea(1, 2)] private string _nothingHiddenMessage = "이 층에는 숨긴 것이 없다.";
        [Tooltip("희망이 꺾여 경계가 되었을 때")]
        [SerializeField, TextArea(1, 2)] private string _hopeBrokenMessage = "희망이 꺾였다. 은재가 다시 경계한다.";
        [Tooltip("감정이 바뀔 때 나오는 얻는 것과 잃는 것. 순서: 평온, 경계, 반감, 동요, 격동, 슬픔, 침잠, 희망, 열망")]
        [SerializeField, TextArea(1, 3)] private string[] _stateMessages =
        {
            "평온 · 장애물이 느려진다.",
            "경계 · 장애물이 기억을 지키러 모인다.",
            "반감 · 숨긴 기억이 드러난다.",
            "동요 · 버티면 가까운 벽이 흔들려 무너진다.",
            "격동 · 벽이 더 빨리, 더 멀리 무너진다.",
            "슬픔 · 장애물을 지나가지만 앞이 흐려진다.",
            "침잠 · 장애물 속에서 느려지지 않는다.",
            "희망 · 장애물이 비켜 주지만 맞으면 크게 흔들린다.",
            "열망 · 드러난 기억이 끌려온다.",
        };

        /// <summary>화제 하나. 대화에서 얻고 미로 안에서 짚는다.</summary>
        public struct Topic
        {
            public string Text { get; set; }
            public Emotion Emotion { get; set; }
        }

        /// <summary>감정이나 단계가 바뀐 직후에 불린다.</summary>
        public event Action Changed;

        public Emotion Current => _person.Emotion;
        public int Stage => _person.EmotionStage;
        /// <summary>지금까지 얻은 화제. 앞의 VISIBLE_TOPICS개만 짚을 수 있다.</summary>
        public IReadOnlyList<Topic> Topics => _topics;
        /// <summary>지금 상태의 이름. 2단계는 2단계 이름이다.</summary>
        public string DisplayName => NameOf(Current, Stage);
        /// <summary>슬픔과 침잠일 때 시선 속도에 곱하는 배율</summary>
        public float SadGazeScale => _sadGazeScale;
        /// <summary>반감이 되었는데 그 층에 드러낼 숨긴 파편이 없을 때의 문구</summary>
        public string NothingHiddenMessage => _nothingHiddenMessage;
        /// <summary>스펙 046 FR-039: 쉼터 안에서 초당 줄어드는 부담감</summary>
        public float RestBurdenReliefPerSecond => _restBurdenReliefPerSecond;
        /// <summary>스펙 046 FR-047: 장애물에 맞을 때 오르는 부담감에 곱할 배율. 희망과 열망에서만 1보다 크다.</summary>
        public float HitBurdenScale => Current == Emotion.Hope ? _hopeHitBurdenScale : 1f;

        /// <summary>시간으로 쌓이는 부담감에 곱할 배율. 평온은 느리게, 2단계는 빠르게 찬다.</summary>
        public float BurdenScale
        {
            get
            {
                if (Current == Emotion.Calm) return _calmBurdenScale;
                return Stage >= 2 ? _stage2BurdenScale : 1f;
            }
        }

        /// <summary>
        /// 스펙 046 FR-047: 장애물에 맞은 것을 알린다. 희망과 열망이면 맞은 횟수를 세고, 정해진 횟수에 닿으면 희망이 꺾여 경계 1단계가 된다.
        /// 지금 감정과 _hopeBreakHits를 사용하며, _hopeHits와 상대의 감정을 바꾸고 꺾였으면 문구를, 아니면 null을 반환한다.
        /// </summary>
        public string RegisterHit()
        {
            if (Current != Emotion.Hope) return null;
            _hopeHits++;
            if (_hopeHits < _hopeBreakHits) return null;
            Set(Emotion.Guard, 1);
            return _hopeBrokenMessage;
        }

        /// <summary>
        /// 감정과 단계의 화면용 이름을 만든다.
        /// emotion과 stage를 사용하며, 1단계 이름 또는 2단계 이름을 반환한다.
        /// </summary>
        public static string NameOf(Emotion emotion, int stage)
        {
            return stage >= 2 ? STAGE2_NAMES[(int)emotion] : STAGE1_NAMES[(int)emotion];
        }

        /// <summary>
        /// 대화 CSV의 emotion 칸 값을 감정으로 바꾼다.
        /// value(guard, stir, sad, hope)를 사용하며, 알맞은 값이면 true를 반환하고 emotion에 쓴다.
        /// </summary>
        public static bool TryParse(string value, out Emotion emotion)
        {
            emotion = Emotion.Calm;
            for (int i = 1; i < CSV_NAMES.Length; i++)
            {
                if (!string.Equals(CSV_NAMES[i], value, StringComparison.OrdinalIgnoreCase)) continue;
                emotion = (Emotion)i;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 감정을 그 인물의 시작 감정으로 되돌리고 화제 목록과 쉼터 시간을 비운다. 인물을 불러오거나 만남을 다시 시작할 때 부른다.
        /// start(시작 감정)를 사용하며, 상대의 감정과 단계, _topics, _restT를 바꾼다. 평온이 아니면 그 감정의 화제 하나를 넣는다.
        /// </summary>
        public void ResetState(Emotion start)
        {
            _topics.Clear();
            _restT = 0f;
            _hopeHits = 0;
            if (start != Emotion.Calm) _topics.Add(new Topic { Text = STAGE1_NAMES[(int)start], Emotion = start });
            Set(start, start == Emotion.Calm ? 0 : 1);
        }

        /// <summary>
        /// 대화에서 고른 선택지의 감정과 화제를 적용한다. 지금 감정이 무엇이든 그 감정의 1단계가 된다.
        /// choice의 Emotion과 Topic을 사용하며, 상대의 감정과 _topics를 바꾸고 감정이 바뀌었으면 상태 문구를, 아니면 null을 반환한다.
        /// </summary>
        public string ApplyChoice(DialogueScript.Choice choice)
        {
            if (!TryParse(choice.Emotion, out Emotion emotion)) return null;
            if (!string.IsNullOrEmpty(choice.Topic) && !HasTopic(choice.Topic))
            {
                // 감정마다 화제를 하나만 두어, 네 감정의 화제가 늘 숫자키 1~4 안에 들어오게 한다.
                var topic = new Topic { Text = choice.Topic, Emotion = emotion };
                int same = _topics.FindIndex(t => t.Emotion == emotion);
                if (same >= 0) _topics[same] = topic;
                else _topics.Add(topic);
            }
            return Set(emotion, 1) ? StateMessage() : null;
        }

        /// <summary>
        /// 미로 안에서 화제를 짚는다. 다른 감정이면 그 감정의 1단계로, 같은 감정의 1단계면 2단계로 바뀐다.
        /// 화제 번호 index를 사용하며, 상대의 감정과 _restT를 바꾸고 문구 줄에 보일 글을, 짚을 화제가 없으면 null을 반환한다.
        /// </summary>
        public string PressTopic(int index)
        {
            if (index < 0 || index >= _topics.Count || index >= VISIBLE_TOPICS) return null;
            Topic topic = _topics[index];
            if (topic.Emotion == Current && Stage >= 2) return _alreadyMaxMessage;
            // 쉼터 안에서 짚어 감정이 바뀌면 가라앉던 시간을 처음부터 다시 센다.
            _restT = 0f;
            if (topic.Emotion != Current) { Set(topic.Emotion, 1); return StateMessage(); }
            Set(topic.Emotion, 2);
            return StateMessage();
        }

        /// <summary>
        /// 쉼터 시간을 진행한다. 시선이 쉼터 안에 있으면 정해진 시간마다 한 단계 내려가고, 밖이면 시간을 0으로 돌린다.
        /// dt, running(미로를 조작하는 중인지), gazeWorld(시선의 월드 위치)를 사용하며, _restT와 상대의 감정을 바꾸고 내려갔으면 상태 문구를, 아니면 null을 반환한다.
        /// </summary>
        public string TickRest(float dt, bool running, Vector3 gazeWorld)
        {
            // 대화로 나가 있거나 깜빡이는 동안에는 시간을 멈춘다.
            if (!running) return null;
            if (Current == Emotion.Calm || !InRestSpot(gazeWorld)) { _restT = 0f; return null; }
            _restT += dt;
            if (_restT < _restStepSeconds) return null;
            _restT = 0f;
            if (Stage >= 2) Set(Current, 1);
            else Set(Emotion.Calm, 0);
            return StateMessage();
        }

        /// <summary>
        /// 경계와 반감일 때만 초상화의 경계 애니메이션을 켜고, 표정 그림이 있는 감정이면 그 그림을 보여 준다. 만남을 떠나 있으면 끈다.
        /// inEncounter(만남 화면에 있는지)와 tears(눈물 초상화를 보여 줄 때인지)를 사용하며, 초상화 그림과 WaryStance의 경계 상태를 바꾼다.
        /// </summary>
        public void SyncWary(bool inEncounter, bool tears)
        {
            // 애니메이터를 다시 켠 뒤에 경계 애니메이션을 재생하도록 표정 그림을 먼저 맞춘다.
            bool wasAnimating = _portrait.Animating;
            _portrait.Show(inEncounter ? Current : Emotion.Calm, Stage, inEncounter && tears, _person.DisplayName);
            // 표정 그림을 보여 주다 애니메이터를 다시 켠 때에는, 경계 상태가 그대로여도 애니메이션을 처음부터 다시 재생하게 한다.
            if (!wasAnimating && _portrait.Animating) _waryStance.SetWary(false);
            _waryStance.SetWary(inEncounter && Current == Emotion.Guard);
        }

        /// <summary>
        /// 감정과 단계를 바꾼다. 같은 값이면 아무것도 하지 않는다.
        /// emotion과 stage를 사용하며, 상대의 감정과 단계를 바꾸고 바뀌었으면 Changed를 부른 뒤 true를 반환한다.
        /// </summary>
        private bool Set(Emotion emotion, int stage)
        {
            if (_person.Emotion == emotion && _person.EmotionStage == stage) return false;
            // 다른 감정으로 바뀌면 희망에서 맞은 횟수를 처음부터 다시 센다.
            if (_person.Emotion != emotion) _hopeHits = 0;
            _person.Emotion = emotion;
            _person.EmotionStage = stage;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// 지금 상태에서 얻는 것과 잃는 것을 적은 문구를 고른다.
        /// 상대의 감정과 단계, _stateMessages를 사용하며, 문구를 반환한다.
        /// </summary>
        private string StateMessage()
        {
            int index = Current == Emotion.Calm ? 0 : ((int)Current - 1) * 2 + Stage;
            return index < _stateMessages.Length ? _stateMessages[index] : "";
        }

        /// <summary>
        /// 같은 글의 화제가 이미 목록에 있는지 찾는다.
        /// text와 _topics를 사용하며, 있으면 true를 반환한다.
        /// </summary>
        private bool HasTopic(string text)
        {
            foreach (Topic topic in _topics)
                if (topic.Text == text) return true;
            return false;
        }

        /// <summary>
        /// 월드 위치가 켜져 있는 쉼터 가운데 하나의 안인지 판단한다.
        /// gazeWorld와 RestSpot.Spots를 사용하며, 안이면 true를 반환한다.
        /// </summary>
        public static bool InRestSpot(Vector3 gazeWorld)
        {
            foreach (RestSpot spot in RestSpot.Spots)
                if (spot.Contains(gazeWorld)) return true;
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 스펙 046: 개발자 진입에서 화제가 없는 감정마다 개발용 화제를 넣어 미로 안에서 감정을 바꿔 볼 수 있게 한다.
        /// _topics를 사용하며, 빠진 감정의 화제를 _topics에 더한다.
        /// </summary>
        public void AddDebugTopics()
        {
            for (int i = 1; i < STAGE1_NAMES.Length; i++)
            {
                Emotion emotion = (Emotion)i;
                if (_topics.FindIndex(t => t.Emotion == emotion) >= 0) continue;
                _topics.Add(new Topic { Text = "개발용 " + STAGE1_NAMES[i], Emotion = emotion });
            }
        }
#endif
    }
}
