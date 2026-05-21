using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentInsertionDesignerWindow : EditorWindow
    {
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private PungentRichDocumentInsertionDefinition _working;
        private string _selectedId = string.Empty;
        private string _status = "Ready.";

        public static void Open()
        {
            PungentRichDocumentInsertionDesignerWindow window = GetWindow<PungentRichDocumentInsertionDesignerWindow>("Insertion Designer");
            window.minSize = new Vector2(720f, 460f);
            window.Show();
        }

        public static void OpenWithDefinition(PungentRichDocumentInsertionDefinition seed)
        {
            PungentRichDocumentInsertionDesignerWindow window = GetWindow<PungentRichDocumentInsertionDesignerWindow>("Insertion Designer");
            window.minSize = new Vector2(720f, 460f);
            window.Show();
            window.Focus();
            window.SetWorkingDefinition(seed);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Insertion Designer");
            minSize = new Vector2(720f, 460f);
            PungentRichDocumentInsertionDefinitionRegistry.EnsureLoaded();
            if (_working == null)
                SelectFirstOrCreate();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawDefinitionList();
            DrawDefinitionDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(new GUIContent("New Chip", "Create an inline chip definition."), EditorStyles.toolbarButton, GUILayout.Width(78f)))
                CreateNew(PungentRichDocumentInsertionRenderMode.InlineChip);
            if (GUILayout.Button(new GUIContent("New Insertion", "Create a slim custom insertion definition."), EditorStyles.toolbarButton, GUILayout.Width(102f)))
                CreateNew(PungentRichDocumentInsertionRenderMode.SlimBlock);

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_working == null))
            {
                if (GUILayout.Button(new GUIContent("Save", "Save this insertion definition."), EditorStyles.toolbarButton, GUILayout.Width(54f)))
                    SaveWorking();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Copy TSV", "Copy all insertion definitions as tab-separated values for review or bulk editing."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                EditorGUIUtility.systemCopyBuffer = PungentRichDocumentInsertionDefinitionDataSheetBridge.ToTsv(PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions);
                _status = "Copied insertion definitions as TSV.";
            }
            if (GUILayout.Button(new GUIContent("Data Sheet", "Export/import definitions through Data Sheets when that utility is installed."), EditorStyles.toolbarDropDown, GUILayout.Width(92f)))
                ShowDataSheetMenu();
            GUILayout.Label(_status, EditorStyles.miniLabel, GUILayout.MinWidth(180f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDefinitionList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230f), GUILayout.ExpandHeight(true));
            UtilityWindowTheme.SectionTitle("Insertions", UtilityWindowTheme.Teal);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            foreach (PungentRichDocumentInsertionDefinition definition in PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions)
            {
                bool selected = _working != null && PungentAuthoringId.EqualsId(_working.id, definition.id);
                string prefix = definition.builtIn ? "Built-in / " : string.Empty;
                GUIContent label = new GUIContent(prefix + definition.displayName, definition.category + " / " + definition.renderMode);
                if (GUILayout.Toggle(selected, label, EditorStyles.miniButton))
                    Select(definition);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawDefinitionDetails()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_working == null)
            {
                EditorGUILayout.HelpBox("Create or select an insertion definition.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawPreview();
            EditorGUILayout.Space(6f);

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(_working.builtIn))
            {
                _working.id = EditorGUILayout.TextField(new GUIContent("ID", "Stable definition id. Existing documents use this value."), _working.id);
            }
            _working.displayName = EditorGUILayout.TextField("Display Name", _working.displayName);
            _working.category = EditorGUILayout.TextField("Category", _working.category);
            _working.syntaxAlias = EditorGUILayout.TextField(new GUIContent("Syntax Alias", "Used by {chip:Alias} and custom insertion menus."), _working.syntaxAlias);
            _working.renderMode = (PungentRichDocumentInsertionRenderMode)EditorGUILayout.EnumPopup("Render Mode", _working.renderMode);
            _working.layoutMode = (PungentRichDocumentInsertionLayoutMode)EditorGUILayout.EnumPopup(new GUIContent("Layout", "How fields are arranged when this insertion is edited in the document canvas."), _working.layoutMode);
            _working.tintHex = EditorGUILayout.TextField(new GUIContent("Tint Hex", "Six digit RGB hex value."), _working.tintHex);
            _working.defaultText = EditorGUILayout.TextField("Default Text", _working.defaultText);
            _working.validationHint = EditorGUILayout.TextField("Validation Hint", _working.validationHint);
            _working.defaultBindingKind = EditorGUILayout.TextField("Default Binding Kind", _working.defaultBindingKind);

            EditorGUILayout.Space(8f);
            DrawFields();
            EditorGUILayout.Space(8f);
            DrawRepeatables();

            if (EditorGUI.EndChangeCheck())
            {
                Repaint();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawFields()
        {
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            if (_working.fields == null)
                _working.fields = new List<PungentRichDocumentInsertionFieldDefinition>();

            for (int i = 0; i < _working.fields.Count; i++)
            {
                PungentRichDocumentInsertionFieldDefinition field = _working.fields[i];
                if (field == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.key = EditorGUILayout.TextField(new GUIContent("Key", "Stable field key stored in document semantic data."), field.key);
                if (GUILayout.Button(new GUIContent("Remove Field", "Remove this field from the definition."), GUILayout.Width(96f)))
                {
                    _working.fields.RemoveAt(i);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                field.displayName = EditorGUILayout.TextField("Label", field.displayName);
                field.fieldKind = (PungentRichDocumentInsertionFieldKind)EditorGUILayout.EnumPopup("Kind", field.fieldKind);
                field.defaultValue = EditorGUILayout.TextField("Default", field.defaultValue);
                field.multiline = EditorGUILayout.ToggleLeft("Multiline", field.multiline);
                field.required = EditorGUILayout.ToggleLeft("Required", field.required);
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Field", GUILayout.Width(100f)))
            {
                _working.fields.Add(PungentRichDocumentInsertionFieldDefinition.Create("field" + (_working.fields.Count + 1), "Field " + (_working.fields.Count + 1)));
            }
        }

        private void DrawRepeatables()
        {
            EditorGUILayout.LabelField("Repeatable Sections", EditorStyles.boldLabel);
            if (_working.repeatableElements == null)
                _working.repeatableElements = new List<PungentRichDocumentInsertionRepeatableElementDefinition>();

            for (int i = 0; i < _working.repeatableElements.Count; i++)
            {
                PungentRichDocumentInsertionRepeatableElementDefinition repeatable = _working.repeatableElements[i];
                if (repeatable == null)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                repeatable.key = EditorGUILayout.TextField(new GUIContent("Key", "Stable repeatable section key stored in document semantic data."), repeatable.key);
                if (GUILayout.Button(new GUIContent("Remove Section", "Remove this repeatable section and its fields."), GUILayout.Width(112f)))
                {
                    _working.repeatableElements.RemoveAt(i);
                    i--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                repeatable.displayName = EditorGUILayout.TextField("Label", repeatable.displayName);
                repeatable.addButtonLabel = EditorGUILayout.TextField("Add Button", repeatable.addButtonLabel);
                if (repeatable.fields == null)
                    repeatable.fields = new List<PungentRichDocumentInsertionFieldDefinition>();
                for (int f = 0; f < repeatable.fields.Count; f++)
                {
                    PungentRichDocumentInsertionFieldDefinition field = repeatable.fields[f];
                    if (field == null)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    field.key = EditorGUILayout.TextField(field.key, GUILayout.MinWidth(80f));
                    field.displayName = EditorGUILayout.TextField(field.displayName, GUILayout.MinWidth(90f));
                    field.fieldKind = (PungentRichDocumentInsertionFieldKind)EditorGUILayout.EnumPopup(field.fieldKind, GUILayout.Width(90f));
                    field.defaultValue = EditorGUILayout.TextField(field.defaultValue, GUILayout.MinWidth(90f));
                    if (GUILayout.Button(new GUIContent("Remove", "Remove this repeatable field."), GUILayout.Width(64f)))
                    {
                        repeatable.fields.RemoveAt(f);
                        f--;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ Add Repeat Field", GUILayout.Width(138f)))
                    repeatable.fields.Add(PungentRichDocumentInsertionFieldDefinition.Create("text", "Text", PungentRichDocumentInsertionFieldKind.LongText, "Text"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Repeatable Section", GUILayout.Width(170f)))
            {
                _working.repeatableElements.Add(new PungentRichDocumentInsertionRepeatableElementDefinition
                {
                    key = "item",
                    displayName = "Item",
                    addButtonLabel = "Add Item",
                    fields = new List<PungentRichDocumentInsertionFieldDefinition>
                    {
                        PungentRichDocumentInsertionFieldDefinition.Create("text", "Text", PungentRichDocumentInsertionFieldKind.LongText, "Text")
                    }
                });
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            Color tint = PungentRichDocumentInsertionDefinitionRegistry.TintForDefinition(_working, UtilityWindowTheme.Teal);
            Rect rect = GUILayoutUtility.GetRect(120f, _working.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip ? 38f : 86f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.14f, 0.15f, 0.16f) : new Color(0.93f, 0.94f, 0.95f));

            Rect inner = new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f);
            if (_working.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
            {
                Vector2 size = EditorStyles.boldLabel.CalcSize(new GUIContent(_working.displayName));
                Rect chip = new Rect(inner.x, inner.y + 2f, Mathf.Max(96f, size.x + 24f), 24f);
                EditorGUI.DrawRect(chip, tint);
                GUI.Label(chip, _working.displayName, CenteredWhiteMiniLabel());
                EditorGUILayout.HelpBox("Typing {chip:" + _working.syntaxAlias + "} will render this inline chip.", MessageType.None);
                return;
            }

            EditorGUI.DrawRect(new Rect(inner.x, inner.y, 4f, inner.height), tint);
            GUI.Label(new Rect(inner.x + 10f, inner.y, inner.width - 10f, 20f), _working.displayName, EditorStyles.boldLabel);
            GUI.Label(new Rect(inner.x + 10f, inner.y + 24f, inner.width - 10f, 20f), string.IsNullOrWhiteSpace(_working.defaultText) ? "Custom insertion text." : _working.defaultText, EditorStyles.label);
            GUI.Label(new Rect(inner.x + 10f, inner.y + 46f, inner.width - 10f, 20f), (_working.fields == null ? 0 : _working.fields.Count) + " field(s), " + (_working.repeatableElements == null ? 0 : _working.repeatableElements.Count) + " repeatable section(s)", EditorStyles.miniLabel);
        }

        private void ShowDataSheetMenu()
        {
            GenericMenu menu = new GenericMenu();
            bool available = PungentRichDocumentInsertionDefinitionDataSheetBridge.IsAvailable;
            if (available)
            {
                menu.AddItem(new GUIContent("Export Definitions To Sheet"), false, () => SetStatus(PungentRichDocumentInsertionDefinitionDataSheetBridge.ExportDefinitionsToSheet(PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions)));
                menu.AddItem(new GUIContent("Import Definitions From Sheet"), false, () => SetStatus(PungentRichDocumentInsertionDefinitionDataSheetBridge.ImportDefinitionsFromSheet()));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Export Definitions To Sheet (Data Sheets not installed)"));
                menu.AddDisabledItem(new GUIContent("Import Definitions From Sheet (Data Sheets not installed)"));
                menu.AddDisabledItem(new GUIContent("Data Sheets are optional; this designer is the source of truth"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy Definitions As TSV"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = PungentRichDocumentInsertionDefinitionDataSheetBridge.ToTsv(PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions);
                SetStatus("Copied insertion definitions as TSV.");
            });
            menu.ShowAsContext();
        }

        private void SelectFirstOrCreate()
        {
            PungentRichDocumentInsertionDefinition first = PungentRichDocumentInsertionDefinitionRegistry.CustomDefinitions.FirstOrDefault() ??
                                                           PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions.FirstOrDefault();
            if (first != null)
                Select(first);
            else
                CreateNew(PungentRichDocumentInsertionRenderMode.SlimBlock);
        }

        private void Select(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return;

            _selectedId = definition.id;
            _working = Clone(definition);
            _detailScroll = Vector2.zero;
        }

        private void CreateNew(PungentRichDocumentInsertionRenderMode mode)
        {
            _working = new PungentRichDocumentInsertionDefinition
            {
                id = mode == PungentRichDocumentInsertionRenderMode.InlineChip ? "custom-chip" : "custom-insertion",
                displayName = mode == PungentRichDocumentInsertionRenderMode.InlineChip ? "Custom Chip" : "Custom Insertion",
                category = "Custom",
                syntaxAlias = mode == PungentRichDocumentInsertionRenderMode.InlineChip ? "Custom Chip" : "Custom Insertion",
                renderMode = mode,
                layoutMode = PungentRichDocumentInsertionLayoutMode.Vertical,
                tintHex = mode == PungentRichDocumentInsertionRenderMode.InlineChip ? "8E7CC3" : "6FA8DC",
                defaultText = mode == PungentRichDocumentInsertionRenderMode.InlineChip ? string.Empty : "Custom insertion text."
            };
            _working.NormalizeInPlace();
            _selectedId = _working.id;
            _status = "Created unsaved " + _working.displayName + ".";
        }

        private void SaveWorking()
        {
            if (_working == null)
                return;

            _working.builtIn = false;
            _working.NormalizeInPlace();
            PungentRichDocumentInsertionDefinitionRegistry.AddOrUpdate(_working);
            string error;
            if (PungentRichDocumentInsertionDefinitionRegistry.Save(out error))
            {
                _selectedId = _working.id;
                _status = "Saved " + _working.displayName + ".";
                PungentRichDocumentInsertionDefinitionStorage.Reload();
                PungentRichDocumentInsertionDefinition saved = PungentRichDocumentInsertionDefinitionRegistry.Find(_selectedId);
                if (saved != null)
                    _working = Clone(saved);
            }
            else
            {
                _status = error;
            }
        }

        private void SetStatus(string status)
        {
            _status = string.IsNullOrWhiteSpace(status) ? "Ready." : status;
            PungentRichDocumentInsertionDefinitionStorage.Reload();
            if (!string.IsNullOrWhiteSpace(_selectedId))
            {
                PungentRichDocumentInsertionDefinition selected = PungentRichDocumentInsertionDefinitionRegistry.Find(_selectedId);
                if (selected != null)
                    _working = Clone(selected);
            }
            Repaint();
        }

        private void SetWorkingDefinition(PungentRichDocumentInsertionDefinition seed)
        {
            if (seed == null)
                return;

            _working = Clone(seed);
            _working.builtIn = false;
            _selectedId = _working.id;
            _status = "Review and save the insertion captured from the document selection.";
            Repaint();
        }

        private static PungentRichDocumentInsertionDefinition Clone(PungentRichDocumentInsertionDefinition source)
        {
            if (source == null)
                return null;

            string json = JsonUtility.ToJson(source);
            PungentRichDocumentInsertionDefinition clone = JsonUtility.FromJson<PungentRichDocumentInsertionDefinition>(json);
            clone.NormalizeInPlace();
            return clone;
        }

        private static GUIStyle CenteredWhiteMiniLabel()
        {
            GUIStyle style = new GUIStyle(EditorStyles.whiteMiniLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            return style;
        }
    }

    internal static class PungentRichDocumentInsertionDefinitionDataSheetBridge
    {
        private const string SheetTitle = "Rich Document Insertion Definitions";
        private static readonly string[] Columns =
        {
            "ID", "Display Name", "Category", "Render Mode", "Layout", "Syntax Alias", "Tint Hex", "Default Text", "Validation Hint", "Default Binding Kind", "Fields", "Repeatables"
        };

        // Data Sheets are optional bulk-edit helpers; Rich Documents remain the source of truth for insertion definitions.
        public static bool IsAvailable =>
            FindType("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorStorage") != null &&
            FindType("PungentFunk.Utilities.DataSheets.PungentDataSheetDataType") != null;

        public static string ExportDefinitionsToSheet(IEnumerable<PungentRichDocumentInsertionDefinition> definitions)
        {
            if (!IsAvailable)
                return "Data Sheets utility is not installed.";

            try
            {
                Type storageType = FindType("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorStorage");
                Type dataTypeType = FindType("PungentFunk.Utilities.DataSheets.PungentDataSheetDataType");
                object sheet = storageType.GetMethod("CreateSheet", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { SheetTitle });
                Type sheetType = sheet.GetType();
                object textType = Enum.Parse(dataTypeType, "Text");

                List<object> columns = new List<object>();
                MethodInfo addColumn = sheetType.GetMethod("AddColumn", new[] { typeof(string), dataTypeType });
                foreach (string column in Columns)
                    columns.Add(addColumn.Invoke(sheet, new[] { column, textType }));

                MethodInfo addRow = sheetType.GetMethod("AddRow", new[] { typeof(string) });
                MethodInfo getOrCreateCell = sheetType.GetMethod("GetOrCreateCell", new[] { typeof(string), typeof(string) });
                foreach (PungentRichDocumentInsertionDefinition definition in definitions ?? new PungentRichDocumentInsertionDefinition[0])
                {
                    if (definition == null)
                        continue;

                    object row = addRow.Invoke(sheet, new object[] { definition.displayName });
                    string rowId = (string)row.GetType().GetField("id").GetValue(row);
                    string[] values =
                    {
                        definition.id,
                        definition.displayName,
                        definition.category,
                        definition.renderMode.ToString(),
                        definition.layoutMode.ToString(),
                        definition.syntaxAlias,
                        definition.tintHex,
                        definition.defaultText,
                        definition.validationHint,
                        definition.defaultBindingKind,
                        SerializeFields(definition.fields),
                        SerializeRepeatables(definition.repeatableElements)
                    };

                    for (int i = 0; i < columns.Count; i++)
                    {
                        string columnId = (string)columns[i].GetType().GetField("id").GetValue(columns[i]);
                        object cell = getOrCreateCell.Invoke(sheet, new object[] { rowId, columnId });
                        cell.GetType().GetField("rawValue").SetValue(cell, values[i] ?? string.Empty);
                        cell.GetType().GetField("displayValue").SetValue(cell, values[i] ?? string.Empty);
                    }
                }

                storageType.GetMethod("UpsertSheet", BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] { sheet, (object)true });
                storageType.GetMethod("SaveNow", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);

                Type windowType = FindType("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorWindow");
                if (windowType != null)
                {
                    string sheetId = (string)sheetType.GetField("id").GetValue(sheet);
                    windowType.GetMethod("OpenAndSelect", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { sheetId });
                }

                return "Exported insertion definitions to a Data Sheet.";
            }
            catch (Exception exception)
            {
                return "Could not export to Data Sheets: " + exception.Message;
            }
        }

        public static string ImportDefinitionsFromSheet()
        {
            if (!IsAvailable)
                return "Data Sheets utility is not installed.";

            try
            {
                Type storageType = FindType("PungentFunk.Utilities.Editor.DataSheets.PungentDataSheetEditorStorage");
                object database = storageType.GetProperty("Database", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                object sheets = database.GetType().GetField("sheets").GetValue(database);
                object sourceSheet = null;
                foreach (object sheet in (System.Collections.IEnumerable)sheets)
                {
                    string title = (string)sheet.GetType().GetField("title").GetValue(sheet);
                    if (title != null && title.IndexOf("Insertion Definition", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sourceSheet = sheet;
                        break;
                    }
                }

                if (sourceSheet == null)
                    return "No Data Sheet named like 'Insertion Definitions' was found.";

                List<PungentRichDocumentInsertionDefinition> definitions = ReadDefinitionsFromSheet(sourceSheet);
                if (definitions.Count == 0)
                    return "No custom definitions were found in the Data Sheet.";

                string preview = string.Join(Environment.NewLine, definitions
                    .Take(10)
                    .Select(definition => "- " + definition.displayName + " (" + definition.id + ")")
                    .ToArray());
                if (definitions.Count > 10)
                    preview += Environment.NewLine + "...and " + (definitions.Count - 10).ToString() + " more.";

                bool apply = EditorUtility.DisplayDialog(
                    "Import Rich Document Insertions",
                    "Import " + definitions.Count + " insertion definition(s) from the selected Data Sheet?" + Environment.NewLine + Environment.NewLine + preview,
                    "Apply Import",
                    "Cancel");
                if (!apply)
                    return "Import cancelled.";

                foreach (PungentRichDocumentInsertionDefinition definition in definitions)
                    PungentRichDocumentInsertionDefinitionRegistry.AddOrUpdate(definition);

                string error;
                if (!PungentRichDocumentInsertionDefinitionRegistry.Save(out error))
                    return error;
                return "Imported " + definitions.Count + " insertion definition(s).";
            }
            catch (Exception exception)
            {
                return "Could not import from Data Sheets: " + exception.Message;
            }
        }

        public static string ToTsv(IEnumerable<PungentRichDocumentInsertionDefinition> definitions)
        {
            List<string> rows = new List<string> { string.Join("\t", Columns) };
            foreach (PungentRichDocumentInsertionDefinition definition in definitions ?? new PungentRichDocumentInsertionDefinition[0])
            {
                if (definition == null)
                    continue;

                rows.Add(string.Join("\t", new[]
                {
                    definition.id,
                    definition.displayName,
                    definition.category,
                    definition.renderMode.ToString(),
                    definition.layoutMode.ToString(),
                    definition.syntaxAlias,
                    definition.tintHex,
                    definition.defaultText,
                    definition.validationHint,
                    definition.defaultBindingKind,
                    SerializeFields(definition.fields),
                    SerializeRepeatables(definition.repeatableElements)
                }.Select(value => (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")).ToArray()));
            }

            return string.Join(Environment.NewLine, rows.ToArray());
        }

        private static List<PungentRichDocumentInsertionDefinition> ReadDefinitionsFromSheet(object sheet)
        {
            Type sheetType = sheet.GetType();
            List<object> columns = ((System.Collections.IEnumerable)sheetType.GetField("columns").GetValue(sheet)).Cast<object>().ToList();
            List<object> rows = ((System.Collections.IEnumerable)sheetType.GetField("rows").GetValue(sheet)).Cast<object>().ToList();
            MethodInfo findCell = sheetType.GetMethod("FindCell", new[] { typeof(string), typeof(string) });
            List<PungentRichDocumentInsertionDefinition> definitions = new List<PungentRichDocumentInsertionDefinition>();

            foreach (object row in rows)
            {
                string rowId = (string)row.GetType().GetField("id").GetValue(row);
                Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (object column in columns)
                {
                    string columnId = (string)column.GetType().GetField("id").GetValue(column);
                    string name = (string)column.GetType().GetField("displayName").GetValue(column);
                    object cell = findCell.Invoke(sheet, new object[] { rowId, columnId });
                    if (cell == null)
                        continue;

                    values[name] = (string)cell.GetType().GetField("rawValue").GetValue(cell);
                }

                if (!values.TryGetValue("ID", out string id) || string.IsNullOrWhiteSpace(id))
                    continue;

                PungentRichDocumentInsertionDefinition definition = new PungentRichDocumentInsertionDefinition
                {
                    id = id,
                    displayName = Value(values, "Display Name"),
                    category = Value(values, "Category"),
                    syntaxAlias = Value(values, "Syntax Alias"),
                    tintHex = Value(values, "Tint Hex"),
                    defaultText = Value(values, "Default Text"),
                    validationHint = Value(values, "Validation Hint"),
                    defaultBindingKind = Value(values, "Default Binding Kind")
                };
                if (Enum.TryParse(Value(values, "Render Mode"), true, out PungentRichDocumentInsertionRenderMode mode))
                    definition.renderMode = mode;
                if (Enum.TryParse(Value(values, "Layout"), true, out PungentRichDocumentInsertionLayoutMode layout))
                    definition.layoutMode = layout;
                definition.fields = ParseFields(Value(values, "Fields"));
                definition.repeatableElements = ParseRepeatables(Value(values, "Repeatables"));
                definition.NormalizeInPlace();
                definitions.Add(definition);
            }

            return definitions;
        }

        private static string SerializeFields(IEnumerable<PungentRichDocumentInsertionFieldDefinition> fields)
        {
            return string.Join("; ", (fields ?? new PungentRichDocumentInsertionFieldDefinition[0])
                .Where(field => field != null)
                .Select(field => field.key + ":" + field.displayName + ":" + field.fieldKind + ":" + field.defaultValue)
                .ToArray());
        }

        private static string SerializeRepeatables(IEnumerable<PungentRichDocumentInsertionRepeatableElementDefinition> repeatables)
        {
            return string.Join("; ", (repeatables ?? new PungentRichDocumentInsertionRepeatableElementDefinition[0])
                .Where(repeatable => repeatable != null)
                .Select(repeatable => repeatable.key + ":" + repeatable.displayName + "(" + SerializeFields(repeatable.fields) + ")")
                .ToArray());
        }

        private static List<PungentRichDocumentInsertionFieldDefinition> ParseFields(string raw)
        {
            List<PungentRichDocumentInsertionFieldDefinition> fields = new List<PungentRichDocumentInsertionFieldDefinition>();
            foreach (string part in (raw ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pieces = part.Split(':');
                if (pieces.Length < 2)
                    continue;

                PungentRichDocumentInsertionFieldKind kind = PungentRichDocumentInsertionFieldKind.Text;
                if (pieces.Length > 2)
                    Enum.TryParse(pieces[2], true, out kind);
                fields.Add(PungentRichDocumentInsertionFieldDefinition.Create(pieces[0], pieces[1], kind, pieces.Length > 3 ? pieces[3] : string.Empty));
            }
            return fields;
        }

        private static List<PungentRichDocumentInsertionRepeatableElementDefinition> ParseRepeatables(string raw)
        {
            List<PungentRichDocumentInsertionRepeatableElementDefinition> repeatables = new List<PungentRichDocumentInsertionRepeatableElementDefinition>();
            string text = raw ?? string.Empty;
            int index = 0;
            while (index < text.Length)
            {
                int open = text.IndexOf('(', index);
                if (open < 0)
                    break;

                int close = text.IndexOf(')', open + 1);
                if (close < 0)
                    break;

                string header = text.Substring(index, open - index).Trim().Trim(';').Trim();
                string fieldText = text.Substring(open + 1, close - open - 1);
                string[] pieces = header.Split(':');
                if (pieces.Length >= 1 && !string.IsNullOrWhiteSpace(pieces[0]))
                {
                    string label = pieces.Length > 1 && !string.IsNullOrWhiteSpace(pieces[1])
                        ? pieces[1]
                        : ObjectNames.NicifyVariableName(pieces[0]);
                    repeatables.Add(new PungentRichDocumentInsertionRepeatableElementDefinition
                    {
                        key = pieces[0],
                        displayName = label,
                        addButtonLabel = "Add " + label,
                        fields = ParseFields(fieldText)
                    });
                }

                index = close + 1;
                while (index < text.Length && (text[index] == ';' || char.IsWhiteSpace(text[index])))
                    index++;
            }

            return repeatables;
        }

        private static string Value(Dictionary<string, string> values, string key)
        {
            return values != null && values.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
#endif
}
