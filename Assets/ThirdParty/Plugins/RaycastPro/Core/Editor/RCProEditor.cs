#if UNITY_EDITOR
namespace RaycastPro.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using RaycastPro;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using Object = UnityEngine.Object;

    [CustomEditor(typeof(RaycastCore), true), CanEditMultipleObjects]
    public sealed class RCProEditor : Editor
    {
        //internal static readonly Color Aqua = new Color(0.1686275f, 0.7764706f, 0.8235294f, 1f);
        internal static Color Aqua =>
            RCProPanel.DarkMode ? new Color(0.17f, 0.87f, 0.92f) : new Color(0.62f, 0.95f, 0.98f);

        internal static Color Violet =>
            RCProPanel.DarkMode ? new Color(0.84f, 0.26f, 0.46f) : new Color(0.9f, 0.67f, 0.77f);

        internal static string RPro => $"<color=#2BC6D2>RaycastPro: </color>";

        // ReSharper disable Unity.PerformanceAnalysis
        public static void Log(string log) => Debug.Log(RPro + log);

        internal static string AQUA_Text(string text) => $"<color=#2BC6D2>{text}</color>";
        internal static string VIOLET_Text(string text) => $"<color=#E04181>{text}</color>";

        private const string INFO = "Info";

        private readonly string[] MultiEditingPropsName = new string[]
        {
            "destination", "detectLayer", "direction", "radius", "height", "iteration", "colliderSize"
        };

        private SerializedProperty _cProp;
        private SerializedObject _cSO;
        private RaycastCore[] _cores;

        // [RCProPanel.SavePreference]
        // public bool sceneGUI;

        public override void OnInspectorGUI()
        {
            if (!target || !(target is RaycastCore pro)) return;

            if (!RCProPanel.rcProInspector || target.GetType().GetCustomAttribute<RCProPanel.RawEditor>(true) != null)
            {
                base.OnInspectorGUI();

                return;
            }

            GUI.color = Color.white;
            _cores = Selection.gameObjects.Select(o => o.GetComponent<RaycastCore>()).ToArray();
            var _isMulti = _cores.Count() > 1;
            HeaderField(target.GetType().Name.ToRegex() + (_isMulti ? " (Multi Editing)" : ""));
            GUI.backgroundColor = Violet;
            InfoField(pro);
            GUI.contentColor = Aqua;
            EditorGUILayout.BeginVertical();

            _cSO = _isMulti ? new SerializedObject(_cores) : new SerializedObject(pro);
            _cSO.Update();

            EditorGUI.BeginChangeCheck();
            pro.EditorPanel(_cSO);
            if (EditorGUI.EndChangeCheck()) _cSO.ApplyModifiedProperties();

            EditorGUILayout.EndVertical();
        }

        public void OnSceneGUI()
        {
            if (target is ISceneGUI iScene && target is RaycastCore core)
            {
                _cSO = new SerializedObject(core);
                _cSO.Update();
                EditorGUI.BeginChangeCheck();
                iScene.OnSceneGUI();
                if (EditorGUI.EndChangeCheck()) _cSO.ApplyModifiedProperties();
            }
        }

        internal static GUIStyle HeaderStyle =>
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

        internal static GUIStyle BoxStyle =>
            new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                contentOffset = Vector2.zero,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(4, 4, 2, 4),
                padding = new RectOffset(4, 4, 2, 4),
            };

        internal static GUIStyle LabelStyle =>
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

        internal static GUIStyle HeaderFoldout
        {
            get
            {
                var style = new GUIStyle(EditorStyles.toolbarButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(12, 4, 2, 2),
                    padding = new RectOffset(0, 0, 0, 0),
                };
                return style;
            }
        }

        internal static void EventField(SerializedObject serializedObject, IEnumerable<string> propertyNames)
        {
            var style = new GUIStyle {alignment = TextAnchor.MiddleCenter};
            EditorGUILayout.BeginVertical(style);
            foreach (var propertyName in propertyNames)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(propertyName));
            EditorGUILayout.EndVertical();
        }

        internal static void HeaderField(string header, string tooltip = "")
        {
            GUILayout.Space(4);

            GUILine(RCProPanel.DarkMode ? Color.white : Color.black);

            if (tooltip != "") GUILayout.Label(header.ToContent(tooltip), HeaderStyle);

            else GUILayout.Label(header, HeaderStyle);

            GUILayout.Space(1);

            GUILine(RCProPanel.DarkMode ? Color.white : Color.black);

            GUILayout.Space(4);
        }

        internal static void BetterSliderField(ref float minValue, ref float maxValue, float minLimit, float maxLimit,
            float labelWidth = 40f)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.FloatField(Mathf.Round(minValue * 100) / 100, GUILayout.Width(labelWidth));
            EditorGUILayout.MinMaxSlider(ref minValue, ref maxValue, minLimit, maxLimit);
            EditorGUILayout.FloatField(Mathf.Round(maxValue * 100) / 100, GUILayout.Width(labelWidth));
            EditorGUILayout.EndHorizontal();
        }

        internal static void LayerField(GUIContent label, ref LayerMask layerMask) // layerField
        {
            LayerMask tempMask = EditorGUILayout.MaskField(label,
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(layerMask), InternalEditorUtility.layers);

            layerMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        }

        internal static LayerMask LayerField(GUIContent label, LayerMask layerMask) // layerField
        {
            LayerMask tempMask = EditorGUILayout.MaskField(label,
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(layerMask), InternalEditorUtility.layers);

            return InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        }

        private static GUIStyle cleanStyle => new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(5, 5, 5, 5),
            padding = new RectOffset(5, 5, 5, 5),
            fixedWidth = 64,
            fixedHeight = 64,
        };

        private static GUIStyle labelStyle => new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(5, 5, 5, 5),
            wordWrap = true,
            richText = true
        };

        private const string CNoInfoDefinition = "No Info Definition.";

        static readonly Dictionary<RaycastCore, bool> InfoExpanded = new();

        const int INFO_CHAR_LIMIT = 200;

        internal static string GetInfo(RaycastCore pro) => pro.Info;

        static GUIStyle _infoLabelStyle;

        static GUIStyle InfoLabelStyle
        {
            get
            {
                if (_infoLabelStyle != null)
                    return _infoLabelStyle;

                _infoLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true,
                    clipping = TextClipping.Clip,

                    alignment = TextAnchor.UpperLeft,

                    padding = new RectOffset(6, 6, 4, 4),
                    margin = new RectOffset(2, 2, 2, 2)
                };

                return _infoLabelStyle;
            }
        }

        //BackUp
        // internal static void InfoField(RaycastCore pro)
        // {
        //     GUILayout.BeginHorizontal(BoxStyle);
        //     GUILayout.Box(EditorGUIUtility.ObjectContent(pro, pro.GetType()).image, cleanStyle);
        //     GUILayout.Label(GetInfo(pro), labelStyle);
        //     GUILayout.EndHorizontal();
        //     GUILine(Autorun.DarkMode ? Color.white : Color.black);
        //     EditorGUILayout.Space(1);
        //     GUILine(Autorun.DarkMode ? Color.white : Color.black);
        //     EditorGUILayout.Space(2);
        // }

        internal static void InfoField(RaycastCore pro)
        {
            if (!pro) return;

            string info = GetInfo(pro);
            if (string.IsNullOrEmpty(info)) return;

            bool expanded = InfoExpanded.TryGetValue(pro, out var e) && e;
            bool overflow = info.Length > INFO_CHAR_LIMIT;

            // متن قابل نمایش
            string displayText = info;

            if (!expanded && overflow)
            {
                displayText = info.Substring(0, INFO_CHAR_LIMIT).TrimEnd() + "...";
            }

            GUILayout.BeginHorizontal(BoxStyle);

            // Icon
            GUILayout.Box(
                EditorGUIUtility.ObjectContent(pro, pro.GetType()).image,
                cleanStyle
            );

            GUILayout.BeginVertical();

            GUILayout.Label(displayText, InfoLabelStyle);

            // Expand / Collapse control
            if (overflow)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(
                        expanded ? "▲" : "⋯",
                        EditorStyles.miniButton,
                        GUILayout.Width(22),
                        GUILayout.Height(16)))
                {
                    InfoExpanded[pro] = !expanded;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Separator
            GUILine(RCProPanel.DarkMode ? Color.white : Color.black);
            EditorGUILayout.Space(1);
            GUILine(RCProPanel.DarkMode ? Color.white : Color.black);
            EditorGUILayout.Space(2);
        }


        internal static void TypeField<T>(string label, ref T type) where T : Object
        {
            type = EditorGUILayout.ObjectField(label, type, typeof(T), true) as T;
        }

        internal static void TypeField<T>(GUIContent label, ref T type) where T : Object
        {
            type = EditorGUILayout.ObjectField(label, type, typeof(T), true) as T;
        }

        internal static T TypeField<T>(GUIContent label, T type) where T : Object
        {
            return EditorGUILayout.ObjectField(label, type, typeof(T), true) as T;
        }

        internal static void ButtonSelectionField(string[] labels, ref bool[] button)
        {
            var bColor = GUI.backgroundColor;

            GUILayout.BeginHorizontal(BoxStyle);
            for (var i = 0; i < labels.Length; i++)
            {
                var style = new GUIStyle(EditorStyles.toolbarButton);
                if (button[i]) GUI.backgroundColor = Color.white;

                if (GUILayout.Button(labels[i], style)) button[i] = !button[i];

                GUI.backgroundColor = bColor;
            }

            GUILayout.EndHorizontal();
        }

        internal static T EnumLabelField<T>(T type, GUIContent content, int column = 4) where T : Enum
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(content);

            var n = type.ToString();

            var s = (int) Enum.Parse(type.GetType(), n);

            var enumNames = Enum.GetNames(type.GetType());
            s = GUILayout.SelectionGrid(s, enumNames, enumNames.Length >= column ? column : enumNames.Length,
                GUILayout.Width(180f));

            EditorGUILayout.EndHorizontal();

            return (T) Enum.ToObject(type.GetType(), s);
        }

        internal static T EnumLabelField<T>(T type, GUIContent content, string[] contents, bool inBox = true,
            int column = 4) where T : Enum
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(content);

            var n = type.ToString();

            var s = (int) Enum.Parse(type.GetType(), n);

            var enumNames = Enum.GetNames(type.GetType()).ToContents(contents);
            s = GUILayout.SelectionGrid(s, enumNames, enumNames.Length >= column ? column : enumNames.Length,
                GUILayout.Width(200f));

            EditorGUILayout.EndHorizontal();

            return (T) Enum.ToObject(type.GetType(), s);
        }

        internal static T EnumHeaderField<T>(T type, bool label = true, int column = 4) where T : Enum
        {
            if (label) HeaderField(typeof(T).Name);

            var n = type.ToString();

            var s = (int) Enum.Parse(type.GetType(), n);

            var enumNames = Enum.GetNames(type.GetType());
            s = GUILayout.SelectionGrid(s, enumNames, enumNames.Length >= column ? column : enumNames.Length);

            return (T) Enum.ToObject(type.GetType(), s);
        }

        internal static T EnumHeaderField<T>(T type, GUIContent content, string[] contents, int column = 4)
            where T : Enum
        {
            HeaderField(content.text, content.tooltip);

            var n = type.ToString();

            var s = (int) Enum.Parse(type.GetType(), n);

            var enumNames = Enum.GetNames(type.GetType()).ToContents(contents);
            s = GUILayout.SelectionGrid(s, enumNames, enumNames.Length >= column ? column : enumNames.Length);

            return (T) Enum.ToObject(type.GetType(), s);
        }

        internal static void GUILine(Color color, int height = 1, int space = 0)
        {
            var rect = EditorGUILayout.GetControlRect(false, height);

            rect.height = height;

            GUILayout.Space(space);

            EditorGUI.DrawRect(rect, color);

            GUILayout.Space(space);
        }

        [Serializable]
        public class SerializableSet<T>
        {
            [SerializeField] private List<T> items = new List<T>();

            private HashSet<T> setCache;

            public HashSet<T> GetSet()
            {
                if (setCache == null)
                    setCache = new HashSet<T>(items);
                return setCache;
            }

            public void Add(T item)
            {
                if (!items.Contains(item))
                {
                    items.Add(item);
                    setCache?.Add(item);
                }
            }

            public void Remove(T item)
            {
                if (items.Remove(item))
                    setCache?.Remove(item);
            }

            public List<T> Items => items;
        }
        
        private static readonly Dictionary<string, ReorderableList> Cache = new();
        public static void DrawSerializedList(
            SerializedProperty listProperty,
            bool showAddRemove = true,
            bool draggable = true)
        {
            // ===== HARD GUARDS =====
            if (listProperty == null ||
                listProperty.serializedObject == null ||
                listProperty.serializedObject.targetObject == null)
            {
                return;
            }

            if (!listProperty.isArray)
            {
                EditorGUILayout.HelpBox("Property is not an array.", MessageType.Error);
                return;
            }

            var so = listProperty.serializedObject;
            var key = $"{so.targetObject.GetInstanceID()}_{listProperty.propertyPath}";

            if (!Cache.TryGetValue(key, out var list) || list == null)
            {
                list = CreateList(so, listProperty, showAddRemove, draggable);
                Cache[key] = list;
            }

            // 🔴 CRITICAL: always rebind
            list.serializedProperty = listProperty;

            list.DoLayoutList();
        }

        private static ReorderableList CreateList(
            SerializedObject so,
            SerializedProperty property,
            bool showAddRemove,
            bool draggable)
        {
            var list = new ReorderableList(
                so,
                property,
                draggable,
                true,
                showAddRemove,
                showAddRemove
            );

            // ===== HEADER =====
            list.drawHeaderCallback = rect =>
            {
                var prop = list.serializedProperty;
                if (prop == null) return;

                EditorGUI.LabelField(
                    rect,
                    new GUIContent(prop.displayName, prop.tooltip)
                );
            };

            // ===== ELEMENT DRAW =====
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                var prop = list.serializedProperty;

                if (prop == null)
                    return;

                if (index < 0 || index >= prop.arraySize)
                    return;

                SerializedProperty element;
                try
                {
                    element = prop.GetArrayElementAtIndex(index);
                }
                catch
                {
                    return;
                }

                rect.y += 2;

                var labelRect = new Rect(
                    rect.x,
                    rect.y,
                    60,
                    EditorGUIUtility.singleLineHeight
                );

                float height = EditorGUIUtility.singleLineHeight;

                if (element.propertyType != SerializedPropertyType.ObjectReference ||
                    element.objectReferenceValue != null)
                {
                    try
                    {
                        height = EditorGUI.GetPropertyHeight(element, true);
                    }
                    catch
                    {
                        height = EditorGUIUtility.singleLineHeight;
                    }
                }

                var fieldRect = new Rect(
                    rect.x + 65,
                    rect.y,
                    rect.width - 65,
                    height
                );

                EditorGUI.LabelField(labelRect, $"Index {index}");
                EditorGUI.PropertyField(fieldRect, element, GUIContent.none, true);
            };

            // ===== HEIGHT =====
            list.elementHeightCallback = index =>
            {
                var prop = list.serializedProperty;

                if (prop == null || index < 0 || index >= prop.arraySize)
                    return EditorGUIUtility.singleLineHeight + 4;

                SerializedProperty element;
                try
                {
                    element = prop.GetArrayElementAtIndex(index);
                }
                catch
                {
                    return EditorGUIUtility.singleLineHeight + 4;
                }

                if (element.propertyType == SerializedPropertyType.ObjectReference &&
                    element.objectReferenceValue == null)
                {
                    return EditorGUIUtility.singleLineHeight + 4;
                }

                try
                {
                    return EditorGUI.GetPropertyHeight(element, true) + 4;
                }
                catch
                {
                    return EditorGUIUtility.singleLineHeight + 4;
                }
            };

            return list;
        }
    }

    [CustomEditor(typeof(Info))]
    public sealed class ExampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();
            var info = target as Info;
            GUI.color = Color.white;
            GUI.backgroundColor = RCProEditor.Violet;
            GUI.contentColor = RCProEditor.Aqua;
            RCProEditor.HeaderField(info.header);
            EditorGUILayout.LabelField(info.description, RCProEditor.BoxStyle);
        }
    }
}

#endif