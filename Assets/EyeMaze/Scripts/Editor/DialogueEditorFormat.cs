using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 대사 편집기의 CSV와 공통 모델, SUDS식 텍스트와 공통 모델 사이를 읽고 쓴다.
    /// 텍스트 표기: "[eye section="talk" actor="EunjaeMom"]"이 단계를 정하고, ":id"가 그룹을 시작하며,
    /// "[eye 이름="값"]"이 칸 값을, Dialogue의 "* 선택지"와 Tab 한 개 들여쓴 명령, 다른 단계의 "Line: 대사"가 행을 나타낸다.
    /// </summary>
    public static class DialogueEditorFormat
    {
        /// <summary>검사 결과 한 줄</summary>
        public class Issue
        {
            /// <summary>텍스트 뷰의 줄 번호 (1부터, 줄을 특정할 수 없으면 0)</summary>
            public int Line;
            public string Message;
            public bool Error = true;
        }

        /// <summary>단계(section)마다 다른 열 구성</summary>
        public class Schema
        {
            /// <summary>그룹 id 열 (id 또는 Strings의 key)</summary>
            public string IdColumn;
            /// <summary>행 본문 열 (*, Line: 뒤의 글)</summary>
            public string BodyColumn;
            /// <summary>그룹의 첫 행에만 적는 칸</summary>
            public string[] GroupColumns;
            /// <summary>행마다 [eye ...]로 적는 칸 (본문과 goto 제외)</summary>
            public string[] ItemColumns;
            /// <summary>따옴표 없이 수치로 적는 칸</summary>
            public string[] NumberColumns;
        }

        public const string MEMO = "memo";
        public const string GOTO = "goto";
        public const string ENDING = "ending";
        public const string TEXT = "text";

        /// <summary>인물·회차 CSV의 머리글 순서</summary>
        public static readonly string[] DAY_COLUMNS =
        {
            "section", "actor", "id", "situation", "text", "response", "need", "set", "trust", "dilation", "spring",
            "goto", "ending", "back", "title", "after", "speaker", "memo", "truth_trust", "key_flag", "leave_trust", "burden_limit", "rest",
        };

        private static readonly string[] STRINGS_COLUMNS = { "key", "text", "memo" };

        /// <summary>단계를 CSV에 적는 순서</summary>
        public static readonly string[] SECTIONS = { Csv.SECTION_TALK, Csv.SECTION_DIALOGUE, Csv.SECTION_ENDING };

        private static readonly Schema TalkSchema = new Schema
        {
            IdColumn = "id",
            BodyColumn = TEXT,
            GroupColumns = new[] { "need", "set" },
            ItemColumns = new[] { "speaker", "memo" },
            NumberColumns = new string[0],
        };

        private static readonly Schema DialogueSchema = new Schema
        {
            IdColumn = "id",
            BodyColumn = TEXT,
            GroupColumns = new[] { "situation", "back" },
            ItemColumns = new[] { "response", "need", "set", "trust", "dilation", "spring", "memo", "ending", "rest" },
            NumberColumns = new[] { "trust", "dilation", "spring" },
        };

        private static readonly Schema EndingSchema = new Schema
        {
            IdColumn = "id",
            BodyColumn = TEXT,
            GroupColumns = new[] { "title", "situation", "after", "truth_trust", "key_flag", "leave_trust", "burden_limit" },
            ItemColumns = new[] { "memo" },
            NumberColumns = new[] { "truth_trust", "leave_trust", "burden_limit" },
        };

        private static readonly Schema StringsSchema = new Schema
        {
            IdColumn = "key",
            BodyColumn = TEXT,
            GroupColumns = new string[0],
            ItemColumns = new[] { "memo" },
            NumberColumns = new string[0],
        };

        /// <summary>
        /// 그룹 단계의 열 구성을 가져온다. Strings 파일의 그룹은 section이 비어 있다.
        /// section을 사용하며, 열 구성을 반환한다.
        /// </summary>
        public static Schema SchemaOf(string section)
        {
            switch (section)
            {
                case Csv.SECTION_TALK: return TalkSchema;
                case Csv.SECTION_DIALOGUE: return DialogueSchema;
                case Csv.SECTION_ENDING: return EndingSchema;
                default: return StringsSchema;
            }
        }

        /// <summary>
        /// 에셋 경로로 파일 종류를 정한다.
        /// path를 사용하며, 지원하는 파일이면 kind에 종류를 쓰고 true를 반환한다.
        /// </summary>
        public static bool TryGetKind(string path, out DialogueEditorKind kind)
        {
            kind = DialogueEditorKind.Strings;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (path == Strings.DefaultPath) return true;
            if (path.StartsWith(DialogueData.DialogueFolder + "/")) { kind = DialogueEditorKind.Day; return true; }
            return false;
        }

        /// <summary>
        /// 단계 이름의 순서 번호를 구한다.
        /// section을 사용하며, talk 0, dialogue 1, ending 2, 그 밖은 3을 반환한다.
        /// </summary>
        public static int SectionRank(string section)
        {
            int i = System.Array.IndexOf(SECTIONS, section);
            return i < 0 ? SECTIONS.Length : i;
        }

        /// <summary>
        /// 그룹을 단계 순서(talk, dialogue, ending)로, 같은 단계 안에서는 actor가 처음 나온 순서로 모은다. 같은 actor 안의 순서는 그대로 둔다.
        /// doc을 사용하며, doc.Groups의 순서를 바꾼다.
        /// </summary>
        public static void Normalize(DialogueEditorDocument doc)
        {
            if (doc.Kind != DialogueEditorKind.Day) return;
            var actorOrder = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                string key = g.Section + "/" + g.Actor;
                if (!actorOrder.Contains(key)) actorOrder.Add(key);
            }
            var sorted = new List<DialogueEditorDocument.Group>(doc.Groups);
            var index = new Dictionary<DialogueEditorDocument.Group, int>();
            for (int i = 0; i < sorted.Count; i++) index[sorted[i]] = i;
            sorted.Sort((a, b) =>
            {
                int c = SectionRank(a.Section).CompareTo(SectionRank(b.Section));
                if (c != 0) return c;
                c = actorOrder.IndexOf(a.Section + "/" + a.Actor).CompareTo(actorOrder.IndexOf(b.Section + "/" + b.Actor));
                return c != 0 ? c : index[a].CompareTo(index[b]);
            });
            doc.Groups.Clear();
            doc.Groups.AddRange(sorted);
        }

        // ---------------------------------------------------------------- CSV → 모델

        /// <summary>
        /// CSV를 공통 모델로 읽는다. 같은 section·actor·id의 행은 처음 나온 그룹에 순서대로 모으고, 그룹 칸은 처음 적힌 값을 쓴다.
        /// asset, kind, path를 사용하며, 새 문서를 반환하고 읽기 문제는 errors에 남긴다.
        /// </summary>
        public static DialogueEditorDocument ReadCsv(TextAsset asset, DialogueEditorKind kind, string path, List<string> errors)
        {
            var doc = new DialogueEditorDocument { Kind = kind, AssetPath = path };
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string section = kind == DialogueEditorKind.Day ? r[Csv.SECTION].ToLowerInvariant() : "";
                string actor = kind == DialogueEditorKind.Day ? r[Csv.ACTOR] : "";
                if (kind == DialogueEditorKind.Day && SectionRank(section) >= SECTIONS.Length)
                {
                    errors.Add($"{r.Line}행 section '{r[Csv.SECTION]}'은 talk, dialogue, ending 중 하나여야 한다. 이 행은 읽지 않았다.");
                    continue;
                }
                Schema s = SchemaOf(section);
                string id = r[s.IdColumn];
                if (id.Length == 0) continue;
                DialogueEditorDocument.Group g = doc.Find(section, actor, id);
                if (g == null)
                {
                    g = new DialogueEditorDocument.Group { Section = section, Actor = actor, Id = id };
                    doc.Groups.Add(g);
                }
                foreach (string c in s.GroupColumns)
                {
                    string v = r[c];
                    if (v.Length == 0) continue;
                    // Talk의 need·set은 런타임처럼 여러 행의 값을 합친다.
                    if (section == Csv.SECTION_TALK && g.Get(c).Length > 0) g.Fields[c] = MergeList(g.Get(c), v);
                    else if (g.Get(c).Length == 0) g.Fields[c] = v;
                }
                if (r[s.BodyColumn].Length == 0 && kind != DialogueEditorKind.Strings) continue;
                var item = new DialogueEditorDocument.Item();
                item.Fields[s.BodyColumn] = r[s.BodyColumn];
                foreach (string c in s.ItemColumns) if (r[c].Length > 0) item.Fields[c] = r[c];
                if (section == Csv.SECTION_DIALOGUE && r[GOTO].Length > 0) item.Fields[GOTO] = r[GOTO];
                g.Items.Add(item);
            }
            Normalize(doc);
            return doc;
        }

        /// <summary>
        /// | 로 나눈 두 목록을 겹치지 않게 합친다.
        /// a, b를 사용하며, 합친 목록 글을 반환한다.
        /// </summary>
        private static string MergeList(string a, string b)
        {
            var list = new List<string>();
            foreach (string part in (a + "|" + b).Split('|'))
            {
                string t = part.Trim();
                if (t.Length > 0 && !list.Contains(t)) list.Add(t);
            }
            return string.Join("|", list);
        }

        // ---------------------------------------------------------------- 모델 → 텍스트

        /// <summary>
        /// 공통 모델을 SUDS식 텍스트로 쓴다. 단계가 바뀔 때마다 [eye section actor] 줄을 쓰고, 그룹 순서와 행 순서를 그대로 따른다.
        /// doc을 사용하며, 텍스트를 반환하고 각 그룹·항목의 TextLine을 새 줄 번호로 바꾼다.
        /// </summary>
        public static string WriteText(DialogueEditorDocument doc)
        {
            var lines = new List<string>();
            string stage = null;
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                Schema s = SchemaOf(g.Section);
                if (doc.Kind == DialogueEditorKind.Day && stage != g.Section + "/" + g.Actor)
                {
                    stage = g.Section + "/" + g.Actor;
                    if (lines.Count > 0) lines.Add("");
                    lines.Add($"[eye section={Quote(g.Section)} actor={Quote(g.Actor)}]");
                }
                if (lines.Count > 0) lines.Add("");
                g.TextLine = lines.Count + 1;
                lines.Add(":" + g.Id);
                string groupAttrs = Attrs(g.Fields, s.GroupColumns, s.NumberColumns, false);
                if (groupAttrs.Length > 0) lines.Add("[eye " + groupAttrs + "]");
                foreach (DialogueEditorDocument.Item i in g.Items)
                {
                    i.TextLine = lines.Count + 1;
                    if (g.Section == Csv.SECTION_DIALOGUE)
                    {
                        lines.Add("* " + Body(i.Get(s.BodyColumn)));
                        var attrColumns = new List<string>(s.ItemColumns);
                        attrColumns.Remove(ENDING);
                        lines.Add("\t[eye " + Attrs(i.Fields, attrColumns.ToArray(), s.NumberColumns, true) + "]");
                        if (i.Get(GOTO).Length > 0) lines.Add("\t[goto " + i.Get(GOTO) + "]");
                        if (i.Get(ENDING).Length > 0) lines.Add("\t[eye ending=" + Quote(i.Get(ENDING)) + "]");
                    }
                    else
                    {
                        string itemAttrs = Attrs(i.Fields, s.ItemColumns, s.NumberColumns, false);
                        if (itemAttrs.Length > 0) lines.Add("[eye " + itemAttrs + "]");
                        lines.Add("Line: " + Body(i.Get(s.BodyColumn)));
                    }
                }
            }
            return string.Join("\n", lines) + "\n";
        }

        /// <summary>
        /// [eye ...] 안에 들어갈 이름="값" 목록을 만든다. 빈 글자 칸은 빼고, 수치 칸은 allNumbers면 0이어도 쓴다.
        /// fields, columns, numbers, allNumbers를 사용하며, 공백으로 이은 글을 반환한다.
        /// </summary>
        private static string Attrs(Dictionary<string, string> fields, string[] columns, string[] numbers, bool allNumbers)
        {
            var parts = new List<string>();
            foreach (string c in columns)
            {
                fields.TryGetValue(c, out string v);
                v = v ?? "";
                if (System.Array.IndexOf(numbers, c) >= 0)
                {
                    if (v.Length == 0 && !allNumbers) continue;
                    parts.Add(c + "=" + (v.Length == 0 ? "0" : v));
                }
                else if (v.Length > 0) parts.Add(c + "=" + Quote(v));
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// 칸 값을 큰따옴표 글로 바꾼다. 따옴표와 역슬래시는 앞에 \를 붙이고, 줄바꿈과 칸 안의 \n 표기는 \n으로 쓴다.
        /// v를 사용하며, 따옴표로 감싼 글을 반환한다.
        /// </summary>
        public static string Quote(string v)
        {
            var sb = new StringBuilder("\"");
            for (int k = 0; k < v.Length; k++)
            {
                char c = v[k];
                if (c == '\\' && k + 1 < v.Length && v[k + 1] == 'n') { sb.Append("\\n"); k++; }
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\n') sb.Append("\\n");
                else if (c != '\r') sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        /// <summary>
        /// 행 본문을 한 줄로 쓴다. 실제 줄바꿈은 \n 표기로 바꾸고 나머지는 그대로 둔다.
        /// v를 사용하며, 한 줄 글을 반환한다.
        /// </summary>
        public static string Body(string v) => v.Replace("\r", "").Replace("\n", "\\n");

        /// <summary>
        /// 선택지 결과를 결과 줄로 나눈다. 줄바꿈 표기(\n)와 칸 안의 실제 줄바꿈을 모두 줄 경계로 본다.
        /// response를 사용하며, 결과 줄 목록(빈 칸이면 빈 목록)을 반환한다.
        /// </summary>
        public static List<string> ResponseLines(string response)
        {
            if (string.IsNullOrEmpty(response)) return new List<string>();
            return new List<string>(Body(response).Split(new[] { "\\n" }, System.StringSplitOptions.None));
        }

        /// <summary>
        /// 결과 줄을 response 한 칸에 넣을 글로 잇는다. 줄 사이에 줄바꿈 표기(\n)를 둔다.
        /// lines를 사용하며, 이은 글을 반환한다.
        /// </summary>
        public static string JoinResponse(List<string> lines) => string.Join("\\n", lines);

        // ---------------------------------------------------------------- 텍스트 → 모델

        /// <summary>
        /// SUDS식 텍스트를 공통 모델로 읽는다. 문법 문제는 줄 번호와 함께 issues에 남긴다.
        /// text, kind, path를 사용하며, 읽은 문서를 반환한다. issues에 오류가 있으면 그 문서는 쓰지 않는다.
        /// </summary>
        public static DialogueEditorDocument ParseText(string text, DialogueEditorKind kind, string path, List<Issue> issues)
        {
            var doc = new DialogueEditorDocument { Kind = kind, AssetPath = path };
            bool day = kind == DialogueEditorKind.Day;
            string section = day ? null : "";
            string actor = "";
            DialogueEditorDocument.Group g = null;
            DialogueEditorDocument.Item choice = null;
            Dictionary<string, string> pending = null;
            int pendingLine = 0;
            void Err(int line, string msg) => issues.Add(new Issue { Line = line, Message = msg });
            void FlushPending()
            {
                if (pending != null) Err(pendingLine, "[eye ...] 다음에 Line: 줄이 와야 한다.");
                pending = null;
            }

            string[] lines = (text ?? "").Replace("\r", "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string raw = lines[n];
                string t = raw.Trim();
                int ln = n + 1;
                if (t.Length == 0) continue;
                bool indented = raw.Length > 0 && char.IsWhiteSpace(raw[0]);
                if (raw.StartsWith("\t\t")) { Err(ln, "명령은 Tab 한 개만 들여쓴다. 선택지 안에 선택지를 넣을 수 없다."); continue; }

                if (t.StartsWith(":"))
                {
                    FlushPending();
                    choice = null;
                    if (indented) Err(ln, "라벨은 줄 맨 앞에 쓴다.");
                    string id = t.Substring(1).Trim();
                    // 이름 없는 라벨이나 단계 없는 라벨 아래 줄은 버릴 그룹에 받아 둬서 같은 오류가 줄마다 반복되지 않게 한다.
                    g = new DialogueEditorDocument.Group { Section = section ?? "", Actor = actor, Id = id, TextLine = ln };
                    if (section == null) { Err(ln, "라벨 앞에 [eye section=\"talk|dialogue|ending\" actor=\"...\"]로 단계를 정한다."); continue; }
                    if (id.Length == 0) { Err(ln, "라벨 이름이 비어 있다."); continue; }
                    if (!IsValidId(id)) Err(ln, $"라벨 '{id}'에 공백, 쉼표, 큰따옴표를 쓸 수 없다.");
                    if (doc.Find(section, actor, id) != null) Err(ln, $"라벨 '{id}'가 이 단계에 이미 있다.");
                    doc.Groups.Add(g);
                    continue;
                }

                bool dialogue = g != null && g.Section == Csv.SECTION_DIALOGUE;
                if (t.StartsWith("[") && TryParseCommand(t, out string cmd, out List<KeyValuePair<string, string>> stageAttrs, out _, out _)
                    && cmd == "eye" && stageAttrs.Exists(kv => kv.Key == Csv.SECTION))
                {
                    FlushPending();
                    choice = null;
                    g = null;
                    if (!day) { Err(ln, "Strings 파일에는 단계 명령을 쓰지 않는다."); continue; }
                    if (indented) Err(ln, "단계 명령은 줄 맨 앞에 쓴다.");
                    string sec = "", act = "";
                    foreach (KeyValuePair<string, string> kv in stageAttrs)
                    {
                        if (kv.Key == Csv.SECTION) sec = kv.Value.ToLowerInvariant();
                        else if (kv.Key == Csv.ACTOR) act = kv.Value;
                        else Err(ln, $"단계 명령에는 section과 actor만 쓴다 ('{kv.Key}').");
                    }
                    if (SectionRank(sec) >= SECTIONS.Length) { Err(ln, $"section '{sec}'은 talk, dialogue, ending 중 하나여야 한다."); section = null; continue; }
                    if (act.Length == 0 || !IsValidId(act)) { Err(ln, "actor에 공백·쉼표·큰따옴표가 없는 ID를 쓴다 (예: EunjaeMom)."); section = null; continue; }
                    section = sec;
                    actor = act;
                    continue;
                }

                if (g == null) { Err(ln, section == null && day ? "먼저 [eye section=\"...\" actor=\"...\"]로 단계를 정한다." : "첫 내용 줄은 :라벨이어야 한다."); continue; }
                Schema s = SchemaOf(g.Section);

                if (dialogue && t.StartsWith("*"))
                {
                    if (indented) Err(ln, "선택지 *는 줄 맨 앞에 쓴다.");
                    string body = t.Substring(1).Trim();
                    if (body.Length == 0) Err(ln, "선택지 글이 비어 있다.");
                    choice = new DialogueEditorDocument.Item { TextLine = ln };
                    choice.Fields[s.BodyColumn] = body;
                    g.Items.Add(choice);
                    continue;
                }
                if (!dialogue && t.StartsWith("Line:"))
                {
                    string body = t.Substring(5).Trim();
                    if (body.Length == 0 && kind != DialogueEditorKind.Strings) Err(ln, "Line: 뒤 대사가 비어 있다.");
                    var item = new DialogueEditorDocument.Item { TextLine = pending != null ? pendingLine : ln };
                    if (pending != null) foreach (KeyValuePair<string, string> kv in pending) item.Fields[kv.Key] = kv.Value;
                    pending = null;
                    item.Fields[s.BodyColumn] = body;
                    g.Items.Add(item);
                    continue;
                }
                if (!t.StartsWith("["))
                {
                    Err(ln, dialogue ? "알 수 없는 줄이다. :라벨, * 선택지, [eye ...], [goto 대상] 중 하나로 쓴다." : "알 수 없는 줄이다. :라벨, [eye ...], Line: 대사 중 하나로 쓴다.");
                    continue;
                }

                if (!TryParseCommand(t, out string name, out List<KeyValuePair<string, string>> attrs, out string arg, out string why))
                {
                    Err(ln, why);
                    continue;
                }
                if (name == "goto")
                {
                    if (!dialogue) { Err(ln, "[goto]는 dialogue 단계에서만 쓴다."); continue; }
                    if (choice == null) { Err(ln, "[goto]는 * 선택지 아래에 쓴다."); continue; }
                    if (arg.Length == 0 || arg.IndexOf(' ') >= 0) { Err(ln, "[goto 대상]에 노드 이름 하나를 쓴다."); continue; }
                    if (choice.Get(GOTO).Length > 0) { Err(ln, "한 선택지에 [goto]를 두 번 쓸 수 없다."); continue; }
                    choice.Fields[GOTO] = arg;
                    continue;
                }
                if (name != "eye") { Err(ln, $"지원하지 않는 명령 [{name}]이다. [eye ...]{(dialogue ? "와 [goto 대상]" : "")}만 쓸 수 있다."); continue; }

                if (dialogue)
                {
                    bool forGroup = choice == null;
                    string[] allowed = forGroup ? s.GroupColumns : s.ItemColumns;
                    Dictionary<string, string> target = forGroup ? g.Fields : choice.Fields;
                    foreach (KeyValuePair<string, string> kv in attrs)
                    {
                        if (System.Array.IndexOf(allowed, kv.Key) < 0)
                        {
                            Err(ln, forGroup ? $"노드 칸에는 situation, back만 쓴다 ('{kv.Key}'). 선택지 칸은 * 줄 아래에 쓴다." : $"선택지에 쓸 수 없는 속성 '{kv.Key}'이다.");
                            continue;
                        }
                        if (target.ContainsKey(kv.Key)) { Err(ln, $"'{kv.Key}' 값을 두 번 적었다."); continue; }
                        if (!CheckNumber(s, kv, ln, issues)) continue;
                        target[kv.Key] = kv.Value;
                    }
                    continue;
                }

                bool anyGroup = false, anyItem = false;
                foreach (KeyValuePair<string, string> kv in attrs)
                {
                    if (System.Array.IndexOf(s.GroupColumns, kv.Key) >= 0) anyGroup = true;
                    else if (System.Array.IndexOf(s.ItemColumns, kv.Key) >= 0) anyItem = true;
                    else Err(ln, $"이 단계에 쓸 수 없는 속성 '{kv.Key}'이다.");
                }
                if (anyGroup && anyItem) { Err(ln, "그룹 칸과 줄 칸을 한 [eye ...]에 섞어 쓸 수 없다."); continue; }
                if (anyGroup)
                {
                    if (g.Items.Count > 0 || pending != null) { Err(ln, "그룹 칸 [eye ...]은 라벨 바로 아래, 첫 Line: 앞에 쓴다."); continue; }
                    foreach (KeyValuePair<string, string> kv in attrs)
                    {
                        if (g.Fields.ContainsKey(kv.Key)) { Err(ln, $"'{kv.Key}' 값을 두 번 적었다."); continue; }
                        g.Fields[kv.Key] = kv.Value;
                    }
                    continue;
                }
                if (!anyItem) continue;
                if (pending != null) Err(pendingLine, "[eye ...] 다음에 Line: 줄이 와야 한다.");
                pending = new Dictionary<string, string>();
                pendingLine = ln;
                foreach (KeyValuePair<string, string> kv in attrs) pending[kv.Key] = kv.Value;
            }
            FlushPending();
            Normalize(doc);
            return doc;
        }

        /// <summary>
        /// 그룹 ID나 actor ID로 쓸 수 있는지 본다. 공백, 쉼표, 큰따옴표가 없어야 한다.
        /// id를 사용하며, 쓸 수 있으면 true를 반환한다.
        /// </summary>
        public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.IndexOfAny(new[] { ' ', '\t', ',', '"' }) < 0;

        /// <summary>
        /// 수치 칸이면 값이 수인지 검사한다.
        /// s, kv, ln을 사용하며, 쓸 수 있으면 true를 반환하고 아니면 issues에 남긴다.
        /// </summary>
        private static bool CheckNumber(Schema s, KeyValuePair<string, string> kv, int ln, List<Issue> issues)
        {
            if (System.Array.IndexOf(s.NumberColumns, kv.Key) < 0 || kv.Value.Length == 0 || IsNumber(kv.Key, kv.Value)) return true;
            issues.Add(new Issue { Line = ln, Message = $"'{kv.Key}' 값 '{kv.Value}'이 수가 아니다{(kv.Key == "dilation" ? " (소수점은 .으로 쓴다)" : " (정수)")}." });
            return false;
        }

        /// <summary>
        /// [이름 ...] 한 줄을 읽는다. [goto 대상]은 arg에, [eye 이름="값" ...]은 attrs에 담는다.
        /// t를 사용하며, 읽었으면 true를 반환하고 못 읽으면 why에 이유를 쓴다.
        /// </summary>
        private static bool TryParseCommand(string t, out string name, out List<KeyValuePair<string, string>> attrs, out string arg, out string why)
        {
            name = ""; arg = ""; why = "";
            attrs = new List<KeyValuePair<string, string>>();
            if (!t.EndsWith("]")) { why = "명령 줄은 ]로 끝나야 한다."; return false; }
            string inner = t.Substring(1, t.Length - 2).Trim();
            int sp = inner.IndexOf(' ');
            name = sp < 0 ? inner : inner.Substring(0, sp);
            string rest = sp < 0 ? "" : inner.Substring(sp + 1).Trim();
            if (name.Length == 0) { why = "명령 이름이 비어 있다."; return false; }
            if (name != "eye") { arg = rest; return true; }

            int k = 0;
            while (k < rest.Length)
            {
                if (rest[k] == ' ' || rest[k] == '\t') { k++; continue; }
                int keyStart = k;
                while (k < rest.Length && (char.IsLetterOrDigit(rest[k]) || rest[k] == '_')) k++;
                string key = rest.Substring(keyStart, k - keyStart);
                if (key.Length == 0 || k >= rest.Length || rest[k] != '=') { why = $"[eye ...] 안은 이름=\"값\" 꼴로 쓴다 ({k + 1}번째 글자 부근)."; return false; }
                k++;
                var sb = new StringBuilder();
                if (k < rest.Length && rest[k] == '"')
                {
                    k++;
                    bool closed = false;
                    while (k < rest.Length)
                    {
                        char c = rest[k];
                        if (c == '\\')
                        {
                            if (k + 1 >= rest.Length) break;
                            char e = rest[k + 1];
                            if (e == '"') sb.Append('"');
                            else if (e == '\\') sb.Append('\\');
                            else if (e == 'n') sb.Append("\\n");
                            else { why = $"'{key}' 값에 지원하지 않는 이스케이프 \\{e}가 있다 (\\\", \\\\, \\n만 쓴다)."; return false; }
                            k += 2;
                            continue;
                        }
                        if (c == '"') { closed = true; k++; break; }
                        sb.Append(c);
                        k++;
                    }
                    if (!closed) { why = $"'{key}' 값의 큰따옴표가 닫히지 않았다."; return false; }
                }
                else
                {
                    while (k < rest.Length && rest[k] != ' ' && rest[k] != '\t') sb.Append(rest[k++]);
                }
                foreach (KeyValuePair<string, string> kv in attrs)
                    if (kv.Key == key) { why = $"'{key}'를 한 줄에 두 번 적었다."; return false; }
                attrs.Add(new KeyValuePair<string, string>(key, sb.ToString().Trim()));
            }
            return true;
        }

        /// <summary>
        /// 수치 칸 글을 불변 문화권 수로 읽을 수 있는지 본다.
        /// column과 v를 사용하며, 읽을 수 있으면 true를 반환한다.
        /// </summary>
        public static bool IsNumber(string column, string v)
        {
            if (column == "dilation" || column == "burden_limit") return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        // ---------------------------------------------------------------- 모델 → CSV, 검사

        /// <summary>
        /// 공통 모델을 CSV 글로 쓴다. 머리글 다음에 그룹 순서(단계별로 모은 순서), 행 순서대로 한 행씩 쓰고 그룹 칸은 그룹의 첫 행에 적는다.
        /// doc을 사용하며, CSV 글(CRLF)을 반환하고 rowLines에 각 행의 텍스트 줄 번호를 차례로 담는다.
        /// </summary>
        public static string WriteCsv(DialogueEditorDocument doc, List<int> rowLines)
        {
            string[] columns = doc.Kind == DialogueEditorKind.Day ? DAY_COLUMNS : STRINGS_COLUMNS;
            var sb = new StringBuilder();
            sb.Append(string.Join(",", columns)).Append("\r\n");
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Items.Count == 0)
                {
                    WriteRow(sb, columns, g, null, true);
                    rowLines.Add(g.TextLine);
                    continue;
                }
                for (int k = 0; k < g.Items.Count; k++)
                {
                    WriteRow(sb, columns, g, g.Items[k], k == 0);
                    rowLines.Add(g.Items[k].TextLine);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// CSV 한 행을 쓴다.
        /// sb, columns, g, item(없으면 그룹 칸만), first(그룹의 첫 행인지)를 사용하며, sb에 행을 덧붙인다.
        /// </summary>
        private static void WriteRow(StringBuilder sb, string[] columns, DialogueEditorDocument.Group g, DialogueEditorDocument.Item item, bool first)
        {
            Schema s = SchemaOf(g.Section);
            var cells = new List<string>();
            foreach (string c in columns)
            {
                string v;
                if (c == Csv.SECTION) v = g.Section;
                else if (c == Csv.ACTOR) v = g.Actor;
                else if (c == s.IdColumn) v = g.Id;
                else if (System.Array.IndexOf(s.GroupColumns, c) >= 0) v = first ? g.Get(c) : "";
                else if (item == null) v = "";
                else if (c == s.BodyColumn || System.Array.IndexOf(s.ItemColumns, c) >= 0 || (c == GOTO && g.Section == Csv.SECTION_DIALOGUE)) v = item.Get(c);
                else v = "";
                cells.Add(Cell(v));
            }
            sb.Append(string.Join(",", cells)).Append("\r\n");
        }

        /// <summary>
        /// CSV 칸 하나를 쓴다. 쉼표, 따옴표, 줄바꿈이나 앞뒤 공백이 있으면 큰따옴표로 감싸고 안의 따옴표는 두 번 쓴다.
        /// v를 사용하며, CSV 칸 글을 반환한다.
        /// </summary>
        private static string Cell(string v)
        {
            v = v ?? "";
            bool quote = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || (v.Length > 0 && (char.IsWhiteSpace(v[0]) || char.IsWhiteSpace(v[v.Length - 1])));
            return quote ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        /// <summary>
        /// 저장하기 전에 공통 모델을 검사한다. 만든 CSV를 기존 런타임 파서로 다시 읽어 그 규칙을 그대로 쓰고,
        /// 단계별 actor 규칙, goto·ending 참조, need·set의 Flags.csv 참조를 맞춰 본다.
        /// doc을 사용하며, 문제를 issues에 남기고 저장할 CSV 글을 반환한다.
        /// </summary>
        public static string Validate(DialogueEditorDocument doc, List<Issue> issues)
        {
            var rowLines = new List<int>();
            string csv = WriteCsv(doc, rowLines);
            int LineOf(int csvLine) => csvLine - 2 >= 0 && csvLine - 2 < rowLines.Count ? rowLines[csvLine - 2] : 0;
            void Err(int line, string msg) => issues.Add(new Issue { Line = line, Message = msg });

            var dialogueActors = new List<string>();
            var endingActors = new List<string>();
            var talkActors = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Id.StartsWith("#")) Err(g.TextLine, $"라벨 '{g.Id}'는 #로 시작할 수 없다 (CSV 주석 줄이 된다).");
                if (doc.Kind == DialogueEditorKind.Strings && g.Items.Count == 0) Err(g.TextLine, $"'{g.Id}'에 Line: 문구가 없다.");
                if (g.Section == Csv.SECTION_ENDING && g.Id.StartsWith("@"))
                    foreach (string c in EndingSchema.GroupColumns)
                        if (g.Get(c).Length > 0) Err(g.TextLine, $"'{g.Id}'에는 {c}를 쓰지 않는다 (title, situation, after는 엔딩에만 쓴다).");
                List<string> list = g.Section == Csv.SECTION_DIALOGUE ? dialogueActors : g.Section == Csv.SECTION_ENDING ? endingActors : talkActors;
                if (!list.Contains(g.Actor)) list.Add(g.Actor);
            }

            // 한 회차 파일은 상담 인물 한 명의 것이다. 상담과 엔딩은 같은 actor 하나만 둔다.
            if (dialogueActors.Count > 1) Err(FirstLine(doc, Csv.SECTION_DIALOGUE, dialogueActors[1]), $"dialogue 단계의 actor는 파일마다 하나다 ({string.Join(", ", dialogueActors)}).");
            if (endingActors.Count > 1) Err(FirstLine(doc, Csv.SECTION_ENDING, endingActors[1]), $"ending 단계의 actor는 파일마다 하나다 ({string.Join(", ", endingActors)}).");
            if (dialogueActors.Count == 1 && endingActors.Count == 1 && dialogueActors[0] != endingActors[0])
                Err(FirstLine(doc, Csv.SECTION_ENDING, endingActors[0]), $"ending의 actor '{endingActors[0]}'가 상담 actor '{dialogueActors[0]}'와 다르다.");

            // 선택지의 ending은 같은 상담 actor의 엔딩 id에서 찾는다. goto는 런타임 파서가 검사한다.
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Section != Csv.SECTION_DIALOGUE) continue;
                foreach (DialogueEditorDocument.Item i in g.Items)
                {
                    string end = i.Get(ENDING);
                    if (end.Length > 0 && (end.StartsWith("@") || doc.Find(Csv.SECTION_ENDING, g.Actor, end) == null))
                        Err(i.TextLine, $"ending '{end}'이 '{g.Actor}'의 엔딩 목록에 없다.");
                    // 결과 줄은 게임에서 한 줄씩 넘기므로 빈 줄이 있으면 빈 메시지가 뜬다.
                    string response = i.Get("response");
                    if (response.Length > 0 && ResponseLines(response).Exists(l => l.Trim().Length == 0))
                        Err(i.TextLine, "결과에 빈 줄이 있다. 줄바꿈(\\n) 앞뒤에 글을 쓰거나 빈 줄을 지운다.");
                }
            }

            // 기존 런타임 파서로 다시 읽어 같은 규칙으로 검사한다. 메시지 앞의 'N행'은 텍스트 줄 번호로 바꾼다.
            var asset = new TextAsset(csv) { name = System.IO.Path.GetFileNameWithoutExtension(doc.AssetPath) };
            var runtimeErrors = new List<string>();
            if (doc.Kind == DialogueEditorKind.Day)
            {
                if (dialogueActors.Count > 0) runtimeErrors.AddRange(DialogueScript.Parse(asset).Errors);
                foreach (string a in talkActors) runtimeErrors.AddRange(TalkScript.Parse(asset, a).Errors);
                if (endingActors.Count > 0) runtimeErrors.AddRange(EndingScript.Parse(asset).Errors);
            }
            else Csv.Read(asset, runtimeErrors);
            Object.DestroyImmediate(asset);
            foreach (string e in runtimeErrors)
            {
                Match m = Regex.Match(e, @"^(\d+)행\s*");
                if (m.Success) issues.Add(new Issue { Line = LineOf(int.Parse(m.Groups[1].Value)), Message = e.Substring(m.Length) });
                else issues.Add(new Issue { Message = e });
            }

            if (doc.Kind == DialogueEditorKind.Day)
            {
                CheckFlags(doc, issues);
                CheckKeyFlag(doc, issues);
            }
            return csv;
        }

        /// <summary>
        /// 이 회차 파일을 쓰는 씬의 손님(ClientCase)을 찾는다.
        /// path를 사용하며, 손님 또는 (없으면) null을 반환한다.
        /// </summary>
        public static ClientCase CaseOf(string path)
        {
            foreach (ClientCase c in Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.DialogueCsv != null && AssetDatabase.GetAssetPath(c.DialogueCsv) == path) return c;
            return null;
        }

        /// <summary>
        /// 이 회차 손님의 파편 Flag ID를 모은다. 같은 Flag가 둘 이상인 것은 빼서 열쇠 파편 후보로 쓰지 않는다.
        /// path를 사용하며, (손님, 고유한 Flag 목록)을 반환한다. 손님이 없으면 (null, 빈 목록)이다.
        /// </summary>
        public static (ClientCase owner, List<string> flags) CaseFragmentFlags(string path)
        {
            ClientCase c = CaseOf(path);
            var flags = new List<string>();
            if (c == null) return (null, flags);
            var count = new Dictionary<string, int>();
            foreach (MemoryFragment f in c.Fragments())
            {
                if (string.IsNullOrEmpty(f.Flag)) continue;
                count.TryGetValue(f.Flag, out int n);
                count[f.Flag] = n + 1;
                if (n == 0) flags.Add(f.Flag);
            }
            flags.RemoveAll(f => count[f] > 1);
            return (c, flags);
        }

        /// <summary>
        /// truth의 key_flag가 이 회차 손님의 파편 중 하나(Flag가 고유한 것)를 가리키는지 본다. 비어 있으면 파편 조건이 없다.
        /// 손님을 씬에서 찾지 못하면 확인할 수 없다는 경고를 남긴다. doc을 사용하며, 문제를 issues에 남긴다.
        /// </summary>
        private static void CheckKeyFlag(DialogueEditorDocument doc, List<Issue> issues)
        {
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Section != Csv.SECTION_ENDING || g.Id != EndingScript.TRUTH) continue;
                string key = g.Get(EndingScript.KEY_FLAG);
                if (key.Length == 0) return;
                (ClientCase owner, List<string> flags) = CaseFragmentFlags(doc.AssetPath);
                if (owner == null)
                {
                    issues.Add(new Issue { Line = g.TextLine, Error = false, Message = $"이 파일을 쓰는 손님(ClientCase)을 열린 씬에서 찾지 못해 열쇠 파편 '{key}'를 확인하지 못했다." });
                    return;
                }
                if (!flags.Contains(key))
                    issues.Add(new Issue { Line = g.TextLine, Message = $"열쇠 파편 '{key}'가 '{owner.PersonName}' 케이스의 파편(Flag가 하나뿐인 것) 중에 없다." });
                return;
            }
        }

        /// <summary>
        /// 단계의 첫 그룹 라벨 줄을 찾는다.
        /// doc, section, actor를 사용하며, 줄 번호 또는 0을 반환한다.
        /// </summary>
        private static int FirstLine(DialogueEditorDocument doc, string section, string actor)
        {
            foreach (DialogueEditorDocument.Group g in doc.Groups) if (g.Section == section && g.Actor == actor) return g.TextLine;
            return 0;
        }

        /// <summary>
        /// need·set에 쓴 플래그를 Flags.csv와 맞춰 본다. 목록에 없는 플래그와 set에 쓴 계산 플래그는 오류, 켜는 곳이 없는 need는 경고다.
        /// doc을 사용하며, 문제를 issues에 남긴다.
        /// </summary>
        private static void CheckFlags(DialogueEditorDocument doc, List<Issue> issues)
        {
            var loadErrors = new List<string>();
            var known = new HashSet<string>();
            var computed = new HashSet<string>();
            foreach (DialogueData.FlagDef d in DialogueData.LoadFlags(loadErrors))
            {
                if (d.Id.Length == 0) continue;
                known.Add(d.Id);
                if (!string.IsNullOrEmpty(d.Condition)) computed.Add(d.Id);
            }
            if (known.Count == 0) return;

            // 켜는 곳은 저장된 다른 파일·씬과 지금 편집 중인 문서에서 모은다.
            var setters = new Dictionary<string, List<DialogueData.Ref>>();
            DialogueData.CollectRefs(DialogueData.LoadDialogues(), setters, new Dictionary<string, List<DialogueData.Ref>>());
            var setHere = new HashSet<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                foreach (string f in Split(g.Get("set"))) setHere.Add(f);
                foreach (DialogueEditorDocument.Item i in g.Items) foreach (string f in Split(i.Get("set"))) setHere.Add(f);
            }

            void Check(string need, string set, int line)
            {
                foreach (string f in Split(need))
                {
                    if (!known.Contains(f)) issues.Add(new Issue { Line = line, Message = $"플래그 '{f}'가 Flags.csv에 없다." });
                    else if (!computed.Contains(f) && !setters.ContainsKey(f) && !setHere.Contains(f))
                        issues.Add(new Issue { Line = line, Error = false, Message = $"플래그 '{f}'를 켜는 곳(파편/단서/set)이 없어 열리지 않는다." });
                }
                foreach (string f in Split(set))
                {
                    if (!known.Contains(f)) issues.Add(new Issue { Line = line, Message = $"플래그 '{f}'가 Flags.csv에 없다." });
                    else if (computed.Contains(f)) issues.Add(new Issue { Line = line, Message = $"'{f}'는 계산 플래그라 set으로 켤 수 없다." });
                }
            }
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Section == Csv.SECTION_TALK) Check(g.Get("need"), g.Get("set"), g.TextLine);
                else if (g.Section == Csv.SECTION_DIALOGUE) foreach (DialogueEditorDocument.Item i in g.Items) Check(i.Get("need"), i.Get("set"), i.TextLine);
            }
        }

        /// <summary>
        /// | 로 나눈 목록 글을 나눈다.
        /// v를 사용하며, 빈 항목을 뺀 목록을 반환한다.
        /// </summary>
        private static List<string> Split(string v)
        {
            var list = new List<string>();
            foreach (string part in (v ?? "").Split('|'))
            {
                string t = part.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// CSV 글을 UTF-8(BOM 포함)로 파일에 쓰고 다시 임포트한다. 내용이 같으면 쓰지 않는다.
        /// path와 csv를 사용하며, 파일을 썼으면 true를 반환한다.
        /// </summary>
        public static bool Save(string path, string csv)
        {
            string full = System.IO.Path.GetFullPath(path);
            if (System.IO.File.Exists(full) && Csv.TryDecodeUtf8(System.IO.File.ReadAllBytes(full), out string old) && old == csv) return false;
            System.IO.File.WriteAllText(full, csv, new UTF8Encoding(true));
            AssetDatabase.ImportAsset(path);
            return true;
        }
    }
}
