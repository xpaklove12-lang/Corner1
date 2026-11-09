using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>필드 UI(말 걸기 안내, 텍스트 상자)와 만남 UI를 상태에 맞게 켜고 끈다. 에디트 모드에서도 반영된다.</summary>
    [ExecuteAlways]
    public class GameFlowUI : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("flow")] private GameFlow _flow;
        [SerializeField, FormerlySerializedAs("encounterGroup")] private CanvasGroup _encounterGroup;
        [SerializeField, FormerlySerializedAs("fieldGroup")] private CanvasGroup _fieldGroup;
        [SerializeField, FormerlySerializedAs("prompt")] private Text _prompt;
        [SerializeField, FormerlySerializedAs("textBox")] private GameObject _textBox;
        [SerializeField, FormerlySerializedAs("textBoxLabel")] private Text _textBoxLabel;
        [SerializeField, FormerlySerializedAs("fieldHint")] private Text _fieldHint;
        [SerializeField, FormerlySerializedAs("fadeOverlay")] private Image _fadeOverlay;

        [Header("초상화")]
        [Tooltip("텍스트 상자 왼쪽의 초상화")]
        [SerializeField, FormerlySerializedAs("portrait")] private Image _portrait;
        [Tooltip("초상화가 있을 때 글자를 오른쪽으로 미는 비율 (상자 너비 기준)")]
        [SerializeField, FormerlySerializedAs("portraitWidth"), Range(0f, 0.5f)] private float _portraitWidth = 0.2f;

        [Header("넘김 표시")]
        [Tooltip("텍스트 상자 오른쪽 아래의 삼각형. 한 줄이 다 찍히면 보여 Space·Enter로 넘길 수 있음을 알린다")]
        [SerializeField] private Text _advanceMark;
        [Tooltip("삼각형이 위아래로 오르내리는 폭 (픽셀)")]
        [SerializeField] private float _markBob = 4f;
        [Tooltip("삼각형이 오르내리는 빠르기 (초당 왕복 수)")]
        [SerializeField] private float _markBobSpeed = 1.5f;

        private bool _markBaseSaved;
        private Vector2 _markBasePos;

        void Update()
        {
            if (_flow == null) return;
            bool enc = _flow.CurrentState == GameFlow.State.Encounter || _flow.CurrentState == GameFlow.State.ZoomOut;
            SetGroup(_encounterGroup, enc);
            SetGroup(_fieldGroup, !enc);

            if (!enc)
            {
                FieldInteractable n = _flow.Nearest;
                string p = _flow.ShowingText ? "" : n != null ? $"Space  {n.Verb} — {n.DisplayName}" : "";
                if (_prompt != null && _prompt.text != p) _prompt.text = p;
                if (_textBox != null && _textBox.activeSelf != _flow.ShowingText) _textBox.SetActive(_flow.ShowingText);
                bool hasPortrait = _flow.ShowingText && _flow.FieldPortrait != null;
                if (_portrait != null)
                {
                    if (_portrait.enabled != hasPortrait) _portrait.enabled = hasPortrait;
                    if (hasPortrait && _portrait.sprite != _flow.FieldPortrait) _portrait.sprite = _flow.FieldPortrait;
                }
                if (_textBoxLabel != null)
                {
                    RectTransform rt = _textBoxLabel.rectTransform;
                    float x = hasPortrait && _portrait != null ? _portraitWidth : 0f;
                    if (!Mathf.Approximately(rt.anchorMin.x, x)) rt.anchorMin = new Vector2(x, rt.anchorMin.y);
                    Typewriter.SetText(_textBoxLabel, _flow.FieldText ?? "");
                }
                if (_advanceMark != null && Application.isPlaying) UpdateAdvanceMark(_flow.ShowingText && !Typewriter.AnyTyping);
                if (_fieldHint != null)
                {
                    // 문을 열어 주기를 기다리는 동안에는 조작 안내 대신 문 안내를 띄운다.
                    string guide = FieldCutscene.DoorGuide;
                    string h = _flow.ShowingText ? Strings.Get("hint.text_next", "Space  다음") : guide != null ? guide : FieldCutscene.IsPlaying ? "" :Strings.Get("hint.field", "A D / ← →  이동   ·   Space  말 걸기 / 살펴보기");
                    if (_fieldHint.text != h) _fieldHint.text = h;
                }
            }

            if (_fadeOverlay != null)
            {
                var c = new Color(0f, 0f, 0f, _flow.Fade);
                if (_fadeOverlay.color != c) _fadeOverlay.color = c;
            }
        }

        /// <summary>
        /// 넘김 삼각형을 켜거나 끄고, 켜져 있으면 제자리에서 위아래로 오르내리게 한다.
        /// on과 _markBob, _markBobSpeed를 사용하며, _advanceMark의 enabled와 anchoredPosition을 바꾼다.
        /// </summary>
        private void UpdateAdvanceMark(bool on)
        {
            RectTransform rt = _advanceMark.rectTransform;
            // 씬에 저장된 위치를 기준점으로 삼아 그 위로만 오르내린다.
            if (!_markBaseSaved) { _markBasePos = rt.anchoredPosition; _markBaseSaved = true; }
            if (_advanceMark.enabled != on) _advanceMark.enabled = on;
            float y = on ? _markBob * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _markBobSpeed * 2f * Mathf.PI)) : 0f;
            rt.anchoredPosition = _markBasePos + new Vector2(0f, y);
        }

        /// <summary>
        /// CanvasGroup을 보이게 하거나 숨긴다.
        /// g, on을 사용하며, 투명도와 입력 차단 여부를 바꾼다.
        /// </summary>
        private static void SetGroup(CanvasGroup g, bool on)
        {
            if (g == null) return;
            float a = on ? 1f : 0f;
            if (!Mathf.Approximately(g.alpha, a)) g.alpha = a;
            if (g.interactable != on) g.interactable = on;
            if (g.blocksRaycasts != on) g.blocksRaycasts = on;
        }
    }
}
