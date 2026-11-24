using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 홍채 = 원형 미로. 바깥 고리(0)에서 안쪽 고리로 들어가 동공(진심)에 닿으면 끝.
    /// 고리는 층(MazeLayer)으로 묶이고, 층 사이에는 문(MazeGate)이 하나씩 있다.
    /// 벽(MazeWall)은 전부 Hierarchy의 GameObject이며, 벽 오브젝트를 끄면 그 자리가 길이 된다.
    /// 좌표: ring(0 = 가장 바깥) / sector(0 ~ sectors-1, 반시계 방향).
    /// 벽, 문, 기억 조각의 sector는 자기 층 기준(로컬)이고, 시선 커서의 sector는 화면 기준(월드)이다.
    /// </summary>
    [ExecuteAlways]
    public class IrisMaze : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField, FormerlySerializedAs("rings"), Range(2, 12)] private int _rings = 6;
        [SerializeField, FormerlySerializedAs("sectors"), Range(6, 32)] private int _sectors = 16;
        [SerializeField, FormerlySerializedAs("ringsPerLayer"), Range(1, 4)] private int _ringsPerLayer = 2;
        [SerializeField, FormerlySerializedAs("outerRadius")] private float _outerRadius = 3f;
        [SerializeField, FormerlySerializedAs("innerRadius")] private float _innerRadius = 0.9f;

        [Header("그리기")]
        [SerializeField, FormerlySerializedAs("lineMaterial")] private Material _lineMaterial;
        [SerializeField, FormerlySerializedAs("wallWidth")] private float _wallWidth = 0.05f;
        [SerializeField, FormerlySerializedAs("wallColorBase")] private Color _wallColorBase = new Color(0.96f, 0.86f, 0.66f, 1f);
        [Tooltip("디렉터가 모드에 따라 바꾼다 (대화 중엔 흐리게, 들여다볼 땐 선명하게)")]
        [SerializeField, FormerlySerializedAs("wallAlpha"), Range(0f, 1f)] private float _wallAlpha = 1f;
        [Tooltip("초점. 0이면 벽이 굵고 흐릿하게, 1이면 선명하게")]
        [SerializeField, FormerlySerializedAs("focus"), Range(0f, 1f)] private float _focus = 1f;

        [Header("생성")]
        [SerializeField, FormerlySerializedAs("seed")] private int _seed = 7;
        [Tooltip("막다른 길을 줄이기 위해 추가로 뚫는 벽 비율")]
        [SerializeField, FormerlySerializedAs("loopChance"), Range(0f, 0.4f)] private float _loopChance = 0.08f;
        [SerializeField, FormerlySerializedAs("pupilDilationRequired"), Range(0f, 1f)] private float _pupilDilationRequired = 0.75f;

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("person")] private Person _person;

        // 스펙 046: 층 순서대로의 문 열쇠 플래그와 이미 지나간 문의 고리 추가
        [NonSerialized] private string[] _gateKeyFlags = Array.Empty<string>();
        private readonly HashSet<int> _passedGateRings = new HashSet<int>();
        // 스펙 046 FR-045: 열쇠로 열어 선이 사라진 문의 고리
        private readonly HashSet<int> _unlockedGateRings = new HashSet<int>();
        private readonly Queue<(int ring, int sector)> _searchQueue = new Queue<(int ring, int sector)>();

        public int Rings => _rings;
        public int Sectors => _sectors;
        public float OuterRadius => _outerRadius;
        public float InnerRadius => _innerRadius;
        public int Seed => _seed;
        public float WallAlpha { get => _wallAlpha; set => _wallAlpha = value; }
        public float Focus { get => _focus; set => _focus = value; }
        public Person Person => _person;
        // 스펙 010: 연출 오브젝트가 같은 선 재질을 쓰도록 공개
        public Material LineMaterial => _lineMaterial;

        public float FocusAlpha => Mathf.Lerp(0.12f, 1f, _focus * _focus);
        public Color WallColor => new Color(_wallColorBase.r, _wallColorBase.g, _wallColorBase.b, _wallColorBase.a * _wallAlpha * FocusAlpha);
        public float WallWidthNow => _wallWidth * (1f + (1f - _focus) * 3f);
        public int GeometryKey => HashCode.Combine(_rings, _sectors, _ringsPerLayer, _outerRadius, _innerRadius);

        public float RingWidth => (_outerRadius - _innerRadius) / _rings;
        public float SectorDeg => 360f / _sectors;
        public int LayerCount => Mathf.CeilToInt(_rings / (float)_ringsPerLayer);
        public MazeLayer[] Layers => GetComponentsInChildren<MazeLayer>(true).OrderBy(l => l.Index).ToArray();

        void OnValidate() => EyeHooks.Repaint();

        // 좌표 계산

        /// <summary>
        /// 고리의 바깥 반지름을 계산한다.
        /// 고리 번호 r을 사용하며, 반지름을 반환한다.
        /// </summary>
        public float RingOuter(int r) => _outerRadius - r * RingWidth;

        /// <summary>
        /// 고리의 안쪽 반지름을 계산한다.
        /// 고리 번호 r을 사용하며, 반지름을 반환한다.
        /// </summary>
        public float RingInner(int r) => _outerRadius - (r + 1) * RingWidth;

        /// <summary>
        /// 고리의 가운데 반지름을 계산한다.
        /// 고리 번호 r을 사용하며, 반지름을 반환한다.
        /// </summary>
        public float RingMid(int r) => _outerRadius - (r + 0.5f) * RingWidth;

        /// <summary>
        /// 고리가 몇 번째 층에 속하는지 계산한다.
        /// 고리 번호 r을 사용하며, 층 번호를 반환한다.
        /// </summary>
        public int LayerOfRing(int r) => Mathf.Clamp(r / _ringsPerLayer, 0, LayerCount - 1);

        /// <summary>
        /// 고리가 자기 층의 가장 안쪽 고리인지 판단한다.
        /// 고리 번호 r을 사용하며, 마지막 고리면 true를 반환한다.
        /// </summary>
        public bool IsLastRingOfLayer(int r) => r % _ringsPerLayer == _ringsPerLayer - 1 || r == _rings - 1;

        /// <summary>
        /// sector 번호를 0 ~ sectors-1 범위로 감는다.
        /// a를 사용하며, 감긴 sector 번호를 반환한다.
        /// </summary>
        public int Mod(int a) => ((a % _sectors) + _sectors) % _sectors;

        /// <summary>
        /// ring r의 안쪽 원호 벽이 어느 층에 속하는지 계산한다. 층 경계 원호는 안쪽 층 소속이다.
        /// 고리 번호 r을 사용하며, 층 번호를 반환한다.
        /// </summary>
        public int ArcOwner(int r)
        {
            if (r >= _rings - 1) return LayerOfRing(_rings - 1);
            return IsLastRingOfLayer(r) ? LayerOfRing(r) + 1 : LayerOfRing(r);
        }

        /// <summary>
        /// 칸 한가운데의 로컬 좌표를 계산한다.
        /// ring, sector를 사용하며, 미로 기준 로컬 좌표를 반환한다.
        /// </summary>
        public Vector3 CellLocal(int ring, int sector) => Shapes.Polar(RingMid(ring), (sector + 0.5f) * SectorDeg);

        /// <summary>
        /// 반지름이 몇 번째 고리에 있는지 계산한다.
        /// radius를 사용하며, 고리 번호(0 = 가장 바깥)를 반환한다.
        /// </summary>
        public int RingAt(float radius) => Mathf.Clamp(Mathf.FloorToInt((_outerRadius - radius) / RingWidth), 0, _rings - 1);

        /// <summary>
        /// 각도가 화면 기준 몇 번째 sector에 있는지 계산한다.
        /// angleDeg를 사용하며, sector 번호를 반환한다.
        /// </summary>
        public int SectorAt(float angleDeg) => Mod(Mathf.FloorToInt(Mathf.Repeat(angleDeg, 360f) / SectorDeg));

        /// <summary>
        /// 층 번호로 층 오브젝트를 찾는다.
        /// index를 사용하며, 찾은 MazeLayer 또는 null을 반환한다.
        /// </summary>
        public MazeLayer GetLayer(int index)
        {
            foreach (MazeLayer l in GetComponentsInChildren<MazeLayer>(true)) if (l.Index == index) return l;
            return null;
        }

        /// <summary>
        /// 층의 현재 회전량을 가져온다.
        /// layer 번호를 사용하며, 칸 단위 회전량을 반환한다.
        /// </summary>
        public int Offset(int layer)
        {
            MazeLayer l = GetLayer(layer);
            return l != null ? l.Offset : 0;
        }

        // 길 찾기

        /// <summary>
        /// 종류와 위치로 벽을 찾는다.
        /// kind, ring, localSector를 사용하며, 찾은 MazeWall 또는 null을 반환한다.
        /// </summary>
        public MazeWall FindWall(MazeWall.Kind kind, int ring, int localSector)
        {
            foreach (MazeWall w in GetComponentsInChildren<MazeWall>(true))
                if (w.WallKind == kind && w.Ring == ring && w.Sector == localSector) return w;
            return null;
        }

        /// <summary>
        /// 위치로 켜져 있는 문을 찾는다.
        /// ring, localSector를 사용하며, 찾은 MazeGate 또는 null을 반환한다.
        /// </summary>
        public MazeGate FindGate(int ring, int localSector)
        {
            foreach (MazeGate g in GetComponentsInChildren<MazeGate>(true))
                if (g.Ring == ring && g.Sector == localSector && g.gameObject.activeSelf) return g;
            return null;
        }

        /// <summary>
        /// ring r 칸에서 안쪽(r+1)으로 갈 수 있는지 판단한다.
        /// r, worldSector를 사용하며, 열려 있으면 true를 반환하고 그 자리의 문을 gate로 돌려준다.
        /// </summary>
        public bool IsArcOpen(int r, int worldSector, out MazeGate gate)
        {
            int owner = ArcOwner(r);
            int ls = Mod(worldSector - Offset(owner));
            gate = FindGate(r, ls);
            MazeWall w = FindWall(MazeWall.Kind.Arc, r, ls);
            return w == null || !w.gameObject.activeSelf;
        }

        /// <summary>
        /// ring r에서 worldBoundary 칸과 그 다음 칸 사이를 지날 수 있는지 판단한다.
        /// r, worldBoundary를 사용하며, 열려 있으면 true를 반환한다.
        /// </summary>
        public bool IsRadialOpen(int r, int worldBoundary)
        {
            int ls = Mod(worldBoundary - Offset(LayerOfRing(r)));
            MazeWall w = FindWall(MazeWall.Kind.Radial, r, ls);
            return w == null || !w.gameObject.activeSelf;
        }

        /// <summary>
        /// 스펙 046: 맞닿은 두 칸 사이를 지금 지날 수 있는지 판단한다. 맞닿지 않은 칸이면 지날 수 없는 것으로 본다.
        /// 두 칸의 고리와 화면 기준 섹터를 사용하며, 벽이 열려 있으면 true를 반환한다.
        /// </summary>
        public bool IsStepOpen(int ringA, int sectorA, int ringB, int sectorB)
        {
            if (ringA == ringB)
            {
                if (sectorB == Mod(sectorA + 1)) return IsRadialOpen(ringA, sectorA);
                if (sectorA == Mod(sectorB + 1)) return IsRadialOpen(ringA, sectorB);
                return false;
            }
            if (Mathf.Abs(ringA - ringB) != 1 || sectorA != sectorB) return false;
            // 잠긴 문이 있는 자리는 벽이 꺼져 있어도 지날 수 없는 것으로 본다.
            return IsArcOpen(Mathf.Min(ringA, ringB), sectorA, out MazeGate gate) && (gate == null || gate.CanPass(_person));
        }

        /// <summary>
        /// 스펙 046: 미로 기준 두 점을 잇는 곧은 길이 닫힌 벽이나 미로 바깥에 막히는지 판단한다.
        /// from과 to(미로 로컬 좌표)를 사용하며, 막히면 true를 반환한다.
        /// </summary>
        public bool IsPathBlocked(Vector2 from, Vector2 to)
        {
            const float STEP = 0.03f;
            int steps = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude / STEP));
            int ring = RingAt(from.magnitude);
            int sector = SectorAt(Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                float radius = point.magnitude;
                if (radius > _outerRadius || radius < _innerRadius) return true;
                int nextRing = RingAt(radius);
                int nextSector = SectorAt(Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg);
                if (nextRing == ring && nextSector == sector) continue;
                // 한 걸음에 고리와 섹터 경계를 함께 넘으면 두 경계를 차례로 넘는 두 길 가운데 하나라도 열려 있는지 본다.
                bool diagonal = nextRing != ring && nextSector != sector;
                bool open = diagonal
                    ? (IsStepOpen(ring, sector, nextRing, sector) && IsStepOpen(nextRing, sector, nextRing, nextSector))
                        || (IsStepOpen(ring, sector, ring, nextSector) && IsStepOpen(ring, nextSector, nextRing, nextSector))
                    : IsStepOpen(ring, sector, nextRing, nextSector);
                if (!open) return true;
                ring = nextRing;
                sector = nextSector;
            }
            return false;
        }

        /// <summary>
        /// 스펙 046: 한 층 안에서 열린 칸만 지나 목표 칸으로 가는 가장 짧은 길의 첫 걸음을 찾는다.
        /// 출발 칸과 목표 칸의 고리와 화면 기준 섹터를 사용하며, 길이 있으면 true를 반환하고 다음 칸을 nextRing, nextSector에 쓴다.
        /// 두 칸이 다른 층이거나 같은 칸이거나 길이 없으면 false를 반환한다.
        /// </summary>
        public bool NextStepToward(int fromRing, int fromSector, int toRing, int toSector, out int nextRing, out int nextSector)
        {
            nextRing = fromRing;
            nextSector = fromSector;
            int layer = LayerOfRing(fromRing);
            if (layer != LayerOfRing(toRing) || (fromRing == toRing && fromSector == toSector)) return false;

            int firstRing = layer * _ringsPerLayer;
            int ringCount = Mathf.Min(_rings - 1, firstRing + _ringsPerLayer - 1) - firstRing + 1;
            // 목표 칸에서부터 거리를 채워 나가면 출발 칸의 이웃 가운데 거리가 가장 짧은 칸이 첫 걸음이다.
            int[,] distance = new int[ringCount, _sectors];
            for (int r = 0; r < ringCount; r++)
                for (int s = 0; s < _sectors; s++) distance[r, s] = -1;
            distance[toRing - firstRing, toSector] = 0;
            _searchQueue.Clear();
            _searchQueue.Enqueue((toRing, toSector));
            while (_searchQueue.Count > 0)
            {
                (int ring, int sector) = _searchQueue.Dequeue();
                int step = distance[ring - firstRing, sector] + 1;
                for (int n = 0; n < 4; n++)
                {
                    int neighborRing = ring + (n == 2 ? 1 : n == 3 ? -1 : 0);
                    int neighborSector = Mod(sector + (n == 0 ? 1 : n == 1 ? -1 : 0));
                    if (neighborRing < firstRing || neighborRing >= firstRing + ringCount) continue;
                    if (distance[neighborRing - firstRing, neighborSector] >= 0) continue;
                    if (!IsStepOpen(ring, sector, neighborRing, neighborSector)) continue;
                    distance[neighborRing - firstRing, neighborSector] = step;
                    _searchQueue.Enqueue((neighborRing, neighborSector));
                }
            }

            int best = distance[fromRing - firstRing, fromSector];
            if (best <= 0) return false;
            for (int n = 0; n < 4; n++)
            {
                int neighborRing = fromRing + (n == 2 ? 1 : n == 3 ? -1 : 0);
                int neighborSector = Mod(fromSector + (n == 0 ? 1 : n == 1 ? -1 : 0));
                if (neighborRing < firstRing || neighborRing >= firstRing + ringCount) continue;
                int d = distance[neighborRing - firstRing, neighborSector];
                if (d < 0 || d >= best || !IsStepOpen(fromRing, fromSector, neighborRing, neighborSector)) continue;
                best = d;
                nextRing = neighborRing;
                nextSector = neighborSector;
            }
            return nextRing != fromRing || nextSector != fromSector;
        }

        // 문 열쇠

        /// <summary>
        /// 스펙 046: 층 순서대로의 문 열쇠 플래그를 받아 지금 있는 문과 앞으로 만들 문에 적용한다.
        /// flags(층 사이 문 순서, 마지막은 동공의 문)를 사용하며, _gateKeyFlags와 모든 문의 열쇠 플래그를 바꾼다.
        /// </summary>
        public void SetGateKeyFlags(string[] flags)
        {
            _gateKeyFlags = flags ?? Array.Empty<string>();
            foreach (MazeGate gate in GetComponentsInChildren<MazeGate>(true)) gate.KeyFlag = KeyFlagOfGate(gate.Ring);
        }

        /// <summary>
        /// 스펙 046: 문이 있는 고리로 그 문의 열쇠 플래그를 찾는다. 가장 안쪽 고리의 문은 동공의 문이다.
        /// 고리 번호 ring과 _gateKeyFlags를 사용하며, 열쇠 플래그 또는 (정해지지 않았으면) 빈 글을 반환한다.
        /// </summary>
        public string KeyFlagOfGate(int ring)
        {
            int index = ring >= _rings - 1 ? LayerCount - 1 : ArcOwner(ring) - 1;
            return index >= 0 && index < _gateKeyFlags.Length ? _gateKeyFlags[index] ?? "" : "";
        }

        /// <summary>
        /// 스펙 046: 시선이 지나간 문을 기억한다. 벽을 다시 만들어 문 오브젝트가 바뀌어도 고리로 기억한다.
        /// 문이 있는 고리 ring을 사용하며, _passedGateRings를 바꾼다.
        /// </summary>
        public void MarkGatePassed(int ring) => _passedGateRings.Add(ring);

        /// <summary>
        /// 스펙 046: 그 고리의 안쪽 경계에 문이 있는지 판단한다.
        /// 고리 번호 ring을 사용하며, 켜져 있는 문이 있으면 true를 반환한다.
        /// </summary>
        public bool HasGateOnRing(int ring)
        {
            foreach (MazeGate gate in GetComponentsInChildren<MazeGate>(false))
                if (gate.Ring == ring) return true;
            return false;
        }

        /// <summary>
        /// 스펙 046: 그 고리의 문을 이미 지나갔는지 판단한다.
        /// 문이 있는 고리 ring을 사용하며, 지나갔으면 true를 반환한다.
        /// </summary>
        public bool IsGatePassed(int ring) => _passedGateRings.Contains(ring);

        /// <summary>
        /// 스펙 046: 지나간 문 기록을 지운다. 만남을 처음부터 다시 시작할 때 부른다.
        /// 입력값은 없으며, _passedGateRings와 _unlockedGateRings를 비운다.
        /// </summary>
        public void ClearPassedGates()
        {
            _passedGateRings.Clear();
            _unlockedGateRings.Clear();
        }

        /// <summary>
        /// 스펙 046 FR-045: 열쇠로 연 문을 기억한다. 벽을 다시 만들어 문 오브젝트가 바뀌어도 고리로 기억한다.
        /// 문이 있는 고리 ring을 사용하며, _unlockedGateRings를 바꾼다.
        /// </summary>
        public void MarkGateUnlocked(int ring) => _unlockedGateRings.Add(ring);

        /// <summary>
        /// 스펙 046 FR-045: 그 고리의 문을 이미 열쇠로 열었는지 판단한다.
        /// 문이 있는 고리 ring을 사용하며, 열었으면 true를 반환한다.
        /// </summary>
        public bool IsGateUnlocked(int ring) => _unlockedGateRings.Contains(ring);

        // 생성

        /// <summary>
        /// 층마다 벽을 새로 만든다. 기억 조각과 층 설정은 그대로 둔다.
        /// newSeed를 사용하며, keepLayer(0 이상)를 주면 그 층의 벽은 그대로 두고 나머지 층만 새로 만든다.
        /// </summary>
        public void Generate(int newSeed, int keepLayer = -1)
        {
            _seed = newSeed;
            foreach (MazeLayer layer in Layers)
            {
                if (layer.Index == keepLayer) continue;
                Transform old = layer.transform.Find(MazeLayer.WALL_CONTAINER_NAME);
                if (old != null)
                {
                    // Play 모드의 Destroy는 프레임 끝에 일어나므로, 같은 프레임의 벽 검사에 안 걸리게 먼저 떼어 낸다.
                    if (Application.isPlaying) { old.gameObject.SetActive(false); old.SetParent(null, false); }
                    EyeHooks.Destroy(old.gameObject);
                }
            }

            int sectorCount = _sectors;
            foreach (MazeLayer layer in Layers)
            {
                int layerIndex = layer.Index;
                if (layerIndex == keepLayer) continue;
                int r0 = layerIndex * _ringsPerLayer;
                int r1 = Mathf.Min(_rings - 1, r0 + _ringsPerLayer - 1);
                if (r0 > _rings - 1) continue;

                Transform container = new GameObject(MazeLayer.WALL_CONTAINER_NAME).transform;
                container.SetParent(layer.transform, false);

                var walls = new Dictionary<(MazeWall.Kind, int, int), MazeWall>();
                for (int r = r0; r <= r1; r++)
                {
                    for (int s = 0; s < sectorCount; s++)
                    {
                        walls[(MazeWall.Kind.Radial, r, s)] = MakeWall(container, MazeWall.Kind.Radial, r, s);
                        if (r < r1) walls[(MazeWall.Kind.Arc, r, s)] = MakeWall(container, MazeWall.Kind.Arc, r, s);
                    }
                }

                // 층 안은 무작위 깊이 우선 탐색으로 모든 칸이 이어진 미로를 만든다.
                var rng = new System.Random(_seed * 31 + layerIndex * 7 + 1);
                int rc = r1 - r0 + 1;
                bool[,] vis = new bool[rc, sectorCount];
                var stack = new Stack<(int r, int s)>();
                var start = (r: r0, s: rng.Next(sectorCount));
                vis[start.r - r0, start.s] = true;
                stack.Push(start);
                var nbrs = new List<(int r, int s, (MazeWall.Kind, int, int) key)>();
                while (stack.Count > 0)
                {
                    (int r, int s) cur = stack.Peek();
                    nbrs.Clear();
                    int sp = Mod(cur.s + 1);
                    int sm = Mod(cur.s - 1);
                    if (!vis[cur.r - r0, sp]) nbrs.Add((cur.r, sp, (MazeWall.Kind.Radial, cur.r, cur.s)));
                    if (!vis[cur.r - r0, sm]) nbrs.Add((cur.r, sm, (MazeWall.Kind.Radial, cur.r, sm)));
                    if (cur.r < r1 && !vis[cur.r + 1 - r0, cur.s]) nbrs.Add((cur.r + 1, cur.s, (MazeWall.Kind.Arc, cur.r, cur.s)));
                    if (cur.r > r0 && !vis[cur.r - 1 - r0, cur.s]) nbrs.Add((cur.r - 1, cur.s, (MazeWall.Kind.Arc, cur.r - 1, cur.s)));
                    if (nbrs.Count == 0) { stack.Pop(); continue; }
                    (int r, int s, (MazeWall.Kind, int, int) key) n = nbrs[rng.Next(nbrs.Count)];
                    walls[n.key].gameObject.SetActive(false);
                    vis[n.r - r0, n.s] = true;
                    stack.Push((n.r, n.s));
                }
                foreach (MazeWall w in walls.Values)
                    if (w.gameObject.activeSelf && rng.NextDouble() < _loopChance) w.gameObject.SetActive(false);

                // 바깥 층과의 경계는 이 층 소속이고 문이 하나 있다.
                if (layerIndex > 0)
                {
                    int rb = r0 - 1;
                    int gs = rng.Next(sectorCount);
                    for (int s = 0; s < sectorCount; s++)
                    {
                        MazeWall w = MakeWall(container, MazeWall.Kind.Arc, rb, s);
                        if (s == gs) w.gameObject.SetActive(false);
                    }
                    string locked = layerIndex == 1
                        ? "단단히 닫힌 문이다. 은재가 조금 더 마음을 열어야 한다."
                        : "더 깊은 곳은 아직 잠겨 있다.";
                    MakeGate(container, rb, gs, $"문 {layerIndex} (신뢰 {layerIndex} 필요)", layerIndex, -1f, locked);
                }

                // 동공과의 경계는 마지막 층 소속이다.
                if (r1 == _rings - 1)
                {
                    int ps = rng.Next(sectorCount);
                    for (int s = 0; s < sectorCount; s++)
                    {
                        MazeWall w = MakeWall(container, MazeWall.Kind.Arc, r1, s);
                        if (s == ps) w.gameObject.SetActive(false);
                    }
                    MakeGate(container, r1, ps, "동공 입구 (동공이 열려야 함)", -1, _pupilDilationRequired,
                        "동공이 좁아져 있다. 은재는 아직 진심을 보여줄 준비가 되지 않았다.");
                }

                EyeHooks.Created(container.gameObject);
            }
            EyeHooks.Repaint();
        }

        /// <summary>
        /// 벽 오브젝트 하나를 만든다.
        /// parent, kind, r, s를 사용하며, 만든 MazeWall을 반환한다.
        /// </summary>
        private MazeWall MakeWall(Transform parent, MazeWall.Kind kind, int r, int s)
        {
            var go = new GameObject($"{(kind == MazeWall.Kind.Arc ? "원호" : "방사")} r{r}-s{s}");
            go.transform.SetParent(parent, false);
            MazeWall w = go.AddComponent<MazeWall>();
            w.Setup(kind, r, s, Shapes.Ensure(go, _lineMaterial, 2));
            return w;
        }

        /// <summary>
        /// 문 오브젝트 하나를 만든다.
        /// parent, r, s, 이름, 필요한 신뢰와 동공, 잠김 문구를 사용하며, 만든 MazeGate를 반환한다.
        /// </summary>
        private MazeGate MakeGate(Transform parent, int r, int s, string gateName, int requiredTrust, float requiredDilation, string lockedMessage)
        {
            var go = new GameObject(gateName);
            go.transform.SetParent(parent, false);
            MazeGate g = go.AddComponent<MazeGate>();
            g.Setup(r, s, requiredTrust, requiredDilation, lockedMessage, Shapes.Ensure(go, _lineMaterial, 4));
            // 스펙 046: 새로 만든 문에도 그 자리의 열쇠 플래그를 넣음
            g.KeyFlag = KeyFlagOfGate(r);
            return g;
        }
    }
}
