using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 컷툰의 칸 하나. 열리면 서서히 나타나며 제자리로 밀려 들어오고, 딸린 칸 연출을 함께 시작한다.
    /// 칸의 제자리는 씬에 저장된 위치다.
    /// </summary>
    public class CuttoonPanel : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("칸이 열릴 때 함께 시작하는 연출")]
        [SerializeField] private CuttoonEffect[] _effects;

        [Header("나타나기")]
        [Tooltip("투명도 0에서 1로 오르는 시간(초)")]
        [SerializeField] private float _appearDuration = 0.45f;
        [Tooltip("나타나기 시작할 때 제자리에서 떨어진 거리(픽셀)")]
        [SerializeField] private Vector2 _slideOffset = new Vector2(0f, -24f);

        private RectTransform _rect;
        private Vector2 _restPosition;
        private float _elapsed;
        private bool _appearing;

        public bool IsBusy
        {
            get
            {
                if (_appearing)
                {
                    return true;
                }

                foreach (CuttoonEffect effect in _effects)
                {
                    if (effect.IsPlaying)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        void Awake()
        {
            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
        }

        void Update()
        {
            if (!_appearing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            float t = _appearDuration > 0f ? Mathf.Clamp01(_elapsed / _appearDuration) : 1f;
            ApplyAppear(t);
            if (t >= 1f)
            {
                _appearing = false;
            }
        }

        void OnValidate()
        {
            _appearDuration = Mathf.Max(0f, _appearDuration);
        }

        /// <summary>
        /// 칸을 열기 시작한다. 나타나기를 처음부터 진행하고 _effects를 모두 시작한다.
        /// _appearing을 true로 바꾼다.
        /// </summary>
        public void Open()
        {
            _elapsed = 0f;
            _appearing = true;
            ApplyAppear(0f);
            foreach (CuttoonEffect effect in _effects)
            {
                effect.Play();
            }
        }

        /// <summary>
        /// 진행 중인 나타나기와 연출을 모두 끝 상태로 바로 맞춘다.
        /// _appearing을 false로 바꾼다.
        /// </summary>
        public void FinishAll()
        {
            _appearing = false;
            ApplyAppear(1f);
            foreach (CuttoonEffect effect in _effects)
            {
                effect.Finish();
            }
        }

        /// <summary>
        /// 칸을 열리기 전 상태로 숨긴다. 투명도 0, 제자리에서 _slideOffset만큼 떨어진 위치로 두고 연출을 되돌린다.
        /// </summary>
        public void Hide()
        {
            _appearing = false;
            ApplyAppear(0f);
            foreach (CuttoonEffect effect in _effects)
            {
                effect.ResetEffect();
            }
        }

        /// <summary>
        /// 나타나기 진행 정도 t(0~1)에 맞게 _group의 투명도와 칸 위치를 바꾼다.
        /// 끝으로 갈수록 천천히 멈추도록 t를 부드럽게 바꿔 쓴다.
        /// </summary>
        private void ApplyAppear(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            _group.alpha = eased;
            _rect.anchoredPosition = _restPosition + _slideOffset * (1f - eased);
        }
    }
}
