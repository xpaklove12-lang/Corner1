using UnityEngine;

public class RotationObstaclesBasic : MonoBehaviour
{
    private const float ROTATION_STEP = 90f;

    [Header("회전")]
    [Tooltip("시계방향 초당 회전 각도. 0이면 정지")]
    [SerializeField, Min(0f)] private float _rotationSpeed = 90f;
    [Tooltip("90도 회전할 때마다 쉬는 시간(초). 0이면 쉬지 않음")]
    [SerializeField, Min(0f)] private float _pauseSeconds = 1.5f;

    private float _rotatedAngle;
    private float _pauseElapsed;
    private bool _isPaused;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (_isPaused)
        {
            _pauseElapsed += dt;
            if (_pauseElapsed < Mathf.Max(0f, _pauseSeconds)) return;

            _isPaused = false;
            _pauseElapsed = 0f;
            return;
        }

        float angle = Mathf.Min(Mathf.Max(0f, _rotationSpeed) * dt, ROTATION_STEP - _rotatedAngle);

        transform.Rotate(0f, 0f, -angle, Space.Self);
        _rotatedAngle += angle;

        if (_rotatedAngle < ROTATION_STEP) return;

        _rotatedAngle = 0f;
        _pauseElapsed = 0f;
        _isPaused = _pauseSeconds > 0f;
    }
}
