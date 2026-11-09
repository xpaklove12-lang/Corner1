using System.Collections.Generic;

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
        // 스펙 009: 깜빡임 회전 때 화면에만 더하는 각도 추가
        private float _blinkVisualDeg;
        // 스펙 009: 벽과 문에 전달한 표시 투명도와 조회용 목록 추가
        private float _wallVisualAlpha = 1f;
        private readonly List<MazeWall> _wallBuffer = new List<MazeWall>();
        private readonly List<MazeGate> _gateBuffer = new List<MazeGate>();

        public int Index => _index;
        /// <summary>층 이름. Strings.csv의 layer.0, layer.1 … 이 있으면 그걸 쓴다.</summary>
        public string DisplayName => Strings.Get("layer." + _index, _displayName);
        public int Offset { get => _offset; set => _offset = value; }
        /// <summary>깜빡임 회전 때 화면에만 더하는 각도(도). 벽 판정에는 쓰지 않는다.</summary>
        public float BlinkVisualDeg { get => _blinkVisualDeg; set => _blinkVisualDeg = value; }

        /// <summary>
        /// 이 층의 벽과 문에 표시 투명도를 전달한다. 값이 그대로면 아무 일도 하지 않아 다른 연출의 투명도를 건드리지 않는다.
        /// alpha를 사용하며, _wallVisualAlpha와 자식 벽, 문의 VisualAlpha를 바꾼다.
        /// </summary>
        public void SetWallVisualAlpha(float alpha)
        {
            if (Mathf.Approximately(alpha, _wallVisualAlpha)) return;
            _wallVisualAlpha = alpha;
            GetComponentsInChildren(true, _wallBuffer);
            foreach (MazeWall wall in _wallBuffer) wall.VisualAlpha = alpha;
            GetComponentsInChildren(true, _gateBuffer);
            foreach (MazeGate gate in _gateBuffer) gate.VisualAlpha = alpha;
        }

        void OnValidate() => EyeHooks.Repaint();

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null) return;
            if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
            // 스펙 009: 칸 회전량에 화면 표시용 각도를 더함
            Quaternion rot = Quaternion.Euler(0f, 0f, _offset * _maze.SectorDeg + _blinkVisualDeg);
            if (transform.localRotation != rot) transform.localRotation = rot;
        }
    }
}
