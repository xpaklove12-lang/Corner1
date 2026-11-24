using System;

using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046 FR-045: 열쇠로 문을 여는 컷신 창. 열쇠를 든 손이 자물쇠 구멍으로 들어가 열쇠를 돌린 뒤 창이 사라진다.
    /// 창과 그림은 씬에 미리 만들어 두고 여기서는 참조만 한다.
    /// </summary>
    public class GateUnlockScene : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("창 전체의 투명도를 바꿀 그룹")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("자물쇠 판 그림. 열쇠가 돌아갈 때 색이 잠깐 바뀐다")]
        [SerializeField] private Graphic _lock;
        [Tooltip("열쇠를 든 손 그림. 피벗을 열쇠 끝에 두어 돌릴 때 끝이 구멍에 머물게 한다")]
        [SerializeField] private RectTransform _hand;
        [Tooltip("손 그림을 바꿔 끼울 Image")]
        [SerializeField] private Image _handImage;
        [Tooltip("열쇠를 돌린 뒤의 손 그림. 비워 두면 그림을 바꾸지 않고 납작하게 줄여 돌린 것처럼 보이게 한다")]
        [SerializeField] private Sprite _turnedSprite;
        [Tooltip("손이 출발하는 자리")]
        [SerializeField] private RectTransform _handStart;
        [Tooltip("열쇠가 구멍에 들어간 자리")]
        [SerializeField] private RectTransform _handInserted;

        [Header("연출")]
        [Tooltip("창이 나타나고 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _fadeSeconds = 0.2f;
        [Tooltip("손이 구멍까지 가는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _insertSeconds = 0.8f;
        [Tooltip("열쇠를 넣은 뒤 돌리기 전에 멈추는 시간(초)")]
        [SerializeField, Min(0f)] private float _pauseSeconds = 0.2f;
        [Tooltip("열쇠를 돌리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _turnSeconds = 0.35f;
        [Tooltip("열쇠를 돌릴 때 손이 기우는 각도(도). 음수면 시계 방향")]
        [SerializeField] private float _turnDegrees = -8f;
        [Tooltip("열쇠를 돌렸을 때 손 그림의 세로 배율. 열쇠가 축을 따라 돌아 납작하게 보이도록 줄인다")]
        [SerializeField, Range(0.1f, 1f)] private float _turnSquash = 0.5f;
        [Tooltip("돌린 뒤 창이 사라지기 전에 멈추는 시간(초)")]
        [SerializeField, Min(0f)] private float _holdSeconds = 0.35f;
        [Tooltip("열쇠가 돌아갔을 때 자물쇠 판의 색")]
        [SerializeField] private Color _turnedColor = new Color(0.8f, 1f, 0.85f, 1f);
        private Color _lockColor;
        private Sprite _handSprite;
        private bool _colorSaved;
        private bool _open;
        private float _elapsed;

        /// <summary>컷신이 끝나 창이 닫힌 직후에 불린다.</summary>
        public event Action Finished;

        public bool IsOpen => _open;

        void Update()
        {
            // 대화 기록 창이 열려 있으면 컷신을 멈춘다.
            if (!_open || DialogueLog.BlocksInput) return;
            _elapsed += Time.unscaledDeltaTime;
            Apply();
            if (_elapsed < TotalSeconds()) return;
            Close();
            Finished?.Invoke();
        }

        /// <summary>
        /// 컷신 창을 열고 처음부터 재생한다.
        /// 입력값은 없으며, 창의 표시와 진행 시간을 바꾼다.
        /// </summary>
        public void Open()
        {
            gameObject.SetActive(true);
            if (!_colorSaved) { _colorSaved = true; _lockColor = _lock.color; _handSprite = _handImage.sprite; }
            _open = true;
            _elapsed = 0f;
            Apply();
        }

        /// <summary>
        /// 컷신 창을 닫는다. 알림은 보내지 않는다.
        /// 입력값은 없으며, 창의 표시와 열림 상태를 바꾼다.
        /// </summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 컷신 전체 길이를 구한다.
        /// 각 구간의 시간을 사용하며, 합친 시간(초)을 반환한다.
        /// </summary>
        private float TotalSeconds()
        {
            return _fadeSeconds + _insertSeconds + _pauseSeconds + _turnSeconds + _holdSeconds + _fadeSeconds;
        }

        /// <summary>
        /// 지금 진행 시간에 맞춰 창의 투명도, 손의 자리와 기울기와 납작함, 자물쇠 판의 색을 정한다.
        /// _elapsed와 각 구간의 시간을 사용하며, _group, _hand, _lock을 바꾼다.
        /// </summary>
        private void Apply()
        {
            float insertFrom = _fadeSeconds;
            float turnFrom = insertFrom + _insertSeconds + _pauseSeconds;
            float fadeOutFrom = turnFrom + _turnSeconds + _holdSeconds;
            float fadeIn = Mathf.Clamp01(_elapsed / _fadeSeconds);
            float fadeOut = Mathf.Clamp01((_elapsed - fadeOutFrom) / _fadeSeconds);
            _group.alpha = Mathf.Min(fadeIn, 1f - fadeOut);

            // 손은 출발 자리에서 구멍까지 가서 멈춘 뒤, 열쇠 끝을 축으로 돌아간다.
            float insert = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_elapsed - insertFrom) / _insertSeconds));
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_elapsed - turnFrom) / _turnSeconds));
            _hand.position = Vector3.Lerp(_handStart.position, _handInserted.position, insert);
            _hand.localRotation = Quaternion.Euler(0f, 0f, _turnDegrees * turn);
            // 돌린 뒤의 그림이 있으면 돌리는 중간에 바꿔 끼우고, 없으면 납작하게 줄인다.
            if (_turnedSprite != null) _handImage.sprite = turn >= 0.5f ? _turnedSprite : _handSprite;
            else _hand.localScale = new Vector3(1f, Mathf.Lerp(1f, _turnSquash, turn), 1f);
            _lock.color = Color.Lerp(_lockColor, _turnedColor, turn);
        }
    }
}
