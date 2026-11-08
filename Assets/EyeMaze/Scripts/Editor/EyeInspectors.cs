using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>디렉터 인스펙터: Play 없이 시선, 이동, 대화, 엔딩을 진행하는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(EyeMazeDirector))]
    public class EyeMazeDirectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var d = (EyeMazeDirector)target;
            Person p = d.Person;
            Object[] roots = EyeEditorOps.Roots(d);

            EditorGUILayout.LabelField("Play 없이 바로 플레이", EditorStyles.boldLabel);
            if (p != null)
                EditorGUILayout.LabelField($"모드 {d.CurrentMode} · 신뢰 {p.Trust} · 동공 {p.Dilation * 100f:0}% · 태엽 {(d.Intro != null ? d.Intro.Wound : 0f):0.0} / {(d.Intro != null ? d.Intro.Capacity : 0)}", EditorStyles.miniBoldLabel);

            DialogueNode node = d.CurrentNode;
            if (d.CurrentMode == EyeMazeDirector.Mode.Talk && node != null) GUILayout.Label(node.Situation, EyeEditorOps.Box);
            if (!string.IsNullOrEmpty(d.Message)) GUILayout.Label("<i>" + d.Message + "</i>", EyeEditorOps.Box);

            // 시선
            using (new EditorGUILayout.HorizontalScope())
            {
                string g = d.CurrentMode == EyeMazeDirector.Mode.Gaze ? "시선 거두기" : "눈을 들여다보기";
                if (GUILayout.Button(g, GUILayout.Height(26))) EyeEditorOps.Run("들여다보기", d.ToggleGaze, roots);
                if (GUILayout.Button("깜빡임", GUILayout.Height(26), GUILayout.Width(80))) EyeEditorOps.Run("깜빡임", d.Blink, roots);
            }

            // 이동 패드
            GUI.enabled = d.CurrentMode == EyeMazeDirector.Mode.Gaze;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↑", GUILayout.Width(60))) EyeEditorOps.Run("이동", () => d.Nudge(Vector2.up), roots);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("←", GUILayout.Width(60))) EyeEditorOps.Run("이동", () => d.Nudge(Vector2.left), roots);
                if (GUILayout.Button("↓", GUILayout.Width(60))) EyeEditorOps.Run("이동", () => d.Nudge(Vector2.down), roots);
                if (GUILayout.Button("→", GUILayout.Width(60))) EyeEditorOps.Run("이동", () => d.Nudge(Vector2.right), roots);
                GUILayout.FlexibleSpace();
            }
            if (d.Cursor != null)
                EditorGUILayout.LabelField($"시선 위치: 고리 {d.Cursor.Ring} · 칸 {d.Cursor.Sector}  (씬 뷰에서 시선 오브젝트를 직접 끌어도 된다)", EditorStyles.miniLabel);
            GUI.enabled = true;

            // 대화
            if (d.CurrentMode == EyeMazeDirector.Mode.Talk && node != null)
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < node.Options.Count; i++)
                {
                    DialogueOption o = node.Options[i];
                    int idx = i;
                    string need = o.Requires != null && !o.Requires.Collected ? o.Requires.Title : (o.RequiresClue != null ? o.RequiresClue.DisplayName : "");
                    string label = d.IsAvailable(o) ? $"{i + 1}. {o.Text}" : $"{i + 1}. ??? ({need} 필요)";
                    if (GUILayout.Button(label)) EyeEditorOps.Run("대화 선택", () => d.ChooseOption(idx), roots);
                }
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("처음부터")) EyeEditorOps.Run("리셋", d.ResetAll, roots);
                if (GUILayout.Button("태엽 가득") && d.Intro != null) EyeEditorOps.Run("태엽 가득", d.Intro.FillUp, d.Intro);
                if (GUILayout.Button("시선 커서 선택") && d.Cursor != null) Selection.activeGameObject = d.Cursor.gameObject;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("반짝이는 파편에 닿기")) EyeEditorOps.Run("파편에 닿기", d.TouchCore, roots);
                using (new EditorGUI.DisabledScope(d.CurrentMode != EyeMazeDirector.Mode.Ending || d.HeartFinished))
                    if (GUILayout.Button("마음 다음 줄 ▶")) EyeEditorOps.Run("마음 다음 줄", d.NextHeartLine, roots);
            }
            if (d.Endings != null && d.Endings.Count > 0)
            {
                EditorGUILayout.LabelField("엔딩 바로 보기", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                    foreach (EndingDef e in d.Endings)
                    {
                        if (e == null) continue;
                        string id = e.Id;
                        if (GUILayout.Button(e.Title.Replace("엔딩 · ", ""))) EyeEditorOps.Run("엔딩 보기", () => d.StartEnding(id, null), roots);
                    }
            }
            EditorGUILayout.HelpBox("모든 버튼은 Ctrl+Z로 되돌릴 수 있다. 에디트 모드에서는 시간이 흐르지 않으니, 이동 한 번마다 태엽이 반 칸씩 풀린다.", MessageType.None);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    /// <summary>미로 인스펙터: 벽을 다시 만드는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(IrisMaze))]
    public class IrisMazeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var m = (IrisMaze)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("미로 다시 만들기 (현재 seed)")) Regenerate(m, m.Seed);
                if (GUILayout.Button("새 seed로 만들기")) Regenerate(m, Random.Range(1, 99999));
            }
            EditorGUILayout.HelpBox("벽은 '층 → 벽 (Walls)' 아래 GameObject다. 벽 오브젝트를 끄면 길이 되고, 켜면 막힌다.\n다시 만들어도 기억 조각과 층 설정은 유지된다. rings, sectors, ringsPerLayer를 바꿨다면 다시 만든다.", MessageType.None);
        }

        /// <summary>
        /// 미로 벽을 Undo 가능한 조작으로 다시 만든다.
        /// m, seed를 사용하며, 미로의 벽 오브젝트를 바꾼다.
        /// </summary>
        private static void Regenerate(IrisMaze m, int seed)
        {
            EyeEditorOps.Run("미로 생성", () => m.Generate(seed), m.gameObject);
        }
    }

    /// <summary>층 인스펙터: 한 칸씩 돌리는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(MazeLayer))]
    public class MazeLayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var l = (MazeLayer)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ 한 칸 돌리기")) EyeEditorOps.Run("층 회전", () => l.Offset++, l);
                if (GUILayout.Button("한 칸 돌리기 ▶")) EyeEditorOps.Run("층 회전", () => l.Offset--, l);
            }
        }
    }

    /// <summary>기억 파편 인스펙터: 시선 커서를 이 파편 위치로 옮기는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(MemoryFragment))]
    public class MemoryFragmentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var f = (MemoryFragment)target;
            EyeMazeDirector d = Object.FindAnyObjectByType<EyeMazeDirector>();
            if (d == null || d.Cursor == null) return;
            if (GUILayout.Button("시선 커서를 이 조각 위치로"))
                EyeEditorOps.Run("커서 이동", () => d.Cursor.SnapTo(f.Ring, f.WorldSector), d.Cursor.gameObject);
        }
    }

    /// <summary>태엽 인스펙터: 감기, 가득, 다 풀기 버튼을 보여준다.</summary>
    [CustomEditor(typeof(WindingIntro))]
    public class WindingIntroEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var w = (WindingIntro)target;
            var roots = new Object[] { w, w.Maze, w.Eye, w.Director, w.Director != null ? w.Director.Person : null };
            EditorGUILayout.LabelField($"태엽 {w.Wound:0.0} / {w.Capacity}   (긴장 {w.Strain * 100f:0}%)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("한 칸 감기")) EyeEditorOps.Run("태엽 감기", () => w.Wind(1f), roots);
                if (GUILayout.Button("가득")) EyeEditorOps.Run("태엽 가득", w.FillUp, roots);
                if (GUILayout.Button("다 풀기")) EyeEditorOps.Run("태엽 풀기", () => { w.Wound = 0f; w.Strain = 0f; }, roots);
            }
            EditorGUILayout.HelpBox("태엽 = 눈을 들여다볼 수 있는 시간이다.\n" +
                "· 대화 중에 감고, 들여다보는 동안 풀린다 (unwindPerSecond).\n" +
                "· 가득 찬 뒤 더 감으면 긴장이 쌓이다가 미끄러져 다 풀린다.\n" +
                "· 용량 = baseCapacity + 신뢰 × capacityPerTrust\n" +
                "· ticksPerBlink칸 풀릴 때마다 깜빡이고 2층이 돈다.", MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
