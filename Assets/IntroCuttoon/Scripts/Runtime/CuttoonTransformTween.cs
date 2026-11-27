using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 3D 세트 안 Transform의 로컬 위치, 로컬 회전, 로컬 크기를 시작 값에서 끝 값으로 바꾸는 칸 연출.
    /// 켜 둔 항목만 바꾸고, 꺼 둔 항목은 씬에 저장된 값 그대로 둔다.
    /// </summary>
    public class CuttoonTransformTween : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private Transform _target;

        [Header("시간")]
        [SerializeField] private float _duration = 1f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("위치")]
        [SerializeField] private bool _animatePosition = false;
        [SerializeField] private Vector3 _fromPosition = Vector3.zero;
        [SerializeField] private Vector3 _toPosition = Vector3.zero;

        [Header("회전")]
        [SerializeField] private bool _animateRotation = false;
        [Tooltip("시작 로컬 회전(오일러 각, 도)")]
        [SerializeField] private Vector3 _fromRotation = Vector3.zero;
        [Tooltip("끝 로컬 회전(오일러 각, 도)")]
        [SerializeField] private Vector3 _toRotation = Vector3.zero;

        // CUT-20: 2-2 운석과 구름의 크기를 바꾸려고 로컬 크기 항목을 더했다.
        [Header("크기")]
        [SerializeField] private bool _animateScale = false;
        [SerializeField] private Vector3 _fromScale = Vector3.one;
        [SerializeField] private Vector3 _toScale = Vector3.one;

        protected override float Duration => _duration;

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
        }

        /// <summary>
        /// 진행 정도 t를 _curve로 바꾼 값만큼 시작 값과 끝 값 사이를 섞어 _target에 넣는다.
        /// 켜 둔 항목에 따라 _target의 localPosition, localRotation, localScale을 바꾼다.
        /// </summary>
        protected override void Apply(float t)
        {
            float eased = _curve.Evaluate(t);

            if (_animatePosition)
            {
                _target.localPosition = Vector3.LerpUnclamped(_fromPosition, _toPosition, eased);
            }

            if (_animateRotation)
            {
                _target.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(_fromRotation, _toRotation, eased));
            }

            if (_animateScale)
            {
                _target.localScale = Vector3.LerpUnclamped(_fromScale, _toScale, eased);
            }
        }
    }
}
