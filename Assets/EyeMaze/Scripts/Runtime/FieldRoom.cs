using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>방. 바닥과 벽을 그리고, 플레이어가 나가지 못하게 막는다. size를 바꾸면 바로 반영된다.</summary>
    [ExecuteAlways]
    public class FieldRoom : MonoBehaviour
    {
        [Header("방")]
        [SerializeField, FormerlySerializedAs("roomName")] private string _roomName = "상담실";
        [SerializeField, FormerlySerializedAs("size")] private Vector2 _size = new Vector2(12f, 7.5f);
        [SerializeField, FormerlySerializedAs("wallThickness")] private float _wallThickness = 0.18f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("floorColor")] private Color _floorColor = new Color(0.27f, 0.22f, 0.19f, 1f);
        [SerializeField, FormerlySerializedAs("wallColor")] private Color _wallColor = new Color(0.78f, 0.7f, 0.6f, 1f);
        [SerializeField, FormerlySerializedAs("floor")] private LineRenderer _floor;
        [SerializeField, FormerlySerializedAs("walls")] private LineRenderer _walls;

        [NonSerialized] private int _drawnKey = int.MinValue;

        public float WallThickness => _wallThickness;

        public Rect Bounds
        {
            get
            {
                Vector2 c = transform.position;
                return new Rect(c - _size * 0.5f, _size);
            }
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            int key = HashCode.Combine(_size, _wallThickness, _floorColor, _wallColor);
            if (key == _drawnKey) return;
            _drawnKey = key;
            if (_floor != null)
            {
                Shapes.SetFilledRect(_floor, _size.x, _size.y);
                Shapes.SetColor(_floor, _floorColor);
                _floor.sortingOrder = -30;
            }
            if (_walls != null)
            {
                float hx = _size.x * 0.5f;
                float hy = _size.y * 0.5f;
                Shapes.SetPoints(_walls, new[]
                {
                    new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f), new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f)
                }, true);
                _walls.widthMultiplier = _wallThickness;
                Shapes.SetColor(_walls, _wallColor);
                _walls.sortingOrder = -29;
            }
        }
    }
}
