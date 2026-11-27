using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 게임 전역 설정 매니저. 씬에 하나 두며, 다른 스크립트보다 먼저 초기화된다.
    /// 언어 설정을 받아 시작 언어를 정하고, 현재 언어의 폰트를 화면 글에 입힌다.
    /// 현재 언어 코드는 어디서나 GameSettings.Language로 읽고, 바꿀 때는 GameSettings.SetLanguage를 부른다.
    /// 값은 정적 Loc에 보관하므로 이 오브젝트를 찾지 않아도 되고, 씬이 바뀌어도 유지된다. 씬마다 이 오브젝트를 하나씩 둔다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSettings : MonoBehaviour
    {
        [Header("언어")]
        [Tooltip("언어 설정 (Data/Loc/LocConfig.asset). 지원 언어와 시작 언어 규칙, 언어별 폰트")]
        [SerializeField] private LocConfig _locConfig;
        /// <summary>글 상자마다 씬에 지정돼 있던 폰트. 언어 폰트가 비었을 때 되돌린다.</summary>
        private readonly Dictionary<Text, Font> _sceneFonts = new Dictionary<Text, Font>();

        /// <summary>현재 언어 코드 (예: ko, en, ja)</summary>
        public static string Language => Loc.Current;

        void Awake() => Loc.Init(_locConfig);

        void Start()
        {
            Loc.LanguageChanged += ApplyFonts;
            ApplyFonts();
        }

        void OnDestroy() => Loc.LanguageChanged -= ApplyFonts;

        /// <summary>
        /// 플레이어가 고른 언어로 바꾸고 저장한다.
        /// code를 사용하며, 목록에 있는 언어면 현재 언어를 바꾸고 true를 반환한다.
        /// </summary>
        public static bool SetLanguage(string code) => Loc.SetLanguage(code);

        /// <summary>
        /// 씬의 모든 uGUI Text(꺼진 것 포함)에 현재 언어의 폰트를 입힌다. 언어 폰트가 비어 있으면 씬에 지정돼 있던 폰트로 되돌린다.
        /// 글자 크기, 정렬, 위치는 바꾸지 않는다.
        /// _locConfig와 Loc.Current를 사용하며, 각 Text의 font와 _sceneFonts를 바꾼다.
        /// </summary>
        private void ApplyFonts()
        {
            LocConfig.Language language = _locConfig.Find(Loc.Current);
            Font font = language != null ? language.Font : null;
            foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!_sceneFonts.TryGetValue(t, out Font original)) _sceneFonts[t] = original = t.font;
                Font target = font != null ? font : original;
                if (t.font != target) t.font = target;
            }
        }
    }
}
