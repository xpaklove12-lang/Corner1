using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 상대의 감정에 따라 씬의 모든 장애물(MazeObstacleBody)의 속도, 왕복 폭, 통과 여부, 투명도, 치우침을 정한다.
    /// 모이기, 부딪혀 물러나기, 비켜 주기는 모두 제자리에서의 치우침으로 처리한다.
    /// </summary>
    public class ObstacleField : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("지금 감정을 읽을 곳")]
        [SerializeField] private EmotionControl _emotion;
        [Tooltip("시선, 미로, 파편을 읽을 디렉터")]
        [SerializeField] private EyeMazeDirector _director;

        [Header("공통")]
        [Tooltip("치우침과 왕복 폭이 목표를 따라가는 속도")]
        [SerializeField, Min(0.01f)] private float _offsetLerpSpeed = 1.2f;
        private MazeObstacleBody[] _bodies = new MazeObstacleBody[0];
        private int[] _layers = new int[0];
        private Vector2[] _positions = new Vector2[0];
        private Vector2[] _clashOffsets = new Vector2[0];
        private Vector2[] _offsets = new Vector2[0];
        private float[] _speeds = new float[0];
        private bool[] _guards = new bool[0];
        private float[] _clashTimes = new float[0];
        private float _speed = 1f;
        private float _swing = 1f;
        // 지킬 기억과, 장애물마다 그 기억 둘레에서 설 자리 번호(지키지 않으면 -1)
        private MemoryFragment _guardTarget;
        private int[] _guardSlots = new int[0];
        private int _guardCount;

        [Header("평온")]
        [Tooltip("평온의 장애물 속도 배율")]
        [SerializeField, Min(0f)] private float _calmSpeedScale = 0.5f;

        [Header("경계와 반감")]
        [Tooltip("경계에서 숨긴 기억 둘레로 모이는 원의 반지름")]
        [SerializeField, Min(0f)] private float _guardWideRadius = 0.45f;
        [Tooltip("반감에서 드러난 기억 둘레로 좁혀 지키는 원의 반지름")]
        [SerializeField, Min(0f)] private float _guardTightRadius = 0.2f;
        [Tooltip("반감에서 시선 쪽으로 비워 두는 틈의 각도")]
        [SerializeField, Range(0f, 180f)] private float _guardGapDegrees = 70f;
        [Tooltip("기억을 둘러싸고 선 장애물의 왕복 폭 배율. 작을수록 원 모양과 틈이 유지된다")]
        [SerializeField, Min(0f)] private float _guardSwingScale = 0.15f;
        [Tooltip("기억을 지키러 모이는 장애물 수. 나머지는 제자리에서 길을 지킨다")]
        [SerializeField, Min(1)] private int _guardMaxCount = 3;

        [Header("동요와 격동")]
        [Tooltip("동요의 속도 배율")]
        [SerializeField, Min(0f)] private float _stirSpeedScale = 1.8f;
        [Tooltip("격동의 속도 배율")]
        [SerializeField, Min(0f)] private float _stirSpeedScale2 = 2.4f;
        [Tooltip("동요의 왕복 폭 배율")]
        [SerializeField, Min(0f)] private float _stirSwingScale = 1.5f;
        [Tooltip("격동의 왕복 폭 배율")]
        [SerializeField, Min(0f)] private float _stirSwingScale2 = 1.8f;
        [Tooltip("두 장애물이 부딪힌 것으로 보는 거리")]
        [SerializeField, Min(0f)] private float _clashRadius = 0.1f;
        [Tooltip("부딪힌 뒤 물러나는 거리")]
        [SerializeField, Min(0f)] private float _clashRetreat = 0.16f;
        [Tooltip("물러나 머무는 시간(초). 머문 뒤 같은 시간 동안은 다시 부딪히지 않는다")]
        [SerializeField, Min(0.05f)] private float _clashHoldSeconds = 1.2f;
        [Tooltip("격동에서 함께 물러나는 범위")]
        [SerializeField, Min(0f)] private float _chainRadius = 0.3f;
        [Tooltip("부딪혀 물러나는 치우침이 목표를 따라가는 속도")]
        [SerializeField, Min(0.01f)] private float _clashLerpSpeed = 6f;

        [Header("슬픔과 침잠")]
        [Tooltip("슬픔과 침잠의 장애물 투명도")]
        [SerializeField, Range(0f, 1f)] private float _sadAlpha = 0.3f;
        [Tooltip("슬픔과 침잠의 장애물 속도 배율")]
        [SerializeField, Min(0f)] private float _sadSpeedScale = 0.5f;

        [Header("희망과 열망")]
        [Tooltip("시선이 이 거리 안에 오면 비켜 주기 시작한다")]
        [SerializeField, Min(0f)] private float _yieldRadius = 0.3f;
        [Tooltip("비켜 주는 거리")]
        [SerializeField, Min(0f)] private float _yieldDistance = 0.3f;
        [Tooltip("물러날 자리에 필요한 빈 거리. 이 안에 다른 장애물이 있으면 비켜 주지 않는다")]
        [SerializeField, Min(0f)] private float _yieldClearance = 0.12f;
        [Tooltip("물러나는 속도")]
        [SerializeField, Min(0f)] private float _yieldSpeed = 0.6f;

        void OnEnable() => _emotion.Changed += Apply;

        void OnDisable() => _emotion.Changed -= Apply;

        void Start()
        {
            _bodies = FindObjectsByType<MazeObstacleBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _layers = new int[_bodies.Length];
            _positions = new Vector2[_bodies.Length];
            _clashOffsets = new Vector2[_bodies.Length];
            _offsets = new Vector2[_bodies.Length];
            _speeds = new float[_bodies.Length];
            _guards = new bool[_bodies.Length];
            _clashTimes = new float[_bodies.Length];
            _guardSlots = new int[_bodies.Length];
            for (int i = 0; i < _bodies.Length; i++)
            {
                MazeLayer layer = _bodies[i].GetComponentInParent<MazeLayer>(true);
                _layers[i] = layer != null ? layer.Index : -1;
                _clashTimes[i] = float.MinValue;
                _speeds[i] = 1f;
            }
            Apply();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // 왕복 폭은 감정이 바뀌어도 장애물이 순간이동하지 않도록 천천히 따라간다.
            IrisMaze maze = _director.Maze;
            Transform mazeTransform = maze.transform;
            for (int i = 0; i < _bodies.Length; i++)
            {
                MazeObstacleBody body = _bodies[i];
                // 기억을 둘러싸고 선 장애물은 왕복을 줄여 원 모양과 틈을 지킨다.
                float swing = _guards[i] ? _guardSwingScale : _swing;
                body.SwingScale = Mathf.MoveTowards(body.SwingScale, swing, _offsetLerpSpeed * dt);
                // 치우침은 미로 기준으로 기억해 두어, 계산을 멈춘 동안 깜빡임으로 미로가 돌아도 함께 돌게 한다.
                body.Offset = mazeTransform.TransformVector(_offsets[i]);
            }
            // 대화로 나가 있거나 깜빡이는 동안에는 치우침 계산을 멈춘다.
            if (!_director.CanRunHazards) return;

            Emotion emotion = _emotion.Current;
            bool stage2 = _emotion.Stage >= 2;
            Vector2 gaze = _director.Cursor.LocalPos;
            int gazeLayer = maze.LayerOfRing(_director.Cursor.Ring);

            for (int i = 0; i < _bodies.Length; i++)
                _positions[i] = mazeTransform.InverseTransformPoint(_bodies[i].transform.position);
            if (emotion == Emotion.Stir) TickClash(dt, stage2);

            Vector2 center = Vector2.zero;
            bool guarding = emotion == Emotion.Guard && TryFindGuardCenter(gazeLayer, gaze, out center);

            for (int i = 0; i < _bodies.Length; i++)
            {
                MazeObstacleBody body = _bodies[i];
                if (!body.isActiveAndEnabled) continue;
                Vector2 current = _offsets[i];
                Vector2 target = Vector2.zero;
                // 속도도 감정이 바뀔 때 천천히 따라간다. 부딪혀 머무는 동안에만 바로 멈춘다.
                _speeds[i] = Mathf.MoveTowards(_speeds[i], _speed, _offsetLerpSpeed * dt);
                float speed = _speeds[i];
                _guards[i] = guarding && _guardSlots[i] >= 0;
                Vector2 next;
                if (emotion == Emotion.Hope)
                {
                    target = YieldTarget(i, current, gaze, maze);
                    next = Vector2.MoveTowards(current, target, _yieldSpeed * dt);
                    // 스펙 046 FR-048: 비켜 주는 장애물은 색으로 알아볼 수 있게 한다.
                    body.Yielding = target != Vector2.zero;
                }
                else
                {
                    float follow = _offsetLerpSpeed;
                    if (_guards[i])
                    {
                        Vector2 home = mazeTransform.InverseTransformPoint(body.HomeWorld);
                        target = GuardPoint(center, gaze, stage2, _guardSlots[i], _guardCount) - home;
                    }
                    else if (emotion == Emotion.Stir)
                    {
                        follow = _clashLerpSpeed;
                        if (_clashTimes[i] > 0f)
                        {
                            // 부딪혀 물러난 장애물은 그 자리에 머문다.
                            target = _clashOffsets[i];
                            speed = 0f;
                        }
                    }
                    next = Vector2.Lerp(current, target, 1f - Mathf.Exp(-follow * dt));
                }
                _offsets[i] = next;
                body.Offset = mazeTransform.TransformVector(next);
                body.SpeedScale = speed;
            }
        }

        /// <summary>
        /// 시선의 이동 속도에 곱할 배율을 구한다. 슬픔은 느려지고, 침잠은 장애물 속에 있는 동안에는 느려지지 않는다.
        /// gazeWorld(시선의 월드 위치)와 지금 감정을 사용하며, 배율을 반환한다.
        /// </summary>
        public float GazeSpeedScale(Vector3 gazeWorld)
        {
            if (_emotion.Current != Emotion.Sad) return 1f;
            if (_emotion.Stage >= 2)
            {
                foreach (MazeObstacleBody body in _bodies)
                    if (body.isActiveAndEnabled && body.Contains(gazeWorld)) return 1f;
            }
            return _emotion.SadGazeScale;
        }

        /// <summary>
        /// 월드 위치가 켜져 있는 장애물 가운데 하나의 안인지 판단한다. 감정이 바뀌는 순간의 무적 판정에 쓴다.
        /// worldPosition을 사용하며, 안이면 true를 반환한다.
        /// </summary>
        public bool IsInsideAny(Vector3 worldPosition)
        {
            foreach (MazeObstacleBody body in _bodies)
                if (body.isActiveAndEnabled && body.Contains(worldPosition)) return true;
            return false;
        }

        /// <summary>
        /// 지금 감정과 단계에 맞는 값을 모든 장애물에 쓴다. 속도, 왕복 폭, 치우침은 Update가 천천히 따라가게 한다.
        /// 감정별 Inspector 값을 사용하며, 각 MazeObstacleBody의 통과 여부, 투명도와 목표 속도 _speed, 목표 왕복 폭 _swing, 부딪힘과 둘러싸기 상태를 바꾼다.
        /// </summary>
        private void Apply()
        {
            Emotion emotion = _emotion.Current;
            bool stage2 = _emotion.Stage >= 2;
            _speed = 1f;
            _swing = 1f;
            if (emotion == Emotion.Calm) _speed = _calmSpeedScale;
            else if (emotion == Emotion.Stir)
            {
                _speed = stage2 ? _stirSpeedScale2 : _stirSpeedScale;
                _swing = stage2 ? _stirSwingScale2 : _stirSwingScale;
            }
            else if (emotion == Emotion.Sad) _speed = _sadSpeedScale;

            bool sad = emotion == Emotion.Sad;
            for (int i = 0; i < _bodies.Length; i++)
            {
                MazeObstacleBody body = _bodies[i];
                body.Passable = sad;
                body.Alpha = sad ? _sadAlpha : 1f;
                // 동요를 벗어나면 물러나 머물던 상태를 푼다.
                if (emotion != Emotion.Stir) _clashTimes[i] = float.MinValue;
                if (emotion != Emotion.Guard) _guards[i] = false;
                if (emotion != Emotion.Hope) body.Yielding = false;
            }
            // 경계를 벗어나면 지키던 기억을 잊어, 다음 경계에서 다시 고른다.
            if (emotion != Emotion.Guard) _guardTarget = null;
        }

        /// <summary>
        /// 장애물의 치우침, 부딪힘, 지키기 상태를 처음으로 되돌린다. 만남을 처음부터 다시 시작할 때 부른다.
        /// 입력값은 없으며, _offsets, _clashOffsets, _clashTimes, _speeds, _guards, _guardTarget과 각 장애물의 치우침을 바꾼다.
        /// </summary>
        public void ResetOffsets()
        {
            _guardTarget = null;
            for (int i = 0; i < _bodies.Length; i++)
            {
                _offsets[i] = Vector2.zero;
                _clashOffsets[i] = Vector2.zero;
                _clashTimes[i] = float.MinValue;
                _speeds[i] = _speed;
                _guards[i] = false;
                _guardSlots[i] = -1;
                _bodies[i].Offset = Vector3.zero;
            }
        }

        /// <summary>
        /// 동요와 격동에서 가까워진 두 장애물을 서로 반대쪽으로 물러나게 한다. 격동이면 근처 장애물도 같은 쪽으로 물러난다.
        /// dt, stage2, _positions(미로 기준 장애물 위치)를 사용하며, _clashTimes와 _clashOffsets를 바꾼다.
        /// </summary>
        private void TickClash(float dt, bool stage2)
        {
            // 양수면 머무는 중이고, 0에서 머문 시간만큼 더 내려가면 다시 부딪힐 수 있다.
            for (int i = 0; i < _bodies.Length; i++)
            {
                // 물러나는 중에는 머무는 시간을 세지 않고, 물러난 자리에 닿은 뒤부터 센다.
                bool retreating = _clashTimes[i] > 0f && (_offsets[i] - _clashOffsets[i]).sqrMagnitude > 0.0004f;
                if (!retreating && _clashTimes[i] > -_clashHoldSeconds) _clashTimes[i] -= dt;
            }

            for (int i = 0; i < _bodies.Length; i++)
            {
                if (!CanClash(i)) continue;
                for (int j = i + 1; j < _bodies.Length; j++)
                {
                    if (!CanClash(j) || _layers[i] != _layers[j]) continue;
                    Vector2 apart = _positions[i] - _positions[j];
                    if (apart.sqrMagnitude > _clashRadius * _clashRadius) continue;
                    Vector2 direction = apart.sqrMagnitude > 1e-8f ? apart.normalized : Vector2.up;
                    Retreat(i, direction, stage2);
                    Retreat(j, -direction, stage2);
                    break;
                }
            }
        }

        /// <summary>
        /// 장애물 하나를 정한 방향으로 물러나 머물게 한다. 격동이면 범위 안의 다른 장애물도 같은 방향으로 물러난다.
        /// 장애물 번호 index, direction(미로 기준 방향), chain(근처도 함께 물러날지)을 사용하며, _clashTimes와 _clashOffsets를 바꾼다.
        /// </summary>
        private void Retreat(int index, Vector2 direction, bool chain)
        {
            // 이미 치우쳐 있던 자리에서 정한 거리만큼 더 물러난다.
            _clashOffsets[index] = _offsets[index] + direction * _clashRetreat;
            _clashTimes[index] = _clashHoldSeconds;
            if (!chain) return;
            for (int k = 0; k < _bodies.Length; k++)
            {
                if (!CanClash(k) || _layers[k] != _layers[index]) continue;
                if ((_positions[k] - _positions[index]).sqrMagnitude > _chainRadius * _chainRadius) continue;
                _clashOffsets[k] = _offsets[k] + direction * _clashRetreat;
                _clashTimes[k] = _clashHoldSeconds;
            }
        }

        /// <summary>
        /// 장애물이 지금 부딪힐 수 있는 상태인지 판단한다. 꺼져 있거나 머무는 중이거나 막 풀려난 장애물은 부딪히지 않는다.
        /// 장애물 번호 index와 _clashTimes를 사용하며, 부딪힐 수 있으면 true를 반환한다.
        /// </summary>
        private bool CanClash(int index)
        {
            return _bodies[index].isActiveAndEnabled && _clashTimes[index] <= -_clashHoldSeconds;
        }

        /// <summary>
        /// 경계와 반감에서 장애물이 지킬 기억의 자리를 찾는다. 한 번 정한 기억은 얻거나 시선이 다른 층으로 갈 때까지 바꾸지 않는다.
        /// 시선이 있는 층 layer, gaze(미로 기준 시선 위치)를 사용하며, _guardTarget과 지킬 장애물을 정하고 찾으면 true를 반환하며 미로 기준 자리를 center에 쓴다.
        /// </summary>
        private bool TryFindGuardCenter(int layer, Vector2 gaze, out Vector2 center)
        {
            center = Vector2.zero;
            IrisMaze maze = _director.Maze;
            bool keep = _guardTarget != null && _guardTarget.isActiveAndEnabled && !_guardTarget.Collected
                && maze.LayerOfRing(_guardTarget.Ring) == layer;
            if (!keep)
            {
                // 숨김 여부와 상관없이 그 층의 얻지 못한 기억 가운데 시선에 가장 가까운 것을 지킨다.
                _guardTarget = null;
                float best = float.MaxValue;
                foreach (MemoryFragment fragment in _director.Fragments)
                {
                    if (fragment.Collected || maze.LayerOfRing(fragment.Ring) != layer) continue;
                    Vector2 position = maze.transform.InverseTransformPoint(fragment.transform.position);
                    float distance = (position - gaze).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    _guardTarget = fragment;
                }
                if (_guardTarget == null) return false;
                AssignGuards(layer, maze.transform.InverseTransformPoint(_guardTarget.transform.position));
            }
            center = maze.transform.InverseTransformPoint(_guardTarget.transform.position);
            return true;
        }

        /// <summary>
        /// 지킬 기억에 가까운 장애물부터 정해진 수만큼 골라 설 자리 번호를 준다. 나머지는 제자리에 남는다.
        /// 층 번호 layer, center(미로 기준 기억 자리), _guardMaxCount를 사용하며, _guardSlots와 _guardCount를 바꾼다.
        /// </summary>
        private void AssignGuards(int layer, Vector2 center)
        {
            Transform mazeTransform = _director.Maze.transform;
            for (int i = 0; i < _bodies.Length; i++) _guardSlots[i] = -1;
            _guardCount = 0;
            while (_guardCount < _guardMaxCount)
            {
                int nearest = -1;
                float best = float.MaxValue;
                for (int i = 0; i < _bodies.Length; i++)
                {
                    if (_guardSlots[i] >= 0 || _layers[i] != layer || !_bodies[i].isActiveAndEnabled) continue;
                    Vector2 home = mazeTransform.InverseTransformPoint(_bodies[i].HomeWorld);
                    float distance = (home - center).sqrMagnitude;
                    if (distance >= best) continue;
                    best = distance;
                    nearest = i;
                }
                if (nearest < 0) return;
                _guardSlots[nearest] = _guardCount++;
            }
        }

        /// <summary>
        /// 기억 둘레의 원에서 장애물 하나가 설 자리를 구한다. 경계는 원 둘레를 고르게 나누고, 반감은 시선 쪽 틈을 뺀 나머지를 고르게 나눈다.
        /// center, gaze(미로 기준), stage2, 자리 번호 slot, 자리 수 count를 사용하며, 미로 기준 자리를 반환한다.
        /// </summary>
        private Vector2 GuardPoint(Vector2 center, Vector2 gaze, bool stage2, int slot, int count)
        {
            if (!stage2) return center + (Vector2)Shapes.Polar(_guardWideRadius, 360f * slot / count);
            Vector2 toGaze = gaze - center;
            float gazeDegrees = Mathf.Atan2(toGaze.y, toGaze.x) * Mathf.Rad2Deg;
            float degrees = gazeDegrees + _guardGapDegrees * 0.5f + (360f - _guardGapDegrees) * (slot + 0.5f) / count;
            return center + (Vector2)Shapes.Polar(_guardTightRadius, degrees);
        }

        /// <summary>
        /// 희망과 열망에서 장애물 하나가 비켜 설 치우침을 구한다. 시선이 멀거나, 물러날 자리 가까이에 다른 장애물이 있거나, 닫힌 벽이 가로막으면 비키지 않는다.
        /// 장애물 번호 index, current(지금 치우침), gaze(미로 기준 시선 위치), maze를 사용하며, 미로 기준 목표 치우침을 반환한다.
        /// </summary>
        private Vector2 YieldTarget(int index, Vector2 current, Vector2 gaze, IrisMaze maze)
        {
            // 물러난 뒤에도 판단이 흔들리지 않도록 치우침을 뺀 자리를 기준으로 본다.
            Vector2 basePosition = _positions[index] - current;
            Vector2 away = basePosition - gaze;
            if (away.sqrMagnitude > _yieldRadius * _yieldRadius) return Vector2.zero;
            Vector2 offset = (away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.up) * _yieldDistance;
            Vector2 destination = basePosition + offset;
            for (int k = 0; k < _bodies.Length; k++)
            {
                if (k == index || !_bodies[k].isActiveAndEnabled) continue;
                if ((_positions[k] - destination).sqrMagnitude < _yieldClearance * _yieldClearance) return Vector2.zero;
            }
            return maze.IsPathBlocked(basePosition, destination) ? Vector2.zero : offset;
        }
    }
}
