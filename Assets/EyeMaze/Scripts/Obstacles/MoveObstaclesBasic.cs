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

    void Start()
    {
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
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            transform.localPosition = Vector3.Lerp(from, to, progress);

            yield return null;
        }

        transform.localPosition = to;
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

        if (direction.sqrMagnitude > _hitRadius * _hitRadius) return;

        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = transform.up;
        }

        _director.AddObstacleBurden(
            _burdenAmount,
            direction.normalized);
    }
}
