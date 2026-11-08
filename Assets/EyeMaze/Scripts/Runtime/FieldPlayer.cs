using System;

using UnityEngine;
using UnityEngine.Serialization;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>
    /// 필드 플레이어. 기본은 사이드뷰로, 바닥에 서서 좌우로만 걷는다 (A/D, ←/→).
    /// sideView를 끄면 탑뷰처럼 사방으로 움직이고 가구에 막힌다.
    /// 위치는 Transform 그대로라서 씬 뷰에서 끌어 옮겨도 된다 (사이드뷰에선 높이가 바닥에 맞춰진다).
    /// </summary>
    [ExecuteAlways]
    public class FieldPlayer : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("room")] private FieldRoom _room;

        [Header("이동")]
        [SerializeField, FormerlySerializedAs("sideView")] private bool _sideView = true;
        [Tooltip("몸의 반폭 (벽 판정)")]
        [SerializeField, FormerlySerializedAs("radius")] private float _radius = 0.22f;
        [Tooltip("키 (사이드뷰)")]
        [SerializeField, FormerlySerializedAs("height")] private float _height = 1.0f;
        [SerializeField, FormerlySerializedAs("moveSpeed")] private float _moveSpeed = 3.2f;
        [SerializeField, FormerlySerializedAs("acceleration")] private float _acceleration = 12f;
        [SerializeField, FormerlySerializedAs("facing")] private Vector2 _facing = Vector2.right;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("color")] private Color _color = Color.white;
        [SerializeField, FormerlySerializedAs("body")] private LineRenderer _body;
        [SerializeField, FormerlySerializedAs("nose")] private LineRenderer _nose;

        [NonSerialized] private Vector2 _velocity;
        [NonSerialized] private int _drawnKey = int.MinValue;

        public Vector2 Position => transform.position;
        public bool SideView => _sideView;
        public float Radius => _radius;
        public Vector2 Facing { get => _facing; set => _facing = value; }

        public float FloorY
        {
            get
            {
                if (_room == null) return transform.position.y - _height * 0.5f;
                return _room.Bounds.yMin + _room.WallThickness * 0.5f;
            }
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            // 사이드뷰에서는 씬 뷰에서 옮겨도 발이 바닥에 붙도록 한다.
            if (_sideView && _room != null)
            {
                float y = FloorY + _height * 0.5f;
                if (!Mathf.Approximately(transform.position.y, y))
                    transform.position = new Vector3(transform.position.x, y, transform.position.z);
            }

            int key = HashCode.Combine(_radius, _height, _color, _facing, _sideView);
            if (key == _drawnKey) return;
            _drawnKey = key;
            if (_body != null)
            {
                if (_sideView)
                {
                    // 세로 캡슐로 사람 실루엣을 그린다.
                    float w = _radius * 2f;
                    float half = Mathf.Max(0.001f, _height * 0.5f - w * 0.5f);
                    _body.numCapVertices = 6;
                    Shapes.SetPoints(_body, new[] { new Vector3(0f, -half, 0f), new Vector3(0f, half, 0f) }, false);
                    _body.widthMultiplier = w;
                }
                else Shapes.SetBlob(_body, _radius * 2f);
                Shapes.SetColor(_body, _color);
                _body.sortingOrder = 3;
            }
            if (_nose != null)
            {
                // 바라보는 쪽에 눈을 찍는다.
                _nose.transform.localPosition = _sideView
                    ? new Vector3(_facing.x * _radius * 0.45f, _height * 0.28f, 0f)
                    : (Vector3)(_facing.normalized * _radius * 0.7f);
                Shapes.SetBlob(_nose, _radius * 0.45f);
                Shapes.SetColor(_nose, Color.black);
                _nose.sortingOrder = 4;
            }
        }

        /// <summary>
        /// 움직임을 즉시 멈춘다.
        /// 입력값은 없으며, _velocity를 0으로 만든다.
        /// </summary>
        public void Stop() => _velocity = Vector2.zero;

        /// <summary>
        /// 이동 입력에 따라 가속하며 움직인다.
        /// input, dt를 사용하며, _velocity와 _facing, 위치를 바꾼다.
        /// </summary>
        public void Steer(Vector2 input, float dt)
        {
            if (_sideView) input.y = 0f;
            Vector2 target = Vector2.ClampMagnitude(input, 1f) * _moveSpeed;
            _velocity = Vector2.MoveTowards(_velocity, target, _acceleration * _moveSpeed * dt);
            if (Mathf.Abs(input.x) > 0.1f) _facing = new Vector2(Mathf.Sign(input.x), 0f);
            else if (!_sideView && input.sqrMagnitude > 0.01f) _facing = input.normalized;
            if (_velocity.sqrMagnitude > 1e-8f) MoveBy(_velocity * dt);
        }

        /// <summary>
        /// 충돌을 풀면서 이동한다. 에디트 모드 버튼도 이 메서드를 쓴다.
        /// delta를 사용하며, transform.position을 바꾼다.
        /// </summary>
        public void MoveBy(Vector2 delta)
        {
            if (_sideView) delta.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.05f));
            Vector2 p = Position;
            for (int i = 0; i < steps; i++)
            {
                p += delta / steps;
                p = Resolve(p);
            }
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        /// <summary>
        /// 방 경계와 막는 물체에 겹치지 않도록 위치를 밀어낸다.
        /// p를 사용하며, 보정된 위치를 반환한다.
        /// </summary>
        private Vector2 Resolve(Vector2 p)
        {
            if (_room != null)
            {
                Rect b = _room.Bounds;
                float inset = _radius + _room.WallThickness * 0.5f;
                p.x = Mathf.Clamp(p.x, b.xMin + inset, b.xMax - inset);
                if (_sideView) p.y = FloorY + _height * 0.5f;
                else p.y = Mathf.Clamp(p.y, b.yMin + inset, b.yMax - inset);
            }
            if (_sideView) return p; // 사이드뷰에서 가구는 배경이라 막지 않는다.

            Transform scope = transform.parent;
            FieldObstacle[] obstacles = scope != null ? scope.GetComponentsInChildren<FieldObstacle>(false) : Object.FindObjectsByType<FieldObstacle>(FindObjectsSortMode.None);
            foreach (FieldObstacle o in obstacles)
            {
                if (!o.Blocks) continue;
                Rect r = o.Bounds;
                var closest = new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
                Vector2 d = p - closest;
                float dist = d.magnitude;
                if (dist < _radius && dist > 1e-5f) p = closest + d / dist * _radius;
            }
            FieldInteractable[] others = scope != null ? scope.GetComponentsInChildren<FieldInteractable>(false) : Object.FindObjectsByType<FieldInteractable>(FindObjectsSortMode.None);
            foreach (FieldInteractable o in others)
            {
                if (!o.Blocks) continue;
                Vector2 c = o.transform.position;
                Vector2 d = p - c;
                float min = o.Radius + _radius;
                float dist = d.magnitude;
                if (dist < min && dist > 1e-5f) p = c + d / dist * min;
            }
            return p;
        }
    }
}
