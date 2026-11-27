using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 새 날개 두 개를 몸의 앞뒤 축으로 위아래로 흔드는 칸 연출.
    /// _duration초 동안 1초에 _frequency번 날갯짓하며, 두 날개를 서로 거울처럼 같은 각도만큼 돌린다.
    /// </summary>
    public class CuttoonFlap : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private Transform _leftWing;
        [SerializeField] private Transform _rightWing;

        [Header("시간")]
        [SerializeField] private float _duration = 1f;

        [Header("날갯짓")]
        [Tooltip("1초 동안의 날갯짓 횟수")]
        [SerializeField] private float _frequency = 5f;
        [Tooltip("날개가 위아래로 움직이는 최대 각도(도)")]
        [SerializeField] private float _amplitude = 35f;
        [Tooltip("날갯짓을 시작하는 자리(날갯짓 한 번을 1로 본 비율)")]
        [SerializeField] private float _phase = 0f;

        protected override float Duration => _duration;

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
            _frequency = Mathf.Max(0f, _frequency);
        }

        /// <summary>
        /// 진행 정도 t에 해당하는 시각의 날개 각도를 사인 곡선으로 구한다.
        /// 구한 각도를 _leftWing과 _rightWing의 로컬 Z 회전에 반대 부호로 넣어 두 날개가 함께 오르내리게 한다.
        /// </summary>
        protected override void Apply(float t)
        {
            float angle = _amplitude * Mathf.Sin((t * _duration * _frequency + _phase) * 2f * Mathf.PI);
            _leftWing.localRotation = Quaternion.Euler(0f, 0f, angle);
            _rightWing.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}
