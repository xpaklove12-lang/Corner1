using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 인물·회차 CSV(Data/Dialogue/인물_DayN.csv)에서 section=ending 행, 곧 한 사람의 엔딩과 문구를 읽은 결과.
    /// 열: id, title, situation, after, text, memo. section 열이 없는 옛 형식 파일은 line 열을 대사로 읽는다.
    /// - 엔딩: 줄 하나가 엔딩 대사 한 줄. 같은 id끼리 한 엔딩이고, title/situation/after는 그 엔딩의 첫 줄에 쓴다.
    /// - 사람별 문구: id를 @intro(눈 속에 들어왔을 때), @idle(기다릴 때 상황), @abyss(심연에 들어왔을 때)로 쓰고 line에 글을 적는다.
    ///   같은 id를 여러 줄 쓰면 줄바꿈으로 이어진다. 아래 설명의 line은 통합 회차 CSV의 text 열이다.
    /// </summary>
    public class EndingScript
    {
        public const string Intro = "@intro";
        public const string Idle = "@idle";
        public const string Abyss = "@abyss";

        private readonly List<EndingDef> _endings = new List<EndingDef>();
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        public string Name { get; private set; }
        public List<EndingDef> Endings => _endings;
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// 사람별 문구(@intro 등)를 가져온다.
        /// id를 사용하며, 글 또는 (없으면) null을 반환한다.
        /// </summary>
        public string Text(string id) => _texts.TryGetValue(id, out string s) ? s : null;

        private static readonly Dictionary<TextAsset, EndingScript> Cache = new Dictionary<TextAsset, EndingScript>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>기억해 둔 엔딩을 모두 지운다. 다음에 Get을 부르면 CSV를 다시 읽는다.</summary>
        public static void Invalidate() => Cache.Clear();

        /// <summary>
        /// CSV를 읽은 엔딩을 가져온다. 처음 한 번만 읽는다.
        /// asset을 사용하며, 엔딩 또는 null을 반환한다. 오류는 Console에 한 번 보여준다.
        /// </summary>
        public static EndingScript Get(TextAsset asset)
        {
            if (asset == null) return null;
            if (Cache.TryGetValue(asset, out EndingScript s)) return s;
            s = Parse(asset);
            Cache[asset] = s;
            foreach (string e in s.Errors) Debug.LogError($"[엔딩 CSV] {asset.name}: {e}", asset);
            return s;
        }

        /// <summary>
        /// CSV를 읽어 엔딩 목록과 사람별 문구를 만든다.
        /// asset을 사용하며, 새 엔딩 묶음을 반환한다.
        /// </summary>
        public static EndingScript Parse(TextAsset asset)
        {
            var s = new EndingScript { Name = asset != null ? asset.name : "" };
            var byId = new Dictionary<string, EndingDef>();
            var firstRow = new Dictionary<string, int>();
            List<Csv.Row> all = Csv.Read(asset, s.Errors);
            string lineColumn = all.Count > 0 && all[0].Has(Csv.SECTION) ? "text" : "line";
            foreach (Csv.Row r in Csv.Section(all, Csv.SECTION_ENDING))
            {
                string id = r["id"];
                if (id.Length == 0) { s.Errors.Add($"{r.Line}행 id 칸이 비어 있다."); continue; }
                string line = r[lineColumn].Replace("\\n", "\n");

                if (id.StartsWith("@"))
                {
                    if (id != Intro && id != Idle && id != Abyss) s.Errors.Add($"{r.Line}행 '{id}'는 모르는 특수 id다 ({Intro}, {Idle}, {Abyss} 중 하나).");
                    if (line.Length == 0) continue;
                    s._texts[id] = s._texts.TryGetValue(id, out string prev) ? prev + "\n" + line : line;
                    continue;
                }

                if (!byId.TryGetValue(id, out EndingDef e))
                {
                    e = new EndingDef(id, r["title"], r["situation"].Replace("\\n", "\n"), r["after"].Replace("\\n", "\n"));
                    byId[id] = e;
                    firstRow[id] = r.Line;
                    s._endings.Add(e);
                }
                else if (r["title"].Length > 0 || r["situation"].Length > 0 || r["after"].Length > 0)
                    s.Errors.Add($"{r.Line}행 '{id}' 엔딩의 title/situation/after는 첫 줄({firstRow[id]}행)에만 쓴다.");
                if (line.Length > 0) e.Lines.Add(line);
            }
            foreach (EndingDef e in s._endings)
            {
                if (string.IsNullOrEmpty(e.Title)) s.Errors.Add($"{firstRow[e.Id]}행 '{e.Id}' 엔딩에 title이 없다.");
                if (e.Lines.Count == 0) s.Errors.Add($"{firstRow[e.Id]}행 '{e.Id}' 엔딩에 대사(line)가 없다.");
            }
            return s;
        }
    }
}
