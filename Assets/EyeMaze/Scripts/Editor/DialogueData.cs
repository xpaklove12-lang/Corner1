using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 기획 데이터(Data/Flags.csv, Data/Dialogue/*.csv)를 읽고 서로 맞는지 검사한다.
    /// CSV를 저장하면 자동으로 다시 읽고 검사해서 Console에 알려준다.
    /// </summary>
    public static class DialogueData
    {
        public const string DataFolder = "Assets/EyeMaze/Data";
        public const string FlagsPath = DataFolder + "/Flags.csv";
        public const string DialogueFolder = DataFolder + "/Dialogue";
        public const string TalkFolder = DataFolder + "/Talk";
        public const string EndingFolder = DataFolder + "/Ending";

        /// <summary>Flags.csv 한 줄</summary>
        public class FlagDef
        {
            public string Id;
            public string Description;
            public string Source;
            /// <summary>계산 플래그 조건 (비어 있으면 직접 켜는 플래그)</summary>
            public string Condition;
            public int Line;
        }

        /// <summary>플래그를 켜거나 쓰는 곳 하나 (관리자 창에 보여주고 누르면 그곳으로 간다)</summary>
        public class Ref
        {
            public string Label;
            public Object Target;
        }

        /// <summary>검사 결과 한 줄</summary>
        public class Issue
        {
            public bool Error;
            public string Text;
            public Object Context;
        }

        /// <summary>
        /// Flags.csv를 읽는다.
        /// errors를 사용하며, 플래그 정의 목록을 반환한다.
        /// </summary>
        public static List<FlagDef> LoadFlags(List<string> errors)
        {
            var list = new List<FlagDef>();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FlagsPath);
            if (asset == null) { errors.Add($"{FlagsPath} 파일이 없다."); return list; }
            foreach (Csv.Row r in Csv.Read(asset, errors))
                list.Add(new FlagDef { Id = r["id"], Description = r["description"], Source = r["source"], Condition = r["condition"], Line = r.Line });
            return list;
        }

        /// <summary>
        /// Data/Dialogue 폴더의 대화 CSV를 모두 불러온다.
        /// 입력값은 없으며, (에셋, 읽은 대화) 목록을 반환한다.
        /// </summary>
        public static List<(TextAsset asset, DialogueScript script)> LoadDialogues()
        {
            var list = new List<(TextAsset, DialogueScript)>();
            if (!AssetDatabase.IsValidFolder(DialogueFolder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { DialogueFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) list.Add((asset, DialogueScript.Parse(asset)));
            }
            return list;
        }

        /// <summary>
        /// Data/Talk 폴더의 필드 대화 CSV를 모두 불러온다.
        /// 입력값은 없으며, (에셋, 읽은 대사) 목록을 반환한다.
        /// </summary>
        public static List<(TextAsset asset, TalkScript script)> LoadTalks()
        {
            var list = new List<(TextAsset, TalkScript)>();
            if (!AssetDatabase.IsValidFolder(TalkFolder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { TalkFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) list.Add((asset, TalkScript.Parse(asset)));
            }
            return list;
        }

        /// <summary>
        /// 플래그마다 켜는 곳(파편, 단서, 대화 set)과 쓰는 곳(대화 need)을 모은다.
        /// dialogues와 열린 씬의 파편/단서를 사용하며, setters와 users 사전을 채운다.
        /// </summary>
        public static void CollectRefs(List<(TextAsset asset, DialogueScript script)> dialogues,
            Dictionary<string, List<Ref>> setters, Dictionary<string, List<Ref>> users)
        {
            void Add(Dictionary<string, List<Ref>> d, string id, string label, Object target)
            {
                if (string.IsNullOrEmpty(id)) return;
                if (!d.TryGetValue(id, out List<Ref> l)) d[id] = l = new List<Ref>();
                l.Add(new Ref { Label = label, Target = target });
            }
            foreach (MemoryFragment f in Object.FindObjectsByType<MemoryFragment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(setters, f.Flag, $"파편 · {f.Title}", f);
            foreach (FieldClue c in Object.FindObjectsByType<FieldClue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(setters, c.Flag, $"단서 · {c.DisplayName}", c);
            foreach ((TextAsset asset, DialogueScript s) in dialogues)
                foreach (DialogueScript.Node n in s.Nodes)
                    foreach (DialogueScript.Choice c in n.Choices)
                    {
                        foreach (string f in c.Set) Add(setters, f, $"{asset.name}.csv {c.Line}행", asset);
                        foreach (string f in c.Need) Add(users, f, $"{asset.name}.csv {c.Line}행", asset);
                    }
            foreach ((TextAsset asset, TalkScript t) in LoadTalks())
                foreach (TalkScript.Group g in t.Groups)
                {
                    foreach (string f in g.Set) Add(setters, f, $"Talk/{asset.name}.csv {g.Row}행", asset);
                    foreach (string f in g.Need) Add(users, f, $"Talk/{asset.name}.csv {g.Row}행", asset);
                }
        }

        /// <summary>
        /// 기획 데이터 전체를 검사한다.
        /// Flags.csv, 대화 CSV, 열린 씬의 파편/단서/손님을 사용하며, 문제 목록을 반환한다.
        /// </summary>
        public static List<Issue> Validate()
        {
            var issues = new List<Issue>();
            var flagAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(FlagsPath);
            void Err(string t, Object c = null) => issues.Add(new Issue { Error = true, Text = t, Context = c });
            void Warn(string t, Object c = null) => issues.Add(new Issue { Error = false, Text = t, Context = c });

            var errors = new List<string>();
            List<FlagDef> defs = LoadFlags(errors);
            foreach (string e in errors) Err("Flags.csv: " + e, flagAsset);
            var known = new HashSet<string>();
            var computed = new HashSet<string>();
            foreach (FlagDef d in defs)
            {
                if (d.Id.Length == 0) { Err($"Flags.csv {d.Line}행 id가 비어 있다.", flagAsset); continue; }
                if (d.Id.IndexOfAny(new[] { ' ', '|', ',', '&', '!' }) >= 0) Err($"Flags.csv {d.Line}행 id '{d.Id}'에 공백, |, 쉼표, &, !를 쓸 수 없다.", flagAsset);
                if (!known.Add(d.Id)) Err($"Flags.csv {d.Line}행 id '{d.Id}'가 두 번 있다.", flagAsset);
                if (!string.IsNullOrEmpty(d.Condition)) computed.Add(d.Id);
            }
            // 계산 플래그 조건 검사: 문법, 모르는 플래그
            foreach (FlagDef d in defs)
            {
                if (string.IsNullOrEmpty(d.Condition)) continue;
                var condErrors = new List<string>();
                Flags.Condition c = Flags.Condition.Parse(d.Condition, condErrors, d.Line);
                foreach (string e in condErrors) Err("Flags.csv " + e, flagAsset);
                if (c == null) continue;
                foreach (string f in c.FlagNames)
                {
                    if (!known.Contains(f)) Err($"Flags.csv {d.Line}행 '{d.Id}'의 condition에 쓴 플래그 '{f}'가 목록에 없다.", flagAsset);
                    if (f == d.Id) Err($"Flags.csv {d.Line}행 '{d.Id}'의 condition이 자기 자신을 쓴다.", flagAsset);
                }
            }

            List<(TextAsset asset, DialogueScript script)> dialogues = LoadDialogues();
            var setters = new Dictionary<string, List<Ref>>();
            var users = new Dictionary<string, List<Ref>>();
            CollectRefs(dialogues, setters, users);

            // 손님별 엔딩 id (그 CSV를 쓰는 ClientCase 기준)
            var endingsByCsv = new Dictionary<TextAsset, HashSet<string>>();
            foreach (ClientCase c in Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.DialogueCsv == null) { Warn($"손님 '{c.PersonName}'에 대화 CSV가 연결되지 않았다.", c); continue; }
                endingsByCsv[c.DialogueCsv] = new HashSet<string>(c.Endings.Where(e => e != null).Select(e => e.Id));
            }

            foreach ((TextAsset asset, DialogueScript s) in dialogues)
            {
                foreach (string e in s.Errors) Err($"{asset.name}.csv: {e}", asset);
                endingsByCsv.TryGetValue(asset, out HashSet<string> endings);
                foreach (DialogueScript.Node n in s.Nodes)
                    foreach (DialogueScript.Choice c in n.Choices)
                    {
                        foreach (string f in c.Need.Concat(c.Set))
                            if (!known.Contains(f)) Err($"{asset.name}.csv {c.Line}행 플래그 '{f}'가 Flags.csv에 없다.", asset);
                        foreach (string f in c.Set)
                            if (computed.Contains(f)) Err($"{asset.name}.csv {c.Line}행 '{f}'는 계산 플래그라 set으로 켤 수 없다.", asset);
                        foreach (string f in c.Need)
                            if (known.Contains(f) && !computed.Contains(f) && !setters.ContainsKey(f)) Warn($"{asset.name}.csv {c.Line}행 플래그 '{f}'를 켜는 곳(파편/단서/set)이 없다. 이 선택지는 열리지 않는다.", asset);
                        if (c.Ending.Length > 0 && endings != null && !endings.Contains(c.Ending))
                            Warn($"{asset.name}.csv {c.Line}행 엔딩 '{c.Ending}'이 손님의 결말 목록에 없다.", asset);
                    }
            }

            // 엔딩 CSV: 문법 검사 + 어느 손님에도 연결되지 않은 파일
            var usedEndingCsv = new HashSet<TextAsset>(Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c.EndingCsv != null).Select(c => c.EndingCsv));
            if (AssetDatabase.IsValidFolder(EndingFolder))
                foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { EndingFolder }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null) continue;
                    foreach (string e in EndingScript.Parse(asset).Errors) Err($"Ending/{asset.name}.csv: {e}", asset);
                    if (!usedEndingCsv.Contains(asset)) Warn($"Ending/{asset.name}.csv가 어느 손님(ClientCase)에도 연결되지 않았다.", asset);
                }
            foreach (ClientCase c in Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.EndingCsv == null) continue;
                var ids = new HashSet<string>(c.Endings.Select(e => e.Id));
                foreach (string must in new[] { "truth", "half", "leave" })
                    if (!ids.Contains(must)) Warn($"Ending/{c.EndingCsv.name}.csv에 '{must}' 엔딩이 없다 (게임이 직접 부르는 엔딩).", c.EndingCsv);
            }

            foreach ((TextAsset asset, TalkScript t) in LoadTalks())
            {
                foreach (string e in t.Errors) Err($"Talk/{asset.name}.csv: {e}", asset);
                foreach (TalkScript.Group g in t.Groups)
                {
                    foreach (string f in g.Need.Concat(g.Set))
                        if (!known.Contains(f)) Err($"Talk/{asset.name}.csv {g.Row}행 플래그 '{f}'가 Flags.csv에 없다.", asset);
                    foreach (string f in g.Set)
                        if (computed.Contains(f)) Err($"Talk/{asset.name}.csv {g.Row}행 '{f}'는 계산 플래그라 set으로 켤 수 없다.", asset);
                    foreach (string f in g.Need)
                        if (known.Contains(f) && !computed.Contains(f) && !setters.ContainsKey(f)) Warn($"Talk/{asset.name}.csv {g.Row}행 플래그 '{f}'를 켜는 곳이 없다. 이 대화는 나오지 않는다.", asset);
                }
            }

            foreach (MemoryFragment f in Object.FindObjectsByType<MemoryFragment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(f.Flag)) Warn($"파편 '{f.Title}'에 플래그가 없다.", f);
                else if (!known.Contains(f.Flag)) Err($"파편 '{f.Title}'의 플래그 '{f.Flag}'가 Flags.csv에 없다.", f);
            }
            foreach (FieldClue c in Object.FindObjectsByType<FieldClue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(c.Flag)) Warn($"단서 '{c.DisplayName}'에 플래그가 없다.", c);
                else if (!known.Contains(c.Flag)) Err($"단서 '{c.DisplayName}'의 플래그 '{c.Flag}'가 Flags.csv에 없다.", c);
            }
            return issues;
        }

        /// <summary>
        /// 검사 결과를 Console에 보여준다.
        /// issues를 사용하며, 오류/경고 로그를 남긴다.
        /// </summary>
        public static void Log(List<Issue> issues)
        {
            foreach (Issue i in issues)
            {
                if (i.Error) Debug.LogError("[기획 데이터] " + i.Text, i.Context);
                else Debug.LogWarning("[기획 데이터] " + i.Text, i.Context);
            }
            if (issues.Count == 0) Debug.Log("[기획 데이터] 문제 없음");
        }

        [MenuItem("EyeMaze/기획 데이터 검사")]
        private static void ValidateMenu() => Log(Validate());

        [MenuItem("EyeMaze/새 대화 CSV 만들기")]
        private static void CreateDialogue()
        {
            string path = EditorUtility.SaveFilePanelInProject("새 대화 CSV", "NewPerson", "csv", "사람 이름(영문)으로 저장", DialogueFolder);
            if (string.IsNullOrEmpty(path)) return;
            const string template =
                "node,situation,text,response,need,set,trust,dilation,spring,goto,ending,back,memo\r\n" +
                "start,처음 장면 상황 설명,첫 번째 질문,상대의 반응,,,0,0,0,,,,\r\n" +
                "start,,플래그가 있어야 열리는 질문,반응,flag_id,,1,0.1,0,next,,,\r\n" +
                "next,다음 장면 상황 (뒤로가기가 자동으로 붙음),질문,반응,,,0,0,0,,,,\r\n";
            File.WriteAllText(path, template, new System.Text.UTF8Encoding(true));
            AssetDatabase.ImportAsset(path);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
    }

    /// <summary>Data 폴더의 CSV가 바뀌면 대화를 다시 읽고 검사한다.</summary>
    public class DialogueDataPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = imported.Concat(deleted).Concat(moved)
                .Any(p => p.StartsWith(DialogueData.DataFolder) && p.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase));
            if (!changed) return;
            DialogueScript.Invalidate();
            TalkScript.Invalidate();
            EndingScript.Invalidate();
            Flags.ReloadDefinitions();
            Strings.Reload();
            // 에셋 임포트 도중에는 씬 검색이 불안정하므로 한 프레임 뒤에 검사한다.
            EditorApplication.delayCall += () =>
            {
                DialogueData.Log(DialogueData.Validate().Where(i => i.Error).ToList());
                FlagManagerWindow.RepaintAll();
                EyeHooks.Repaint();
            };
        }
    }
}
