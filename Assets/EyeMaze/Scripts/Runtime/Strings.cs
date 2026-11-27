using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 공용 문구 표 (Data/Loc/UI.csv). 열: key, context, maxlen, ko, en, ja, note, 언어_ok, ko_synced.
    /// 태엽/조작 안내, 시스템 메시지, 오프닝 독백, 잠긴 문, 층 이름처럼 특정 사람에 속하지 않는 글을 둔다.
    /// 같은 key를 여러 줄에 쓰면 여러 줄짜리 글(예: 오프닝 독백)이 된다.
    /// 글 안의 {name} 같은 칸은 게임이 채운다 (key마다 쓸 수 있는 칸은 note에 적어 둔다).
    /// 현재 언어 칸이 비어 있으면 ko 칸을, 표에 없는 key는 코드에 적힌 기본 글을 쓴다.
    /// </summary>
    public static class Strings
    {
        public const string DefaultPath = "Assets/EyeMaze/Data/Loc/UI.csv";
        /// <summary>씬 인스펙터 문구 표의 기본 경로. LocConfig의 씬 문구 파일로 연결해 읽는다.</summary>
        public const string SCENE_PATH = "Assets/EyeMaze/Data/Loc/Scene.csv";
        /// <summary>씬 문구 key 앞부분. 인스펙터 칸 값이 이것으로 시작하면 key로 본다.</summary>
        public const string SCENE_KEY_PREFIX = "scene.";
        /// <summary>원문 칸. 옛 형식(key, text, memo) 파일이면 text 칸을 원문으로 읽는다.</summary>
        public const string SOURCE_COLUMN = Loc.SOURCE_LANGUAGE;
        private const string OLD_SOURCE_COLUMN = "text";
        /// <summary>화자·대상 이름 번역 key 앞부분. name.원래이름</summary>
        public const string NAME_PREFIX = "name.";
        /// <summary>손님 3인칭 번역 key 앞부분. pron.원래이름</summary>
        public const string PRONOUN_PREFIX = "pron.";
        private const string NARRATION_SPEAKER = "-";
        private const string UNKNOWN_SPEAKER = "???";
        private const char HANGUL_FIRST = '가';
        private const char HANGUL_LAST = '힣';
        private const int HANGUL_FINAL_COUNT = 28;
        private const int HANGUL_FINAL_RIEUL = 8;

        /// <summary>숫자 0~9를 한국어로 읽었을 때의 받침 번호 (영, 일, 이, 삼, 사, 오, 육, 칠, 팔, 구)</summary>
        private static readonly int[] DIGIT_FINALS = { 21, 8, 0, 16, 0, 0, 1, 8, 8, 0 };
        /// <summary>없다고 이미 경고한 key</summary>
        private static readonly HashSet<string> MissingWarned = new HashSet<string>();

        private static readonly Dictionary<string, List<Csv.Row>> Table = new Dictionary<string, List<Csv.Row>>();
        /// <summary>현재 언어로 고른 줄 목록. 언어나 파일이 바뀔 때만 다시 만든다.</summary>
        private static readonly Dictionary<string, List<string>> ResolvedLines = new Dictionary<string, List<string>>();
        /// <summary>현재 언어로 고른 줄을 줄바꿈으로 이은 글</summary>
        private static readonly Dictionary<string, string> ResolvedText = new Dictionary<string, string>();
        private static TextAsset _asset;
        private static bool _loaded;
        /// <summary>ResolvedLines·ResolvedText를 만든 언어. null이면 아직 만들지 않았다.</summary>
        private static string _resolvedLanguage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            _asset = null;
            Table.Clear();
            MissingWarned.Clear();
            ClearResolved();
        }

        /// <summary>
        /// 현재 언어로 골라 둔 글을 비운다. 다음 조회 때 다시 만든다.
        /// 입력값은 없으며, ResolvedLines, ResolvedText, _resolvedLanguage를 바꾼다.
        /// </summary>
        private static void ClearResolved()
        {
            ResolvedLines.Clear();
            ResolvedText.Clear();
            _resolvedLanguage = null;
        }

        /// <summary>
        /// 골라 둔 글이 현재 언어의 것이 아니면 모든 key의 글을 현재 언어로 다시 고른다.
        /// 표가 바뀌거나 언어가 바뀐 뒤 처음 조회할 때 한 번만 일어난다.
        /// Table과 Loc.Current를 사용하며, ResolvedLines, ResolvedText, _resolvedLanguage를 바꾼다.
        /// </summary>
        private static void EnsureResolved()
        {
            string lang = Loc.Current;
            if (_resolvedLanguage == lang) return;
            ResolvedLines.Clear();
            ResolvedText.Clear();
            foreach (KeyValuePair<string, List<Csv.Row>> pair in Table)
            {
                var lines = new List<string>(pair.Value.Count);
                foreach (Csv.Row r in pair.Value) lines.Add(Pick(r, lang));
                ResolvedLines[pair.Key] = lines;
                ResolvedText[pair.Key] = string.Join("\n", lines);
            }
            _resolvedLanguage = lang;
        }

        /// <summary>
        /// 문구 표를 읽는다. 공용 문구 표에 이어 LocConfig에 연결한 씬 문구 표도 함께 읽는다. 같은 파일이면 다시 읽지 않는다.
        /// asset, force와 Loc.Config의 씬 문구 파일을 사용하며, 문구 표를 바꾼다.
        /// </summary>
        public static void Load(TextAsset asset, bool force = false)
        {
            if (asset == null || (asset == _asset && _loaded && !force)) return;
            _asset = asset;
            _loaded = true;
            Table.Clear();
            ClearResolved();
            AddRows(asset);
            LocConfig config = Loc.Config;
            if (config != null && config.SceneTexts != null) AddRows(config.SceneTexts);
        }

        /// <summary>
        /// 한 파일의 줄을 key별로 문구 표에 더한다.
        /// asset을 사용하며, Table을 바꾸고 읽기 오류를 로그로 남긴다.
        /// </summary>
        private static void AddRows(TextAsset asset)
        {
            var errors = new List<string>();
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string key = r["key"];
                if (key.Length == 0) continue;
                if (!Table.TryGetValue(key, out List<Csv.Row> list)) Table[key] = list = new List<Csv.Row>();
                list.Add(r);
            }
            foreach (string e in errors) Debug.LogError($"[{asset.name}.csv] {e}", asset);
        }

        /// <summary>
        /// 한 줄에서 lang 언어의 글을 고른다. 그 언어 칸이 비어 있으면 원문 칸을 쓴다.
        /// r과 lang을 사용하며, 줄바꿈 표기 \n을 실제 줄바꿈으로 바꾼 글을 반환한다.
        /// </summary>
        private static string Pick(Csv.Row r, string lang)
        {
            string s = lang != Loc.SOURCE_LANGUAGE && r.Has(lang) ? r[lang] : "";
            if (s.Length == 0) s = r.Has(SOURCE_COLUMN) ? r[SOURCE_COLUMN] : r[OLD_SOURCE_COLUMN];
            return s.Replace("\\n", "\n");
        }

        /// <summary>마지막으로 읽은 파일을 다시 읽는다 (에디터에서 파일이 바뀌었을 때).</summary>
        public static void Reload() => Load(_asset, true);

        /// <summary>
        /// 아직 아무도 표를 넘겨주지 않았으면, 에디터에서는 기본 경로의 파일을 직접 읽는다.
        /// </summary>
        private static void Ensure()
        {
            if (_loaded) return;
#if UNITY_EDITOR
            Load(UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultPath));
#endif
        }

        /// <summary>
        /// key의 현재 언어 글을 가져온다. 여러 줄이면 줄바꿈으로 잇는다. {이름} 칸은 vars로 채운다.
        /// key, fallback(표에 없을 때 쓸 글), vars를 사용하며, 완성된 글을 반환한다.
        /// </summary>
        public static string Get(string key, string fallback, params (string name, object value)[] vars)
        {
            Ensure();
            EnsureResolved();
            string s = ResolvedText.TryGetValue(key, out string text) ? text : fallback;
            if (s == null) return "";
            foreach ((string name, object value) in vars) s = Fill(s, name, value?.ToString() ?? "");
            return s;
        }

        /// <summary>
        /// key의 현재 언어 글을 가져온다. 표에 반드시 있어야 하는 key에 쓴다.
        /// 표에 없으면 key 이름을 그대로 보이고, 같은 key는 한 번만 경고한다.
        /// key와 vars를 사용하며, 완성된 글을 반환한다.
        /// </summary>
        public static string Get(string key, params (string name, object value)[] vars)
        {
            Ensure();
            EnsureResolved();
            if (!ResolvedText.ContainsKey(key) && _loaded && MissingWarned.Add(key))
                Debug.LogWarning($"[UI.csv] key '{key}'가 없다. 화면에는 key 이름이 보인다.", _asset);
            return Get(key, key, vars);
        }

        /// <summary>
        /// 씬 인스펙터 문구 칸의 값을 화면에 보일 글로 바꾼다. 값이 씬 문구 key면 그 key의 현재 언어 글을,
        /// 아직 옮기지 않은 글이면 적힌 글을 그대로 쓴다. key가 표에 없으면 key 이름이 보인다.
        /// value를 사용하며, 화면에 보일 글을 반환한다.
        /// </summary>
        public static string Scene(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith(SCENE_KEY_PREFIX, System.StringComparison.Ordinal)) return value ?? "";
            return Get(value);
        }

        /// <summary>
        /// 화자·대상의 원래 이름을 현재 언어 이름으로 바꾼다. name.원래이름 key가 없으면 원래 이름을 쓴다.
        /// 빈 이름, 서술(-), ???는 바꾸지 않는다. 초상화 찾기 같은 판정에는 원래 이름을 그대로 쓴다.
        /// original을 사용하며, 화면에 보일 이름을 반환한다.
        /// </summary>
        public static string Name(string original)
        {
            if (string.IsNullOrEmpty(original) || original == NARRATION_SPEAKER || original == UNKNOWN_SPEAKER) return original;
            return Get(NAME_PREFIX + original, original);
        }

        /// <summary>
        /// 글 속 {name} 칸을 value로 채운다. {name|이/가}처럼 조사를 함께 적으면 value의 끝 글자 받침에 맞는 쪽을 붙인다.
        /// 앞쪽은 받침이 있을 때, 뒤쪽은 없을 때 쓴다. 뒤쪽이 "로"이면 ㄹ 받침에도 뒤쪽을 쓴다.
        /// s, name, value를 사용하며, 채운 글을 반환한다.
        /// </summary>
        public static string Fill(string s, string name, string value)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            s = s.Replace("{" + name + "}", value);
            string open = "{" + name + "|";
            int at = s.IndexOf(open, System.StringComparison.Ordinal);
            while (at >= 0)
            {
                int close = s.IndexOf('}', at + open.Length);
                if (close < 0) break;
                string[] pair = s.Substring(at + open.Length, close - at - open.Length).Split('/');
                string particle = pair.Length == 2 ? Particle(value, pair[0], pair[1]) : "";
                s = s.Substring(0, at) + value + particle + s.Substring(close + 1);
                at = s.IndexOf(open, at + value.Length + particle.Length, System.StringComparison.Ordinal);
            }
            return s;
        }

        /// <summary>
        /// 낱말 끝 글자의 받침에 맞는 조사를 고른다. 한글이 아니면 숫자 읽기로 판단하고, 그 밖의 글자는 받침이 없는 것으로 본다.
        /// word, withFinal(받침 있을 때), withoutFinal(받침 없을 때)을 사용하며, 고른 조사를 반환한다.
        /// </summary>
        private static string Particle(string word, string withFinal, string withoutFinal)
        {
            if (string.IsNullOrEmpty(word)) return withoutFinal;
            char last = word[word.Length - 1];
            int final;
            if (last >= HANGUL_FIRST && last <= HANGUL_LAST) final = (last - HANGUL_FIRST) % HANGUL_FINAL_COUNT;
            else if (last >= '0' && last <= '9') final = DIGIT_FINALS[last - '0'];
            else final = 0;
            if (final == 0) return withoutFinal;
            if (final == HANGUL_FINAL_RIEUL && withoutFinal == "로") return withoutFinal;
            return withFinal;
        }

        /// <summary>
        /// key의 현재 언어 글을 줄 목록으로 가져온다 (오프닝 독백처럼 한 줄씩 넘기는 글). 줄마다 번역이 비어 있으면 원문을 쓴다.
        /// key를 사용하며, 줄 목록의 복사본 또는 (표에 없으면) null을 반환한다.
        /// </summary>
        public static List<string> Lines(string key)
        {
            Ensure();
            EnsureResolved();
            return ResolvedLines.TryGetValue(key, out List<string> lines) && lines.Count > 0 ? new List<string>(lines) : null;
        }

        /// <summary>
        /// key의 원문(ko) 글을 가져온다. 언어와 상관없이 원문을 보여야 하는 에디터 표시에 쓴다.
        /// key를 사용하며, 여러 줄이면 줄바꿈으로 이은 원문 또는 (표에 없으면) null을 반환한다.
        /// </summary>
        public static string Source(string key)
        {
            Ensure();
            if (!Table.TryGetValue(key, out List<Csv.Row> rows)) return null;
            var lines = new List<string>(rows.Count);
            foreach (Csv.Row r in rows) lines.Add(Pick(r, Loc.SOURCE_LANGUAGE));
            return string.Join("\n", lines);
        }

        /// <summary>표에 있는 key들 (에디터 검사용)</summary>
        public static IEnumerable<string> Keys { get { Ensure(); return Table.Keys; } }
    }
}
