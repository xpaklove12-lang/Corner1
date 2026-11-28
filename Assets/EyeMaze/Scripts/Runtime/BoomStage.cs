using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로 난이도 단계 하나. 무늬가 도는 빠르기와, 무늬가 고른 칸 중 실제로 터지는 칸의 비율을 담는다.
    /// 문을 하나 열 때마다 다음 단계로 오른다.
    /// </summary>
    [Serializable]
    public class BoomStage
    {
        [Header("난이도")]
        [Tooltip("무늬가 도는 빠르기 배율. 낮을수록 경고가 길고 터지는 간격이 넓다. 바퀴마다 빨라지는 배속에 곱한다")]
        [SerializeField, Min(0.1f)] private float _speed = 1f;
        [Tooltip("무늬가 고른 칸 중 실제로 터지는 칸의 비율 (0~1). 낮을수록 피할 칸이 많다")]
        [SerializeField, Range(0f, 1f)] private float _cellRatio = 1f;

        public float Speed => Mathf.Max(0.1f, _speed);
        public float CellRatio => Mathf.Clamp01(_cellRatio);

        public BoomStage() { }

        /// <summary>
        /// 기본 단계 목록을 코드로 짤 때 쓰는 생성자.
        /// speed, cellRatio를 사용하며, 새 단계를 만든다.
        /// </summary>
        public BoomStage(float speed, float cellRatio)
        {
            _speed = speed;
            _cellRatio = cellRatio;
        }
    }
}
