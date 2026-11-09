using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>디렉터 상태를 UI에 보여준다. 에디트 모드에서도 갱신된다.</summary>
    [ExecuteAlways]
    public class EyeMazeUI : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("director")] private EyeMazeDirector _director;

        [Header("글")]
        [SerializeField, FormerlySerializedAs("status")] private Text _status;
        [SerializeField, FormerlySerializedAs("situation")] private Text _situation;
        [SerializeField, FormerlySerializedAs("message")] private Text _message;
        [SerializeField, FormerlySerializedAs("memories")] private Text _memories;
        [SerializeField, FormerlySerializedAs("hint")] private Text _hint;
        [Tooltip("상황 앞에 붙는 표시")]
        [SerializeField, FormerlySerializedAs("situationPrefix")] private string _situationPrefix = "▶ ";
        [Tooltip("대화(반응) 앞에 붙는 표시")]
        [SerializeField, FormerlySerializedAs("messagePrefix")] private string _messagePrefix = "* ";

        [Header("선택지")]
        [SerializeField, FormerlySerializedAs("optionButtons")] private Button[] _optionButtons = new Button[3];
        [SerializeField, FormerlySerializedAs("optionLabels")] private Text[] _optionLabels = new Text[3];
        [Tooltip("첫 노드가 아닐 때 보이는 '처음 이야기로' 공통 선택지 버튼")]
        [SerializeField] private Button _returnButton;
        [SerializeField] private Text _returnLabel;
        [SerializeField, FormerlySerializedAs("gazeButtonLabel")] private Text _gazeButtonLabel;

        [Header("부담감")]
        [Tooltip("부담감 막대의 채움. 피벗을 아래에 두고 세로 스케일로 채운다")]
        [SerializeField] private RectTransform _burdenFill;
        [Tooltip("대화로 부담감이 갑자기 바뀔 때 막대가 따라가는 속도 (초당 0~1 비율)")]
        [SerializeField] private float _burdenAnimSpeed = 0.5f;
        // 스펙 006: 깜빡일 때 가릴 부담감 표시 참조와 현재 투명도 추가
        [Tooltip("깜빡일 때 함께 가려지는 부담감 막대와 글")]
        [SerializeField] private Graphic[] _burdenGraphics = new Graphic[0];

        [Header("신뢰도 막대")]
        [Tooltip("신뢰도 막대의 핸들. 가운데가 신뢰 0, 왼쪽 끝이 손님이 떠나는 신뢰(leaveTrust), 오른쪽 끝이 진실 엔딩 신뢰(truthTrust)")]
        [SerializeField] private RectTransform _trustHandle;
        [Tooltip("가운데에서 핸들까지 칠하는 채움")]
        [SerializeField] private RectTransform _trustFill;
        [Tooltip("신뢰에 따라 색이 바뀌는 그림들 (핸들, 채움)")]
        [SerializeField] private Graphic[] _trustTinted = new Graphic[0];
        [SerializeField] private Color _trustNeutralColor = Color.white;
        [SerializeField] private Color _trustNegativeColor = new Color(0.95f, 0.27f, 0.24f, 1f);
        [SerializeField] private Color _trustPositiveColor = new Color(0.13f, 0.56f, 0.97f, 1f);
        [Tooltip("신뢰가 바뀔 때 핸들이 따라가는 속도 (초당 -1~1 비율)")]
        [SerializeField] private float _trustAnimSpeed = 3f;

        [NonSerialized] private float _burdenShown = -1f;
        [NonSerialized] private float _trustShown = float.NaN;
        [NonSerialized] private float _burdenAlpha = -1f;

        // 스펙 010: 기억 목록 줄 강조 시간과 현재 강조 상태 추가
        [Header("기억 목록 강조")]
        [Tooltip("새로 얻은 파편 줄이 원래 색으로 돌아오는 시간(초)")]
        [SerializeField] private float _highlightSeconds = 0.6f;

        [NonSerialized] private float _highlightT;
        [NonSerialized] private string _highlightTitle;
        [NonSerialized] private Color _highlightColor;

        // 스펙 006: 깜빡임 전체 화면 페이드 필드 제거

        // 스펙 010: 파편 획득과 연출 정리 이벤트 구독
        void OnEnable()
        {
            MemoryFragment.PickedUp += OnFragmentPickedUp;
            MemoryFragment.FeedbackCleared += OnFeedbackCleared;
        }

        void OnDisable()
        {
            MemoryFragment.PickedUp -= OnFragmentPickedUp;
            MemoryFragment.FeedbackCleared -= OnFeedbackCleared;
        }

        void Update()
        {
            EyeMazeDirector d = _director;
            if (d == null) return;
            Person p = d.Person;

            if (p != null)
            {
                Set(_status, Strings.Get("ui.status", "신뢰 {trust}",
                    ("trust", p.Trust > 0 ? "+" + p.Trust : p.Trust.ToString()), ("dilation", (p.Dilation * 100f).ToString("0")), ("mode", ModeName(d.CurrentMode))));
                UpdateTrustBar(p.Trust, d.LeaveTrust, d.TruthTrust);
            }

            DialogueScript.Node node = d.CurrentNode;
            string sit;
            if (d.CurrentMode == EyeMazeDirector.Mode.Ending)
            {
                EndingDef e = d.CurrentEnding;
                sit = e != null && !string.IsNullOrEmpty(e.Situation) ? e.Situation : Strings.Get("ui.ending_situation", "{name}의 마음이 들린다.", ("name", p != null ? p.DisplayName : "그녀"));
            }
            else if (d.InAbyss) sit = Strings.Get("ui.abyss", "그녀의 마음 속 심연");
            else if (d.IsReading) sit = Strings.Get("ui.reading", "시선이 파편에 머문다.");
            else if (d.CurrentMode == EyeMazeDirector.Mode.Gaze)
            {
                MazeLayer layer = d.CursorLayer;
                sit = Strings.Get("ui.gaze_situation", "{name}의 눈 속 — {layer}", ("name", p != null ? p.DisplayName : ""), ("layer", layer != null ? layer.DisplayName : ""));
                // 스펙 009: 신호 중 들여다보면 대화로 돌아오라는 온보딩 문구를 추가
                if (d.RestRequested) sit += "\n" + Strings.Get("ui.rest_hint_gaze", "{name}가 하고 싶은 말이 있는 거 같다. 잠시 관찰을 멈춰보자.", ("name", p != null ? p.DisplayName : ""));
            }
            // 스펙 009: 신호 중에는 장면 설명 자리에 쉬자는 대사를 표시
            // 스펙 009: 쉬게 해 줄 때임을 알리는 온보딩 문구를 대사 아래에 추가
            else if (d.RestRequested) sit = d.RestLine + "\n" + Strings.Get("ui.rest_hint_talk", "(잠시 쉴 시간이 필요한 거 같다.)");
            else sit = node != null ? node.Situation : d.IdleSituation;
            // 공용 문구 속 이름과 대명사를 지금 손님에 맞춘다.
            sit = d.Personalize(sit);
            Set(_situation, string.IsNullOrEmpty(sit) ? "" : _situationPrefix + sit);
            Set(_message, Prefixed(d.Personalize(d.Message), _messagePrefix));
            // 스펙 010: 새로 얻은 파편 줄을 강조해 표시
            Set(_memories, HighlightedSummary(d.CollectedSummary()));

            IReadOnlyList<EyeMazeDirector.ChoiceView> choices = d.CurrentMode == EyeMazeDirector.Mode.Talk
                ? d.CurrentChoices() : System.Array.Empty<EyeMazeDirector.ChoiceView>();
            EnsureButtons(choices.Count);
            LayoutButtons(choices.Count);
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                bool show = i < choices.Count;
                if (_optionButtons[i] != null && _optionButtons[i].gameObject.activeSelf != show)
                    _optionButtons[i].gameObject.SetActive(show);
                if (!show || i >= _optionLabels.Length) continue;
                EyeMazeDirector.ChoiceView c = choices[i];
                Set(_optionLabels[i], c.Available ? $"{i + 1}. {c.Text}" : $"{i + 1}. " + Strings.Get("ui.locked_choice", "??? (아직 모르는 이야기)"));
            }

            if (_burdenFill != null)
            {
                // Play에서는 막대가 실제 값을 일정한 속도로 따라가 차오르거나 떨어지는 모습이 보인다. 에디트 모드에서는 바로 맞춘다.
                bool snap = !Application.isPlaying || _burdenShown < 0f;
                _burdenShown = snap ? d.Burden : Mathf.MoveTowards(_burdenShown, d.Burden, _burdenAnimSpeed * Time.deltaTime);
                Vector3 scale = new Vector3(1f, _burdenShown, 1f);
                if (_burdenFill.localScale != scale) _burdenFill.localScale = scale;
            }

            // 스펙 006: 눈꺼풀이 닫히는 만큼 부담감 막대와 글을 가린다.
            float burdenAlpha = 1f - d.BlinkAmount;
            if (!Mathf.Approximately(burdenAlpha, _burdenAlpha))
            {
                _burdenAlpha = burdenAlpha;
                foreach (Graphic g in _burdenGraphics) g.canvasRenderer.SetAlpha(burdenAlpha);
            }

            bool showReturn = d.CanReturnToStart;
            if (_returnButton != null && _returnButton.gameObject.activeSelf != showReturn)
                _returnButton.gameObject.SetActive(showReturn);
            if (showReturn)
            {
                // 스펙 004: 보이는 선택지 바로 다음 번호와 자리에 표시
                Set(_returnLabel, $"{choices.Count + 1}. {d.ReturnText}");
                PlaceReturnButton(choices.Count);
            }

            // 스펙 004: 들어갈 때는 마음 읽기, 나올 때는 관찰 중지로 표시
            Set(_gazeButtonLabel, d.CurrentMode == EyeMazeDirector.Mode.Gaze ? Strings.Get("ui.gaze_button_gaze", "관찰 중지 (Space)") : Strings.Get("ui.gaze_button", "마음 읽기 (Space)"));
            Set(_hint, d.IsReading
                ? Strings.Get("hint.reading", "Space  계속")
                : d.InAbyss
                ? Strings.Get("hint.abyss", "WASD / 방향키로 가장 반짝이는 파편에 다가간다")
                : d.CurrentMode == EyeMazeDirector.Mode.Gaze
                // 시선 조작 안내에서 금색 문, 하늘색 점 설명 삭제
                ? Strings.Get("hint.gaze", "WASD / 방향키로 이동   ·   Space 관찰 중지")
                : d.CurrentMode == EyeMazeDirector.Mode.Ending ? (d.HeartFinished ? Strings.Get("hint.ending_done", "Space  돌아가기") : Strings.Get("hint.ending", "Space  계속"))
                // 스펙 004: Q 안내를 잠시 휴식하기로 표시
                : Strings.Get("hint.talk", "Space = 마음 읽기   ·   숫자키 대화   ·   Q 잠시 휴식하기"));
        }

        /// <summary>
        /// 새로 얻은 파편의 이름과 색을 기억하고 줄 강조를 시작한다.
        /// fragment를 사용하며, _highlightTitle, _highlightColor, _highlightT를 바꾼다.
        /// </summary>
        private void OnFragmentPickedUp(MemoryFragment fragment)
        {
            _highlightTitle = fragment.Title;
            _highlightColor = fragment.Tint;
            _highlightT = _highlightSeconds;
        }

        /// <summary>
        /// 재생 중인 줄 강조를 바로 지운다.
        /// 입력값은 없으며, _highlightT를 0으로 바꾼다.
        /// </summary>
        private void OnFeedbackCleared() => _highlightT = 0f;

        /// <summary>
        /// 강조 중이면 기억 목록에서 새 파편 줄에 색을 입히고 남은 시간을 줄인다.
        /// summary와 _highlightT를 사용하며, 색을 입힌 목록 글을 반환하고 _highlightT를 바꾼다.
        /// </summary>
        private string HighlightedSummary(string summary)
        {
            if (_highlightT <= 0f) return summary;
            _highlightT = Mathf.Max(0f, _highlightT - Time.deltaTime);
            // 스펙 010: 줄 색이 원래 색에서 파편 색으로 갔다가 원래 색으로 돌아오도록 계산
            float progress = 1f - _highlightT / Mathf.Max(0.01f, _highlightSeconds);
            Color color = Color.Lerp(_memories.color, _highlightColor, Mathf.Sin(progress * Mathf.PI));
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            string target = "· " + _highlightTitle;
            string[] lines = summary.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] == target) lines[i] = $"<color=#{hex}>{target}</color>";
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 신뢰도 막대의 핸들 위치와 채움, 색을 신뢰에 맞춘다. 가운데가 0이고, 왼쪽 반은 0~leave를, 오른쪽 반은 0~truth를 같은 칸으로 나눈다.
        /// trust, leave(음수), truth를 사용하며, 핸들과 채움의 앵커, _trustTinted의 색, _trustShown을 바꾼다.
        /// </summary>
        private void UpdateTrustBar(int trust, int leave, int truth)
        {
            if (_trustHandle == null && _trustFill == null) return;
            // 음수 쪽은 떠나는 신뢰에서, 양수 쪽은 진실 엔딩 신뢰에서 막대 끝에 정확히 닿는다.
            float target = trust < 0
                ? -Mathf.Clamp01(trust / (float)Mathf.Min(-1, leave))
                : Mathf.Clamp01(trust / (float)Mathf.Max(1, truth));
            // Play에서는 핸들이 일정한 속도로 따라가고, 에디트 모드에서는 바로 맞춘다.
            bool snap = !Application.isPlaying || float.IsNaN(_trustShown);
            _trustShown = snap ? target : Mathf.MoveTowards(_trustShown, target, _trustAnimSpeed * Time.deltaTime);

            float x = 0.5f + 0.5f * _trustShown;
            if (_trustHandle != null)
            {
                Vector2 a = new Vector2(x, 0.5f);
                if (_trustHandle.anchorMin != a) { _trustHandle.anchorMin = a; _trustHandle.anchorMax = a; }
            }
            if (_trustFill != null)
            {
                Vector2 min = new Vector2(Mathf.Min(0.5f, x), 0f);
                Vector2 max = new Vector2(Mathf.Max(0.5f, x), 1f);
                if (_trustFill.anchorMin != min) _trustFill.anchorMin = min;
                if (_trustFill.anchorMax != max) _trustFill.anchorMax = max;
            }
            Color c = _trustShown < 0f
                ? Color.Lerp(_trustNeutralColor, _trustNegativeColor, -_trustShown)
                : Color.Lerp(_trustNeutralColor, _trustPositiveColor, _trustShown);
            foreach (Graphic g in _trustTinted)
                if (g != null && g.color != c) g.color = c;
        }

        /// <summary>
        /// 선택지가 버튼보다 많으면(예: 선택지 3개 + 뒤로가기) Play 중에 마지막 버튼을 복제해 늘린다.
        /// 복제한 버튼은 씬에 저장되지 않는다. count를 사용하며, _optionButtons와 _optionLabels를 늘린다.
        /// </summary>
        private void EnsureButtons(int count)
        {
            if (!Application.isPlaying || count <= _optionButtons.Length || _optionButtons.Length == 0) return;
            Button last = _optionButtons[_optionButtons.Length - 1];
            if (last == null) return;
            if (_bandMax < 0f) RememberBand();
            var buttons = new List<Button>(_optionButtons);
            var labels = new List<Text>(_optionLabels);
            while (buttons.Count < count)
            {
                int idx = buttons.Count;
                Button b = Instantiate(last, last.transform.parent);
                b.name = $"선택지 {idx + 1}";
                // 복제하면 원래 버튼의 클릭 연결(ChooseOption2)까지 따라오므로 새로 건다.
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => { if (_director != null) _director.ChooseOption(idx); });
                buttons.Add(b);
                labels.Add(b.GetComponentInChildren<Text>(true));
            }
            _optionButtons = buttons.ToArray();
            _optionLabels = labels.ToArray();
        }

        // 원래 버튼들이 차지하던 세로 범위 (anchor y). 보이는 버튼 수에 맞춰 이 안을 나눠 쓴다.
        [System.NonSerialized] private float _bandMin = -1f, _bandMax = -1f, _bandGap;
        [System.NonSerialized] private int _laidOut = -1;
        // 스펙 004: 공통 선택지 버튼의 씬 저장 자리 기억
        [System.NonSerialized] private bool _returnAnchorSaved;
        [System.NonSerialized] private Vector2 _returnAnchorMin;
        [System.NonSerialized] private Vector2 _returnAnchorMax;

        /// <summary>
        /// 원래 버튼들이 차지하던 세로 범위와 간격을 기억한다.
        /// _optionButtons를 사용하며, _bandMin/_bandMax/_bandGap을 쓴다.
        /// </summary>
        private void RememberBand()
        {
            _bandMin = 1f; _bandMax = 0f;
            float h = 0f;
            int n = 0;
            foreach (Button b in _optionButtons)
            {
                if (b == null) continue;
                var rt = (RectTransform)b.transform;
                _bandMin = Mathf.Min(_bandMin, rt.anchorMin.y);
                _bandMax = Mathf.Max(_bandMax, rt.anchorMax.y);
                h += rt.anchorMax.y - rt.anchorMin.y;
                n++;
            }
            _bandGap = n > 1 ? Mathf.Max(0f, (_bandMax - _bandMin - h) / (n - 1)) : 0f;
        }

        /// <summary>
        /// Play 중에 공통 선택지 버튼을 보이는 선택지 바로 다음 칸에 놓는다. 다음 칸이 없으면 씬에 저장된 자리로 돌아간다.
        /// 보이는 선택지 수 count를 사용하며, _returnButton의 anchor를 바꾼다.
        /// </summary>
        private void PlaceReturnButton(int count)
        {
            if (!Application.isPlaying) return;
            var rt = (RectTransform)_returnButton.transform;
            if (!_returnAnchorSaved)
            {
                _returnAnchorSaved = true;
                _returnAnchorMin = rt.anchorMin;
                _returnAnchorMax = rt.anchorMax;
            }
            bool hasSlot = count < _optionButtons.Length;
            RectTransform slot = hasSlot ? (RectTransform)_optionButtons[count].transform : null;
            rt.anchorMin = hasSlot ? slot.anchorMin : _returnAnchorMin;
            rt.anchorMax = hasSlot ? slot.anchorMax : _returnAnchorMax;
        }

        /// <summary>
        /// Play 중에 버튼이 원래보다 많아졌을 때만, 보이는 버튼 수에 맞춰 세로로 고르게 다시 놓는다.
        /// count를 사용하며, 버튼들의 anchor를 바꾼다.
        /// </summary>
        private void LayoutButtons(int count)
        {
            if (!Application.isPlaying || _bandMax < 0f || count == _laidOut || count <= 0) return;
            _laidOut = count;
            float slot = (_bandMax - _bandMin - _bandGap * (count - 1)) / count;
            for (int i = 0; i < count && i < _optionButtons.Length; i++)
            {
                if (_optionButtons[i] == null) continue;
                var rt = (RectTransform)_optionButtons[i].transform;
                float top = _bandMax - i * (slot + _bandGap);
                rt.anchorMin = new Vector2(rt.anchorMin.x, top - slot);
                rt.anchorMax = new Vector2(rt.anchorMax.x, top);
            }
        }

        /// <summary>
        /// 빈 줄이 아닌 각 문단 앞에 표시를 붙인다.
        /// text, prefix를 사용하며, 표시가 붙은 글을 반환한다.
        /// </summary>
        private static string Prefixed(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                bool startsParagraph = i == 0 || lines[i - 1].Trim().Length == 0;
                if (lines[i].Trim().Length > 0 && startsParagraph) lines[i] = prefix + lines[i];
                else if (lines[i].Trim().Length > 0) lines[i] = new string(' ', prefix.Length) + lines[i];
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// 디렉터 모드를 화면에 보일 이름으로 바꾼다.
        /// m을 사용하며, 모드 이름을 반환한다.
        /// </summary>
        private static string ModeName(EyeMazeDirector.Mode m)
        {
            switch (m)
            {
                case EyeMazeDirector.Mode.Gaze: return Strings.Get("mode.gaze", "들여다보는 중");
                case EyeMazeDirector.Mode.Ending: return Strings.Get("mode.ending", "마음");
                default: return Strings.Get("mode.talk", "대화 중");
            }
        }

        /// <summary>
        /// Text에 글을 쓴다. Typewriter가 있으면 한 글자씩 찍는다.
        /// t, s를 사용하며, Text의 글을 바꾼다.
        /// </summary>
        private static void Set(Text t, string s) => Typewriter.SetText(t, s ?? "");
    }
}
