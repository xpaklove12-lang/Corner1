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

        private readonly Dictionary<MemoryFragment, LineRenderer> _glows = new Dictionary<MemoryFragment, LineRenderer>();
        private readonly Dictionary<MemoryFragment, LineRenderer> _outerGlows = new Dictionary<MemoryFragment, LineRenderer>();
        private readonly List<ActiveRing> _activeRings = new List<ActiveRing>();

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
            UpdateRings(Time.deltaTime);
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
            foreach (KeyValuePair<MemoryFragment, LineRenderer> pair in _glows)
            {
                if (!pair.Key.isActiveAndEnabled) continue;
                UpdateGlow(pair.Key, pair.Value, _glowSizeRatio, _glowAlpha);
                UpdateGlow(pair.Key, _outerGlows[pair.Key], _outerGlowSizeRatio, _outerGlowAlpha);
            }
        }

        /// <summary>
        /// 후광 하나의 크기와 색을 파편의 맥동에 맞추고, 주운 파편의 후광은 끈다.
        /// fragment, line, sizeRatio, alpha를 사용하며, line의 표시 여부와 두께, 색을 바꾼다.
        /// </summary>
        private void UpdateGlow(MemoryFragment fragment, LineRenderer line, float sizeRatio, float alpha)
        {
            line.enabled = !fragment.Collected;
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
            var go = new GameObject("획득 고리");
            go.transform.SetParent(fragment.Layer.transform, false);
            go.transform.position = fragment.transform.position;
            LineRenderer line = Shapes.Ensure(go, _maze.LineMaterial, 0);
            line.sortingOrder = RING_SORTING_ORDER;
            line.widthMultiplier = _maze.WallWidthNow;
            var ring = new ActiveRing(fragment, line);
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
            float diameter = Mathf.Lerp(ring.Fragment.BodyDiameter, _maze.RingWidth * _ringEndRatio, eased);
            Shapes.SetCircle(ring.Line, diameter * 0.5f, RING_SEGMENTS);
            Color color = ring.Fragment.MazeColor;
            color.a *= 1f - progress;
            Shapes.SetColor(ring.Line, color);
        }

        /// <summary>
        /// 재생 중인 고리를 모두 바로 지운다.
        /// 입력값은 없으며, _activeRings를 비운다.
        /// </summary>
        private void ClearRings()
        {
            foreach (ActiveRing ring in _activeRings) Destroy(ring.Line.gameObject);
            _activeRings.Clear();
        }

        /// <summary>재생 중인 획득 고리 하나의 상태.</summary>
        private sealed class ActiveRing
        {
            public MemoryFragment Fragment { get; }
            public LineRenderer Line { get; }
            public float Elapsed { get; set; }

            public ActiveRing(MemoryFragment fragment, LineRenderer line)
            {
                Fragment = fragment;
                Line = line;
            }
        }
    }
}
