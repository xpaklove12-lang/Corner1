using System;
using System.Linq;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

using Object = UnityEngine.Object;

namespace EyeMaze.EditorTools
{
    /// <summary>런타임 코드가 만들고 지우는 오브젝트를 에디터 Undo에 연결한다.</summary>
    [InitializeOnLoad]
    public static class EyeHookInstaller
    {
        static EyeHookInstaller()
        {
            EyeHooks.OnCreated = go =>
            {
                if (!Application.isPlaying && go != null) Undo.RegisterCreatedObjectUndo(go, "EyeMaze 생성");
            };
            EyeHooks.OnDestroyRequest = go =>
            {
                if (go != null) Undo.DestroyObjectImmediate(go);
            };
        }
    }

    /// <summary>에디트 모드 조작을 Undo 한 단계로 묶는다.</summary>
    public static class EyeEditorOps
    {
        public static GUIStyle Box
        {
            get
            {
                var s = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 12, richText = true };
                s.padding = new RectOffset(8, 8, 6, 6);
                return s;
            }
        }

        /// <summary>
        /// 조작 하나를 Undo 한 단계로 묶어 실행한다.
        /// undoName, action, roots(되돌릴 대상)를 사용하며, 에디트 모드에서는 대상과 씬을 변경됨으로 표시한다.
        /// </summary>
        public static void Run(string undoName, Action action, params Object[] roots)
        {
            bool edit = !Application.isPlaying;
            Object[] valid = roots.Where(r => r != null).Distinct().ToArray();
            int group = 0;
            if (edit)
            {
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                foreach (Object r in valid) Undo.RegisterFullObjectHierarchyUndo(r, undoName);
            }

            action();

            if (edit)
            {
                foreach (Object r in valid) EditorUtility.SetDirty(r);
                Undo.CollapseUndoOperations(group);
                GameObject go = valid.Select(r => r is Component c ? c.gameObject : r as GameObject).FirstOrDefault(g => g != null);
                if (go != null) EditorSceneManager.MarkSceneDirty(go.scene);
                EditorApplication.QueuePlayerLoopUpdate();
            }
            SceneView.RepaintAll();
        }

        /// <summary>
        /// 디렉터 조작에서 함께 되돌려야 할 오브젝트들을 모은다.
        /// d를 사용하며, 디렉터와 미로, 상대 오브젝트 배열을 반환한다.
        /// </summary>
        public static Object[] Roots(EyeMazeDirector d)
        {
            if (d == null) return new Object[0];
            return new Object[]
            {
                d.gameObject,
                d.Maze != null ? d.Maze.gameObject : null,
                d.Person != null ? d.Person.gameObject : null,
            };
        }
    }
}
