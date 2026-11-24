using EyeMaze;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SplineObstacle : MonoBehaviour
{
    private const float MIN_RENDER_LENGTH = 0.0001f;
    private const float MIN_SAMPLE_SPACING = 0.002f;

    [Serializable]
    private class OriginTarget
    {
        [Header("연결")]
        [SerializeField] private Transform _origin;

        public Transform Origin => _origin;
    }

    [Header("연결")]
    [SerializeField] private Transform _origin;
    [SerializeField] private LineRenderer _linePrefab;
    [SerializeField] private OriginTarget[] _originTargets = Array.Empty<OriginTarget>();
    [SerializeField] private EyeMazeDirector _director;

    [Header("파형")]
    [SerializeField, Min(0.01f)] private float _drawDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _segmentLength = 0.22f;
    [SerializeField, Min(0f)] private float _amplitude;
    [SerializeField, Range(1, 5)] private int _waveCount = 1;
    [SerializeField, Range(8, 96)] private int _samples = 24;

    [Header("접촉")]
    [SerializeField, Range(0f, 1f)] private float _contactBurden = 0.02f;
    [SerializeField, Min(0.01f)] private float _contactInterval = 0.5f;
    [SerializeField, Min(0f)] private float _cursorRadius = 0.04f;
    private readonly List<LineRenderer> _lines = new List<LineRenderer>();
    private Vector3[] _homePositions;
    private Vector3[] _starts;
    private Vector3[] _ends;
    private Vector3[] _normals;
    private float _elapsed;
    private bool _running;
    private float _contactCooldown;
    // 스펙 046: 감정별 값을 받는 컴포넌트와 선 프리팹의 원래 색 추가
    private MazeObstacleBody _body;
    private Color _lineStartColor;
    private Color _lineEndColor;
    // 스펙 046: 지금 구간에 이미 반영한 치우침
    private Vector3 _appliedOffset;
    // 스펙 046 FR-040: 이미 피격을 준 접촉이 이어지는 중인지 기억
    private bool _touching;

    private int ConnectionCount => _originTargets.Length > 0 ? _originTargets.Length : 1;

    void Awake()
    {
        if (_director == null)
        {
            foreach (EyeMazeDirector candidate in FindObjectsByType<EyeMazeDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != gameObject.scene) continue;
                if (_director != null)
                {
                    Debug.LogWarning("SplineObstacle: Director를 직접 지정하세요.", this);
                    enabled = false;
                    return;
                }
                _director = candidate;
            }
        }
        _homePositions = new Vector3[ConnectionCount];
        _starts = new Vector3[ConnectionCount];
        _ends = new Vector3[ConnectionCount];
        _normals = new Vector3[ConnectionCount];
        for (int i = 0; i < ConnectionCount; i++)
        {
            if (OriginAt(i) == null || _linePrefab == null)
            {
                Debug.LogWarning("SplineObstacle: OriginPos와 선 프리팹을 연결하세요.", this);
                enabled = false;
                return;
            }
            _homePositions[i] = OriginAt(i).localPosition;
        }
        _body = GetComponent<MazeObstacleBody>();
        // 스펙 046: 침잠의 장애물 속 판정이 기준점 대신 그려진 선을 쓰도록 연결
        if (_body != null) _body.ShapeContains = ContainsPoint;
        _lineStartColor = _linePrefab.startColor;
        _lineEndColor = _linePrefab.endColor;
    }

    void LateUpdate()
    {
        if (_director != null && !_director.CanRunHazards)
        {
            if (_running) StopDrawing();
            return;
        }
        if (!_running) { DrawLines(); return; }
        _contactCooldown = Mathf.Max(0f, _contactCooldown - Time.deltaTime);
        float duration = _drawDuration + _moveDuration;
        // 스펙 046: 감정별 속도 배율을 곱함
        _elapsed = Mathf.Min(duration, _elapsed + Time.deltaTime * (_body != null ? _body.SpeedScale : 1f));
        // 스펙 046: 구간 도중에 치우침이 바뀌면 그만큼 구간을 옮겨 멈춰 있는 선도 물러나게 함
        if (_body != null && _body.Offset != _appliedOffset)
        {
            Vector3 shift = _body.Offset - _appliedOffset;
            _appliedOffset = _body.Offset;
            for (int i = 0; i < ConnectionCount; i++) { _starts[i] += shift; _ends[i] += shift; }
        }
        float head = Mathf.Clamp01(_elapsed / _drawDuration);
        float tail = Mathf.Clamp01((_elapsed - _drawDuration) / _moveDuration);
        for (int i = 0; i < ConnectionCount; i++)
        {
            RenderSegment(i, tail, head);
            OriginAt(i).position = WavePoint(i, tail);
        }
        CheckLineContact();
        if (_director != null && !_director.CanRunHazards) { StopDrawing(); return; }
        if (_elapsed >= duration)
        {
            for (int i = 0; i < ConnectionCount; i++) OriginAt(i).localPosition = _homePositions[i];
            BeginSegment();
        }
    }

    void OnDisable() { StopDrawing(); }

    void OnValidate()
    {
        _drawDuration = Mathf.Max(0.01f, _drawDuration);
        _moveDuration = Mathf.Max(0.01f, _moveDuration);
        _segmentLength = Mathf.Max(0.01f, _segmentLength);
        _amplitude = Mathf.Max(0f, _amplitude);
        _waveCount = Mathf.Clamp(_waveCount, 1, 5);
        _samples = Mathf.Clamp(_samples, 8, 96);
        _contactBurden = Mathf.Clamp01(_contactBurden);
        _contactInterval = Mathf.Max(0.01f, _contactInterval);
        _cursorRadius = Mathf.Max(0f, _cursorRadius);
    }

    /// <summary>
    /// 저장된 OriginPos들의 현재 위치와 로컬 Y축으로 선의 반복 생성을 시작한다.
    /// 기존 선을 재사용하고 현재 위치를 다음 파형 구간의 시작점으로 저장한다.
    /// </summary>
    public void DrawLines()
    {
        if (_homePositions == null) return;
        while (_lines.Count < ConnectionCount)
        {
            LineRenderer line = Instantiate(_linePrefab, transform);
            line.useWorldSpace = true;
            line.loop = false;
            _lines.Add(line);
        }
        _running = true;
        BeginSegment();
    }

    /// <summary>
    /// index에 해당하는 연결 또는 기존 단일 기준점을 사용한다.
    /// 실제 이동시킬 OriginPos Transform을 반환한다.
    /// </summary>
    private Transform OriginAt(int index) { return _originTargets.Length > 0 ? _originTargets[index].Origin : _origin; }

    /// <summary>
    /// 현재 표시된 선분과 굵기를 사용해 시선 접촉을 판정한다.
    /// 시선 반지름과 접촉 간격을 사용하며 접촉하면 부담감과 재적용 대기를 변경한다.
    /// </summary>
    private void CheckLineContact()
    {
        if (_director == null || !_director.CanRunHazards || _contactBurden <= 0f) return;
        Vector2 cursor = _director.Cursor.transform.position;
        // 스펙 046: 선분 판정을 침잠의 장애물 속 판정과 함께 쓰도록 TryFindLineContact로 옮김
        // 스펙 046 FR-040: 시선이 선에서 벗어났다가 다시 닿아야 또 피격을 줌
        if (!TryFindLineContact(cursor, out Vector2 hitDirection)) { _touching = false; return; }
        // 스펙 046: 슬픔과 침잠에서는 닿아도 피격하지 않음
        if (_touching || _contactCooldown > 0f || (_body != null && _body.Passable)) return;
        if (!_director.AddObstacleBurden(_contactBurden, hitDirection)) return;
        _contactCooldown = _contactInterval;
        _touching = true;
    }

    /// <summary>
    /// 스펙 046: 월드 위치가 지금 그려진 선분의 굵기 안에 있는지 판단한다. 침잠의 장애물 속 판정에 쓴다.
    /// worldPosition을 사용하며, 선 위에 있으면 true를 반환한다.
    /// </summary>
    private bool ContainsPoint(Vector3 worldPosition)
    {
        return _running && TryFindLineContact(worldPosition, out _);
    }

    /// <summary>
    /// 스펙 046: 현재 표시된 선분과 굵기로 한 점이 선에 닿았는지 찾는다.
    /// cursor(월드 위치)와 시선 반지름을 사용하며, 닿았으면 true를 반환하고 선에서 밀려날 방향을 direction에 쓴다.
    /// </summary>
    private bool TryFindLineContact(Vector2 cursor, out Vector2 direction)
    {
        direction = Vector2.zero;
        foreach (LineRenderer line in _lines)
        {
            if (!line.enabled || !line.gameObject.activeInHierarchy || line.positionCount < 2) continue;
            AnimationCurve width = line.widthCurve;
            for (int p = 1; p < line.positionCount; p++)
            {
                Vector2 start = line.GetPosition(p - 1);
                Vector2 delta = (Vector2)line.GetPosition(p) - start;
                if (delta.sqrMagnitude < MIN_RENDER_LENGTH * MIN_RENDER_LENGTH) continue;
                float t = Mathf.Clamp01(Vector2.Dot(cursor - start, delta) / delta.sqrMagnitude);
                float along = (p - 1 + t) / (line.positionCount - 1f);
                float radius = _cursorRadius + Mathf.Abs(line.widthMultiplier * width.Evaluate(along)) * 0.5f;
                if ((cursor - start - delta * t).sqrMagnitude > radius * radius) continue;
                direction = cursor - start - delta * t;
                if (direction.sqrMagnitude < MIN_RENDER_LENGTH * MIN_RENDER_LENGTH) direction = new Vector2(-delta.y, delta.x);
                direction = direction.normalized;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// index 구간의 tail부터 head까지 현재 길이에 맞는 점 수로 선을 갱신한다.
    /// 직선은 두 점만 사용하고 짧은 파형은 점 수를 줄이며 유효 길이일 때만 표시한다.
    /// </summary>
    private void RenderSegment(int index, float tail, float head)
    {

        LineRenderer line = _lines[index];
        float length = Vector3.Distance(_starts[index], _ends[index]) * Mathf.Max(0f, head - tail);
        if (length < MIN_RENDER_LENGTH)
        {
            line.enabled = false;
            return;
        }

        int segments = _amplitude > 0f
            ? Mathf.Clamp(Mathf.FloorToInt(length / MIN_SAMPLE_SPACING), 1, _samples)
            : 1;
        line.positionCount = segments + 1;
        for (int p = 0; p <= segments; p++) line.SetPosition(p, WavePoint(index, Mathf.Lerp(tail, head, p / (float)segments)));
        // 스펙 046: 감정별 투명도를 선 색에 곱함
        if (_body != null)
        {
            line.startColor = new Color(_lineStartColor.r, _lineStartColor.g, _lineStartColor.b, _lineStartColor.a * _body.Alpha);
            line.endColor = new Color(_lineEndColor.r, _lineEndColor.g, _lineEndColor.b, _lineEndColor.a * _body.Alpha);
        }
        line.enabled = true;
    }

    /// <summary>
    /// 각 OriginPos의 현재 위치와 로컬 Y축으로 다음 구간의 시작과 끝을 정한다.
    /// 구간 길이와 로컬 X축을 사용하며 선을 숨기고 모든 정점, 목표 좌표와 타이머를 초기화한다.
    /// </summary>
    private void BeginSegment()
    {
        _elapsed = 0f;
        _appliedOffset = _body != null ? _body.Offset : Vector3.zero;
        for (int i = 0; i < ConnectionCount; i++)
        {
            Transform origin = OriginAt(i);
            // 스펙 046: 감정별 치우침과 왕복 폭 배율을 구간에 반영
            _starts[i] = origin.position + (_body != null ? _body.Offset : Vector3.zero);
            _ends[i] = _starts[i] + origin.up * (_segmentLength * (_body != null ? _body.SwingScale : 1f));
            _normals[i] = origin.right;
            LineRenderer line = _lines[i];
            line.enabled = false;
            line.positionCount = 2;
            line.SetPosition(0, _starts[i]);
            line.SetPosition(1, _starts[i]);
        }
    }

    /// <summary>
    /// index의 구간과 t 진행률로 전진 축에 수직인 sin 파형을 계산한다.
    /// 진폭과 파형 수를 사용하며 양 끝이 정확히 구간 끝점에 연결되는 월드 위치를 반환한다.
    /// </summary>
    private Vector3 WavePoint(int index, float t)
    {
        if (t <= 0f) return _starts[index];
        if (t >= 1f) return _ends[index];
        Vector3 delta = _ends[index] - _starts[index];
        float amplitude = Mathf.Min(_amplitude, delta.magnitude * 0.25f);
        return Vector3.Lerp(_starts[index], _ends[index], t) + _normals[index] * (Mathf.Sin(t * Mathf.PI * 2f * _waveCount) * amplitude);
    }

    /// <summary>
    /// 관찰 중단 또는 비활성화 시 생성된 선을 숨기고 이동 기준점을 복원한다.
    /// 저장된 초기 로컬 위치를 사용해 OriginPos와 반복 상태를 변경한다.
    /// </summary>
    private void StopDrawing()
    {
        if (_running)
        {
            for (int i = 0; i < ConnectionCount; i++)
                if (OriginAt(i) != null) OriginAt(i).localPosition = _homePositions[i];
        }
        foreach (LineRenderer line in _lines) if (line != null) line.enabled = false;
        _running = false;
        _elapsed = 0f;
        _contactCooldown = 0f;
    }
}
