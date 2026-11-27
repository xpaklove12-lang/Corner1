using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 칸 카메라를 중심점 둘레로 돌리며 거리를 바꾸는 칸 연출.
    /// 좌우 각도, 거리, 중심점이 칸 화면에 놓이는 위치를 같은 곡선으로 섞어,
    /// 카메라가 옆으로 돌고 뒤로 빠지는 동안 중심점이 칸 화면에서 곧은 선을 따라 움직이게 한다.
    /// </summary>
    public class CuttoonOrbitTween : CuttoonEffect
    {
        [Header("대상")]
        [SerializeField] private Camera _camera;
        [Tooltip("카메라 부모 기준 로컬 위치로 적은 중심점")]
        [SerializeField] private Vector3 _pivot = Vector3.zero;

        [Header("시간")]
        [SerializeField] private float _duration = 1f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("각도")]
        [Tooltip("내려다보는 로컬 X 회전(도). 연출 동안 바뀌지 않는다")]
        [SerializeField] private float _pitch = 0f;
        [Tooltip("시작 로컬 Y 회전(도)")]
        [SerializeField] private float _fromYaw = 0f;
        [Tooltip("끝 로컬 Y 회전(도)")]
        [SerializeField] private float _toYaw = 0f;

        [Header("거리")]
        [Tooltip("시작할 때 카메라가 바라보는 방향으로 잰 중심점까지의 거리(미터)")]
        [SerializeField] private float _fromDistance = 1f;
        [Tooltip("끝날 때 카메라가 바라보는 방향으로 잰 중심점까지의 거리(미터)")]
        [SerializeField] private float _toDistance = 1f;

        [Header("화면 위치")]
        [Tooltip("시작할 때 중심점이 놓이는 칸 화면 정규화 위치(0~1)")]
        [SerializeField] private Vector2 _fromView = new Vector2(0.5f, 0.5f);
        [Tooltip("끝날 때 중심점이 놓이는 칸 화면 정규화 위치(0~1)")]
        [SerializeField] private Vector2 _toView = new Vector2(0.5f, 0.5f);

        protected override float Duration => _duration;

        protected override void OnValidate()
        {
            base.OnValidate();
            _duration = Mathf.Max(0f, _duration);
            _fromDistance = Mathf.Max(0.01f, _fromDistance);
            _toDistance = Mathf.Max(0.01f, _toDistance);
        }

        /// <summary>
        /// 진행 정도 t를 _curve로 바꾼 값만큼 좌우 각도, 거리, 화면 위치를 섞고,
        /// 중심점이 그 화면 위치에 오도록 _camera의 위치와 회전을 정한다.
        /// </summary>
        protected override void Apply(float t)
        {
            float eased = _curve.Evaluate(t);
            float yaw = Mathf.LerpUnclamped(_fromYaw, _toYaw, eased);
            float distance = Mathf.LerpUnclamped(_fromDistance, _toDistance, eased);
            Vector2 view = Vector2.LerpUnclamped(_fromView, _toView, eased);

            Transform cameraTransform = _camera.transform;
            Transform space = cameraTransform.parent;
            Quaternion rotation = space.rotation * Quaternion.Euler(_pitch, yaw, 0f);
            float halfHeight = distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * _camera.aspect;

            // 중심점을 화면 위치에 두려면 카메라를 그 반대 방향으로 옮긴다.
            Vector3 position = space.TransformPoint(_pivot)
                - rotation * Vector3.forward * distance
                - rotation * Vector3.right * ((view.x - 0.5f) * 2f * halfWidth)
                - rotation * Vector3.up * ((view.y - 0.5f) * 2f * halfHeight);
            cameraTransform.SetPositionAndRotation(position, rotation);
        }
    }
}
