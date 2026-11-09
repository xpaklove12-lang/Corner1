using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 미로의 한 층(고리 묶음). 오브젝트를 회전시키면 그 층의 벽, 문, 기억 조각이 함께 돈다.
    /// </summary>
    [ExecuteAlways]
    public class MazeLayer : MonoBehaviour
    {
        public const string WALL_CONTAINER_NAME = "벽 (Walls)";

        [Header("층")]
        [SerializeField, FormerlySerializedAs("index")] private int _index;
        [SerializeField, FormerlySerializedAs("displayName")] private string _displayName = "층";
        [Tooltip("현재 회전량 (칸 단위, 반시계 +)")]
        [SerializeField, FormerlySerializedAs("offset")] private int _offset;
        // 스펙 006: 깜빡임 층 회전 설정 제거

        private IrisMaze _maze;

        public int Index => _index;
        /// <summary>층 이름. Strings.csv의 layer.0, layer.1 … 이 있으면 그걸 쓴다.</summary>
        public string DisplayName => Strings.Get("layer." + _index, _displayName);
        public int Offset { get => _offset; set => _offset = value; }

        void OnValidate() => EyeHooks.Repaint();

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null) return;
            if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
            Quaternion rot = Quaternion.Euler(0f, 0f, _offset * _maze.SectorDeg);
            if (transform.localRotation != rot) transform.localRotation = rot;
        }
    }
}
