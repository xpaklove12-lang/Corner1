using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 필드의 가구. outline이면 흰 테두리 사각형, 아니면 꽉 찬 사각형.
    /// 위치는 Transform, 크기는 size로 편집한다. blocks는 탑뷰에서만 의미가 있다.
    /// </summary>
    [ExecuteAlways]
    public class FieldObstacle : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField, FormerlySerializedAs("size")] private Vector2 _size = new Vector2(2f, 1f);
        [SerializeField, FormerlySerializedAs("color")] private Color _color = Color.white;
        [SerializeField, FormerlySerializedAs("outline")] private bool _outline = true;
        [SerializeField, FormerlySerializedAs("lineWidth")] private float _lineWidth = 0.07f;
        [SerializeField, FormerlySerializedAs("blocks")] private bool _blocks;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;

        [NonSerialized] private int _drawnKey = int.MinValue;

        public bool Blocks => _blocks;

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
            int key = HashCode.Combine(_size, _color, _outline, _lineWidth);
            if (_line == null || key == _drawnKey) return;
            _drawnKey = key;
            if (_outline)
            {
                float hx = _size.x * 0.5f;
                float hy = _size.y * 0.5f;
                _line.numCapVertices = 0;
                _line.numCornerVertices = 0;
                Shapes.SetPoints(_line, new[]
                {
                    new Vector3(-hx, -hy, 0f), new Vector3(hx, -hy, 0f), new Vector3(hx, hy, 0f), new Vector3(-hx, hy, 0f)
                }, true);
                _line.widthMultiplier = _lineWidth;
            }
            else Shapes.SetFilledRect(_line, _size.x, _size.y);
            Shapes.SetColor(_line, _color);
            _line.sortingOrder = -20;
        }
    }
}
