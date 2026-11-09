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
            return g;
        }
    }
}
