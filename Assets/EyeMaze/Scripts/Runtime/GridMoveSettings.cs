using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>칸 단위 이동에서 방향키를 칸 방향으로 바꾸는 방식</summary>
    public enum GridControl
    {
        /// <summary>월드(화면) 좌표 기준. 누른 방향(위 = 화면 위쪽)과 가장 가까운 옆 칸으로 간다.</summary>
        Screen,
        /// <summary>원 기준. 위 = 바깥쪽, 아래 = 안쪽, 왼쪽 = 반시계, 오른쪽 = 시계로 언제나 같다.</summary>
        Ring
    }

    /// <summary>
    /// 시선의 칸 단위 이동 설정 묶음. 켜기, 방향 방식, 한 칸 시간, 벽에 부딪혔을 때의 튕김과 벽 빛남을 담는다.
    /// 디렉터가 자기 값을 갖고, 폭발 미로 같은 다른 곳이 통째로 넘겨 덮어쓸 수 있다.
    /// </summary>
    [Serializable]
    public class GridMoveSettings
    {
        [Header("켜기")]
        [Tooltip("켜면 시선이 칸 단위로 한 칸씩 움직인다. 동공 안(심연)에 들어서면 가운데 파편까지 한 번에 간다")]
        [SerializeField] private bool _enabled;
        [Tooltip("방향키를 칸 방향으로 바꾸는 방식. Ring = 원 기준(위 = 바깥쪽, 아래 = 안쪽), Screen = 월드(화면) 좌표 기준")]
        [SerializeField] private GridControl _control = GridControl.Screen;

        [Header("한 칸 이동")]
        [Tooltip("새로 눌러 한 칸 옮겨 가는 데 걸리는 시간(초). 빠르게 튀어 나가 살짝 지나쳤다가 되돌아와 앉는다")]
        [SerializeField, Min(0.01f)] private float _stepSeconds = 0.14f;
        [Tooltip("누르고 있어 이어서 갈 때 한 칸 시간(초)")]
        [SerializeField, Min(0.01f)] private float _chainStepSeconds = 0.12f;
        [Tooltip("방향키를 누르고 있을 때 이어서 움직이기 시작하기까지의 시간(초). 짧게 누르면 한 칸만 간다")]
        [SerializeField, Min(0f)] private float _repeatDelay = 0.2f;
        [Tooltip("화면 기준에서 누른 쪽 칸이 막혔을 때, 누른 방향과 이 값(내적) 이상으로 비슷한 다른 열린 칸으로 비껴 간다. 1이면 비껴 가지 않는다")]
        [SerializeField, Range(0f, 1f)] private float _slideMinDot = 0.5f;

        [Header("쫀득함")]
        [Tooltip("칸 한가운데를 지나쳤다 돌아오는 세기(ease-out-back 상수). 0이면 지나치지 않고, 1.7이면 약 10% 지나친다")]
        [SerializeField, Min(0f)] private float _overshoot = 1.5f;
        [Tooltip("걷는 동안 시선이 가는 쪽으로 늘어나는 정도 (0 = 늘어나지 않음)")]
        [SerializeField, Range(0f, 1f)] private float _stretch = 0.45f;
        [Tooltip("칸에 내려앉을 때 납작해지는 정도 (0 = 찌그러지지 않음)")]
        [SerializeField, Range(0f, 1f)] private float _landSquash = 0.3f;
        [Tooltip("내려앉아 납작해졌다가 원래 모양으로 돌아오는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _landSquashSeconds = 0.12f;

        [Header("벽에 부딪힘")]
        [Tooltip("막힌 쪽으로 살짝 밀렸다가 돌아오는 거리 (미로 기준). 칸 반 폭보다 작게 둔다")]
        [SerializeField, Min(0f)] private float _bumpDistance = 0.08f;
        [Tooltip("밀렸다가 돌아오는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _bumpSeconds = 0.16f;
        [Tooltip("막은 벽이 빛나는 색")]
        [SerializeField] private Color _wallGlowColor = new Color(1f, 0.45f, 0.45f, 1f);
        [Tooltip("막은 벽이 빛났다가 원래대로 돌아오는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _wallGlowSeconds = 0.4f;

        public bool Enabled => _enabled;
        public GridControl Control => _control;
        public float StepSeconds => _stepSeconds;
        public float ChainStepSeconds => _chainStepSeconds;
        public float RepeatDelay => _repeatDelay;
        public float Overshoot => _overshoot;
        public float Stretch => _stretch;
        public float LandSquash => _landSquash;
        public float LandSquashSeconds => _landSquashSeconds;
        public float SlideMinDot => _slideMinDot;
        public float BumpDistance => _bumpDistance;
        public float BumpSeconds => _bumpSeconds;
        public Color WallGlowColor => _wallGlowColor;
        public float WallGlowSeconds => _wallGlowSeconds;

        public GridMoveSettings() { }

        /// <summary>
        /// 켜기와 방향 방식만 정해 만든다. 나머지는 기본값을 쓴다.
        /// enabled, control을 사용하며, 새 설정 묶음을 만든다.
        /// </summary>
        public GridMoveSettings(bool enabled, GridControl control)
        {
            _enabled = enabled;
            _control = control;
        }
    }
}
