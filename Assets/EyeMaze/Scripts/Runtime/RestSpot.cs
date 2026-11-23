using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 미로에 놓는 쉼터. 시선이 반지름 안에 머물면 상대의 감정이 한 단계씩 가라앉는다.
    /// 층 오브젝트 아래에 두면 층과 함께 돈다. 원은 같은 오브젝트의 LineRenderer로 그린다.
    /// </summary>
    [ExecuteAlways]
    public class RestSpot : MonoBehaviour
    {
        private const int CIRCLE_SEGMENTS = 40;

        private static readonly List<RestSpot> ActiveSpots = new List<RestSpot>();

        [Header("쉼터")]
        [Tooltip("쉼터 반지름 (이 오브젝트의 로컬 거리)")]
        [SerializeField, Min(0.01f)] private float _radius = 0.14f;
        [Tooltip("쉼터 원의 색")]
        [SerializeField] private Color _color = new Color(0.62f, 0.95f, 0.72f, 0.85f);
        [Tooltip("쉼터 원을 그릴 선")]
        [SerializeField] private LineRenderer _line;

        private float _drawnRadius = -1f;
        private Color _drawnColor;

        /// <summary>지금 켜져 있는 쉼터 목록</summary>
        public static IReadOnlyList<RestSpot> Spots => ActiveSpots;

        void OnEnable()
        {
            ActiveSpots.Add(this);
            _drawnRadius = -1f;
        }

        void OnDisable() => ActiveSpots.Remove(this);

        void OnValidate() => _drawnRadius = -1f;

        void Update()
        {
            if (Mathf.Approximately(_drawnRadius, _radius) && _drawnColor == _color) return;
            _drawnRadius = _radius;
            _drawnColor = _color;
            Shapes.SetCircle(_line, _radius, CIRCLE_SEGMENTS);
            Shapes.SetColor(_line, _color);
        }

        /// <summary>
        /// 월드 위치가 쉼터 안에 있는지 판단한다.
        /// worldPosition과 _radius를 사용하며, 안에 있으면 true를 반환한다.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            Vector2 local = transform.InverseTransformPoint(worldPosition);
            return local.sqrMagnitude <= _radius * _radius;
        }
    }
}
