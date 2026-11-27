using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 현재 언어를 정하고 보관한다.
    /// 시작할 때 저장값, 운영체제 언어, 대체 언어 순서로 언어를 고르고, 플레이어가 바꿀 때만 저장한다.
    /// 설정을 받기 전에는 원문 언어(ko)를 쓴다.
    /// </summary>
    public static class Loc
    {
        /// <summary>원문 언어 코드. 이 언어에서는 번역을 거치지 않는다.</summary>
        public const string SOURCE_LANGUAGE = "ko";
        public const string DefaultConfigPath = "Assets/EyeMaze/Data/Loc/LocConfig.asset";

        private static LocConfig _config;
        private static string _current = SOURCE_LANGUAGE;
        private static bool _initialized;

        /// <summary>현재 언어가 바뀐 뒤 알린다. 고정 라벨이 글을 다시 넣을 때 쓴다.</summary>
        public static event Action LanguageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _config = null;
            _current = SOURCE_LANGUAGE;
            _initialized = false;
            LanguageChanged = null;
        }

        /// <summary>현재 언어 코드</summary>
        public static string Current { get { Ensure(); return _current; } }

        /// <summary>현재 언어가 원문 언어인지</summary>
        public static bool IsSource => Current == SOURCE_LANGUAGE;

        /// <summary>지금 쓰는 언어 설정. 아직 받지 않았으면 null이다.</summary>
        public static LocConfig Config { get { Ensure(); return _config; } }

        /// <summary>
        /// 언어 설정을 받아 시작 언어를 정한다. 같은 설정이면 다시 정하지 않는다.
        /// config를 사용하며, 현재 언어를 바꾼다.
        /// </summary>
        public static void Init(LocConfig config)
        {
            if (config == null || (config == _config && _initialized)) return;
            _config = config;
            _initialized = true;
            string start = StartLanguage(config);
            if (start == _current) return;
            _current = start;
            LanguageChanged?.Invoke();
        }

        /// <summary>
        /// 아직 아무도 설정을 넘겨주지 않았으면, 에디터에서는 기본 경로의 설정을 직접 읽는다.
        /// </summary>
        private static void Ensure()
        {
            if (_initialized) return;
#if UNITY_EDITOR
            Init(UnityEditor.AssetDatabase.LoadAssetAtPath<LocConfig>(DefaultConfigPath));
#endif
        }

        /// <summary>
        /// 시작 언어를 고른다. 저장값, 운영체제 언어, 대체 언어 순서로 목록에 있는 첫 코드를 쓴다.
        /// config를 사용하며, 고른 언어 코드를 반환한다. 고른 값은 저장하지 않는다.
        /// </summary>
        private static string StartLanguage(LocConfig config)
        {
            string saved = PlayerPrefs.GetString(config.LanguagePrefKey, "");
            if (config.Has(saved)) return saved;
            if (config.DetectSystemLanguage)
            {
                string system = FromSystemLanguage(Application.systemLanguage);
                if (config.Has(system)) return system;
            }
            return config.Has(config.FallbackLanguage) ? config.FallbackLanguage : SOURCE_LANGUAGE;
        }

        /// <summary>
        /// 운영체제 언어를 언어 코드로 바꾼다.
        /// language를 사용하며, 언어 코드 또는 대응이 없으면 빈 문자열을 반환한다.
        /// </summary>
        public static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.English: return "en";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                default: return "";
            }
        }

        /// <summary>
        /// 플레이어가 고른 언어로 바꾸고 저장한다.
        /// code를 사용하며, 목록에 있으면 현재 언어와 PlayerPrefs를 바꾸고 true를 반환한다.
        /// </summary>
        public static bool SetLanguage(string code)
        {
            Ensure();
            if (_config == null || !_config.Has(code)) return false;
            PlayerPrefs.SetString(_config.LanguagePrefKey, code);
            PlayerPrefs.Save();
            if (code == _current) return true;
            _current = code;
            LanguageChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 저장된 언어를 지우고 시작 언어 규칙으로 다시 고른다. 자동 감지를 확인할 때 쓴다.
        /// 입력값은 없으며, PlayerPrefs와 현재 언어를 바꾼다.
        /// </summary>
        public static void ClearSavedLanguage()
        {
            Ensure();
            if (_config == null) return;
            PlayerPrefs.DeleteKey(_config.LanguagePrefKey);
            PlayerPrefs.Save();
            string start = StartLanguage(_config);
            if (start == _current) return;
            _current = start;
            LanguageChanged?.Invoke();
        }
    }
}
