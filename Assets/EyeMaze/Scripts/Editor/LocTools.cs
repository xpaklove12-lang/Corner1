using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 개발 확인용 언어 바꾸기 창 (메뉴 EyeMaze/번역/언어 바꾸기). 스펙 LOC-04.
    /// 현재 언어, 저장된 언어, 운영체제 언어를 보여 주고, 언어를 고르거나 저장된 언어를 지운다.
    /// Play 중이면 곧바로 바뀌고, 아니면 저장만 되어 다음 Play부터 그 언어로 시작한다.
    /// </summary>
    public class LocTools : EditorWindow
    {
        /// <summary>
        /// 언어 바꾸기 창을 연다.
        /// 입력값은 없으며, 창을 띄운다.
        /// </summary>
        [MenuItem("EyeMaze/번역/언어 바꾸기")]
        private static void Open()
        {
            LocTools window = GetWindow<LocTools>(true, "언어 바꾸기");
            window.minSize = new Vector2(260f, 200f);
        }

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            LocConfig config = Loc.Config;
            if (config == null)
            {
                EditorGUILayout.HelpBox($"언어 설정이 없다. {Loc.DefaultConfigPath}를 만들거나 씬의 GameSettings에서 Loc Config 칸을 채운다.", MessageType.Warning);
                return;
            }

            string saved = PlayerPrefs.GetString(config.LanguagePrefKey, "");
            string system = Loc.FromSystemLanguage(Application.systemLanguage);
            EditorGUILayout.LabelField("현재 언어", Loc.Current);
            EditorGUILayout.LabelField("저장된 언어", saved.Length > 0 ? saved : "없음");
            EditorGUILayout.LabelField("운영체제 언어", $"{Application.systemLanguage} → {(system.Length > 0 ? system : "대응 없음")}");
            EditorGUILayout.Space();

            foreach (LocConfig.Language language in config.Languages)
            {
                if (language == null || string.IsNullOrEmpty(language.Code)) continue;
                using (new EditorGUI.DisabledScope(language.Code == Loc.Current && language.Code == saved))
                {
                    if (GUILayout.Button($"{language.Name} ({language.Code})")) Loc.SetLanguage(language.Code);
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(saved.Length == 0))
            {
                if (GUILayout.Button("저장된 언어 지우기")) Loc.ClearSavedLanguage();
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Play 중이 아니면 저장만 한다. 다음 Play부터 이 언어로 시작한다.", MessageType.Info);
        }
    }
}
