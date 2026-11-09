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
        // 스펙 010: 저해상도에서도 형태가 보이도록 마름모 본체의 대각선 배율과 폭 곡선 추가
        private const float BODY_SIZE_RATIO = 0.46f;
        private static readonly AnimationCurve DIAMOND_WIDTH = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f), new Keyframe(0.5f, 1f, 2f, -2f), new Keyframe(1f, 0f, -2f, 0f));

        // 스펙 010: 획득 순간과 연출 정리를 알리는 이벤트 추가
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

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("color")] private Color _color = new Color(0.6f, 0.95f, 1f, 1f);
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;

        // 스펙 010: 맥동 주기와 최소 밝기 설정 추가
        [Header("맥동")]
        [Tooltip("맥동 한 주기(초)")]
        [SerializeField] private float _pulseSeconds = 1.6f;
        [Tooltip("맥동의 가장 어두운 밝기 배율")]
        [SerializeField, Range(0f, 1f)] private float _pulseMinBrightness = 0.7f;

        [NonSerialized] private int _drawnKey = int.MinValue;
        private IrisMaze _maze;

        /// <summary>파편 이름. Flags.csv에서 이 파편 플래그 줄의 title 칸이 있으면 그걸 쓴다.</summary>
        public string Title => Flags.TitleOf(_flag) ?? _title;
        /// <summary>파편을 주울 때 나오는 글. Flags.csv의 text 칸이 있으면 그걸 쓴다.</summary>
        public string Text => Flags.TextOf(_flag) ?? _text;
        public int Ring { get => _ring; set => _ring = value; }
        public int Sector { get => _sector; set => _sector = value; }
        public string Flag => _flag;

        /// <summary>주웠는지. 바꾸면 플래그도 함께 켜지고 꺼진다.</summary>
        public bool Collected
        {
            get => _collected;
            set
            {
                // 스펙 010: Play 중 값이 바뀔 때만 획득 또는 연출 정리를 알림
                bool changed = _collected != value;
                _collected = value;
                Flags.Set(_flag, value);
                if (!changed || !Application.isPlaying) return;
                if (value) PickedUp?.Invoke(this);
                else FeedbackCleared?.Invoke();
            }
        }
        public MazeLayer Layer => GetComponentInParent<MazeLayer>(true);
        // 스펙 010: 연출이 쓰는 파편 색, 본체 지름, 현재 표시 색 추가
        public Color Tint => _color;
        public float BodyDiameter => _maze.RingWidth * BODY_SIZE_RATIO;
        // 스펙 010: 후광이 쓰는 맥동 없는 색과 맥동 파형(0~1) 추가
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

        void OnEnable()
        {
            _drawnKey = int.MinValue;
            // 스펙 010: 켜진 프레임의 후광 갱신이 첫 Update보다 먼저 돌아도 미로를 쓸 수 있게 여기서 찾음
            _maze = GetComponentInParent<IrisMaze>();
        }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        // 스펙 010: 손님 전환으로 파편 묶음이 꺼질 때 연출 정리를 알림
        void OnDisable()
        {
            if (Application.isPlaying) FeedbackCleared?.Invoke();
        }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            int key = HashCode.Combine(_ring, _sector, _maze.GeometryKey, _collected, _color, _maze.WallAlpha, _maze.Focus);
            if (key == _drawnKey)
            {
                // 스펙 010: 그림이 그대로여도 Play 중에는 맥동 색을 매 프레임 갱신
                if (Application.isPlaying && !_collected) Shapes.SetColor(_line, DrawColor);
                return;
            }
            _drawnKey = key;

            _ring = Mathf.Clamp(_ring, 0, _maze.Rings - 1);
            _sector = _maze.Mod(_sector);
            transform.localPosition = _maze.CellLocal(_ring, _sector);
            transform.localRotation = Quaternion.identity;
            // 스펙 010: 본체를 마름모로 그리고 표시 색을 공용 프로퍼티로 계산
            DrawDiamond(_line, BodyDiameter);
            Shapes.SetColor(_line, DrawColor);
            _line.sortingOrder = 7;
            _line.enabled = !_collected;
        }

        // 스펙 010: 층이 돌아도 마름모가 같은 모양으로 찍히도록 Play 중 회전과 화면 픽셀 위치를 고정
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
