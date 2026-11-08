using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 플래그 관리자. 게임 전체에서 켜진 플래그(문자열 id)를 기억한다.
    /// 기억 파편을 줍거나, 단서를 살펴보거나, 대화 선택지(set 칸)를 고르면 켜지고,
    /// 대화 선택지의 need 칸은 여기서 켜져 있는지를 본다.
    ///
    /// Flags.csv에서 condition 칸을 채운 플래그는 "계산 플래그"다. 직접 켜지 않고, 볼 때마다 조건으로 계산한다.
    /// 예) trust_3 = trust>=3,  not_asked = !asked_piano,  ready = trust>=2 &amp; clue_sheet_music
    /// </summary>
    public static class Flags
    {
        /// <summary>condition에서 쓸 수 있는 수치 이름 (값은 디렉터가 Values로 알려준다)</summary>
        public static readonly string[] KnownValues = { "trust", "dilation", "spring" };

        private static readonly HashSet<string> On = new HashSet<string>();
        private static readonly Dictionary<string, Condition> Computed = new Dictionary<string, Condition>();
        private static readonly Dictionary<string, string> Titles = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
        private static TextAsset _defsAsset;
        private static bool _built;
        private static int _depth;

        /// <summary>플래그가 바뀔 때마다 불린다 (플래그 관리자 창 갱신용)</summary>
        public static event Action Changed;

        /// <summary>수치 이름(trust 등)의 지금 값을 알려준다. 모르는 이름이면 null</summary>
        public static Func<string, float?> Values { get; set; }

        /// <summary>직접 켜진 플래그들 (계산 플래그 제외)</summary>
        public static IReadOnlyCollection<string> AllOn { get { Build(); return On; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            On.Clear();
            _built = false;
        }

        /// <summary>
        /// Flags.csv를 읽어 계산 플래그(condition 칸)를 등록한다. 같은 파일이면 다시 읽지 않는다.
        /// asset과 force를 사용하며, 계산 플래그 목록을 바꾼다.
        /// </summary>
        public static void LoadDefinitions(TextAsset asset, bool force = false)
        {
            if (asset == null || (asset == _defsAsset && !force)) return;
            _defsAsset = asset;
            Computed.Clear();
            Titles.Clear();
            Texts.Clear();
            var errors = new List<string>();
            foreach (Csv.Row r in Csv.Read(asset, errors))
            {
                string id = r["id"];
                if (id.Length > 0 && r["title"].Length > 0) Titles[id] = r["title"].Replace("\\n", "\n");
                if (id.Length > 0 && r["text"].Length > 0) Texts[id] = r["text"].Replace("\\n", "\n");
                string cond = r["condition"];
                if (id.Length == 0 || cond.Length == 0) continue;
                Condition c = Condition.Parse(cond, errors, r.Line);
                if (c != null) Computed[id] = c;
            }
            foreach (string e in errors) Debug.LogError($"[Flags.csv] {e}", asset);
            Changed?.Invoke();
        }

        /// <summary>
        /// 마지막으로 읽은 Flags.csv를 다시 읽는다 (에디터에서 파일이 바뀌었을 때).
        /// </summary>
        public static void ReloadDefinitions() => LoadDefinitions(_defsAsset, true);

        /// <summary>
        /// 플래그에 붙은 힌트 제목(Flags.csv의 title 칸)을 가져온다. 파편/단서 이름으로 쓴다.
        /// id를 사용하며, 제목 또는 (없으면) null을 반환한다.
        /// </summary>
        public static string TitleOf(string id) => !string.IsNullOrEmpty(id) && Titles.TryGetValue(id, out string t) ? t : null;

        /// <summary>
        /// 플래그에 붙은 힌트 글(Flags.csv의 text 칸)을 가져온다. 파편을 줍거나 단서를 살펴볼 때 나오는 글이다.
        /// id를 사용하며, 글 또는 (없으면) null을 반환한다.
        /// </summary>
        public static string TextOf(string id) => !string.IsNullOrEmpty(id) && Texts.TryGetValue(id, out string t) ? t : null;

        /// <summary>
        /// 계산 플래그인지 본다.
        /// id를 사용하며, condition이 있는 플래그면 true를 반환한다.
        /// </summary>
        public static bool IsComputed(string id) => !string.IsNullOrEmpty(id) && Computed.ContainsKey(id);

        /// <summary>
        /// 플래그가 켜져 있는지 본다. 계산 플래그는 지금 값으로 조건을 계산한다. 빈 id는 항상 켜진 것으로 본다.
        /// id를 사용하며, 켜져 있으면 true를 반환한다.
        /// </summary>
        public static bool IsOn(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            if (Computed.TryGetValue(id, out Condition c))
            {
                // 계산 플래그끼리 서로를 부르며 끝없이 도는 것을 막는다.
                if (_depth > 16) { Debug.LogError($"[Flags] '{id}' 조건이 서로를 계속 부른다 (순환)."); return false; }
                _depth++;
                try { return c.Evaluate(); }
                finally { _depth--; }
            }
            Build();
            return On.Contains(id);
        }

        /// <summary>
        /// 목록의 플래그가 모두 켜져 있는지 본다.
        /// ids를 사용하며, 모두 켜져 있으면(또는 목록이 비면) true를 반환한다.
        /// </summary>
        public static bool AllSet(IReadOnlyList<string> ids)
        {
            if (ids == null) return true;
            for (int i = 0; i < ids.Count; i++) if (!IsOn(ids[i])) return false;
            return true;
        }

        /// <summary>
        /// 플래그를 켜거나 끈다. 계산 플래그는 직접 바꿀 수 없다.
        /// id와 on을 사용하며, 켜진 플래그 목록을 바꾸고 Changed를 부른다.
        /// </summary>
        public static void Set(string id, bool on = true)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (IsComputed(id)) { Debug.LogWarning($"[Flags] '{id}'는 계산 플래그라 직접 켜고 끌 수 없다 (Flags.csv의 condition을 따른다)."); return; }
            Build();
            bool changed = on ? On.Add(id) : On.Remove(id);
            if (changed) Changed?.Invoke();
        }

        /// <summary>
        /// 모든 플래그를 끈 뒤, 씬의 파편/단서 상태에서 다시 채운다.
        /// 입력값은 없으며, 켜진 플래그 목록을 바꾼다.
        /// </summary>
        public static void ResetAll()
        {
            On.Clear();
            _built = false;
            Build();
            Changed?.Invoke();
        }

        /// <summary>
        /// 처음 한 번, 씬에 저장된 파편(collected)과 단서(examined) 상태를 플래그로 옮긴다.
        /// 에디트 모드에서 스크립트가 다시 컴파일돼도 상태가 맞게 유지된다.
        /// </summary>
        private static void Build()
        {
            if (_built) return;
            _built = true;
            foreach (MemoryFragment f in UnityEngine.Object.FindObjectsByType<MemoryFragment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.Collected && !string.IsNullOrEmpty(f.Flag)) On.Add(f.Flag);
            foreach (FieldClue c in UnityEngine.Object.FindObjectsByType<FieldClue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.Examined && !string.IsNullOrEmpty(c.Flag)) On.Add(c.Flag);
        }

        /// <summary>
        /// 계산 플래그의 조건. &amp;로 이어진 항이 모두 참이면 참이다.
        /// 항: 플래그 이름 / !플래그 이름 / 수치 비교(trust&gt;=3, dilation&lt;0.5, spring==0 …)
        /// </summary>
        public class Condition
        {
            private enum Kind { Flag, NotFlag, Compare }

            private struct Term
            {
                public Kind Kind;
                public string Name;
                public string Op;
                public float Number;
            }

            private static readonly string[] Ops = { ">=", "<=", "!=", "==", ">", "<" };
            private readonly List<Term> _terms = new List<Term>();

            /// <summary>조건 안에서 쓰는 다른 플래그 이름들 (검사용)</summary>
            public IEnumerable<string> FlagNames { get { foreach (Term t in _terms) if (t.Kind != Kind.Compare) yield return t.Name; } }

            /// <summary>조건 안에서 쓰는 수치 이름들 (검사용)</summary>
            public IEnumerable<string> ValueNames { get { foreach (Term t in _terms) if (t.Kind == Kind.Compare) yield return t.Name; } }

            /// <summary>
            /// 조건 글을 읽는다.
            /// text, errors, line을 사용하며, 읽은 조건 또는 (틀리면) null을 반환하고 errors에 남긴다.
            /// </summary>
            public static Condition Parse(string text, List<string> errors, int line)
            {
                var c = new Condition();
                foreach (string raw in text.Split('&'))
                {
                    string s = raw.Trim();
                    if (s.Length == 0) { errors?.Add($"{line}행 condition에 빈 항이 있다: {text}"); return null; }
                    string op = null;
                    int at = -1;
                    foreach (string o in Ops) { at = s.IndexOf(o, StringComparison.Ordinal); if (at > 0) { op = o; break; } }
                    if (op != null)
                    {
                        string name = s.Substring(0, at).Trim();
                        string num = s.Substring(at + op.Length).Trim();
                        if (!float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        { errors?.Add($"{line}행 condition '{s}'의 오른쪽이 숫자가 아니다."); return null; }
                        if (Array.IndexOf(KnownValues, name) < 0)
                        { errors?.Add($"{line}행 condition '{s}'의 '{name}'은 모르는 수치다. ({string.Join(", ", KnownValues)} 중 하나)"); return null; }
                        c._terms.Add(new Term { Kind = Kind.Compare, Name = name, Op = op, Number = v });
                    }
                    else if (s.StartsWith("!")) c._terms.Add(new Term { Kind = Kind.NotFlag, Name = s.Substring(1).Trim() });
                    else c._terms.Add(new Term { Kind = Kind.Flag, Name = s });
                }
                return c;
            }

            /// <summary>
            /// 지금 플래그와 수치로 조건을 계산한다.
            /// Flags와 Values를 사용하며, 모든 항이 참이면 true를 반환한다.
            /// </summary>
            public bool Evaluate()
            {
                foreach (Term t in _terms)
                {
                    switch (t.Kind)
                    {
                        case Kind.Flag: if (!IsOn(t.Name)) return false; break;
                        case Kind.NotFlag: if (IsOn(t.Name)) return false; break;
                        default:
                            float? v = Values?.Invoke(t.Name);
                            if (v == null || !Compare(v.Value, t.Op, t.Number)) return false;
                            break;
                    }
                }
                return true;
            }

            private static bool Compare(float a, string op, float b)
            {
                const float eps = 1e-4f;
                switch (op)
                {
                    case ">=": return a >= b - eps;
                    case "<=": return a <= b + eps;
                    case ">": return a > b + eps;
                    case "<": return a < b - eps;
                    case "==": return Mathf.Abs(a - b) <= eps;
                    default: return Mathf.Abs(a - b) > eps;
                }
            }
        }
    }
}
