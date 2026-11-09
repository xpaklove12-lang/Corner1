using System.Collections.Generic;
using System.Globalization;
using System.Text;

using UnityEngine;

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
