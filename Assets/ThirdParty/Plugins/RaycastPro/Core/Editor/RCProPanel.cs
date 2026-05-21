using System.Globalization;

#if UNITY_EDITOR
namespace RaycastPro.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Bullets;
    using Bullets2D;
    using Casters;
    using Casters2D;
    using Detectors;
    using Detectors2D;
    using Planers;
    using Planers2D;
    using RaySensors;
    using RaySensors2D;
    using UnityEditor;
    using UnityEngine;
    using Sensor;

    public sealed class RCProPanel : EditorWindow
    {
        internal const string KEY = "RaycastPro_Key : ";
        internal const string CResourcePath = "Resource_Path";
        internal const string CShowOnStart = "Show On Startup";
        private const int width  = 450;
        private const int height = 728;


        internal static Mode mode = Mode.TwoD;
        internal static CoreMode coreMode = CoreMode.RaySensors;
        internal static bool DarkMode => EditorGUIUtility.isProSkin;

        internal static bool showOnStart;

        public RCProSettings settingProfile;
        public PanelDataBase panelDataBase;

        internal static RCProSettings SettingProfile;
        internal static PanelDataBase PanelProfile;

        [SavePreference] internal string settingProfilePath;
        [SavePreference] internal string panelDatabasePath;

        private SerializedObject settingProfSO;
        private SerializedObject panelDatabaseSO;

        internal static bool realtimeEditor => !SettingProfile || SettingProfile.realtimeEditor;
        internal static bool rcProInspector => !SettingProfile || SettingProfile.rcProInspector;

        internal static Color DefaultColor => SettingProfile ? SettingProfile.DefaultColor : RCProEditor.Aqua;
        internal static Color DetectColor => SettingProfile ? SettingProfile.DetectColor : new Color(.3f, 1, .3f, 1f);
        internal static Color HelperColor => SettingProfile ? SettingProfile.HelperColor : new Color(1f, .7f, .0f, 1f);
        internal static Color BlockColor => SettingProfile ? SettingProfile.BlockColor : new Color(1f, .2f, .2f, 1f);

        internal static float normalDiscRadius => SettingProfile ? SettingProfile.normalDiscRadius : .2f;
        internal static float elementDotSize => SettingProfile ? SettingProfile.elementDotSize : .05f;
        internal static float alphaAmount => SettingProfile ? SettingProfile.alphaAmount : .2f;
        internal static float gizmosOffTime => SettingProfile ? SettingProfile.gizmosOffTime : 4f;

        internal static float raysStepSize => SettingProfile ? SettingProfile.raysStepSize : 4f;
        internal static float normalFilterRadius => SettingProfile ? SettingProfile.normalFilterRadius : 1f;
        internal static float linerMaxWidth => SettingProfile ? SettingProfile.linerMaxWidth : 1f;

        internal static bool DrawBlockLine => !SettingProfile || SettingProfile.DrawBlockLine;
        internal static bool DrawDetectLine => !SettingProfile || SettingProfile.DrawDetectLine;
        internal static bool DrawGuide => !SettingProfile || SettingProfile.DrawGuide;
        internal static bool ShowLabels => !SettingProfile || SettingProfile.ShowLabels;

        internal static int maxSubdivideTime => SettingProfile ? SettingProfile.maxSubdivideTime : 6;
        internal static bool drawHierarchyIcons => !SettingProfile || SettingProfile.drawHierarchyIcons;
        internal static int hierarchyIconsOffset => SettingProfile ? SettingProfile.hierarchyIconsOffset : 100;


        internal static Dictionary<Type, Texture2D> ICON_DICTIONARY = new Dictionary<Type, Texture2D>();
        internal static EditorWindow window;
        internal static bool LoadWhenOpen = false;
        private Texture2D headerTexture;
        private Vector2 scrollPos;
        public static string ResourcePath => GetFolderPath("Gizmos", "RaycastPro");

        public static string GetFolderPath(string currentFolderName, string parentFolderName)
        {
            string[] guids = AssetDatabase.FindAssets($"t:Folder {currentFolderName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string[] pathParts = path.Split('/');

                if (pathParts.Length >= 2 && pathParts[^2] == parentFolderName)
                {
                    return path;
                }
            }

            return string.Empty;
        }

        private float timer;

        private SerializedObject _so;

        private void OnEnable()
        {
            LoadPreferences();
            showOnStart = EditorPrefs.GetBool(KEY + CShowOnStart, true);
            headerTexture = IconManager.Header;

            modes = new[] {"Favourite", "Utility", "2D", "3D"};

            _so = new SerializedObject(this);

            // Load Settings
            settingProfilePath = EditorPrefs.GetString(KEY + nameof(settingProfilePath));
            var file1 = AssetDatabase.LoadAssetAtPath<RCProSettings>(settingProfilePath);
            if (file1)
            {
                settingProfile = file1;
                // Singleton
                SettingProfile = settingProfile;
                settingProfSO = new SerializedObject(settingProfile);
            }

            panelDatabasePath = EditorPrefs.GetString(KEY + nameof(panelDatabasePath));
            var file2 = AssetDatabase.LoadAssetAtPath<PanelDataBase>(panelDatabasePath);
            if (file2)
            {
                panelDataBase = file2;
                // Singleton
                PanelProfile = panelDataBase;
                panelDatabaseSO = new SerializedObject(panelDataBase);
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(KEY + CShowOnStart, showOnStart);

            _so = null;
        }

        private void OnFocus()
        {
            Repaint();
        }

        private List<MonoScript> cores = new List<MonoScript>();

        private Color lineColor;

        private static string[] modes;


        private float time;
        private string ColorHash;
        private Color randomColor;

        private void OnInspectorUpdate()
        {
            time += .1f;
            if (time > 1)
            {
                time %= 1f;
            }

            randomColor = Color.HSVToRGB(time % 1f, 1f, 1f);
            ColorHash = ColorUtility.ToHtmlStringRGB(randomColor);
            
            Repaint();
        }

        private const string mScript = "m_Script";
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        
        private void InitContext()
        {
            lineColor = DarkMode ? RCProEditor.Aqua : Color.violet;
            GUI.color =  Color.white;
        }
        private void InitStyles()
        {
            _boxStyle ??= new GUIStyle(GUI.skin.box)
            {
                margin = new RectOffset(0, 0, 0, 0),
                alignment = TextAnchor.UpperCenter
            };

            _labelStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.UpperCenter,
                richText = true
            };

            _buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                richText = true
            };
        }
        private void DrawHeader()
        {
            if (headerTexture)
                GUILayout.Box(headerTexture, _boxStyle, GUILayout.Width(width), GUILayout.Height(153));

            RCProEditor.GUILine(lineColor);
            GUI.color =  DarkMode ? Color.white : Color.black;
            GUILayout.Label(
                "<b>RAYCAST_PRO v1.1.5</b> developed by <color=#2BC6D2>KIYNL</color>",
                _labelStyle
            );
            GUI.color =  Color.white;
            RCProEditor.GUILine(lineColor);
        }
        private void DrawModeSelector()
        {
            GUI.contentColor = RCProEditor.Aqua;
            GUI.backgroundColor = RCProEditor.Violet;

            mode = (Mode)GUILayout.SelectionGrid((int)mode, modes, 4);
        }
        private void DrawCoreIcons()
        {
            cores.Clear();

            if (!panelDataBase)
                return;

            cores = mode switch
            {
                Mode.Favorite => panelDataBase.favorite.ToList(),
                Mode.Utility  => panelDataBase.utility.ToList(),
                _             => ResolveCoresByMode()
            };

            IconLayout(cores, 7);
        }
        private List<MonoScript> ResolveCoresByMode()
        {
            coreMode = (CoreMode)GUILayout.SelectionGrid(
                (int)coreMode,
                Enum.GetNames(typeof(CoreMode)),
                5
            );

            return mode switch
            {
                Mode.ThreeD => coreMode switch
                {
                    CoreMode.RaySensors => panelDataBase.raySensors.ToList(),
                    CoreMode.Detectors  => panelDataBase.detectors.ToList(),
                    CoreMode.Planers    => panelDataBase.planers.ToList(),
                    CoreMode.Casters    => panelDataBase.casters.ToList(),
                    CoreMode.Bullets    => panelDataBase.bullets.ToList(),
                    _ => cores
                },

                Mode.TwoD => coreMode switch
                {
                    CoreMode.RaySensors => panelDataBase.raySensors2D.ToList(),
                    CoreMode.Detectors  => panelDataBase.detectors2D.ToList(),
                    CoreMode.Planers    => panelDataBase.planers2D.ToList(),
                    CoreMode.Casters    => panelDataBase.casters2D.ToList(),
                    CoreMode.Bullets    => panelDataBase.bullets2D.ToList(),
                    _ => cores
                },

                _ => cores
            };
        }
        private void DrawSettingsPanel()
        {
            if (_so == null)
                _so = new SerializedObject(this);

            GUILayout.Space(4);
            RCProEditor.GUILine(lineColor);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.BeginVertical(RCProEditor.BoxStyle);

            _so.Update();
            EditorGUI.BeginChangeCheck();

            var settingProp = _so.FindProperty(nameof(settingProfile));
            var panelProp   = _so.FindProperty(nameof(panelDataBase));

            if (settingProp != null)
                EditorGUILayout.PropertyField(settingProp, true);

            if (panelProp != null)
                EditorGUILayout.PropertyField(panelProp, true);

            if (EditorGUI.EndChangeCheck())
                ApplyProfileChanges();

            _so.ApplyModifiedProperties();

            DrawSettingProfileInspector();

            EditorGUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void ApplyProfileChanges()
        {
            if (settingProfile)
            {
                settingProfilePath = AssetDatabase.GetAssetPath(settingProfile);
                settingProfSO = new SerializedObject(settingProfile);
                EditorPrefs.SetString(KEY + nameof(settingProfilePath), settingProfilePath);
            }

            if (panelDataBase)
            {
                panelDatabasePath = AssetDatabase.GetAssetPath(panelDataBase);
                panelDatabaseSO = new SerializedObject(panelDataBase);
                EditorPrefs.SetString(KEY + nameof(panelDatabasePath), panelDatabasePath);
            }

            SceneView.RepaintAll();
        }
        private void DrawSettingProfileInspector()
        {
            if (!settingProfile || settingProfSO == null)
                return;

            settingProfSO.Update();
            var iterator = settingProfSO.GetIterator();

            if (!iterator.NextVisible(true))
                return;

            do
            {
                if (iterator.name != mScript)
                    EditorGUILayout.PropertyField(iterator, true);

            } while (iterator.NextVisible(false));

            settingProfSO.ApplyModifiedProperties();
        }
        private void DrawFooter()
        {
            GUILayout.Space(4);
            RCProEditor.GUILine(lineColor);

            if (GUILayout.Button($"{ColoredChar('♥')} Thanks for Submit a Review {ColoredChar('♥')}", _buttonStyle))
                Application.OpenURL("https://assetstore.unity.com/packages/tools/physics/raycastpro-214714#reviews");

            if (GUILayout.Button($"Follow tutorials on {ColoredChar('▶')} YouTube", _buttonStyle))
                Application.OpenURL("https://www.youtube.com/@KiynL");

            GUILayout.Space(2);
            RCProEditor.GUILine(lineColor);
            GUILayout.Label("Copyright all rights reserved", _labelStyle);
            RCProEditor.GUILine(lineColor);

            showOnStart = EditorGUILayout.Toggle("Show Panel on Start", showOnStart);
            RCProEditor.GUILine(lineColor);
        }
        private string ColoredChar(char c)
        {
            return $"<color=#{ColorHash}>{c}</color>";
        }
        private string ColoredText(string text)
        {
            return $"<color=#{ColorHash}>{text}</color>";
        }
        private void OnGUI()
        {
            InitContext();
            InitStyles();
            
            DrawHeader();
            DrawModeSelector();
            DrawCoreIcons();
            DrawSettingsPanel();
            DrawFooter();
        }

        [MenuItem("Tools/RaycastPro", priority = -10000)]
        public static void Init()
        {
            // Get existing open window or if none, make a new one:
            window = GetWindow(typeof(RCProPanel), true, "RaycastPro Panel", true);

            window.maxSize = new Vector2(width, height);
            window.minSize = new Vector2(width, height);

            window.Show();

            mode = SceneView.lastActiveSceneView.in2DMode ? Mode.TwoD : Mode.ThreeD;
        }

        public static void SavePreferences()
        {
            RCProEditor.Log("<color=#00FF00>Preferences Saves.</color>");
            var type = typeof(RCProPanel);
            var fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var fieldInfo in fields)
            {
                if (fieldInfo.GetCustomAttribute(typeof(SavePreference)) == null) continue;

                if (fieldInfo.FieldType == typeof(bool))
                    EditorPrefs.SetBool(KEY + fieldInfo.Name, (bool) fieldInfo.GetValue(null));
                else if (fieldInfo.FieldType == typeof(float))
                    EditorPrefs.SetFloat(KEY + fieldInfo.Name, (float) fieldInfo.GetValue(null));
                else if (fieldInfo.FieldType == typeof(int))
                    EditorPrefs.SetInt(KEY + fieldInfo.Name, (int) fieldInfo.GetValue(null));
                else if (fieldInfo.FieldType == typeof(string))
                    EditorPrefs.SetString(KEY + fieldInfo.Name, (string) fieldInfo.GetValue(null));
                else if (fieldInfo.FieldType == typeof(Color))
                    SaveColor(KEY + fieldInfo.Name, (Color) fieldInfo.GetValue(null));
            }
        }

        public static void LoadPreferences(bool message = true)
        {
            if (message) RCProEditor.Log("<color=#00FF00>Preferences Loaded.</color>");

            var type = typeof(RCProPanel);
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var fieldInfo in fields)
            {
                if (fieldInfo.GetCustomAttribute(typeof(SavePreference)) == null) continue;
                if (!EditorPrefs.HasKey(KEY + fieldInfo.Name)) continue;
                if (fieldInfo.FieldType == typeof(bool))
                {
                    fieldInfo.SetValue(null, EditorPrefs.GetBool(KEY + fieldInfo.Name));
                }
                else if (fieldInfo.FieldType == typeof(float))
                {
                    fieldInfo.SetValue(null, EditorPrefs.GetFloat(KEY + fieldInfo.Name));
                }
                else if (fieldInfo.FieldType == typeof(int))
                {
                    fieldInfo.SetValue(null, EditorPrefs.GetInt(KEY + fieldInfo.Name));
                }
                else if (fieldInfo.FieldType == typeof(string))
                {
                    fieldInfo.SetValue(null, EditorPrefs.GetString(KEY + fieldInfo.Name));
                }
                else if (fieldInfo.FieldType == typeof(Color))
                {
                    fieldInfo.SetValue(null, LoadColor(KEY + fieldInfo.Name));
                }
            }
        }

        private static void SaveColor(string key, Color color)
        {
            EditorPrefs.SetBool(key, true);
            EditorPrefs.SetFloat(key + "R", color.r);
            EditorPrefs.SetFloat(key + "G", color.g);
            EditorPrefs.SetFloat(key + "B", color.b);
            EditorPrefs.SetFloat(key + "A", color.a);
        }

        private static Color LoadColor(string key)
        {
            var col = new Color
            {
                r = EditorPrefs.GetFloat(key + "R"),
                g = EditorPrefs.GetFloat(key + "G"),
                b = EditorPrefs.GetFloat(key + "B"),
                a = EditorPrefs.GetFloat(key + "A")
            };
            return col;
        }

        public void IconLayout(List<MonoScript> types, int columnWidth)
        {
            var rows = types.Count / columnWidth;
            var guiStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperCenter
            };

            EditorGUILayout.BeginVertical(guiStyle);

            for (var i = 0; i <= rows; i++)
            {
                EditorGUILayout.BeginHorizontal(guiStyle);
                for (var j = 0; j < columnWidth; j++)
                {
                    var index = i * columnWidth + j;
                    if (index > types.Count - 1)
                    {
                        if (j == 0) break;

                        GUILayout.Box("", GUILayout.Width(60), GUILayout.Height(60));
                    }
                    else
                    {
                        if (types[index])
                        {
                            var _T = types[index];
                            Button(_T);

                            if (IsNew(_T))
                            {
                                // Rect آخرین کنترل (همان Button)
                                var lastRect = GUILayoutUtility.GetLastRect();

                                Rect badgeRect = new Rect(
                                    lastRect.xMax - 22, // 20 + 2
                                    lastRect.y,         // 2 - 2
                                    22,                 // 18 + 4
                                    16                  // 12 + 4
                                );

                                // Flat background
                                EditorGUI.DrawRect(badgeRect, RCProEditor.Violet);

                                var badgeStyle = new GUIStyle(EditorStyles.label)
                                {
                                    richText = true,
                                    alignment = TextAnchor.MiddleCenter,
                                    fontSize = 9,
                                    fontStyle = FontStyle.Bold,
                                    normal = { textColor = Color.white },
                                    padding = new RectOffset(0, 0, 0, 0)
                                };

                                GUI.Label(badgeRect, ColoredText("New"), badgeStyle);
                            }

                        }
                        else
                        {
                            GUILayout.Box("", GUILayout.Width(60), GUILayout.Height(60));
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private static readonly Dictionary<MonoScript, GUIContent> _guiContentCache
            = new Dictionary<MonoScript, GUIContent>();
        
        private static GUIContent GetCachedContent(MonoScript script)
        {
            if (script == null)
                return GUIContent.none;

            if (_guiContentCache.TryGetValue(script, out var cached))
                return cached;

            // icon (فقط یک بار)
            var unityContent = EditorGUIUtility.ObjectContent(script, typeof(MonoScript));
            var icon = unityContent.image as Texture2D;

            // description
            string description = "no Information";
            var scriptType = script.GetClass();

            if (scriptType != null && scriptType.IsSubclassOf(typeof(RaycastCore)))
            {
                description = RaycastCore.GetInfo(scriptType);
            }

            var content = new GUIContent(icon, description);
            _guiContentCache.Add(script, content);

            return content;
        }

        public static Texture2D GetIconFromScript(MonoScript script)
        {
            if (script == null) return null;
            var content = EditorGUIUtility.ObjectContent(script, typeof(MonoScript));
            return content.image as Texture2D;
        }
        
        static bool IsNew(MonoScript script)
        {
            if (!script)
                return false;

            var type = script.GetClass();
            if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type))
                return false;

            return Attribute.IsDefined(type, typeof(IsNewAttribute), false);
        }
        public bool Button(MonoScript type)
        {
            var content = GetCachedContent(type);

            var style = new GUIStyle(GUI.skin.button)
            {
                stretchWidth = false,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(6, 6, 6, 6),
                padding = new RectOffset(4, 4, 4, 4),
                wordWrap = false,
            };

            EditorGUILayout.BeginVertical();
            GUI.contentColor = Color.white;
            GUI.backgroundColor = DarkMode ? Color.white : RCProEditor.Violet;

            bool click = GUILayout.Button(content, style, GUILayout.Width(56), GUILayout.Height(56));

            string name = type.name;

            var labelStyle = new GUIStyle(RCProEditor.BoxStyle)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(4, 4, 0, 2)
            };

            var text = DarkMode
                ? $"<color=#2BC6D2>{name.ToRegex()}</color>"
                : $"<color=#000000>{name.ToRegex()}</color>";

            GUILayout.Box(text, labelStyle, GUILayout.Width(60), GUILayout.Height(30));

            EditorGUILayout.EndVertical();

            GUI.contentColor = RCProEditor.Aqua;
            GUI.backgroundColor = RCProEditor.Violet;

            if (click)
                CreateCore(type);

            return click;
        }


        private static void CreateCore(MonoScript monoScript)
        {
            if (monoScript == null)
            {
                Debug.LogWarning("MonoScript is null.");
                return;
            }

            var type = monoScript.GetClass();

            if (type == null)
            {
                Debug.LogWarning("Could not extract a class from the MonoScript.");
                return;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                Debug.LogWarning($"The class {type.Name} does not inherit from MonoBehaviour.");
                return;
            }

            GameObject obj = new GameObject();

            if (type.IsSubclassOf(typeof(Planar)))
            {
                GameObject.DestroyImmediate(obj);
                obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                obj.transform.localScale = new Vector3(10f, 10f, 1f);
            }
            else if (type.IsSubclassOf(typeof(Planar2D)))
            {
                var spriteRenderer = obj.AddComponent<SpriteRenderer>();

                var tex = new Texture2D(20, 200);
                var sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    100.0f);

                sprite.name = type.Name;

                spriteRenderer.sprite = sprite;
                spriteRenderer.color = DefaultColor;

                obj.transform.localScale = new Vector3(1f, 1f, 1f);

                var boxCollider = obj.AddComponent<BoxCollider2D>();
                boxCollider.size = new Vector2(0.2f, 2f);
            }
            else if (type.IsSubclassOf(typeof(Bullet)))
            {
                GameObject.DestroyImmediate(obj);
                obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            }
            else if (type.IsSubclassOf(typeof(Bullet2D)))
            {
                var spriteRenderer = obj.AddComponent<SpriteRenderer>();

                var tex = new Texture2D(100, 60);
                var sprite = Sprite.Create(tex, new Rect(0.0f, 0.0f, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                    100.0f);

                sprite.name = type.Name;

                spriteRenderer.sprite = sprite;
                spriteRenderer.color = DefaultColor;

                obj.transform.localScale = new Vector3(1f, 1f, 1f);

                var boxCollider = obj.AddComponent<BoxCollider2D>();
                boxCollider.size = new Vector2(1f, 0.6f);
            }

            obj.name = type.Name.ToRegex(); // فرض بر این است که ToRegex() یک اکستنشن متد است

            Undo.RegisterCreatedObjectUndo(obj, "create_core, ID: " + obj.GetInstanceID());

            var camera = SceneView.lastActiveSceneView.camera.transform;
            obj.transform.position = camera.position + camera.forward * 10f;

            obj.AddComponent(type);

            var activeSelection = Selection.activeTransform;

            if (activeSelection)
            {
                obj.transform.parent = activeSelection;
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            Selection.activeTransform = obj.transform;
        }


        [AttributeUsage(AttributeTargets.Field)]
        internal class SavePreference : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field)]
        internal class OnInit : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Class)]
        internal class RawEditor : Attribute
        {
        }

        internal enum CoreMode
        {
            RaySensors,
            Detectors,
            Planers,
            Casters,
            Bullets
        }

        internal enum Mode
        {
            Favorite,
            Utility,
            TwoD,
            ThreeD,
        }
    }
}
#endif