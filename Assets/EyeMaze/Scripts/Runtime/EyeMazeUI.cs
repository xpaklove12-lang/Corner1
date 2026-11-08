using System;

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

        [NonSerialized] private float _burdenShown = -1f;
        [NonSerialized] private float _burdenAlpha = -1f;

        // 스펙 006: 깜빡임 전체 화면 페이드 필드 제거

        void Update()
        {
            EyeMazeDirector d = _director;
            if (d == null) return;
            Person p = d.Person;

            if (p != null)
                Set(_status, $"신뢰 {p.Trust}   ·   동공 {p.Dilation * 100f:0}%   ·   {ModeName(d.CurrentMode)}");

            DialogueNode node = d.CurrentNode;
            string sit;
            if (d.CurrentMode == EyeMazeDirector.Mode.Ending)
            {
                EndingDef e = d.CurrentEnding;
                sit = e != null && !string.IsNullOrEmpty(e.Situation) ? e.Situation : $"{(p != null ? p.DisplayName : "그녀")}의 마음이 들린다.";
            }
            else if (d.InAbyss) sit = "그녀의 마음 속 심연";
            else if (d.IsReading) sit = "시선이 파편에 머문다.";
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
            if (showReturn) Set(_returnLabel, $"4. {d.ReturnText}");

            Set(_gazeButtonLabel, d.CurrentMode == EyeMazeDirector.Mode.Gaze ? "시선 거두기 (Space)" : "들여다보기 (Space)");
            Set(_hint, d.IsReading
                ? "Space / Z  계속"
                : d.InAbyss
                ? "WASD / 방향키로 가장 반짝이는 파편에 다가간다"
                : d.CurrentMode == EyeMazeDirector.Mode.Gaze
                ? "WASD / 방향키로 이동   ·   Space 시선 거두기   ·   금색 = 잠긴 문   ·   하늘색 점 = 파편"
                : d.CurrentMode == EyeMazeDirector.Mode.Ending ? (d.HeartFinished ? "Space / Z  돌아가기" : "Space / Z  계속")
                : "Space = 눈 들여다보기   ·   1 2 3 4 대화   ·   Q 자리 떠나기");
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
