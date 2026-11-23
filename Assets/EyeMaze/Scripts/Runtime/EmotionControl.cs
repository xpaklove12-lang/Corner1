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

        [Header("수치")]
        [Tooltip("쉼터에서 한 단계 내려가는 시간(초)")]
        [SerializeField, Min(0.1f)] private float _restStepSeconds = 2.5f;
        [Tooltip("평온일 때 부담감 누적 속도 배율")]
        [SerializeField, Min(0f)] private float _calmBurdenScale = 0.6f;
        [Tooltip("2단계일 때 부담감 누적 속도 배율")]
        [SerializeField, Min(0f)] private float _stage2BurdenScale = 2f;
        [Tooltip("슬픔과 침잠일 때 시선 속도 배율")]
        [SerializeField, Range(0.05f, 1f)] private float _sadGazeScale = 0.55f;

        [Header("문구")]
        [Tooltip("2단계에서 같은 감정의 화제를 또 짚었을 때")]
        [SerializeField, TextArea(1, 2)] private string _alreadyMaxMessage = "더는 밀어붙일 수 없다.";
        [Tooltip("반감이 되었는데 그 층에 드러낼 숨긴 파편이 없을 때")]
        [SerializeField, TextArea(1, 2)] private string _nothingHiddenMessage = "이 층에는 숨긴 것이 없다.";
        [Tooltip("감정이 바뀔 때 나오는 얻는 것과 잃는 것. 순서: 평온, 경계, 반감, 동요, 격동, 슬픔, 침잠, 희망, 열망")]
        [SerializeField, TextArea(1, 3)] private string[] _stateMessages =
        {
            "마음이 가라앉았다. 장애물이 느려지고 부담이 천천히 쌓인다. 숨긴 것은 보이지 않는다.",
            "은재가 경계한다. 장애물이 숨긴 기억 쪽으로 모인다. 그쪽에 무언가 있다.",
            "반감이 솟는다. 숨긴 기억이 드러나지만 장애물이 좁혀 지킨다. 부담이 빨리 쌓인다.",
            "마음이 흔들린다. 장애물이 빨라져 서로 부딪히고, 기억 가까이에서 말이 새어 나온다.",
            "마음이 요동친다. 장애물이 연달아 부딪혀 틈이 길어지고 말이 더 이어진다. 부담이 빨리 쌓인다.",
            "슬픔에 잠긴다. 장애물을 지나갈 수 있지만 시선이 느려진다. 지나온 길이 남는다.",
            "깊이 가라앉는다. 장애물 속에서는 느려지지 않고 흔적이 멀리 남는다. 부담이 빨리 쌓인다.",
            "희망이 비친다. 가까운 장애물이 비켜 주고, 열쇠가 맞는 문이 빛난다.",
            "간절해진다. 드러난 기억이 이쪽으로 끌려온다. 부담이 빨리 쌓인다.",
        };

        private readonly List<Topic> _topics = new List<Topic>();
        private float _restT;

        /// <summary>화제 하나. 대화에서 얻고 미로 안에서 짚는다.</summary>
        public struct Topic
        {
            public string Text;
            public Emotion Emotion;
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
        /// 감정을 평온으로 되돌리고 화제 목록과 쉼터 시간을 비운다. 인물을 불러오거나 만남을 다시 시작할 때 부른다.
        /// 입력값은 없으며, 상대의 감정과 단계, _topics, _restT를 바꾼다.
        /// </summary>
        public void ResetState()
        {
            _topics.Clear();
            _restT = 0f;
            Set(Emotion.Calm, 0);
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
        /// 경계와 반감일 때만 초상화의 경계 애니메이션을 켠다. 만남을 떠나 있으면 끈다.
        /// inEncounter(만남 화면에 있는지)를 사용하며, WaryStance의 경계 상태를 바꾼다.
        /// </summary>
        public void SyncWary(bool inEncounter)
        {
            _waryStance.SetWary(inEncounter && Current == Emotion.Guard);
        }

        /// <summary>
        /// 감정과 단계를 바꾼다. 같은 값이면 아무것도 하지 않는다.
        /// emotion과 stage를 사용하며, 상대의 감정과 단계를 바꾸고 바뀌었으면 Changed를 부른 뒤 true를 반환한다.
        /// </summary>
        private bool Set(Emotion emotion, int stage)
        {
            if (_person.Emotion == emotion && _person.EmotionStage == stage) return false;
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
        private static bool InRestSpot(Vector3 gazeWorld)
        {
            foreach (RestSpot spot in RestSpot.Spots)
                if (spot.Contains(gazeWorld)) return true;
            return false;
        }
    }
}
