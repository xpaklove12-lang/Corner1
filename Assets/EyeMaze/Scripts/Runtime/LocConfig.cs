using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 언어 설정 에셋 (Data/Loc/LocConfig.asset).
    /// 지원 언어 목록과, 저장값이 없을 때 시작 언어를 고르는 규칙을 둔다. 첫 항목이 원문 언어(ko)다.
    /// </summary>
    [CreateAssetMenu(menuName = "EyeMaze/언어 설정", fileName = "LocConfig")]
    public class LocConfig : ScriptableObject
    {
        /// <summary>언어 목록의 한 항목</summary>
        [Serializable]
        public class Language
        {
            [Tooltip("언어 코드 (ISO 639-1). 번역 파일의 열 이름과 같다. 예: ko, en, ja")]
            [SerializeField] private string _code = "";
            [Tooltip("그 언어로 쓴 언어 이름. 예: 한국어, English, 日本語")]
            [SerializeField] private string _name = "";
            [Tooltip("이 언어의 화면 글 폰트. 비우면 씬에 지정된 폰트를 쓴다")]
            [SerializeField] private Font _font;

            public string Code => _code;
            public string Name => _name;
            public Font Font => _font;
        }

        [Header("언어")]
        [Tooltip("지원 언어. 첫 항목은 원문 언어(ko)다")]
        [SerializeField] private List<Language> _languages = new List<Language>();

        [Header("시작 언어")]
        [Tooltip("저장값이 없을 때 운영체제 언어를 볼지. 끄면 저장값이 없을 때 바로 대체 언어로 시작한다")]
        [SerializeField] private bool _detectSystemLanguage = true;
        [Tooltip("저장값도 없고 운영체제 언어도 목록에 없을 때 쓰는 언어 코드")]
        [SerializeField] private string _fallbackLanguage = "en";
        [Tooltip("플레이어가 고른 언어 코드를 저장하는 PlayerPrefs 이름")]
        [SerializeField] private string _languagePrefKey = "EyeMaze.Language";

        public IReadOnlyList<Language> Languages => _languages;
        public bool DetectSystemLanguage => _detectSystemLanguage;
        public string FallbackLanguage => _fallbackLanguage;
        public string LanguagePrefKey => _languagePrefKey;

        /// <summary>
        /// 언어 코드로 목록의 항목을 찾는다.
        /// code를 사용하며, 찾은 항목 또는 null을 반환한다.
        /// </summary>
        public Language Find(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (Language l in _languages) if (l != null && l.Code == code) return l;
            return null;
        }

        /// <summary>
        /// 언어 코드가 목록에 있는지 본다.
        /// code를 사용하며, 있으면 true를 반환한다.
        /// </summary>
        public bool Has(string code) => Find(code) != null;

        void OnValidate()
        {
            var seen = new HashSet<string>();
            foreach (Language l in _languages)
            {
                if (l == null) continue;
                if (string.IsNullOrEmpty(l.Code)) Debug.LogWarning("[LocConfig] 언어 코드가 빈 항목이 있다.", this);
                else if (!seen.Add(l.Code)) Debug.LogWarning($"[LocConfig] 언어 코드 '{l.Code}'가 두 번 있다.", this);
            }
            if (!Has(_fallbackLanguage)) Debug.LogWarning($"[LocConfig] 대체 언어 '{_fallbackLanguage}'가 언어 목록에 없다. 원문 언어로 시작한다.", this);
        }
    }
}
