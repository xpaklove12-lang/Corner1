using UnityEngine;

namespace EyeMaze
{
    public class SplineBarrageBullet : MonoBehaviour
    {
        [Header("이동 상태")]
        private SplineBarrage _owner;
        private float _distance;
        private float _offset;
        private float _age;
        private int _direction;

        /// <summary>
        /// owner, direction, offset으로 탄환을 경로의 시작 또는 끝에 배치한다.
        /// 소유자와 진행 방향, 레인, 수명을 저장하고 탄환을 활성화한다.
        /// </summary>
        public void Launch(SplineBarrage owner, int direction, float offset)
        {
            _owner = owner;
            _direction = direction;
            _offset = offset;
            _distance = direction > 0 ? 0f : owner.Length;
            _age = 0f;
            transform.SetParent(null, false);
            transform.position = owner.PointAt(_distance, _offset);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// dt와 소유자의 탄속으로 경로 이동과 시선 접촉을 계산한다.
        /// 위치와 수명을 변경하고 반환해야 하면 true를 반환한다.
        /// </summary>
        public bool Advance(float dt)
        {
            float remaining = Mathf.Min(dt, Mathf.Max(0f, _owner.Lifetime - _age)) * _owner.Speed;
            _age += dt;
            while (remaining > 0f)
            {
                float step = Mathf.Min(0.025f, remaining);
                float next = Mathf.Clamp(_distance + step * _direction, 0f, _owner.Length);
                Vector3 end = _owner.PointAt(next, _offset);
                Vector2 start = transform.position;
                Vector2 delta = (Vector2)end - start;
                Vector2 cursor = _owner.Cursor.transform.position;
                float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(cursor - start, delta) / delta.sqrMagnitude) : 0f;
                transform.position = end;
                if ((cursor - start - delta * t).sqrMagnitude <= _owner.HitRadius * _owner.HitRadius)
                {
                    _owner.Director.HitByBullet(delta.normalized);
                    return true;
                }
                _distance = next;
                remaining -= step;
                if (_distance <= 0f || _distance >= _owner.Length) return true;
            }
            return _age >= _owner.Lifetime;
        }
    }
}
