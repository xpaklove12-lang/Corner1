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
    /// 텍스트 표기: ":id"가 그룹을 시작하고, "[eye 이름="값"]"이 칸 값을, Dialogue의 "* 선택지"와 "[goto 대상]",
    /// 다른 파일의 "Line: 대사"가 행을 나타낸다.
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

        /// <summary>파일 종류마다 다른 열 구성</summary>
        public class Schema
        {
            /// <summary>CSV에 쓰는 열 순서</summary>
            public string[] Columns;
            /// <summary>그룹 id 열 (node, group, id, key)</summary>
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

        private static readonly Schema DialogueSchema = new Schema
        {
            Columns = new[] { "node", "situation", "text", "response", "need", "set", "trust", "dilation", "spring", "goto", "ending", "back", "memo" },
            IdColumn = "node",
            BodyColumn = "text",
            GroupColumns = new[] { "situation", "back" },
            ItemColumns = new[] { "response", "need", "set", "trust", "dilation", "spring", "memo", "ending" },
            NumberColumns = new[] { "trust", "dilation", "spring" },
        };

        private static readonly Schema TalkSchema = new Schema
        {
            Columns = new[] { "group", "need", "set", "speaker", "text", "memo" },
            IdColumn = "group",
            BodyColumn = "text",
            GroupColumns = new[] { "need", "set" },
            ItemColumns = new[] { "speaker", "memo" },
            NumberColumns = new string[0],
        };

        private static readonly Schema EndingSchema = new Schema
        {
            Columns = new[] { "id", "title", "situation", "after", "line", "memo" },
            IdColumn = "id",
            BodyColumn = "line",
            GroupColumns = new[] { "title", "situation", "after" },
            ItemColumns = new[] { "memo" },
            NumberColumns = new string[0],
        };

        private static readonly Schema StringsSchema = new Schema
        {
            Columns = new[] { "key", "text", "memo" },
            IdColumn = "key",
            BodyColumn = "text",
            GroupColumns = new string[0],
            ItemColumns = new[] { "memo" },
            NumberColumns = new string[0],
        };

        /// <summary>
        /// 파일 종류의 열 구성을 가져온다.
        /// kind를 사용하며, 열 구성을 반환한다.
        /// </summary>
        public static Schema SchemaOf(DialogueEditorKind kind)
        {
            switch (kind)
            {
                case DialogueEditorKind.Dialogue: return DialogueSchema;
                case DialogueEditorKind.Talk: return TalkSchema;
                case DialogueEditorKind.Ending: return EndingSchema;
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
            if (path == Strings.DefaultPath) { kind = DialogueEditorKind.Strings; return true; }
            if (path.StartsWith(DialogueData.DialogueFolder + "/")) { kind = DialogueEditorKind.Dialogue; return true; }
            if (path.StartsWith(DialogueData.TalkFolder + "/")) { kind = DialogueEditorKind.Talk; return true; }
            if (path.StartsWith(DialogueData.EndingFolder + "/")) { kind = DialogueEditorKind.Ending; return true; }
            return false;
        }

        // ---------------------------------------------------------------- CSV → 모델

        /// <summary>
        /// CSV를 공통 모델로 읽는다. 같은 id의 행은 처음 나온 그룹에 순서대로 모으고, 그룹 칸은 처음 적힌 값을 쓴다.
        /// asset, kind, path를 사용하며, 새 문서를 반환하고 읽기 문제는 errors에 남긴다.
        /// </summary>
        public static DialogueEditorDocument ReadCsv(TextAsset asset, DialogueEditorKind kind, string path, List<string> errors)
        {
            var doc = new DialogueEditorDocument { Kind = kind, AssetPath = path };
            Schema s = SchemaOf(kind);
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string id = r[s.IdColumn];
                if (id.Length == 0) continue;
                DialogueEditorDocument.Group g = doc.Find(id);
                if (g == null)
                {
                    g = new DialogueEditorDocument.Group { Id = id };
                    doc.Groups.Add(g);
                }
                foreach (string c in s.GroupColumns)
                {
                    string v = r[c];
                    if (v.Length == 0) continue;
                    // Talk의 need·set은 런타임처럼 여러 행의 값을 합친다.
                    if (kind == DialogueEditorKind.Talk && g.Get(c).Length > 0) g.Fields[c] = MergeList(g.Get(c), v);
                    else if (g.Get(c).Length == 0) g.Fields[c] = v;
                }
                if (r[s.BodyColumn].Length == 0 && !(kind == DialogueEditorKind.Strings)) continue;
                var item = new DialogueEditorDocument.Item();
                item.Fields[s.BodyColumn] = r[s.BodyColumn];
                foreach (string c in s.ItemColumns) if (r[c].Length > 0) item.Fields[c] = r[c];
                if (kind == DialogueEditorKind.Dialogue && r[GOTO].Length > 0) item.Fields[GOTO] = r[GOTO];
                g.Items.Add(item);
            }
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
        /// 공통 모델을 SUDS식 텍스트로 쓴다. 그룹 순서와 행 순서를 그대로 따른다.
        /// doc을 사용하며, 텍스트를 반환하고 각 그룹·항목의 TextLine을 새 줄 번호로 바꾼다.
        /// </summary>
        public static string WriteText(DialogueEditorDocument doc)
        {
            Schema s = SchemaOf(doc.Kind);
            var lines = new List<string>();
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (lines.Count > 0) lines.Add("");
                g.TextLine = lines.Count + 1;
                lines.Add(":" + g.Id);
                string groupAttrs = Attrs(g.Fields, s.GroupColumns, s.NumberColumns, false);
                if (groupAttrs.Length > 0) lines.Add("[eye " + groupAttrs + "]");
                foreach (DialogueEditorDocument.Item i in g.Items)
                {
                    i.TextLine = lines.Count + 1;
                    if (doc.Kind == DialogueEditorKind.Dialogue)
                    {
                        lines.Add("* " + Body(i.Get(s.BodyColumn)));
                        var attrColumns = new List<string>(s.ItemColumns);
                        attrColumns.Remove(ENDING);
                        lines.Add("  [eye " + Attrs(i.Fields, attrColumns.ToArray(), s.NumberColumns, true) + "]");
                        if (i.Get(GOTO).Length > 0) lines.Add("  [goto " + i.Get(GOTO) + "]");
                        if (i.Get(ENDING).Length > 0) lines.Add("  [eye ending=" + Quote(i.Get(ENDING)) + "]");
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
        private static string Body(string v) => v.Replace("\r", "").Replace("\n", "\\n");

        // ---------------------------------------------------------------- 모델 → CSV, 검사

        /// <summary>
        /// 공통 모델을 CSV 글로 쓴다. 머리글 다음에 그룹 순서, 행 순서대로 한 행씩 쓰고 그룹 칸은 그룹의 첫 행에 적는다.
        /// doc을 사용하며, CSV 글(CRLF)을 반환하고 rowLines에 각 행의 텍스트 줄 번호를 차례로 담는다.
        /// </summary>
        public static string WriteCsv(DialogueEditorDocument doc, List<int> rowLines)
        {
            Schema s = SchemaOf(doc.Kind);
            var sb = new StringBuilder();
            sb.Append(string.Join(",", s.Columns)).Append("\r\n");
            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Items.Count == 0)
                {
                    WriteRow(sb, s, g, null, true);
                    rowLines.Add(g.TextLine);
                    continue;
                }
                for (int k = 0; k < g.Items.Count; k++)
                {
                    WriteRow(sb, s, g, g.Items[k], k == 0);
                    rowLines.Add(g.Items[k].TextLine);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// CSV 한 행을 쓴다.
        /// sb, s, g, item(없으면 그룹 칸만), first(그룹의 첫 행인지)를 사용하며, sb에 행을 덧붙인다.
        /// </summary>
        private static void WriteRow(StringBuilder sb, Schema s, DialogueEditorDocument.Group g, DialogueEditorDocument.Item item, bool first)
        {
            var cells = new List<string>();
            foreach (string c in s.Columns)
            {
                string v;
                if (c == s.IdColumn) v = g.Id;
                else if (System.Array.IndexOf(s.GroupColumns, c) >= 0) v = first ? g.Get(c) : "";
                else v = item != null ? item.Get(c) : "";
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
        /// Dialogue·Talk의 need·set은 Flags.csv와 맞춰 본다.
        /// doc을 사용하며, 문제를 issues에 남기고 저장할 CSV 글을 반환한다.
        /// </summary>
        public static string Validate(DialogueEditorDocument doc, List<Issue> issues)
        {
            var rowLines = new List<int>();
            string csv = WriteCsv(doc, rowLines);
            int LineOf(int csvLine) => csvLine - 2 >= 0 && csvLine - 2 < rowLines.Count ? rowLines[csvLine - 2] : 0;

            foreach (DialogueEditorDocument.Group g in doc.Groups)
            {
                if (g.Id.StartsWith("#")) issues.Add(new Issue { Line = g.TextLine, Message = $"라벨 '{g.Id}'는 #로 시작할 수 없다 (CSV 주석 줄이 된다)." });
                if (doc.Kind == DialogueEditorKind.Strings && g.Items.Count == 0)
                    issues.Add(new Issue { Line = g.TextLine, Message = $"'{g.Id}'에 Line: 문구가 없다." });
                if (doc.Kind == DialogueEditorKind.Ending && g.Id.StartsWith("@"))
                    foreach (string c in SchemaOf(doc.Kind).GroupColumns)
                        if (g.Get(c).Length > 0) issues.Add(new Issue { Line = g.TextLine, Message = $"'{g.Id}'에는 {c}를 쓰지 않는다 (title, situation, after는 엔딩에만 쓴다)." });
            }

            // 기존 런타임 파서로 다시 읽어 같은 규칙으로 검사한다. 메시지 앞의 'N행'은 텍스트 줄 번호로 바꾼다.
            var asset = new TextAsset(csv) { name = System.IO.Path.GetFileNameWithoutExtension(doc.AssetPath) };
            List<string> runtimeErrors;
            switch (doc.Kind)
            {
                case DialogueEditorKind.Dialogue: runtimeErrors = DialogueScript.Parse(asset).Errors; break;
                case DialogueEditorKind.Talk: runtimeErrors = TalkScript.Parse(asset).Errors; break;
                case DialogueEditorKind.Ending: runtimeErrors = EndingScript.Parse(asset).Errors; break;
                default: runtimeErrors = new List<string>(); Csv.Read(asset, runtimeErrors); break;
            }
            Object.DestroyImmediate(asset);
            foreach (string e in runtimeErrors)
            {
                Match m = Regex.Match(e, @"^(\d+)행\s*");
                if (m.Success) issues.Add(new Issue { Line = LineOf(int.Parse(m.Groups[1].Value)), Message = e.Substring(m.Length) });
                else issues.Add(new Issue { Message = e });
            }

            if (doc.Kind == DialogueEditorKind.Dialogue || doc.Kind == DialogueEditorKind.Talk) CheckFlags(doc, issues);
            return csv;
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
                if (doc.Kind == DialogueEditorKind.Talk) Check(g.Get("need"), g.Get("set"), g.TextLine);
                else foreach (DialogueEditorDocument.Item i in g.Items) Check(i.Get("need"), i.Get("set"), i.TextLine);
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

        // ---------------------------------------------------------------- 텍스트 → 모델

        /// <summary>
        /// SUDS식 텍스트를 공통 모델로 읽는다. 문법 문제는 줄 번호와 함께 issues에 남긴다.
        /// text, kind, path를 사용하며, 읽은 문서를 반환한다. issues에 오류가 있으면 그 문서는 쓰지 않는다.
        /// </summary>
        public static DialogueEditorDocument ParseText(string text, DialogueEditorKind kind, string path, List<Issue> issues)
        {
            var doc = new DialogueEditorDocument { Kind = kind, AssetPath = path };
            Schema s = SchemaOf(kind);
            bool dialogue = kind == DialogueEditorKind.Dialogue;
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
                string t = lines[n].Trim();
                int ln = n + 1;
                if (t.Length == 0) continue;

                if (t.StartsWith(":"))
                {
                    FlushPending();
                    string id = t.Substring(1).Trim();
                    choice = null;
                    // 이름 없는 라벨 아래 줄은 버릴 그룹에 받아 둬서 같은 오류가 줄마다 반복되지 않게 한다.
                    if (id.Length == 0) { Err(ln, "라벨 이름이 비어 있다."); g = new DialogueEditorDocument.Group { TextLine = ln }; continue; }
                    if (id.IndexOfAny(new[] { ' ', '\t', ',', '"' }) >= 0) Err(ln, $"라벨 '{id}'에 공백, 쉼표, 큰따옴표를 쓸 수 없다.");
                    if (doc.Find(id) != null) Err(ln, $"라벨 '{id}'가 이미 있다.");
                    g = new DialogueEditorDocument.Group { Id = id, TextLine = ln };
                    doc.Groups.Add(g);
                    continue;
                }
                if (g == null) { Err(ln, "첫 내용 줄은 :라벨이어야 한다."); continue; }

                if (dialogue && t.StartsWith("*"))
                {
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
                    if (!dialogue) { Err(ln, "[goto]는 Dialogue 파일에서만 쓴다."); continue; }
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
                    else Err(ln, $"이 파일에 쓸 수 없는 속성 '{kv.Key}'이다.");
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
                if (pending != null) { Err(pendingLine, "[eye ...] 다음에 Line: 줄이 와야 한다."); }
                pending = new Dictionary<string, string>();
                pendingLine = ln;
                foreach (KeyValuePair<string, string> kv in attrs) pending[kv.Key] = kv.Value;
            }
            FlushPending();
            return doc;
        }

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
            if (column == "dilation") return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
    }
}
