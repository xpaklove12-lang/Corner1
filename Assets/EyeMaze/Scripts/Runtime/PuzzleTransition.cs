using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 048: 파편 퍼즐 진입·복귀 연출. 파편이 그 자리에서 커졌다가 눈 영역을 덮는 네모 창으로 펼쳐지고,
    /// 퍼즐이 끝나면 네모가 파편 자리로 줄어들며 사라진다.
    /// </summary>
    public class PuzzleTransition : MonoBehaviour
    {
        private enum Phase { Idle, Zoom, Expand, Shown, Shrink }

        [Header("시간")]
        [Tooltip("파편이 그 자리에서 커지는 시간(초)")]
        [SerializeField, Min(0f)] private float _zoomSeconds = 0.35f;
        [Tooltip("네모 창이 파편에서 빠져나와 펼쳐지는 시간(초)")]
        [SerializeField, Min(0f)] private float _expandSeconds = 0.35f;
        [Tooltip("네모 창이 파편 자리로 줄어드는 시간(초)")]
        [SerializeField, Min(0f)] private float _shrinkSeconds = 0.3f;

        [Header("모양")]
        [Tooltip("확대가 끝났을 때 마름모 대각선(월드)")]
        [SerializeField, Min(0.01f)] private float _zoomDiameter = 1.2f;
        [Tooltip("네모 창 가운데 (이 오브젝트 로컬)")]
        [SerializeField] private Vector2 _panelCenter = new Vector2(0f, 0.19f);
        [Tooltip("네모 창 가로·세로(월드). 눈 영역을 덮게 둔다")]
        [SerializeField] private Vector2 _panelSize = new Vector2(10.3f, 7.35f);
        [SerializeField] private Color _panelColor = Color.black;
        [SerializeField] private Color _frameColor = Color.white;
        [SerializeField, Min(0.001f)] private float _frameWidth = 0.06f;
        [SerializeField] private Material _lineMaterial;
        [Tooltip("그리기 순서. 눈꺼풀(22)보다 높고 퍼즐(40)보다 낮게 둔다")]
        [SerializeField] private int _sortingOrder = 30;

        private Phase _phase = Phase.Idle;
        private float _t;
        private Vector3 _origin;
        private float _originSize;
        private Color _tint;
        private Action _onDone;
        private Transform _drawRoot;
        private LineRenderer _diamond;
        private LineRenderer _fill;
        private LineRenderer _frame;

        /// <summary>연출이나 네모 창이 남아 있는지.</summary>
        public bool IsBusy => _phase != Phase.Idle;

        void Update()
        {
            if (_phase == Phase.Idle || _phase == Phase.Shown) return;
            _t += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Zoom:
                    DrawZoom(Progress(_zoomSeconds));
                    if (_t >= _zoomSeconds) Enter(Phase.Expand);
                    break;
                case Phase.Expand:
                    DrawPanel(Progress(_expandSeconds), true);
                    if (_t >= _expandSeconds)
                    {
                        Enter(Phase.Shown);
                        Finish();
                    }
                    break;
                case Phase.Shrink:
                    DrawPanel(1f - Progress(_shrinkSeconds), false);
                    if (_t >= _shrinkSeconds)
                    {
                        Stop();
                        Finish();
                    }
                    break;
            }
        }

        void OnDisable()
        {
            Stop();
        }

        /// <summary>
        /// 파편 위치에서 확대와 네모 펼치기를 시작한다.
        /// worldPos, fragmentSize, tint, onShown을 사용하며, 연출 상태와 그리기 오브젝트를 만들고 다 펼쳐지면 onShown을 부른다.
        /// </summary>
        public void Play(Vector3 worldPos, float fragmentSize, Color tint, Action onShown)
        {
            Stop();
            _origin = transform.InverseTransformPoint(worldPos);
            _origin.z = 0f;
            _originSize = fragmentSize;
            _tint = tint;
            _onDone = onShown;
            Build();
            Enter(Phase.Zoom);
            DrawZoom(0f);
        }

        /// <summary>
        /// 펼쳐진 네모를 처음 파편 위치로 줄이기 시작한다. 연출이 없으면 바로 onHidden을 부른다.
        /// onHidden을 사용하며, 연출 상태를 바꾸고 다 줄어들면 onHidden을 부른다.
        /// </summary>
        public void Return(Action onHidden)
        {
            if (_phase == Phase.Idle)
            {
                onHidden?.Invoke();
                return;
            }
            _onDone = onHidden;
            _diamond.enabled = false;
            Enter(Phase.Shrink);
            DrawPanel(1f, false);
        }

        /// <summary>
        /// 연출을 바로 끝내고 그리기 오브젝트를 지운다. 기다리던 일은 부르지 않는다.
        /// 입력값은 없으며, 연출 상태와 그리기 오브젝트를 바꾼다.
        /// </summary>
        public void Stop()
        {
            _phase = Phase.Idle;
            if (_drawRoot != null) Destroy(_drawRoot.gameObject);
            _drawRoot = null;
            _diamond = null;
            _fill = null;
            _frame = null;
        }

        /// <summary>
        /// 다음 단계로 넘어가고 단계 시간을 0으로 돌린다.
        /// phase를 사용하며, _phase와 _t를 바꾼다.
        /// </summary>
        private void Enter(Phase phase)
        {
            _phase = phase;
            _t = 0f;
        }

        /// <summary>
        /// 기다리던 일을 한 번만 부른다.
        /// _onDone을 사용하며, _onDone을 비운다.
        /// </summary>
        private void Finish()
        {
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        /// <summary>
        /// 현재 단계 진행도를 부드러운 0~1 값으로 구한다. 시간이 0이면 1이다.
        /// seconds와 _t를 사용하며, 진행도를 반환한다.
        /// </summary>
        private float Progress(float seconds)
        {
            return seconds > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / seconds)) : 1f;
        }

        /// <summary>
        /// 마름모, 채움, 테두리 그리기 오브젝트를 만든다.
        /// 모양 설정을 사용하며, _drawRoot 아래 그리기 오브젝트를 바꾼다.
        /// </summary>
        private void Build()
        {
            _drawRoot = new GameObject("퍼즐 연출").transform;
            _drawRoot.SetParent(transform, false);
            _fill = CreateLine("채움", 0);
            _frame = CreateLine("테두리", 1);
            _diamond = CreateLine("파편", 2);
            Shapes.SetColor(_frame, _frameColor);
            _frame.widthMultiplier = _frameWidth;
            _fill.enabled = false;
            _frame.enabled = false;
        }

        /// <summary>
        /// _drawRoot 아래에 선 그리기 오브젝트를 만든다.
        /// objectName과 orderOffset을 사용하며, 설정된 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer CreateLine(string objectName, int orderOffset)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_drawRoot, false);
            LineRenderer line = Shapes.Ensure(go, _lineMaterial, 0);
            line.sortingOrder = _sortingOrder + orderOffset;
            return line;
        }

        /// <summary>
        /// 파편 위치에서 마름모를 파편 크기에서 _zoomDiameter까지 키워 그린다.
        /// p(0~1)를 사용하며, 마름모 위치, 크기, 색을 바꾼다.
        /// </summary>
        private void DrawZoom(float p)
        {
            _diamond.transform.localPosition = _origin;
            MemoryFragment.DrawDiamond(_diamond, Mathf.Lerp(_originSize, _zoomDiameter, p));
            Shapes.SetColor(_diamond, _tint);
        }

        /// <summary>
        /// 네모 창을 파편 위치·크기(p=0)에서 _panelCenter·_panelSize(p=1) 사이로 그린다. 펼칠 때는 마름모를 함께 흐리게 한다.
        /// p와 expanding을 사용하며, 채움, 테두리, 마름모의 위치, 크기, 색을 바꾼다.
        /// </summary>
        private void DrawPanel(float p, bool expanding)
        {
            Vector3 center = Vector3.Lerp(_origin, new Vector3(_panelCenter.x, _panelCenter.y, 0f), p);
            float startSize = expanding ? _zoomDiameter : _originSize;
            float w = Mathf.Lerp(startSize, _panelSize.x, p);
            float h = Mathf.Lerp(startSize, _panelSize.y, p);

            _fill.enabled = true;
            _frame.enabled = true;
            _fill.transform.localPosition = center;
            Shapes.SetFilledRect(_fill, w, h);
            Shapes.SetColor(_fill, _panelColor);

            float hw = w * 0.5f, hh = h * 0.5f;
            _frame.transform.localPosition = center;
            Shapes.SetPoints(_frame, new[]
            {
                new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
                new Vector3(hw, hh, 0f), new Vector3(-hw, hh, 0f),
            }, true);

            if (!expanding) return;
            // 마름모는 네모가 빠져나오는 동안 흐려지며 사라진다.
            Color faded = _tint;
            faded.a *= 1f - p;
            Shapes.SetColor(_diamond, faded);
            _diamond.enabled = p < 1f;
        }
    }
}
