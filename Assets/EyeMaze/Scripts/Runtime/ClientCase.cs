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

        [Header("문구")]
        [SerializeField, FormerlySerializedAs("introMessage"), TextArea(2, 4)] private string _introMessage;
        [SerializeField, FormerlySerializedAs("idleSituation"), TextArea(2, 4)] private string _idleSituation;
        [SerializeField, FormerlySerializedAs("abyssMessage"), TextArea(1, 3)] private string _abyssMessage;
        [Tooltip("이 사람이 들어와 앉은 뒤 하는 첫마디 (없으면 생략)")]
        [SerializeField, FormerlySerializedAs("entryLine"), TextArea(1, 3)] private string _entryLine;

        [Header("결말")]
        [SerializeField, FormerlySerializedAs("endings")] private List<EndingDef> _endings = new List<EndingDef>();
        [SerializeField, FormerlySerializedAs("truthTrust")] private int _truthTrust = 3;
        [SerializeField, FormerlySerializedAs("keyFragment")] private MemoryFragment _keyFragment;
        [SerializeField, FormerlySerializedAs("leaveTrust")] private int _leaveTrust = -2;

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
        public int TruthTrust => _truthTrust;
        public MemoryFragment KeyFragment => _keyFragment;
        public int LeaveTrust => _leaveTrust;
    }
}
