using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 눈의 바깥 모양: 흰자(끝이 뾰족한 아몬드형), 홍채 바탕, 홍채 테두리, 윗눈꺼풀 선과 속눈썹.
    /// 홍채 미로가 통째로 흰자 안에 들어가도록 흰자 높이는 홍채 지름보다 조금 크게 둔다.
    /// 크기·색·속눈썹 수는 인스펙터에서 바로 바꿀 수 있다.
    /// </summary>
    [ExecuteAlways]
    public class EyeVisual : MonoBehaviour
    {
        private static readonly AnimationCurve _lens = new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -3f, -3f));

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("maze")] private IrisMaze _maze;

        [Header("흰자")]
        [SerializeField, FormerlySerializedAs("scleraWidth")] private float _scleraWidth = 10f;
        [SerializeField, FormerlySerializedAs("scleraHeight")] private float _scleraHeight = 7f;
        [SerializeField, FormerlySerializedAs("scleraColor")] private Color _scleraColor = Color.white;

        [Header("홍채")]
        [SerializeField, FormerlySerializedAs("irisColor")] private Color _irisColor = Color.black;
        [SerializeField, FormerlySerializedAs("rimColor")] private Color _rimColor = Color.black;
        [Tooltip("홍채 바탕을 미로보다 얼마나 크게 깔지 (지름에 더함)")]
        [SerializeField, FormerlySerializedAs("irisDiscPadding")] private float _irisDiscPadding = 0.2f;
        [SerializeField, FormerlySerializedAs("rimWidth")] private float _rimWidth = 0.12f;

        [Header("눈꺼풀 / 속눈썹")]
        [SerializeField, FormerlySerializedAs("lidColor")] private Color _lidColor = Color.white;
        [Tooltip("윗눈꺼풀 선 두께")]
        [SerializeField, FormerlySerializedAs("lidWidth")] private float _lidWidth = 0.22f;
        [Tooltip("눈꼬리 쪽으로 눈꺼풀 선이 흰자보다 더 뻗는 정도")]
        [SerializeField, FormerlySerializedAs("lidOvershoot")] private float _lidOvershoot = 0.35f;
        [SerializeField, FormerlySerializedAs("lashCount"), Range(0, 12)] private int _lashCount = 7;
        [SerializeField, FormerlySerializedAs("lashLength")] private float _lashLength = 0.6f;
        [SerializeField, FormerlySerializedAs("lashWidth")] private float _lashWidth = 0.13f;
        [Tooltip("속눈썹이 바깥쪽으로 벌어지는 각도")]
        [SerializeField, FormerlySerializedAs("lashSpread")] private float _lashSpread = 35f;

        [Header("초점")]
        [Tooltip("초점. 0이면 어둡고 테두리가 번져 보인다. 태엽이 조절한다")]
        [SerializeField, FormerlySerializedAs("focus"), Range(0f, 1f)] private float _focus = 1f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("sclera")] private LineRenderer _sclera;
        [SerializeField, FormerlySerializedAs("irisDisc")] private LineRenderer _irisDisc;
        [SerializeField, FormerlySerializedAs("irisRim")] private LineRenderer _irisRim;
        [SerializeField, FormerlySerializedAs("upperLid")] private LineRenderer _upperLid;
        [SerializeField, FormerlySerializedAs("lashes")] private LineRenderer[] _lashes = new LineRenderer[0];

        [NonSerialized] private int _drawnKey = int.MinValue;

        public float Focus { get => _focus; set => _focus = value; }

        /// <summary>
        /// 흰자 위쪽 가장자리의 점을 계산한다.
        /// t(0 = 왼쪽 눈꼬리, 1 = 오른쪽 눈꼬리)를 사용하며, 로컬 좌표를 반환한다.
        /// </summary>
        public Vector3 UpperEdge(float t)
        {
            float x = Mathf.Lerp(-_scleraWidth * 0.5f, _scleraWidth * 0.5f, t);
            float y = _scleraHeight * 0.5f * _lens.Evaluate(Mathf.Clamp01(t));
            return new Vector3(x, y, 0f);
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            float irisRadius = _maze != null ? _maze.OuterRadius : 3f;
            int key = HashCode.Combine(
                HashCode.Combine(_scleraWidth, _scleraHeight, _scleraColor, _irisColor, _rimColor, irisRadius, _focus, _irisDiscPadding),
                HashCode.Combine(_rimWidth, _lidColor, _lidWidth, _lidOvershoot, _lashCount, _lashLength, _lashWidth, _lashSpread),
                _lashes != null ? _lashes.Length : 0);
            if (key == _drawnKey) return;
            _drawnKey = key;

            float f = _focus * _focus * (3f - 2f * _focus);
            var dimSclera = Color.Lerp(_scleraColor * 0.45f, _scleraColor, f); dimSclera.a = _scleraColor.a;
            var dimIris = Color.Lerp(new Color(0.22f, 0.2f, 0.19f, _irisColor.a), _irisColor, f);
            var softRim = _rimColor; softRim.a *= Mathf.Lerp(0.25f, 1f, f);
            var lid = Color.Lerp(_lidColor * 0.45f, _lidColor, f); lid.a = _lidColor.a;

            if (_sclera != null)
            {
                const int SCLERA_POINTS = 33;
                var pts = new Vector3[SCLERA_POINTS];
                for (int i = 0; i < SCLERA_POINTS; i++) pts[i] = new Vector3(Mathf.Lerp(-_scleraWidth * 0.5f, _scleraWidth * 0.5f, i / (SCLERA_POINTS - 1f)), 0f, 0f);
                Shapes.SetPoints(_sclera, pts, false);
                _sclera.numCapVertices = 0;
                _sclera.widthCurve = _lens;
                _sclera.widthMultiplier = _scleraHeight;
                Shapes.SetColor(_sclera, dimSclera);
                _sclera.sortingOrder = -5;
            }
            if (_irisDisc != null)
            {
                Shapes.SetBlob(_irisDisc, irisRadius * 2f + _irisDiscPadding);
                Shapes.SetColor(_irisDisc, dimIris);
                _irisDisc.sortingOrder = -4;
            }
            if (_irisRim != null)
            {
                Shapes.SetCircle(_irisRim, irisRadius + _irisDiscPadding * 0.5f, 72);
                _irisRim.widthMultiplier = _rimWidth + (1f - f) * 0.4f;
                Shapes.SetColor(_irisRim, softRim);
                _irisRim.sortingOrder = -3;
            }

            // 윗눈꺼풀은 흰자 위 가장자리를 따라 굵게 긋고, 눈꼬리 쪽으로 살짝 더 뻗는다.
            if (_upperLid != null)
            {
                const int LID_POINTS = 41;
                var pts = new Vector3[LID_POINTS + 2];
                pts[0] = UpperEdge(0f) + new Vector3(-_lidOvershoot, -_lidOvershoot * 0.15f, 0f);
                for (int i = 0; i < LID_POINTS; i++) pts[i + 1] = UpperEdge(i / (LID_POINTS - 1f));
                pts[LID_POINTS + 1] = UpperEdge(1f) + new Vector3(_lidOvershoot, -_lidOvershoot * 0.15f, 0f);
                Shapes.SetPoints(_upperLid, pts, false);
                _upperLid.numCapVertices = 2;
                _upperLid.widthMultiplier = _lidWidth;
                Shapes.SetColor(_upperLid, lid);
                _upperLid.sortingOrder = 22;
            }

            // 속눈썹은 가운데일수록 길게, 눈꼬리 쪽일수록 바깥으로 벌어지게 그린다.
            if (_lashes != null)
            {
                int count = Mathf.Min(_lashCount, _lashes.Length);
                for (int i = 0; i < _lashes.Length; i++)
                {
                    var l = _lashes[i];
                    if (l == null) continue;
                    bool show = i < count;
                    if (l.enabled != show) l.enabled = show;
                    if (!show) continue;

                    float t = count == 1 ? 0.5f : Mathf.Lerp(0.2f, 0.8f, i / (count - 1f));
                    var p = UpperEdge(t);
                    var tangent = (UpperEdge(t + 0.01f) - UpperEdge(t - 0.01f)).normalized;
                    var normal = new Vector3(-tangent.y, tangent.x, 0f); // 위쪽(바깥)
                    float side = (t - 0.5f) * 2f; // -1 왼쪽 ~ +1 오른쪽
                    normal = Quaternion.Euler(0f, 0f, -side * _lashSpread) * normal;
                    float len = _lashLength * (0.75f + 0.25f * (1f - Mathf.Abs(side)));
                    Shapes.SetPoints(l, new[] { p, p + normal * len }, false);
                    l.numCapVertices = 2;
                    l.widthMultiplier = _lashWidth;
                    Shapes.SetColor(l, lid);
                    l.sortingOrder = 22;
                }
            }
        }
    }
}
