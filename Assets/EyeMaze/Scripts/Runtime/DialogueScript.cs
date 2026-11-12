using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 인물·회차 CSV(Data/Dialogue/인물_DayN.csv)에서 section=dialogue 행을 읽은 결과.
    /// 줄 하나가 선택지 하나이고, 같은 id 값을 가진 줄들이 한 장면이 된다. 첫 장면이 시작 장면이다.
    /// 열: id, situation, text, response, need, set, trust, dilation, spring, goto, ending, back, rest, ending_need, emotion (그 밖의 열은 이 단계에서 무시).
    /// section 열이 없는 옛 형식 파일은 node 열을 장면 id로 읽는다.
    /// 자세한 규칙은 Data/README.md에 있다.
    /// </summary>
    public class DialogueScript
    {
        /// <summary>시작 장면이 아닌 곳에서 자동으로 붙는 뒤로가기 말</summary>
        public const string DefaultBackLabel = "다른 질문을 해볼까요?";

        /// <summary>back 칸에 이 값을 쓰면 그 장면에선 뒤로가기를 붙이지 않는다</summary>
        public const string NoBack = "-";

        /// <summary>선택지 하나</summary>
        public class Choice
        {
            public string Text;
            public string Response;
            public string[] Need = Array.Empty<string>();
            public string[] Set = Array.Empty<string>();
            public int Trust;
            public float Dilation;
            public int Spring;
            public string Goto;
            public string Ending;
            /// <summary>스펙 014 FR-038: 고른 뒤 ending으로 갈지 가르는 조건. 비면 고르는 즉시 ending으로 간다.</summary>
            public string[] EndingNeed = Array.Empty<string>();
            // 스펙 011: 내담자가 쉬자고 할 때의 대사 추가
            public string Rest;
            // 스펙 015: 고른 뒤 내담자의 감정 추가 (비었거나 정해지지 않은 값이면 null)
            public ClientEmotion? Emotion;
            public int Line;
        }

        /// <summary>장면 하나</summary>
        public class Node
        {
            public string Id;
            public string Situation;
            public string BackLabel;
            public int Line;
            public readonly List<Choice> Choices = new List<Choice>();
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, Node> _byId = new Dictionary<string, Node>();

        public string Name { get; private set; }
        public IReadOnlyList<Node> Nodes => _nodes;
        public Node Root => _nodes.Count > 0 ? _nodes[0] : null;
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// id로 장면을 찾는다.
        /// id를 사용하며, 찾은 장면 또는 null을 반환한다.
        /// </summary>
        public Node Find(string id) => !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out Node n) ? n : null;

        /// <summary>
        /// 이 대화에서 선택지가 켜는 플래그를 모두 모은다.
        /// 입력값은 없으며, 플래그 id 집합을 반환한다.
        /// </summary>
        public HashSet<string> FlagsSet()
        {
            var set = new HashSet<string>();
            foreach (Node n in _nodes) foreach (Choice c in n.Choices) foreach (string f in c.Set) set.Add(f);
            return set;
        }

        // 읽은 결과를 TextAsset마다 기억한다. 에디터에서 CSV가 다시 임포트되면 Invalidate로 지운다.
        private static readonly Dictionary<TextAsset, DialogueScript> Cache = new Dictionary<TextAsset, DialogueScript>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>
        /// 기억해 둔 대화를 모두 지운다. 다음에 Get을 부르면 CSV를 다시 읽는다.
        /// </summary>
        public static void Invalidate() => Cache.Clear();

        /// <summary>
        /// CSV를 읽은 대화를 가져온다. 처음 한 번만 읽고 그다음부터는 기억해 둔 것을 쓴다.
        /// asset을 사용하며, 대화 또는 null을 반환한다. 오류는 Console에 한 번 보여준다.
        /// </summary>
        public static DialogueScript Get(TextAsset asset)
        {
            if (asset == null) return null;
            if (Cache.TryGetValue(asset, out DialogueScript s)) return s;
            s = Parse(asset);
            Cache[asset] = s;
            foreach (string e in s.Errors) Debug.LogError($"[대화 CSV] {asset.name}: {e}", asset);
            return s;
        }

        /// <summary>
        /// CSV를 읽어 장면과 선택지를 만들고 틀린 곳을 찾는다.
        /// asset을 사용하며, 새 대화를 반환한다.
        /// </summary>
        public static DialogueScript Parse(TextAsset asset)
        {
            var s = new DialogueScript { Name = asset != null ? asset.name : "" };
            List<Csv.Row> all = Csv.Read(asset, s.Errors);
            string idColumn = Csv.IdColumn(all, "node");
            List<Csv.Row> rows = Csv.Section(all, Csv.SECTION_DIALOGUE);
            if (all.Count > 0 && !all[0].Has(idColumn)) s.Errors.Add($"머리글에 {idColumn} 열이 없다.");

            foreach (Csv.Row r in rows)
            {
                string id = r[idColumn];
                if (id.Length == 0) { s.Errors.Add($"{r.Line}행 {idColumn} 칸이 비어 있다."); continue; }
                if (!s._byId.TryGetValue(id, out Node node))
                {
                    node = new Node { Id = id, Line = r.Line };
                    s._byId[id] = node;
                    s._nodes.Add(node);
                }
                if (r["situation"].Length > 0)
                {
                    if (!string.IsNullOrEmpty(node.Situation)) s.Errors.Add($"{r.Line}행 '{id}' 장면의 situation이 이미 {node.Line}행에 있다.");
                    else node.Situation = Text(r["situation"]);
                }
                if (r["back"].Length > 0) node.BackLabel = Text(r["back"]);
                if (r["text"].Length == 0) continue;

                node.Choices.Add(new Choice
                {
                    Text = Text(r["text"]),
                    Response = Text(r["response"]),
                    Need = r.List("need"),
                    Set = r.List("set"),
                    Trust = r.Int("trust", s.Errors),
                    Dilation = r.Float("dilation", s.Errors),
                    Spring = r.Int("spring", s.Errors),
                    Goto = r["goto"],
                    Ending = r["ending"],
                    EndingNeed = r.List("ending_need"),
                    Rest = Text(r["rest"]),
                    // 스펙 015: emotion 칸을 감정으로 읽는다
                    Emotion = ReadEmotion(r, s.Errors),
                    Line = r.Line,
                });
            }

            foreach (Node n in s._nodes)
            {
                if (n.Choices.Count == 0) s.Errors.Add($"{n.Line}행 '{n.Id}' 장면에 선택지가 없다.");
                if (n.Choices.Count > 3) s.Errors.Add($"{n.Line}행 '{n.Id}' 장면의 선택지가 {n.Choices.Count}개다. 뒤로가기를 빼고 3개까지 넣을 수 있다.");
                foreach (Choice c in n.Choices)
                {
                    if (c.Goto.Length > 0 && s.Find(c.Goto) == null) s.Errors.Add($"{c.Line}행 goto '{c.Goto}' 장면이 없다.");
                    // 결과 분기 조건이 있으면 조건이 맞지 않을 때 goto로 가므로 함께 쓸 수 있다.
                    if (c.Goto.Length > 0 && c.Ending.Length > 0 && c.EndingNeed.Length == 0) s.Errors.Add($"{c.Line}행 goto와 ending을 함께 쓸 수 없다 (ending이 먼저 실행된다). 함께 쓰려면 ending_need를 채운다.");
                    if (c.EndingNeed.Length > 0 && c.Ending.Length == 0) s.Errors.Add($"{c.Line}행 ending_need는 ending이 있는 선택지에만 쓴다.");
                }
            }
            return s;
        }

        /// <summary>
        /// 칸 글을 화면용으로 다듬는다. 칸 안에 직접 적은 \n은 줄바꿈으로 바꾼다.
        /// s를 사용하며, 바뀐 글을 반환한다.
        /// </summary>
        private static string Text(string s) => s.Replace("\\n", "\n");

        /// <summary>
        /// 스펙 015: 선택지 행의 emotion 칸을 감정으로 읽는다. 정해지지 않은 값은 오류로 남기고 감정을 바꾸지 않는다.
        /// r과 errors를 사용하며, 감정 또는 (비었거나 틀린 값이면) null을 반환한다.
        /// </summary>
        private static ClientEmotion? ReadEmotion(Csv.Row r, List<string> errors)
        {
            string v = r["emotion"];
            if (v.Length == 0) return null;
            if (ClientEmotionCsv.TryParse(v, out ClientEmotion e)) return e;
            errors.Add($"{r.Line}행 emotion '{v}'는 정해진 값({ClientEmotionCsv.VALUES})이 아니다.");
            return null;
        }
    }
}
