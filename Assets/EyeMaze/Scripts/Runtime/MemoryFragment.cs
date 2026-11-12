using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 미로 속 기억 조각. 층(MazeLayer) 아래에 두면 그 층과 함께 회전한다.
    /// ring / sector를 바꾸면 에디트 모드에서도 바로 그 칸으로 옮겨진다.
    /// </summary>
    [ExecuteAlways]
    public class MemoryFragment : MonoBehaviour
    {
        // 스펙 012: 저해상도에서도 형태가 보이도록 마름모 본체의 대각선 배율과 폭 곡선 추가
        private const float BODY_SIZE_RATIO = 0.46f;
        private static readonly AnimationCurve DIAMOND_WIDTH = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), new Keyframe(0.5f, 1f, 2f, -2f), new Keyframe(1f, 0f, -2f, 0f));

        // 스펙 012: 획득 순간과 연출 정리를 알리는 이벤트 추가
        public static event Action<MemoryFragment> PickedUp;
        public static event Action FeedbackCleared;

        [Header("내용")]
        [SerializeField, FormerlySerializedAs("title")] private string _title = "기억";
        [SerializeField, FormerlySerializedAs("text"), TextArea(2, 5)] private string _text;

        [Header("위치")]
        [SerializeField, FormerlySerializedAs("ring")] private int _ring;
        [Tooltip("자기 층 기준 sector")]
        [SerializeField, FormerlySerializedAs("sector")] private int _sector;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("collected")] private bool _collected;
        [Tooltip("주우면 켜지는 플래그 id (Data/Flags.csv에 적힌 것). 대화 CSV의 need 칸에서 쓴다")]
        [SerializeField] private string _flag;
        // 스펙 016: 회상이 끝난 뒤에 나타나는 추가 파편 조건 추가
        [Tooltip("이 플래그의 회상이 끝난 뒤에 미로에 나타난다. 비우면 처음부터 있다")]
        [SerializeField] private string _appearAfter;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("color")] private Color _color = new Color(0.6f, 0.95f, 1f, 1f);
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;
        // 스펙 016: 회상을 본 뒤 자리에 남는 사물 그림과 렌더러 추가
        [Tooltip("회상을 본 뒤 이 자리에 남는 사물 그림. 비우면 작은 마름모가 남는다")]
        [SerializeField] private Sprite _seenSprite;
        [Tooltip("사물 그림을 그릴 렌더러 (파편의 자식 오브젝트)")]
        [SerializeField] private SpriteRenderer _seenRenderer;

        // 스펙 012: 맥동 주기와 최소 밝기 설정 추가
        [Header("맥동")]
        [Tooltip("맥동 한 주기(초)")]
        [SerializeField] private float _pulseSeconds = 1.6f;
        [Tooltip("맥동의 가장 어두운 밝기 배율")]
        [SerializeField, Range(0f, 1f)] private float _pulseMinBrightness = 0.7f;

        [NonSerialized] private int _drawnKey = int.MinValue;
        private IrisMaze _maze;
        // 스펙 016: 회상을 끝까지 봤는지, 기억의 자리 색, 디렉터가 나타나게 했는지
        [NonSerialized] private bool _seen;
        [NonSerialized] private Color _seenColor;
        [NonSerialized] private bool _appeared;
        // 스펙 017: 다른 기억과 이었는지
        [NonSerialized] private bool _linked;

        /// <summary>파편 이름. Flags.csv에서 이 파편 플래그 줄의 title 칸이 있으면 그걸 쓴다.</summary>
        public string Title => Flags.TitleOf(_flag) ?? _title;
        /// <summary>파편을 주울 때 나오는 글. Flags.csv의 text 칸이 있으면 그걸 쓴다.</summary>
        public string Text => Flags.TextOf(_flag) ?? _text;
        public int Ring { get => _ring; set => _ring = value; }
        public int Sector { get => _sector; set => _sector = value; }
        public string Flag => _flag;
        // 스펙 016: 추가 파편 조건과 회상 본 상태 공개
        public string AppearAfter => _appearAfter;
        public bool Seen => _seen;
        // 스펙 017: 이은 상태 공개
        public bool Linked => _linked;

        /// <summary>주웠는지. 바꾸면 플래그도 함께 켜지고 꺼진다.</summary>
        public bool Collected
        {
            get => _collected;
            set
            {
                // 스펙 012: Play 중 값이 바뀔 때만 획득 또는 연출 정리를 알림
                bool changed = _collected != value;
                _collected = value;
                Flags.Set(_flag, value);
                if (!changed || !Application.isPlaying) return;
                if (value) PickedUp?.Invoke(this);
                else FeedbackCleared?.Invoke();
            }
        }
        public MazeLayer Layer => GetComponentInParent<MazeLayer>(true);
        // 스펙 012: 연출이 쓰는 파편 색, 본체 지름, 현재 표시 색 추가
        public Color Tint => _color;
        public float BodyDiameter => _maze.RingWidth * BODY_SIZE_RATIO;
        // 스펙 012: 후광이 쓰는 맥동 없는 색과 맥동 파형(0~1) 추가
        public Color MazeColor
        {
            get
            {
                Color c = _color;
                c.a *= Mathf.Lerp(0.35f, 1f, _maze.WallAlpha) * _maze.FocusAlpha;
                return c;
            }
        }
        public float PulseWave => !Application.isPlaying || _collected
            ? 1f
            : 0.5f + 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / Mathf.Max(0.01f, _pulseSeconds));
        public Color DrawColor
        {
            get
            {
                Color c = MazeColor;
                c.a *= Mathf.Lerp(_pulseMinBrightness, 1f, PulseWave);
                return c;
            }
        }

        /// <summary>화면 기준 sector (층 회전 반영).</summary>
        public int WorldSector
        {
            get
            {
                if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
                MazeLayer l = Layer;
                return _maze != null ? _maze.Mod(_sector + (l != null ? l.Offset : 0)) : _sector;
            }
        }

        // 스펙 016: 추가 파편은 디렉터가 나타나게 하기 전까지 꺼 둔다
        void Awake()
        {
            if (Application.isPlaying && !string.IsNullOrEmpty(_appearAfter) && !_appeared) gameObject.SetActive(false);
        }

        void OnEnable()
        {
            _drawnKey = int.MinValue;
            // 스펙 012: 켜진 프레임의 후광 갱신이 첫 Update보다 먼저 돌아도 미로를 쓸 수 있게 여기서 찾음
            _maze = GetComponentInParent<IrisMaze>();
        }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        // 스펙 012: 손님 전환으로 파편 묶음이 꺼질 때 연출 정리를 알림
        void OnDisable()
        {
            if (Application.isPlaying) FeedbackCleared?.Invoke();
        }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            // 스펙 016: 기억의 자리 상태와 색이 바뀌어도 다시 그린다
            int key = HashCode.Combine(HashCode.Combine(_ring, _sector, _maze.GeometryKey, _collected, _color, _maze.WallAlpha, _maze.Focus), _seen, _seenColor, _seenSprite);
            if (key == _drawnKey)
            {
                // 스펙 012: 그림이 그대로여도 Play 중에는 맥동 색을 매 프레임 갱신
                if (Application.isPlaying && !_collected) Shapes.SetColor(_line, DrawColor);
                return;
            }
            _drawnKey = key;

            _ring = Mathf.Clamp(_ring, 0, _maze.Rings - 1);
            _sector = _maze.Mod(_sector);
            transform.localPosition = _maze.CellLocal(_ring, _sector);
            transform.localRotation = Quaternion.identity;
            // 스펙 012: 본체를 마름모로 그리고 표시 색을 공용 프로퍼티로 계산
            DrawDiamond(_line, BodyDiameter);
            Shapes.SetColor(_line, DrawColor);
            _line.sortingOrder = 7;
            _line.enabled = !_collected;
            // 스펙 016: 회상을 본 파편은 자리에 사물 그림이나 작은 마름모를 남긴다
            DrawSeenMark();
        }

        /// <summary>
        /// 스펙 016: 회상을 본 파편의 자리에 _seenSprite를, 없으면 본체 지름 절반 크기의 맥동 없는 마름모를 기억의 자리 색으로 그린다.
        /// _seen, _seenColor, _seenSprite와 미로 투명도를 사용하며, _seenRenderer와 _line의 그림과 표시 상태를 바꾼다.
        /// </summary>
        private void DrawSeenMark()
        {
            bool mark = _collected && _seen;
            bool sprite = mark && _seenSprite != null && _seenRenderer != null;
            // 미로가 흐려질 때 함께 흐려지도록 미로 투명도를 곱한다.
            Color c = _seenColor;
            c.a *= Mathf.Lerp(0.35f, 1f, _maze.WallAlpha) * _maze.FocusAlpha;
            if (_seenRenderer != null)
            {
                _seenRenderer.enabled = sprite;
                if (sprite)
                {
                    _seenRenderer.sprite = _seenSprite;
                    _seenRenderer.color = c;
                    _seenRenderer.sortingOrder = 7;
                }
            }
            if (!mark || sprite) return;
            DrawDiamond(_line, BodyDiameter * 0.5f);
            Shapes.SetColor(_line, c);
            _line.enabled = true;
        }

        /// <summary>
        /// 스펙 016: 회상을 끝까지 본 것으로 표시한다.
        /// color(기억의 자리 색)를 사용하며, _seen과 _seenColor를 바꾸고 다시 그리게 한다.
        /// </summary>
        public void MarkSeen(Color color)
        {
            _seen = true;
            _seenColor = color;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// 스펙 017: 다른 기억과 이은 것으로 표시하고 기억의 자리를 이은 뒤의 색으로 그린다.
        /// color(이은 뒤의 기억의 자리 색)를 사용하며, _linked와 _seenColor를 바꾸고 다시 그리게 한다.
        /// </summary>
        public void MarkLinked(Color color)
        {
            _linked = true;
            _seenColor = color;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// 스펙 016: 회상 본 표시를 지운다.
        /// 입력값은 없으며, _seen과 _linked를 끄고 다시 그리게 한다.
        /// </summary>
        public void ClearSeen()
        {
            _seen = false;
            // 스펙 017: 회상 본 표시와 함께 이은 상태도 지운다
            _linked = false;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// 스펙 016: 회상 뒤에 나타나는 파편을 미로에 나타낸다.
        /// 입력값은 없으며, _appeared를 켜고 오브젝트를 켠다.
        /// </summary>
        public void Appear()
        {
            _appeared = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 스펙 016: 회상 뒤에 나타나는 파편을 다시 숨긴다.
        /// 입력값은 없으며, _appeared를 끄고 오브젝트를 끈다.
        /// </summary>
        public void Hide()
        {
            _appeared = false;
            gameObject.SetActive(false);
        }

        // 스펙 012: 층이 돌아도 마름모가 같은 모양으로 찍히도록 Play 중 회전과 화면 픽셀 위치를 고정
        void LateUpdate()
        {
            if (!Application.isPlaying || _maze == null) return;
            Vector3 world = transform.parent.TransformPoint(_maze.CellLocal(_ring, _sector));
            transform.SetPositionAndRotation(SnapToScreenPixel(world), Quaternion.identity);
        }

        /// <summary>
        /// 가운데가 가장 넓은 선으로 마름모를 그린다.
        /// line과 diagonal(대각선 길이)을 사용하며, line의 점 목록과 폭을 바꾼다.
        /// </summary>
        public static void DrawDiamond(LineRenderer line, float diagonal)
        {
            float half = diagonal * 0.5f;
            Shapes.SetPoints(line, new[] { new Vector3(-half, 0f, 0f), Vector3.zero, new Vector3(half, 0f, 0f) }, false);
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.widthCurve = DIAMOND_WIDTH;
            line.widthMultiplier = diagonal;
        }

        /// <summary>
        /// 월드 좌표를 메인 카메라 렌더 텍스처의 가장 가까운 픽셀 중심으로 옮긴다.
        /// world와 Camera.main을 사용하며, 맞춘 월드 좌표를 반환한다.
        /// </summary>
        private static Vector3 SnapToScreenPixel(Vector3 world)
        {
            Camera camera = Camera.main;
            RenderTexture target = camera.targetTexture;
            if (target == null) return world;
            float pixel = camera.orthographicSize * 2f / target.height;
            Vector3 origin = camera.transform.position;
            world.x = origin.x + (Mathf.Floor((world.x - origin.x) / pixel) + 0.5f) * pixel;
            world.y = origin.y + (Mathf.Floor((world.y - origin.y) / pixel) + 0.5f) * pixel;
            return world;
        }
    }
}
