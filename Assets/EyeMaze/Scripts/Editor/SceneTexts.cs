using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using EyeMaze.EditorTools;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>
    /// 씬 문구 파일(Data/Loc/Scene.csv)을 읽고 쓰고, 씬 인스펙터 문구를 이 파일로 옮기는 에디터 도구.
    /// 파일의 열은 공용 문구 파일(Data/Loc/UI.csv)과 같다.
    /// </summary>
    public static class SceneTexts
    {
        private const string KEY_COLUMN = "key";
        private const string CONTEXT_COLUMN = "context";
        /// <summary>파일이 없거나 머리글을 읽지 못할 때 쓰는 열 순서. 공용 문구 파일과 같다.</summary>
        private static readonly string[] DEFAULT_COLUMNS = { "key", "context", "maxlen", "ko", "en", "ja", "note", "en_ok", "ja_ok", "ko_synced" };
        /// <summary>형식별 씬 문구 칸 목록</summary>
        private static readonly Dictionary<Type, List<FieldInfo>> FieldCache = new Dictionary<Type, List<FieldInfo>>();

        /// <summary>
        /// 씬 문구 파일을 읽는다. 파일이 없으면 기본 머리글과 빈 줄 목록을 돌려준다.
        /// Strings.SCENE_PATH의 파일을 사용하며, 줄마다 열 이름과 값을 담은 목록을 반환하고 머리글을 columns에 쓴다.
        /// </summary>
        public static List<Dictionary<string, string>> Read(out List<string> columns)
        {
            columns = new List<string>(DEFAULT_COLUMNS);
            var rows = new List<Dictionary<string, string>>();
            string full = Path.GetFullPath(Strings.SCENE_PATH);
            if (!File.Exists(full) || !Csv.TryDecodeUtf8(File.ReadAllBytes(full), out string text)) return rows;

            string header = text.Split('\n')[0].Trim();
            if (header.Length > 0)
            {
                columns.Clear();
                foreach (string c in header.Split(',')) if (c.Trim().Length > 0) columns.Add(c.Trim());
            }
            foreach (Csv.Row r in Csv.Parse(text, null))
            {
                var cells = new Dictionary<string, string>();
                foreach (string c in columns) cells[c] = r[c];
                rows.Add(cells);
            }
            return rows;
        }

        /// <summary>
        /// 씬 문구 파일을 쓰고 다시 임포트한다. 내용이 같으면 쓰지 않는다.
        /// columns와 rows를 사용하며, Strings.SCENE_PATH의 파일을 바꾼다.
        /// </summary>
        public static void Write(List<string> columns, List<Dictionary<string, string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", columns)).Append("\r\n");
            foreach (Dictionary<string, string> row in rows)
            {
                var cells = new List<string>(columns.Count);
                foreach (string c in columns) cells.Add(Cell(row.TryGetValue(c, out string v) ? v : ""));
                sb.Append(string.Join(",", cells)).Append("\r\n");
            }
            DialogueEditorFormat.Save(Strings.SCENE_PATH, sb.ToString());
        }

        /// <summary>
        /// key의 원문(ko)을 바꾼다. 파일에 key가 없으면 줄을 더한다.
        /// key와 source(원문, 줄바꿈 포함)를 사용하며, 씬 문구 파일을 바꾼다.
        /// </summary>
        public static void SetSource(string key, string source)
        {
            List<Dictionary<string, string>> rows = Read(out List<string> columns);
            Dictionary<string, string> row = rows.Find(r => r.GetValueOrDefault(KEY_COLUMN, "") == key);
            if (row == null)
            {
                row = new Dictionary<string, string> { [KEY_COLUMN] = key };
                rows.Add(row);
            }
            row[Strings.SOURCE_COLUMN] = Escape(source);
            Write(columns, rows);
        }

        /// <summary>
        /// 형식에서 [SceneText]가 붙은 문자열 칸을 찾는다. 부모 형식의 칸도 포함한다.
        /// type을 사용하며, 칸 목록을 반환한다.
        /// </summary>
        public static List<FieldInfo> SceneTextFields(Type type)
        {
            if (FieldCache.TryGetValue(type, out List<FieldInfo> cached)) return cached;
            var list = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (f.FieldType == typeof(string) && f.IsDefined(typeof(SceneTextAttribute), false)) list.Add(f);
            }
            FieldCache[type] = list;
            return list;
        }

        /// <summary>
        /// 열린 씬의 씬 문구 칸에 적힌 글을 씬 문구 파일로 옮기고, 칸을 key로 바꾼다.
        /// 같은 형식, 같은 칸의 같은 글은 key 하나를 같이 쓴다. 이미 key인 칸과 빈 칸은 건너뛴다.
        /// 씬은 바뀐 것으로 표시만 하고 저장하지 않으며, Ctrl+Z로 되돌릴 수 있다.
        /// </summary>
        [MenuItem("EyeMaze/번역/씬 문구 옮기기")]
        private static void MoveSceneTexts()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[씬 문구] Play 중에는 옮기지 않는다. Play를 멈추고 다시 실행한다.");
                return;
            }
            List<Dictionary<string, string>> rows = Read(out List<string> columns);
            var keyOfText = new Dictionary<string, string>();
            var nextIndex = new Dictionary<string, int>();
            foreach (Dictionary<string, string> r in rows) Remember(r.GetValueOrDefault(KEY_COLUMN, ""), r.GetValueOrDefault(Strings.SOURCE_COLUMN, ""), keyOfText, nextIndex);

            int moved = 0;
            Undo.SetCurrentGroupName("씬 문구 옮기기");
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || EditorUtility.IsPersistent(mb)) continue;
                List<FieldInfo> fields = SceneTextFields(mb.GetType());
                if (fields.Count == 0) continue;
                var so = new SerializedObject(mb);
                foreach (FieldInfo f in fields)
                {
                    SerializedProperty prop = so.FindProperty(f.Name);
                    if (prop == null || prop.propertyType != SerializedPropertyType.String) continue;
                    string value = prop.stringValue;
                    if (string.IsNullOrWhiteSpace(value) || value.StartsWith(Strings.SCENE_KEY_PREFIX, StringComparison.Ordinal)) continue;

                    string group = $"{Strings.SCENE_KEY_PREFIX}{mb.GetType().Name.ToLowerInvariant()}.{f.Name.TrimStart('_').ToLowerInvariant()}";
                    string source = Escape(value.Trim());
                    if (!keyOfText.TryGetValue(group + "\n" + source, out string key))
                    {
                        nextIndex.TryGetValue(group, out int n);
                        key = $"{group}.{n + 1}";
                        rows.Add(new Dictionary<string, string> { [KEY_COLUMN] = key, [CONTEXT_COLUMN] = PathOf(mb.transform), [Strings.SOURCE_COLUMN] = source });
                        Remember(key, source, keyOfText, nextIndex);
                    }
                    prop.stringValue = key;
                    moved++;
                }
                so.ApplyModifiedProperties();
            }

            if (moved == 0)
            {
                Debug.Log("[씬 문구] 옮길 글이 없다. 모든 씬 문구 칸이 이미 key이거나 비어 있다.");
                return;
            }
            Write(columns, rows);
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[씬 문구] 칸 {moved}개를 {Strings.SCENE_PATH}의 key로 옮겼다. 씬을 저장하면 반영된다.");
        }

        /// <summary>
        /// 파일에 있는 줄을 같은 글 찾기 표와 다음 번호 표에 더한다.
        /// key, source와 두 표를 사용하며, keyOfText와 nextIndex를 바꾼다.
        /// </summary>
        private static void Remember(string key, string source, Dictionary<string, string> keyOfText, Dictionary<string, int> nextIndex)
        {
            int dot = key.LastIndexOf('.');
            if (dot <= 0 || !int.TryParse(key.Substring(dot + 1), out int n)) return;
            string group = key.Substring(0, dot);
            keyOfText[group + "\n" + source] = key;
            nextIndex[group] = Math.Max(n, nextIndex.TryGetValue(group, out int old) ? old : 0);
        }

        /// <summary>
        /// 오브젝트의 계층 경로를 만든다. 번역할 때 글이 어디에 쓰이는지 알려 주는 context 칸에 쓴다.
        /// t를 사용하며, "부모/자식" 모양의 경로를 반환한다.
        /// </summary>
        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>
        /// 글 속 줄바꿈을 CSV에 쓰는 \n 표기로 바꾼다.
        /// text를 사용하며, 바꾼 글을 반환한다.
        /// </summary>
        private static string Escape(string text) => (text ?? "").Replace("\r\n", "\n").Replace("\n", "\\n");

        /// <summary>
        /// CSV 칸 값을 쓴다. 쉼표, 따옴표, 줄바꿈, 앞뒤 공백이 있으면 따옴표로 감싼다.
        /// v를 사용하며, 쓸 칸 글을 반환한다.
        /// </summary>
        private static string Cell(string v)
        {
            v = v ?? "";
            bool quote = v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 || (v.Length > 0 && (char.IsWhiteSpace(v[0]) || char.IsWhiteSpace(v[v.Length - 1])));
            return quote ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
