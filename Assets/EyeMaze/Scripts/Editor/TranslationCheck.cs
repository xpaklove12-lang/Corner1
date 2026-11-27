using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>
    /// 공용 문구 파일(Data/Loc/UI.csv)과 씬 문구 파일(Data/Loc/Scene.csv)의 번역을 검사한다. 기획 데이터 검사가 부른다.
    /// 번역은 파일을 통째로 바꿔 넣으므로, 바꿔 넣은 파일이 게임에서 깨지지 않는지 여기서 확인한다.
    /// </summary>
    public static class TranslationCheck
    {
        /// <summary>언어 폰트가 비었을 때 씬 글에 쓰는 기본 폰트</summary>
        private const string DEFAULT_FONT_PATH = "Assets/EyeMaze/Fonts/Galmuri11.ttf";
        private const string OK_SUFFIX = "_ok";
        /// <summary>경고 한 줄에 보일 예시 수</summary>
        private const int SAMPLE_COUNT = 10;
        /// <summary>언어 칸이 아닌 열</summary>
        private static readonly HashSet<string> NonLanguageColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "key", "context", "maxlen", "note", "ko_synced" };
        /// <summary>글 속 {이름} 또는 {이름|조사} 칸</summary>
        private static readonly Regex Placeholder = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)(?:\|[^}]*)?\}");

        /// <summary>
        /// 두 번역 파일을 검사한다.
        /// err, warn과 Loc.Config의 언어 목록을 사용하며, 문제를 err와 warn으로 남긴다.
        /// </summary>
        public static void Validate(Action<string, Object> err, Action<string, Object> warn)
        {
            LocConfig config = Loc.Config;
            if (config == null) return;
            var fonts = new Dictionary<string, HashSet<int>>();
            CheckFile(Strings.DefaultPath, false, config, fonts, err, warn);
            CheckFile(Strings.SCENE_PATH, true, config, fonts, err, warn);
        }

        /// <summary>
        /// 번역 파일 하나를 검사한다. 파일이 없으면 건너뛴다.
        /// path, uniqueKeys(같은 key를 한 번만 쓰는 파일인지), config, fonts(언어별 글자 표 캐시), err, warn을 사용하며, 문제를 err와 warn으로 남긴다.
        /// </summary>
        private static void CheckFile(string path, bool uniqueKeys, LocConfig config, Dictionary<string, HashSet<int>> fonts, Action<string, Object> err, Action<string, Object> warn)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null) return;
            string label = Path.GetFileName(path);
            var readErrors = new List<string>();
            List<Csv.Row> rows = Csv.Read(asset, readErrors);
            foreach (string e in readErrors) err($"{label}: {e}", asset);
            if (!Csv.TryDecodeUtf8(asset.bytes, out string text)) return;

            List<string> header = text.Split('\n')[0].Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
            var codes = new HashSet<string>(config.Languages.Where(l => l != null).Select(l => l.Code));
            foreach (string c in header)
            {
                string code = c.EndsWith(OK_SUFFIX, StringComparison.OrdinalIgnoreCase) ? c.Substring(0, c.Length - OK_SUFFIX.Length) : c;
                if (NonLanguageColumns.Contains(c) || codes.Contains(code)) continue;
                err($"{label}: '{c}' 칸의 언어 '{code}'가 언어 설정(LocConfig)에 없다.", asset);
            }
            List<string> targets = codes.Where(c => c != Loc.SOURCE_LANGUAGE).ToList();
            foreach (string code in targets.Where(c => !header.Contains(c)))
                warn($"{label}: 언어 '{code}' 칸이 없다.", asset);

            if (uniqueKeys)
            {
                foreach (IGrouping<string, Csv.Row> g in rows.Where(r => r["key"].Length > 0).GroupBy(r => r["key"]).Where(g => g.Count() > 1))
                    err($"{label}: key '{g.Key}'가 {string.Join(", ", g.Select(r => r.Line + "행"))}에 두 번 이상 있다.", asset);
            }

            foreach (string code in targets.Where(c => header.Contains(c)))
            {
                HashSet<int> glyphs = GlyphsOf(config.Find(code), fonts);
                int empty = 0, recheck = 0;
                var missing = new SortedSet<string>();
                foreach (Csv.Row r in rows)
                {
                    if (r["key"].Length == 0) continue;
                    string source = r[Loc.SOURCE_LANGUAGE];
                    string t = r[code];
                    if (t.Length == 0)
                    {
                        if (source.Length > 0) empty++;
                        continue;
                    }
                    string wanted = string.Join(", ", Names(source));
                    string got = string.Join(", ", Names(t));
                    if (wanted != got) err($"{label} {r.Line}행 '{r["key"]}': {code} 번역의 칸 {{{got}}}이(가) 원문의 칸 {{{wanted}}}과 다르다.", asset);
                    if (int.TryParse(r["maxlen"], out int max) && max > 0 && t.Replace("\\n", "").Length > max)
                        warn($"{label} {r.Line}행 '{r["key"]}': {code} 번역이 {t.Length}자로 최대 {max}자를 넘는다.", asset);
                    if (header.Contains(code + OK_SUFFIX) && r[code + OK_SUFFIX].Length == 0) recheck++;
                    if (glyphs != null) foreach (int cp in Codepoints(t)) if (!glyphs.Contains(cp)) missing.Add(char.ConvertFromUtf32(cp));
                }
                if (empty > 0) warn($"{label}: {code} 번역이 빈 줄이 {empty}개다.", asset);
                if (recheck > 0) warn($"{label}: {code} 번역 중 다시 확인할 줄({code}{OK_SUFFIX}이 빈 줄)이 {recheck}개다.", asset);
                if (missing.Count > 0) warn($"{label}: {code} 폰트에 없는 글자 {missing.Count}개: {string.Join(" ", missing.Take(SAMPLE_COUNT))}{(missing.Count > SAMPLE_COUNT ? " …" : "")}", asset);
            }
        }

        /// <summary>
        /// 글 속 칸 이름을 모은다. 조사 표기({client|이/가})는 이름만 본다.
        /// s를 사용하며, 정렬한 칸 이름 목록을 반환한다.
        /// </summary>
        private static List<string> Names(string s) => Placeholder.Matches(s).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>
        /// 글의 글자를 코드 값으로 꺼낸다. 공백, 제어 문자, 줄바꿈 표기와 {…} 칸은 뺀다.
        /// s를 사용하며, 코드 값을 차례로 돌려준다.
        /// </summary>
        private static IEnumerable<int> Codepoints(string s)
        {
            s = Placeholder.Replace(s.Replace("\\n", " "), " ");
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i]) || char.IsControl(s[i])) continue;
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length)
                {
                    yield return char.ConvertToUtf32(s[i], s[i + 1]);
                    i++;
                    continue;
                }
                yield return s[i];
            }
        }

        /// <summary>
        /// 언어의 폰트 파일에서 글자 표를 읽는다. 폰트가 비었으면 기본 폰트를 쓰고, 한 번 읽은 폰트는 다시 읽지 않는다.
        /// language와 fonts를 사용하며, 글자 코드 집합 또는 (읽을 수 없으면) null을 반환한다.
        /// </summary>
        private static HashSet<int> GlyphsOf(LocConfig.Language language, Dictionary<string, HashSet<int>> fonts)
        {
            string path = language != null && language.Font != null ? AssetDatabase.GetAssetPath(language.Font) : DEFAULT_FONT_PATH;
            if (fonts.TryGetValue(path, out HashSet<int> cached)) return cached;
            HashSet<int> glyphs = null;
            string full = Path.GetFullPath(path);
            if (File.Exists(full) && (path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
            {
                try { glyphs = FontCmap.Read(File.ReadAllBytes(full)); }
                catch (Exception e) { Debug.LogWarning($"[번역 검사] 폰트 '{path}'의 글자 표를 읽지 못했다: {e.Message}"); }
            }
            fonts[path] = glyphs;
            return glyphs;
        }
    }

    /// <summary>
    /// TrueType·OpenType 폰트 파일의 cmap 표에서 글자가 있는 코드 값을 읽는다.
    /// Unity 동적 폰트의 HasCharacter는 글자를 불러오기 전까지 믿을 수 없어 파일을 직접 읽는다.
    /// </summary>
    public static class FontCmap
    {
        /// <summary>
        /// 폰트 파일에서 글자가 있는 코드 값을 모은다. 유니코드 전체 표(형식 12)를 먼저 쓰고, 없으면 BMP 표(형식 4)를 쓴다.
        /// data(폰트 파일 바이트)를 사용하며, 코드 값 집합을 반환한다. cmap 표가 없으면 빈 집합을 반환한다.
        /// </summary>
        public static HashSet<int> Read(byte[] data)
        {
            var glyphs = new HashSet<int>();
            int cmap = FindTable(data, "cmap");
            if (cmap < 0) return glyphs;
            int count = U16(data, cmap + 2);
            int format12 = -1, format4 = -1;
            for (int i = 0; i < count; i++)
            {
                int rec = cmap + 4 + i * 8;
                int platform = U16(data, rec), encoding = U16(data, rec + 2);
                int sub = cmap + (int)U32(data, rec + 4);
                int format = U16(data, sub);
                bool unicode = platform == 0 || (platform == 3 && (encoding == 1 || encoding == 10));
                if (!unicode) continue;
                if (format == 12 && format12 < 0) format12 = sub;
                if (format == 4 && format4 < 0) format4 = sub;
            }
            if (format12 >= 0) ReadFormat12(data, format12, glyphs);
            else if (format4 >= 0) ReadFormat4(data, format4, glyphs);
            return glyphs;
        }

        /// <summary>
        /// 표 목록에서 이름이 tag인 표의 시작 위치를 찾는다.
        /// data와 tag를 사용하며, 시작 위치 또는 (없으면) -1을 반환한다.
        /// </summary>
        private static int FindTable(byte[] data, string tag)
        {
            int tables = U16(data, 4);
            for (int i = 0; i < tables; i++)
            {
                int rec = 12 + i * 16;
                string name = new string(new[] { (char)data[rec], (char)data[rec + 1], (char)data[rec + 2], (char)data[rec + 3] });
                if (name == tag) return (int)U32(data, rec + 8);
            }
            return -1;
        }

        /// <summary>
        /// 형식 4 표(BMP 구간 목록)를 읽는다.
        /// data, at(표 시작 위치)를 사용하며, 글자가 있는 코드 값을 glyphs에 더한다.
        /// </summary>
        private static void ReadFormat4(byte[] data, int at, HashSet<int> glyphs)
        {
            int segments = U16(data, at + 6) / 2;
            int ends = at + 14, starts = ends + segments * 2 + 2, deltas = starts + segments * 2, ranges = deltas + segments * 2;
            for (int s = 0; s < segments; s++)
            {
                int end = U16(data, ends + s * 2), start = U16(data, starts + s * 2);
                int delta = (short)U16(data, deltas + s * 2);
                int rangePos = ranges + s * 2, range = U16(data, rangePos);
                for (int c = start; c <= end && c != 0xFFFF; c++)
                {
                    int glyph;
                    if (range == 0) glyph = (c + delta) & 0xFFFF;
                    else
                    {
                        glyph = U16(data, rangePos + range + (c - start) * 2);
                        if (glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    }
                    if (glyph != 0) glyphs.Add(c);
                }
            }
        }

        /// <summary>
        /// 형식 12 표(유니코드 전체 구간 목록)를 읽는다.
        /// data, at(표 시작 위치)를 사용하며, 글자가 있는 코드 값을 glyphs에 더한다.
        /// </summary>
        private static void ReadFormat12(byte[] data, int at, HashSet<int> glyphs)
        {
            long groups = U32(data, at + 12);
            for (long g = 0; g < groups; g++)
            {
                int rec = at + 16 + (int)g * 12;
                long start = U32(data, rec), end = U32(data, rec + 4), first = U32(data, rec + 8);
                for (long c = start; c <= end; c++) if (first + (c - start) != 0) glyphs.Add((int)c);
            }
        }

        /// <summary>
        /// 빅 엔디언 16비트 값을 읽는다.
        /// d(바이트)와 i(위치)를 사용하며, 읽은 값을 반환한다.
        /// </summary>
        private static int U16(byte[] d, int i) => (d[i] << 8) | d[i + 1];

        /// <summary>
        /// 빅 엔디언 32비트 값을 읽는다.
        /// d(바이트)와 i(위치)를 사용하며, 읽은 값을 반환한다.
        /// </summary>
        private static long U32(byte[] d, int i) => ((long)d[i] << 24) | ((long)d[i + 1] << 16) | ((long)d[i + 2] << 8) | d[i + 3];
    }
}
