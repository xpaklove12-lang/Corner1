using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 장애물 하나가 감정에 따라 받는 값. ObstacleField가 값을 쓰고, 같은 오브젝트의 장애물 스크립트가 읽는다.
    /// 이 컴포넌트가 없는 장애물은 기존과 똑같이 움직인다.
    /// </summary>
    public class MazeObstacleBody : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("침잠의 장애물 속 판정과 회전 장애물 피격에 쓰는 반지름")]
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.12f;

        [Header("그림")]
        [Tooltip("투명도를 바꿀 그림")]
        [SerializeField] private Renderer[] _renderers = new Renderer[0];

        private Color[] _baseColors;
        private float _alpha = 1f;
        private Vector3 _homeLocal;

        public float HitRadius => _hitRadius;
        /// <summary>치우침이 없을 때의 기준 자리(월드). 부모가 돌면 함께 돈다.</summary>
        public Vector3 HomeWorld => transform.parent != null ? transform.parent.TransformPoint(_homeLocal) : _homeLocal;
        /// <summary>장애물이 자기 모양으로 "안에 있는지"를 판정할 때 넣는다. 비어 있으면 판정 반지름을 쓴다.</summary>
        public System.Func<Vector3, bool> ShapeContains { get; set; }
        /// <summary>움직이는 속도에 곱하는 배율</summary>
        public float SpeedScale { get; set; } = 1f;
        /// <summary>왕복 폭에 곱하는 배율</summary>
        public float SwingScale { get; set; } = 1f;
        /// <summary>켜면 시선이 닿아도 피격이 일어나지 않는다.</summary>
        public bool Passable { get; set; }
        /// <summary>제자리에서의 치우침(월드 거리)</summary>
        public Vector3 Offset { get; set; }

        /// <summary>그림 투명도. 값이 바뀔 때만 그림에 반영한다.</summary>
        public float Alpha
        {
            get => _alpha;
            set
            {
                if (Mathf.Approximately(_alpha, value)) return;
                _alpha = value;
                ApplyAlpha();
            }
        }

        void Awake()
        {
            _homeLocal = transform.localPosition;
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _baseColors[i] = ColorOf(_renderers[i]);
            // 꺼져 있는 동안 받은 투명도를 켜질 때 반영한다.
            ApplyAlpha();
        }

        /// <summary>
        /// 월드 위치가 이 장애물 안인지 판단한다. 선 장애물처럼 자기 모양 판정이 있으면 그것을, 없으면 판정 반지름을 쓴다.
        /// worldPosition, ShapeContains, _hitRadius를 사용하며, 안이면 true를 반환한다.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            if (ShapeContains != null) return ShapeContains(worldPosition);
            Vector2 delta = worldPosition - transform.position;
            return delta.sqrMagnitude <= _hitRadius * _hitRadius;
        }

        /// <summary>
        /// 연결된 그림들의 원래 색에 지금 투명도를 곱해 칠한다.
        /// _baseColors와 _alpha를 사용하며, 그림의 색을 바꾼다.
        /// </summary>
        private void ApplyAlpha()
        {
            // 아직 켜진 적이 없으면 원래 색을 모르므로 Awake에서 반영한다.
            if (_baseColors == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = _baseColors[i];
                color.a *= _alpha;
                SetColor(_renderers[i], color);
            }
        }

        /// <summary>
        /// 그림의 종류에 맞는 색을 읽는다.
        /// target(선, 스프라이트, 그 밖의 그림)을 사용하며, 지금 색을 반환한다.
        /// </summary>
        private static Color ColorOf(Renderer target)
        {
            if (target is LineRenderer line) return line.startColor;
            if (target is SpriteRenderer sprite) return sprite.color;
            return target.material.color;
        }

        /// <summary>
        /// 그림의 종류에 맞게 색을 칠한다.
        /// target과 color를 사용하며, 선은 시작색과 끝색, 스프라이트와 그 밖의 그림은 색을 바꾼다.
        /// </summary>
        private static void SetColor(Renderer target, Color color)
        {
            if (target is LineRenderer line) Shapes.SetColor(line, color);
            else if (target is SpriteRenderer sprite) sprite.color = color;
            else target.material.color = color;
        }
    }
}
