using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>깜빡임 회전에서 층이 출발하는 순서</summary>
    public enum BlinkLayerOrder { Together, OuterFirst }

    /// <summary>깜빡임 뒤 재생할 연출 종류</summary>
    public enum BlinkChangeKind { Rotate, PupilRipple }

    /// <summary>
    /// 깜빡임 회전 연출 후보 하나의 시간, 층 순서, 곡선을 담는다.
    /// </summary>
    [Serializable]
    public class BlinkTurnPreset
    {
        [Header("이름")]
        [SerializeField] private string _name;

        [Header("눈꺼풀")]
        [Tooltip("눈이 감기는 시간(초)")]
        [SerializeField] private float _closeSeconds;
        [Tooltip("완전히 감긴 채 머무는 시간(초)")]
        [SerializeField] private float _holdSeconds;
        [Tooltip("눈이 떠지는 시간(초)")]
        [SerializeField] private float _openSeconds;

        [Header("회전")]
        // 스펙 007: 눈을 뜨기 시작한 뒤 회전이 출발하기까지의 지연 추가
        [Tooltip("눈을 뜨기 시작한 뒤 회전이 출발하기까지 기다리는 시간(초)")]
        [SerializeField] private float _turnStartDelaySeconds;
        [Tooltip("층 하나가 도는 시간(초)")]
        [SerializeField] private float _turnSeconds;
        [Tooltip("다음 층이 출발하기까지 늦는 시간(초). 동시 출발이면 쓰지 않는다")]
        [SerializeField] private float _layerDelaySeconds;
        [SerializeField] private BlinkLayerOrder _layerOrder;
        [Tooltip("켜면 홀수 번째 층이 반대쪽 각도에서 출발한다")]
        [SerializeField] private bool _alternateLayers;
        [Tooltip("회전 진행 곡선. 가로는 시간(0~1), 세로는 회전 진행도(0~1)")]
        [SerializeField] private AnimationCurve _curve;
        // 스펙 007: 층이 도는 동안 벽과 문을 옅게 하는 정도 추가
        [Tooltip("층이 도는 동안 벽과 문을 옅게 하는 정도. 0이면 그대로, 0.6이면 가장 옅을 때 40%까지 보인다")]
        [SerializeField, Range(0f, 1f)] private float _wallFadeAmount;

        // 스펙 008: 후보가 재생할 연출 종류 추가
        [Header("연출")]
        [Tooltip("Rotate면 미로 회전, 그 외에는 벽 변화 연출을 재생한다")]
        [SerializeField] private BlinkChangeKind _changeKind;

        public string Name => _name;
        public float CloseSeconds => _closeSeconds;
        public float HoldSeconds => _holdSeconds;
        public float OpenSeconds => _openSeconds;
        public float TurnStartDelaySeconds => _turnStartDelaySeconds;
        public float WallFadeAmount => _wallFadeAmount;
        public float TurnSeconds => _turnSeconds;
        public float LayerDelaySeconds => _layerDelaySeconds;
        public BlinkLayerOrder LayerOrder => _layerOrder;
        public bool AlternateLayers => _alternateLayers;
        public AnimationCurve Curve => _curve;
        public BlinkChangeKind ChangeKind => _changeKind;

        /// <summary>
        /// 연출 후보의 모든 값을 정한다.
        /// 이름, 눈꺼풀 시간, 회전 시간, 층 지연과 순서, 층 번갈아 돌기, 곡선, 연출 종류, 회전 출발 지연, 벽 옅어짐 정도를 사용하며, 각 설정 필드를 바꾼다.
        /// </summary>
        public BlinkTurnPreset(string name, float closeSeconds, float holdSeconds, float openSeconds, float turnSeconds, float layerDelaySeconds, BlinkLayerOrder layerOrder, bool alternateLayers, AnimationCurve curve, BlinkChangeKind changeKind = BlinkChangeKind.Rotate, float turnStartDelaySeconds = 0f, float wallFadeAmount = 0f)
        {
            _changeKind = changeKind;
            _turnStartDelaySeconds = turnStartDelaySeconds;
            _wallFadeAmount = wallFadeAmount;
            _name = name;
            _closeSeconds = closeSeconds;
            _holdSeconds = holdSeconds;
            _openSeconds = openSeconds;
            _turnSeconds = turnSeconds;
            _layerDelaySeconds = layerDelaySeconds;
            _layerOrder = layerOrder;
            _alternateLayers = alternateLayers;
            _curve = curve;
        }
    }
}
