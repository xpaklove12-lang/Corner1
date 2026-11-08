using System.Linq;

using UnityEngine;

using UnityEditor;

namespace EyeMaze.EditorTools
{
    /// <summary>게임 흐름 인스펙터: 필드 화면과 눈 화면을 Play 없이 오가는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(GameFlow))]
    public class GameFlowEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var f = (GameFlow)target;
            EditorGUILayout.LabelField($"지금 화면: {f.CurrentState}", EditorStyles.boldLabel);

            FieldNPC npc = f.CurrentNpc != null ? f.CurrentNpc
                : Object.FindObjectsByType<FieldNPC>(FindObjectsSortMode.None).FirstOrDefault(n => !n.TalkOnly);
            var roots = new Object[]
            {
                f, f.Cam != null ? f.Cam.transform : null,
                f.Director != null ? f.Director.gameObject : null,
                f.Intro, npc,
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("필드 화면 보기", GUILayout.Height(26)))
                    EyeEditorOps.Run("필드 화면", () => { f.CurrentState = GameFlow.State.Encounter; f.ReturnToFieldNow(); }, roots);
                GUI.enabled = npc != null;
                if (GUILayout.Button(npc != null ? $"{npc.DisplayName} 눈 화면 보기" : "눈 화면 보기", GUILayout.Height(26)))
                    EyeEditorOps.Run("만남 화면", () => { f.CurrentState = GameFlow.State.Field; f.BeginEncounter(npc); }, roots);
                GUI.enabled = true;
            }
            if (f.Player != null && npc != null && GUILayout.Button($"플레이어를 {npc.DisplayName} 앞으로"))
                EyeEditorOps.Run("플레이어 이동", () =>
                    f.Player.transform.position = npc.transform.position + (f.Player.SideView ? Vector3.right : Vector3.down) * (npc.Radius + f.Player.Radius + 0.3f),
                    f.Player.transform);

            EditorGUILayout.HelpBox(
                "필드의 모든 것(플레이어, 손님, 가구, 단서)은 Transform을 끌어서 옮긴다. 가구 크기는 FieldObstacle의 size로 바꾼다.\n" +
                "눈 화면에서는 '게임 디렉터'의 버튼으로 Play 없이 미로와 대화를 진행할 수 있다.", MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }

    /// <summary>플레이어 인스펙터: 충돌을 지키며 조금씩 옮기는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(FieldPlayer))]
    public class FieldPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (FieldPlayer)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("충돌을 지키며 조금씩 이동", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↑", GUILayout.Width(50))) Step(p, Vector2.up);
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("←", GUILayout.Width(50))) Step(p, Vector2.left);
                if (GUILayout.Button("↓", GUILayout.Width(50))) Step(p, Vector2.down);
                if (GUILayout.Button("→", GUILayout.Width(50))) Step(p, Vector2.right);
                GUILayout.FlexibleSpace();
            }
        }

        /// <summary>
        /// 플레이어를 한 걸음 옮긴다.
        /// p, dir을 사용하며, 플레이어의 방향과 위치를 바꾼다.
        /// </summary>
        private static void Step(FieldPlayer p, Vector2 dir)
        {
            EyeEditorOps.Run("플레이어 이동", () => { p.Facing = dir; p.MoveBy(dir * 0.4f); }, p.gameObject);
        }
    }

    /// <summary>단서 인스펙터: 살펴본 상태를 바꾸는 버튼을 보여준다.</summary>
    [CustomEditor(typeof(FieldClue))]
    public class FieldClueEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var c = (FieldClue)target;
            if (GUILayout.Button(c.Examined ? "살펴보지 않은 상태로" : "살펴본 상태로"))
                EyeEditorOps.Run("단서", () => c.Examined = !c.Examined, c);
        }
    }
}
