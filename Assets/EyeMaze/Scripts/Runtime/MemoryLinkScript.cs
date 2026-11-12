using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 이어지는 기억의 쌍(Data/Links.csv)을 읽어 두 플래그의 쌍에 대한 결과 플래그와 재사용할 방을 제공한다.
    /// 열: a, b, result, room. a와 b의 순서는 상관없다. result는 연결 회상의 Flashbacks.csv flag와 같고, room은 a 또는 b 중 하나다.
    /// </summary>
    public static class MemoryLinkScript
    {
        /// <summary>이어지는 쌍 하나</summary>
        public class Link
        {
            public string A { get; set; }
            public string B { get; set; }
            public string Result { get; set; }
            public string Room { get; set; }
            public int Row { get; set; }

            /// <summary>
            /// 이 쌍이 두 플래그로 이루어졌는지 확인한다.
            /// first와 second를 사용하며, 순서와 상관없이 a, b와 같으면 true를 반환한다.
            /// </summary>
            public bool Matches(string first, string second)
            {
                return (A == first && B == second) || (A == second && B == first);
            }
        }

        private static readonly List<Link> _links = new List<Link>();
        private static TextAsset _asset;

        /// <summary>읽은 쌍 전체. 파일이 연결되지 않았으면 비어 있다</summary>
        public static IReadOnlyList<Link> Links => _links;

        /// <summary>
        /// 이어지는 쌍 CSV를 읽는다. 같은 파일이면 다시 읽지 않는다.
        /// asset과 force를 사용하며, 쌍 목록을 바꾸고 문제가 있으면 Console에 오류를 남긴다.
        /// </summary>
        public static void Load(TextAsset asset, bool force = false)
        {
            if (asset == _asset && !force) return;
            _asset = asset;
            _links.Clear();
            if (asset == null) return;
            var errors = new List<string>();
            _links.AddRange(Parse(asset, errors));
            foreach (string e in errors) Debug.LogError($"[Links.csv] {e}", asset);
        }

        /// <summary>
        /// 마지막으로 읽은 쌍 CSV를 다시 읽는다 (에디터에서 파일이 바뀌었을 때).
        /// 입력값은 없으며, 쌍 목록을 바꾼다.
        /// </summary>
        public static void Reload() => Load(_asset, true);

        /// <summary>
        /// 두 플래그의 쌍을 찾는다. 순서는 상관없다.
        /// first와 second를 사용하며, 쌍 또는 (없으면) null을 반환한다.
        /// </summary>
        public static Link Find(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return null;
            foreach (Link l in _links) if (l.Matches(first, second)) return l;
            return null;
        }

        /// <summary>
        /// 쌍 CSV를 쌍 목록으로 나눈다. 빈 칸이 있는 줄은 빼고 errors에 남긴다.
        /// asset과 errors를 사용하며, 쌍 목록을 반환한다.
        /// </summary>
        public static List<Link> Parse(TextAsset asset, List<string> errors)
        {
            var result = new List<Link>();
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                var link = new Link { A = r["a"], B = r["b"], Result = r["result"], Room = r["room"], Row = r.Line };
                if (link.A.Length == 0 || link.B.Length == 0 || link.Result.Length == 0 || link.Room.Length == 0)
                {
                    errors.Add($"{r.Line}행 a, b, result, room 중 비어 있는 칸이 있다.");
                    continue;
                }
                result.Add(link);
            }
            return result;
        }
    }
}
