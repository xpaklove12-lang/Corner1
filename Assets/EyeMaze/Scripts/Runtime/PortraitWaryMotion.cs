using System;

using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 038: 지정한 초상화가 보이는 동안 좌우로 흘끔거리고 움츠리며 떠는 경계 동작을 반복한다.
    /// </summary>
    public class PortraitWaryMotion : MonoBehaviour
    {
        private const int STEP_COUNT = 3;

        [Header("대상")]
        [Tooltip("움직일 초상화 Image")]
        [SerializeField] private Image _image;
        [Tooltip("켜면 아래 초상화가 보이는 동안 경계 동작을 반복한다")]
        [SerializeField] private bool _play = true;
        [Tooltip("경계 동작을 재생할 초상화 (예: 은재)")]
        [SerializeField] private Sprite[] _sprites = Array.Empty<Sprite>();

        [Header("흘끔거림")]
        [Tooltip("좌우로 흘끔거리는 폭(픽셀)")]
        [SerializeField, Min(0f)] private float _glanceDistance = 6f;
        [Tooltip("한쪽으로 옮기는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _glanceSeconds = 0.15f;
        [Tooltip("한 자리에 머무는 시간(초)")]
        [SerializeField, Min(0f)] private float _holdSeconds = 0.8f;

        [Header("움츠림")]
        [Tooltip("움츠렸을 때 세로 크기 비율")]
        [SerializeField, Range(0.5f, 1f)] private float _shrinkScale = 0.97f;
        [Tooltip("움츠렸다 펴는 한 주기(초)")]
        [SerializeField, Min(0.01f)] private float _breathSeconds = 1.6f;

        [Header("떨림")]
        [Tooltip("떨림 폭(픽셀)")]
        [SerializeField, Min(0f)] private float _tremble = 0.6f;
        [Tooltip("떨림 빠르기")]
        [SerializeField, Min(0f)] private float _trembleSpeed = 18f;

        private RectTransform _rect;
        private Vector2 _basePos;
        private Vector3 _baseScale;
        private bool _active;
        private int _step;
        private float _stepT;
        private float _fromX;
        private float _time;

        void Awake()
        {
            // 씬에 저장된 초상화 위치와 크기를 기준점으로 삼는다.
            _rect = _image.rectTransform;
            _basePos = _rect.anchoredPosition;
            _baseScale = _rect.localScale;
        }

        void LateUpdate()
        {
            if (!ShouldPlay())
            {
                if (_active) ResetMotion();
                return;
            }
            _active = true;
            Animate(Time.unscaledDeltaTime);
        }

        void OnDisable()
        {
            if (_active) ResetMotion();
        }

        /// <summary>
        /// 경계 동작을 재생할 조건인지 확인한다.
        /// _play, _image의 표시 상태와 스프라이트, _sprites를 사용하며, 재생해야 하면 true를 반환한다.
        /// </summary>
        private bool ShouldPlay()
        {
            if (!_play || !_image.enabled || !_image.gameObject.activeInHierarchy || _image.sprite == null) return false;
            return Array.IndexOf(_sprites, _image.sprite) >= 0;
        }

        /// <summary>
        /// 흘끔거림, 움츠림, 떨림을 한 프레임 진행해 기준점 위에 적용한다.
        /// deltaTime과 움직임 설정값을 사용하며, _step, _stepT, _fromX, _time과 초상화 위치·크기를 바꾼다.
        /// </summary>
        private void Animate(float deltaTime)
        {
            _time += deltaTime;
            _stepT += deltaTime;
            if (_stepT >= _glanceSeconds + _holdSeconds)
            {
                _fromX = StepX(_step);
                _step = (_step + 1) % STEP_COUNT;
                _stepT = 0f;
            }
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_stepT / _glanceSeconds));
            float x = Mathf.Lerp(_fromX, StepX(_step), e);

            // 떨림은 Perlin 노이즈를 -1~1로 옮겨 아주 작게 더한다.
            float n = _time * _trembleSpeed;
            var shake = new Vector2(Mathf.PerlinNoise(n, 0.3f) * 2f - 1f, Mathf.PerlinNoise(0.7f, n) * 2f - 1f) * _tremble;
            _rect.anchoredPosition = _basePos + new Vector2(x, 0f) + shake;

            float breath = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * _time / _breathSeconds);
            float sy = Mathf.Lerp(1f, _shrinkScale, breath);
            _rect.localScale = new Vector3(_baseScale.x, _baseScale.y * sy, _baseScale.z);
        }

        /// <summary>
        /// 흘끔 순서 step의 가로 위치를 구한다.
        /// step과 _glanceDistance를 사용하며, 왼쪽·오른쪽·가운데 중 하나의 픽셀 위치를 반환한다.
        /// </summary>
        private float StepX(int step)
        {
            if (step == 0) return -_glanceDistance;
            if (step == 1) return _glanceDistance;
            return 0f;
        }

        /// <summary>
        /// 초상화를 기준 위치·크기로 되돌리고 동작 진행을 처음으로 돌린다.
        /// 입력값은 없으며, 초상화 위치·크기와 _active, _step, _stepT, _fromX, _time을 바꾼다.
        /// </summary>
        private void ResetMotion()
        {
            _rect.anchoredPosition = _basePos;
            _rect.localScale = _baseScale;
            _active = false;
            _step = 0;
            _stepT = 0f;
            _fromX = 0f;
            _time = 0f;
        }
    }
}
