using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 게임 시작 독백. 검은 화면에서 한 줄씩 한 글자씩 찍히고, 다 찍히면 ▼가 깜빡인다.
    /// Space / Enter / 클릭으로 (찍히는 중이면 한 번에 다 보여주고) 다음 줄로 넘긴다.
    /// 마지막 줄 다음엔 검은 화면이 서서히 걷히며 필드가 시작된다.
    /// 대사는 DefaultLines()에서 고치면 반영된다 (useScriptLines 켜짐). 인스펙터에서 고치려면 useScriptLines를 끈다.
    /// </summary>
    [ExecuteAlways]
    public class OpeningMonologue : MonoBehaviour
    {
        private static OpeningMonologue _current;

        [Header("재생")]
        [SerializeField, FormerlySerializedAs("playOnStart")] private bool _playOnStart = true;
        [Tooltip("에디트 모드에서 검은 화면과 첫 줄을 미리 보기")]
        [SerializeField, FormerlySerializedAs("previewInEditor")] private bool _previewInEditor;

        [Header("대사")]
        [Tooltip("켜면 lines 대신 이 스크립트의 DefaultLines() 대사를 쓴다. 끄면 인스펙터의 lines를 쓴다")]
        [SerializeField, FormerlySerializedAs("useScriptLines")] private bool _useScriptLines = true;
        [SerializeField, FormerlySerializedAs("lines"), TextArea(1, 3)] private List<string> _lines = DefaultLines();

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("group")] private CanvasGroup _group;
        [SerializeField, FormerlySerializedAs("text")] private Text _text;
        [Tooltip("다 찍히면 깜빡이는 ▼")]
        [SerializeField, FormerlySerializedAs("nextMark")] private Text _nextMark;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("fadeOutSeconds")] private float _fadeOutSeconds = 1.4f;
        [Tooltip("줄이 바뀔 때 잠깐 비워 두는 시간(초)")]
        [SerializeField, FormerlySerializedAs("gapBetweenLines")] private float _gapBetweenLines = 0.25f;

        [NonSerialized] private bool _playing;
        [NonSerialized] private bool _finishing;
        [NonSerialized] private int _index;
        [NonSerialized] private float _gap;
        [NonSerialized] private float _blinkT;

        public static bool IsPlaying => _current != null && _current._playing;

        void OnValidate() => EyeHooks.Repaint();

        void Start()
        {
            if (!Application.isPlaying) return;
            _current = this;
            SyncLines();
            if (_playOnStart && _lines != null && _lines.Count > 0) Begin();
            else Hide();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                SyncLines();
                // 에디트 모드에서는 평소엔 숨기고, 미리보기를 켜면 첫 줄을 보여준다.
                SetGroup(_previewInEditor ? 1f : 0f, false);
                if (_previewInEditor && _text != null && _lines != null && _lines.Count > 0 && _text.text != _lines[0]) _text.text = _lines[0];
                if (_nextMark != null && _nextMark.enabled != _previewInEditor) _nextMark.enabled = _previewInEditor;
                return;
            }
            if (!_playing) return;

            float dt = Time.unscaledDeltaTime;
            if (_finishing)
            {
                if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, dt / Mathf.Max(0.05f, _fadeOutSeconds));
                if (_group == null || _group.alpha <= 0f) Hide();
                return;
            }

            if (_gap > 0f)
            {
                _gap -= dt;
                if (_gap <= 0f) ShowLine();
                if (_nextMark != null) _nextMark.enabled = false;
                return;
            }

            Typewriter tw = _text != null ? _text.GetComponent<Typewriter>() : null;
            bool typing = tw != null && tw.IsTyping;

            if (_nextMark != null)
            {
                _blinkT += dt;
                bool show = !typing && Mathf.Repeat(_blinkT, 0.8f) < 0.5f;
                if (_nextMark.enabled != show) _nextMark.enabled = show;
            }

            if (!Pressed()) return;
            if (typing) { tw.Complete(); return; }

            _index++;
            if (_index >= _lines.Count)
            {
                _finishing = true;
                Typewriter.SetText(_text, "");
                if (_nextMark != null) _nextMark.enabled = false;
            }
            else
            {
                Typewriter.SetText(_text, "");
                _gap = _gapBetweenLines;
            }
        }

        /// <summary>
        /// 스크립트에 적힌 기본 독백 대사를 만든다.
        /// 입력값은 없으며, 새 대사 목록을 반환한다.
        /// </summary>
        public static List<string> DefaultLines() => new List<string>
        {
            "눈은 영혼의 창이라는 말이 있다.",
            "내겐 이 말이 단순한 비유가 아니다.",
            "언제부터인가, 사람의 눈을 들여다보면 그 너머 마음의 조각을 읽을 수 있게 되었다.",
            "상담소를 연 것도 그래서였다. 내가 본 것을 그들도 보았으면 해서.",
            "그토록 아름다운 마음의 조각들을.",
            "띵-동",
            "..?",
        };

        /// <summary>
        /// 독백을 처음부터 시작한다.
        /// 입력값은 없으며, 재생 상태를 켜고 검은 화면과 첫 줄을 보여준다.
        /// </summary>
        public void Begin()
        {
            _playing = true;
            _finishing = false;
            _index = 0;
            _gap = 0f;
            SetGroup(1f, true);
            ShowLine();
        }

        /// <summary>
        /// useScriptLines가 켜져 있으면 스크립트의 대사를 lines에 옮긴다.
        /// DefaultLines()를 사용하며, 다를 때만 _lines를 바꾼다.
        /// </summary>
        private void SyncLines()
        {
            if (!_useScriptLines) return;
            // Strings.csv에 opening 줄들이 있으면 그걸 쓴다 (Play 중에만, 씬은 바꾸지 않음).
            List<string> csv = Application.isPlaying ? Strings.Lines("opening") : null;
            if (csv != null) { _lines = csv; return; }
            List<string> d = DefaultLines();
            if (_lines != null && _lines.Count == d.Count && _lines.SequenceEqual(d)) return;
            _lines = d;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// 지금 줄을 글자 연출로 띄운다.
        /// _index를 사용하며, 독백 Text의 목표 글을 바꾼다.
        /// </summary>
        private void ShowLine() => Typewriter.SetText(_text, _lines[_index]);

        /// <summary>
        /// 독백을 끝내고 화면을 숨긴다.
        /// 입력값은 없으며, 재생 상태를 끄고 CanvasGroup을 숨긴다.
        /// </summary>
        private void Hide()
        {
            _playing = false;
            SetGroup(0f, false);
        }

        /// <summary>
        /// 독백 화면의 투명도와 입력 차단을 정한다.
        /// alpha, block을 사용하며, CanvasGroup 상태를 바꾼다.
        /// </summary>
        private void SetGroup(float alpha, bool block)
        {
            if (_group == null) return;
            if (!Mathf.Approximately(_group.alpha, alpha)) _group.alpha = alpha;
            if (_group.blocksRaycasts != block) _group.blocksRaycasts = block;
        }

        /// <summary>
        /// 이번 프레임에 넘기기 입력이 들어왔는지 확인한다.
        /// 키보드, 마우스, 게임패드 입력을 사용하며, 눌렸으면 true를 반환한다.
        /// </summary>
        private static bool Pressed()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) return true;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            if (pad != null && pad.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }
    }
}
