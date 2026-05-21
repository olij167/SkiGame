using PungentFunk.Utilities.Editor.Theme;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PungentSpatialProviderTemplateWindow : EditorWindow
    {
        private enum TemplateKind
        {
            Path,
            Area,
            CombinedPathArea
        }

        private TemplateKind _templateKind = TemplateKind.Path;
        private string _className = "MySpatialProviderAdapter";
        private string _namespaceName = "MyProject.SpatialAdapters";
        private string _targetFolder = "Assets";
        private Vector2 _scroll;

        [MenuItem("Tools/PungentFunk Utilities/Scene Workflow/Create Spatial Provider Template")]
        public static void Open()
        {
            PungentSpatialProviderTemplateWindow window = GetWindow<PungentSpatialProviderTemplateWindow>("Spatial Provider Template");
            window.minSize = new Vector2(540f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            UtilityWindowTheme.Header(
                "Spatial Provider Template",
                "Create a generic project-side adapter that implements PungentFunk spatial provider interfaces without referencing project-specific utility package code.",
                _targetFolder);

            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Blue)))
            {
                _className = EditorGUILayout.TextField("Class Name", _className);
                _namespaceName = EditorGUILayout.TextField("Namespace", _namespaceName);
                _templateKind = (TemplateKind)EditorGUILayout.EnumPopup("Template", _templateKind);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.TextField("Target Folder", _targetFolder);
                    if (GUILayout.Button("Choose", GUILayout.Width(72f)))
                        ChooseFolder();
                }
            }

            string preview = BuildTemplate(SanitizeIdentifier(_className), _namespaceName.Trim(), _templateKind);
            using (new EditorGUILayout.VerticalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Teal)))
            {
                UtilityWindowTheme.SectionTitle("Preview", UtilityWindowTheme.Teal);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                EditorGUILayout.TextArea(preview, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!CanCreate()))
                {
                    if (UtilityWindowTheme.TintedButton("Create Template", UtilityWindowTheme.Green, GUILayout.Height(30f), GUILayout.Width(150f)))
                        CreateTemplate(preview);
                }
            }
        }

        private void ChooseFolder()
        {
            string absolute = EditorUtility.OpenFolderPanel("Spatial Provider Template Folder", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute))
                return;

            absolute = absolute.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!absolute.StartsWith(dataPath))
            {
                EditorUtility.DisplayDialog("Choose Project Folder", "Choose a folder inside this project's Assets directory.", "OK");
                return;
            }

            _targetFolder = "Assets" + absolute.Substring(dataPath.Length);
        }

        private bool CanCreate()
        {
            return !string.IsNullOrWhiteSpace(_className) &&
                   !string.IsNullOrWhiteSpace(_targetFolder) &&
                   AssetDatabase.IsValidFolder(_targetFolder);
        }

        private void CreateTemplate(string preview)
        {
            string className = SanitizeIdentifier(_className);
            string path = AssetDatabase.GenerateUniqueAssetPath(_targetFolder.TrimEnd('/') + "/" + className + ".cs");
            File.WriteAllText(path, preview, Encoding.UTF8);
            AssetDatabase.Refresh();
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "MySpatialProviderAdapter";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = char.IsLetterOrDigit(c) || c == '_';
                if (!valid)
                    continue;
                if (builder.Length == 0 && char.IsDigit(c))
                    builder.Append('_');
                builder.Append(c);
            }

            return builder.Length == 0 ? "MySpatialProviderAdapter" : builder.ToString();
        }

        private static string BuildTemplate(string className, string namespaceName, TemplateKind kind)
        {
            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using PungentFunk.Utilities.SceneTools;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            if (hasNamespace)
            {
                builder.Append("namespace ").AppendLine(namespaceName);
                builder.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;
            string interfaces = kind == TemplateKind.Area
                ? "IPungentAreaShapeProvider, IPungentAreaVolumeProvider, IPungentSpatialBoundsProvider, IPungentSpatialLabelProvider"
                : kind == TemplateKind.CombinedPathArea
                    ? "IPungentPathPointProvider, IPungentAreaShapeProvider, IPungentAreaVolumeProvider, IPungentSpatialBoundsProvider, IPungentSpatialLabelProvider"
                    : "IPungentPathPointProvider, IPungentSpatialBoundsProvider, IPungentSpatialLabelProvider";
            builder.Append(indent).AppendLine("public sealed class " + className + " : MonoBehaviour, " + interfaces);
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    [SerializeField] private string spatialId = \"custom-spatial-provider\";");
            builder.Append(indent).AppendLine("    [SerializeField] private string displayName = \"Custom Spatial Provider\";");
            builder.Append(indent).AppendLine("    [SerializeField] private Color color = Color.cyan;");
            builder.Append(indent).AppendLine("    [SerializeField] private List<string> tags = new List<string>();");
            if (kind != TemplateKind.Area)
                builder.Append(indent).AppendLine("    [SerializeField] private List<Vector3> localPathPoints = new List<Vector3>();");
            if (kind != TemplateKind.Path)
            {
                builder.Append(indent).AppendLine("    [SerializeField] private Vector2 rectangleSize = new Vector2(5f, 5f);");
                builder.Append(indent).AppendLine("    [SerializeField] private float minY = 0f;");
                builder.Append(indent).AppendLine("    [SerializeField] private float maxY = 3f;");
                builder.Append(indent).AppendLine("    [SerializeField] private bool runtimeQueryable = true;");
            }
            builder.AppendLine();
            if (kind != TemplateKind.Area)
                builder.Append(indent).AppendLine("    public int PointCount => localPathPoints != null ? localPathPoints.Count : 0;");
            builder.Append(indent).AppendLine("    public string SpatialId => spatialId;");
            builder.Append(indent).AppendLine("    public string SpatialDisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;");
            builder.Append(indent).AppendLine("    public IReadOnlyList<string> SpatialTags => tags;");
            builder.Append(indent).AppendLine("    public Color SpatialColor => color;");
            builder.AppendLine();
            if (kind != TemplateKind.Area)
            {
                builder.Append(indent).AppendLine("    public Vector3 GetWorldPoint(int index)");
                builder.Append(indent).AppendLine("    {");
                builder.Append(indent).AppendLine("        return transform.TransformPoint(localPathPoints[index]);");
                builder.Append(indent).AppendLine("    }");
                builder.AppendLine();
            }
            if (kind != TemplateKind.Path)
                AppendAreaTemplate(builder, indent);
            builder.Append(indent).AppendLine("    public bool TryGetSpatialBounds(out Bounds bounds)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        bounds = default;");
            if (kind == TemplateKind.Area)
            {
                builder.Append(indent).AppendLine("        if (!TryGetAreaVolume(out PungentAreaVolume volume))");
                builder.Append(indent).AppendLine("            return false;");
                builder.Append(indent).AppendLine("        bounds = volume.Bounds;");
                builder.Append(indent).AppendLine("        return true;");
                builder.Append(indent).AppendLine("    }");
            }
            else
            {
                builder.Append(indent).AppendLine("        if (PointCount == 0)");
                builder.Append(indent).AppendLine("            return false;");
            builder.AppendLine();
                builder.Append(indent).AppendLine("        bounds = new Bounds(GetWorldPoint(0), Vector3.zero);");
                builder.Append(indent).AppendLine("        for (int i = 1; i < PointCount; i++)");
                builder.Append(indent).AppendLine("            bounds.Encapsulate(GetWorldPoint(i));");
                if (kind == TemplateKind.CombinedPathArea)
                {
                    builder.Append(indent).AppendLine("        if (TryGetAreaVolume(out PungentAreaVolume volume))");
                    builder.Append(indent).AppendLine("            bounds.Encapsulate(volume.Bounds);");
                }
                builder.Append(indent).AppendLine("        return true;");
                builder.Append(indent).AppendLine("    }");
            }
            builder.Append(indent).AppendLine("}");

            if (hasNamespace)
                builder.AppendLine("}");

            return builder.ToString();
        }

        private static void AppendAreaTemplate(StringBuilder builder, string indent)
        {
            builder.Append(indent).AppendLine("    public bool TryGetAreaShape(out PungentAreaShape shape)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        Vector2 half = rectangleSize * 0.5f;");
            builder.Append(indent).AppendLine("        Vector3[] polygon =");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            transform.TransformPoint(new Vector3(-half.x, 0f, -half.y)),");
            builder.Append(indent).AppendLine("            transform.TransformPoint(new Vector3(-half.x, 0f, half.y)),");
            builder.Append(indent).AppendLine("            transform.TransformPoint(new Vector3(half.x, 0f, half.y)),");
            builder.Append(indent).AppendLine("            transform.TransformPoint(new Vector3(half.x, 0f, -half.y))");
            builder.Append(indent).AppendLine("        };");
            builder.Append(indent).AppendLine("        shape = new PungentAreaShape");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            Kind = PungentAreaShapeKind.RectangleXZ,");
            builder.Append(indent).AppendLine("            Projection = PungentSpatialProjectionMode.XZ,");
            builder.Append(indent).AppendLine("            Center = transform.position,");
            builder.Append(indent).AppendLine("            Rotation = transform.rotation,");
            builder.Append(indent).AppendLine("            Size = rectangleSize,");
            builder.Append(indent).AppendLine("            WorldPolygon = polygon");
            builder.Append(indent).AppendLine("        };");
            builder.Append(indent).AppendLine("        return true;");
            builder.Append(indent).AppendLine("    }");
            builder.AppendLine();
            builder.Append(indent).AppendLine("    public bool TryGetAreaVolume(out PungentAreaVolume volume)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        volume = default;");
            builder.Append(indent).AppendLine("        if (!TryGetAreaShape(out PungentAreaShape shape))");
            builder.Append(indent).AppendLine("            return false;");
            builder.Append(indent).AppendLine("        Bounds bounds = new Bounds(shape.WorldPolygon[0], Vector3.zero);");
            builder.Append(indent).AppendLine("        for (int i = 1; i < shape.WorldPolygon.Length; i++)");
            builder.Append(indent).AppendLine("            bounds.Encapsulate(shape.WorldPolygon[i]);");
            builder.Append(indent).AppendLine("        bounds.Encapsulate(new Vector3(bounds.center.x, minY, bounds.center.z));");
            builder.Append(indent).AppendLine("        bounds.Encapsulate(new Vector3(bounds.center.x, maxY, bounds.center.z));");
            builder.Append(indent).AppendLine("        volume = new PungentAreaVolume");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).AppendLine("            Shape = shape,");
            builder.Append(indent).AppendLine("            Bounds = bounds,");
            builder.Append(indent).AppendLine("            MinY = Mathf.Min(minY, maxY),");
            builder.Append(indent).AppendLine("            MaxY = Mathf.Max(minY, maxY),");
            builder.Append(indent).AppendLine("            HasFiniteHeight = true,");
            builder.Append(indent).AppendLine("            RuntimeQueryable = runtimeQueryable");
            builder.Append(indent).AppendLine("        };");
            builder.Append(indent).AppendLine("        return true;");
            builder.Append(indent).AppendLine("    }");
            builder.AppendLine();
        }
    }
#endif
}
