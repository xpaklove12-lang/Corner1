using UnityEngine;

namespace EyeMaze
{
    public class MazeBullet : MonoBehaviour
    {
        [Header("이동 상태")]
        private MazeGunner _owner;
        private Vector2 _direction;
        private float _travelled;
        private float _age;
        private bool _flying;

        /// <summary>
        /// owner와 발사 위치·방향을 받아 탄환의 이동 상태를 초기화하고 활성화한다.
        /// position과 direction을 사용하며, 부모 Transform에서 분리하고 소유자와 수명, 이동 거리를 저장한다.
        /// </summary>
        public void Launch(MazeGunner owner, Vector3 position, Vector2 direction)
        {
            _owner = owner;
            _direction = direction.normalized;
            _travelled = 0f;
            _age = 0f;
            _flying = true;
            transform.SetParent(null, false);
            transform.position = position;
            transform.rotation = Quaternion.FromToRotation(Vector3.right, _direction);
            gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (!_flying) return;
            if (!_owner.CanFire) { ReturnToPool(); return; }

            float dt = Mathf.Min(Time.deltaTime, Mathf.Max(0f, _owner.BulletLifetime - _age));
            float distance = _owner.BulletSpeed * dt;
            if (!_owner.DespawnAtIrisEdge) distance = Mathf.Min(distance, Mathf.Max(0f, _owner.Range - _travelled));
            Vector2 start = transform.position;
            Vector2 end = start + _direction * distance;
            Vector2 segment = end - start;
            Vector2 cursor = _owner.Cursor.transform.position;
            float t = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(cursor - start, segment) / segment.sqrMagnitude) : 0f;
            if ((cursor - (start + segment * t)).sqrMagnitude <= _owner.HitRadius * _owner.HitRadius)
            {
                _owner.Director.HitByBullet(_direction);
                ReturnToPool();
                return;
            }

            transform.position = new Vector3(end.x, end.y, transform.position.z);
            _travelled += distance;
            _age += Time.deltaTime;
            // 흰자 직전 소멸을 쓰는 포대는 사거리 대신 홍채 바깥 경계까지의 거리로 판단한다.
            bool outOfRange = _owner.DespawnAtIrisEdge
                ? (end - _owner.IrisCenter).sqrMagnitude >= _owner.IrisEdgeRadius * _owner.IrisEdgeRadius
                : _travelled >= _owner.Range;
            if (outOfRange || _age >= _owner.BulletLifetime) ReturnToPool();
        }

        /// <summary>
        /// 현재 대여 상태를 확인해 탄환을 소유자의 풀로 한 번 반환한다.
        /// 입력값은 없으며, 비행 상태를 해제하고 오브젝트를 비활성화한다.
        /// </summary>
        public void ReturnToPool()
        {
            if (!_flying) return;
            _flying = false;
            _owner.ReleaseBullet(this);
        }
    }
}
