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
        // 스펙 006: 눈꺼풀 덮개 그리기 상수 추가
        private const int COVER_COLUMNS = 33;
        private const int COVER_SORTING_ORDER = 21;
        private const float COVER_MARGIN = 0.05f;
        // 스펙 011: 눈물 메시 그리기 상수 추가
        private const int TEAR_SORTING_ORDER = 20;
        private const float TEAR_WAVE_FREQUENCY = 9f;

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
        // 스펙 006: 눈을 감을 때 덮는 색 추가
        [Tooltip("눈을 감을 때 눈 안쪽을 덮는 색")]
        [SerializeField] private Color _lidCoverColor = Color.black;
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
        [Tooltip("초점. 0이면 어둡고 테두리가 번져 보인다")]
        [SerializeField, FormerlySerializedAs("focus"), Range(0f, 1f)] private float _focus = 1f;

        // 스펙 011: 눈물 모양과 색 설정 추가
        [Header("눈물")]
        [SerializeField] private Color _tearColor = new Color(0.62f, 0.8f, 1f, 0.45f);
        [Tooltip("가득 찼을 때 눈 높이 대비 수위")]
        [SerializeField, Range(0f, 1f)] private float _tearMaxHeight = 0.55f;
        [Tooltip("눈물 수면의 일렁임 높이")]
        [SerializeField] private float _tearWaveHeight = 0.06f;
        [Tooltip("눈물 수면의 일렁임 빠르기")]
        [SerializeField] private float _tearWaveSpeed = 2f;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("sclera")] private LineRenderer _sclera;
        [SerializeField, FormerlySerializedAs("irisDisc")] private LineRenderer _irisDisc;
        [SerializeField, FormerlySerializedAs("irisRim")] private LineRenderer _irisRim;
        [SerializeField, FormerlySerializedAs("upperLid")] private LineRenderer _upperLid;
        [SerializeField, FormerlySerializedAs("lashes")] private LineRenderer[] _lashes = new LineRenderer[0];

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 스펙 006: 눈꺼풀 닫힘 정도와 덮개 메시 상태 추가
        [NonSerialized] private float _closed;
        [NonSerialized] private Mesh _coverMesh;
        [NonSerialized] private MeshRenderer _coverRenderer;
        [NonSerialized] private Vector3[] _coverVertices;
        [NonSerialized] private Color[] _coverColors;
        // 스펙 006: 깜빡임 중 힙 할당을 막는 재사용 배열과 눈꺼풀 캐시 키 추가
        [NonSerialized] private int _drawnLidKey = int.MinValue;
        [NonSerialized] private Vector3[] _lidPoints;
        [NonSerialized] private readonly Vector3[] _lashPoints = new Vector3[2];
        // 스펙 011: 눈물 수위와 메시 상태 추가
        [NonSerialized] private float _tearLevel;
        [NonSerialized] private Mesh _tearMesh;
        [NonSerialized] private MeshRenderer _tearRenderer;
        [NonSerialized] private Vector3[] _tearVertices;
        [NonSerialized] private Color[] _tearColors;

        public float Focus { get => _focus; set => _focus = value; }

        /// <summary>눈물 수위 (0 = 없음, 1 = 가득)</summary>
        public float TearLevel { get => _tearLevel; set => _tearLevel = Mathf.Clamp01(value); }

        /// <summary>눈꺼풀이 닫힌 정도 (0 = 뜸, 1 = 완전히 감김)</summary>
        public float Closed { get => _closed; set => _closed = Mathf.Clamp01(value); }

        /// <summary>
        /// 윗눈꺼풀이 지나는 점을 계산한다. 눈이 감길수록 가운데 선으로 내려온다.
        /// t(0 = 왼쪽 눈꼬리, 1 = 오른쪽 눈꼬리)와 _closed를 사용하며, 로컬 좌표를 반환한다.
        /// </summary>
        public Vector3 UpperEdge(float t)
        {
            float x = Mathf.Lerp(-_scleraWidth * 0.5f, _scleraWidth * 0.5f, t);
            float y = _scleraHeight * 0.5f * _lens.Evaluate(Mathf.Clamp01(t)) * (1f - _closed);
            return new Vector3(x, y, 0f);
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            // 스펙 011: 수면이 일렁이므로 눈 모양 캐시와 상관없이 매 프레임 그린다.
            DrawTear();
            float irisRadius = _maze != null ? _maze.OuterRadius : 3f;
            int key = HashCode.Combine(
                HashCode.Combine(_scleraWidth, _scleraHeight, _scleraColor, _irisColor, _rimColor, irisRadius, _focus, _irisDiscPadding),
                HashCode.Combine(_rimWidth, _lidColor, _lidWidth, _lidOvershoot, _lashCount, _lashLength, _lashWidth, _lashSpread),
                _lashes != null ? _lashes.Length : 0);
            // 스펙 006: 깜빡임 중에는 눈꺼풀 부분만 다시 그린다.
            bool shapeChanged = key != _drawnKey;
            int lidKey = HashCode.Combine(key, _closed, _lidCoverColor);
            if (!shapeChanged && lidKey == _drawnLidKey) return;
            _drawnKey = key;
            _drawnLidKey = lidKey;

            float f = _focus * _focus * (3f - 2f * _focus);
            var dimSclera = Color.Lerp(_scleraColor * 0.45f, _scleraColor, f); dimSclera.a = _scleraColor.a;
            var dimIris = Color.Lerp(new Color(0.22f, 0.2f, 0.19f, _irisColor.a), _irisColor, f);
            var softRim = _rimColor; softRim.a *= Mathf.Lerp(0.25f, 1f, f);
            var lid = Color.Lerp(_lidColor * 0.45f, _lidColor, f); lid.a = _lidColor.a;

            if (shapeChanged && _sclera != null)
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
            if (shapeChanged && _irisDisc != null)
            {
                Shapes.SetBlob(_irisDisc, irisRadius * 2f + _irisDiscPadding);
                Shapes.SetColor(_irisDisc, dimIris);
                _irisDisc.sortingOrder = -4;
            }
            if (shapeChanged && _irisRim != null)
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
                if (_lidPoints == null) _lidPoints = new Vector3[LID_POINTS + 2];
                Vector3[] pts = _lidPoints;
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
                    _lashPoints[0] = p;
                    _lashPoints[1] = p + normal * len;
                    Shapes.SetPoints(l, _lashPoints, false);
                    l.numCapVertices = 2;
                    l.widthMultiplier = _lashWidth;
                    Shapes.SetColor(l, lid);
                    l.sortingOrder = 22;
                }
            }

            // 스펙 006: 흰자와 미로를 덮는 눈꺼풀 그리기
            DrawCover();
        }

        /// <summary>
        /// 위아래 눈꺼풀이 덮은 영역을 메시로 그린다. Play 중에만 그리고 눈을 뜨고 있으면 숨긴다.
        /// _closed, 흰자 크기, _lidCoverColor를 사용하며, 덮개 메시의 정점과 표시 상태를 바꾼다.
        /// </summary>
        private void DrawCover()
        {
            if (!Application.isPlaying || _sclera == null) return;
            if (_coverMesh == null) CreateCover();
            _coverRenderer.enabled = _closed > 0f;
            if (!_coverRenderer.enabled) return;

            float halfWidth = _scleraWidth * 0.5f;
            float halfHeight = _scleraHeight * 0.5f;
            float open = 1f - _closed;
            for (int i = 0; i < COVER_COLUMNS; i++)
            {
                float t = i / (COVER_COLUMNS - 1f);
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                float edge = halfHeight * _lens.Evaluate(t);
                int v = i * 4;
                _coverVertices[v] = new Vector3(x, edge + COVER_MARGIN, 0f);
                _coverVertices[v + 1] = new Vector3(x, edge * open, 0f);
                _coverVertices[v + 2] = new Vector3(x, -edge * open, 0f);
                _coverVertices[v + 3] = new Vector3(x, -edge - COVER_MARGIN, 0f);
                for (int c = 0; c < 4; c++) _coverColors[v + c] = _lidCoverColor;
            }
            _coverMesh.vertices = _coverVertices;
            _coverMesh.colors = _coverColors;
            _coverMesh.RecalculateBounds();
        }

        /// <summary>
        /// 눈꺼풀 덮개 오브젝트와 메시를 Play 중에 한 번 만든다. 씬에는 저장되지 않는다.
        /// _sclera의 재질을 사용하며, 덮개 메시와 렌더러, 정점 배열을 만든다.
        /// </summary>
        private void CreateCover()
        {
            var cover = new GameObject("눈꺼풀 덮개");
            cover.transform.SetParent(transform, false);

            _coverVertices = new Vector3[COVER_COLUMNS * 4];
            _coverColors = new Color[COVER_COLUMNS * 4];
            var triangles = new int[(COVER_COLUMNS - 1) * 12];
            for (int i = 0; i < COVER_COLUMNS - 1; i++)
            {
                int v = i * 4;
                int n = i * 12;
                triangles[n] = v;
                triangles[n + 1] = v + 4;
                triangles[n + 2] = v + 5;
                triangles[n + 3] = v;
                triangles[n + 4] = v + 5;
                triangles[n + 5] = v + 1;
                triangles[n + 6] = v + 2;
                triangles[n + 7] = v + 6;
                triangles[n + 8] = v + 7;
                triangles[n + 9] = v + 2;
                triangles[n + 10] = v + 7;
                triangles[n + 11] = v + 3;
            }

            _coverMesh = new Mesh { name = "눈꺼풀 덮개", vertices = _coverVertices, triangles = triangles };
            cover.AddComponent<MeshFilter>().sharedMesh = _coverMesh;
            _coverRenderer = cover.AddComponent<MeshRenderer>();
            _coverRenderer.sharedMaterial = _sclera.sharedMaterial;
            _coverRenderer.sortingOrder = COVER_SORTING_ORDER;
        }

        /// <summary>
        /// 눈 아래쪽부터 차오르는 눈물을 흰자 모양에 맞춰 메시로 그린다. Play 중에만 그리고 수위가 0이면 숨긴다.
        /// _tearLevel, 흰자 크기, 눈물 설정값을 사용하며, 눈물 메시의 정점과 표시 상태를 바꾼다.
        /// </summary>
        private void DrawTear()
        {
            if (!Application.isPlaying || _sclera == null) return;
            if (_tearMesh == null)
            {
                if (_tearLevel <= 0f) return;
                CreateTear();
            }
            _tearRenderer.enabled = _tearLevel > 0f;
            if (!_tearRenderer.enabled) return;

            float halfWidth = _scleraWidth * 0.5f;
            float halfHeight = _scleraHeight * 0.5f;
            float surface = -halfHeight + _scleraHeight * _tearMaxHeight * _tearLevel;
            float phase = Time.time * _tearWaveSpeed;
            for (int i = 0; i < COVER_COLUMNS; i++)
            {
                float t = i / (COVER_COLUMNS - 1f);
                float edge = halfHeight * _lens.Evaluate(t);
                float wave = Mathf.Sin(t * TEAR_WAVE_FREQUENCY + phase) * _tearWaveHeight * _tearLevel;
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                _tearVertices[i * 2] = new Vector3(x, Mathf.Clamp(surface + wave, -edge, edge), 0f);
                _tearVertices[i * 2 + 1] = new Vector3(x, -edge, 0f);
                _tearColors[i * 2] = _tearColor;
                _tearColors[i * 2 + 1] = _tearColor;
            }
            _tearMesh.vertices = _tearVertices;
            _tearMesh.colors = _tearColors;
            _tearMesh.RecalculateBounds();
        }

        /// <summary>
        /// 눈물 오브젝트와 메시를 Play 중에 한 번 만든다. 씬에는 저장되지 않는다.
        /// _sclera의 재질을 사용하며, 눈물 메시와 렌더러, 정점 배열을 만든다.
        /// </summary>
        private void CreateTear()
        {
            var tear = new GameObject("눈물");
            tear.transform.SetParent(transform, false);

            _tearVertices = new Vector3[COVER_COLUMNS * 2];
            _tearColors = new Color[COVER_COLUMNS * 2];
            var triangles = new int[(COVER_COLUMNS - 1) * 6];
            for (int i = 0; i < COVER_COLUMNS - 1; i++)
            {
                int v = i * 2;
                int n = i * 6;
                triangles[n] = v;
                triangles[n + 1] = v + 2;
                triangles[n + 2] = v + 3;
                triangles[n + 3] = v;
                triangles[n + 4] = v + 3;
                triangles[n + 5] = v + 1;
            }

            _tearMesh = new Mesh { name = "눈물", vertices = _tearVertices, triangles = triangles };
            tear.AddComponent<MeshFilter>().sharedMesh = _tearMesh;
            _tearRenderer = tear.AddComponent<MeshRenderer>();
            _tearRenderer.sharedMaterial = _sclera.sharedMaterial;
            _tearRenderer.sortingOrder = TEAR_SORTING_ORDER;
        }
    }
}
