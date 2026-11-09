using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 내담자가 쉬고 싶어 한다는 신호를 눈으로 보여 준다. 동공이 먼저 떨리고, 눈물이 차오르며 초점이 흐려진다.
    /// </summary>
    [Serializable]
    public class ClientTearCue
    {
        private const float MIN_SECONDS = 0.01f;
        private const float TREMBLE_RAMP = 4f;

        [Header("눈물 신호")]
        [Tooltip("신호가 끝까지 차오르는 시간(초)")]
        [SerializeField] private float _tearRiseSeconds = 3f;
        [Tooltip("신호가 가라앉는 시간(초)")]
        [SerializeField] private float _tearFallSeconds = 1.5f;
        [Tooltip("눈물이 가득 찼을 때의 초점")]
        [SerializeField, Range(0f, 1f)] private float _tearBlurFocus = 0.35f;

        [NonSerialized] private float _amount;
        [NonSerialized] private float _mazeBaseFocus;
        [NonSerialized] private float _eyeBaseFocus;

        /// <summary>
        /// 신호를 한 프레임 진행한다. 신호가 시작될 때 씬에 정해진 초점을 기억해 둔다.
        /// dt, active(신호를 보여 줄지), maze, eye, pupil을 사용하며, _amount와 동공, 눈물, 초점 값을 바꾼다.
        /// </summary>
        public void Tick(float dt, bool active, IrisMaze maze, EyeVisual eye, Pupil pupil)
        {
            if (_amount <= 0f && !active) return;
            if (_amount <= 0f)
            {
                _mazeBaseFocus = maze.Focus;
                _eyeBaseFocus = eye.Focus;
            }
            float seconds = Mathf.Max(MIN_SECONDS, active ? _tearRiseSeconds : _tearFallSeconds);
            _amount = Mathf.MoveTowards(_amount, active ? 1f : 0f, dt / seconds);
            Apply(maze, eye, pupil);
        }

        /// <summary>
        /// 신호를 바로 없애고 값을 원래대로 되돌린다.
        /// maze, eye, pupil을 사용하며, 신호가 없으면 아무 일도 하지 않는다. _amount와 동공, 눈물, 초점 값을 바꾼다.
        /// </summary>
        public void Clear(IrisMaze maze, EyeVisual eye, Pupil pupil)
        {
            if (_amount <= 0f) return;
            _amount = 0f;
            Apply(maze, eye, pupil);
        }

        /// <summary>
        /// 지금 진행도를 동공 떨림, 눈물 수위, 초점에 반영한다.
        /// _amount와 기억해 둔 초점을 사용하며, pupil.Tremble, eye.TearLevel, eye.Focus, maze.Focus를 바꾼다.
        /// </summary>
        private void Apply(IrisMaze maze, EyeVisual eye, Pupil pupil)
        {
            float water = Mathf.SmoothStep(0f, 1f, _amount);
            pupil.Tremble = Mathf.Clamp01(_amount * TREMBLE_RAMP);
            eye.TearLevel = water;
            eye.Focus = Mathf.Lerp(_eyeBaseFocus, _tearBlurFocus, water);
            maze.Focus = Mathf.Lerp(_mazeBaseFocus, _tearBlurFocus, water);
        }
    }
}
