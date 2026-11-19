using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Splines;

namespace EyeMaze
{
    public class SplineBarrage : MonoBehaviour
    {
        private const int PATH_SAMPLES = 128;

        [Header("연결")]
        [SerializeField] private SplineContainer _path;
        [SerializeField] private EyeMazeDirector _director;
        [SerializeField] private SplineBarrageBullet _bulletPrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private LineRenderer[] _warningLines;

        [Header("탄막")]
        [SerializeField, Min(0.01f)] private float _warningSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float _waveInterval = 1.2f;
        [SerializeField, Range(2, 9)] private int _laneCount = 3;
        [SerializeField, Min(0.01f)] private float _laneSpacing = 0.14f;
        [SerializeField, Min(0.01f)] private float _speed = 0.65f;
        [SerializeField, Min(0.01f)] private float _lifetime = 8f;
        [SerializeField, Min(0.001f)] private float _hitRadius = 0.045f;
        [SerializeField, Min(1)] private int _initialPoolSize = 24;
        private readonly Vector3[] _points = new Vector3[PATH_SAMPLES + 1];
        private readonly float[] _distances = new float[PATH_SAMPLES + 1];
        private readonly List<SplineBarrageBullet> _active = new List<SplineBarrageBullet>();
        private ObjectPool<SplineBarrageBullet> _pool;
        private float _timer;
        private int _wave;
        private bool _running;
        private bool _warning;
        private bool _ready;

        public EyeMazeDirector Director => _director;
        public GazeCursor Cursor => _director.Cursor;
        public float Length => _distances[PATH_SAMPLES];
        public float Speed => _speed;
        public float Lifetime => _lifetime;
        public float HitRadius => _hitRadius;

        void Awake()
        {
            if (_director == null)
            {
                IrisMaze maze = GetComponentInParent<IrisMaze>();
                foreach (EyeMazeDirector candidate in FindObjectsByType<EyeMazeDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.gameObject.scene != gameObject.scene || (maze != null && candidate.Maze != maze)) continue;
                    if (_director != null) { _director = null; break; }
                    _director = candidate;
                }
            }
            if (_director == null || _path.Spline.Count < 2 || _path.Spline.Closed || _warningLines.Length < _laneCount)
            {
                Debug.LogWarning("SplineBarrage: 열린 경로의 Knot 2개 이상, 진행 제어와 레인 수 이상의 예고선이 필요합니다.", this);
                enabled = false;
                return;
            }
            _pool = new ObjectPool<SplineBarrageBullet>(CreateBullet, null, StoreBullet, DestroyBullet, true, _initialPoolSize, int.MaxValue);
            var warmup = new SplineBarrageBullet[_initialPoolSize];
            for (int i = 0; i < warmup.Length; i++) warmup[i] = _pool.Get();
            foreach (SplineBarrageBullet bullet in warmup) _pool.Release(bullet);
            _ready = true;
            HideWarnings();
        }

        void LateUpdate()
        {
            if (!_director.CanRunHazards) { ResetBarrage(); return; }
            if (!_running)
            {
                if (!BuildPath()) { enabled = false; return; }
                _running = true;
                BeginWarning();
            }
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_director.CanRunHazards) { ResetBarrage(); return; }
                if (_active[i].Advance(Time.deltaTime)) ReleaseAt(i);
            }
            if (!_director.CanRunHazards) { ResetBarrage(); return; }
            _timer += Time.deltaTime;
            if (_warning && _timer >= _warningSeconds)
            {
                FireWave();
                HideWarnings();
                _warning = false;
                _timer = 0f;
            }
            else if (!_warning && _timer >= _waveInterval) BeginWarning();
        }

        void OnDisable() { ResetBarrage(); }

        void OnDestroy()
        {
            ResetBarrage();
            _pool?.Dispose();
        }

        void OnValidate()
        {
            _laneCount = Mathf.Clamp(_laneCount, 2, 9);
            _laneSpacing = Mathf.Max(0.01f, _laneSpacing);
            _warningSeconds = Mathf.Max(0.01f, _warningSeconds);
            _waveInterval = Mathf.Max(0.01f, _waveInterval);
            _speed = Mathf.Max(0.01f, _speed);
            _lifetime = Mathf.Max(0.01f, _lifetime);
            _hitRadius = Mathf.Max(0.001f, _hitRadius);
            _initialPoolSize = Mathf.Max(1, _initialPoolSize);
        }

        /// <summary>
        /// 저장된 SplineContainer를 사용해 곡선의 월드 위치와 누적 거리 표를 갱신한다.
        /// 유효한 길이가 있으면 true를 반환하며 Transform과 Knot는 변경하지 않는다.
        /// </summary>
        private bool BuildPath()
        {
            _distances[0] = 0f;
            for (int i = 0; i <= PATH_SAMPLES; i++)
            {
                _points[i] = _path.EvaluatePosition(i / (float)PATH_SAMPLES);
                if (i > 0) _distances[i] = _distances[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
            }
            if (Length > 0.001f) return true;
            Debug.LogWarning("SplineBarrage: 경로 길이가 0입니다.", this);
            return false;
        }

        /// <summary>
        /// distance와 offset을 사용해 누적 거리상의 위치와 레인 오프셋을 계산한다.
        /// 경로를 따라 배치한 월드 좌표를 반환한다.
        /// </summary>
        public Vector3 PointAt(float distance, float offset)
        {
            int lo = 1;
            int hi = PATH_SAMPLES;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_distances[mid] < distance) lo = mid + 1;
                else hi = mid;
            }
            Vector3 tangent = _points[lo] - _points[lo - 1];
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            float t = Mathf.InverseLerp(_distances[lo - 1], _distances[lo], distance);
            return Vector3.Lerp(_points[lo - 1], _points[lo], t) + normal * offset;
        }

        /// <summary>
        /// lane 번호와 레인 간격으로 중심 경로 기준의 오프셋을 계산해 반환한다.
        /// 기존 상태는 변경하지 않는다.
        /// </summary>
        private float LaneOffset(int lane) { return (lane - (_laneCount - 1) * 0.5f) * _laneSpacing; }

        /// <summary>
        /// 웨이브 번호와 경로 표를 사용해 빈 레인을 제외한 예고선을 갱신한다.
        /// 예고 상태와 타이머, 선의 월드 위치를 변경한다.
        /// </summary>
        private void BeginWarning()
        {
            _warning = true;
            _timer = 0f;
            for (int lane = 0; lane < _warningLines.Length; lane++)
            {
                LineRenderer line = _warningLines[lane];
                line.enabled = lane < _laneCount && lane != _wave % _laneCount;
                if (!line.enabled) continue;
                line.positionCount = PATH_SAMPLES + 1;
                for (int i = 0; i <= PATH_SAMPLES; i++) line.SetPosition(i, PointAt(_distances[i], LaneOffset(lane)));
            }
        }

        /// <summary>
        /// 현재 웨이브의 빈 레인을 제외하고 양 끝에 풀 탄환을 배치한다.
        /// 활성 목록과 다음 빈 레인을 위한 웨이브 번호를 변경한다.
        /// </summary>
        private void FireWave()
        {
            for (int lane = 0; lane < _laneCount; lane++)
            {
                if (lane == _wave % _laneCount) continue;
                for (int direction = -1; direction <= 1; direction += 2)
                {
                    SplineBarrageBullet bullet = _pool.Get();
                    _active.Add(bullet);
                    bullet.Launch(this, direction, LaneOffset(lane));
                }
            }
            _wave = (_wave + 1) % _laneCount;
        }

        /// <summary>
        /// 탄환 프리팹과 풀 기준점을 사용해 비활성 탄환을 생성하여 반환한다.
        /// 풀 확장 또는 초기 준비 시에만 인스턴스를 만든다.
        /// </summary>
        private SplineBarrageBullet CreateBullet()
        {
            SplineBarrageBullet bullet = Instantiate(_bulletPrefab, _poolRoot);
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        /// <summary>
        /// bullet을 비활성화하고 풀 기준점 아래로 옮긴다.
        /// 탄환의 부모와 활성 상태를 변경하며 반환값은 없다.
        /// </summary>
        private void StoreBullet(SplineBarrageBullet bullet)
        {
            bullet.gameObject.SetActive(false);
            bullet.transform.SetParent(_poolRoot, false);
        }

        /// <summary>
        /// 풀 해제 시 살아 있는 bullet 오브젝트를 제거한다.
        /// 씬 종료로 이미 파괴된 탄환은 건너뛰며 반환값은 없다.
        /// </summary>
        private void DestroyBullet(SplineBarrageBullet bullet) { if (bullet != null) Destroy(bullet.gameObject); }

        /// <summary>
        /// index에 해당하는 활성 탄환을 목록에서 제거하고 풀로 반환한다.
        /// 씬 종료로 파괴된 탄환은 목록만 정리한다.
        /// </summary>
        private void ReleaseAt(int index)
        {
            SplineBarrageBullet bullet = _active[index];
            _active.RemoveAt(index);
            if (bullet != null) _pool.Release(bullet);
        }

        /// <summary>
        /// 입력값 없이 예고선을 모두 숨긴다.
        /// 각 LineRenderer의 활성 표시 상태를 변경한다.
        /// </summary>
        private void HideWarnings()
        {
            foreach (LineRenderer line in _warningLines) if (line != null) line.enabled = false;
        }

        /// <summary>
        /// 입력값 없이 활성 탄환과 예고를 정리하고 첫 웨이브로 초기화한다.
        /// 다음 조작 시 경로와 예고를 다시 준비하도록 진행 상태를 변경한다.
        /// </summary>
        private void ResetBarrage()
        {
            if (!_ready) return;
            for (int i = _active.Count - 1; i >= 0; i--) ReleaseAt(i);
            HideWarnings();
            _running = false;
            _warning = false;
            _timer = 0f;
            _wave = 0;
        }
    }
}
