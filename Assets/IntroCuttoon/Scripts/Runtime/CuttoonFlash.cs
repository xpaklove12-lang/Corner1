using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸이 열릴 때 화면 전체를 흰색으로 덮었다가 걷어 내는 칸 연출.
    /// 흰 화면은 씬에 둔 _overlay의 투명도로 조절한다.
    /// </summary>
    public class CuttoonFlash : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private CanvasGroup _overlay;

        [Header("번쩍임")]
        [SerializeField] private float _duration = 0.9f;
        [Tooltip("번쩍이는 순간 흰 화면의 투명도")]
        [SerializeField] private float _peakAlpha = 1f;

        protected override float Duration => _duration;

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
            _peakAlpha = Mathf.Clamp01(_peakAlpha);
        }

        /// <summary>
        /// 진행 정도 t가 커질수록 _overlay의 투명도를 _peakAlpha에서 0으로 낮춘다.
        /// </summary>
        protected override void Apply(float t)
        {
            _overlay.alpha = _peakAlpha * (1f - t);
        }

        /// <summary>
        /// 번쩍이기 전 상태로 _overlay의 투명도를 0으로 둔다.
        /// </summary>
        protected override void ResetState()
        {
            _overlay.alpha = 0f;
        }
    }
}
