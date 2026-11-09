using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 007: 대화 기록 창. L 키나 게임패드 Select로 열고 닫으며, 열린 동안 게임을 멈추고 기록을 보여준다.
    /// 메신저처럼 플레이어가 고른 말은 오른쪽 말풍선, 상대의 말은 왼쪽 얼굴과 말풍선(위에 이름), 서술은 가운데 글로 보여준다.
    /// 줄 칸은 프리팹에 미리 만들어 두고, 여기서는 켜고 끄며 글과 색만 바꾼다.
    /// </summary>
    public class DialogueLogUI : MonoBehaviour
    {
        /// <summary>프리팹에 미리 만들어 둔 기록 줄 칸 하나</summary>
        [Serializable]
        private class LogRow
        {
            [Tooltip("줄 전체. 기록이 없는 칸은 끈다")]
            [SerializeField] private GameObject _root;
            [Tooltip("줄의 정렬(왼쪽, 오른쪽, 가운데)을 정하는 레이아웃")]
            [SerializeField] private HorizontalLayoutGroup _layout;
            [Tooltip("상대 말 왼쪽의 얼굴")]
            [SerializeField] private Image _face;
            [SerializeField] private Text _name;
            [SerializeField] private Image _bubble;
            [SerializeField] private Text _body;
            [Tooltip("말풍선 너비를 글 길이에 맞추는 레이아웃")]
            [SerializeField] private LayoutElement _bodyLayout;

            public GameObject Root => _root;
            public HorizontalLayoutGroup Layout => _layout;
            public Image Face => _face;
            public Text Name => _name;
            public Image Bubble => _bubble;
            public Text Body => _body;
            public LayoutElement BodyLayout => _bodyLayout;
        }

        [Header("연결")]
        [Tooltip("여닫는 창. 기본으로 꺼 둔다")]
        [SerializeField] private GameObject _window;
        [SerializeField] private ScrollRect _scrollRect;
        [Tooltip("기록 줄 칸들. 칸 수만큼 최근 기록을 보관하고 보여준다")]
        [SerializeField] private LogRow[] _rows = new LogRow[0];

        [Header("말풍선")]
        [Tooltip("말풍선 안 글의 최대 너비. 이보다 긴 글은 줄을 바꾼다")]
        [SerializeField] private float _bubbleMaxWidth = 1000f;
        [SerializeField] private Color _playerBubbleColor = new Color(0.3f, 0.3f, 0.34f, 1f);
        [SerializeField] private Color _playerTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] private Color _otherBubbleColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] private Color _otherTextColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [Tooltip("상황, 엔딩 줄 같은 서술 글 색")]
        [SerializeField] private Color _narrationTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        [Header("조작")]
        [Tooltip("키와 스틱으로 스크롤하는 속도 (초당 픽셀)")]
        [SerializeField] private float _scrollSpeed = 600f;
        [Tooltip("열린 동안 Time.timeScale을 0으로 두어 게임을 멈출지")]
        [SerializeField] private bool _pauseGame = true;

        private float _savedTimeScale = 1f;
        private int _drawnVersion = -1;

        void Awake()
        {
            DialogueLog.MaxEntries = _rows.Length;
        }

        void OnDisable()
        {
            if (DialogueLog.IsOpen) Close();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool toggle = (kb != null && kb.lKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame);
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);

            if (!DialogueLog.IsOpen)
            {
                // 시작 독백 중에는 기록 창을 열지 않는다.
                if (toggle && !OpeningMonologue.IsPlaying) Open();
                return;
            }
            if (toggle || back)
            {
                Close();
                return;
            }
            if (_drawnVersion != DialogueLog.Version) Redraw();
            Scroll(ReadScrollInput(kb, pad));
        }

        /// <summary>
        /// 창을 열고 기록을 그린 뒤 맨 아래로 스크롤한다.
        /// _pauseGame을 사용하며, 창 활성 상태와 DialogueLog.IsOpen, Time.timeScale을 바꾼다.
        /// </summary>
        public void Open()
        {
            if (DialogueLog.IsOpen) return;
            DialogueLog.IsOpen = true;
            if (_pauseGame)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            _window.SetActive(true);
            Redraw();
            // 줄 높이를 먼저 계산해야 맨 아래 위치가 맞는다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        /// <summary>
        /// 창을 닫고 멈췄던 시간을 되돌린다.
        /// _savedTimeScale을 사용하며, 창 활성 상태와 DialogueLog.IsOpen, DialogueLog.ClosedFrame, Time.timeScale을 바꾼다.
        /// </summary>
        public void Close()
        {
            if (!DialogueLog.IsOpen) return;
            DialogueLog.IsOpen = false;
            DialogueLog.ClosedFrame = Time.frameCount;
            if (_pauseGame) Time.timeScale = _savedTimeScale;
            _window.SetActive(false);
        }

        /// <summary>
        /// 최근 기록을 줄 칸에 채우고, 남는 칸은 끈다.
        /// DialogueLog.Entries와 _rows를 사용하며, 줄 칸의 활성 상태와 글, _drawnVersion을 바꾼다.
        /// </summary>
        private void Redraw()
        {
            var entries = DialogueLog.Entries;
            int count = Mathf.Min(entries.Count, _rows.Length);
            int first = entries.Count - count;
            for (int i = 0; i < _rows.Length; i++)
            {
                bool show = i < count;
                if (_rows[i].Root.activeSelf != show) _rows[i].Root.SetActive(show);
                if (show) FillRow(_rows[i], entries[first + i]);
            }
            _drawnVersion = DialogueLog.Version;
        }

        /// <summary>
        /// 기록 한 줄을 종류에 맞는 정렬, 말풍선, 색으로 줄 칸에 넣는다.
        /// row, e와 말풍선 색 설정을 사용하며, 줄 칸의 정렬과 얼굴, 이름, 말풍선, 글을 바꾼다.
        /// </summary>
        private void FillRow(LogRow row, DialogueLog.Entry e)
        {
            bool narration = !e.IsPlayer && e.Speaker.Length == 0;
            row.Layout.childAlignment = e.IsPlayer ? TextAnchor.UpperRight : narration ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;

            // 플레이어 말은 이름 없이 오른쪽, 상대 말은 얼굴과 이름을 두고 왼쪽에 붙인다.
            bool other = !e.IsPlayer && !narration;
            if (row.Name.gameObject.activeSelf != other) row.Name.gameObject.SetActive(other);
            if (other) row.Name.text = e.Speaker;
            if (row.Face.gameObject.activeSelf != other) row.Face.gameObject.SetActive(other);
            // 얼굴이 없는 사람도 말풍선 위치가 같도록 칸은 남기고 그림만 끈다.
            row.Face.sprite = e.Portrait;
            row.Face.enabled = other && e.Portrait != null;

            row.Bubble.enabled = !narration;
            row.Bubble.color = e.IsPlayer ? _playerBubbleColor : _otherBubbleColor;
            row.Body.color = e.IsPlayer ? _playerTextColor : narration ? _narrationTextColor : _otherTextColor;
            row.Body.alignment = narration ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            row.Body.text = e.Text;

            // 짧은 말은 글 길이만큼, 긴 말은 최대 너비에서 줄을 바꾼다.
            row.BodyLayout.preferredWidth = Mathf.Min(row.Body.preferredWidth, _bubbleMaxWidth);
        }

        /// <summary>
        /// 키와 스틱의 위아래 입력을 읽는다.
        /// kb, pad를 사용하며, 위는 양수, 아래는 음수인 -1~1 값을 반환한다.
        /// </summary>
        private static float ReadScrollInput(Keyboard kb, Gamepad pad)
        {
            float dir = 0f;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir -= 1f;
            }
            if (pad != null)
            {
                float y = pad.leftStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.2f) dir += y;
            }
            return Mathf.Clamp(dir, -1f, 1f);
        }

        /// <summary>
        /// 목록을 위아래로 스크롤한다. 게임이 멈춰 있어도 움직이도록 unscaled 시간을 쓴다.
        /// dir와 _scrollSpeed를 사용하며, _scrollRect.verticalNormalizedPosition을 바꾼다.
        /// </summary>
        private void Scroll(float dir)
        {
            if (Mathf.Approximately(dir, 0f)) return;
            float range = _scrollRect.content.rect.height - _scrollRect.viewport.rect.height;
            if (range <= 0f) return;
            float pos = _scrollRect.verticalNormalizedPosition + dir * _scrollSpeed * Time.unscaledDeltaTime / range;
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(pos);
        }
    }
}
