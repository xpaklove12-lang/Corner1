using System.Collections;

using UnityEngine;

using EyeMaze;

public class MoveObstaclesBasic : MonoBehaviour
{
    [Header("이동")]
    [SerializeField, Min(0.01f)] private float _moveTime = 1f;
    [SerializeField] private float _moveDistance = 1f;

    [Header("피격")]
    [SerializeField] private EyeMazeDirector _director;
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _burdenAmount = 0.02f;

    // 스펙 046: 감정별 값을 받는 컴포넌트와 왕복 시작 위치 추가
    private MazeObstacleBody _body;
    private Vector3 _startPos;
    // 스펙 046 FR-040: 이미 피격을 준 접촉이 이어지는 중인지 기억
    private bool _touching;

    void Start()
    {
        _body = GetComponent<MazeObstacleBody>();
        StartCoroutine(MoveObstacle());
    }

    void Update()
    {
        CheckContact();
    }

    /// <summary>
    /// 초기 로컬 위치와 현재 로컬 Y축 방향의 목표 사이를 반복 이동한다.
    /// 월드 이동 거리와 부모 좌표계를 사용하며 transform.localPosition을 변경한다.
    /// </summary>
    private IEnumerator MoveObstacle()
    {
        Vector3 startPos = transform.localPosition;
        _startPos = startPos;
        Vector3 movement = transform.up * _moveDistance;
        if (transform.parent != null) movement = transform.parent.InverseTransformVector(movement);
        Vector3 targetPos = startPos + movement;

        while (true)
        {
            yield return MoveTo(startPos, targetPos);
            yield return MoveTo(targetPos, startPos);
        }
    }

    /// <summary>
    /// 부모 좌표계의 from에서 to까지 _moveTime 동안 보간 이동한다.
    /// 두 로컬 위치를 사용하며 transform.localPosition을 변경한다.
    /// </summary>
    private IEnumerator MoveTo(Vector3 from, Vector3 to)
    {
        float duration = Mathf.Max(0.01f, _moveTime);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 스펙 046: 감정별 속도 배율을 곱함
            elapsed += Time.deltaTime * (_body != null ? _body.SpeedScale : 1f);
            float progress = Mathf.Clamp01(elapsed / duration);

            transform.localPosition = Placed(Vector3.Lerp(from, to, progress));

            yield return null;
        }

        transform.localPosition = Placed(to);
    }

    /// <summary>
    /// 스펙 046: 왕복 위치에 감정별 왕복 폭 배율과 치우침을 반영한다. MazeObstacleBody가 없으면 그대로 돌려준다.
    /// 부모 좌표계의 position과 _startPos를 사용하며, 실제로 놓을 로컬 위치를 반환한다.
    /// </summary>
    private Vector3 Placed(Vector3 position)
    {
        if (_body == null) return position;
        Vector3 offset = _body.Offset;
        if (transform.parent != null) offset = transform.parent.InverseTransformVector(offset);
        return _startPos + (position - _startPos) * _body.SwingScale + offset;
    }

    /// <summary>
    /// 관찰 중 시선과 장애물 사이 거리를 접촉 반지름과 비교한다.
    /// 접촉하면 부담감 증가량과 밀려날 방향으로 기존 피격을 요청한다.
    /// </summary>
    private void CheckContact()
    {
        if (!_director.CanRunHazards) return;
        Vector2 direction =
            _director.Cursor.transform.position - transform.position;

        // 스펙 046 FR-040: 시선이 벗어났다가 다시 닿아야 또 피격을 줌
        if (direction.sqrMagnitude > _hitRadius * _hitRadius) { _touching = false; return; }
        // 스펙 046: 슬픔과 침잠에서는 닿아도 피격하지 않음
        if (_touching || (_body != null && _body.Passable)) return;

        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = transform.up;
        }

        _touching = _director.AddObstacleBurden(
            _burdenAmount,
            direction.normalized);
    }
}
