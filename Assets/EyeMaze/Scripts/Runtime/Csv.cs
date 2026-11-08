using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// UTF-8 CSV를 읽는다. 따옴표 안의 쉼표/줄바꿈("" 는 따옴표 하나)을 지원한다.
    /// 첫 칸이 #로 시작하는 줄과 빈 줄은 건너뛰고, 첫 번째 남은 줄을 머리글로 쓴다.
    /// 머리글 이름으로 열을 찾으므로 기획자가 열 순서를 바꾸거나 메모 열을 더해도 된다.
    /// </summary>
    public static class Csv
    {
        /// <summary>CSV 한 줄. 머리글 이름으로 칸을 꺼낸다.</summary>
        public class Row
        {
            private readonly Dictionary<string, int> _columns;
            private readonly List<string> _cells;

            /// <summary>파일 안의 줄 번호 (1부터, 엑셀 행 번호와 같음)</summary>
            public int Line { get; }

            public Row(Dictionary<string, int> columns, List<string> cells, int line)
            {
                _columns = columns;
                _cells = cells;
                Line = line;
            }

            /// <summary>
            /// 머리글 이름의 칸 값을 꺼낸다.
            /// column을 사용하며, 앞뒤 공백을 뺀 값 또는 빈 문자열을 반환한다.
            /// </summary>
            public string this[string column]
            {
                get
                {
                    if (!_columns.TryGetValue(column, out int i) || i >= _cells.Count) return "";
                    return _cells[i].Trim();
                }
            }

            /// <summary>
            /// 칸 값을 정수로 읽는다. 비어 있으면 0이다.
            /// column과 errors를 사용하며, 읽은 값을 반환하고 못 읽으면 errors에 남긴다.
            /// </summary>
            public int Int(string column, List<string> errors)
            {
                string s = this[column];
                if (s.Length == 0) return 0;
                if (int.TryParse(s, out int v)) return v;
                errors?.Add($"{Line}행 '{column}' 칸이 정수가 아니다: {s}");
                return 0;
            }

            /// <summary>
            /// 칸 값을 실수로 읽는다. 비어 있으면 0이다.
            /// column과 errors를 사용하며, 읽은 값을 반환하고 못 읽으면 errors에 남긴다.
            /// </summary>
            public float Float(string column, List<string> errors)
            {
                string s = this[column];
                if (s.Length == 0) return 0f;
                if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
                errors?.Add($"{Line}행 '{column}' 칸이 숫자가 아니다: {s}");
                return 0f;
            }

            /// <summary>
            /// | 로 나눈 목록 칸을 읽는다.
            /// column을 사용하며, 빈 항목을 뺀 배열을 반환한다.
            /// </summary>
            public string[] List(string column)
            {
                string s = this[column];
                if (s.Length == 0) return Array.Empty<string>();
                var list = new List<string>();
                foreach (string part in s.Split('|'))
                {
                    string t = part.Trim();
                    if (t.Length > 0) list.Add(t);
                }
                return list.ToArray();
            }

            public bool Has(string column) => _columns.ContainsKey(column);
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 바이트를 UTF-8로 읽는다. UTF-8이 아니면(엑셀 기본 저장 CP949 등) 실패한다.
        /// bytes를 사용하며, 성공하면 BOM을 뺀 글을 text에 쓰고 true를 반환한다.
        /// </summary>
        public static bool TryDecodeUtf8(byte[] bytes, out string text)
        {
            text = "";
            if (bytes == null) return false;
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            try { text = StrictUtf8.GetString(bytes, start, bytes.Length - start); return true; }
            catch (DecoderFallbackException) { return false; }
        }

        /// <summary>
        /// TextAsset을 UTF-8 CSV로 읽는다.
        /// asset을 사용하며, 줄 목록을 반환하고 문제가 있으면 errors에 남긴다.
        /// </summary>
        public static List<Row> Read(TextAsset asset, List<string> errors)
        {
            if (asset == null) return new List<Row>();
            if (!TryDecodeUtf8(asset.bytes, out string text))
            {
                errors.Add($"{asset.name}.csv 가 UTF-8이 아니다. 엑셀이면 '다른 이름으로 저장 → CSV UTF-8(쉼표로 분리)'로 저장해 주세요.");
                return new List<Row>();
            }
            return Parse(text, errors);
        }

        /// <summary>
        /// CSV 글을 줄 목록으로 나눈다.
        /// text를 사용하며, 머리글을 뺀 줄 목록을 반환한다.
        /// </summary>
        public static List<Row> Parse(string text, List<string> errors)
        {
            var rows = new List<Row>();
            Dictionary<string, int> columns = null;
            foreach ((List<string> cells, int line) in Split(text))
            {
                bool empty = true;
                foreach (string c in cells) if (c.Trim().Length > 0) { empty = false; break; }
                if (empty) continue;
                if (cells[0].TrimStart().StartsWith("#")) continue;
                if (columns == null)
                {
                    columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < cells.Count; i++)
                    {
                        string name = cells[i].Trim();
                        if (name.Length > 0 && !columns.ContainsKey(name)) columns[name] = i;
                    }
                    continue;
                }
                rows.Add(new Row(columns, cells, line));
            }
            if (columns == null) errors?.Add("머리글 줄이 없다.");
            return rows;
        }

        /// <summary>
        /// RFC 4180 방식으로 칸을 나눈다.
        /// text를 사용하며, (칸 목록, 시작 줄 번호)를 차례로 돌려준다.
        /// </summary>
        private static IEnumerable<(List<string>, int)> Split(string text)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            int line = 1, rowLine = 1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else
                    {
                        if (c == '\n') line++;
                        if (c != '\r') sb.Append(c);
                    }
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    cells.Add(sb.ToString()); sb.Clear();
                    yield return (cells, rowLine);
                    cells = new List<string>();
                    line++;
                    rowLine = line;
                }
                else sb.Append(c);
            }
            if (sb.Length > 0 || cells.Count > 0)
            {
                cells.Add(sb.ToString());
                yield return (cells, rowLine);
            }
        }
    }
}
