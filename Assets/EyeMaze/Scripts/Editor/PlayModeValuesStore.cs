using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace EyeMaze.EditorTools
{
    [InitializeOnLoad]
    public static class PlayModeValuesStore
    {
        private const string DATA_KEY = "EyeMaze.PlayModeValues.Data";
        private const string BASELINE_KEY = "EyeMaze.PlayModeValues.Baseline";
        private const int ARRAY_SIZE = -100;

        [Serializable]
        public class FieldValue
        {
            public string Path;
            public int Kind;
            public string Text;
            public Color Color;
            public Vector4 Vector;
            public Vector3Int IntegerVector;
            public Quaternion Rotation;
            public Rect Rect;
            public RectInt IntegerRect;
            public Bounds Bounds;
            public BoundsInt IntegerBounds;
        }

        [Serializable]
        public class Entry
        {
            public string Id;
            public string Type;
            public string Label;
            public List<FieldValue> Fields = new List<FieldValue>();
        }

        [Serializable]
        public class Snapshot
        {
            public List<Entry> Entries = new List<Entry>();
            public string Report = "";
        }

        [Serializable]
        private class Baseline
        {
            public List<string> Ids = new List<string>();
        }

        /// <summary>에디터 로드 시 Play Mode 전환 구독을 등록하며 보관 데이터는 세션에 유지한다.</summary>
        static PlayModeValuesStore()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play Mode 진입 직전 저장된 씬 컴포넌트의 식별자를 수집해 신규 생성 대상과 구분한다.</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            var baseline = new Baseline();
            foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component == null || EditorUtility.IsPersistent(component)
                    || string.IsNullOrEmpty(component.gameObject.scene.path)) continue;
                baseline.Ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(component).ToString());
            }
            SessionState.SetString(BASELINE_KEY, JsonUtility.ToJson(baseline));
        }

        /// <summary>입력 없이 세션에 보관한 목록과 결과 문구를 읽어 반환한다.</summary>
        public static Snapshot Load()
        {
            string json = SessionState.GetString(DATA_KEY, "");
            return string.IsNullOrEmpty(json) ? new Snapshot() : JsonUtility.FromJson<Snapshot>(json);
        }

        /// <summary>입력 없이 현재 세션의 보관 목록을 비운다.</summary>
        public static void Clear()
        {
            SessionState.EraseString(DATA_KEY);
        }

        /// <summary>components의 지원되는 Inspector 값을 누적 보관하고 처리 결과를 세션에 기록한다.</summary>
        public static void Capture(IEnumerable<Component> components)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Snapshot snapshot = Load();
            var baseline = JsonUtility.FromJson<Baseline>(SessionState.GetString(BASELINE_KEY, "{}"));
            var allowed = new Dictionary<string, string>();
            foreach (string original in baseline.Ids ?? new List<string>())
                if (GlobalObjectId.TryParse(original, out GlobalObjectId originalId)) allowed[GetLookupId(originalId)] = original;
            var messages = new List<string>();
            int saved = 0, skipped = 0;
            foreach (Component component in components)
            {
                if (component == null) continue;
                string label = GetLabel(component);
                string lookup = GetLookupId(GlobalObjectId.GetGlobalObjectIdSlow(component));
                if (EditorUtility.IsPersistent(component) || string.IsNullOrEmpty(component.gameObject.scene.path) || !allowed.TryGetValue(lookup, out string id))
                {
                    skipped++;
                    messages.Add(label + ": 저장된 씬의 기존 대상이 아닙니다. 도구 설치 후 Play Mode에 다시 진입해야 합니다.");
                    continue;
                }
                var entry = new Entry { Id = id, Type = component.GetType().AssemblyQualifiedName, Label = label };
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool next = property.NextVisible(true);
                while (next)
                {
                    if (property.editable && !IsInternal(property.name))
                    {
                        var values = new List<FieldValue>();
                        if (CaptureProperty(property, values)) entry.Fields.AddRange(values);
                        else messages.Add(label + " / " + property.displayName + ": 미지원 필드 제외");
                    }
                    next = property.NextVisible(false);
                }
                if (entry.Fields.Count == 0) { skipped++; messages.Add(label + ": 보관할 지원 필드 없음"); continue; }
                snapshot.Entries.RemoveAll(e => e.Id == id);
                snapshot.Entries.Add(entry);
                saved++;
            }
            snapshot.Report = $"보관 {saved}개 · 건너뜀 {skipped}개\n" + string.Join("\n", messages);
            SessionState.SetString(DATA_KEY, JsonUtility.ToJson(snapshot));
        }

        /// <summary>입력 없이 보관값을 Edit Mode의 원래 대상에 적용하고 한 Undo 그룹과 처리 결과를 만든다.</summary>
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Snapshot snapshot = Load();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("플레이 값 일괄 적용");
            int applied = 0, skipped = 0;
            var messages = new List<string>();
            foreach (Entry entry in snapshot.Entries)
            {
                Component target = null;
                if (GlobalObjectId.TryParse(entry.Id, out GlobalObjectId id))
                    target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Component;
                if (target == null || target.GetType().AssemblyQualifiedName != entry.Type)
                {
                    skipped++;
                    messages.Add(entry.Label + ": 대상 삭제, 씬 미로드 또는 타입 변경");
                    continue;
                }
                Undo.RecordObject(target, "플레이 값 일괄 적용");
                var serialized = new SerializedObject(target);
                int fields = 0;
                foreach (FieldValue value in entry.Fields)
                {
                    SerializedProperty property = serialized.FindProperty(value.Path);
                    if (property == null || !property.editable || !ApplyProperty(property, value))
                    {
                        messages.Add(entry.Label + " / " + value.Path + ": 필드 누락 또는 타입 변경");
                        continue;
                    }
                    fields++;
                }
                serialized.ApplyModifiedProperties();
                if (fields == 0) { skipped++; continue; }
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
                applied++;
            }
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            snapshot.Report = $"적용 {applied}개 · 건너뜀 {skipped}개\nCtrl+S로 씬을 저장하세요. Ctrl+Z로 이번 적용을 되돌릴 수 있습니다.\n" + string.Join("\n", messages);
            SessionState.SetString(DATA_KEY, JsonUtility.ToJson(snapshot));
        }

        /// <summary>component의 씬과 계층 경로 및 타입을 결합해 보관 목록 표시명을 반환한다.</summary>
        public static string GetLabel(Component component)
        {
            string path = component.name;
            for (Transform parent = component.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return component.gameObject.scene.name + "/" + path + " — " + component.GetType().Name;
        }

        /// <summary>name이 컴포넌트 내부 식별 또는 프리팹 연결 필드인지 반환한다.</summary>
        private static bool IsInternal(string name)
        {
            return name == "m_Script" || name == "m_GameObject" || name == "m_ObjectHideFlags" || name == "m_Name"
                || name == "m_EditorHideFlags" || name == "m_EditorClassIdentifier" || name.StartsWith("m_Prefab", StringComparison.Ordinal)
                || name == "m_CorrespondingSourceObject";
        }

        /// <summary>id의 프리팹 원본 식별자와 인스턴스 식별자를 합쳐 Play Mode에서 풀린 대상과 비교할 키를 반환한다.</summary>
        private static string GetLookupId(GlobalObjectId id)
        {
            ulong localId = (id.targetObjectId ^ id.targetPrefabId) & 0x7fffffffffffffffUL;
            return id.assetGUID + ":" + localId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>property의 지원값을 values에 추가하며 미지원 자식이 있으면 false를 반환한다.</summary>
        private static bool CaptureProperty(SerializedProperty property, List<FieldValue> values)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                if (property.arrayElementType.StartsWith("PPtr", StringComparison.Ordinal)) return false;
                values.Add(new FieldValue { Path = property.propertyPath, Kind = ARRAY_SIZE, Text = property.arraySize.ToString(CultureInfo.InvariantCulture) });
                for (int i = 0; i < property.arraySize; i++)
                    if (!CaptureProperty(property.GetArrayElementAtIndex(i), values)) return false;
                return true;
            }
            var value = new FieldValue { Path = property.propertyPath, Kind = (int)property.propertyType };
            switch (property.propertyType)
            {
                case SerializedPropertyType.Generic:
                    SerializedProperty child = property.Copy();
                    SerializedProperty end = property.GetEndProperty();
                    bool next = child.NextVisible(true);
                    while (next && !SerializedProperty.EqualContents(child, end))
                    {
                        if (!CaptureProperty(child, values)) return false;
                        next = child.NextVisible(false);
                    }
                    return true;
                case SerializedPropertyType.Integer: value.Text = property.longValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Float: value.Text = property.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: value.Text = property.boolValue ? "1" : "0"; break;
                case SerializedPropertyType.Enum: value.Text = property.intValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask: value.Text = property.intValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String: value.Text = property.stringValue; break;
                case SerializedPropertyType.Color: value.Color = property.colorValue; break;
                case SerializedPropertyType.Vector2: value.Vector = property.vector2Value; break;
                case SerializedPropertyType.Vector3: value.Vector = property.vector3Value; break;
                case SerializedPropertyType.Vector4: value.Vector = property.vector4Value; break;
                case SerializedPropertyType.Vector2Int: var v = property.vector2IntValue; value.IntegerVector = new Vector3Int(v.x, v.y, 0); break;
                case SerializedPropertyType.Vector3Int: value.IntegerVector = property.vector3IntValue; break;
                case SerializedPropertyType.Quaternion: value.Rotation = property.quaternionValue; break;
                case SerializedPropertyType.Rect: value.Rect = property.rectValue; break;
                case SerializedPropertyType.RectInt: value.IntegerRect = property.rectIntValue; break;
                case SerializedPropertyType.Bounds: value.Bounds = property.boundsValue; break;
                case SerializedPropertyType.BoundsInt: value.IntegerBounds = property.boundsIntValue; break;
                default: return false;
            }
            values.Add(value);
            return true;
        }

        /// <summary>value의 보관 타입이 property와 일치하면 값을 기록하고 성공 여부를 반환한다.</summary>
        private static bool ApplyProperty(SerializedProperty property, FieldValue value)
        {
            if (value.Kind == ARRAY_SIZE)
            {
                if (!property.isArray || property.propertyType == SerializedPropertyType.String) return false;
                property.arraySize = int.Parse(value.Text, CultureInfo.InvariantCulture);
                return true;
            }
            if ((int)property.propertyType != value.Kind) return false;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.longValue = long.Parse(value.Text, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Float: property.doubleValue = double.Parse(value.Text, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: property.boolValue = value.Text == "1"; break;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask: property.intValue = int.Parse(value.Text, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String: property.stringValue = value.Text; break;
                case SerializedPropertyType.Color: property.colorValue = value.Color; break;
                case SerializedPropertyType.Vector2: property.vector2Value = value.Vector; break;
                case SerializedPropertyType.Vector3: property.vector3Value = value.Vector; break;
                case SerializedPropertyType.Vector4: property.vector4Value = value.Vector; break;
                case SerializedPropertyType.Vector2Int: property.vector2IntValue = new Vector2Int(value.IntegerVector.x, value.IntegerVector.y); break;
                case SerializedPropertyType.Vector3Int: property.vector3IntValue = value.IntegerVector; break;
                case SerializedPropertyType.Quaternion: property.quaternionValue = value.Rotation; break;
                case SerializedPropertyType.Rect: property.rectValue = value.Rect; break;
                case SerializedPropertyType.RectInt: property.rectIntValue = value.IntegerRect; break;
                case SerializedPropertyType.Bounds: property.boundsValue = value.Bounds; break;
                case SerializedPropertyType.BoundsInt: property.boundsIntValue = value.IntegerBounds; break;
                default: return false;
            }
            return true;
        }
    }
}
