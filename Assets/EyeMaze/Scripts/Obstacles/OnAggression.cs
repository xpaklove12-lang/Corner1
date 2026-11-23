using UnityEngine;

public class OnAggression : MonoBehaviour
{
    private const float HALF_TURN = 180f;

    private enum RotationPattern { HalfTurn, SineSweep }

    [Header("회전 방식")]
    [Tooltip("HalfTurn: 반바퀴 왕복, SineSweep: 사인파로 좌우 흔들기")]
    [SerializeField] private RotationPattern _pattern = RotationPattern.HalfTurn;

    [Header("반바퀴 왕복")]
    [Tooltip("Z축 초당 회전 각도. 0이면 정지")]
    [SerializeField, Min(0f)] private float _rotationSpeed = 180f;
    [Tooltip("반바퀴 회전과 원점 복귀 사이에 멈추는 시간(초). 0이면 멈추지 않음")]
    [SerializeField, Min(0f)] private float _periodSeconds = 2f;

    [Header("사인파 흔들기")]
    [Tooltip("기준 방향에서 좌우로 흔드는 최대 각도. 0이면 흔들지 않음")]
    [SerializeField, Min(0f)] private float _sweepAmplitude = 45f;
    [Tooltip("좌우로 한 번 왕복하는 시간(초)")]
    [SerializeField, Min(0.01f)] private float _sweepPeriod = 2f;

    private Quaternion _baseRotation;
    private float _angle;
    private float _targetAngle;
    private float _waitElapsed;
    private float _sweepElapsed;

    void Awake()
    {
        // 제작자가 배치한 회전을 원점으로 삼는다.
        _baseRotation = transform.localRotation;
    }

    void Update()
    {
        if (_pattern == RotationPattern.SineSweep) SweepAggression(Time.deltaTime);
        else RotateAggression(Time.deltaTime);
    }

    /// <summary>
    /// 목표 각도까지 Z축으로 돌고, 도달하면 주기만큼 멈춘 뒤 목표를 0도와 180도 사이에서 바꾼다.
    /// deltaTime, _rotationSpeed, _periodSeconds를 사용하며, _angle, _targetAngle, _waitElapsed와 localRotation을 바꾼다.
    /// </summary>
    private void RotateAggression(float deltaTime)
    {
        if (_angle != _targetAngle)
        {
            _angle = Mathf.MoveTowards(_angle, _targetAngle, _rotationSpeed * deltaTime);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, _angle);
            return;
        }

        _waitElapsed += deltaTime;
        if (_waitElapsed < _periodSeconds) return;

        _waitElapsed = 0f;
        _targetAngle = _targetAngle == 0f ? HALF_TURN : 0f;
    }

    /// <summary>
    /// 경과 시간에 따라 기준 회전에서 Z축으로 사인파 각도만큼 돌린다.
    /// deltaTime, _sweepAmplitude, _sweepPeriod를 사용하며, _sweepElapsed와 localRotation을 바꾼다.
    /// </summary>
    private void SweepAggression(float deltaTime)
    {
        _sweepElapsed = Mathf.Repeat(_sweepElapsed + deltaTime, _sweepPeriod);
        float angle = _sweepAmplitude * Mathf.Sin(2f * Mathf.PI * _sweepElapsed / _sweepPeriod);
        transform.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
