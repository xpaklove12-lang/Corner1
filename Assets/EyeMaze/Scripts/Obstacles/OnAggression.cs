using UnityEngine;

public class OnAggression : MonoBehaviour
{
    private const float HALF_TURN = 180f;

    [Header("회전")]
    [Tooltip("Y축 초당 회전 각도. 0이면 정지")]
    [SerializeField, Min(0f)] private float _rotationSpeed = 180f;
    [Tooltip("반바퀴 회전과 원점 복귀 사이에 멈추는 시간(초). 0이면 멈추지 않음")]
    [SerializeField, Min(0f)] private float _periodSeconds = 2f;

    private Quaternion _baseRotation;
    private float _angle;
    private float _targetAngle;
    private float _waitElapsed;

    void Awake()
    {
        // 제작자가 배치한 회전을 원점으로 삼는다.
        _baseRotation = transform.localRotation;
    }

    void Update()
    {
        RotateAggression(Time.deltaTime);
    }

    /// <summary>
    /// 목표 각도까지 Y축으로 돌고, 도달하면 주기만큼 멈춘 뒤 목표를 0도와 180도 사이에서 바꾼다.
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
}
