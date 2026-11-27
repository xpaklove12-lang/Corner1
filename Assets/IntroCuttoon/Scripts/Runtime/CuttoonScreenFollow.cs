using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸 카메라에 비친 3D 부품의 화면 범위 오른쪽에 UI 표시를 붙여 두는 컴포넌트.
    /// 부품이나 카메라가 움직여도 부품과 표시 사이 간격을 칸 화면 단위로 같게 지킨다.
    /// </summary>
    public class CuttoonScreenFollow : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("부품을 찍는 칸 카메라")]
        [SerializeField] private Camera _camera;
        [Tooltip("칸 카메라 화면을 보여 주는 RawImage")]
        [SerializeField] private RectTransform _view;
        [Tooltip("화면 범위를 잴 3D 부품")]
        [SerializeField] private MeshFilter[] _parts;
        [Tooltip("부품 옆에 붙일 UI 표시. 표시의 기준점이 부품 옆 자리에 놓인다")]
        [SerializeField] private RectTransform _mark;

        [Header("붙이기")]
        [Tooltip("켜면 Play 동안 매 프레임 표시를 부품 옆으로 옮긴다")]
        [SerializeField] private bool _follow = false;
        [Tooltip("부품 화면 범위 오른쪽 끝에서 표시 기준점까지의 간격(칸 화면 단위)")]
        [SerializeField] private float _gap = 16f;
        [Tooltip("표시 기준점 높이를 부품 화면 범위 아래 끝에서 높이의 몇 비율 지점에 둘지")]
        [SerializeField] private float _heightRatio = 0.75f;

        private Vector3[][] _vertices;

        void Awake()
        {
            // 메시 꼭짓점은 읽을 때마다 배열을 새로 만들므로 처음에 한 번 받아 둔다.
            _vertices = new Vector3[_parts.Length][];
            for (int i = 0; i < _parts.Length; i++)
            {
                _vertices[i] = _parts[i].sharedMesh.vertices;
            }
        }

        void LateUpdate()
        {
            if (!_follow)
            {
                return;
            }

            PlaceMark();
        }

        void OnValidate()
        {
            _heightRatio = Mathf.Clamp01(_heightRatio);
        }

        /// <summary>
        /// _parts 화면 범위의 오른쪽 끝에서 _gap만큼, 아래 끝에서 높이의 _heightRatio만큼 떨어진 _view 안 자리를 구한다.
        /// _mark의 기준점이 그 자리에 놓이도록 _mark의 월드 위치를 바꾼다.
        /// </summary>
        private void PlaceMark()
        {
            Rect bounds = ViewportBounds();
            Rect viewRect = _view.rect;
            Vector2 local = new Vector2(
                viewRect.xMin + bounds.xMax * viewRect.width + _gap,
                viewRect.yMin + (bounds.yMin + bounds.height * _heightRatio) * viewRect.height);
            _mark.position = _view.TransformPoint(local);
        }

        /// <summary>
        /// _camera로 본 _parts 메시 꼭짓점의 화면 범위를 뷰포트 좌표(0~1) Rect로 구해 돌려준다.
        /// </summary>
        private Rect ViewportBounds()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < _parts.Length; i++)
            {
                Matrix4x4 toWorld = _parts[i].transform.localToWorldMatrix;
                foreach (Vector3 vertex in _vertices[i])
                {
                    Vector2 viewport = _camera.WorldToViewportPoint(toWorld.MultiplyPoint3x4(vertex));
                    min = Vector2.Min(min, viewport);
                    max = Vector2.Max(max, viewport);
                }
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
