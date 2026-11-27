// 소리 담당에 알릴 이벤트 형식을 쓰려고 System을 더함
using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IntroCuttoon
{
    /// <summary>
    /// 인트로 컷툰 진행. 입력에 따라 칸을 차례로 열고, 쪽을 넘기고, 마지막 칸 뒤에 암전한다.
    /// Space, Enter, 마우스 왼쪽 클릭은 다음 칸, R은 처음부터 다시 보기다.
    /// </summary>
    public class CuttoonPlayer : MonoBehaviour
    {
        private enum PlayState
        {
            Showing,
            PageFading,
            WaitingBlackout,
            Blackout,
            Ended,
        }

        [Header("쪽")]
        [Tooltip("보여 줄 순서대로 놓은 쪽")]
        [SerializeField] private CuttoonPage[] _pages;
        [Tooltip("쪽을 넘길 때 지금 쪽이 사라지는 시간(초)")]
        [SerializeField] private float _pageFadeDuration = 0.35f;

        [Header("자동 진행")]
        [SerializeField] private bool _autoAdvance = false;
        [Tooltip("진행 중인 것이 없어진 뒤 다음 칸까지 기다리는 시간(초)")]
        [SerializeField] private float _autoAdvanceInterval = 1.5f;

        [Header("암전")]
        [SerializeField] private CanvasGroup _blackout;
        [Tooltip("마지막 칸 연출이 끝난 뒤 암전을 시작하기까지의 시간(초)")]
        [SerializeField] private float _blackoutDelay = 0.8f;
        [Tooltip("화면이 검게 덮이는 시간(초)")]
        [SerializeField] private float _blackoutDuration = 1.6f;

        [Header("안내")]
        [SerializeField] private bool _showGuide = true;
        [SerializeField] private GameObject _guideRoot;
        [SerializeField] private Text _pageText;
        [Tooltip("{0}은 지금 쪽, {1}은 전체 쪽 수")]
        [SerializeField] private string _pageFormat = "쪽 {0} / {1}";

        private PlayState _state;
        private int _pageIndex;
        private float _timer;

        // 소리 담당이 칸 진행에 맞춰 소리를 내도록 알림 네 개를 더함
        /// <summary>
        /// 칸이 열린 직후 쪽 번호와 그 쪽의 칸 번호를 0부터 센 값으로 알린다.
        /// </summary>
        public event Action<int, int> PanelOpened;

        /// <summary>
        /// 다음 칸 입력으로 진행 중인 칸 연출을 끝 상태로 맞추기 직전에 알린다.
        /// </summary>
        public event Action FinishingOpened;

        /// <summary>
        /// 처음으로 돌아가거나 쪽 바로 가기로 모든 쪽을 되돌리기 직전에, 다시 시작할 쪽 번호를 0부터 센 값으로 알린다.
        /// </summary>
        public event Action<int> Restarting;

        /// <summary>
        /// 암전이 시작될 때 화면이 검게 덮이는 시간(초)을 알린다.
        /// </summary>
        public event Action<float> BlackoutStarted;

        private CuttoonPage CurrentPage => _pages[_pageIndex];

        private bool IsLastPanel => _pageIndex == _pages.Length - 1 && CurrentPage.OpenedCount == CurrentPage.PanelCount;

        void Start()
        {
            _guideRoot.SetActive(_showGuide);
            Restart();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Restart();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 확인용 임시 키. 숫자패드 1~5로 그 쪽 첫 칸으로 바로 간다. 나중에 지운다.
            if (keyboard != null)
            {
                for (int i = 0; i < _pages.Length && i < 9; i++)
                {
                    if (keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                    {
                        JumpToPage(i);
                        return;
                    }
                }
            }
#endif

            bool advancePressed =
                (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);

            if (advancePressed)
            {
                RequestAdvance();
            }

            switch (_state)
            {
                case PlayState.Showing:
                    UpdateShowing();
                    break;
                case PlayState.PageFading:
                    UpdatePageFade();
                    break;
                case PlayState.WaitingBlackout:
                case PlayState.Blackout:
                    UpdateBlackout();
                    break;
            }
        }

        void OnValidate()
        {
            _pageFadeDuration = Mathf.Max(0f, _pageFadeDuration);
            _autoAdvanceInterval = Mathf.Max(0f, _autoAdvanceInterval);
            _blackoutDelay = Mathf.Max(0f, _blackoutDelay);
            _blackoutDuration = Mathf.Max(0f, _blackoutDuration);
        }

        /// <summary>
        /// 모든 쪽, 칸, 연출, 암전을 처음 상태로 되돌리고 1쪽 첫 칸을 연다.
        /// _state를 Showing으로, _pageIndex를 0으로 바꾼다.
        /// </summary>
        public void Restart()
        {
            // 처음부터 다시 시작함을 소리 담당에 알림
            Restarting?.Invoke(0);

            foreach (CuttoonPage page in _pages)
            {
                page.ResetPage();
                page.Alpha = 0f;
            }

            _blackout.alpha = 0f;
            _pageIndex = 0;
            CurrentPage.Alpha = 1f;
            OpenNextPanel();
            UpdatePageText();
        }

        /// <summary>
        /// 다음 칸 입력 하나를 처리한다. 진행 중인 것이 있으면 끝 상태로 맞추고, 없으면 다음 칸이나 다음 쪽으로 간다.
        /// 칸 보기 상태가 아닐 때는 아무것도 하지 않는다.
        /// </summary>
        public void RequestAdvance()
        {
            if (_state != PlayState.Showing)
            {
                return;
            }

            if (CurrentPage.IsBusy)
            {
                // 연출을 끝 상태로 맞추기 전에 소리 담당에 알림
                FinishingOpened?.Invoke();
                CurrentPage.FinishOpened();
                return;
            }

            Advance();
        }

        /// <summary>
        /// 지금 쪽에 남은 칸이 있으면 다음 칸을 열고, 쪽의 마지막 칸이면 쪽 넘김을 시작한다.
        /// 마지막 쪽의 마지막 칸이면 아무것도 하지 않는다(암전은 UpdateShowing이 시작한다).
        /// </summary>
        private void Advance()
        {
            if (CurrentPage.OpenedCount < CurrentPage.PanelCount)
            {
                OpenNextPanel();
                return;
            }

            if (_pageIndex < _pages.Length - 1)
            {
                _state = PlayState.PageFading;
                _timer = 0f;
            }
        }

        /// <summary>
        /// 지금 쪽의 다음 칸을 열고 칸 보기 상태로 둔다.
        /// _timer를 0으로 되돌려 자동 진행 대기를 새로 센다.
        /// </summary>
        private void OpenNextPanel()
        {
            CurrentPage.OpenNext();
            _state = PlayState.Showing;
            _timer = 0f;

            // 열린 칸의 쪽 번호와 칸 번호를 소리 담당에 알림
            PanelOpened?.Invoke(_pageIndex, CurrentPage.OpenedCount - 1);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// 확인용 임시 기능. 모든 쪽, 칸, 연출, 암전을 처음 상태로 되돌린 뒤 index 쪽의 첫 칸을 연다.
        /// _state를 Showing으로, _pageIndex를 index로 바꾸고 하단 쪽 번호를 갱신한다. 나중에 지운다.
        /// </summary>
        private void JumpToPage(int index)
        {
            // 이 쪽부터 다시 시작함을 소리 담당에 알림
            Restarting?.Invoke(index);

            foreach (CuttoonPage page in _pages)
            {
                page.ResetPage();
                page.Alpha = 0f;
            }

            _blackout.alpha = 0f;
            _pageIndex = index;
            CurrentPage.Alpha = 1f;
            OpenNextPanel();
            UpdatePageText();
        }
#endif

        /// <summary>
        /// 칸 보기 상태에서 매 프레임 할 일을 처리한다.
        /// 마지막 칸의 연출이 끝나면 암전 대기로 넘어가고, _autoAdvance가 켜져 있으면 대기 시간이 지난 뒤 다음 칸으로 간다.
        /// </summary>
        private void UpdateShowing()
        {
            if (CurrentPage.IsBusy)
            {
                _timer = 0f;
                return;
            }

            if (IsLastPanel)
            {
                _state = PlayState.WaitingBlackout;
                _timer = 0f;
                return;
            }

            if (!_autoAdvance)
            {
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= _autoAdvanceInterval)
            {
                Advance();
            }
        }

        /// <summary>
        /// 쪽 넘김 상태에서 지금 쪽의 투명도를 1에서 0으로 낮춘다.
        /// 다 사라지면 지금 쪽을 숨기고 다음 쪽의 첫 칸을 연다.
        /// </summary>
        private void UpdatePageFade()
        {
            _timer += Time.deltaTime;
            float t = _pageFadeDuration > 0f ? Mathf.Clamp01(_timer / _pageFadeDuration) : 1f;
            CurrentPage.Alpha = 1f - t;
            if (t < 1f)
            {
                return;
            }

            CurrentPage.ResetPage();
            _pageIndex++;
            CurrentPage.Alpha = 1f;
            OpenNextPanel();
            UpdatePageText();
        }

        /// <summary>
        /// 암전 대기와 암전 상태를 처리한다. _blackoutDelay초 기다린 뒤 _blackoutDuration초 동안 _blackout의 투명도를 0에서 1로 올린다.
        /// 다 덮이면 _state를 Ended로 바꾼다.
        /// </summary>
        private void UpdateBlackout()
        {
            _timer += Time.deltaTime;

            if (_state == PlayState.WaitingBlackout)
            {
                if (_timer >= _blackoutDelay)
                {
                    _state = PlayState.Blackout;
                    _timer = 0f;

                    // 암전이 시작됨을 덮이는 시간과 함께 소리 담당에 알림
                    BlackoutStarted?.Invoke(_blackoutDuration);
                }

                return;
            }

            float t = _blackoutDuration > 0f ? Mathf.Clamp01(_timer / _blackoutDuration) : 1f;
            _blackout.alpha = t;
            if (t >= 1f)
            {
                _state = PlayState.Ended;
            }
        }

        /// <summary>
        /// 하단 쪽 번호 글을 _pageFormat으로 지금 쪽과 전체 쪽 수에 맞춰 바꾼다.
        /// </summary>
        private void UpdatePageText()
        {
            _pageText.text = string.Format(_pageFormat, _pageIndex + 1, _pages.Length);
        }
    }
}
