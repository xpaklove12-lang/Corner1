using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 3D 세트 안 Renderer의 색(_BaseColor, 투명도 포함)을 시작 색에서 끝 색으로 바꾸는 칸 연출.
    /// 재질 애셋은 그대로 두고 MaterialPropertyBlock으로 대상 Renderer의 색만 바꾼다.
    /// </summary>
    public class CuttoonColorTween : CuttoonEffect
    {
        private static readonly int _baseColorId = Shader.PropertyToID("_BaseColor");

        [Header("대상")]
        [SerializeField] private Renderer _target;

        [Header("시간")]
        [SerializeField] private float _duration = 1f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("색")]
        [SerializeField] private Color _fromColor = Color.white;
        [SerializeField] private Color _toColor = Color.white;

        private MaterialPropertyBlock _block;

        protected override float Duration => _duration;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
        }

        /// <summary>
        /// 진행 정도 t를 _curve로 바꾼 값만큼 _fromColor와 _toColor를 섞어 _target의 색으로 넣는다.
        /// _target에 걸린 MaterialPropertyBlock에서 _BaseColor만 바꾸고 다른 값은 그대로 둔다.
        /// </summary>
        protected override void Apply(float t)
        {
            Color color = Color.LerpUnclamped(_fromColor, _toColor, _curve.Evaluate(t));
            _target.GetPropertyBlock(_block);
            _block.SetColor(_baseColorId, color);
            _target.SetPropertyBlock(_block);
        }
    }
}
