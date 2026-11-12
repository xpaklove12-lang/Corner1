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
        // 스펙 015: 사물이 막은 길목을 돌아가는 길의 최대 길이 추가
        [Tooltip("사물이 막은 길목의 양쪽 칸을 잇는 우회로의 최대 칸 수. 이보다 길면 가까운 벽을 하나 열어 짧은 우회로를 만든다")]
        [SerializeField, Range(2, 12)] private int _maxDetourCells = 6;
        [SerializeField, FormerlySerializedAs("pupilDilationRequired"), Range(0f, 1f)] private float _pupilDilationRequired = 0.75f;

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("person")] private Person _person;

        // 스펙 015: 미로를 만들 때 길목이나 벽을 맡길 사물 목록
        [NonSerialized] private readonly List<MazeProp> _layoutProps = new List<MazeProp>();
        // 스펙 018: 층 문을 여는 플래그 목록. 0번이 1층으로 들어가는 문이다.
        [NonSerialized] private readonly List<string> _gateFlags = new List<string>();

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

        // 생성

        /// <summary>
        /// 스펙 018: 층 문을 여는 플래그 목록을 정한다. 다음 Generate부터 문을 만들 때 넣는다.
        /// flags(0번이 1층으로 들어가는 문)를 사용하며, _gateFlags를 바꾼다.
        /// </summary>
        public void SetGateFlags(IEnumerable<string> flags)
        {
            _gateFlags.Clear();
            _gateFlags.AddRange(flags);
        }

        /// <summary>
        /// 스펙 015: 미로를 만들 때 길목이나 벽을 맡길 사물을 정한다. 다음 Generate부터 적용된다.
        /// props를 사용하며, _layoutProps를 바꾼다.
        /// </summary>
        public void SetLayoutProps(IEnumerable<MazeProp> props)
        {
            _layoutProps.Clear();
            _layoutProps.AddRange(props);
        }

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
                    CollectNeighbors(cur, r0, r1, nbrs);
                    nbrs.RemoveAll(n => vis[n.r - r0, n.s]);
                    if (nbrs.Count == 0) { stack.Pop(); continue; }
                    (int r, int s, (MazeWall.Kind, int, int) key) n = nbrs[rng.Next(nbrs.Count)];
                    walls[n.key].gameObject.SetActive(false);
                    vis[n.r - r0, n.s] = true;
                    stack.Push((n.r, n.s));
                }
                foreach (MazeWall w in walls.Values)
                    if (w.gameObject.activeSelf && rng.NextDouble() < _loopChance) w.gameObject.SetActive(false);
                // 스펙 015: 이 층 사물에게 길목과 벽을 맡긴다.
                AssignProps(walls, r0, r1, layerIndex, rng);

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
                    // 스펙 018: 문 플래그가 있으면 선택지로 여는 문, 없으면 신뢰로 여는 문을 만든다.
                    string flag = layerIndex - 1 < _gateFlags.Count ? _gateFlags[layerIndex - 1] : "";
                    bool byFlag = !string.IsNullOrEmpty(flag);
                    MakeGate(container, rb, gs, byFlag ? $"문 {layerIndex} (선택지로 열림)" : $"문 {layerIndex} (신뢰 {layerIndex} 필요)",
                        byFlag ? -1 : layerIndex, -1f, locked, flag, layerIndex);
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
                        "동공이 좁아져 있다. 은재는 아직 진심을 보여줄 준비가 되지 않았다.", "", 0);
                }

                EyeHooks.Created(container.gameObject);
            }
            EyeHooks.Repaint();
        }

        /// <summary>
        /// 스펙 015: 층 안 칸 cur의 이웃 칸과 그 사이 벽의 키를 모은다. 같은 고리의 양옆과 같은 층 안의 안쪽, 바깥쪽 칸이다.
        /// cur, r0, r1(층의 첫, 끝 고리)을 사용하며, nbrs를 다시 채운다.
        /// </summary>
        private void CollectNeighbors((int r, int s) cur, int r0, int r1, List<(int r, int s, (MazeWall.Kind, int, int) key)> nbrs)
        {
            nbrs.Clear();
            int sp = Mod(cur.s + 1);
            int sm = Mod(cur.s - 1);
            nbrs.Add((cur.r, sp, (MazeWall.Kind.Radial, cur.r, cur.s)));
            nbrs.Add((cur.r, sm, (MazeWall.Kind.Radial, cur.r, sm)));
            if (cur.r < r1) nbrs.Add((cur.r + 1, cur.s, (MazeWall.Kind.Arc, cur.r, cur.s)));
            if (cur.r > r0) nbrs.Add((cur.r - 1, cur.s, (MazeWall.Kind.Arc, cur.r - 1, cur.s)));
        }

        /// <summary>
        /// 스펙 015: 이 층 사물에게 면을 맡긴다. 막음, 밀어냄, 다가가면 막음 사물은 기준 칸에 맞닿은 열린 면 가운데 길목 하나를 받는다. 가장 바깥 고리와 맞닿은 원호 면은 받지 않는다.
        /// 막아도 그 길목의 양쪽 칸이 _maxDetourCells칸 안의 우회로로 이어지는 길목만 고르고, 없으면 기준 칸의 열린 면 하나를 고른 뒤 벽 하나를 열어 짧은 우회로를 만들고 맡긴다. 길 냄 사물은 기준 칸에 맞닿은 닫힌 벽 하나를 받고, 없으면 같은 층에서 두 칸 거리까지 찾는다.
        /// walls, r0, r1, layerIndex, rng와 _layoutProps를 사용하며, 사물의 맡은 면과 벽의 활성 상태를 바꾼다.
        /// </summary>
        private void AssignProps(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, int r0, int r1, int layerIndex, System.Random rng)
        {
            var mine = _layoutProps.Where(p => p != null && LayerOfRing(p.Ring) == layerIndex)
                .OrderBy(p => p.Ring).ThenBy(p => Mod(p.Sector)).ThenBy(p => p.name).ToList();
            foreach (MazeProp p in mine) p.ClearPassage();
            var blocked = new HashSet<(MazeWall.Kind, int, int)>();
            var used = new HashSet<(MazeWall.Kind, int, int)>();
            var assigned = new List<(MazeWall.Kind, int, int)>();
            foreach (MazeProp p in mine)
            {
                if (p.Kind == MazePropKind.OpenPath) continue;
                List<(MazeWall.Kind, int, int)> faces = FacesOf((p.Ring, Mod(p.Sector)), r0, r1, walls, rng);
                faces.RemoveAll(f => walls[f].gameObject.activeSelf || used.Contains(f));
                // 스펙 015: 가장 바깥 고리와 맞닿은 원호 면은 맡기지 않음. 그림이 호 길이에 맞춰 커져 바깥 통로를 덮기 때문이다.
                faces.RemoveAll(f => f.Item1 == MazeWall.Kind.Arc && f.Item2 == 0);
                // 막아도 바로 양쪽 칸이 짧은 우회로로 이어지는 길목을 고른다. 여럿이면 우회로가 가장 짧은 길목이다.
                (MazeWall.Kind, int, int)? pick = null;
                int bestLen = int.MaxValue;
                foreach ((MazeWall.Kind, int, int) f in faces)
                {
                    blocked.Add(f);
                    int len = DetourLength(walls, blocked, f, r0, r1);
                    // 앞서 맡긴 길목의 우회로를 끊는 길목은 고르지 않는다.
                    bool keepsOthers = assigned.All(g => DetourLength(walls, blocked, g, r0, r1) <= _maxDetourCells);
                    blocked.Remove(f);
                    if (keepsOthers && len <= _maxDetourCells && len < bestLen) { bestLen = len; pick = f; }
                }
                if (pick == null && faces.Count > 0)
                {
                    // 가까운 우회로가 없으면 벽을 열어 이 길목과 앞서 맡긴 길목 모두의 우회로를 짧게 만든다.
                    pick = PickWithShortDetour(walls, blocked, used, assigned, faces, r0, r1, rng);
                }
                if (pick == null)
                {
                    Debug.LogWarning($"[미로] {p.name}에게 맡길 길목이 없다. 기준 칸 가운데에 놓이고 길을 막지 않는다.", p);
                    continue;
                }
                blocked.Add(pick.Value);
                used.Add(pick.Value);
                assigned.Add(pick.Value);
                p.AssignPassage(pick.Value.Item1, pick.Value.Item2, pick.Value.Item3, null);
            }
            foreach (MazeProp p in mine)
            {
                if (p.Kind != MazePropKind.OpenPath) continue;
                // 기준 칸에 닫힌 벽이 없으면 같은 층에서 한두 칸 떨어진 칸까지 찾는다.
                var faces = new List<(MazeWall.Kind, int, int)>();
                for (int reach = 0; reach <= 2 && faces.Count == 0; reach++)
                    for (int r = Mathf.Max(r0, p.Ring - reach); r <= Mathf.Min(r1, p.Ring + reach); r++)
                        for (int ds = -reach; ds <= reach; ds++)
                        {
                            if (Mathf.Abs(r - p.Ring) + Mathf.Abs(ds) != reach) continue;
                            foreach ((MazeWall.Kind, int, int) f in FacesOf((r, Mod(p.Sector + ds)), r0, r1, walls, rng))
                                if (walls[f].gameObject.activeSelf && !used.Contains(f) && !faces.Contains(f)) faces.Add(f);
                        }
                if (faces.Count == 0)
                {
                    Debug.LogWarning($"[미로] {p.name}에게 맡길 닫힌 벽이 없다. 기준 칸 가운데에 놓이고 길을 열지 않는다.", p);
                    continue;
                }
                used.Add(faces[0]);
                p.AssignPassage(faces[0].Item1, faces[0].Item2, faces[0].Item3, walls[faces[0]]);
            }
        }

        /// <summary>
        /// 스펙 015: 칸에 맞닿은 층 안의 면(양옆 방사 벽, 같은 층 안의 위아래 원호 벽)을 rng 순서로 섞어 모은다. rng가 null이면 섞지 않는다.
        /// cell, r0, r1, walls, rng를 사용하며, 벽 키 목록을 반환한다.
        /// </summary>
        private List<(MazeWall.Kind, int, int)> FacesOf((int r, int s) cell, int r0, int r1, Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, System.Random rng)
        {
            var faces = new List<(MazeWall.Kind, int, int)>
            {
                (MazeWall.Kind.Radial, cell.r, cell.s),
                (MazeWall.Kind.Radial, cell.r, Mod(cell.s - 1)),
            };
            if (cell.r < r1) faces.Add((MazeWall.Kind.Arc, cell.r, cell.s));
            if (cell.r > r0) faces.Add((MazeWall.Kind.Arc, cell.r - 1, cell.s));
            faces.RemoveAll(f => !walls.ContainsKey(f));
            if (rng == null) return faces;
            for (int i = faces.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (faces[i], faces[j]) = (faces[j], faces[i]);
            }
            return faces;
        }

        /// <summary>
        /// 스펙 015: 벽 키가 잇는 두 칸을 구한다. 원호 벽은 고리 r과 r+1, 방사 벽은 칸 s와 s+1 사이다.
        /// key를 사용하며, 두 칸을 돌려준다.
        /// </summary>
        private ((int r, int s) a, (int r, int s) b) CellsOf((MazeWall.Kind kind, int r, int s) key)
        {
            return key.kind == MazeWall.Kind.Arc ? ((key.r, key.s), (key.r + 1, key.s)) : ((key.r, key.s), (key.r, Mod(key.s + 1)));
        }

        /// <summary>
        /// 스펙 015: from 칸에서 열린 면(꺼진 벽)으로만 이동해 닿는 층 안의 칸과 그 거리를 구한다. blocked의 면은 지나지 않는다.
        /// walls, blocked, from, r0, r1을 사용하며, 칸별 거리 사전을 반환한다.
        /// </summary>
        private Dictionary<(int r, int s), int> Reach(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, HashSet<(MazeWall.Kind, int, int)> blocked, (int r, int s) from, int r0, int r1)
        {
            var dist = new Dictionary<(int r, int s), int> { [from] = 0 };
            var queue = new Queue<(int r, int s)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                (int r, int s) c = queue.Dequeue();
                foreach ((MazeWall.Kind, int, int) f in FacesOf(c, r0, r1, walls, null))
                {
                    if (walls[f].gameObject.activeSelf || blocked.Contains(f)) continue;
                    ((int r, int s) a, (int r, int s) b) = CellsOf(f);
                    (int r, int s) next = a == c ? b : a;
                    if (dist.ContainsKey(next)) continue;
                    dist[next] = dist[c] + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        /// <summary>
        /// 스펙 015: 막은 길목 face의 양쪽 칸이 다른 열린 면으로 몇 칸 만에 이어지는지 구한다. blocked의 면은 지나지 않는다.
        /// walls, blocked, face, r0, r1을 사용하며, 이동 칸 수를 반환한다. 이어지지 않으면 int.MaxValue다.
        /// </summary>
        private int DetourLength(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, HashSet<(MazeWall.Kind, int, int)> blocked, (MazeWall.Kind, int, int) face, int r0, int r1)
        {
            ((int r, int s) a, (int r, int s) b) = CellsOf(face);
            return Reach(walls, blocked, a, r0, r1).TryGetValue(b, out int d) ? d : int.MaxValue;
        }

        /// <summary>
        /// 스펙 015: 가까운 우회로가 없을 때 길목과 열 벽을 함께 고른다. 후보 길목마다 닫힌 벽을 하나씩 열어 보고, 이 길목과 앞서 맡긴 길목들의 우회로 가운데 가장 긴 것이 가장 짧아지는 조합을 고른다.
        /// 그래도 _maxDetourCells보다 길면 같은 방법으로 벽을 세 개까지 더 연다. 다른 사물이 맡은 면(used)은 열지 않는다.
        /// walls, blocked, used, assigned, faces, r0, r1, rng를 사용하며, 고른 길목을 반환하고 벽의 활성 상태를 바꾼다. 우회로를 만들 수 없으면 null을 반환한다.
        /// </summary>
        private (MazeWall.Kind, int, int)? PickWithShortDetour(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, HashSet<(MazeWall.Kind, int, int)> blocked,
            HashSet<(MazeWall.Kind, int, int)> used, List<(MazeWall.Kind, int, int)> assigned, List<(MazeWall.Kind, int, int)> faces, int r0, int r1, System.Random rng)
        {
            (MazeWall.Kind, int, int) bestFace = faces[0];
            (MazeWall.Kind, int, int)? bestWall = null;
            int bestScore = int.MaxValue;
            foreach ((MazeWall.Kind, int, int) f in faces)
            {
                blocked.Add(f);
                foreach (KeyValuePair<(MazeWall.Kind, int, int), MazeWall> pair in walls)
                {
                    if (!pair.Value.gameObject.activeSelf || blocked.Contains(pair.Key) || used.Contains(pair.Key)) continue;
                    pair.Value.gameObject.SetActive(false);
                    int worst = WorstDetour(walls, blocked, assigned, f, r0, r1);
                    pair.Value.gameObject.SetActive(true);
                    // 우회로가 없는 조합은 고르지 않는다.
                    if (worst == int.MaxValue) continue;
                    int score = worst * 4 + rng.Next(4);
                    if (score < bestScore) { bestScore = score; bestFace = f; bestWall = pair.Key; }
                }
                blocked.Remove(f);
            }
            // 어떤 벽을 열어도 우회로가 생기지 않으면 길목을 맡기지 않는다.
            if (bestWall == null) return null;
            walls[bestWall.Value].gameObject.SetActive(false);

            // 벽 하나로 부족하면 같은 길목에서 벽을 세 개까지 더 연다.
            blocked.Add(bestFace);
            for (int more = 0; more < 3 && WorstDetour(walls, blocked, assigned, bestFace, r0, r1) > _maxDetourCells; more++)
            {
                (MazeWall.Kind, int, int)? extra = null;
                int extraScore = int.MaxValue;
                foreach (KeyValuePair<(MazeWall.Kind, int, int), MazeWall> pair in walls)
                {
                    if (!pair.Value.gameObject.activeSelf || blocked.Contains(pair.Key) || used.Contains(pair.Key)) continue;
                    pair.Value.gameObject.SetActive(false);
                    int worst = WorstDetour(walls, blocked, assigned, bestFace, r0, r1);
                    pair.Value.gameObject.SetActive(true);
                    if (worst == int.MaxValue) continue;
                    int score = worst * 4 + rng.Next(4);
                    if (score < extraScore) { extraScore = score; extra = pair.Key; }
                }
                if (extra == null) break;
                walls[extra.Value].gameObject.SetActive(false);
            }
            blocked.Remove(bestFace);
            return bestFace;
        }

        /// <summary>
        /// 스펙 015: face와 앞서 맡긴 길목들의 우회로 가운데 가장 긴 칸 수를 구한다. blocked의 면은 지나지 않는다.
        /// walls, blocked, assigned, face, r0, r1을 사용하며, 가장 긴 우회로 칸 수를 반환한다.
        /// </summary>
        private int WorstDetour(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, HashSet<(MazeWall.Kind, int, int)> blocked, List<(MazeWall.Kind, int, int)> assigned, (MazeWall.Kind, int, int) face, int r0, int r1)
        {
            int worst = DetourLength(walls, blocked, face, r0, r1);
            foreach ((MazeWall.Kind, int, int) g in assigned) worst = Mathf.Max(worst, DetourLength(walls, blocked, g, r0, r1));
            return worst;
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
        /// parent, r, s, 이름, 필요한 신뢰와 동공, 잠김 문구, 여는 플래그, 문 번호를 사용하며, 만든 MazeGate를 반환한다.
        /// </summary>
        private MazeGate MakeGate(Transform parent, int r, int s, string gateName, int requiredTrust, float requiredDilation, string lockedMessage, string requiredFlag, int order)
        {
            var go = new GameObject(gateName);
            go.transform.SetParent(parent, false);
            MazeGate g = go.AddComponent<MazeGate>();
            g.Setup(r, s, requiredTrust, requiredDilation, lockedMessage, Shapes.Ensure(go, _lineMaterial, 4), requiredFlag, order);
            return g;
        }
    }
}
