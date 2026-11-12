using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 상담 한 건(손님 한 명)의 데이터. 그 사람에게 말을 걸어 눈 속으로 들어갈 때 디렉터가 이 데이터를 불러온다.
    /// 인물·회차 CSV(Data/Dialogue/인물_DayN.csv) 하나에서 상담 대화와 엔딩·문구를 읽고, 기억 파편 묶음과 결말 조건을 가진다.
    /// 상담 순서는 order 값으로 정한다.
    /// </summary>
    public class ClientCase : MonoBehaviour
    {
        [Header("손님")]
        [Tooltip("상담 순서 (작을수록 먼저)")]
        [SerializeField, FormerlySerializedAs("order")] private int _order;
        [SerializeField, FormerlySerializedAs("personName")] private string _personName = "은재";
        [Tooltip("엔딩/심연 문구에서 쓰는 3인칭 (그녀 / 그)")]
        [SerializeField, FormerlySerializedAs("pronoun")] private string _pronoun = "그녀";
        [Tooltip("필드에 앉아 있는 이 사람")]
        [SerializeField, FormerlySerializedAs("npc")] private FieldNPC _npc;

        [Header("눈 속")]
        [Tooltip("이 사람의 인물·회차 CSV (UTF-8, Data/Dialogue/인물_DayN.csv). 상담 대화와 엔딩·문구를 함께 읽는다")]
        [SerializeField] private TextAsset _dialogueCsv;
        [Tooltip("이 케이스의 기억 파편 묶음들 (층마다 하나)")]
        [SerializeField, FormerlySerializedAs("fragmentGroups")] private List<GameObject> _fragmentGroups = new List<GameObject>();
        [Tooltip("미로 모양 (seed)")]
        [SerializeField, FormerlySerializedAs("mazeSeed")] private int _mazeSeed = 12345;
        // 스펙 015: 이 인물의 미로 사물 묶음 추가
        [Tooltip("이 인물의 미로 사물 묶음들 (층마다 하나, 해당 MazeLayer 아래)")]
        [SerializeField] private List<GameObject> _propGroups = new List<GameObject>();
        // 스펙 018: 층 문을 여는 플래그 목록 추가
        [Tooltip("층 문을 여는 플래그. 0번이 1층으로 들어가는 문이다. 비우면 그 문은 신뢰로 열린다")]
        [SerializeField] private string[] _gateFlags = new string[0];

        // 스펙 015: 시작 감정과 감정별 눈 표현 추가
        [Header("감정")]
        [Tooltip("상담 시작 때의 감정")]
        [SerializeField] private ClientEmotion _startEmotion = ClientEmotion.Calm;
        [Tooltip("이 인물의 감정별 눈 표현. 없는 감정은 기본값(변화 없음)으로 그린다")]
        [SerializeField] private EmotionLook[] _emotionLooks = new EmotionLook[0];

        [Header("문구")]
        [SerializeField, FormerlySerializedAs("introMessage"), TextArea(2, 4)] private string _introMessage;
        [SerializeField, FormerlySerializedAs("idleSituation"), TextArea(2, 4)] private string _idleSituation;
        [SerializeField, FormerlySerializedAs("abyssMessage"), TextArea(1, 3)] private string _abyssMessage;
        [Tooltip("이 사람이 들어와 앉은 뒤 하는 첫마디 (없으면 생략)")]
        [SerializeField, FormerlySerializedAs("entryLine"), TextArea(1, 3)] private string _entryLine;

        [Header("결말")]
        [SerializeField, FormerlySerializedAs("endings")] private List<EndingDef> _endings = new List<EndingDef>();

        public int Order => _order;
        public string PersonName => _personName;
        public string Pronoun => _pronoun;
        public FieldNPC Npc => _npc;
        public TextAsset DialogueCsv => _dialogueCsv;
        public DialogueScript Script => DialogueScript.Get(_dialogueCsv);

        /// <summary>회차 CSV의 엔딩과 문구. CSV에 없으면 아래 인스펙터 값을 쓴다.</summary>
        public EndingScript EndingData => EndingScript.Get(_dialogueCsv);
        public List<GameObject> FragmentGroups => _fragmentGroups;
        public int MazeSeed => _mazeSeed;
        // 스펙 015: 사물 묶음과 시작 감정 공개
        public List<GameObject> PropGroups => _propGroups;
        // 스펙 018: 문 플래그 목록 공개
        public string[] GateFlags => _gateFlags;
        public ClientEmotion StartEmotion => _startEmotion;
        public string IntroMessage => EndingData?.Text(EndingScript.Intro) ?? _introMessage;
        public string IdleSituation => EndingData?.Text(EndingScript.Idle) ?? _idleSituation;
        public string AbyssMessage => EndingData?.Text(EndingScript.Abyss) ?? _abyssMessage;
        public string EntryLine => _entryLine;
        /// <summary>결말 목록. 엔딩 CSV가 있으면 그걸, 없으면 인스펙터의 목록을 쓴다.</summary>
        public List<EndingDef> Endings
        {
            get
            {
                EndingScript e = EndingData;
                return e != null && e.Endings.Count > 0 ? e.Endings : _endings;
            }
        }

        /// <summary>truth 판정 신뢰 기준. 회차 CSV truth 행의 truth_trust이며 없으면 null이다.</summary>
        public int? TruthTrust => EndingData?.TruthTrust;
        /// <summary>truth 판정 열쇠 파편 Flag ID. 회차 CSV truth 행의 key_flag이며 비면 파편 조건이 없다.</summary>
        public string KeyFlag => EndingData?.KeyFlag ?? "";
        /// <summary>leave 판정 신뢰 기준. 회차 CSV leave 행의 leave_trust이며 없으면 null이다.</summary>
        public int? LeaveTrust => EndingData?.LeaveTrust;
        /// <summary>leave 판정 부담 기준(0~1). 회차 CSV leave 행의 burden_limit이며 없으면 null이다.</summary>
        public float? BurdenLimit => EndingData?.BurdenLimit;

        /// <summary>
        /// 이 케이스의 파편 묶음 안에 있는 기억 파편을 모두 모은다 (꺼진 것 포함).
        /// 입력값은 없으며, 파편 목록을 반환한다.
        /// </summary>
        public List<MemoryFragment> Fragments()
        {
            var list = new List<MemoryFragment>();
            foreach (GameObject g in _fragmentGroups)
                if (g != null) list.AddRange(g.GetComponentsInChildren<MemoryFragment>(true));
            return list;
        }

        /// <summary>
        /// 스펙 015: 이 케이스의 사물 묶음 안에 있는 미로 사물을 모두 모은다 (꺼진 것 포함).
        /// 입력값은 없으며, 사물 목록을 반환한다.
        /// </summary>
        public List<MazeProp> Props()
        {
            var list = new List<MazeProp>();
            foreach (GameObject g in _propGroups)
                if (g != null) list.AddRange(g.GetComponentsInChildren<MazeProp>(true));
            return list;
        }

        /// <summary>
        /// 스펙 015: 감정의 눈 표현을 찾는다.
        /// emotion과 _emotionLooks를 사용하며, 처음 맞는 표현 또는 (없으면) null을 반환한다.
        /// </summary>
        public EmotionLook LookOf(ClientEmotion emotion)
        {
            foreach (EmotionLook l in _emotionLooks)
                if (l != null && l.Emotion == emotion) return l;
            return null;
        }

        /// <summary>
        /// key_flag와 Flag가 같은 이 케이스의 파편을 찾는다. 하나뿐일 때만 그 파편을 돌려준다.
        /// 입력값은 없으며, 파편(조건이 없거나 못 찾으면 null)을 반환하고 못 찾은 이유를 error에 쓴다.
        /// </summary>
        public MemoryFragment FindKeyFragment(out string error)
        {
            error = "";
            string flag = KeyFlag;
            if (flag.Length == 0) return null;
            MemoryFragment found = null;
            int count = 0;
            foreach (MemoryFragment f in Fragments())
                if (f.Flag == flag) { found = f; count++; }
            if (count == 1) return found;
            error = count == 0 ? $"열쇠 파편 '{flag}'가 '{_personName}' 케이스의 파편에 없다." : $"열쇠 파편 '{flag}'가 '{_personName}' 케이스에 {count}개 있다.";
            return null;
        }
    }
}
