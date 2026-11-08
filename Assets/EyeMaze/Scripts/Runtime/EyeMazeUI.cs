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
        [SerializeField, FormerlySerializedAs("gazeButtonLabel")] private Text _gazeButtonLabel;

        [Header("태엽")]
        [Tooltip("태엽 막대 (남은 태엽 / 용량)")]
        [SerializeField, FormerlySerializedAs("springFill")] private RectTransform _springFill;
        [SerializeField, FormerlySerializedAs("springFillImage")] private Image _springFillImage;
        [SerializeField, FormerlySerializedAs("springLabel")] private Text _springLabel;
        [Tooltip("태엽을 감을수록 도는 톱니 아이콘")]
        [SerializeField, FormerlySerializedAs("springGear")] private RectTransform _springGear;
        [Tooltip("켜면 막대가 아래에서 위로 차오름")]
        [SerializeField, FormerlySerializedAs("springVertical")] private bool _springVertical = true;
        [Tooltip("막대 위의 파란 목표선 (태엽의 targetPoint 높이에 맞춰짐)")]
        [SerializeField, FormerlySerializedAs("springTarget")] private RectTransform _springTarget;

        [Header("깜빡임")]
        [SerializeField, FormerlySerializedAs("blinkOverlay")] private Image _blinkOverlay;

        void Update()
        {
            EyeMazeDirector d = _director;
            if (d == null) return;
            Person p = d.Person;

            if (p != null)
                Set(_status, $"신뢰 {p.Trust}   ·   동공 {p.Dilation * 100f:0}%   ·   {ModeName(d.CurrentMode)}");

            DialogueNode node = d.CurrentNode;
            WindingIntro w = d.Intro;
            string sit;
            if (d.CurrentMode == EyeMazeDirector.Mode.Ending)
            {
                EndingDef e = d.CurrentEnding;
                sit = e != null && !string.IsNullOrEmpty(e.Situation) ? e.Situation : $"{(p != null ? p.DisplayName : "그녀")}의 마음이 들린다.";
            }
            else if (d.InAbyss) sit = "그녀의 마음 속 심연";
            else if (d.IsReading) sit = "시선이 파편에 머문다. 태엽이 잠시 멈췄다.";
            else if (d.CurrentMode == EyeMazeDirector.Mode.Gaze)
            {
                MazeLayer layer = d.CursorLayer;
                sit = $"{(p != null ? p.DisplayName : "")}의 눈 속 — {(layer != null ? layer.DisplayName : "")}";
            }
            else sit = node != null ? node.Situation : d.IdleSituation;
            // 공용 문구 속 이름과 대명사를 지금 손님에 맞춘다.
            sit = d.Personalize(sit);
            Set(_situation, string.IsNullOrEmpty(sit) ? "" : _situationPrefix + sit);
            Set(_message, Prefixed(d.Personalize(d.Message), _messagePrefix));
            Set(_memories, d.CollectedSummary());

            for (int i = 0; i < _optionButtons.Length; i++)
            {
                bool show = d.CurrentMode == EyeMazeDirector.Mode.Talk && node != null && i < node.Options.Count;
                if (_optionButtons[i] != null && _optionButtons[i].gameObject.activeSelf != show)
                    _optionButtons[i].gameObject.SetActive(show);
                if (!show || i >= _optionLabels.Length) continue;
                DialogueOption o = node.Options[i];
                Set(_optionLabels[i], d.IsAvailable(o) ? $"{i + 1}. {o.Text}" : $"{i + 1}. ??? (아직 모르는 이야기)");
            }

            Set(_gazeButtonLabel, d.CurrentMode == EyeMazeDirector.Mode.Gaze ? "시선 거두기 (Space)"
                : (w != null && w.IsHolding ? "…파란 선에서 뗀다" : "Space 꾹 → 파란 선에서 떼기"));
            Set(_hint, d.IsReading
                ? "Space / Z  계속 (읽는 동안 태엽이 멈춤)"
                : d.InAbyss
                ? "WASD / 방향키로 가장 반짝이는 파편에 다가간다"
                : d.CurrentMode == EyeMazeDirector.Mode.Gaze
                ? "WASD / 방향키로 이동   ·   Space 시선 거두기   ·   금색 = 잠긴 문   ·   하늘색 점 = 파편"
                : d.CurrentMode == EyeMazeDirector.Mode.Ending ? (d.HeartFinished ? "Space / Z  돌아가기" : "Space / Z  계속")
                : "Space 꾹 = 태엽 감기, 파란 선에서 떼면 들여다보기   ·   1 2 3 대화   ·   Q 자리 떠나기");

            if (w != null)
            {
                if (_springFill != null)
                {
                    float g = w.Gauge01;
                    Vector2 max = _springVertical ? new Vector2(1f, g) : new Vector2(g, 1f);
                    if (_springFill.anchorMax != max) _springFill.anchorMax = max;
                }
                if (_springFillImage != null)
                {
                    Color c = Color.Lerp(w.GaugeColor, w.StrainColor, w.Strain);
                    if (_springFillImage.color != c) _springFillImage.color = c;
                }
                if (_springGear != null)
                {
                    Quaternion rot = Quaternion.Euler(0f, 0f, -w.Gauge01 * w.Capacity * 18f + Mathf.Sin(Time.realtimeSinceStartup * 60f) * w.Strain * 4f);
                    if (_springGear.localRotation != rot) _springGear.localRotation = rot;
                }
                if (_springTarget != null)
                {
                    Vector2 a = _springVertical ? new Vector2(0f, w.TargetPoint) : new Vector2(w.TargetPoint, 0f);
                    Vector2 b = _springVertical ? new Vector2(1f, w.TargetPoint) : new Vector2(w.TargetPoint, 1f);
                    if (_springTarget.anchorMin != a) _springTarget.anchorMin = a;
                    if (_springTarget.anchorMax != b) _springTarget.anchorMax = b;
                }
                Set(_springLabel, $"태엽  {Mathf.CeilToInt(w.Wound - 1e-3f)} / {w.Capacity}" + (w.IsFull ? "   — 가득 (더 감으면 미끄러진다)" : ""));
            }
            if (_blinkOverlay != null)
            {
                var c = new Color(0f, 0f, 0f, d.BlinkAmount * 0.97f);
                if (_blinkOverlay.color != c) _blinkOverlay.color = c;
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
                case EyeMazeDirector.Mode.Gaze: return "들여다보는 중";
                case EyeMazeDirector.Mode.Ending: return "마음";
                default: return "대화 중";
            }
        }

        /// <summary>
        /// Text에 글을 쓴다. Typewriter가 있으면 한 글자씩 찍는다.
        /// t, s를 사용하며, Text의 글을 바꾼다.
        /// </summary>
        private static void Set(Text t, string s) => Typewriter.SetText(t, s ?? "");
    }
}
