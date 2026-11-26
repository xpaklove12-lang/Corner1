using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸이 열린 뒤 여러 모습(예: 뜬 눈, 반쯤 감은 눈, 감은 눈)을 차례로 하나씩만 보이는 칸 연출.
    /// 한 단계는 _stepInterval초 동안 보인다.
    /// </summary>
    public class CuttoonSteps : CuttoonEffect
    {
        [Header("단계")]
        [Tooltip("차례로 보일 모습. 첫 번째가 칸이 열릴 때의 모습이다")]
        [SerializeField] private GameObject[] _steps;
        [Tooltip("모습이 한 단계 바뀌는 간격(초)")]
        [SerializeField] private float _stepInterval = 0.9f;

        protected override float Duration => _stepInterval * (_steps.Length - 1);

        protected override void OnValidate()
        {
            base.OnValidate();
            _stepInterval = Mathf.Max(0f, _stepInterval);
        }

        /// <summary>
        /// 진행 정도 t에 해당하는 단계 하나만 보이게 한다.
        /// t가 1이면 마지막 단계가 보인다.
        /// </summary>
        protected override void Apply(float t)
        {
            int index = Mathf.Min(_steps.Length - 1, Mathf.FloorToInt(t * (_steps.Length - 1) + 0.0001f));
            ShowStep(index);
        }

        /// <summary>
        /// 시작 전 상태로 첫 단계만 보이게 한다.
        /// </summary>
        protected override void ResetState()
        {
            ShowStep(0);
        }

        /// <summary>
        /// _steps 가운데 index번 모습만 켜고 나머지는 끈다.
        /// </summary>
        private void ShowStep(int index)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                _steps[i].SetActive(i == index);
            }
        }
    }
}
