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

        void Update()
        {
            if (_flow == null) return;
            bool enc = _flow.CurrentState == GameFlow.State.Encounter || _flow.CurrentState == GameFlow.State.ZoomOut;
            SetGroup(_encounterGroup, enc);
            SetGroup(_fieldGroup, !enc);

            if (!enc)
            {
                FieldInteractable n = _flow.Nearest;
                string p = _flow.ShowingText ? "" : n != null ? $"Z  {n.Verb} — {n.DisplayName}" : "";
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
                if (_fieldHint != null)
                {
                    string h = _flow.ShowingText ? Strings.Get("hint.text_next", "Z  다음") : FieldCutscene.IsPlaying ? "" : Strings.Get("hint.field", "A D / ← →  이동   ·   Z  말 걸기 / 살펴보기");
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
