using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>파편의 빛 번짐과 획득 고리를 Play 중에 만들어 그린다. 씬의 오브젝트 하나에 붙인다.</summary>
    public class FragmentFeedback : MonoBehaviour
    {
        private const int GLOW_SORTING_ORDER = 6;
        private const int OUTER_GLOW_SORTING_ORDER = 5;
        private const int RING_SORTING_ORDER = 8;
        private const int RING_SEGMENTS = 48;

        [Header("연결")]
        [SerializeField] private IrisMaze _maze;
        [Tooltip("첫 안내 상태를 읽을 디렉터")]
        [SerializeField] private EyeMazeDirector _director;

        [Header("후광")]
        [Tooltip("안쪽 후광 지름 (RingWidth 배율)")]
        [SerializeField] private float _glowSizeRatio = 0.6f;
        [Tooltip("안쪽 후광 투명도")]
        [SerializeField, Range(0f, 1f)] private float _glowAlpha = 0.3f;
        [Tooltip("바깥 후광 지름 (RingWidth 배율)")]
        [SerializeField] private float _outerGlowSizeRatio = 1.3f;
        [Tooltip("바깥 후광 투명도")]
        [SerializeField, Range(0f, 1f)] private float _outerGlowAlpha = 0f;
        [Tooltip("맥동이 가장 어두울 때의 후광 투명도 배율")]
        [SerializeField, Range(0f, 1f)] private float _glowPulseMinAlpha = 0.45f;
        [Tooltip("맥동이 가장 작을 때의 후광 크기 배율")]
        [SerializeField, Range(0f, 1f)] private float _glowPulseMinScale = 1f;

        [Header("획득 고리")]
        [Tooltip("획득 고리 재생 시간(초)")]
        [SerializeField] private float _ringSeconds = 0.6f;
        [Tooltip("고리가 끝날 때의 지름 (RingWidth 배율)")]
        [SerializeField] private float _ringEndRatio = 1.5f;
        // 스펙 046 FR-043: 드러난 파편에 닿아 보게 하는 고리의 간격과 지난 시간
        [Header("끌어오는 빛줄")]
        [Tooltip("열망에서 끌려오는 기억과 시선을 잇는 빛줄의 투명도")]
        [SerializeField, Range(0f, 1f)] private float _tetherAlpha = 0.45f;

        [Header("첫 안내 고리")]
        [Tooltip("첫 파편을 얻기 전, 드러난 파편에서 고리가 퍼지는 간격(초)")]
        [SerializeField, Min(0.2f)] private float _nudgeRingSeconds = 2f;
        private float _nudgeRingT;
        private readonly Dictionary<MemoryFragment, LineRenderer> _tethers = new Dictionary<MemoryFragment, LineRenderer>();

        private readonly Dictionary<MemoryFragment, LineRenderer> _glows = new Dictionary<MemoryFragment, LineRenderer>();
        private readonly Dictionary<MemoryFragment, LineRenderer> _outerGlows = new Dictionary<MemoryFragment, LineRenderer>();
        private readonly List<ActiveRing> _activeRings = new List<ActiveRing>();
        private readonly List<MemoryFragment> _staleFragments = new List<MemoryFragment>();

        void OnEnable()
        {
            MemoryFragment.PickedUp += PlayRing;
            MemoryFragment.FeedbackCleared += ClearRings;
        }

        void OnDisable()
        {
            MemoryFragment.PickedUp -= PlayRing;
            MemoryFragment.FeedbackCleared -= ClearRings;
            ClearRings();
        }

        void Start()
        {
            foreach (MemoryFragment fragment in _maze.GetComponentsInChildren<MemoryFragment>(true))
            {
                _glows.Add(fragment, CreateGlow(fragment, "빛 번짐", GLOW_SORTING_ORDER));
                _outerGlows.Add(fragment, CreateGlow(fragment, "바깥 빛 번짐", OUTER_GLOW_SORTING_ORDER));
            }
        }

        void LateUpdate()
        {
            UpdateGlows();
            TickNudgeRings(Time.deltaTime);
            UpdateTethers();
            UpdateRings(Time.deltaTime);
        }

        /// <summary>
        /// 스펙 046 FR-048: 열망에서 시선이 있는 층의 드러났지만 얻지 못한 파편과 시선 사이에 빛줄을 그린다. 그 밖에는 빛줄을 끈다.
        /// 디렉터의 감정과 시선, 파편의 맥동을 사용하며, _tethers의 선을 만들거나 켜고 끄고 다시 그린다.
        /// </summary>
        private void UpdateTethers()
        {
            EmotionControl emotion = _director.EmotionControl;
            bool pulling = _director.CanRunHazards && emotion.Current == Emotion.Hope && emotion.Stage >= 2;
            int layer = pulling ? _maze.LayerOfRing(_director.Cursor.Ring) : -1;
            foreach (MemoryFragment fragment in _glows.Keys)
            {
                if (fragment == null) continue;
                bool show = pulling && fragment.isActiveAndEnabled && !fragment.Collected && !fragment.Hidden && _maze.LayerOfRing(fragment.Ring) == layer;
                if (!_tethers.TryGetValue(fragment, out LineRenderer line))
                {
                    if (!show) continue;
                    var go = new GameObject("끌어오는 빛줄");
                    go.transform.SetParent(transform, false);
                    line = Shapes.Ensure(go, _maze.LineMaterial, 0);
                    line.useWorldSpace = true;
                    line.sortingOrder = OUTER_GLOW_SORTING_ORDER;
                    _tethers.Add(fragment, line);
                }
                if (line == null) continue;
                line.enabled = show;
                if (!show) continue;
                line.positionCount = 2;
                line.SetPosition(0, fragment.transform.position);
                line.SetPosition(1, _director.Cursor.transform.position);
                line.widthMultiplier = _maze.WallWidthNow * 0.6f;
                Color color = fragment.MazeColor;
                color.a *= _tetherAlpha * Mathf.Lerp(0.5f, 1f, fragment.PulseWave);
                Shapes.SetColor(line, color);
            }
        }

        /// <summary>
        /// 스펙 046 FR-043: 첫 파편을 얻기 전, 드러났지만 얻지 못한 파편에서 정해진 간격마다 고리를 퍼뜨린다.
        /// dt, _nudgeRingSeconds, 디렉터의 안내 상태를 사용하며, _nudgeRingT와 _activeRings를 바꾼다.
        /// </summary>
        private void TickNudgeRings(float dt)
        {
            // 안내가 시작되는 순간 바로 한 번 퍼지도록 시간을 가득 채워 둔다.
            if (!_director.NudgeFragments) { _nudgeRingT = _nudgeRingSeconds; return; }
            _nudgeRingT += dt;
            if (_nudgeRingT < _nudgeRingSeconds) return;
            _nudgeRingT = 0f;
            foreach (MemoryFragment fragment in _glows.Keys)
                if (fragment != null && fragment.isActiveAndEnabled && !fragment.Collected && !fragment.Hidden) PlayRing(fragment);
        }

        /// <summary>
        /// 파편 자식으로 후광 원 하나를 만든다.
        /// fragment, objectName, sortingOrder를 사용하며, 만든 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer CreateGlow(MemoryFragment fragment, string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(fragment.transform, false);
            LineRenderer line = Shapes.Ensure(go, _maze.LineMaterial, 0);
            line.sortingOrder = sortingOrder;
            return line;
        }

        /// <summary>
        /// 켜져 있는 파편의 두 겹 후광을 맞춘다.
        /// _glows와 _outerGlows를 사용하며, 각 후광의 표시 여부와 크기, 색을 바꾼다.
        /// </summary>
        private void UpdateGlows()
        {
            _staleFragments.Clear();
            foreach (KeyValuePair<MemoryFragment, LineRenderer> pair in _glows)
            {
                if (pair.Key == null || pair.Value == null || !_outerGlows.TryGetValue(pair.Key, out LineRenderer outerGlow) || outerGlow == null)
                {
                    _staleFragments.Add(pair.Key);
                    continue;
                }
                if (!pair.Key.isActiveAndEnabled) continue;
                UpdateGlow(pair.Key, pair.Value, _glowSizeRatio, _glowAlpha);
                UpdateGlow(pair.Key, outerGlow, _outerGlowSizeRatio, _outerGlowAlpha);
            }

            foreach (MemoryFragment fragment in _staleFragments)
            {
                _glows.Remove(fragment);
                _outerGlows.Remove(fragment);
            }
        }

        /// <summary>
        /// 후광 하나의 크기와 색을 파편의 맥동에 맞추고, 주운 파편의 후광은 끈다.
        /// fragment, line, sizeRatio, alpha를 사용하며, line의 표시 여부와 두께, 색을 바꾼다.
        /// </summary>
        private void UpdateGlow(MemoryFragment fragment, LineRenderer line, float sizeRatio, float alpha)
        {
            // 스펙 046: 숨긴 파편의 후광도 끔
            line.enabled = !fragment.Collected && !fragment.Hidden;
            if (!line.enabled) return;
            float wave = fragment.PulseWave;
            float diagonal = _maze.RingWidth * sizeRatio * Mathf.Lerp(_glowPulseMinScale, 1f, wave);
            if (!Mathf.Approximately(line.widthMultiplier, diagonal)) MemoryFragment.DrawDiamond(line, diagonal);
            Color color = fragment.MazeColor;
            color.a *= alpha * Mathf.Lerp(_glowPulseMinAlpha, 1f, wave);
            Shapes.SetColor(line, color);
        }

        /// <summary>
        /// 파편이 속한 층의 자식으로 획득 고리를 만들어 재생을 시작한다.
        /// fragment를 사용하며, _activeRings에 고리를 추가한다.
        /// </summary>
        private void PlayRing(MemoryFragment fragment)
        {
            if (fragment == null) return;
            MazeLayer layer = fragment.Layer;
            if (layer == null) return;
            var go = new GameObject("획득 고리");
            go.transform.SetParent(layer.transform, false);
            go.transform.position = fragment.transform.position;
            LineRenderer line = Shapes.Ensure(go, _maze.LineMaterial, 0);
            line.sortingOrder = RING_SORTING_ORDER;
            line.widthMultiplier = _maze.WallWidthNow;
            var ring = new ActiveRing(fragment.BodyDiameter, fragment.MazeColor, line);
            _activeRings.Add(ring);
            DrawRing(ring, 0f);
        }

        /// <summary>
        /// 재생 중인 고리를 진행시키고 끝난 고리를 지운다.
        /// dt와 _ringSeconds를 사용하며, _activeRings와 고리의 모양을 바꾼다.
        /// </summary>
        private void UpdateRings(float dt)
        {
            float duration = Mathf.Max(0.01f, _ringSeconds);
            for (int i = _activeRings.Count - 1; i >= 0; i--)
            {
                ActiveRing ring = _activeRings[i];
                if (ring.Line == null)
                {
                    _activeRings.RemoveAt(i);
                    continue;
                }
                ring.Elapsed += dt;
                if (ring.Elapsed < duration)
                {
                    DrawRing(ring, ring.Elapsed / duration);
                    continue;
                }
                Destroy(ring.Line.gameObject);
                _activeRings.RemoveAt(i);
            }
        }

        /// <summary>
        /// 진행도에 맞춰 고리의 지름과 투명도를 그린다.
        /// ring과 progress(0~1)를 사용하며, 고리의 점 목록과 색을 바꾼다.
        /// </summary>
        private void DrawRing(ActiveRing ring, float progress)
        {
            float eased = 1f - (1f - progress) * (1f - progress);
            float diameter = Mathf.Lerp(ring.StartDiameter, _maze.RingWidth * _ringEndRatio, eased);
            Shapes.SetCircle(ring.Line, diameter * 0.5f, RING_SEGMENTS);
            Color color = ring.Color;
            color.a *= 1f - progress;
            Shapes.SetColor(ring.Line, color);
        }

        /// <summary>
        /// 재생 중인 고리를 모두 바로 지운다.
        /// 입력값은 없으며, _activeRings를 비운다.
        /// </summary>
        private void ClearRings()
        {
            foreach (ActiveRing ring in _activeRings)
                if (ring.Line != null) Destroy(ring.Line.gameObject);
            _activeRings.Clear();
        }

        /// <summary>재생 중인 획득 고리 하나의 상태.</summary>
        private sealed class ActiveRing
        {
            public float StartDiameter { get; }
            public Color Color { get; }
            public LineRenderer Line { get; }
            public float Elapsed { get; set; }

            /// <summary>
            /// 재생할 고리 하나의 상태를 만든다.
            /// startDiameter, color, line을 사용하며, 파편과 독립된 재생 상태를 저장한다.
            /// </summary>
            public ActiveRing(float startDiameter, Color color, LineRenderer line)
            {
                StartDiameter = startDiameter;
                Color = color;
                Line = line;
            }
        }
    }
}
