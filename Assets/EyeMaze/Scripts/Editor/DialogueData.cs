using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>
    /// 기획 데이터(Data/Flags.csv, Data/Dialogue/인물_DayN.csv)를 읽고 서로 맞는지 검사한다.
    /// 인물·회차 CSV 하나에 section=talk(필드 대사), dialogue(상담), ending(엔딩·문구) 행이 함께 있다.
    /// CSV를 저장하면 자동으로 다시 읽고 검사해서 Console에 알려준다.
    /// </summary>
    public static class DialogueData
    {
        public const string DataFolder = "Assets/EyeMaze/Data";
        public const string FlagsPath = DataFolder + "/Flags.csv";
        public const string DialogueFolder = DataFolder + "/Dialogue";
        // 스펙 016: 회상 대본 경로와 권장 최대 줄 수 추가
        // 스펙 016: 상수 이름 FlashbacksPath → FLASHBACKS_PATH
        public const string FLASHBACKS_PATH = DataFolder + "/Flashbacks.csv";
        private const int FLASHBACK_MAX_LINES = 12;
        // 스펙 017: 이어지는 쌍 경로 추가
        public const string LINKS_PATH = DataFolder + "/Links.csv";

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
        /// Data/Dialogue 폴더의 인물·회차 CSV 에셋을 모두 찾는다.
        /// 입력값은 없으며, 에셋 목록을 반환한다.
        /// </summary>
        public static List<TextAsset> LoadDayAssets()
        {
            var list = new List<TextAsset>();
            if (!AssetDatabase.IsValidFolder(DialogueFolder)) return list;
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { DialogueFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        /// <summary>
        /// 인물·회차 CSV의 상담 대화(section=dialogue)를 모두 불러온다.
        /// 입력값은 없으며, (에셋, 읽은 대화) 목록을 반환한다.
        /// </summary>
        public static List<(TextAsset asset, DialogueScript script)> LoadDialogues()
        {
            var list = new List<(TextAsset, DialogueScript)>();
            foreach (TextAsset asset in LoadDayAssets()) list.Add((asset, DialogueScript.Parse(asset)));
            return list;
        }

        /// <summary>
        /// CSV의 talk 행에 나오는 actor ID를 처음 나온 순서로 모은다.
        /// asset을 사용하며, actor ID 목록을 반환한다.
        /// </summary>
        public static List<string> TalkActors(TextAsset asset)
        {
            var actors = new List<string>();
            foreach (Csv.Row r in Csv.Section(Csv.Read(asset, new List<string>()), Csv.SECTION_TALK))
                if (r[Csv.ACTOR].Length > 0 && !actors.Contains(r[Csv.ACTOR])) actors.Add(r[Csv.ACTOR]);
            return actors;
        }

        /// <summary>
        /// 인물·회차 CSV의 필드 대사(section=talk)를 actor마다 불러온다.
        /// 입력값은 없으며, (에셋, actor, 읽은 대사) 목록을 반환한다.
        /// </summary>
        public static List<(TextAsset asset, string actor, TalkScript script)> LoadTalks()
        {
            var list = new List<(TextAsset, string, TalkScript)>();
            foreach (TextAsset asset in LoadDayAssets())
                foreach (string actor in TalkActors(asset)) list.Add((asset, actor, TalkScript.Parse(asset, actor)));
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
                        foreach (string f in c.Need.Concat(c.EndingNeed)) Add(users, f, $"{asset.name}.csv {c.Line}행", asset);
                    }
            foreach ((TextAsset asset, string actor, TalkScript t) in LoadTalks())
                foreach (TalkScript.Group g in t.Groups)
                {
                    foreach (string f in g.Set) Add(setters, f, $"{asset.name}.csv {actor} {g.Row}행", asset);
                    foreach (string f in g.Need) Add(users, f, $"{asset.name}.csv {actor} {g.Row}행", asset);
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
                        foreach (string f in c.Need.Concat(c.Set).Concat(c.EndingNeed))
                            if (!known.Contains(f)) Err($"{asset.name}.csv {c.Line}행 플래그 '{f}'가 Flags.csv에 없다.", asset);
                        foreach (string f in c.Set)
                            if (computed.Contains(f)) Err($"{asset.name}.csv {c.Line}행 '{f}'는 계산 플래그라 set으로 켤 수 없다.", asset);
                        foreach (string f in c.Need)
                            if (known.Contains(f) && !computed.Contains(f) && !setters.ContainsKey(f)) Warn($"{asset.name}.csv {c.Line}행 플래그 '{f}'를 켜는 곳(파편/단서/set)이 없다. 이 선택지는 열리지 않는다.", asset);
                        if (c.Ending.Length > 0 && endings != null && !endings.Contains(c.Ending))
                            Warn($"{asset.name}.csv {c.Line}행 엔딩 '{c.Ending}'이 손님의 결말 목록에 없다.", asset);
                    }
            }

            // 엔딩(section=ending): 문법 검사 + 어느 손님에도 연결되지 않은 파일
            var cases = Object.FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var usedCsv = new HashSet<TextAsset>(cases.Where(c => c.DialogueCsv != null).Select(c => c.DialogueCsv));
            foreach (TextAsset asset in LoadDayAssets())
            {
                foreach (string e in EndingScript.Parse(asset).Errors) Err($"{asset.name}.csv ending: {e}", asset);
                if (!usedCsv.Contains(asset)) Warn($"{asset.name}.csv가 어느 손님(ClientCase)에도 연결되지 않았다.", asset);
            }
            foreach (ClientCase c in cases)
            {
                if (c.DialogueCsv == null) continue;
                var ids = new HashSet<string>(c.Endings.Select(e => e.Id));
                foreach (string must in new[] { "truth", "half", "leave" })
                    if (!ids.Contains(must)) Warn($"{c.DialogueCsv.name}.csv에 '{must}' 엔딩이 없다 (게임이 직접 부르는 엔딩).", c.DialogueCsv);
                // 엔딩 판정의 열쇠 파편은 이 손님의 파편 중 Flag가 하나뿐인 것이어야 한다.
                c.FindKeyFragment(out string keyError);
                if (keyError.Length > 0) Err($"{c.DialogueCsv.name}.csv: {keyError}", c.DialogueCsv);
            }

            foreach ((TextAsset asset, string actor, TalkScript t) in LoadTalks())
            {
                string where = $"{asset.name}.csv {actor}";
                foreach (string e in t.Errors) Err($"{where}: {e}", asset);
                foreach (TalkScript.Group g in t.Groups)
                {
                    foreach (string f in g.Need.Concat(g.Set))
                        if (!known.Contains(f)) Err($"{where} {g.Row}행 플래그 '{f}'가 Flags.csv에 없다.", asset);
                    foreach (string f in g.Set)
                        if (computed.Contains(f)) Err($"{where} {g.Row}행 '{f}'는 계산 플래그라 set으로 켤 수 없다.", asset);
                    foreach (string f in g.Need)
                        if (known.Contains(f) && !computed.Contains(f) && !setters.ContainsKey(f)) Warn($"{where} {g.Row}행 플래그 '{f}'를 켜는 곳이 없다. 이 대화는 나오지 않는다.", asset);
                }
            }

            // 필드 NPC: 회차 CSV를 쓰면 그 안에서 자기 talk 행을 고를 actor ID가 있어야 한다. talk 행이 없는 actor는 인스펙터 대사를 쓴다.
            foreach (FieldNPC n in Object.FindObjectsByType<FieldNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (n.TalkCsv == null) continue;
                if (string.IsNullOrEmpty(n.DialogueActorId)) Err($"필드 NPC '{n.name}'에 actor ID가 없다. {n.TalkCsv.name}.csv에서 대사를 고를 수 없다.", n);
            }

            foreach (MemoryFragment f in Object.FindObjectsByType<MemoryFragment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(f.Flag)) Warn($"파편 '{f.Title}'에 플래그가 없다.", f);
                else if (!known.Contains(f.Flag)) Err($"파편 '{f.Title}'의 플래그 '{f.Flag}'가 Flags.csv에 없다.", f);
            }
            // 스펙 015: 막음 사물과 같은 칸에 놓인 파편을 오류로 알린다
            foreach (ClientCase c in cases)
            {
                List<MemoryFragment> fragments = c.Fragments();
                foreach (MazeProp p in c.Props())
                {
                    if (p.Kind != MazePropKind.Block) continue;
                    foreach (MemoryFragment f in fragments)
                        if (f.Layer == p.Layer && f.Ring == p.Ring && f.Sector == p.Sector)
                            Err($"손님 '{c.PersonName}'의 막음 사물 '{p.name}'이 파편 '{f.Title}'과 같은 칸(ring {p.Ring}, sector {p.Sector})에 있다.", p);
                }
            }
            foreach (FieldClue c in Object.FindObjectsByType<FieldClue>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(c.Flag)) Warn($"단서 '{c.DisplayName}'에 플래그가 없다.", c);
                else if (!known.Contains(c.Flag)) Err($"단서 '{c.DisplayName}'의 플래그 '{c.Flag}'가 Flags.csv에 없다.", c);
            }
            // 스펙 016: 회상 대본의 길이, 플래그, 사물 id를 검사한다
            ValidateFlashbacks(known, Err, Warn);
            // 스펙 017: 이어지는 쌍의 플래그, 기억의 자리, 연결 회상 대본, 재사용할 방을 검사한다
            ValidateLinks(known, Err);
            return issues;
        }

        /// <summary>
        /// 스펙 016: 회상 대본(Flashbacks.csv)을 검사한다. 대본 줄이 12줄을 넘으면 경고, flag가 Flags.csv에 없거나 prop이 그 방에 없으면 오류로 알린다.
        /// 방은 FlashbackPlayer.FindRoom과 같은 규칙으로 실제 재생될 방 하나다. 원래 회상은 플래그가 같은 첫 방이고, 연결 회상은 Links.csv room의 첫 방(쌍이 없으면 그 이름의 추가 사물 묶음을 가진 첫 방)이다.
        /// 방이 없는 회상은 글로 나오므로 prop을 검사하지 않는다. 같은 플래그의 방이 둘 이상이면 경고한다.
        /// known(Flags.csv의 id), err, warn과 열린 씬의 회상 방, Links.csv를 사용하며, err와 warn으로 문제를 남긴다. 파일이 없으면 검사하지 않는다.
        /// </summary>
        private static void ValidateFlashbacks(HashSet<string> known, System.Action<string, Object> err, System.Action<string, Object> warn)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(FLASHBACKS_PATH);
            if (asset == null) return;
            var errors = new List<string>();
            Dictionary<string, FlashbackScript.Flashback> flashbacks = FlashbackScript.Parse(asset, errors);
            foreach (string e in errors) err("Flashbacks.csv: " + e, asset);
            // 스펙 016: 후보 방을 합치지 않고 FindRoom처럼 실제 재생될 방 하나만 본다
            FlashbackRoom[] rooms = PlayerRooms();
            foreach (IGrouping<string, FlashbackRoom> g in rooms.GroupBy(r => r.Flag).Where(g => g.Count() > 1))
                warn($"회상 방 플래그 '{g.Key}'를 쓰는 방이 {g.Count()}개다. 회상 재생기 목록의 첫 방 '{g.First().name}'만 재생된다.", g.First());
            var linksAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LINKS_PATH);
            List<MemoryLinkScript.Link> links = linksAsset != null ? MemoryLinkScript.Parse(linksAsset, new List<string>()) : new List<MemoryLinkScript.Link>();
            foreach (FlashbackScript.Flashback f in flashbacks.Values)
            {
                if (f.LineCount > FLASHBACK_MAX_LINES)
                    warn($"Flashbacks.csv {f.Row}행 회상 '{f.Flag}'의 대본 줄이 {f.LineCount}줄이다. {FLASHBACK_MAX_LINES}줄 이하를 권장한다.", asset);
                if (!known.Contains(f.Flag)) err($"Flashbacks.csv {f.Row}행 회상 플래그 '{f.Flag}'가 Flags.csv에 없다.", asset);

                FlashbackRoom room = FirstRoom(rooms, f.Flag);
                GameObject extra = null;
                if (room == null)
                {
                    // 연결 회상은 재사용하는 방의 기본 사물과 그 이름의 추가 사물 묶음을 쓴다.
                    MemoryLinkScript.Link link = links.FirstOrDefault(l => l.Result == f.Flag);
                    room = link != null ? FirstRoom(rooms, link.Room) : rooms.FirstOrDefault(r => r.ExtraSets.Any(g => g != null && g.name == f.Flag));
                    if (room != null) extra = room.ExtraSets.FirstOrDefault(g => g != null && g.name == f.Flag);
                }
                if (room == null) continue;
                var propIds = new HashSet<string>();
                foreach (FlashbackProp p in room.GetComponentsInChildren<FlashbackProp>(true))
                    if (InSet(p, extra) || !InAnyExtraSet(p, room)) propIds.Add(p.Id);
                foreach (FlashbackScript.Step s in f.Steps)
                    if (!propIds.Contains(s.Prop)) err($"Flashbacks.csv {s.Row}행 prop '{s.Prop}'가 회상 '{f.Flag}'의 방에 없다.", asset);
            }
        }

        /// <summary>
        /// 스펙 016: 회상 재생기가 쓰는 회상 방 목록을 가져온다. 씬에 회상 재생기가 없으면 열린 씬의 모든 회상 방을 쓴다.
        /// 열린 씬의 FlashbackPlayer와 FlashbackRoom을 사용하며, 빈 칸을 뺀 방 목록을 반환한다.
        /// </summary>
        private static FlashbackRoom[] PlayerRooms()
        {
            FlashbackPlayer player = Object.FindObjectsByType<FlashbackPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            FlashbackRoom[] rooms = player != null ? player.Rooms : Object.FindObjectsByType<FlashbackRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return rooms.Where(r => r != null).ToArray();
        }

        /// <summary>
        /// 스펙 016: FlashbackPlayer.FindRoom과 같은 규칙으로 플래그가 같은 첫 방을 찾는다.
        /// rooms와 flag를 사용하며, 찾은 방 또는 null을 반환한다.
        /// </summary>
        private static FlashbackRoom FirstRoom(FlashbackRoom[] rooms, string flag)
        {
            if (string.IsNullOrEmpty(flag)) return null;
            return rooms.FirstOrDefault(r => r.Flag == flag);
        }

        /// <summary>
        /// 스펙 017: 이어지는 쌍(Links.csv)을 검사한다. a와 b가 같음, a, b, result가 Flags.csv에 없음, a나 b의 회상 대본이나 회상 방이 없어 기억의 자리가 될 수 없음,
        /// 연결 회상 대본 없음, room이 a도 b도 아님, room의 회상 방 없음을 오류로 알린다.
        /// known(Flags.csv의 id), err, Flashbacks.csv, 회상 재생기의 방 목록을 사용하며, err로 문제를 남긴다. 파일이 없으면 검사하지 않는다.
        /// </summary>
        private static void ValidateLinks(HashSet<string> known, System.Action<string, Object> err)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LINKS_PATH);
            if (asset == null) return;
            var errors = new List<string>();
            List<MemoryLinkScript.Link> links = MemoryLinkScript.Parse(asset, errors);
            foreach (string e in errors) err("Links.csv: " + e, asset);
            var flashbackAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(FLASHBACKS_PATH);
            Dictionary<string, FlashbackScript.Flashback> flashbacks = flashbackAsset != null
                ? FlashbackScript.Parse(flashbackAsset, new List<string>())
                : new Dictionary<string, FlashbackScript.Flashback>();
            FlashbackRoom[] rooms = PlayerRooms();
            foreach (MemoryLinkScript.Link l in links)
            {
                string where = $"Links.csv {l.Row}행";
                if (l.A == l.B) err($"{where} a와 b가 같은 플래그 '{l.A}'다.", asset);
                foreach (string f in new[] { l.A, l.B, l.Result })
                    if (!known.Contains(f)) err($"{where} 플래그 '{f}'가 Flags.csv에 없다.", asset);
                foreach (string f in new[] { l.A, l.B }.Distinct())
                    if (!flashbacks.ContainsKey(f) || FirstRoom(rooms, f) == null)
                        err($"{where} '{f}'의 회상 대본이나 회상 방이 없어 기억의 자리가 될 수 없다. 이 쌍은 이을 수 없다.", asset);
                if (!flashbacks.ContainsKey(l.Result)) err($"{where} 연결 회상 '{l.Result}'의 대본이 Flashbacks.csv에 없다.", asset);
                if (l.Room != l.A && l.Room != l.B) err($"{where} room '{l.Room}'이 a도 b도 아니다.", asset);
                else if (FirstRoom(rooms, l.Room) == null) err($"{where} room '{l.Room}'의 회상 방이 없다.", asset);
            }
            // 수정(017): 같은 result를 여러 쌍이 쓰면 한 쌍만 이어도 나머지가 이은 것으로 처리되므로 오류로 알리게 했다.
            foreach (IGrouping<string, MemoryLinkScript.Link> g in links.GroupBy(l => l.Result).Where(g => g.Count() > 1))
                err($"Links.csv {string.Join(", ", g.Select(l => l.Row + "행"))} 쌍들이 같은 result '{g.Key}'를 쓴다. 쌍마다 다른 result를 쓴다.", asset);
        }

        /// <summary>
        /// 스펙 016: 사물이 추가 사물 묶음 set 아래에 있는지 확인한다.
        /// p와 set을 사용하며, set이 있고 p가 그 아래에 있으면 true를 반환한다.
        /// </summary>
        private static bool InSet(FlashbackProp p, GameObject set) => set != null && p.transform.IsChildOf(set.transform);

        /// <summary>
        /// 스펙 016: 사물이 방의 추가 사물 묶음 중 하나에 속하는지 확인한다.
        /// p와 room을 사용하며, 어느 추가 사물 묶음 아래에 있으면 true를 반환한다.
        /// </summary>
        private static bool InAnyExtraSet(FlashbackProp p, FlashbackRoom room) => room.ExtraSets.Any(g => InSet(p, g));

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
            string path = EditorUtility.SaveFilePanelInProject("새 대화 CSV", "NewPerson_Day1", "csv", "인물 이름(영문)_Day회차로 저장", DialogueFolder);
            if (string.IsNullOrEmpty(path)) return;
            // 파일 이름의 _DayN 앞부분을 상담 인물 actor ID로 쓴다.
            string actor = Path.GetFileNameWithoutExtension(path);
            int day = actor.LastIndexOf("_Day", System.StringComparison.Ordinal);
            if (day > 0) actor = actor.Substring(0, day);
            // 스펙 015: 새 파일 머리글에 emotion 추가
            var columns = new List<string> { "section", "actor", "id", "situation", "text", "response", "need", "set", "trust", "dilation", "spring", "goto", "ending", "back", "title", "after", "speaker", "memo", "truth_trust", "key_flag", "leave_trust", "burden_limit", "rest", "ending_need", "emotion" };
            var sb = new System.Text.StringBuilder(string.Join(",", columns)).Append("\r\n");
            void Row(string section, string id, params (string column, string value)[] cells)
            {
                var row = new string[columns.Count];
                row[0] = section; row[1] = actor; row[2] = id;
                foreach ((string column, string value) in cells) row[columns.IndexOf(column)] = value;
                sb.Append(string.Join(",", row)).Append("\r\n");
            }
            Row("talk", "enter", ("text", "들어와 앉은 뒤 첫마디"));
            Row("dialogue", "start", ("situation", "처음 장면 상황 설명"), ("text", "첫 번째 질문"), ("response", "상대의 반응"), ("trust", "0"), ("dilation", "0"), ("spring", "0"));
            Row("dialogue", "start", ("text", "플래그가 있어야 열리는 질문"), ("response", "반응"), ("need", "flag_id"), ("trust", "1"), ("dilation", "0.1"), ("spring", "0"), ("goto", "next"));
            Row("dialogue", "next", ("situation", "다음 장면 상황 (뒤로가기가 자동으로 붙음)"), ("text", "질문"), ("response", "반응"), ("trust", "0"), ("dilation", "0"), ("spring", "0"));
            Row("ending", "@intro", ("text", "눈 속에 들어왔을 때 첫 문구"));
            // truth·leave 첫 줄에는 엔딩 판정 기준을 함께 적는다. 열쇠 파편(key_flag)은 비워 두면 신뢰만 본다.
            Row("ending", "truth", ("title", "엔딩 · truth"), ("situation", "엔딩 상황"), ("after", "필드로 돌아온 뒤 말을 걸면 나오는 말"), ("text", "엔딩 대사"), ("truth_trust", "3"));
            Row("ending", "half", ("title", "엔딩 · half"), ("situation", "엔딩 상황"), ("after", "필드로 돌아온 뒤 말을 걸면 나오는 말"), ("text", "엔딩 대사"));
            Row("ending", "leave", ("title", "엔딩 · leave"), ("situation", "엔딩 상황"), ("after", "필드로 돌아온 뒤 말을 걸면 나오는 말"), ("text", "엔딩 대사"), ("leave_trust", "-2"), ("burden_limit", "1"));
            File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));
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
            // 스펙 016: 회상 대본도 다시 읽는다
            FlashbackScript.Reload();
            // 스펙 017: 이어지는 쌍도 다시 읽는다
            MemoryLinkScript.Reload();
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
