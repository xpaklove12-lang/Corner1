using UnityEngine;

using EyeMaze;

public class RotationObstaclesBasic : MonoBehaviour
{
    private const float ROTATION_STEP = 90f;

    [Header("회전")]
    [Tooltip("시계방향 초당 회전 각도. 0이면 정지")]
    [SerializeField, Min(0f)] private float _rotationSpeed = 90f;
    [Tooltip("90도 회전할 때마다 쉬는 시간(초). 0이면 쉬지 않음")]
    [SerializeField, Min(0f)] private float _pauseSeconds = 1.5f;

    // 스펙 046 FR-021: 왕복 장애물과 같은 피격 판정 추가
    [Header("피격")]
    [SerializeField] private EyeMazeDirector _director;
    [SerializeField, Range(0f, 1f)] private float _burdenAmount = 0.02f;

    private float _rotatedAngle;
    private float _pauseElapsed;
    private bool _isPaused;
    // 스펙 046: 감정별 값을 받는 컴포넌트와 원래 로컬 위치 추가
    private MazeObstacleBody _body;
    private Vector3 _homePos;

    void Start()
    {
        _body = GetComponent<MazeObstacleBody>();
        _homePos = transform.localPosition;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 스펙 046: 감정별 속도 배율과 치우침을 반영하고 시선 접촉을 확인함
        if (_body != null)
        {
            dt *= _body.SpeedScale;
            Vector3 offset = _body.Offset;
            if (transform.parent != null) offset = transform.parent.InverseTransformVector(offset);
            transform.localPosition = _homePos + offset;
            CheckContact();
        }

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

    /// <summary>
    /// 스펙 046 FR-021: 관찰 중 시선과 장애물 사이 거리를 MazeObstacleBody의 판정 반지름과 비교한다.
    /// 접촉하면 부담감 증가량과 밀려날 방향으로 기존 피격을 요청한다.
    /// </summary>
    private void CheckContact()
    {
        if (_director == null || !_director.CanRunHazards || _body.Passable) return;

        Vector2 direction = _director.Cursor.transform.position - transform.position;
        if (direction.sqrMagnitude > _body.HitRadius * _body.HitRadius) return;
        if (direction.sqrMagnitude < 0.000001f) direction = transform.up;

        _director.AddObstacleBurden(_burdenAmount, direction.normalized);
    }
}
