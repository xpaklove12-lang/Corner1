using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 상담 한 건(손님 한 명)의 데이터. 그 사람에게 말을 걸어 눈 속으로 들어갈 때 디렉터가 이 데이터를 불러온다.
    /// 대화 장면(dialogue 아래 DialogueNode들), 기억 파편 묶음, 결말과 문구를 가진다.
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
        [Tooltip("대화 장면들의 부모")]
        [SerializeField, FormerlySerializedAs("dialogue")] private Transform _dialogue;
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
        public Transform Dialogue => _dialogue;
        public List<GameObject> FragmentGroups => _fragmentGroups;
        public int MazeSeed => _mazeSeed;
        public string IntroMessage => _introMessage;
        public string IdleSituation => _idleSituation;
        public string AbyssMessage => _abyssMessage;
        public string EntryLine => _entryLine;
        public List<EndingDef> Endings => _endings;
        public int TruthTrust => _truthTrust;
        public MemoryFragment KeyFragment => _keyFragment;
        public int LeaveTrust => _leaveTrust;
    }
}
