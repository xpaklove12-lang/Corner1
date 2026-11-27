using System;
using System.Collections.Generic;

using UnityEngine;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>
    /// 깜빡임 뒤 벽이 바뀌는 연출을 맡는다. 옛 벽을 표시 전용 잔상으로 남기고, 눈을 뜨는 동안 잔상과 새 벽의 표시값만 바꾼다.
    /// 벽 판정은 이 연출과 상관없이 눈이 감긴 순간 새 벽으로 확정된다.
    /// </summary>
    [Serializable]
    public class MazeWallChangeEffect
    {
        private const int GATE_KEY_KIND = 2;
        private const float MIN_SECONDS = 0.01f;
        private const int RIPPLE_SEGMENTS = 96;
        private const float RIPPLE_FADE_TAIL = 0.25f;
        private const int RIPPLE_SORTING_ORDER = 6;

        [Header("공통")]
        [Tooltip("교차 전환과 파문 진행 곡선")]
        [SerializeField] private AnimationCurve _changeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("파문이 닿은 벽 하나가 바뀌는 시간(초)")]
        [SerializeField] private float _wallFadeSeconds = 0.35f;

        [Header("동공 파문")]
        [Tooltip("파문이 바깥까지 가는 시간(초)")]
        [SerializeField] private float _rippleSeconds = 1.4f;

        [NonSerialized] private IrisMaze _maze;
        [NonSerialized] private int _keepLayer = -1;
        [NonSerialized] private float _effectTime;
        [NonSerialized] private readonly List<Ghost> _ghosts = new List<Ghost>();
        [NonSerialized] private readonly List<ChangedWall> _changedWalls = new List<ChangedWall>();
        [NonSerialized] private LineRenderer _ripple;

        /// <summary>잔상 선 하나와 그 선의 원래 위치, 전환 상태</summary>
        private class Ghost
        {
            public LineRenderer Line;
            public (int kind, int ring, int sector) Key;
            public Vector3 MazeMid;
            public Color Color;
            public float AlphaRatio;
            public float FadeStart = -1f;
        }

        /// <summary>새로 나타나는 벽이나 문 하나와 그 전환 상태</summary>
        private class ChangedWall
        {
            public MazeWall Wall;
            public MazeGate Gate;
            public Transform Transform;
            public Vector3 MazeMid;
            public float FadeStart = -1f;
        }

        /// <summary>연출이 진행 중인지 (옛 벽을 복사한 뒤 정리하기 전까지)</summary>
        public bool IsPlaying => _maze != null;

        /// <summary>눈을 뜨기 시작한 뒤 연출이 끝날 때까지 걸리는 시간(초)</summary>
        public float Duration => Seconds(_rippleSeconds) + Seconds(_wallFadeSeconds);

        /// <summary>
        /// 다시 만들 층의 켜진 벽과 문을 표시 전용 잔상으로 복사한다.
        /// maze, keepLayer(벽을 유지할 층)를 사용하며, _maze와 _keepLayer, _ghosts를 바꾼다.
        /// </summary>
        public void Capture(IrisMaze maze, int keepLayer)
        {
            _maze = maze;
            _keepLayer = keepLayer;
            float wallAlpha = Mathf.Max(0.001f, maze.WallColor.a);
            foreach (MazeLayer layer in maze.Layers)
            {
                if (layer.Index == keepLayer) continue;
                foreach (MazeWall wall in layer.GetComponentsInChildren<MazeWall>(false))
                    AddGhost(layer.transform, wall.GetComponent<LineRenderer>(), WallKey(wall), wallAlpha);
                foreach (MazeGate gate in layer.GetComponentsInChildren<MazeGate>(false))
                    AddGhost(layer.transform, gate.GetComponent<LineRenderer>(), GateKey(gate), wallAlpha);
            }
        }

        /// <summary>
        /// 벽을 새로 만든 직후 바뀐 벽만 골라 연출을 준비한다.
        /// Capture로 복사한 잔상을 사용하며, _ghosts, _changedWalls, 새 벽의 VisualAlpha와 _ripple을 바꾼다.
        /// </summary>
        public void Begin()
        {
            _effectTime = 0f;

            var before = new HashSet<(int kind, int ring, int sector)>();
            foreach (Ghost ghost in _ghosts) before.Add(ghost.Key);

            var after = new HashSet<(int kind, int ring, int sector)>();
            foreach (MazeLayer layer in _maze.Layers)
            {
                if (layer.Index == _keepLayer) continue;
                foreach (MazeWall wall in layer.GetComponentsInChildren<MazeWall>(false))
                {
                    var key = WallKey(wall);
                    after.Add(key);
                    if (!before.Contains(key)) AddChangedWall(layer.transform, wall.transform, wall, null, key);
                }
                foreach (MazeGate gate in layer.GetComponentsInChildren<MazeGate>(false))
                {
                    var key = GateKey(gate);
                    after.Add(key);
                    if (!before.Contains(key)) AddChangedWall(layer.transform, gate.transform, null, gate, key);
                }
            }

            // 전후로 같은 자리에 켜져 있는 벽은 바뀌지 않았으므로 잔상을 지운다.
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                if (!after.Contains(_ghosts[i].Key)) continue;
                Object.Destroy(_ghosts[i].Line.gameObject);
                _ghosts.RemoveAt(i);
            }

            _ripple = MakeRippleLine();
        }

        /// <summary>
        /// 눈을 뜨기 시작한 뒤 지난 시간에 맞춰 파문과 벽 교차 전환을 갱신한다.
        /// elapsed(눈을 뜨기 시작한 뒤 지난 초)와 미로 반지름을 사용하며, _effectTime과 파문의 모양과 색, 잔상과 새 벽의 표시값을 바꾼다.
        /// </summary>
        public void Tick(float elapsed)
        {
            _effectTime = Mathf.Max(0f, elapsed);
            float progress = Mathf.Clamp01(_effectTime / Seconds(_rippleSeconds));
            float radius = Mathf.Lerp(_maze.InnerRadius, _maze.OuterRadius, _changeCurve.Evaluate(progress));
            _ripple.enabled = progress < 1f;
            Shapes.SetCircle(_ripple, radius, RIPPLE_SEGMENTS);
            SetEffectColor(_ripple, RippleAlpha(progress));
            MarkReached(radius);
            ApplyReachFades();
        }

        /// <summary>
        /// 잔상과 파문을 지우고 새 벽의 표시값을 원래대로 되돌린다.
        /// 입력값은 없으며, 연출 중이 아니면 아무 일도 하지 않는다. _ghosts, _changedWalls, _ripple, _maze를 비운다.
        /// </summary>
        public void Finish()
        {
            if (_maze == null) return;
            foreach (Ghost ghost in _ghosts) Object.Destroy(ghost.Line.gameObject);
            _ghosts.Clear();
            foreach (ChangedWall changed in _changedWalls)
            {
                if (changed.Transform != null) SetVisualAlpha(changed, 1f);
            }
            _changedWalls.Clear();
            Object.Destroy(_ripple.gameObject);
            _ripple = null;
            _maze = null;
        }

        /// <summary>
        /// 벽의 위치 키를 만든다.
        /// wall을 사용하며, 종류, 고리, 칸으로 된 키를 반환한다.
        /// </summary>
        private static (int kind, int ring, int sector) WallKey(MazeWall wall) => ((int)wall.WallKind, wall.Ring, wall.Sector);

        /// <summary>
        /// 문의 위치 키를 만든다. 문은 같은 자리의 벽과 구분되는 종류 값을 쓴다.
        /// gate를 사용하며, 종류, 고리, 칸으로 된 키를 반환한다.
        /// </summary>
        private static (int kind, int ring, int sector) GateKey(MazeGate gate) => (GATE_KEY_KIND, gate.Ring, gate.Sector);

        /// <summary>
        /// 연출 시간이 0이 되지 않도록 최솟값을 적용한다.
        /// seconds를 사용하며, MIN_SECONDS 이상의 값을 반환한다.
        /// </summary>
        private static float Seconds(float seconds) => Mathf.Max(MIN_SECONDS, seconds);

        /// <summary>
        /// 연출 곡선의 끝에서 파문을 투명하게 할 값을 계산한다.
        /// progress(0~1)를 사용하며, 투명도 배율을 반환한다.
        /// </summary>
        private static float RippleAlpha(float progress) => Mathf.Clamp01((1f - progress) / RIPPLE_FADE_TAIL);

        /// <summary>
        /// 위치 키가 가리키는 벽이나 문의 중점을 미로 기준 좌표로 계산한다.
        /// layer와 key를 사용하며, 미로 로컬 좌표를 반환한다.
        /// </summary>
        private Vector3 MazeMidOf(Transform layer, (int kind, int ring, int sector) key)
        {
            float degrees = _maze.SectorDeg;
            Vector3 local = key.kind == (int)MazeWall.Kind.Radial
                ? Shapes.Polar((_maze.RingOuter(key.ring) + _maze.RingInner(key.ring)) * 0.5f, (key.sector + 1) * degrees)
                : Shapes.Polar(_maze.RingInner(key.ring), (key.sector + 0.5f) * degrees);
            return _maze.transform.InverseTransformPoint(layer.TransformPoint(local));
        }

        /// <summary>
        /// 벽이나 문의 선을 복사한 잔상 하나를 만든다.
        /// layer, source, key, wallAlpha(복사 시점의 벽 투명도)를 사용하며, _ghosts에 잔상을 추가한다.
        /// </summary>
        private void AddGhost(Transform layer, LineRenderer source, (int kind, int ring, int sector) key, float wallAlpha)
        {
            LineRenderer line = MakeGhostLine(layer, source, "잔상");
            var points = new Vector3[source.positionCount];
            source.GetPositions(points);
            Shapes.SetPoints(line, points, source.loop);
            _ghosts.Add(new Ghost
            {
                Line = line,
                Key = key,
                MazeMid = MazeMidOf(layer, key),
                Color = source.startColor,
                AlphaRatio = source.startColor.a / wallAlpha,
            });
        }

        /// <summary>
        /// 원본 선과 모양 설정이 같은 표시 전용 선 오브젝트를 만든다.
        /// parent, source, objectName을 사용하며, 만든 LineRenderer를 반환한다.
        /// </summary>
        private static LineRenderer MakeGhostLine(Transform parent, LineRenderer source, string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            LineRenderer line = Shapes.Ensure(go, source.sharedMaterial, source.numCapVertices);
            line.widthMultiplier = source.widthMultiplier;
            line.sortingOrder = source.sortingOrder;
            Shapes.SetColor(line, source.startColor);
            return line;
        }

        /// <summary>
        /// 새로 나타나는 벽이나 문을 목록에 넣고 숨긴다.
        /// layer, target, wall, gate, key를 사용하며, _changedWalls와 대상의 VisualAlpha를 바꾼다.
        /// </summary>
        private void AddChangedWall(Transform layer, Transform target, MazeWall wall, MazeGate gate, (int kind, int ring, int sector) key)
        {
            var changed = new ChangedWall
            {
                Wall = wall,
                Gate = gate,
                Transform = target,
                MazeMid = MazeMidOf(layer, key),
            };
            SetVisualAlpha(changed, 0f);
            _changedWalls.Add(changed);
        }

        /// <summary>
        /// 새 벽이나 문의 표시 투명도를 바꾼다.
        /// changed와 alpha를 사용하며, 대상의 VisualAlpha를 바꾼다.
        /// </summary>
        private static void SetVisualAlpha(ChangedWall changed, float alpha)
        {
            if (changed.Wall != null) changed.Wall.VisualAlpha = alpha;
            else changed.Gate.VisualAlpha = alpha;
        }

        /// <summary>
        /// 잔상의 색을 지금 벽 투명도에 맞춰 칠한다.
        /// ghost와 visibility(0~1)를 사용하며, 잔상 선의 색을 바꾼다.
        /// </summary>
        private void SetGhostVisibility(Ghost ghost, float visibility)
        {
            Color color = ghost.Color;
            color.a = ghost.AlphaRatio * _maze.WallColor.a * visibility;
            Shapes.SetColor(ghost.Line, color);
        }

        /// <summary>
        /// 연출용 선의 색을 지금 벽 색에 맞춰 칠한다.
        /// line과 alpha를 사용하며, 선의 색을 바꾼다.
        /// </summary>
        private void SetEffectColor(LineRenderer line, float alpha)
        {
            Color color = _maze.WallColor;
            color.a *= alpha;
            Shapes.SetColor(line, color);
        }

        /// <summary>
        /// 미로 아래에 파문 선 오브젝트를 만든다.
        /// 미로의 선 재질과 벽 두께를 사용하며, 만든 LineRenderer를 반환한다.
        /// </summary>
        private LineRenderer MakeRippleLine()
        {
            var go = new GameObject("파문");
            go.transform.SetParent(_maze.transform, false);
            LineRenderer line = Shapes.Ensure(go, _maze.LineMaterial, 2);
            line.widthMultiplier = _maze.WallWidthNow;
            line.sortingOrder = RIPPLE_SORTING_ORDER;
            line.enabled = false;
            return line;
        }

        /// <summary>
        /// 파문이 닿은 벽의 교차 전환을 시작한다.
        /// radius(파문 반지름)를 사용하며, 닿은 잔상과 새 벽의 FadeStart를 바꾼다.
        /// </summary>
        private void MarkReached(float radius)
        {
            foreach (Ghost ghost in _ghosts)
                if (ghost.FadeStart < 0f && ghost.MazeMid.magnitude <= radius) ghost.FadeStart = _effectTime;
            foreach (ChangedWall changed in _changedWalls)
                if (changed.FadeStart < 0f && changed.MazeMid.magnitude <= radius) changed.FadeStart = _effectTime;
        }

        /// <summary>
        /// 벽 하나의 교차 전환 진행도를 계산한다.
        /// fadeStart(시작 시각, 음수면 아직 시작 전)와 _effectTime을 사용하며, 0~1 진행도를 반환한다.
        /// </summary>
        private float FadeProgress(float fadeStart)
        {
            if (fadeStart < 0f) return 0f;
            return _changeCurve.Evaluate(Mathf.Clamp01((_effectTime - fadeStart) / Seconds(_wallFadeSeconds)));
        }

        /// <summary>
        /// 파문이 닿은 벽마다 각자의 시작 시각에 맞춰 교차 전환을 적용한다.
        /// _ghosts와 _changedWalls의 FadeStart를 사용하며, 잔상의 색과 새 벽의 VisualAlpha를 바꾼다.
        /// </summary>
        private void ApplyReachFades()
        {
            foreach (Ghost ghost in _ghosts) SetGhostVisibility(ghost, 1f - FadeProgress(ghost.FadeStart));
            foreach (ChangedWall changed in _changedWalls)
                if (changed.Transform != null) SetVisualAlpha(changed, FadeProgress(changed.FadeStart));
        }
    }
}
