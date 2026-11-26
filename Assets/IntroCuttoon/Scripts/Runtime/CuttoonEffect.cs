using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸이 열릴 때 함께 시작하는 연출의 공통 바탕.
    /// _delay초 기다린 뒤 Duration초 동안 진행 정도 0~1을 Apply에 넘긴다.
    /// </summary>
    public abstract class CuttoonEffect : MonoBehaviour
    {
        [Header("시작")]
        [Tooltip("칸이 열린 뒤 연출을 시작하기까지의 시간(초)")]
        [SerializeField] private float _delay = 0f;

        private float _elapsed;
        private bool _playing;

        public bool IsPlaying => _playing;

        /// <summary>
        /// 연출 한 번에 걸리는 시간(초)을 돌려준다. _delay는 포함하지 않는다.
        /// </summary>
        protected abstract float Duration { get; }

        void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < _delay)
            {
                return;
            }

            float t = Duration > 0f ? Mathf.Clamp01((_elapsed - _delay) / Duration) : 1f;
            Apply(t);
            if (t >= 1f)
            {
                _playing = false;
            }
        }

        protected virtual void OnValidate()
        {
            _delay = Mathf.Max(0f, _delay);
        }

        /// <summary>
        /// 연출을 처음 상태로 맞추고 시작한다.
        /// _elapsed를 0으로, _playing을 true로 바꾼다.
        /// </summary>
        public void Play()
        {
            _elapsed = 0f;
            _playing = true;
            ResetState();
        }

        /// <summary>
        /// 진행 중인 연출을 끝 상태로 바로 맞춘다.
        /// 진행 중이 아니면 아무것도 바꾸지 않고, 진행 중이면 _playing을 false로 바꾼다.
        /// </summary>
        public void Finish()
        {
            if (!_playing)
            {
                return;
            }

            _playing = false;
            Apply(1f);
        }

        /// <summary>
        /// 연출을 멈추고 칸이 열리기 전 상태로 되돌린다.
        /// _playing을 false로 바꾸고 ResetState를 부른다.
        /// </summary>
        public void ResetEffect()
        {
            _playing = false;
            ResetState();
        }

        /// <summary>
        /// 진행 정도 t(0~1)에 맞게 대상의 상태를 바꾼다.
        /// t가 1이면 끝 상태가 된다.
        /// </summary>
        protected abstract void Apply(float t);

        /// <summary>
        /// 연출을 시작하기 전 상태로 대상을 맞춘다.
        /// 기본 동작은 Apply(0)이며, 기다리는 동안에도 이 상태가 유지된다.
        /// </summary>
        protected virtual void ResetState()
        {
            Apply(0f);
        }
    }
}
