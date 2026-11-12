using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 회상 대본(Data/Flashbacks.csv)을 읽어 플래그별 도입 줄, 단계, 회상 뒤 감정을 제공한다.
    /// 열: flag, order, prop, speaker, text, emotion_after. prop이 적힌 줄에서 새 단계가 시작되고, 비운 줄은 앞줄과 같은 단계에 이어진다.
    /// </summary>
    public static class FlashbackScript
    {
        /// <summary>회상 글 칸에 나오는 줄 하나</summary>
        public class Line
        {
            public string Speaker { get; set; }
            public string Text { get; set; }
            public int Row { get; set; }
        }

        /// <summary>사물 하나에 상호작용하면 나오는 줄 묶음</summary>
        public class Step
        {
            public string Prop { get; set; }
            public int Row { get; set; }
            public List<Line> Lines { get; } = new List<Line>();
        }

        /// <summary>플래그 하나의 회상 전체</summary>
        public class Flashback
        {
            public string Flag { get; set; }
            public int Row { get; set; }
            public ClientEmotion? EmotionAfter { get; set; }
            public List<Line> Intro { get; } = new List<Line>();
            public List<Step> Steps { get; } = new List<Step>();

            /// <summary>도입 줄과 단계 줄을 합친 대본 줄 수</summary>
            public int LineCount
            {
                get
                {
                    int count = Intro.Count;
                    foreach (Step s in Steps) count += s.Lines.Count;
                    return count;
                }
            }
        }

        private static readonly Dictionary<string, Flashback> _flashbacks = new Dictionary<string, Flashback>();
        private static TextAsset _asset;

        /// <summary>
        /// 회상 대본을 읽는다. 같은 파일이면 다시 읽지 않는다.
        /// asset과 force를 사용하며, 플래그별 회상 목록을 바꾸고 문제가 있으면 Console에 오류를 남긴다.
        /// </summary>
        public static void Load(TextAsset asset, bool force = false)
        {
            if (asset == _asset && !force) return;
            _asset = asset;
            _flashbacks.Clear();
            if (asset == null) return;
            var errors = new List<string>();
            foreach (KeyValuePair<string, Flashback> pair in Parse(asset, errors)) _flashbacks[pair.Key] = pair.Value;
            foreach (string e in errors) Debug.LogError($"[Flashbacks.csv] {e}", asset);
        }

        /// <summary>
        /// 마지막으로 읽은 회상 대본을 다시 읽는다 (에디터에서 파일이 바뀌었을 때).
        /// 입력값은 없으며, 플래그별 회상 목록을 바꾼다.
        /// </summary>
        public static void Reload() => Load(_asset, true);

        /// <summary>
        /// 플래그의 회상을 찾는다.
        /// flag를 사용하며, 회상 또는 (없으면) null을 반환한다.
        /// </summary>
        public static Flashback Find(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return null;
            return _flashbacks.TryGetValue(flag, out Flashback f) ? f : null;
        }

        /// <summary>
        /// 회상 대본 CSV를 플래그별 회상으로 나눈다. 같은 플래그의 줄은 order 순서로 정렬한다.
        /// asset과 errors를 사용하며, 플래그별 회상 사전을 반환하고 문제가 있으면 errors에 남긴다.
        /// </summary>
        public static Dictionary<string, Flashback> Parse(TextAsset asset, List<string> errors)
        {
            var rowsByFlag = new Dictionary<string, List<(int order, Csv.Row row)>>();
            var flagOrder = new List<string>();
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string flag = r["flag"];
                if (flag.Length == 0) { errors.Add($"{r.Line}행 flag가 비어 있다."); continue; }
                if (r["text"].Length == 0) errors.Add($"{r.Line}행 text가 비어 있다.");
                if (!rowsByFlag.TryGetValue(flag, out List<(int, Csv.Row)> list))
                {
                    rowsByFlag[flag] = list = new List<(int, Csv.Row)>();
                    flagOrder.Add(flag);
                }
                list.Add((r.Int("order", errors), r));
            }

            var result = new Dictionary<string, Flashback>();
            foreach (string flag in flagOrder)
            {
                List<(int order, Csv.Row row)> rows = rowsByFlag[flag];
                // 같은 order가 있으면 파일에 적힌 순서를 유지한다.
                rows.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.row.Line.CompareTo(b.row.Line));
                var f = new Flashback { Flag = flag, Row = rows[0].row.Line };
                Step step = null;
                foreach ((int _, Csv.Row r) in rows)
                {
                    string emotion = r["emotion_after"];
                    if (emotion.Length > 0)
                    {
                        // 첫 줄이 아닌 줄의 값은 오류로 알리고 쓰지 않는다.
                        if (r != rows[0].row) errors.Add($"{r.Line}행 '{flag}'의 emotion_after는 그 회상의 첫 줄에만 적는다.");
                        else if (f.EmotionAfter.HasValue) errors.Add($"{r.Line}행 '{flag}'의 emotion_after가 두 번 적혀 있다. 첫 줄에만 적는다.");
                        else if (ClientEmotionCsv.TryParse(emotion, out ClientEmotion e)) f.EmotionAfter = e;
                        else errors.Add($"{r.Line}행 emotion_after '{emotion}'는 쓸 수 없는 값이다. ({ClientEmotionCsv.VALUES})");
                    }
                    var line = new Line { Speaker = r["speaker"], Text = r["text"].Replace("\\n", "\n"), Row = r.Line };
                    string prop = r["prop"];
                    if (prop.Length > 0)
                    {
                        step = new Step { Prop = prop, Row = r.Line };
                        f.Steps.Add(step);
                    }
                    if (step != null) step.Lines.Add(line);
                    else f.Intro.Add(line);
                }
                result[flag] = f;
            }
            return result;
        }
    }
}
