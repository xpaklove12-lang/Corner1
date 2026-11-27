using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸이 열릴 때 대상(보통 쪽 전체)을 흔들었다가 제자리에 두는 칸 연출.
    /// 흔들림 폭은 _amplitude에서 시작해 0까지 줄어든다.
    /// </summary>
    public class CuttoonShake : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private RectTransform _target;

        [Header("흔들림")]
        [SerializeField] private float _duration = 0.6f;
        [Tooltip("처음 흔들림의 최대 거리(픽셀)")]
        [SerializeField] private float _amplitude = 18f;

        private Vector2 _origin;

        protected override float Duration => _duration;

        void Awake()
        {
            _origin = _target.anchoredPosition;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
            _amplitude = Mathf.Max(0f, _amplitude);
        }

        /// <summary>
        /// 진행 정도 t가 커질수록 줄어드는 폭으로 _target을 무작위 위치에 옮긴다.
        /// t가 1이면 _target을 _origin에 둔다.
        /// </summary>
        protected override void Apply(float t)
        {
            if (t >= 1f)
            {
                _target.anchoredPosition = _origin;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * (_amplitude * (1f - t));
            _target.anchoredPosition = _origin + offset;
        }

        /// <summary>
        /// 흔들기 전 상태로 _target을 _origin에 둔다.
        /// </summary>
        protected override void ResetState()
        {
            _target.anchoredPosition = _origin;
        }
    }
}
