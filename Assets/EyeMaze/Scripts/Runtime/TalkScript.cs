using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 필드에서 말을 걸었을 때 나오는 대사 CSV(Data/Talk/이름.csv)를 읽은 결과.
    /// 줄 하나가 대사 한 줄이고, 같은 group 값을 가진 줄들이 한 번의 대화가 된다 (Z로 한 줄씩 넘김).
    /// 열: group, need, set, speaker, text (그 밖의 열은 메모로 무시).
    ///
    /// 말을 걸면 위에서부터 need 플래그가 모두 켜진 첫 group을 보여준다. 그래서 조건이 까다로운 group을 위에 둔다.
    /// 예약된 group: enter(컷신으로 들어와서 하는 말), reveal(이 사람과의 대화 뒤 다음 사람이 들어올 때의 서술), rest(쉬어 주고 돌아온 직후의 리액션).
    /// </summary>
    public class TalkScript
    {
        public const string Enter = "enter";
        public const string Reveal = "reveal";
        // 스펙 009: 쉬어 주고 돌아온 직후의 리액션 묶음 추가
        public const string Rest = "rest";

        /// <summary>speaker 칸에 이 값을 쓰면 이름 없는 서술이 된다</summary>
        public const string Narration = "-";

        /// <summary>대사 한 줄</summary>
        public class Line
        {
            /// <summary>비면 말 건 사람, "-"면 서술, 그 밖에는 그 이름의 사람</summary>
            public string Speaker;
            public string Text;
            public int Row;
        }

        /// <summary>한 번의 대화</summary>
        public class Group
        {
            public string Id;
            public string[] Need = Array.Empty<string>();
            public string[] Set = Array.Empty<string>();
            public int Row;
            public readonly List<Line> Lines = new List<Line>();

            // 스펙 009: rest 묶음도 평소 말을 걸 때 고르지 않도록 예약
            public bool IsReserved => Id == Enter || Id == Reveal || Id == Rest;
        }

        private readonly List<Group> _groups = new List<Group>();

        public string Name { get; private set; }
        public IReadOnlyList<Group> Groups => _groups;
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// id로 대화를 찾는다.
        /// id를 사용하며, 찾은 대화 또는 null을 반환한다.
        /// </summary>
        public Group Find(string id)
        {
            foreach (Group g in _groups) if (g.Id == id) return g;
            return null;
        }

        /// <summary>
        /// 말을 걸었을 때 보여줄 대화를 고른다. 위에서부터 need가 모두 켜진 첫 대화 (enter/reveal 제외).
        /// 플래그를 사용하며, 고른 대화 또는 null을 반환한다.
        /// </summary>
        public Group Pick()
        {
            foreach (Group g in _groups)
                if (!g.IsReserved && Flags.AllSet(g.Need)) return g;
            return null;
        }

        private static readonly Dictionary<TextAsset, TalkScript> Cache = new Dictionary<TextAsset, TalkScript>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>기억해 둔 대사를 모두 지운다. 다음에 Get을 부르면 CSV를 다시 읽는다.</summary>
        public static void Invalidate() => Cache.Clear();

        /// <summary>
        /// CSV를 읽은 대사를 가져온다. 처음 한 번만 읽는다.
        /// asset을 사용하며, 대사 또는 null을 반환한다. 오류는 Console에 한 번 보여준다.
        /// </summary>
        public static TalkScript Get(TextAsset asset)
        {
            if (asset == null) return null;
            if (Cache.TryGetValue(asset, out TalkScript s)) return s;
            s = Parse(asset);
            Cache[asset] = s;
            foreach (string e in s.Errors) Debug.LogError($"[필드 대화 CSV] {asset.name}: {e}", asset);
            return s;
        }

        /// <summary>
        /// CSV를 읽어 대화 묶음을 만든다.
        /// asset을 사용하며, 새 대사를 반환한다.
        /// </summary>
        public static TalkScript Parse(TextAsset asset)
        {
            var s = new TalkScript { Name = asset != null ? asset.name : "" };
            foreach (Csv.Row r in Csv.Read(asset, s.Errors))
            {
                string id = r["group"];
                if (id.Length == 0) { s.Errors.Add($"{r.Line}행 group 칸이 비어 있다."); continue; }
                Group g = s.Find(id);
                if (g == null)
                {
                    g = new Group { Id = id, Row = r.Line };
                    s._groups.Add(g);
                }
                if (r["need"].Length > 0) g.Need = Merge(g.Need, r.List("need"));
                if (r["set"].Length > 0) g.Set = Merge(g.Set, r.List("set"));
                string text = r["text"];
                if (text.Length == 0) continue;
                g.Lines.Add(new Line { Speaker = r["speaker"], Text = text.Replace("\\n", "\n"), Row = r.Line });
            }
            foreach (Group g in s._groups)
                if (g.Lines.Count == 0) s.Errors.Add($"{g.Row}행 '{g.Id}' 대화에 대사(text)가 없다.");
            return s;
        }

        private static string[] Merge(string[] a, string[] b)
        {
            var list = new List<string>(a);
            foreach (string x in b) if (!list.Contains(x)) list.Add(x);
            return list.ToArray();
        }
    }
}
