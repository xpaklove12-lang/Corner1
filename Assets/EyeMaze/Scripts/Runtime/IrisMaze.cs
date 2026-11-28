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

        [Header("상처 무늬")]
        [Tooltip("드러난 상처(숨은 무늬 벽)의 색. 보통 벽과 구분되게 칠한다")]
        [SerializeField] private Color _hiddenWallColor = new Color(0.95f, 0.35f, 0.42f, 1f);
        [Tooltip("드러난 상처의 굵기 배율 (보통 벽 = 1)")]
        [SerializeField] private float _hiddenWallWidthScale = 1.6f;
        [Tooltip("상처가 드러나는 순간 밝게 번쩍였다가 가라앉는 시간(초)")]
        [SerializeField] private float _hiddenWallFlashSeconds = 1.2f;
        [Tooltip("상처가 다 드러났을 때 보통 벽의 밝기. 낮을수록 상처 무늬가 또렷하게 떠오른다")]
        [SerializeField, Range(0f, 1f)] private float _revealedNormalWallAlpha = 0.35f;
        [Tooltip("파편을 읽어 새 상처가 생길 때, 금 하나가 끝까지 번지는 시간(초)")]
        [SerializeField] private float _scarCarveSeconds = 0.5f;
        [Tooltip("새 상처가 동공 쪽부터 가장 바깥까지 번져 나가는 데 걸리는 시간(초)")]
        [SerializeField] private float _scarSpreadSeconds = 0.8f;
        [Tooltip("새겨진 상처가 다 그려진 뒤 보이는 채로 머무는 시간(초)")]
        [SerializeField] private float _scarGlimpseSeconds = 0.6f;
        [Tooltip("머문 뒤 상처가 다시 보이지 않게 가라앉는 시간(초)")]
        [SerializeField] private float _scarFadeSeconds = 0.8f;
        [Tooltip("부딪혀 찾아낸 상처가 금 가듯 그려지는 시간(초)")]
        [SerializeField] private float _scarDiscoverSeconds = 0.25f;

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("person")] private Person _person;

        // 층 문을 여는 플래그 목록. 0번이 1층으로 들어가는 문이다.
        [NonSerialized] private readonly List<string> _gateFlags = new List<string>();
        // 손님의 홍채 무늬 장 목록. 파편을 읽을 때마다 한 장씩 숨은 벽으로 막힌다.
        [NonSerialized] private readonly List<IrisPatternSheet> _patternSheets = new List<IrisPatternSheet>();
        // 무늬가 모두 막히기까지의 단계 수와 지금 단계
        [NonSerialized] private int _complexityStages;
        [NonSerialized] private int _complexityStage;
        [NonSerialized] private float _hiddenWallAlpha;
        // 지금 미로에 무늬 벽이 있는지
        [NonSerialized] private bool _hasPattern;
        // 마지막으로 알린 무늬 경고. 벽을 다시 만들 때마다 같은 경고를 반복하지 않는다.
        [NonSerialized] private string _lastPatternWarning = "";
        // 부딪혀 찾아낸 상처 자리 (종류, 고리, 층 기준 칸). 층을 다시 만들어도 남는다.
        [NonSerialized] private readonly HashSet<(MazeWall.Kind, int, int)> _discovered = new HashSet<(MazeWall.Kind, int, int)>();

        public int Rings => _rings;
        public int Sectors => _sectors;
        public float OuterRadius => _outerRadius;
        public float InnerRadius => _innerRadius;
        public int Seed => _seed;
        public float WallAlpha { get => _wallAlpha; set => _wallAlpha = value; }
        public float Focus { get => _focus; set => _focus = value; }
        public Person Person => _person;
        /// <summary>켜져 있으면 조건과 상관없이 모든 층 문을 지나갈 수 있다. 테스트 씬에서 쓴다.</summary>
        public bool OpenAllGates { get; set; }
        /// <summary>무늬 장이 모두 막히기까지의 단계 수. 다음 Generate부터 무늬 장을 이 수만큼 나눠 막는다. 0이면 무늬를 쓰지 않는다.</summary>
        public int ComplexityStages { get => _complexityStages; set => _complexityStages = Mathf.Max(0, value); }
        /// <summary>지금 복잡도 단계. 이 단계 이하에서 막히는 무늬 벽이 켜져 있다.</summary>
        public int ComplexityStage => _complexityStage;
        /// <summary>무늬 벽(숨은 벽)의 표시 투명도(0~1). 판정에는 쓰지 않는다.</summary>
        public float HiddenWallAlpha { get => _hiddenWallAlpha; set => _hiddenWallAlpha = Mathf.Clamp01(value); }
        /// <summary>보통 벽에 곱하는 밝기. 무늬가 있으면 숨은 벽이 드러나는 만큼 어두워져 무늬가 떠오른다.</summary>
        public float NormalWallAlpha => _hasPattern ? Mathf.Lerp(1f, _revealedNormalWallAlpha, _hiddenWallAlpha) : 1f;
        // 연출 오브젝트가 같은 선 재질을 쓰도록 공개
        public Material LineMaterial => _lineMaterial;

        public float FocusAlpha => Mathf.Lerp(0.12f, 1f, _focus * _focus);
        public Color WallColor => new Color(_wallColorBase.r, _wallColorBase.g, _wallColorBase.b, _wallColorBase.a * _wallAlpha * FocusAlpha);
        /// <summary>드러난 숨은 벽의 색. 보통 벽처럼 모드 투명도와 초점을 곱한다.</summary>
        public Color HiddenWallColor => new Color(_hiddenWallColor.r, _hiddenWallColor.g, _hiddenWallColor.b, _hiddenWallColor.a * _wallAlpha * FocusAlpha);
        public float HiddenWallWidthScale => _hiddenWallWidthScale;
        public float HiddenWallFlashSeconds => _hiddenWallFlashSeconds;
        public float ScarGlimpseSeconds => _scarGlimpseSeconds;
        public float ScarFadeSeconds => _scarFadeSeconds;
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
        /// ring r 칸의 안쪽 원호 자리에 켜져 있는 벽을 찾는다.
        /// r, worldSector를 사용하며, 켜진 MazeWall 또는 null을 반환한다.
        /// </summary>
        public MazeWall ArcWallAt(int r, int worldSector)
        {
            MazeWall w = FindWall(MazeWall.Kind.Arc, r, Mod(worldSector - Offset(ArcOwner(r))));
            return w != null && w.gameObject.activeSelf ? w : null;
        }

        /// <summary>
        /// ring r에서 worldBoundary 칸과 그 다음 칸 사이에 켜져 있는 벽을 찾는다.
        /// r, worldBoundary를 사용하며, 켜진 MazeWall 또는 null을 반환한다.
        /// </summary>
        public MazeWall RadialWallAt(int r, int worldBoundary)
        {
            MazeWall w = FindWall(MazeWall.Kind.Radial, r, Mod(worldBoundary - Offset(LayerOfRing(r))));
            return w != null && w.gameObject.activeSelf ? w : null;
        }

        // 상처

        /// <summary>
        /// 시선이 부딪힌 숨은 상처를 찾아낸 것으로 기록하고, 금이 번지듯 드러낸다. 찾아낸 상처는 리셋 전까지 보인다.
        /// 층을 다시 만들어도 같은 자리의 상처가 다시 보이도록 자리로 기억한다.
        /// w를 사용하며, 새로 찾아냈으면 true를 반환하고 _discovered와 벽의 표시 상태를 바꾼다.
        /// </summary>
        public bool Discover(MazeWall w)
        {
            if (w == null || !w.IsHidden || w.Discovered) return false;
            _discovered.Add((w.WallKind, w.Ring, w.Sector));
            w.Discovered = true;
            w.Carve(0f, _scarDiscoverSeconds);
            w.Flash();
            return true;
        }

        /// <summary>
        /// 찾아낸 상처 기록을 모두 지우고 다시 숨긴다. 리셋할 때 쓴다.
        /// 입력값은 없으며, _discovered와 벽의 Discovered를 바꾼다.
        /// </summary>
        public void ClearDiscovered()
        {
            _discovered.Clear();
            foreach (MazeWall w in GetComponentsInChildren<MazeWall>(true)) w.Discovered = false;
        }

        /// <summary>
        /// 상처가 새겨질 때 동공에서 바깥으로 번져 나가도록, 벽 자리에 따라 새김을 늦출 시간을 계산한다.
        /// w의 고리와 _scarSpreadSeconds를 사용하며, 늦출 초를 반환한다.
        /// </summary>
        private float CarveDelay(MazeWall w)
        {
            float t = Mathf.InverseLerp(_innerRadius, _outerRadius, RingMid(w.Ring));
            return t * _scarSpreadSeconds;
        }

        // 생성

        /// <summary>
        /// 층 문을 여는 플래그 목록을 정한다. 다음 Generate부터 문을 만들 때 넣는다.
        /// flags(0번이 1층으로 들어가는 문)를 사용하며, _gateFlags를 바꾼다.
        /// </summary>
        public void SetGateFlags(IEnumerable<string> flags)
        {
            _gateFlags.Clear();
            _gateFlags.AddRange(flags);
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
            var warnings = new List<string>();
            // 다시 만들지 않고 남겨 둔 층의 무늬도 무늬로 친다.
            _hasPattern = keepLayer >= 0 && GetComponentsInChildren<MazeWall>(true).Any(w => w.IsHidden);
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

                // 홍채 무늬 벽 자리와 막히는 단계를 먼저 정한다.
                Dictionary<(MazeWall.Kind, int, int), int> pattern = PlanPattern(walls, layerIndex, r0, r1, warnings);

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
                    // 무늬 벽 자리는 뚫지 않는다. 무늬는 층이 끊기지 않게 골라 두었으므로 모든 칸에 닿는다.
                    nbrs.RemoveAll(n => vis[n.r - r0, n.s] || pattern.ContainsKey(n.key));
                    if (nbrs.Count == 0) { stack.Pop(); continue; }
                    (int r, int s, (MazeWall.Kind, int, int) key) n = nbrs[rng.Next(nbrs.Count)];
                    walls[n.key].gameObject.SetActive(false);
                    vis[n.r - r0, n.s] = true;
                    stack.Push((n.r, n.s));
                }
                foreach (MazeWall w in walls.Values)
                    if (w.gameObject.activeSelf && !pattern.ContainsKey((w.WallKind, w.Ring, w.Sector)) && rng.NextDouble() < _loopChance) w.gameObject.SetActive(false);
                // 무늬 벽은 정한 단계에서 막히는 숨은 벽이 된다.
                foreach (KeyValuePair<(MazeWall.Kind, int, int), int> p in pattern)
                {
                    walls[p.Key].CloseAtStage = p.Value;
                    // 다시 만든 층에서도 이미 찾아낸 상처는 보인다.
                    walls[p.Key].Discovered = _discovered.Contains(p.Key);
                }
                if (pattern.Count > 0) _hasPattern = true;

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
                    // 문 플래그가 있으면 선택지로 여는 문, 없으면 신뢰로 여는 문을 만든다.
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
            ApplyComplexity();
            ReportPatternWarnings(warnings);
            EyeHooks.Repaint();
        }

        /// <summary>
        /// 홍채 무늬 장 목록을 정한다. 다음 Generate부터 무늬 벽으로 쓴다.
        /// sheets를 사용하며, _patternSheets를 바꾼다.
        /// </summary>
        public void SetPatternSheets(IEnumerable<IrisPatternSheet> sheets)
        {
            _patternSheets.Clear();
            if (sheets != null) _patternSheets.AddRange(sheets.Where(sheet => sheet != null));
        }

        /// <summary>
        /// 복잡도 단계를 바꾸고, 그 단계 이하에서 막히는 무늬 벽을 켠다.
        /// stage를 사용하며, _complexityStage와 무늬 벽의 활성 상태를 바꾼다.
        /// </summary>
        public void SetComplexityStage(int stage)
        {
            _complexityStage = Mathf.Max(0, stage);
            ApplyComplexity();
        }

        /// <summary>
        /// 무늬 벽마다 지금 단계에서 막혀 있어야 하는지 보고 켜거나 끈다. 처음부터 있던 벽은 건드리지 않는다.
        /// _complexityStage와 벽의 CloseAtStage를 사용하며, 무늬 벽의 활성 상태를 바꾼다.
        /// </summary>
        private void ApplyComplexity()
        {
            foreach (MazeWall w in GetComponentsInChildren<MazeWall>(true))
            {
                if (!w.IsHidden) continue;
                bool closed = _complexityStage >= w.CloseAtStage;
                if (w.gameObject.activeSelf == closed) continue;
                w.gameObject.SetActive(closed);
                // 새로 생긴 상처는 동공에서 바깥으로 금이 번지듯 새겨지고, 잠깐 보였다가 가라앉는다.
                if (closed && Application.isPlaying) w.Carve(CarveDelay(w), _scarCarveSeconds);
            }
        }

        /// <summary>
        /// 지금 막혀 있는 숨은 벽을 모두 번쩍이게 한다. 의심이 풀려 숨은 벽이 드러나는 순간에 쓴다.
        /// 입력값은 없으며, 켜진 숨은 벽의 번쩍임 시작 시각을 바꾼다.
        /// </summary>
        public void FlashHiddenWalls()
        {
            foreach (MazeWall w in GetComponentsInChildren<MazeWall>(false))
                if (w.IsHidden) w.Flash();
        }

        /// <summary>
        /// 이 층에 들어가는 홍채 무늬 벽 자리와 막히는 단계를 정한다. 장 순서대로 벽을 더하되, 더하면 층 안 칸이 끊기는 벽은 빼고 warnings에 적는다.
        /// 장 i는 1 + i * 단계 수 / 장 수 단계에 막혀, 장이 단계 수로 고르게 나뉜다.
        /// walls(층 안 벽), layerIndex, r0, r1(층의 첫, 끝 고리), _patternSheets, _complexityStages를 사용하며, 벽 자리별 막히는 단계를 반환한다.
        /// </summary>
        private Dictionary<(MazeWall.Kind, int, int), int> PlanPattern(Dictionary<(MazeWall.Kind, int, int), MazeWall> walls, int layerIndex, int r0, int r1, List<string> warnings)
        {
            var pattern = new Dictionary<(MazeWall.Kind, int, int), int>();
            if (_complexityStages <= 0 || _patternSheets.Count == 0) return pattern;
            var keys = new List<(MazeWall.Kind kind, int ring, int sector)>();
            var closed = new HashSet<(MazeWall.Kind, int, int)>();
            for (int i = 0; i < _patternSheets.Count; i++)
            {
                int stage = 1 + i * _complexityStages / _patternSheets.Count;
                keys.Clear();
                _patternSheets[i].CollectWalls(_sectors, keys);
                int dropped = 0;
                foreach ((MazeWall.Kind kind, int ring, int sector) k in keys)
                {
                    // 다른 층의 벽은 이 층의 무늬가 아니다.
                    if (k.ring < r0 || k.ring > r1) continue;
                    var key = (k.kind, k.ring, k.sector);
                    // 층 안에 없는 자리는 층 경계 원호뿐이다. 경계는 문 자리라 무늬로 쓰지 않는다.
                    if (!walls.ContainsKey(key)) { dropped++; continue; }
                    if (pattern.ContainsKey(key)) continue;
                    closed.Add(key);
                    if (!IsLayerConnected(r0, r1, closed)) { closed.Remove(key); dropped++; continue; }
                    pattern[key] = stage;
                }
                if (dropped > 0) warnings.Add($"'{_patternSheets[i].Name}' 장의 벽 {dropped}개를 {layerIndex}층에서 뺐다 (층 경계 원호이거나, 막으면 칸이 갇힘).");
            }
            return pattern;
        }

        /// <summary>
        /// closed 자리가 모두 막혀도 층 안의 모든 칸이 이어져 있는지 확인한다.
        /// r0, r1(층의 첫, 끝 고리)과 closed를 사용하며, 이어져 있으면 true를 반환한다.
        /// </summary>
        private bool IsLayerConnected(int r0, int r1, HashSet<(MazeWall.Kind, int, int)> closed)
        {
            int cellCount = (r1 - r0 + 1) * _sectors;
            bool[] seen = new bool[cellCount];
            var queue = new Queue<(int r, int s)>();
            var nbrs = new List<(int r, int s, (MazeWall.Kind, int, int) key)>();
            seen[0] = true;
            queue.Enqueue((r0, 0));
            int reached = 1;
            while (queue.Count > 0)
            {
                CollectNeighbors(queue.Dequeue(), r0, r1, nbrs);
                foreach ((int r, int s, (MazeWall.Kind, int, int) key) n in nbrs)
                {
                    int index = (n.r - r0) * _sectors + n.s;
                    if (seen[index] || closed.Contains(n.key)) continue;
                    seen[index] = true;
                    reached++;
                    queue.Enqueue((n.r, n.s));
                }
            }
            return reached == cellCount;
        }

        /// <summary>
        /// 무늬 장에서 뺀 벽을 콘솔에 알린다. 지난번과 같은 내용이면 다시 알리지 않는다.
        /// warnings와 _lastPatternWarning을 사용하며, _lastPatternWarning을 바꾼다.
        /// </summary>
        private void ReportPatternWarnings(List<string> warnings)
        {
            string text = string.Join("\n", warnings);
            if (text == _lastPatternWarning) return;
            _lastPatternWarning = text;
            if (text.Length > 0) Debug.LogWarning($"[홍채 무늬] {text}", this);
        }

        /// <summary>
        /// 층 안 칸 cur의 이웃 칸과 그 사이 벽의 키를 모은다. 같은 고리의 양옆과 같은 층 안의 안쪽, 바깥쪽 칸이다.
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
