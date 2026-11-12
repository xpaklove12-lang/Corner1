using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>동공. 인물의 dilation(0~1)에 따라 커지고 작아진다.</summary>
    [ExecuteAlways]
    public class Pupil : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("person")] private Person _person;

        [Header("크기")]
        [SerializeField, FormerlySerializedAs("minRadius")] private float _minRadius = 0.3f;
        [SerializeField, FormerlySerializedAs("maxRadius")] private float _maxRadius = 0.82f;
        [Tooltip("1을 넣으면 한 번 움찔 수축했다가 돌아온다")]
        [SerializeField, FormerlySerializedAs("pulse"), Range(0f, 1f)] private float _pulse;

        // 스펙 011: 동공 떨림 설정 추가
        [Header("떨림")]
        [Tooltip("동공이 떨리는 거리")]
        [SerializeField] private float _trembleDistance = 0.05f;
        [Tooltip("동공이 떨리는 빠르기")]
        [SerializeField] private float _trembleSpeed = 28f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("color")] private Color _color = new Color(0.03f, 0.03f, 0.04f, 1f);
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;
        [SerializeField, FormerlySerializedAs("catchlight")] private LineRenderer _catchlight;
        [SerializeField, FormerlySerializedAs("shown"), HideInInspector] private float _shown = -1f;

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 스펙 011: 떨림 세기와 떨리기 전 위치 추가
        [NonSerialized] private float _tremble;
        [NonSerialized] private bool _trembling;
        [NonSerialized] private Vector3 _restPosition;
        // 스펙 015: 감정 표현의 떨림 거리, 빠르기, 시선 피함 추가
        [NonSerialized] private float _emotionTrembleDistance;
        [NonSerialized] private float _emotionTrembleSpeed = 28f;
        [NonSerialized] private Vector2 _emotionShift;

        public float Pulse { get => _pulse; set => _pulse = value; }
        /// <summary>동공 떨림 세기 (0~1). 0이면 원래 자리로 돌아온다.</summary>
        public float Tremble { get => _tremble; set => _tremble = Mathf.Clamp01(value); }
        /// <summary>감정 표현의 동공 떨림 거리. 휴식 신호 떨림과 함께 있으면 큰 쪽을 쓴다.</summary>
        public float EmotionTrembleDistance { get => _emotionTrembleDistance; set => _emotionTrembleDistance = Mathf.Max(0f, value); }
        /// <summary>감정 표현의 동공 떨림 빠르기</summary>
        public float EmotionTrembleSpeed { get => _emotionTrembleSpeed; set => _emotionTrembleSpeed = value; }
        /// <summary>감정 표현의 시선 피함. 동공 위치에만 더하고 크기는 바꾸지 않는다.</summary>
        public Vector2 EmotionShift { get => _emotionShift; set => _emotionShift = value; }
        public float TargetRadius => Mathf.Lerp(_minRadius, _maxRadius, _person != null ? _person.Dilation : 0.5f);

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            // 스펙 011: 신호 중에는 동공을 제자리 주변에서 떨리게 한다.
            if (Application.isPlaying) ApplyTremble();
            float target = TargetRadius;
            if (_shown < 0f || !Application.isPlaying) _shown = target;
            else _shown = Mathf.MoveTowards(_shown, target, Time.deltaTime * 0.6f);
            if (Application.isPlaying && _pulse > 0f) _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 1.6f);

            int key = HashCode.Combine(_shown, _color, _pulse);
            if (key == _drawnKey) return;
            _drawnKey = key;
            float r = _shown * (1f - 0.35f * Mathf.Sin(_pulse * Mathf.PI));

            if (_line != null)
            {
                Shapes.SetBlob(_line, r * 2f);
                Shapes.SetColor(_line, _color);
                _line.sortingOrder = 8;
            }
            if (_catchlight != null)
            {
                _catchlight.transform.localPosition = new Vector3(r * 0.35f, r * 0.4f, 0f);
                Shapes.SetBlob(_catchlight, Mathf.Max(0.06f, r * 0.32f));
                Shapes.SetColor(_catchlight, new Color(1f, 1f, 1f, 0.85f));
                _catchlight.sortingOrder = 9;
            }
        }

        /// <summary>
        /// 떨림 세기에 맞춰 동공을 원래 자리 주변으로 흔든다. 떨림이 끝나면 원래 자리로 되돌린다.
        /// 휴식 신호 떨림과 감정 떨림 중 거리가 큰 쪽으로 흔들고, 감정의 시선 피함만큼 비켜 둔다.
        /// _tremble, _trembleDistance, _trembleSpeed, 감정 표현 값을 사용하며, Transform 위치와 _restPosition을 바꾼다.
        /// </summary>
        private void ApplyTremble()
        {
            // 스펙 015: 휴식 신호 떨림과 감정 떨림 중 큰 쪽을 쓰고 시선 피함을 더한다
            float tearDistance = _trembleDistance * _tremble;
            bool emotionWins = _emotionTrembleDistance > tearDistance;
            float distance = emotionWins ? _emotionTrembleDistance : tearDistance;
            float speed = emotionWins ? _emotionTrembleSpeed : _trembleSpeed;
            if (distance <= 0f && _emotionShift == Vector2.zero)
            {
                if (_trembling) transform.localPosition = _restPosition;
                _trembling = false;
                return;
            }
            if (!_trembling)
            {
                _restPosition = transform.localPosition;
                _trembling = true;
            }
            // 스펙 015: 떨림 거리와 빠르기를 큰 쪽 값으로 바꾸고 시선 피함을 더함
            float t = Time.time * speed;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * (2f * distance);
            transform.localPosition = _restPosition + offset + (Vector3)_emotionShift;
        }
    }
}
