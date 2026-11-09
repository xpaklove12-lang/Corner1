using System.Collections.Generic;

namespace EyeMaze.EditorTools
{
    /// <summary>대사 편집기가 다루는 CSV 종류</summary>
    public enum DialogueEditorKind
    {
        /// <summary>인물·회차 CSV (Data/Dialogue/인물_DayN.csv). talk·dialogue·ending 행을 함께 가진다.</summary>
        Day,
        /// <summary>공용 문구 표 (Data/Strings.csv)</summary>
        Strings,
    }

    /// <summary>
    /// 대사 편집기 전용 공통 모델. 한 CSV 파일의 그룹 순서, 그룹 안 행 순서, 알려진 열과 memo 값을 담는다.
    /// 인물·회차 CSV에서는 (section, actor, id)가 그룹 하나다. Talk의 대화, Dialogue의 장면, Ending의 엔딩이 그룹이 되고,
    /// 그 아래 CSV 행 하나가 항목 하나가 된다. Strings에서는 key가 그룹이고 section과 actor는 비어 있다.
    /// 칸 값은 CSV에 적힌 글 그대로(앞뒤 공백만 뺀 값) 보관한다.
    /// </summary>
    public class DialogueEditorDocument
    {
        /// <summary>그룹 하나 (Talk 대화, Dialogue 장면, Ending 엔딩, Strings key)</summary>
        public class Group
        {
            public string Section = "";
            public string Actor = "";
            public string Id = "";
            /// <summary>그룹에 한 번만 적는 칸 (situation, back, need, set, title 등)</summary>
            public Dictionary<string, string> Fields = new Dictionary<string, string>();
            public List<Item> Items = new List<Item>();
            /// <summary>텍스트 뷰에서 이 그룹의 라벨 줄 번호 (1부터, 모르면 0)</summary>
            public int TextLine;

            /// <summary>
            /// 칸 값을 꺼낸다.
            /// column을 사용하며, 값 또는 빈 문자열을 반환한다.
            /// </summary>
            public string Get(string column) => Fields.TryGetValue(column, out string v) ? v : "";

            /// <summary>
            /// 같은 section·actor의 그룹인지 본다.
            /// other를 사용하며, 같으면 true를 반환한다.
            /// </summary>
            public bool SameStage(Group other) => other != null && other.Section == Section && other.Actor == Actor;
        }

        /// <summary>항목 하나 (CSV 한 행: Dialogue 선택지, Talk·Ending·Strings 대사 한 줄)</summary>
        public class Item
        {
            public Dictionary<string, string> Fields = new Dictionary<string, string>();
            /// <summary>텍스트 뷰에서 이 항목이 시작하는 줄 번호 (1부터, 모르면 0)</summary>
            public int TextLine;

            /// <summary>
            /// 칸 값을 꺼낸다.
            /// column을 사용하며, 값 또는 빈 문자열을 반환한다.
            /// </summary>
            public string Get(string column) => Fields.TryGetValue(column, out string v) ? v : "";
        }

        public DialogueEditorKind Kind;
        public string AssetPath = "";
        public readonly List<Group> Groups = new List<Group>();

        /// <summary>
        /// section·actor·id로 그룹을 찾는다.
        /// section, actor, id를 사용하며, 찾은 그룹 또는 null을 반환한다.
        /// </summary>
        public Group Find(string section, string actor, string id)
        {
            foreach (Group g in Groups) if (g.Section == section && g.Actor == actor && g.Id == id) return g;
            return null;
        }

        /// <summary>
        /// 그룹의 순서 번호를 찾는다.
        /// section, actor, id를 사용하며, 번호 또는 -1을 반환한다.
        /// </summary>
        public int IndexOf(string section, string actor, string id)
        {
            for (int i = 0; i < Groups.Count; i++)
                if (Groups[i].Section == section && Groups[i].Actor == actor && Groups[i].Id == id) return i;
            return -1;
        }

        /// <summary>
        /// 모델을 통째로 복사한다. 노드 뷰 편집을 검사하기 전에 원본을 건드리지 않으려고 쓴다.
        /// 입력값은 없으며, 새 문서를 반환한다.
        /// </summary>
        public DialogueEditorDocument Clone()
        {
            var d = new DialogueEditorDocument { Kind = Kind, AssetPath = AssetPath };
            foreach (Group g in Groups)
            {
                var ng = new Group { Section = g.Section, Actor = g.Actor, Id = g.Id, Fields = new Dictionary<string, string>(g.Fields), TextLine = g.TextLine };
                foreach (Item i in g.Items) ng.Items.Add(new Item { Fields = new Dictionary<string, string>(i.Fields), TextLine = i.TextLine });
                d.Groups.Add(ng);
            }
            return d;
        }
    }
}
