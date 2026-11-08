using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>대화 선택지 하나. 조건(파편, 단서)과 고른 결과(신뢰, 동공, 반응, 엔딩)를 가진다.</summary>
    [Serializable]
    public class DialogueOption
    {
        [Header("말")]
        [SerializeField, FormerlySerializedAs("text")] private string _text = "...";
        [SerializeField, FormerlySerializedAs("response"), TextArea(2, 4)] private string _response;

        [Header("조건")]
        [Tooltip("이 기억 조각을 모아야 고를 수 있다 (없으면 항상 가능)")]
        [SerializeField, FormerlySerializedAs("requires")] private MemoryFragment _requires;
        [Tooltip("필드에서 이 단서를 살펴봐야 고를 수 있다 (없으면 상관없음)")]
        [SerializeField, FormerlySerializedAs("requiresClue")] private FieldClue _requiresClue;

        [Header("결과")]
        [SerializeField, FormerlySerializedAs("trust")] private int _trust;
        [SerializeField, FormerlySerializedAs("dilation"), Range(-1f, 1f)] private float _dilation;
        [Tooltip("고르면 다음 대화로 넘어간다")]
        [SerializeField, FormerlySerializedAs("advance")] private bool _advance;
        [Tooltip("비워 두면 평소처럼. 엔딩 id(예: delete)를 넣으면 고르는 순간 그 엔딩으로 끝난다")]
        [SerializeField, FormerlySerializedAs("ending")] private string _ending;

        public string Text => _text;
        public string Response => _response;
        public MemoryFragment Requires => _requires;
        public FieldClue RequiresClue => _requiresClue;
        public int Trust => _trust;
        public float Dilation => _dilation;
        public bool Advance => _advance;
        public string Ending => _ending;
    }

    /// <summary>현실의 대화 한 장면. 대화 루트 아래 순서대로 진행한다. 복제(Ctrl+D)로 장면을 늘린다.</summary>
    public class DialogueNode : MonoBehaviour
    {
        [Header("장면")]
        [SerializeField, FormerlySerializedAs("situation"), TextArea(2, 5)] private string _situation;
        [SerializeField, FormerlySerializedAs("options")] private List<DialogueOption> _options = new List<DialogueOption>();

        public string Situation => _situation;
        public List<DialogueOption> Options => _options;

        void OnValidate() => EyeHooks.Repaint();
    }
}
