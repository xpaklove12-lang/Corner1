using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>미로 사물의 종류</summary>
    public enum MazePropKind
    {
        [InspectorName("막음")] Block,
        [InspectorName("밀어냄")] Push,
        [InspectorName("길 냄")] OpenPath,
        [InspectorName("다가가면 막음")] FlipBlock,
    }

    /// <summary>
    /// 미로에 놓이는 사물. 인물의 사물 묶음(ClientCase._propGroups) 아래에 두면 그 층과 함께 회전한다.
    /// 막는 사물은 미로를 만들 때 기준 칸 주변의 길목(두 칸 사이의 면) 하나를 맡아 그 면 가운데에 놓이고, 길 냄 사물은 닫힌 벽 하나를 맡아 보이는 동안 그 벽을 연다.
    /// 보일 감정 목록이 비어 있으면 감정과 무관하게 항상 보인다. 표시 여부는 EyeMazeDirector가 정한다. 에디트 모드에서는 항상 보이게 그린다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class MazeProp : MonoBehaviour
    {
        private const int SORTING_ORDER = 6;
        private const float MIN_SECONDS = 0.01f;
        private const int STAFF_LINES = 5;
        private const float STAFF_LENGTH = 0.3f;
        private const float STAFF_WIDTH = 0.02f;
        private const float OPEN_PATH_SPRITE_RATIO = 0.35f;

        [Header("종류")]
        [SerializeField] private MazePropKind _kind = MazePropKind.Block;

        [Header("위치")]
        [Tooltip("기준 고리. 길목이나 벽은 이 칸에 맞닿은 면 가운데서 정해진다")]
        [SerializeField] private int _ring;
        [Tooltip("기준 칸 (자기 층 기준 sector)")]
        [SerializeField] private int _sector;

        [Header("감정")]
        [Tooltip("이 사물이 보일 감정 목록. 비우면 항상 보인다")]
        [SerializeField] private ClientEmotion[] _showIn = new ClientEmotion[0];

        [Header("그림")]
        [SerializeField] private Sprite _sprite;

        [Header("길목")]
        [Tooltip("맡은 면의 통로 폭에 맞춰 그림을 돌리고 크기를 맞출지. 끄면 회전과 크기는 씬 값을 쓴다")]
        [SerializeField] private bool _fitToPassage;
        [Tooltip("그림 가로 길이를 통로 폭의 몇 배로 맞출지")]
        [SerializeField, Range(0.3f, 1.5f)] private float _passageFill = 0.9f;
        // 스펙 015: 그림이 옆 통로를 덮지 않도록 두께 상한과 막힌 범위 표시선 추가
        [Tooltip("그림 두께(통로를 따라가는 방향)의 상한. 고리 폭의 몇 배까지 허용할지")]
        [SerializeField, Range(0.2f, 1f)] private float _thicknessFill = 0.6f;
        [Tooltip("막고 있는 동안 막힌 길목 전체에 긋는 선의 색")]
        [SerializeField] private Color _barrierColor = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("막힌 길목에 긋는 선의 굵기")]
        [SerializeField] private float _barrierWidth = 0.025f;

        [Header("길 냄")]
        [Tooltip("열린 벽 자리에 그리는 오선 색")]
        [SerializeField] private Color _staffColor = new Color(1f, 1f, 1f, 0.8f);

        [Header("다가가면 막음")]
        [Tooltip("시선이 이 거리 안으로 들어오면 뒤집히며 길목을 막는다")]
        [SerializeField] private float _flipDistance = 0.6f;
        [Tooltip("뒤집히는 시간(초)")]
        [SerializeField] private float _flipSeconds = 0.35f;
        [Tooltip("뒤집히기 전 크기 배율")]
        [SerializeField, Range(0.1f, 1f)] private float _idleScale = 0.5f;
        [Tooltip("뒤집힌 뒤 그림. 비우면 원래 그림에 아래 색을 곱한다")]
        [SerializeField] private Sprite _flippedSprite;
        [Tooltip("뒤집힌 그림이 없을 때 곱할 색")]
        [SerializeField] private Color _flippedTint = new Color(0.45f, 0.45f, 0.5f, 1f);

        [Header("어지러움")]
        [Tooltip("시선이 이 거리 안으로 오면 주변 벽이 흔들린다. 0이면 끈다")]
        [SerializeField] private float _dizzyRadius;
        [Tooltip("벽 흔들림 최대 폭")]
        [SerializeField] private float _dizzyAmplitude = 0.05f;
        [Tooltip("벽 흔들림 빠르기")]
        [SerializeField] private float _dizzySpeed = 6f;

        // 스펙 015 FR-020: 막혔을 때 보일 글의 key 추가
        [Header("막혔을 때 글")]
        [Tooltip("이 사물에 막혔을 때 보일 글의 Strings.csv key. 비우면 글을 보이지 않는다")]
        [SerializeField] private string _blockedKey;

        [NonSerialized] private bool _visible;
        [NonSerialized] private float _shown;
        [NonSerialized] private float _fadeSeconds = MIN_SECONDS;
        [NonSerialized] private float _revealSeconds;
        // 표시가 바뀐 뒤 지난 시간. 음수면 선명하게 보이는 연출이 없다.
        [NonSerialized] private float _changeT = -1f;
        [NonSerialized] private IrisMaze _maze;
        [NonSerialized] private MazeLayer _layer;
        [NonSerialized] private SpriteRenderer _renderer;
        [NonSerialized] private Vector3 _sceneScale = Vector3.one;
        [NonSerialized] private bool _hasPassage;
        [NonSerialized] private MazeWall.Kind _passageKind;
        [NonSerialized] private int _passageRing;
        [NonSerialized] private int _passageSector;
        [NonSerialized] private MazeWall _openedWall;
        [NonSerialized] private bool _flipped;
        // 스펙 015: 막힌 길목 표시선 렌더러 추가
        [NonSerialized] private LineRenderer _barrier;
        // 뒤집힘 진행도 (0~1). 뒤집히지 않았으면 0이다.
        [NonSerialized] private float _flipT;
        [NonSerialized] private LineRenderer[] _staff;

        public MazePropKind Kind => _kind;
        public int Ring => _ring;
        public int Sector => _sector;
        public MazeLayer Layer => GetComponentInParent<MazeLayer>(true);
        public float FlipDistance => _flipDistance;
        public float DizzyRadius => _dizzyRadius;
        public float DizzyAmplitude => _dizzyAmplitude;
        public float DizzySpeed => _dizzySpeed;
        public string BlockedKey => _blockedKey;

        /// <summary>디렉터가 정한 표시 여부. 사라지기 시작하면 바로 false가 된다.</summary>
        public bool Visible => _visible;
        /// <summary>지금 보이고 이동 판정에 쓰이는지. 에디트 모드에서는 항상 보이는 것으로 본다.</summary>
        public bool Solid => isActiveAndEnabled && (!Application.isPlaying || _visible);
        /// <summary>지금 맡은 길목을 막거나 밀어내는지. 다가가면 막음 사물은 뒤집힌 뒤에만 막는다.</summary>
        public bool Blocks => _hasPassage && Solid && (_kind == MazePropKind.Block || _kind == MazePropKind.Push || (_kind == MazePropKind.FlipBlock && _flipped));
        /// <summary>시선이 다가오면 뒤집힐 수 있는 상태인지</summary>
        public bool CanFlip => _kind == MazePropKind.FlipBlock && _hasPassage && Solid && !_flipped;
        public bool HasPassage => _hasPassage;
        public MazeWall.Kind PassageKind => _passageKind;
        public int PassageRing => _passageRing;

        /// <summary>맡은 면의 화면 기준 sector (층 회전 반영)</summary>
        public int WorldPassageSector
        {
            get
            {
                EnsureRefs();
                return _maze.Mod(_passageSector + (_layer != null ? _layer.Offset : 0));
            }
        }

        /// <summary>맡은 면 가운데의 월드 위치. 면이 없으면 사물 위치다.</summary>
        public Vector3 PassageCenterWorld
        {
            get
            {
                if (!_hasPassage || transform.parent == null) return transform.position;
                return transform.parent.TransformPoint(PassageCenterLocal());
            }
        }

        void OnEnable()
        {
            EnsureRefs();
            _renderer = GetComponent<SpriteRenderer>();
            _sceneScale = transform.localScale;
        }

        void OnDisable()
        {
            // 묶음이 꺼지면 열어 둔 벽을 되돌리고 오선을 지운다.
            if (_openedWall != null && !_openedWall.gameObject.activeSelf) _openedWall.gameObject.SetActive(true);
            DestroyStaff();
        }

        void OnValidate() => EyeHooks.Repaint();

        void Update()
        {
            EnsureRefs();
            if (_maze == null) return;
            _ring = Mathf.Clamp(_ring, 0, _maze.Rings - 1);
            _sector = _maze.Mod(_sector);
            if (Application.isPlaying)
            {
                AdvanceChange(Time.deltaTime);
                if (_flipped && _flipT < 1f) _flipT = Mathf.MoveTowards(_flipT, 1f, Time.deltaTime / Mathf.Max(MIN_SECONDS, _flipSeconds));
            }

            float shown = Application.isPlaying ? _shown : 1f;
            float alpha = shown * Mathf.Lerp(MazeAlpha, 1f, VividWeight);
            PlaceOnPassage();
            bool flippedLook = _kind == MazePropKind.FlipBlock && _flipped && _flipT >= 0.5f;
            _renderer.sprite = flippedLook && _flippedSprite != null ? _flippedSprite : _sprite;
            Color tint = flippedLook && _flippedSprite == null ? _flippedTint : Color.white;
            _renderer.sortingOrder = SORTING_ORDER;
            // 수정(015): Play 중 길목을 맡지 못한 사물은 막지 않으므로 그리지 않는다.
            _renderer.enabled = shown > 0f && (!Application.isPlaying || _hasPassage || _kind == MazePropKind.OpenPath);
            _renderer.color = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
            UpdateOpenedWall();
            DrawStaff(alpha);
            DrawBarrier(alpha);
        }

        /// <summary>
        /// 감정에서 이 사물이 보이는지 판단한다.
        /// emotion과 _showIn을 사용하며, 목록이 비었거나 감정이 목록에 있으면 true를 반환한다.
        /// </summary>
        public bool ShowsIn(ClientEmotion emotion) => _showIn.Length == 0 || Array.IndexOf(_showIn, emotion) >= 0;

        /// <summary>
        /// 연출 없이 바로 보이거나 숨긴다. 상담 시작과 리셋에서 쓴다. 다가가면 막음 사물은 뒤집히지 않은 상태로 돌아간다.
        /// show를 사용하며, _visible, _shown, 선명하게 보이는 연출 상태와 뒤집힘 상태를 바꾼다.
        /// </summary>
        public void SetShown(bool show)
        {
            _visible = show;
            _shown = show ? 1f : 0f;
            _changeT = -1f;
            ResetFlip();
        }

        /// <summary>
        /// 표시가 바뀌면 fadeSeconds초에 걸쳐 나타나거나 사라지고, 그 뒤 revealSeconds초 동안 선명하게 보인다.
        /// 사라지기 시작할 때 바로 이동 판정에서 빠지고 뒤집힘이 풀린다. 표시가 그대로면 아무것도 하지 않는다.
        /// show, fadeSeconds, revealSeconds를 사용하며, _visible과 연출 시간, 뒤집힘 상태를 바꾼다.
        /// </summary>
        public void FadeTo(bool show, float fadeSeconds, float revealSeconds)
        {
            if (show == _visible) return;
            _visible = show;
            _fadeSeconds = Mathf.Max(MIN_SECONDS, fadeSeconds);
            _revealSeconds = Mathf.Max(0f, revealSeconds);
            _changeT = 0f;
            if (!show) ResetFlip();
        }

        /// <summary>
        /// 진행 중인 표시 변화를 끝 상태로 건너뛴다.
        /// _visible을 사용하며, _shown과 선명하게 보이는 연출 상태를 바꾼다.
        /// </summary>
        public void Finish()
        {
            _shown = _visible ? 1f : 0f;
            _changeT = -1f;
        }

        /// <summary>
        /// 스펙 015: 다가가면 막음 사물을 뒤집기 시작한다. 이 순간부터 길목을 막는다.
        /// 입력값은 없으며, _flipped와 _flipT를 바꾼다.
        /// </summary>
        public void Flip()
        {
            if (_kind != MazePropKind.FlipBlock || _flipped) return;
            _flipped = true;
            _flipT = 0f;
        }

        /// <summary>
        /// 스펙 015: 미로를 만들 때 맡은 면을 정한다. 길 냄 사물은 열 벽도 함께 받는다.
        /// kind, ring, localSector(자기 층 기준), wall을 사용하며, 맡은 면 정보와 열 벽을 바꾼다.
        /// </summary>
        public void AssignPassage(MazeWall.Kind kind, int ring, int localSector, MazeWall wall)
        {
            _hasPassage = true;
            _passageKind = kind;
            _passageRing = ring;
            _passageSector = localSector;
            _openedWall = wall;
        }

        /// <summary>
        /// 스펙 015: 맡은 면을 비운다. 그 층의 벽을 새로 만들기 전에 쓴다.
        /// 입력값은 없으며, 맡은 면 정보와 열 벽 참조를 비운다.
        /// </summary>
        public void ClearPassage()
        {
            _hasPassage = false;
            _openedWall = null;
        }

        /// <summary>미로와 층 참조가 없으면 부모에서 찾는다.</summary>
        private void EnsureRefs()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_layer == null) _layer = Layer;
        }

        /// <summary>뒤집힘을 풀어 처음 상태로 되돌린다. _flipped와 _flipT를 바꾼다.</summary>
        private void ResetFlip()
        {
            _flipped = false;
            _flipT = 0f;
        }

        /// <summary>
        /// 스펙 015: 맡은 면 가운데의 부모 기준 위치를 구한다. 방사 면은 고리 가운데, 원호 면은 두 고리 사이 원 위다.
        /// 맡은 면 정보와 미로 모양을 사용하며, 부모 기준 위치를 반환한다.
        /// </summary>
        private Vector3 PassageCenterLocal()
        {
            float d = _maze.SectorDeg;
            return _passageKind == MazeWall.Kind.Radial
                ? Shapes.Polar(_maze.RingMid(_passageRing), (_passageSector + 1) * d)
                : Shapes.Polar(_maze.RingInner(_passageRing), (_passageSector + 0.5f) * d);
        }

        /// <summary>
        /// 스펙 015: 통로를 가로지르는 방향(부모 기준 단위 벡터)과 통로 폭을 구한다. 방사 면은 반지름 방향과 고리 폭, 원호 면은 원 둘레 방향과 칸의 호 길이다.
        /// 맡은 면 정보와 미로 모양을 사용하며, across와 width를 돌려준다.
        /// </summary>
        private void PassageAcross(out Vector3 across, out float width)
        {
            float d = _maze.SectorDeg;
            if (_passageKind == MazeWall.Kind.Radial)
            {
                across = Shapes.Polar(1f, (_passageSector + 1) * d);
                width = _maze.RingWidth;
                return;
            }
            float a = (_passageSector + 0.5f) * d * Mathf.Deg2Rad;
            across = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
            width = _maze.RingInner(_passageRing) * d * Mathf.Deg2Rad;
        }

        /// <summary>
        /// 스펙 015: 사물을 놓는다. 맡은 면이 없으면 기준 칸 가운데에 두고, 있으면 면 가운데에 둔다.
        /// _fitToPassage가 켜져 있으면 통로를 가로지르게 돌리고 통로 폭에 맞춰 크기를 정한다. 다가가면 막음 사물은 뒤집히기 전 통로 가장자리에 작게 놓였다가 뒤집히며 가운데로 커진다.
        /// 맡은 면, 설정값, 뒤집힘 진행도를 사용하며, transform의 위치, 회전, 크기를 바꾼다.
        /// </summary>
        private void PlaceOnPassage()
        {
            if (!_hasPassage)
            {
                transform.localPosition = _maze.CellLocal(_ring, _sector);
                return;
            }
            Vector3 center = PassageCenterLocal();
            PassageAcross(out Vector3 across, out float width);
            Vector3 full = _sceneScale;
            if (_fitToPassage && _renderer.sprite != null)
            {
                // 수정(015): 통로 폭에만 맞추던 크기를 두께 상한(고리 폭 × _thicknessFill)으로도 제한해 옆 통로를 덮지 않게 했다.
                float s = Mathf.Min(_passageFill * width / Mathf.Max(0.01f, _sprite.bounds.size.x),
                    _thicknessFill * _maze.RingWidth / Mathf.Max(0.01f, _sprite.bounds.size.y));
                full = new Vector3(s, s, 1f);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg);
            }
            if (_kind == MazePropKind.OpenPath)
            {
                // 길 냄 사물은 열린 틈을 비워 두도록 작게 줄여 틈 가장자리에 둔다.
                transform.localPosition = center + across * width * 0.5f * (1f - OPEN_PATH_SPRITE_RATIO);
                transform.localScale = full * OPEN_PATH_SPRITE_RATIO;
                return;
            }
            if (_kind != MazePropKind.FlipBlock)
            {
                transform.localPosition = center;
                transform.localScale = full;
                return;
            }

            // 뒤집히기 전에는 통로 한쪽 가장자리에 작게 놓여 길을 막지 않는다.
            float grow = _flipped ? Mathf.SmoothStep(0f, 1f, _flipT) : 0f;
            float size = Mathf.Lerp(_idleScale, 1f, grow);
            float edge = width * _passageFill * 0.5f * (1f - _idleScale) * (1f - grow);
            float turn = _flipped ? Mathf.Abs(Mathf.Cos(_flipT * Mathf.PI)) : 1f;
            transform.localPosition = center + across * edge;
            transform.localScale = new Vector3(full.x * size * turn, full.y * size, 1f);
        }

        /// <summary>
        /// 스펙 015: 길 냄 사물이 맡은 벽을 Play Mode에서 보이는 동안 끄고, 사라지기 시작하면 다시 켠다.
        /// _openedWall과 _visible을 사용하며, 벽 오브젝트의 활성 상태를 바꾼다.
        /// </summary>
        private void UpdateOpenedWall()
        {
            if (_kind != MazePropKind.OpenPath || _openedWall == null || !Application.isPlaying) return;
            bool wallOn = !_visible;
            if (_openedWall.gameObject.activeSelf != wallOn) _openedWall.gameObject.SetActive(wallOn);
        }

        /// <summary>
        /// 스펙 015: 길 냄 사물이 맡은 벽 자리에 통로 방향으로 오선 다섯 줄을 그린다. 다른 사물이거나 면이 없으면 지운다.
        /// alpha와 맡은 면, _staffColor를 사용하며, 저장되지 않는 자식 LineRenderer를 만들거나 점과 색을 바꾼다.
        /// </summary>
        private void DrawStaff(float alpha)
        {
            if (_kind != MazePropKind.OpenPath || !_hasPassage || alpha <= 0f)
            {
                if (_staff != null) foreach (LineRenderer l in _staff) l.enabled = false;
                return;
            }
            if (_staff == null)
            {
                _staff = new LineRenderer[STAFF_LINES];
                for (int i = 0; i < STAFF_LINES; i++)
                {
                    var go = new GameObject("오선") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(transform, false);
                    _staff[i] = Shapes.Ensure(go, _maze.LineMaterial, 0);
                }
            }
            Vector3 center = PassageCenterLocal();
            PassageAcross(out Vector3 across, out float width);
            Vector3 along = new Vector3(-across.y, across.x, 0f);
            Color c = _staffColor;
            c.a *= alpha;
            for (int i = 0; i < STAFF_LINES; i++)
            {
                float t = (i / (float)(STAFF_LINES - 1) - 0.5f) * width * 0.6f;
                Vector3 mid = center + across * t;
                Vector3 a = ToStaffSpace(_staff[i], mid - along * STAFF_LENGTH * 0.5f);
                Vector3 b = ToStaffSpace(_staff[i], mid + along * STAFF_LENGTH * 0.5f);
                Shapes.SetPoints(_staff[i], new[] { a, b }, false);
                _staff[i].widthMultiplier = STAFF_WIDTH;
                _staff[i].sortingOrder = SORTING_ORDER - 1;
                Shapes.SetColor(_staff[i], c);
                _staff[i].enabled = true;
            }
        }

        /// <summary>부모 기준 좌표를 오선 렌더러의 좌표로 바꾼다. line과 local을 사용하며, 렌더러 기준 좌표를 반환한다.</summary>
        private Vector3 ToStaffSpace(LineRenderer line, Vector3 local)
        {
            Transform parent = transform.parent;
            Vector3 world = parent != null ? parent.TransformPoint(local) : local;
            return line.transform.InverseTransformPoint(world);
        }

        /// <summary>
        /// 스펙 015: 사물이 길목을 막고 있는 동안 막힌 길목 전체를 가로지르는 선을 그어, 보이는 범위와 막히는 범위를 같게 한다. 막지 않으면 숨긴다.
        /// alpha, Blocks, 맡은 면, _barrierColor, _barrierWidth를 사용하며, 저장되지 않는 자식 LineRenderer를 만들거나 점과 색을 바꾼다.
        /// </summary>
        private void DrawBarrier(float alpha)
        {
            if (!Application.isPlaying || !Blocks || alpha <= 0f)
            {
                if (_barrier != null) _barrier.enabled = false;
                return;
            }
            if (_barrier == null)
            {
                var go = new GameObject("막힘 선") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                _barrier = Shapes.Ensure(go, _maze.LineMaterial, 0);
            }
            Vector3 center = PassageCenterLocal();
            PassageAcross(out Vector3 across, out float width);
            Vector3 a = ToStaffSpace(_barrier, center - across * width * 0.5f);
            Vector3 b = ToStaffSpace(_barrier, center + across * width * 0.5f);
            Shapes.SetPoints(_barrier, new[] { a, b }, false);
            _barrier.widthMultiplier = _barrierWidth;
            _barrier.sortingOrder = SORTING_ORDER - 1;
            Color c = _barrierColor;
            c.a *= alpha;
            Shapes.SetColor(_barrier, c);
            _barrier.enabled = true;
        }

        /// <summary>오선 렌더러 오브젝트를 지운다. _staff를 비운다.</summary>
        private void DestroyStaff()
        {
            // 스펙 015: 막힘 선도 함께 지운다
            if (_barrier != null)
            {
                if (Application.isPlaying) Destroy(_barrier.gameObject);
                else DestroyImmediate(_barrier.gameObject);
                _barrier = null;
            }
            if (_staff == null) return;
            foreach (LineRenderer l in _staff)
            {
                if (l == null) continue;
                if (Application.isPlaying) Destroy(l.gameObject);
                else DestroyImmediate(l.gameObject);
            }
            _staff = null;
        }

        /// <summary>미로 벽과 같은 흐려짐 배율 (MemoryFragment.MazeColor와 같은 식)</summary>
        private float MazeAlpha => Mathf.Lerp(0.35f, 1f, _maze.WallAlpha) * _maze.FocusAlpha;

        /// <summary>미로 흐려짐을 무시하고 선명하게 보이는 정도 (0~1)</summary>
        private float VividWeight
        {
            get
            {
                if (_changeT < 0f) return 0f;
                float settle = _changeT - _fadeSeconds - _revealSeconds;
                return settle <= 0f ? 1f : Mathf.Clamp01(1f - settle / _fadeSeconds);
            }
        }

        /// <summary>
        /// 표시 변화를 한 프레임 진행한다. 나타나고 사라지기, 선명하게 보이기, 미로 흐려짐으로 돌아가기 순서다.
        /// dt와 연출 시간을 사용하며, _shown과 _changeT를 바꾼다.
        /// </summary>
        private void AdvanceChange(float dt)
        {
            if (_changeT < 0f) return;
            _changeT += dt;
            _shown = Mathf.MoveTowards(_shown, _visible ? 1f : 0f, dt / _fadeSeconds);
            if (_changeT >= _fadeSeconds * 2f + _revealSeconds) _changeT = -1f;
        }
    }
}
