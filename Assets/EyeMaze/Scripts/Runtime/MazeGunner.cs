using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Pool;

namespace EyeMaze
{
    public class MazeGunner : MonoBehaviour
    {
        private const int BURST_SHOTS = 3;

        private enum FirePhase { Waiting, Warning, Reloading, BurstInterval, BurstRest, Sequencing }

        [Header("연결")]
        [SerializeField] private EyeMazeDirector _director;
        [SerializeField] private GazeCursor _cursor;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private MazeBullet _bulletPrefab;
        [SerializeField] private LineRenderer _warningLine;
        [SerializeField] private LineRenderer[] _additionalWarningLines = new LineRenderer[0];

        [Header("발사")]
        [Tooltip("Muzzle 로컬 오른쪽 기준 반시계방향 각도")]
        [SerializeField] private float[] _fireAngles = { 0f };
        [Tooltip("켜면 각도마다 정반대(180도 뒤) 방향으로도 함께 발사한다. 같은 방향이 이미 있으면 추가하지 않는다")]
        [SerializeField] private bool _fireOpposite;
        [SerializeField, Min(0.01f)] private float _warningSeconds = 0.8f;
        [Tooltip("연속 발사를 사용하지 않을 때의 재장전 시간")]
        [SerializeField, Min(0.01f)] private float _reloadSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float _bulletSpeed = 2f;
        [SerializeField, Min(0.01f)] private float _range = 0.8f;
        [SerializeField, Min(0.01f)] private float _bulletLifetime = 3f;
        [SerializeField, Min(0.001f)] private float _hitRadius = 0.08f;
        [SerializeField, Min(1)] private int _initialPoolSize = 8;

        [Header("순차 발사")]
        [Tooltip("각도 목록을 한 발씩 쏘는 간격(초). 0이면 모든 각도를 동시에 발사")]
        [SerializeField, Min(0f)] private float _shotInterval;

        [Header("흰자 직전 소멸")]
        [Tooltip("켜면 사거리 대신 홍채 바깥 테두리 직전에서 탄환이 사라진다")]
        [SerializeField] private bool _despawnAtIrisEdge;
        [Tooltip("홍채 바깥 반지름에서 뺄 여유 거리")]
        [SerializeField, Min(0f)] private float _irisEdgeMargin = 0.05f;

        [Header("3회 연속 발사")]
        [SerializeField] private bool _burstFire;
        [Tooltip("연속하는 발사 사이 간격(초)")]
        [SerializeField, Min(0.01f)] private float _burstInterval = 0.2f;
        [Tooltip("세 번째 발사부터 다음 첫 발사까지의 휴식(초). 다음 예고 포함")]
        [SerializeField, Min(0.01f)] private float _burstRestSeconds = 1.5f;

        private ObjectPool<MazeBullet> _pool;
        private readonly List<MazeBullet> _activeBullets = new List<MazeBullet>();
        private FirePhase _phase;
        private float _timer;
        private int _burstShotsFired;
        private int _nextShotIndex;
        private float[] _shotAngles = { 0f };

        public EyeMazeDirector Director => _director;
        public GazeCursor Cursor => _cursor;
        public bool CanFire => isActiveAndEnabled && _director.CanRunHazards;
        public float BulletSpeed => _bulletSpeed;
        public float Range => _range;
        public float BulletLifetime => _bulletLifetime;
        public float HitRadius => _hitRadius;
        public bool DespawnAtIrisEdge => _despawnAtIrisEdge;
        public Vector2 IrisCenter => _director.Maze.transform.position;
        public float IrisEdgeRadius => _director.Maze.OuterRadius * _director.Maze.transform.lossyScale.x - _irisEdgeMargin;
        private int ShotCount => _shotAngles.Length;

        void Awake()
        {
            BuildShotAngles();
            if (!ResolveContext())
            {
                Debug.LogWarning("Gunner: 같은 미로의 진행 제어를 찾을 수 없습니다. Director를 지정하세요.", this);
                enabled = false;
                return;
            }
            _pool = new ObjectPool<MazeBullet>(CreateBullet, null, DeactivateBullet, DestroyBullet,
                true, _initialPoolSize, int.MaxValue);
            MazeBullet[] warmup = new MazeBullet[_initialPoolSize];
            for (int i = 0; i < warmup.Length; i++) warmup[i] = _pool.Get();
            foreach (MazeBullet bullet in warmup) _pool.Release(bullet);
            UpdateWarningLines(false);
        }

        void LateUpdate()
        {
            if (!CanFire) { ResetFire(); return; }
            if (Time.deltaTime <= 0f) return;
            if (_phase == FirePhase.Waiting) BeginWarning();
            _timer += Time.deltaTime;
            if (_phase == FirePhase.Warning)
            {
                UpdateWarningLines(true);
                if (_timer < _warningSeconds) return;
                FireVolley();
            }
            else if (_phase == FirePhase.BurstInterval)
            {
                if (_timer >= Mathf.Max(0.01f, _burstInterval)) FireVolley();
            }
            else if (_phase == FirePhase.BurstRest)
            {
                float rest = Mathf.Max(0.01f, _burstRestSeconds);
                UpdateWarningLines(_timer >= Mathf.Max(0f, rest - _warningSeconds));
                if (_timer < rest) return;
                _burstShotsFired = 0;
                FireVolley();
            }
            else if (_phase == FirePhase.Sequencing)
            {
                if (_timer >= _shotInterval) FireNextShot();
            }
            else if (_phase == FirePhase.Reloading && _timer >= _reloadSeconds) BeginWarning();
        }

        void OnDisable() { ResetFire(); }

        void OnDestroy()
        {
            ResetFire();
            _pool?.Dispose();
        }

        void OnValidate()
        {
            _warningSeconds = Mathf.Max(0.01f, _warningSeconds);
            _reloadSeconds = Mathf.Max(0.01f, _reloadSeconds);
            _bulletSpeed = Mathf.Max(0.01f, _bulletSpeed);
            _range = Mathf.Max(0.01f, _range);
            _bulletLifetime = Mathf.Max(0.01f, _bulletLifetime);
            _hitRadius = Mathf.Max(0.001f, _hitRadius);
            _initialPoolSize = Mathf.Max(1, _initialPoolSize);
            _burstInterval = Mathf.Max(0.01f, _burstInterval);
            _burstRestSeconds = Mathf.Max(0.01f, _burstRestSeconds);
            BuildShotAngles();
        }

        /// <summary>
        /// _fireAngles와 _fireOpposite로 실제 발사 각도 목록을 만든다.
        /// 각도가 없으면 0도 하나를 쓰고, 반대 방향 발사가 켜져 있으면 겹치지 않는 180도 뒤 각도를 덧붙여 _shotAngles에 저장한다.
        /// </summary>
        private void BuildShotAngles()
        {
            if (_fireAngles == null || _fireAngles.Length == 0)
            {
                _shotAngles = new[] { 0f };
                return;
            }
            var angles = new List<float>(_fireAngles);
            if (_fireOpposite)
            {
                foreach (float angle in _fireAngles)
                {
                    float opposite = angle + 180f;
                    // 이미 같은 방향이 있으면 두 번 쏘지 않는다.
                    if (angles.Exists(a => Mathf.Abs(Mathf.DeltaAngle(a, opposite)) < 0.01f)) continue;
                    angles.Add(opposite);
                }
            }
            _shotAngles = angles.ToArray();
        }

        /// <summary>
        /// 현재 Muzzle과 각도 목록으로 탄환을 대여해 발사한다.
        /// _shotInterval이 0이면 모두 동시에 쏘고 다음 단계로 넘어가며, 0보다 크면 첫 발을 쏘고 순차 발사 단계로 바꾼다.
        /// </summary>
        private void FireVolley()
        {
            UpdateWarningLines(false);
            if (_shotInterval > 0f)
            {
                _nextShotIndex = 0;
                FireNextShot();
                return;
            }
            for (int i = 0; i < ShotCount; i++) LaunchBullet(i);
            FinishVolley();
        }

        /// <summary>
        /// _nextShotIndex 각도로 한 발을 쏘고 번호를 올린다.
        /// 남은 각도가 있으면 순차 발사 단계와 타이머를 갱신하고, 없으면 발사 마무리 단계로 넘어간다.
        /// </summary>
        private void FireNextShot()
        {
            LaunchBullet(_nextShotIndex);
            _nextShotIndex++;
            _timer = 0f;
            if (_nextShotIndex < ShotCount)
            {
                _phase = FirePhase.Sequencing;
                return;
            }
            _nextShotIndex = 0;
            FinishVolley();
        }

        /// <summary>
        /// index 각도와 현재 Muzzle 회전으로 풀에서 탄환 하나를 대여해 발사한다.
        /// 발사한 탄환을 _activeBullets에 추가한다.
        /// </summary>
        private void LaunchBullet(int index)
        {
            MazeBullet bullet = _pool.Get();
            _activeBullets.Add(bullet);
            bullet.Launch(this, _muzzle.position, GetFireDirection(index));
        }

        /// <summary>
        /// 한 번의 발사를 마친 뒤 연속 발사 설정과 완료 횟수에 따라 간격 대기, 휴식 또는 단발 재장전으로 전환한다.
        /// _timer, _burstShotsFired, _phase를 변경한다.
        /// </summary>
        private void FinishVolley()
        {
            _timer = 0f;
            if (!_burstFire)
            {
                _burstShotsFired = 0;
                _phase = FirePhase.Reloading;
                return;
            }
            _burstShotsFired++;
            _phase = _burstShotsFired < BURST_SHOTS ? FirePhase.BurstInterval : FirePhase.BurstRest;
        }

        /// <summary>
        /// 입력값 없이 발사 예고 단계를 시작하고 타이머와 선 표시를 변경한다.
        /// 예고선은 Inspector에 저장된 외형을 사용한다.
        /// </summary>
        private void BeginWarning()
        {
            _phase = FirePhase.Warning;
            _timer = 0f;
            UpdateWarningLines(true);
        }

        /// <summary>
        /// index에 해당하는 발사 각도를 Muzzle 회전에 합성한다.
        /// 각도 목록이 비어 있으면 0도를 사용하고 월드 발사 방향을 반환한다.
        /// </summary>
        private Vector3 GetFireDirection(int index)
        {
            float angle = _shotAngles[index];
            return _muzzle.rotation * Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.right;
        }

        /// <summary>
        /// visible과 발사 각도 목록으로 연결된 예고선의 표시 여부와 양 끝점을 갱신한다.
        /// 사용하지 않는 추가 예고선은 숨기며 외형 설정은 유지한다.
        /// </summary>
        private void UpdateWarningLines(bool visible)
        {
            int extraCount = _additionalWarningLines == null ? 0 : _additionalWarningLines.Length;
            for (int i = 0; i <= extraCount; i++)
            {
                LineRenderer line = i == 0 ? _warningLine : _additionalWarningLines[i - 1];
                if (line == null) continue;
                line.enabled = visible && i < ShotCount;
                if (!line.enabled) continue;
                line.SetPosition(0, _muzzle.position);
                line.SetPosition(1, _muzzle.position + GetFireDirection(i) * _range);
            }
        }

        /// <summary>
        /// Inspector 참조 또는 부모 미로와 동일 씬의 유일한 진행 제어로 연결을 결정한다.
        /// 기존 참조와 씬 후보를 사용해 디렉터와 시선을 저장하고 연결 성공 여부를 반환한다.
        /// </summary>
        private bool ResolveContext()
        {
            if (_director == null)
            {
                IrisMaze maze = GetComponentInParent<IrisMaze>();
                foreach (EyeMazeDirector candidate in FindObjectsByType<EyeMazeDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != gameObject.scene || (maze != null && candidate.Maze != maze)) continue;
                    if (_director != null) { _director = null; return false; }
                    _director = candidate;
                }
            }
            if (_director == null) return false;
            if (_cursor == null) _cursor = _director.Cursor;
            return _cursor != null;
        }

        /// <summary>
        /// 탄환 프리팹과 풀 기준점을 사용해 재사용할 비활성 탄환을 생성한다.
        /// 생성된 MazeBullet을 반환한다.
        /// </summary>
        private MazeBullet CreateBullet()
        {
            MazeBullet bullet = Instantiate(_bulletPrefab, _poolRoot);
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        /// <summary>
        /// 반환된 bullet을 비활성화하고 풀 기준점 아래로 돌려놓아 다음 대여까지 숨긴다.
        /// 반환값은 없다.
        /// </summary>
        private void DeactivateBullet(MazeBullet bullet)
        {
            bullet.gameObject.SetActive(false);
            bullet.transform.SetParent(_poolRoot, false);
        }

        /// <summary>
        /// 풀 해제 대상 bullet의 GameObject를 제거한다.
        /// 반환값은 없다.
        /// </summary>
        private void DestroyBullet(MazeBullet bullet)
        {
            // 씬 종료에서는 풀 기준점의 자식이 포대보다 먼저 제거될 수 있다.
            if (bullet != null) Destroy(bullet.gameObject);
        }

        /// <summary>
        /// bullet을 활성 목록에서 제거하고 풀에 반환한다.
        /// 풀의 비활성 탄환 수를 변경하며 반환값은 없다.
        /// </summary>
        public void ReleaseBullet(MazeBullet bullet)
        {
            _activeBullets.Remove(bullet);
            _pool.Release(bullet);
        }

        /// <summary>
        /// 입력값 없이 활성 탄환을 모두 회수하고 예고와 타이머를 초기화한다.
        /// 다음 조작 시 예고부터 다시 시작하도록 대기 상태로 변경한다.
        /// </summary>
        private void ResetFire()
        {
            for (int i = _activeBullets.Count - 1; i >= 0; i--)
            {
                if (_activeBullets[i] != null) _activeBullets[i].ReturnToPool();
                else _activeBullets.RemoveAt(i);
            }
            UpdateWarningLines(false);
            _phase = FirePhase.Waiting;
            _timer = 0f;
            _burstShotsFired = 0;
            _nextShotIndex = 0;
        }
    }
}
