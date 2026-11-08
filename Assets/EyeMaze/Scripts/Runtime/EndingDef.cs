using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 한 손님의 결말 하나. 케이스의 결말 목록에서 id로 찾는다.
    /// 대사(lines)를 한 줄씩 넘기고, 마지막에 "— 엔딩 · 제목 —"이 뜬 뒤 필드로 돌아간다.
    /// </summary>
    [Serializable]
    public class EndingDef
    {
        [Header("결말")]
        [Tooltip("선택지에서 부르는 이름 (truth, half, leave, delete …)")]
        [SerializeField, FormerlySerializedAs("id")] private string _id = "ending";
        [SerializeField, FormerlySerializedAs("title")] private string _title = "엔딩";
        [Tooltip("엔딩 동안 상황 칸에 뜨는 문장")]
        [SerializeField, FormerlySerializedAs("situation"), TextArea(1, 2)] private string _situation;
        [SerializeField, FormerlySerializedAs("lines"), TextArea(1, 3)] private List<string> _lines = new List<string>();
        [Tooltip("필드로 돌아온 뒤 이 사람에게 말을 걸면 나오는 말")]
        [SerializeField, FormerlySerializedAs("afterLine"), TextArea(1, 3)] private string _afterLine;

        public string Id => _id;
        public string Title => _title;
        public string Situation => _situation;
        public List<string> Lines => _lines;
        public string AfterLine => _afterLine;
    }
}
