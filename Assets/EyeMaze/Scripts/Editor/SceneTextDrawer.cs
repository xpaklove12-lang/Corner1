using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// [SceneText] 칸을 그린다. 칸 값이 씬 문구 key면 key와 그 원문을 보여 주고, 원문을 고쳐 씬 문구 파일에 저장한다.
    /// 아직 옮기지 않은 글이면 글을 그대로 고치게 하고, 옮기기 메뉴를 알려 준다.
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneTextAttribute))]
    public class SceneTextDrawer : PropertyDrawer
    {
        private const float GAP = 2f;
        /// <summary>고치는 중인 원문. key마다 하나이며, 저장하면 지운다.</summary>
        private static readonly Dictionary<string, string> Drafts = new Dictionary<string, string>();

        /// <summary>
        /// 칸의 높이를 정한다. key면 key 줄, 원문 칸, 안내 줄을, 아니면 글 칸과 안내 줄을 더한다.
        /// property와 attribute의 줄 수를 사용하며, 높이를 반환한다.
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String) return EditorGUI.GetPropertyHeight(property, label);
            float line = EditorGUIUtility.singleLineHeight;
            float area = AreaHeight();
            return IsKey(property.stringValue) ? line + GAP + area + GAP + line : area + GAP + line;
        }

        /// <summary>
        /// 칸을 그린다. key면 원문을 보여 주고 "원문 저장" 버튼으로 씬 문구 파일에 쓴다.
        /// position, property, label을 사용하며, 칸 값이나 씬 문구 파일을 바꾼다.
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            float line = EditorGUIUtility.singleLineHeight;
            float area = AreaHeight();
            string value = property.stringValue;
            EditorGUI.BeginProperty(position, label, property);

            if (!IsKey(value))
            {
                var areaRect = new Rect(position.x, position.y, position.width, area);
                Rect field = EditorGUI.PrefixLabel(areaRect, label);
                EditorGUI.BeginChangeCheck();
                string text = EditorGUI.TextArea(field, value ?? "", EditorStyles.textArea);
                if (EditorGUI.EndChangeCheck()) property.stringValue = text;
                var note = new Rect(field.x, areaRect.yMax + GAP, field.width, line);
                EditorGUI.LabelField(note, "번역하려면 EyeMaze/번역/씬 문구 옮기기로 key로 바꾼다.", EditorStyles.miniLabel);
                EditorGUI.EndProperty();
                return;
            }

            var keyRect = new Rect(position.x, position.y, position.width, line);
            EditorGUI.BeginChangeCheck();
            string key = EditorGUI.DelayedTextField(keyRect, label, value);
            if (EditorGUI.EndChangeCheck()) property.stringValue = key;

            string saved = Strings.Source(value);
            string draft = Drafts.TryGetValue(value, out string d) ? d : saved ?? "";
            float labelWidth = EditorGUIUtility.labelWidth;
            var body = new Rect(keyRect.x + labelWidth, keyRect.yMax + GAP, keyRect.width - labelWidth, area);
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUI.TextArea(body, draft, EditorStyles.textArea);
            if (EditorGUI.EndChangeCheck()) Drafts[value] = edited;

            var row = new Rect(body.x, body.yMax + GAP, body.width, line);
            if (saved == null)
            {
                EditorGUI.LabelField(row, "씬 문구 파일에 이 key가 없다. 원문을 적고 저장하면 줄을 더한다.", EditorStyles.miniLabel);
            }
            if (Drafts.ContainsKey(value) && Drafts[value] != (saved ?? ""))
            {
                var button = new Rect(row.xMax - 90f, row.y, 90f, line);
                if (GUI.Button(button, "원문 저장"))
                {
                    SceneTexts.SetSource(value, Drafts[value]);
                    Drafts.Remove(value);
                    GUI.FocusControl(null);
                }
            }
            else if (saved != null)
            {
                EditorGUI.LabelField(row, "원문(ko). 번역은 Loc/Scene.csv에서 고친다.", EditorStyles.miniLabel);
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 칸 값이 씬 문구 key인지 본다.
        /// value를 사용하며, key면 true를 반환한다.
        /// </summary>
        private static bool IsKey(string value) => !string.IsNullOrEmpty(value) && value.StartsWith(Strings.SCENE_KEY_PREFIX, System.StringComparison.Ordinal);

        /// <summary>
        /// 원문 칸의 높이를 정한다.
        /// attribute의 줄 수를 사용하며, 높이를 반환한다.
        /// </summary>
        private float AreaHeight()
        {
            int lines = ((SceneTextAttribute)attribute).Lines;
            return EditorGUIUtility.singleLineHeight * lines + (lines > 1 ? 4f : 0f);
        }
    }
}
