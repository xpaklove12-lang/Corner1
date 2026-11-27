using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 빨리감기 중 오른쪽에서 왼쪽으로 지나가는 가로줄과 화면 가운데의 ▶▶ 표시를 보인다.
    /// 배속과 상관없이 실제 시간으로 페이드와 ▶▶의 밝기 변화를 처리하고, 가로줄 셰이더에 실제 시간을 넘긴다.
    /// </summary>
    public class FastForwardOverlay : MonoBehaviour
    {
        private static readonly int _timeId = Shader.PropertyToID("_FastForwardTime");

        [Header("연결")]
        [Tooltip("오버레이 전체를 나타내고 숨기는 CanvasGroup")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("화면 가운데 ▶▶ 표시")]
        [SerializeField] private Text _mark;

        [Header("연출")]
        [Tooltip("나타나고 사라지는 시간 (초, 실제 시간)")]
        [SerializeField] private float _fadeSeconds = 0.15f;
        [Tooltip("▶▶가 흐려졌다 다시 밝아지는 한 주기 (초, 실제 시간)")]
        [SerializeField] private float _markPulseSeconds = 1.2f;
        [Tooltip("▶▶가 가장 흐려졌을 때의 불투명도")]
        [SerializeField, Range(0f, 1f)] private float _markMinAlpha = 0.25f;

        void OnValidate()
        {
            _fadeSeconds = Mathf.Max(0.01f, _fadeSeconds);
            _markPulseSeconds = Mathf.Max(0.05f, _markPulseSeconds);
        }

        void Update()
        {
            float target = CutsceneFastForward.IsFastForwarding ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / _fadeSeconds);
            if (_group.alpha <= 0f) return;

            // 셰이더는 배속의 영향을 받지 않도록 실제 시간으로 줄 배치를 바꾼다.
            Shader.SetGlobalFloat(_timeId, Time.unscaledTime);
            // ▶▶는 켜고 끄지 않고 불투명도를 사인 곡선으로 부드럽게 오르내린다.
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime / _markPulseSeconds * 2f * Mathf.PI);
            _mark.canvasRenderer.SetAlpha(Mathf.Lerp(_markMinAlpha, 1f, wave));
        }
    }
}
