using UnityEngine;
using UnityEngine.Splines;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 040: 스플라인을 따라 LineRenderer 점을 채워 곡선을 그린다. 에디트 모드에서도 스플라인을 고치면 바로 따라온다.
    /// </summary>
    [ExecuteAlways]
    public class SplineLine : MonoBehaviour
    {
        private const int MIN_KNOTS = 2;

        [Header("연결")]
        [Tooltip("따라 그릴 스플라인")]
        [SerializeField] private SplineContainer _container;
        [Tooltip("점을 채울 선. 두께·색·재질은 이 선의 값을 그대로 쓴다")]
        [SerializeField] private LineRenderer _line;

        [Header("모양")]
        [Tooltip("컨테이너 안 스플라인 번호")]
        [SerializeField, Min(0)] private int _splineIndex;
        [Tooltip("점 사이 곡선 구간 하나에 찍는 점 수. 클수록 매끄럽다")]
        [SerializeField, Range(1, 64)] private int _samplesPerCurve = 16;

        private bool _dirty = true;
        private Matrix4x4 _lastRelative;

        void OnEnable()
        {
            Spline.Changed += OnSplineChanged;
            _dirty = true;
        }

        void OnDisable()
        {
            Spline.Changed -= OnSplineChanged;
        }

        void OnValidate()
        {
            _dirty = true;
        }

        void Update()
        {
            if (_container == null || _line == null) return;
            // 컨테이너와 선의 상대 위치가 바뀌었을 때만 다시 그린다.
            if (!_dirty && RelativeMatrix() == _lastRelative) return;
            Rebuild();
        }

        /// <summary>
        /// 스플라인이 바뀌었다는 알림을 받아, 따라 그리는 스플라인이면 바로 다시 그린다.
        /// spline, knotIndex, modification을 사용하며, 선의 점을 바꾼다.
        /// </summary>
        private void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            // 에디트 모드에서는 Update가 늦게 올 수 있어 알림을 받은 즉시 그린다.
            if (_line != null && TryGetSpline(out Spline mine) && mine == spline) Rebuild();
        }

        /// <summary>
        /// 컨테이너에서 _splineIndex번 스플라인을 꺼낸다.
        /// _container와 _splineIndex를 사용하며, 있으면 spline에 담고 true를 반환한다.
        /// </summary>
        private bool TryGetSpline(out Spline spline)
        {
            spline = null;
            if (_container == null || _splineIndex >= _container.Splines.Count) return false;
            spline = _container.Splines[_splineIndex];
            return spline != null;
        }

        /// <summary>
        /// 컨테이너 로컬 좌표를 선 로컬 좌표로 바꾸는 변환을 구한다.
        /// 두 Transform을 사용하며, 변환 행렬을 반환한다.
        /// </summary>
        private Matrix4x4 RelativeMatrix()
        {
            return _line.transform.worldToLocalMatrix * _container.transform.localToWorldMatrix;
        }

        /// <summary>
        /// 스플라인을 곡선 구간마다 샘플링해 선 로컬 좌표로 바꾼 뒤 LineRenderer 점과 loop를 채운다.
        /// 스플라인, _samplesPerCurve와 두 Transform을 사용하며, _line의 점·loop·좌표 공간과 _dirty, _lastRelative를 바꾼다.
        /// </summary>
        private void Rebuild()
        {
            _dirty = false;
            _lastRelative = RelativeMatrix();
            if (!TryGetSpline(out Spline spline) || spline.Count < MIN_KNOTS) return;

            bool closed = spline.Closed;
            int curves = closed ? spline.Count : spline.Count - 1;
            // 닫힌 곡선은 loop로 이어지므로 마지막 점(시작점과 같은 곳)을 찍지 않는다.
            int count = curves * _samplesPerCurve + (closed ? 0 : 1);
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(curves * _samplesPerCurve);
                Vector3 local = (Vector3)spline.EvaluatePosition(t);
                points[i] = _lastRelative.MultiplyPoint3x4(local);
            }

            _line.useWorldSpace = false;
            _line.loop = closed;
            _line.positionCount = count;
            _line.SetPositions(points);
        }
    }
}
