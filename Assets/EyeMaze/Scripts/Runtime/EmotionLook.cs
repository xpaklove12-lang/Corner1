using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 한 감정의 눈 표현 값 묶음. 인물(ClientCase)마다 따로 두어 같은 감정이라도 눈이 다르게 변한다.
    /// 값이 없는 감정은 기본값(눈꺼풀 닫힘 0, 색조 없음, 떨림 0, 눈물 0, 시선 피함 0)으로 그린다.
    /// </summary>
    [Serializable]
    public class EmotionLook
    {
        private const float DEFAULT_TREMBLE_SPEED = 28f;

        [Header("감정")]
        [Tooltip("이 표현이 쓰이는 감정")]
        [SerializeField] private ClientEmotion _emotion = ClientEmotion.Calm;

        [Header("눈꺼풀과 홍채")]
        [Tooltip("눈꺼풀이 닫힌 정도 (0 = 뜸, 1 = 감김)")]
        [SerializeField, Range(0f, 1f)] private float _lidClosed;
        [Tooltip("홍채에 섞는 색. 알파가 섞는 세기다")]
        [SerializeField] private Color _irisTint = Color.clear;

        [Header("동공")]
        [Tooltip("동공이 떨리는 거리. 0이면 떨지 않는다")]
        [SerializeField] private float _trembleDistance;
        [Tooltip("동공이 떨리는 빠르기")]
        [SerializeField] private float _trembleSpeed = DEFAULT_TREMBLE_SPEED;
        [Tooltip("동공이 한쪽으로 쏠리는 거리와 방향 (시선 피함). 디렉터의 최대 거리를 넘으면 줄여 쓴다")]
        [SerializeField] private Vector2 _gazeShift;

        [Header("눈물")]
        [Tooltip("눈물 수위 (0 = 없음, 1 = 가득)")]
        [SerializeField, Range(0f, 1f)] private float _tearLevel;

        public ClientEmotion Emotion => _emotion;
        public float LidClosed => _lidClosed;
        public Color IrisTint => _irisTint;
        public float TrembleDistance => _trembleDistance;
        public float TrembleSpeed => _trembleSpeed;
        public float TearLevel => _tearLevel;
        public Vector2 GazeShift => _gazeShift;

        /// <summary>
        /// 다른 표현의 값을 그대로 옮겨 온다.
        /// other를 사용하며, 이 표현의 모든 값을 바꾼다.
        /// </summary>
        public void CopyFrom(EmotionLook other)
        {
            _emotion = other._emotion;
            _lidClosed = other._lidClosed;
            _irisTint = other._irisTint;
            _trembleDistance = other._trembleDistance;
            _trembleSpeed = other._trembleSpeed;
            _gazeShift = other._gazeShift;
            _tearLevel = other._tearLevel;
        }

        /// <summary>
        /// 두 표현 사이의 값을 계산해 이 표현에 쓴다. 색조는 한쪽이 투명하면 다른 쪽 색으로 세기만 바꾼다.
        /// from, to, t(0~1)를 사용하며, 이 표현의 모든 값을 바꾼다.
        /// </summary>
        public void Lerp(EmotionLook from, EmotionLook to, float t)
        {
            _emotion = t < 1f ? from._emotion : to._emotion;
            _lidClosed = Mathf.Lerp(from._lidClosed, to._lidClosed, t);
            Color a = from._irisTint;
            Color b = to._irisTint;
            // 투명한 쪽의 색 성분이 섞여 홍채가 어두워지지 않게 한다.
            if (a.a <= 0f) a = new Color(b.r, b.g, b.b, 0f);
            if (b.a <= 0f) b = new Color(a.r, a.g, a.b, 0f);
            _irisTint = Color.Lerp(a, b, t);
            _trembleDistance = Mathf.Lerp(from._trembleDistance, to._trembleDistance, t);
            _trembleSpeed = Mathf.Lerp(from._trembleSpeed, to._trembleSpeed, t);
            _gazeShift = Vector2.Lerp(from._gazeShift, to._gazeShift, t);
            _tearLevel = Mathf.Lerp(from._tearLevel, to._tearLevel, t);
        }
    }
}
