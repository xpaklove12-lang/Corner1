using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>필드의 단서. 살펴보면 examined와 플래그가 켜지고, 그 플래그가 필요한 대화 선택지가 열린다.</summary>
    [ExecuteAlways]
    public class FieldClue : FieldInteractable
    {
        [Header("단서")]
        [SerializeField, FormerlySerializedAs("text"), TextArea(2, 5)] private string _text;
        [SerializeField, FormerlySerializedAs("examined")] private bool _examined;
        [Tooltip("살펴보면 켜지는 플래그 id (Data/Flags.csv에 적힌 것). 대화 CSV의 need 칸에서 쓴다")]
        [SerializeField] private string _flag;
        [SerializeField, FormerlySerializedAs("sparkle")] private LineRenderer _sparkle;

        [NonSerialized] private int _drawnKey = int.MinValue;

        public string Flag => _flag;

        /// <summary>단서 이름. Flags.csv에서 이 단서 플래그 줄의 title 칸이 있으면 그걸 쓴다.</summary>
        public override string DisplayName => Flags.TitleOf(_flag) ?? base.DisplayName;
        /// <summary>살펴볼 때 나오는 글. Flags.csv의 text 칸이 있으면 그걸 쓴다.</summary>
        public string Text => Flags.TextOf(_flag) ?? _text;

        /// <summary>살펴봤는지. 바꾸면 플래그도 함께 켜지고 꺼진다.</summary>
        public bool Examined
        {
            get => _examined;
            set { _examined = value; Flags.Set(_flag, value); }
        }
        public override string Verb => Strings.Get("verb.examine", "살펴보기");

        void OnEnable() { _drawnKey = int.MinValue; }
        protected override void OnValidate() { _drawnKey = int.MinValue; base.OnValidate(); }

        void Update()
        {
            int key = HashCode.Combine(_examined, Radius);
            if (_sparkle == null || key == _drawnKey) return;
            _drawnKey = key;
            Shapes.SetBlob(_sparkle, 0.16f);
            Shapes.SetColor(_sparkle, _examined ? new Color(1f, 1f, 1f, 0.3f) : Color.white);
            _sparkle.sortingOrder = 2;
        }

        /// <summary>
        /// 단서를 살펴본 것으로 표시하고 내용을 글 상자에 띄운다.
        /// flow를 사용하며, Examined(와 플래그)를 켜고 필드 글 상자를 연다.
        /// </summary>
        public override void Interact(GameFlow flow)
        {
            Examined = true;
            flow.ShowText($"{DisplayName}\n{Text}");
        }
    }
}
