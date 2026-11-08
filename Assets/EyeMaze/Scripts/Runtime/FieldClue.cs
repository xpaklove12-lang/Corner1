using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>필드의 단서. 살펴보면 examined가 켜지고, 이 단서가 필요한 대화 선택지가 열린다.</summary>
    [ExecuteAlways]
    public class FieldClue : FieldInteractable
    {
        [Header("단서")]
        [SerializeField, FormerlySerializedAs("text"), TextArea(2, 5)] private string _text;
        [SerializeField, FormerlySerializedAs("examined")] private bool _examined;
        [SerializeField, FormerlySerializedAs("sparkle")] private LineRenderer _sparkle;

        [NonSerialized] private int _drawnKey = int.MinValue;

        public bool Examined { get => _examined; set => _examined = value; }
        public override string Verb => "살펴보기";

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
        /// flow를 사용하며, _examined를 켜고 필드 글 상자를 연다.
        /// </summary>
        public override void Interact(GameFlow flow)
        {
            _examined = true;
            flow.ShowText($"{DisplayName}\n{_text}");
        }
    }
}
