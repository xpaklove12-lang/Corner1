using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 동공 한가운데의 '가장 반짝이는 파편'. 미로를 지나 심연(동공)에 들어온 뒤 여기에 닿으면 그 사람의 마음이 들린다.
    /// 반짝임(광선과 깜빡이는 빛무리)은 에디트 모드에서도 보인다. 자식 오브젝트(빛무리/광선/심)는 없으면 만든다.
    /// </summary>
    [ExecuteAlways]
    public class CoreFragment : MonoBehaviour
    {
        [Header("파편")]
        [SerializeField, FormerlySerializedAs("title")] private string _title = "가장 반짝이는 파편";
        [Tooltip("이 거리(미로 기준) 안으로 시선이 들어오면 닿은 것으로 친다")]
        [SerializeField, FormerlySerializedAs("touchRadius")] private float _touchRadius = 0.22f;
        [SerializeField, FormerlySerializedAs("touched")] private bool _touched;

        [Header("모양")]
        [SerializeField, FormerlySerializedAs("coreSize")] private float _coreSize = 0.16f;
        [SerializeField, FormerlySerializedAs("rayLength")] private float _rayLength = 0.42f;
        [SerializeField, FormerlySerializedAs("rayWidth")] private float _rayWidth = 0.035f;
        [Tooltip("초당 반짝임 횟수")]
        [SerializeField, FormerlySerializedAs("twinkleSpeed")] private float _twinkleSpeed = 1.3f;
        [SerializeField, FormerlySerializedAs("color")] private Color _color = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField, FormerlySerializedAs("outline")] private Color _outline = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField, FormerlySerializedAs("material")] private Material _material;

        [Header("그림 (자동 생성)")]
        [SerializeField, FormerlySerializedAs("glow")] private LineRenderer _glow;
        [SerializeField, FormerlySerializedAs("rim")] private LineRenderer _rim;
        [SerializeField, FormerlySerializedAs("core")] private LineRenderer _core;
        [SerializeField, FormerlySerializedAs("rayA")] private LineRenderer _rayA;
        [SerializeField, FormerlySerializedAs("rayB")] private LineRenderer _rayB;

        [NonSerialized] private float _touchFlash;

        public float TouchRadius => _touchRadius;

        void OnEnable() => EnsureParts();
        void OnValidate() => EyeHooks.Repaint();

        void Update()
        {
            if (_glow == null || _rim == null || _core == null || _rayA == null || _rayB == null) EnsureParts();
            transform.localPosition = Vector3.zero;

            float t = Application.isPlaying ? Time.time : (float)DateTime.Now.TimeOfDay.TotalSeconds;
            float tw = 0.5f + 0.5f * Mathf.Sin(t * _twinkleSpeed * Mathf.PI * 2f);
            if (Application.isPlaying) _touchFlash = Mathf.MoveTowards(_touchFlash, 0f, Time.deltaTime * 0.8f);
            float boost = 1f + _touchFlash * 1.5f + (_touched ? 0.25f : 0f);

            Color c = _color;
            Color g = _color;
            g.a = 0.25f + 0.25f * tw;

            Shapes.SetBlob(_glow, _coreSize * (2.2f + 0.8f * tw) * boost);
            Shapes.SetColor(_glow, g);
            _glow.sortingOrder = 9;

            Shapes.SetBlob(_rim, _coreSize * 1.35f * boost);
            Shapes.SetColor(_rim, _outline);
            _rim.sortingOrder = 10;

            Shapes.SetBlob(_core, _coreSize * boost);
            Shapes.SetColor(_core, c);
            _core.sortingOrder = 11;

            float len = _rayLength * (0.65f + 0.35f * tw) * boost;
            float spin = t * 25f;
            SetRay(_rayA, len, spin, c);
            SetRay(_rayB, len * 0.7f, spin + 90f, c);

            if (!Application.isPlaying) EyeHooks.Repaint();
        }

        /// <summary>
        /// 파편에 닿은 상태로 바꾸고 한 번 크게 빛나게 한다.
        /// 입력값은 없으며, _touched와 _touchFlash를 바꾼다.
        /// </summary>
        public void Touch()
        {
            _touched = true;
            _touchFlash = 1f;
        }

        /// <summary>
        /// 닿지 않은 처음 상태로 되돌린다.
        /// 입력값은 없으며, _touched와 _touchFlash를 초기화한다.
        /// </summary>
        public void ResetState()
        {
            _touched = false;
            _touchFlash = 0f;
        }

        /// <summary>
        /// 그림용 자식 LineRenderer들이 없으면 찾거나 만든다.
        /// _material(없으면 부모 LineRenderer의 재질)을 사용하며, 그림 참조 필드들을 채운다.
        /// </summary>
        public void EnsureParts()
        {
            if (_material == null)
            {
                LineRenderer parentLine = transform.parent != null ? transform.parent.GetComponent<LineRenderer>() : null;
                if (parentLine != null) _material = parentLine.sharedMaterial;
            }
            _glow = Part(_glow, "빛무리", 16);
            _rim = Part(_rim, "테두리", 16);
            _core = Part(_core, "심", 16);
            _rayA = Part(_rayA, "광선 1", 4);
            _rayB = Part(_rayB, "광선 2", 4);
        }

        /// <summary>
        /// 그림 하나를 이미 있으면 재질만 채우고, 없으면 이름으로 찾거나 새로 만든다.
        /// lr, partName, caps를 사용하며, 준비된 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer Part(LineRenderer lr, string partName, int caps)
        {
            if (lr != null)
            {
                if (lr.sharedMaterial == null && _material != null) lr.sharedMaterial = _material;
                return lr;
            }
            Transform t = transform.Find(partName);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(partName);
                go.transform.SetParent(transform, false);
                EyeHooks.Created(go);
            }
            return Shapes.Ensure(go, _material, caps);
        }

        /// <summary>
        /// 가운데가 굵고 양 끝이 가는 광선 하나를 그린다.
        /// lr, len, deg, c를 사용하며, lr의 점과 두께, 색을 바꾼다.
        /// </summary>
        private void SetRay(LineRenderer lr, float len, float deg, Color c)
        {
            Vector3 d = Shapes.Polar(len * 0.5f, deg);
            Shapes.SetPoints(lr, new[] { -d, Vector3.zero, d }, false);
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
            lr.widthMultiplier = _rayWidth;
            Shapes.SetColor(lr, c);
            lr.sortingOrder = 11;
        }
    }
}
