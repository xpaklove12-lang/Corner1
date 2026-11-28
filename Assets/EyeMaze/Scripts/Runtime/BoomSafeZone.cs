using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 폭발 미로의 안전지대 칸 하나. 이 칸은 어떤 폭발 무늬에도 터지지 않는다.
    /// 사각 미로(BoomSquareMaze) 아래에 두고, 씬 뷰에서 이동 툴(W)로 끌어 놓으면 놓은 칸 한가운데로 맞춰진다.
    /// 열과 행을 Inspector에서 바꿔도 그 칸으로 옮겨진다. 늘리려면 복제(Ctrl+D)하고, 줄이려면 지운다.
    /// </summary>
    [ExecuteAlways]
    public class BoomSafeZone : MonoBehaviour
    {
        [Header("위치")]
        [Tooltip("열 (0 = 왼쪽)")]
        [SerializeField] private int _column;
        [Tooltip("행 (0 = 아래)")]
        [SerializeField] private int _row;

        [Header("씬 뷰 표시")]
        [SerializeField] private Color _gizmoColor = new Color(0.35f, 0.95f, 0.75f, 1f);

        private BoomSquareMaze _square;
        // 마지막으로 칸에 맞춘 월드 위치. 이와 달라지면 사용자가 끌어 옮긴 것이다.
        private Vector3 _appliedPos;
        private bool _applied;

        public Vector2Int Cell => new Vector2Int(_column, _row);

        private BoomSquareMaze Square => _square != null ? _square : (_square = GetComponentInParent<BoomSquareMaze>());

        void OnValidate() => _applied = false;

        void Update()
        {
            if (Square == null) return;
            // 씬 뷰에서 끌어 옮겼으면 놓은 자리의 칸을 받아들인다.
            if (_applied && (transform.position - _appliedPos).sqrMagnitude > 1e-8f) PickCellAt(transform.position);
            SnapToCell();
        }

        /// <summary>
        /// 월드 위치가 사각 미로의 어느 칸에 있는지 계산해 열과 행으로 정한다.
        /// world(월드 위치)와 사각 미로 Transform을 사용하며, _column과 _row를 바꾼다.
        /// </summary>
        private void PickCellAt(Vector3 world)
        {
            Vector2Int cell = Square.CellAtLocal(Square.transform.InverseTransformPoint(world));
            _column = cell.x;
            _row = cell.y;
        }

        /// <summary>
        /// 열과 행을 범위 안으로 맞추고, 오브젝트를 그 칸 한가운데로 옮긴다.
        /// _column, _row, 사각 미로 모양을 사용하며, _column, _row, transform.position과 적용 기록을 바꾼다.
        /// </summary>
        private void SnapToCell()
        {
            _column = Mathf.Clamp(_column, 0, Square.Columns - 1);
            _row = Mathf.Clamp(_row, 0, Square.Rows - 1);
            Vector3 pos = Square.transform.TransformPoint(Square.CellCenterLocal(Cell));
            // 아주 작은 오차로는 옮기지 않아 프리팹 인스턴스에 쓸데없는 위치 오버라이드가 생기지 않게 한다.
            if ((transform.position - pos).sqrMagnitude > 1e-8f) transform.position = pos;
            _appliedPos = pos;
            _applied = true;
        }

        void OnDrawGizmos()
        {
            if (Square == null) return;
            // 칸 테두리를 사각 미로 기준으로 그린다.
            Gizmos.matrix = Square.transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            Vector3 center = Square.CellCenterLocal(Cell);
            Gizmos.DrawWireCube(center, new Vector3(Square.CellSize, Square.CellSize, 0f) * 0.92f);
            Gizmos.DrawSphere(center, Square.CellSize * 0.12f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
