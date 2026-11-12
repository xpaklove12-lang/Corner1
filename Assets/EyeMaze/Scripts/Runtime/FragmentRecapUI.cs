using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 019: 기억 목록에서 고른 파편의 요약 패널. 제목과 획득 문구를 보여 주고, 회상이 있으면 다시 재생한다.
    /// 패널이 열려 있는 동안 게임 시간을 멈춘다.
    /// </summary>
    public class FragmentRecapUI : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("패널을 열 수 있는지 묻고 회상 재생을 맡길 디렉터")]
        [SerializeField] private EyeMazeDirector _director;

        [Header("패널")]
        [Tooltip("열고 닫을 패널 오브젝트")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _bodyText;
        [Tooltip("회상을 다시 재생하는 버튼. 회상이 없는 파편에서는 숨긴다")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _closeButton;

        private MemoryFragment _fragment;
        private float _savedTimeScale = 1f;

        /// <summary>패널이 열려 있는지</summary>
        public static bool IsOpen { get; private set; }
        /// <summary>패널을 닫은 프레임 번호</summary>
        public static int ClosedFrame { get; private set; } = -1;
        /// <summary>패널이 열려 있거나 이번 프레임에 닫혀서 게임 입력을 막아야 하는지</summary>
        public static bool BlocksInput => IsOpen || ClosedFrame == Time.frameCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            ClosedFrame = -1;
        }

        void OnEnable()
        {
            _playButton.onClick.AddListener(Replay);
            _closeButton.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            _playButton.onClick.RemoveListener(Replay);
            _closeButton.onClick.RemoveListener(Close);
            if (IsOpen) Close();
        }

        void Update()
        {
            // 대화 기록 창이 위에 열려 있으면 그 창이 닫힌 뒤에 닫을 수 있다.
            if (!IsOpen || DialogueLog.BlocksInput) return;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (back) Close();
        }

        /// <summary>
        /// 파편의 요약 패널을 연다. 디렉터가 열 수 없는 상태라고 하면 아무것도 하지 않는다.
        /// fragment의 제목과 획득 문구, 회상 유무를 사용하며, 패널 글과 재생 버튼 표시, IsOpen, Time.timeScale을 바꾼다.
        /// </summary>
        public void Open(MemoryFragment fragment)
        {
            if (IsOpen || !_director.CanOpenRecap) return;
            _fragment = fragment;
            _titleText.text = fragment.Title;
            _bodyText.text = fragment.Text;
            _playButton.gameObject.SetActive(_director.CanReplay(fragment));
            _panel.SetActive(true);
            IsOpen = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 패널을 닫고 게임 시간을 열기 전 값으로 되돌린다.
        /// _savedTimeScale을 사용하며, 패널 활성 상태와 IsOpen, ClosedFrame, Time.timeScale을 바꾼다.
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            Time.timeScale = _savedTimeScale;
            _panel.SetActive(false);
        }

        /// <summary>
        /// 패널을 닫고 지금 보이던 파편의 회상을 다시 재생한다.
        /// _fragment를 사용하며, 패널 상태와 디렉터의 회상 재생 상태를 바꾼다.
        /// </summary>
        private void Replay()
        {
            MemoryFragment fragment = _fragment;
            Close();
            _director.ReplayFlashback(fragment);
        }
    }
}
