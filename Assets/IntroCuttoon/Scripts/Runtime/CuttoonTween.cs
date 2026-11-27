using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 대상 RectTransform의 위치, 크기, 투명도를 시작 값에서 끝 값으로 바꾸는 칸 연출.
    /// 켜 둔 항목만 바꾸고, 꺼 둔 항목은 씬에 저장된 값 그대로 둔다.
    /// </summary>
    public class CuttoonTween : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private RectTransform _target;
        [Tooltip("투명도를 바꿀 때만 쓴다")]
        [SerializeField] private CanvasGroup _targetGroup;

        [Header("시간")]
        [SerializeField] private float _duration = 1f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("위치")]
        [SerializeField] private bool _animatePosition = false;
        [SerializeField] private Vector2 _fromPosition = Vector2.zero;
        [SerializeField] private Vector2 _toPosition = Vector2.zero;

        [Header("크기")]
        [SerializeField] private bool _animateScale = false;
        [SerializeField] private Vector3 _fromScale = Vector3.one;
        [SerializeField] private Vector3 _toScale = Vector3.one;

        [Header("투명도")]
        [SerializeField] private bool _animateAlpha = false;
        [SerializeField] private float _fromAlpha = 0f;
        [SerializeField] private float _toAlpha = 1f;

        protected override float Duration => _duration;

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
            _fromAlpha = Mathf.Clamp01(_fromAlpha);
            _toAlpha = Mathf.Clamp01(_toAlpha);
        }

        /// <summary>
        /// 진행 정도 t를 _curve로 바꾼 값만큼 시작 값과 끝 값 사이를 섞어 대상에 넣는다.
        /// 켜 둔 항목에 따라 _target의 anchoredPosition, localScale, _targetGroup의 alpha를 바꾼다.
        /// </summary>
        protected override void Apply(float t)
        {
            float eased = _curve.Evaluate(t);

            if (_animatePosition)
            {
                _target.anchoredPosition = Vector2.LerpUnclamped(_fromPosition, _toPosition, eased);
            }

            if (_animateScale)
            {
                _target.localScale = Vector3.LerpUnclamped(_fromScale, _toScale, eased);
            }

            if (_animateAlpha)
            {
                _targetGroup.alpha = Mathf.Lerp(_fromAlpha, _toAlpha, eased);
            }
        }
    }
}
