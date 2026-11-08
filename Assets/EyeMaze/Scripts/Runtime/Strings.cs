using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 공용 문구 표 (Data/Strings.csv). 열: key, text, memo.
    /// 태엽/조작 안내, 시스템 메시지, 오프닝 독백, 잠긴 문, 층 이름처럼 특정 사람에 속하지 않는 글을 둔다.
    /// 같은 key를 여러 줄에 쓰면 여러 줄짜리 글(예: 오프닝 독백)이 된다.
    /// 글 안의 {name} 같은 칸은 게임이 채운다 (key마다 쓸 수 있는 칸은 memo에 적어 둔다).
    /// 표에 없는 key는 코드에 적힌 기본 글을 쓴다.
    /// </summary>
    public static class Strings
    {
        public const string DefaultPath = "Assets/EyeMaze/Data/Strings.csv";

        private static readonly Dictionary<string, List<string>> Table = new Dictionary<string, List<string>>();
        private static TextAsset _asset;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            _asset = null;
            Table.Clear();
        }

        /// <summary>
        /// 문구 표를 읽는다. 같은 파일이면 다시 읽지 않는다.
        /// asset과 force를 사용하며, 문구 표를 바꾼다.
        /// </summary>
        public static void Load(TextAsset asset, bool force = false)
        {
            if (asset == null || (asset == _asset && _loaded && !force)) return;
            _asset = asset;
            _loaded = true;
            Table.Clear();
            var errors = new List<string>();
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string key = r["key"];
                if (key.Length == 0) continue;
                if (!Table.TryGetValue(key, out List<string> list)) Table[key] = list = new List<string>();
                list.Add(r["text"].Replace("\\n", "\n"));
            }
            foreach (string e in errors) Debug.LogError($"[Strings.csv] {e}", asset);
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
        /// key의 글을 가져온다. 여러 줄이면 줄바꿈으로 잇는다. {이름} 칸은 vars로 채운다.
        /// key, fallback(표에 없을 때 쓸 글), vars를 사용하며, 완성된 글을 반환한다.
        /// </summary>
        public static string Get(string key, string fallback, params (string name, object value)[] vars)
        {
            Ensure();
            string s = Table.TryGetValue(key, out List<string> list) && list.Count > 0 ? string.Join("\n", list) : fallback;
            if (s == null) return "";
            foreach ((string name, object value) in vars) s = s.Replace("{" + name + "}", value?.ToString() ?? "");
            return s;
        }

        /// <summary>
        /// key의 글을 줄 목록으로 가져온다 (오프닝 독백처럼 한 줄씩 넘기는 글).
        /// key를 사용하며, 줄 목록 또는 (표에 없으면) null을 반환한다.
        /// </summary>
        public static List<string> Lines(string key)
        {
            Ensure();
            return Table.TryGetValue(key, out List<string> list) && list.Count > 0 ? new List<string>(list) : null;
        }

        /// <summary>표에 있는 key들 (에디터 검사용)</summary>
        public static IEnumerable<string> Keys { get { Ensure(); return Table.Keys; } }
    }
}
