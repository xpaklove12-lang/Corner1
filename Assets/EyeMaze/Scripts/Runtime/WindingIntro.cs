using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 태엽은 화면에 보이지 않는 값이다. 들여다보는 동안 0에서 용량까지 초당 unwindPerSecond칸씩 차고, 다 차면 그대로 멈춘다.
    /// 대화 중 Space(패드 A)를 눌렀다 떼면 바로 눈을 들여다본다. 신뢰가 깊어질수록 용량이 늘어난다.
    /// </summary>
    [ExecuteAlways]
    public class WindingIntro : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("director")] private EyeMazeDirector _director;
        [SerializeField, FormerlySerializedAs("maze")] private IrisMaze _maze;
        [SerializeField, FormerlySerializedAs("eye")] private EyeVisual _eye;
        [Tooltip("홍채 둘레의 톱니. 표시하지 않도록 항상 꺼 둔다")]
        [SerializeField, FormerlySerializedAs("crown")] private LineRenderer _crown;
        [Tooltip("톱니 바깥의 원호. 표시하지 않도록 항상 꺼 둔다")]
        [SerializeField, FormerlySerializedAs("gauge")] private LineRenderer _gauge;

        [Header("태엽")]
        [Tooltip("지금 찬 칸 수")]
        [SerializeField, FormerlySerializedAs("wound")] private float _wound;
        [Tooltip("신뢰 0일 때의 용량 (칸 수)")]
        [SerializeField, FormerlySerializedAs("baseCapacity")] private int _baseCapacity = 14;
        [Tooltip("신뢰 1당 늘어나는 칸 수")]
        [SerializeField, FormerlySerializedAs("capacityPerTrust")] private int _capacityPerTrust = 5;
        [Tooltip("들여다보는 동안 초당 차는 칸 수")]
        [SerializeField, FormerlySerializedAs("unwindPerSecond")] private float _unwindPerSecond = 1f;

        [Header("문구")]
        [SerializeField, TextArea(2, 4)] private string _firstGuide = "Space를 누르면 은재의 눈을 들여다본다.";
        [SerializeField, TextArea(2, 4)] private string _againGuide = "다시 마주 앉았다. Space를 누르면 눈을 들여다본다.";

        [NonSerialized] private bool _holding;
        [NonSerialized] private bool _couldEnterLastFrame;

        public EyeMazeDirector Director => _director;
        public IrisMaze Maze => _maze;
        public EyeVisual Eye => _eye;
        public float Wound { get => _wound; set => _wound = value; }
        public float UnwindPerSecond => _unwindPerSecond;

        public int Capacity
        {
            get
            {
                int trust = _director != null && _director.Person != null ? Mathf.Max(0, _director.Person.Trust) : 0;
                return Mathf.Max(1, _baseCapacity + _capacityPerTrust * trust);
            }
        }

        public bool IsFull => _wound >= Capacity - 1e-3f;

        /// <summary>찬 정도 (0~1)</summary>
        public float Fill => Mathf.Clamp01(_wound / Capacity);

        void OnValidate() => EyeHooks.Repaint();

        /// <summary>
        /// 만남을 시작할 때 태엽 값을 0으로 되돌린다.
        /// first(처음 만나는지)를 사용하며, 태엽 상태를 초기화하고 안내 문구를 바꾼다.
        /// </summary>
        public void ResetForEncounter(bool first)
        {
            _wound = 0f;
            if (_director != null) _director.Message = first ? _firstGuide : _againGuide;
        }

        /// <summary>
        /// 대화 결과로 들여다볼 시간을 늘리거나(+) 줄인다(-).
        /// ticks를 사용하며, 늘릴수록 _wound를 낮춘다.
        /// </summary>
        public void AddFree(float ticks)
        {
            _wound = Mathf.Clamp(_wound - ticks, 0f, Capacity);
        }

        /// <summary>
        /// 들여다보는 동안 태엽 값을 채운다. 용량에 닿으면 더 오르지 않는다.
        /// ticks를 사용하며, _wound를 바꾼다.
        /// </summary>
        public void Unwind(float ticks)
        {
            if (ticks <= 0f) return;
            _wound = Mathf.Min(Capacity, _wound + ticks);
        }

        /// <summary>
        /// 태엽 값을 처음 상태로 되돌린다. 에디트 모드 미리보기에서 쓴다.
        /// 입력값은 없으며, _wound를 0으로 바꾼다.
        /// </summary>
        public void FillUp()
        {
            _wound = 0f;
        }

        void Update()
        {
            bool canEnter = _director != null && _director.AcceptInput && _director.CurrentMode == EyeMazeDirector.Mode.Talk;
            if (Application.isPlaying && canEnter) HandleInput();
            else _holding = false;
            _couldEnterLastFrame = canEnter;

            ApplyVisuals();
        }

        /// <summary>
        /// Space(패드 A)를 눌렀다 떼면 눈을 들여다본다.
        /// 키 입력을 사용하며, _holding과 디렉터의 모드를 바꾼다.
        /// </summary>
        private void HandleInput()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool pressed = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool down = (kb != null && kb.spaceKey.isPressed) || (pad != null && pad.buttonSouth.isPressed);

            // 시선을 거두려고 누른 Space가 다시 들여다보기로 이어지지 않게, 새로 누른 경우에만 시작한다.
            if (!_holding && pressed && _couldEnterLastFrame) _holding = true;
            if (_holding && !down)
            {
                _holding = false;
                _director.ToggleGaze();
            }
        }

        /// <summary>
        /// 미로와 눈의 초점을 선명하게 유지하고, 톱니와 원호를 꺼 둔다.
        /// 입력값은 없으며, 미로와 눈의 초점, 톱니와 원호의 표시 여부를 바꾼다.
        /// </summary>
        private void ApplyVisuals()
        {
            if (_maze != null && !Mathf.Approximately(_maze.Focus, 1f)) _maze.Focus = 1f;
            if (_eye != null && !Mathf.Approximately(_eye.Focus, 1f)) _eye.Focus = 1f;
            if (_crown != null && _crown.enabled) _crown.enabled = false;
            if (_gauge != null && _gauge.enabled) _gauge.enabled = false;
        }
    }
}
