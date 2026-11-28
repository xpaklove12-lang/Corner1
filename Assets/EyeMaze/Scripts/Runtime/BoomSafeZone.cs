using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로의 안전지대 칸 하나. 이 칸은 어떤 폭발 무늬에도 터지지 않는다.
    /// 폭발 미로(MazeBoomField) 아래에 두고, 씬 뷰에서 이동 툴(W)로 끌어 놓으면 놓은 칸 한가운데로 맞춰진다.
    /// 고리와 칸 번호를 Inspector에서 바꿔도 그 칸으로 옮겨진다. 늘리려면 복제(Ctrl+D)하고, 줄이려면 지운다.
    /// </summary>
    [ExecuteAlways]
    public class BoomSafeZone : MonoBehaviour
    {
        private const int GIZMO_ARC_SEGMENTS = 8;

        [Header("위치")]
        [Tooltip("고리 (0 = 가장 바깥)")]
        [SerializeField] private int _ring;
        [Tooltip("칸 번호 (반시계 방향, 미로 기준)")]
        [SerializeField] private int _sector;

        [Header("씬 뷰 표시")]
        [SerializeField] private Color _gizmoColor = new Color(0.35f, 0.95f, 0.75f, 1f);

        private IrisMaze _maze;
        // 마지막으로 칸에 맞춘 월드 위치. 이와 달라지면 사용자가 끌어 옮긴 것이다.
        private Vector3 _appliedPos;
        private bool _applied;

        public int Ring => _ring;
        public int Sector => _sector;

        private IrisMaze Maze => _maze != null ? _maze : (_maze = GetComponentInParent<IrisMaze>());

        void OnValidate() => _applied = false;

        void Update()
        {
            if (Maze == null) return;
            // 씬 뷰에서 끌어 옮겼으면 놓은 자리의 칸을 받아들인다.
            if (_applied && (transform.position - _appliedPos).sqrMagnitude > 1e-8f) PickCellAt(transform.position);
            SnapToCell();
        }

        /// <summary>
        /// 월드 위치가 미로의 어느 칸에 있는지 계산해 고리와 칸 번호로 정한다.
        /// world(월드 위치)와 미로 Transform을 사용하며, _ring과 _sector를 바꾼다.
        /// </summary>
        private void PickCellAt(Vector3 world)
        {
            Vector3 p = Maze.transform.InverseTransformPoint(world);
            _ring = Maze.RingAt(new Vector2(p.x, p.y).magnitude);
            _sector = Maze.SectorAt(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 고리와 칸 번호를 범위 안으로 맞추고, 오브젝트를 그 칸 한가운데로 옮긴다.
        /// _ring, _sector, 미로 모양을 사용하며, _ring, _sector, transform.position과 적용 기록을 바꾼다.
        /// </summary>
        private void SnapToCell()
        {
            _ring = Mathf.Clamp(_ring, 0, Maze.Rings - 1);
            _sector = Maze.Mod(_sector);
            Vector3 pos = Maze.transform.TransformPoint(Maze.CellLocal(_ring, _sector));
            // 아주 작은 오차로는 옮기지 않아 프리팹 인스턴스에 쓸데없는 위치 오버라이드가 생기지 않게 한다.
            if ((transform.position - pos).sqrMagnitude > 1e-8f) transform.position = pos;
            _appliedPos = pos;
            _applied = true;
        }

        void OnDrawGizmos()
        {
            if (Maze == null) return;
            // 칸 테두리(바깥 원호, 안쪽 원호, 양쪽 칸막이)를 미로 기준으로 그린다.
            Gizmos.matrix = Maze.transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            float outer = Maze.RingOuter(_ring);
            float inner = Maze.RingInner(_ring);
            float a0 = _sector * Maze.SectorDeg;
            float a1 = a0 + Maze.SectorDeg;
            for (int i = 0; i < GIZMO_ARC_SEGMENTS; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)GIZMO_ARC_SEGMENTS);
                float t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)GIZMO_ARC_SEGMENTS);
                Gizmos.DrawLine(Shapes.Polar(outer, t0), Shapes.Polar(outer, t1));
                Gizmos.DrawLine(Shapes.Polar(inner, t0), Shapes.Polar(inner, t1));
            }
            Gizmos.DrawLine(Shapes.Polar(inner, a0), Shapes.Polar(outer, a0));
            Gizmos.DrawLine(Shapes.Polar(inner, a1), Shapes.Polar(outer, a1));
            Gizmos.DrawSphere(Maze.CellLocal(_ring, _sector), Maze.RingWidth * 0.15f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
