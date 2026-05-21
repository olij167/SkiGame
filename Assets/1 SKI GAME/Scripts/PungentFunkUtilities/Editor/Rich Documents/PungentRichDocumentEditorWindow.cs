using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Checklists;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.Checklists;
using PungentFunk.Utilities.Editor.Core;
using PungentFunk.Utilities.Editor.Core.Help;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.RichDocuments;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PungentFunk.Utilities.Editor.RichDocuments
{
#if UNITY_EDITOR
    public sealed class PungentRichDocumentEditorWindow : EditorWindow
    {
        private enum RichDocumentSurfaceMode
        {
            Document = 0,
            RawSource = 1
        }

        private const string PrefPrefix = "PungentFunkUtilities.RichDocumentEditor.";
        private const string PrefSelectedDocument = PrefPrefix + "SelectedDocument";
        private const string PrefTokenKey = PrefPrefix + "TokenKey";
        private const string PrefFontName = PrefPrefix + "FontName";
        private const string PrefFontSize = PrefPrefix + "FontSize";
        private const string PrefSurfaceMode = PrefPrefix + "SurfaceMode";
        private const string PrefShowLineNumbers = PrefPrefix + "ShowLineNumbers";
        private const string PrefShowAnnotationMarkers = PrefPrefix + "ShowAnnotationMarkers";
        private const string PrefShowSemanticTint = PrefPrefix + "ShowSemanticTint";
        private const string PrefShowTokenTechnicalText = PrefPrefix + "ShowTokenTechnicalText";
        private const string PrefCompactLineSpacing = PrefPrefix + "CompactLineSpacing";
        private const string PrefShowRawSyntaxHints = PrefPrefix + "ShowRawSyntaxHints";
        private const string PrefShowPropertiesTray = PrefPrefix + "ShowPropertiesTray";
        private const string PrefShowInlinePropagation = PrefPrefix + "ShowInlinePropagation";
        private const double AutosaveDelaySeconds = 2.25d;
        private const float PageMaxWidth = 860f;
        private const float PageMinWidth = 380f;
        private static readonly List<string> StatusChoices = new List<string> { "Draft", "In Progress", "Review", "Ready", "Published", "Blocked" };
        private static readonly List<string> PriorityChoices = new List<string> { "Low", "Normal", "High", "Critical" };

        private string _selectedDocumentId = string.Empty;
        private string _tokenKey = "playerName";
        private string _fontName = "Default";
        private int _fontSize = 14;
        private bool _showLineNumbers;
        private bool _showAnnotationMarkers = true;
        private bool _showSemanticTint = true;
        private bool _showTokenTechnicalText;
        private bool _compactLineSpacing;
        private bool _showRawSyntaxHints;
        private bool _showPropertiesTray = true;
        private bool _showInlinePropagation;
        private bool _dirty;
        private double _lastEditTime;
        private string _saveState = "Saved";
        private string _status = "Use the File menu for document actions. Sticky Notes is for quick contextual notes.";
        private RichDocumentSurfaceMode _surfaceMode = RichDocumentSurfaceMode.Document;
        private PungentAuthoringValidationResult _lastValidation;
        private PungentRichDocument _document;
        private VisualElement _contentRoot;
        private Label _savePill;
        private Label _statusLabel;
        private Label _validationPill;
        private ToolbarButton _surfaceModeButton;
        private ToolbarButton _propertiesButton;
        private ToolbarButton _inlinePropagationButton;
        private PungentRichDocumentDocumentCanvasView _canvasView;
        private PungentRichDocumentInlinePropagationView _inlinePropagationView;
        private TextField _rawSourceField;
        private PungentRichDocumentPropertiesView _propertiesView;

        public static void Open()
        {
            PungentRichDocumentEditorWindow window = GetWindow<PungentRichDocumentEditorWindow>("Rich Document Editor");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        public static void OpenAndSelect(string documentId)
        {
            PungentRichDocumentEditorWindow window = GetWindow<PungentRichDocumentEditorWindow>("Rich Document Editor");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
            window.Focus();
            window.SelectDocument(documentId);
        }

        public static void CreateChecklistDefinitionDocument()
        {
            PungentRichDocumentEditorWindow window = GetWindow<PungentRichDocumentEditorWindow>("Rich Document Editor");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
            window.Focus();
            window.CreateFromTemplate("checklist-definition");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Rich Document Editor");
            _selectedDocumentId = UtilityWindowPrefs.GetString(PrefSelectedDocument, string.Empty);
            _tokenKey = UtilityWindowPrefs.GetString(PrefTokenKey, "playerName");
            _fontName = UtilityWindowPrefs.GetString(PrefFontName, "Default");
            _fontSize = Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefFontSize, 14), 9, 32);
            _surfaceMode = (RichDocumentSurfaceMode)Mathf.Clamp(UtilityWindowPrefs.GetInt(PrefSurfaceMode, (int)RichDocumentSurfaceMode.Document), 0, 1);
            _showLineNumbers = UtilityWindowPrefs.GetBool(PrefShowLineNumbers, false);
            _showAnnotationMarkers = UtilityWindowPrefs.GetBool(PrefShowAnnotationMarkers, true);
            _showSemanticTint = UtilityWindowPrefs.GetBool(PrefShowSemanticTint, true);
            _showTokenTechnicalText = UtilityWindowPrefs.GetBool(PrefShowTokenTechnicalText, false);
            _compactLineSpacing = UtilityWindowPrefs.GetBool(PrefCompactLineSpacing, false);
            _showRawSyntaxHints = UtilityWindowPrefs.GetBool(PrefShowRawSyntaxHints, false);
            _showPropertiesTray = UtilityWindowPrefs.GetBool(PrefShowPropertiesTray, true);
            _showInlinePropagation = UtilityWindowPrefs.GetBool(PrefShowInlinePropagation, false);

            PungentRichDocumentStorage.EnsureLoaded();
            SelectInitialDocument();

            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += CommitBeforeReload;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= CommitBeforeReload;
            SavePrefs();
            SaveDirty("Saved document before closing.", false);
        }

        private void CreateGUI()
        {
            BuildWindow();
        }

        private void BuildWindow()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.12f, 0.125f, 0.13f) : new Color(0.82f, 0.84f, 0.86f));
            root.RegisterCallback<KeyDownEvent>(HandleKeyboardShortcut, TrickleDown.TrickleDown);

            root.Add(BuildDocumentToolbar());
            root.Add(BuildAuthoringToolbar());

            _contentRoot = new VisualElement();
            _contentRoot.style.flexGrow = 1f;
            _contentRoot.style.flexDirection = FlexDirection.Column;
            root.Add(_contentRoot);

            root.Add(BuildFooter());
            RefreshDocumentArea();
            RefreshStatusChrome();
        }

        private Toolbar BuildDocumentToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.style.minHeight = 30;

            toolbar.Add(CreateToolbarButton("File", ShowFileMenu, "New, open, duplicate, archive, and sticky-note handoff actions.", 48));
            toolbar.Add(CreateToolbarButton("Recent", ShowRecentDocumentMenu, "Open a compact recent-document list.", 68));
            toolbar.Add(CreateToolbarButton("Save", () => SaveDirty("Saved document.", false), "Save this document now.", 54));
            toolbar.Add(CreateToolbarButton("Sticky Notes", OpenStickyNotes, "Open the lightweight contextual notes surface.", 92));

            _surfaceModeButton = CreateToolbarButton(SurfaceModeLabel(), ToggleSurfaceMode, "Switch between the document canvas and raw source repair view.", 92);
            toolbar.Add(_surfaceModeButton);
            _propertiesButton = CreateToolbarButton("Properties", TogglePropertiesTray, "Show or hide semantic binding, annotation, and metadata properties.", 82);
            toolbar.Add(_propertiesButton);

            toolbar.Add(CreateToolbarButton("Validate", RunValidation, "Run local validation for this document only.", 70));
            toolbar.Add(CreateToolbarButton("Propagate", OpenPropagationPreview, "Preview current-document extraction before applying anything.", 82));
            _inlinePropagationButton = CreateToolbarButton("Outputs", ToggleInlinePropagation, "Show or hide the in-editor current-document output review/apply panel.", 70);
            toolbar.Add(_inlinePropagationButton);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            _savePill = new Label(_saveState);
            _savePill.tooltip = "Click to save now.";
            _savePill.RegisterCallback<MouseDownEvent>(_ => SaveDirty("Saved document.", false));
            ApplyPillStyle(_savePill, UtilityWindowTheme.Green, 104);
            toolbar.Add(_savePill);
            toolbar.Add(CreateToolbarButton("?", OpenHelp, "Open contextual help if Help Browser is installed.", 28));
            return toolbar;
        }

        private Toolbar BuildAuthoringToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.style.minHeight = 34;

            TextField fontField = new TextField();
            fontField.value = _fontName;
            fontField.tooltip = "Font label for the editor session.";
            fontField.style.width = 112;
            fontField.RegisterValueChangedCallback(evt =>
            {
                _fontName = string.IsNullOrWhiteSpace(evt.newValue) ? "Default" : evt.newValue;
                UtilityWindowPrefs.SetString(PrefFontName, _fontName);
                _status = "Font label updated.";
                RefreshStatusChrome();
            });
            toolbar.Add(fontField);

            IntegerField fontSizeField = new IntegerField();
            fontSizeField.value = _fontSize;
            fontSizeField.tooltip = "Document canvas text size.";
            fontSizeField.style.width = 48;
            fontSizeField.RegisterValueChangedCallback(evt =>
            {
                _fontSize = Mathf.Clamp(evt.newValue, 9, 32);
                UtilityWindowPrefs.SetInt(PrefFontSize, _fontSize);
                RefreshDocumentArea();
            });
            toolbar.Add(fontSizeField);

            toolbar.Add(CreateToolbarButton("B", ApplyBold, "Apply bold styling to selected text, or insert bold text at the active line.", 30));
            toolbar.Add(CreateToolbarButton("I", ApplyItalic, "Apply italic styling to selected text, or insert italic text at the active line.", 30));
            ToolbarMenu headingMenu = new ToolbarMenu
            {
                text = "Heading",
                tooltip = "Apply a heading level to the active document line."
            };
            headingMenu.style.width = 78;
            headingMenu.menu.AppendAction("H1", _ => ApplyHeading(1));
            headingMenu.menu.AppendAction("H2", _ => ApplyHeading(2));
            headingMenu.menu.AppendAction("H3", _ => ApplyHeading(3));
            toolbar.Add(headingMenu);
            toolbar.Add(CreateToolbarButton("Formatting", ShowFormattingMenu, "Headings, quote/code styles, line numbers, semantic tint, annotation markers, and canvas density.", 82));
            toolbar.Add(CreateToolbarButton("Annotate", ShowAnnotateMenu, "Add comments or bookmarks at the active line.", 76));

            TextField tokenField = new TextField();
            tokenField.value = _tokenKey;
            tokenField.tooltip = "Token key to insert. Completed {tokenKey} text resolves to an inline chip in the document canvas.";
            tokenField.style.width = 124;
            tokenField.RegisterValueChangedCallback(evt =>
            {
                _tokenKey = PungentRichDocumentParser.NormalizeTokenKey(evt.newValue);
                UtilityWindowPrefs.SetString(PrefTokenKey, _tokenKey);
            });
            toolbar.Add(tokenField);

            toolbar.Add(CreateToolbarButton("Token", InsertToken, "Insert a token at the current document position.", 56));
            toolbar.Add(CreateToolbarButton("Insert...", ShowInsertMenu, "Open integration tray for dialogue, quest, tutorial, command, and game copy blocks.", 74));
            toolbar.Add(CreateToolbarButton("Docs", OpenDocumentationLinks, "Open Documentation Links if installed.", 48));
            toolbar.Add(CreateToolbarButton("Validator", OpenTokenValidator, "Open Token Validator if installed.", 74));
            toolbar.Add(CreateToolbarButton("Advanced...", ShowAdvancedMenu, "Raw/source and derived-block maintenance actions.", 86));

            return toolbar;
        }

        private VisualElement BuildFooter()
        {
            VisualElement footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.alignItems = Align.Center;
            footer.style.minHeight = 24;
            footer.style.paddingLeft = 8;
            footer.style.paddingRight = 8;
            footer.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.1f, 0.105f, 0.11f) : new Color(0.74f, 0.76f, 0.78f));

            _statusLabel = new Label(_status);
            _statusLabel.style.flexGrow = 1f;
            _statusLabel.style.fontSize = 11;
            footer.Add(_statusLabel);

            _validationPill = new Label(string.Empty);
            _validationPill.tooltip = "Click to inspect validation issues or run validation.";
            _validationPill.RegisterCallback<MouseDownEvent>(_ => ShowValidationMenu());
            ApplyPillStyle(_validationPill, UtilityWindowTheme.Neutral, 136);
            footer.Add(_validationPill);
            return footer;
        }

        private void RefreshDocumentArea()
        {
            if (_contentRoot == null)
                return;

            _contentRoot.Clear();
            _canvasView = null;
            _inlinePropagationView = null;
            _rawSourceField = null;
            _propertiesView = null;

            if (!string.IsNullOrWhiteSpace(PungentRichDocumentStorage.LastError))
                _contentRoot.Add(new HelpBox(PungentRichDocumentStorage.LastError, HelpBoxMessageType.Warning));

            if (_document == null)
            {
                _contentRoot.Add(CreateEmptyState());
                RefreshStatusChrome();
                return;
            }

            EnsureBodyTextAvailable(_document);

            VisualElement editorRow = new VisualElement();
            editorRow.style.flexDirection = FlexDirection.Row;
            editorRow.style.flexGrow = 1f;
            _contentRoot.Add(editorRow);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.paddingTop = 16;
            scroll.style.paddingBottom = 24;
            editorRow.Add(scroll);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.flexGrow = 1f;
            row.style.paddingLeft = 20;
            row.style.paddingRight = 20;
            scroll.Add(row);

            VisualElement page = CreatePageElement();
            row.Add(page);
            BuildDocumentHeader(page);

            if (_surfaceMode == RichDocumentSurfaceMode.Document)
            {
                _canvasView = new PungentRichDocumentDocumentCanvasView
                {
                    FontSize = _fontSize,
                    ShowLineNumbers = _showLineNumbers,
                    ShowAnnotationMarkers = _showAnnotationMarkers,
                    ShowSemanticTint = _showSemanticTint,
                    ShowTokenTechnicalText = _showTokenTechnicalText,
                    CompactLineSpacing = _compactLineSpacing,
                    ShowRawSyntaxHints = _showRawSyntaxHints,
                    OnDirty = MarkDirty,
                    OnStatus = SetStatus,
                    OnTokenReferenced = AddTokenTarget,
                    OnSelectionChanged = RefreshPropertiesTray,
                    OpenTokenValidator = OpenTokenValidator,
                    OpenDocumentationLinks = OpenDocumentationLinks,
                    TokenKeyProvider = () => _tokenKey
                };
                _canvasView.Bind(_document);
                page.Add(_canvasView);
            }
            else
            {
                BuildRawSourceEditor(page);
            }

            if (_showInlinePropagation && _surfaceMode == RichDocumentSurfaceMode.Document)
            {
                _inlinePropagationView = new PungentRichDocumentInlinePropagationView();
                _inlinePropagationView.Bind(_document, PrepareDocumentForRead, OpenPropagationPreview, SetStatus);
                editorRow.Add(_inlinePropagationView);
            }

            if (_showPropertiesTray)
            {
                _propertiesView = new PungentRichDocumentPropertiesView(ConvertSelectionToSemantic, AddAnnotationAtActiveLine, MarkDirty, SetStatus, CollapsePropertiesTray, ConvertSelectionToCustomInsertion, OpenPropagationPreview);
                editorRow.Add(_propertiesView);
            }
            else
            {
                editorRow.Add(CreateCollapsedPropertiesRail());
            }
            RefreshPropertiesTray();
            RefreshStatusChrome();
        }

        private VisualElement CreatePageElement()
        {
            VisualElement page = new VisualElement();
            page.style.width = Length.Percent(100);
            page.style.maxWidth = PageMaxWidth;
            page.style.minWidth = PageMinWidth;
            page.style.minHeight = 560;
            page.style.paddingLeft = 48;
            page.style.paddingRight = 48;
            page.style.paddingTop = 36;
            page.style.paddingBottom = 48;
            page.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.17f, 0.175f, 0.18f) : Color.white);
            page.style.borderTopWidth = 1;
            page.style.borderRightWidth = 1;
            page.style.borderBottomWidth = 1;
            page.style.borderLeftWidth = 1;
            page.style.borderTopColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));
            page.style.borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f));
            page.style.borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.24f));
            page.style.borderLeftColor = new StyleColor(new Color(0f, 0f, 0f, 0.18f));
            return page;
        }

        private VisualElement CreateCollapsedPropertiesRail()
        {
            VisualElement rail = new VisualElement();
            rail.style.width = 46;
            rail.style.flexShrink = 0;
            rail.style.alignItems = Align.Center;
            rail.style.paddingTop = 12;
            rail.style.borderLeftWidth = 1;
            rail.style.borderLeftColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.28f, 0.29f, 0.31f) : new Color(0.68f, 0.7f, 0.72f));
            rail.style.backgroundColor = new StyleColor(EditorGUIUtility.isProSkin ? new Color(0.13f, 0.135f, 0.14f) : new Color(0.84f, 0.855f, 0.87f));

            Button open = new Button(ExpandPropertiesTray)
            {
                text = ">",
                tooltip = "Show properties."
            };
            open.style.width = 28;
            open.style.height = 24;
            rail.Add(open);

            Label label = new Label("Properties");
            label.style.fontSize = 10;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = 8;
            label.style.whiteSpace = WhiteSpace.Normal;
            rail.Add(label);
            return rail;
        }

        private void BuildDocumentHeader(VisualElement page)
        {
            TextField title = new TextField();
            title.value = _document.title ?? string.Empty;
            title.tooltip = "Document title";
            title.style.fontSize = 26;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            title.RegisterValueChangedCallback(evt =>
            {
                _document.title = evt.newValue ?? string.Empty;
                MarkDirty("Unsaved changes");
            });
            page.Add(title);

            TextField summary = new TextField
            {
                multiline = true,
                value = _document.summary ?? string.Empty
            };
            summary.tooltip = "Short summary shown in the authoring browser.";
            summary.style.minHeight = 32;
            summary.style.marginBottom = 8;
            summary.RegisterValueChangedCallback(evt =>
            {
                _document.summary = evt.newValue ?? string.Empty;
                MarkDirty("Unsaved changes");
            });
            page.Add(summary);

            VisualElement metadata = new VisualElement();
            metadata.style.flexDirection = FlexDirection.Row;
            metadata.style.alignItems = Align.Center;
            metadata.style.marginBottom = 18;
            page.Add(metadata);

            metadata.Add(CreateMetadataPopup("Status", _document.status, StatusChoices, 118, next => _document.status = next));
            metadata.Add(CreateMetadataPopup("Priority", _document.priority, PriorityChoices, 104, next => _document.priority = next));
            TextField tags = CreateMetadataField("Tags", string.Join(", ", _document.tags ?? new List<string>()), 280, next => _document.tags = PungentRichDocumentEditorGUI.ParseTags(next));
            tags.style.flexGrow = 1f;
            tags.tooltip = "Comma-separated tags. These stay as metadata; use the document body for integrated game text.";
            metadata.Add(tags);

            Label wordCount = new Label(WordCountForDisplay() + " words");
            wordCount.style.marginLeft = 8;
            wordCount.style.color = EditorGUIUtility.isProSkin ? new Color(0.72f, 0.74f, 0.76f) : new Color(0.34f, 0.35f, 0.36f);
            metadata.Add(wordCount);
        }

        private TextField CreateMetadataField(string label, string value, float width, Action<string> setter)
        {
            TextField field = new TextField(label);
            field.value = value ?? string.Empty;
            field.style.width = width;
            field.style.marginRight = 8;
            field.RegisterValueChangedCallback(evt =>
            {
                setter?.Invoke(evt.newValue ?? string.Empty);
                MarkDirty("Unsaved changes");
            });
            return field;
        }

        private PopupField<string> CreateMetadataPopup(string label, string value, List<string> baseChoices, float width, Action<string> setter)
        {
            List<string> choices = new List<string>(baseChoices ?? new List<string>());
            string current = string.IsNullOrWhiteSpace(value) ? (choices.Count == 0 ? string.Empty : choices[0]) : value.Trim();
            if (!choices.Any(choice => string.Equals(choice, current, StringComparison.OrdinalIgnoreCase)))
                choices.Add(current);

            PopupField<string> popup = new PopupField<string>(label, choices, current);
            popup.style.width = width;
            popup.style.marginRight = 8;
            popup.tooltip = label + " metadata. This affects browser/provider metadata, not document text.";
            popup.RegisterValueChangedCallback(evt =>
            {
                setter?.Invoke(evt.newValue ?? string.Empty);
                MarkDirty("Unsaved metadata changes");
            });
            return popup;
        }

        private void BuildRawSourceEditor(VisualElement page)
        {
            HelpBox help = new HelpBox("Raw Source is for manual repair and paste-in control. Switch back to Document to render chips and embedded integrations.", HelpBoxMessageType.Info);
            help.style.marginBottom = 8;
            page.Add(help);

            _rawSourceField = new TextField
            {
                multiline = true,
                value = _document.bodyText ?? string.Empty
            };
            _rawSourceField.style.minHeight = 440;
            _rawSourceField.style.fontSize = _fontSize;
            _rawSourceField.style.flexGrow = 1f;
            _rawSourceField.RegisterValueChangedCallback(evt =>
            {
                _document.bodyText = evt.newValue ?? string.Empty;
                MarkDirty("Unsaved raw source changes");
            });
            page.Add(_rawSourceField);
        }

        private VisualElement CreateEmptyState()
        {
            VisualElement outer = new VisualElement();
            outer.style.flexGrow = 1f;
            outer.style.justifyContent = Justify.Center;
            outer.style.alignItems = Align.Center;

            VisualElement page = CreatePageElement();
            page.style.width = Mathf.Clamp(position.width - 120f, PageMinWidth, PageMaxWidth);
            page.style.minHeight = 360;
            outer.Add(page);

            Label title = new Label("Rich Document Editor");
            title.style.fontSize = 24;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            page.Add(title);

            Label body = new Label("Create or open a rich document from the File menu. Sticky Notes is for quick contextual notes; this surface is the notebook.");
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginBottom = 18;
            page.Add(body);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            page.Add(buttons);
            buttons.Add(new Button(() => CreateFromTemplate("general-document")) { text = "New Document" });
            buttons.Add(new Button(ShowTemplateMenu) { text = "Template..." });
            buttons.Add(new Button(OpenStickyNotes) { text = "Open Sticky Notes" });
            return outer;
        }

        private ToolbarButton CreateToolbarButton(string text, Action action, string tooltip, float width)
        {
            ToolbarButton button = new ToolbarButton(action)
            {
                text = text,
                tooltip = tooltip
            };
            if (width > 0f)
                button.style.width = width;
            return button;
        }

        private void ShowTemplateMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentRichDocumentTemplateDefinition template in PungentRichDocumentTemplates.All)
            {
                string id = template.id;
                menu.AddItem(new GUIContent(template.displayName), false, () => CreateFromTemplate(id));
            }

            menu.ShowAsContext();
        }

        private void ShowRecentDocumentMenu()
        {
            GenericMenu menu = new GenericMenu();
            List<PungentRichDocument> documents = PungentRichDocumentStorage.Database.Documents
                .Where(document => document != null && !document.archived)
                .OrderByDescending(document => document.updatedUtc, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            if (documents.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No rich documents yet"));
            }
            else
            {
                foreach (PungentRichDocument document in documents)
                {
                    string id = document.id;
                    bool selected = _document != null && PungentAuthoringId.EqualsId(_document.id, id);
                    menu.AddItem(new GUIContent(string.IsNullOrWhiteSpace(document.title) ? "Untitled Document" : document.title), selected, () => SelectDocument(id));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Sticky Notes"), false, OpenStickyNotes);
            menu.ShowAsContext();
        }

        private void ShowFileMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (PungentRichDocumentTemplateDefinition template in PungentRichDocumentTemplates.All)
            {
                string id = template.id;
                menu.AddItem(new GUIContent("New/" + template.displayName), false, () => CreateFromTemplate(id));
            }

            List<PungentRichDocument> documents = PungentRichDocumentStorage.Database.Documents
                .Where(document => document != null && !document.archived)
                .OrderBy(document => string.IsNullOrWhiteSpace(document.title) ? "Untitled Document" : document.title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            menu.AddSeparator(string.Empty);
            if (documents.Count == 0)
                menu.AddDisabledItem(new GUIContent("Open/No rich documents yet"));
            else
            {
                foreach (PungentRichDocument document in documents)
                {
                    string id = document.id;
                    bool selected = _document != null && PungentAuthoringId.EqualsId(_document.id, id);
                    menu.AddItem(new GUIContent("Open/" + (string.IsNullOrWhiteSpace(document.title) ? "Untitled Document" : document.title)), selected, () => SelectDocument(id));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Save"), false, () => SaveDirty("Saved document.", false));
            if (_document != null)
            {
                menu.AddItem(new GUIContent("Duplicate Document"), false, DuplicateCurrentDocument);
                menu.AddItem(new GUIContent("Rename Document (Edit Title Field)"), false, FocusDocumentTitle);
                menu.AddItem(new GUIContent("Copy Document ID"), false, () => EditorGUIUtility.systemCopyBuffer = _document.id);
                menu.AddItem(new GUIContent(_document.archived ? "Unarchive Document" : "Archive Document"), false, ToggleCurrentDocumentArchived);
                menu.AddItem(new GUIContent("Delete Document..."), false, DeleteCurrentDocument);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Duplicate Document"));
                menu.AddDisabledItem(new GUIContent("Rename Document (Edit Title Field)"));
                menu.AddDisabledItem(new GUIContent("Copy Document ID"));
                menu.AddDisabledItem(new GUIContent("Archive Document"));
                menu.AddDisabledItem(new GUIContent("Delete Document..."));
            }

            menu.AddSeparator(string.Empty);
            AddCreateFromStickyNoteMenu(menu);
            menu.AddItem(new GUIContent("Open Sticky Notes"), false, OpenStickyNotes);
            // RDE/STICKY-NOTES MIGRATION NOTE: Rich Document file actions own notebook-style handoffs; do not rebuild the old Notes & Roadmap browser here.
            menu.ShowAsContext();
        }

        private void AddCreateFromStickyNoteMenu(GenericMenu menu)
        {
            PungentNoteStorage.EnsureLoaded();
            List<PungentNote> notes = PungentNoteStorage.Database.notes
                .Where(note => note != null && !note.archived)
                .OrderByDescending(note => note.updatedUtc, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            if (notes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("Create From Sticky Note/No sticky notes available"));
                return;
            }

            foreach (PungentNote note in notes)
            {
                PungentNote captured = note;
                string label = string.IsNullOrWhiteSpace(captured.title) ? "Untitled Sticky Note" : captured.title;
                menu.AddItem(new GUIContent("Create From Sticky Note/" + label), false, () => CreateLinkedDocumentFromStickyNote(captured));
            }
        }

        private void CreateLinkedDocumentFromStickyNote(PungentNote note)
        {
            SaveDirty("Saved before creating a linked rich document.", false);
            if (!PungentRichDocumentNoteBridge.TryCreateLinkedCopy(note, out PungentRichDocument document, out string error, true))
            {
                _status = string.IsNullOrWhiteSpace(error) ? "Could not create linked rich document." : error;
                RefreshStatusChrome();
                return;
            }

            _document = document;
            _selectedDocumentId = document.id;
            _surfaceMode = RichDocumentSurfaceMode.Document;
            _dirty = false;
            _saveState = "Saved";
            SavePrefs();
            RefreshDocumentArea();
            _status = "Created linked rich document. Source sticky note was not changed.";
            RefreshStatusChrome();
        }

        private void DuplicateCurrentDocument()
        {
            if (_document == null)
                return;

            SaveDirty("Saved before duplicating document.", false);
            PungentRichDocument copy = JsonUtility.FromJson<PungentRichDocument>(JsonUtility.ToJson(_document));
            copy.id = PungentAuthoringId.NewValue();
            copy.title = (string.IsNullOrWhiteSpace(_document.title) ? "Untitled Document" : _document.title.Trim()) + " Copy";
            string now = DateTime.UtcNow.ToString("o");
            copy.createdUtc = now;
            copy.updatedUtc = now;
            copy.NormalizeInPlace();
            PungentRichDocumentStorage.Database.AddOrUpdate(copy);
            _document = copy;
            _selectedDocumentId = copy.id;
            MarkDirty("Duplicated document.");
            SaveDirty("Saved duplicated document.", false);
            RefreshDocumentArea();
        }

        private void FocusDocumentTitle()
        {
            _status = "Edit the document title at the top of the page.";
            RefreshStatusChrome();
        }

        private void ToggleCurrentDocumentArchived()
        {
            if (_document == null)
                return;

            _document.archived = !_document.archived;
            MarkDirty(_document.archived ? "Archived document." : "Unarchived document.");
            SaveDirty("Saved archive state.", false);
            RefreshDocumentArea();
        }

        private void DeleteCurrentDocument()
        {
            if (_document == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete Rich Document", "Delete '" + _document.title + "'? This only removes the rich document storage record.", "Delete", "Cancel"))
                return;

            string deletedId = _document.id;
            PungentRichDocumentStorage.Database.Delete(deletedId);
            PungentRichDocumentStorage.Save(out string error);
            _document = null;
            _selectedDocumentId = string.Empty;
            _dirty = false;
            _saveState = string.IsNullOrWhiteSpace(error) ? "Saved" : "Save warning";
            _status = string.IsNullOrWhiteSpace(error) ? "Deleted rich document." : error;
            SavePrefs();
            RefreshDocumentArea();
        }

        private void ShowFormattingMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Settings/Show Line Numbers"), _showLineNumbers, () => ToggleFormattingSetting(ref _showLineNumbers, PrefShowLineNumbers, "Line numbers " + (!_showLineNumbers ? "shown." : "hidden.")));
            menu.AddItem(new GUIContent("Settings/Show Annotation Markers"), _showAnnotationMarkers, () => ToggleFormattingSetting(ref _showAnnotationMarkers, PrefShowAnnotationMarkers, "Annotation markers " + (!_showAnnotationMarkers ? "shown." : "hidden.")));
            menu.AddItem(new GUIContent("Settings/Show Semantic Tint"), _showSemanticTint, () => ToggleFormattingSetting(ref _showSemanticTint, PrefShowSemanticTint, "Semantic tint " + (!_showSemanticTint ? "shown." : "muted.")));
            menu.AddItem(new GUIContent("Settings/Show Token Technical Text"), _showTokenTechnicalText, () => ToggleFormattingSetting(ref _showTokenTechnicalText, PrefShowTokenTechnicalText, "Token technical text " + (!_showTokenTechnicalText ? "shown." : "shown in tooltips only.")));
            menu.AddItem(new GUIContent("Settings/Compact Line Spacing"), _compactLineSpacing, () => ToggleFormattingSetting(ref _compactLineSpacing, PrefCompactLineSpacing, "Line spacing set to " + (!_compactLineSpacing ? "compact." : "comfortable.")));
            menu.AddItem(new GUIContent("Settings/Show Script Syntax Hints (Raw Markers)"), _showRawSyntaxHints, ToggleRawSyntaxHints);
            menu.AddItem(new GUIContent("Settings/Reset Editor Formatting"), false, ResetEditorFormatting);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Heading/H1"), false, () => ApplyHeading(1));
            menu.AddItem(new GUIContent("Heading/H2"), false, () => ApplyHeading(2));
            menu.AddItem(new GUIContent("Heading/H3"), false, () => ApplyHeading(3));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Quote"), false, ApplyQuote);
            menu.AddItem(new GUIContent("Code Block"), false, ApplyCodeBlock);
            menu.AddItem(new GUIContent("Divider"), false, ApplyDivider);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Colour/Red"), false, () => InsertRawSnippet("<color=#" + ColorUtility.ToHtmlStringRGB(UtilityWindowTheme.Red) + ">coloured text</color>"));
            menu.AddItem(new GUIContent("Colour/Blue"), false, () => InsertRawSnippet("<color=#" + ColorUtility.ToHtmlStringRGB(UtilityWindowTheme.Cyan) + ">coloured text</color>"));
            menu.AddItem(new GUIContent("Align/Center"), false, () => InsertRawSnippet("<align=center>Text</align>"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Help/Formatting Help"), false, () => OpenHelp("formatting", "formatting"));
            menu.AddItem(new GUIContent("Help/Semantic Spans And Bindings"), false, () => OpenHelp("semantic-spans", "semantic-spans"));
            menu.AddItem(new GUIContent("Help/Propagation Preview"), false, () => OpenHelp("propagation-preview", "propagation-preview"));
            menu.ShowAsContext();
        }

        private void ShowAnnotateMenu()
        {
            GenericMenu menu = new GenericMenu();
            bool hasDocument = _document != null && _surfaceMode == RichDocumentSurfaceMode.Document;
            if (hasDocument)
            {
                menu.AddItem(new GUIContent("Add Comment At Active Line"), false, () => AddAnnotationAtActiveLine(PungentRichDocumentAnnotationKind.Comment));
                menu.AddItem(new GUIContent("Add Bookmark At Active Line"), false, () => AddAnnotationAtActiveLine(PungentRichDocumentAnnotationKind.Bookmark));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Add Comment At Active Line"));
                menu.AddDisabledItem(new GUIContent("Add Bookmark At Active Line"));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Show Annotation Markers"), _showAnnotationMarkers, () => ToggleFormattingSetting(ref _showAnnotationMarkers, PrefShowAnnotationMarkers, "Annotation markers " + (!_showAnnotationMarkers ? "shown." : "hidden.")));
            menu.AddItem(new GUIContent(_showPropertiesTray ? "Collapse Properties" : "Show Properties"), false, TogglePropertiesTray);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Help/Comments And Bookmarks"), false, () => OpenHelp("annotations", "annotations"));
            menu.ShowAsContext();
        }

        private void ToggleFormattingSetting(ref bool field, string prefKey, string status)
        {
            field = !field;
            UtilityWindowPrefs.SetBool(prefKey, field);
            _status = status;
            RefreshDocumentArea();
        }

        private void ToggleRawSyntaxHints()
        {
            _showRawSyntaxHints = !_showRawSyntaxHints;
            UtilityWindowPrefs.SetBool(PrefShowRawSyntaxHints, _showRawSyntaxHints);
            _status = _showRawSyntaxHints
                ? "Script syntax hints shown, for example Yarn options, Ink choices, tags, knots, and commands."
                : "Script syntax hints hidden; semantic styling remains visible.";
            RefreshDocumentArea();
        }

        private void ResetEditorFormatting()
        {
            _showLineNumbers = false;
            _showAnnotationMarkers = true;
            _showSemanticTint = true;
            _showTokenTechnicalText = false;
            _compactLineSpacing = false;
            _showRawSyntaxHints = false;
            SavePrefs();
            _status = "Editor formatting reset.";
            RefreshDocumentArea();
        }

        private void ShowInsertMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Dialogue/Speaker Line"), false, () => InsertIntegration(PungentRichDocumentStreamNodeKind.DialogueLine));
            menu.AddItem(new GUIContent("Dialogue/Choice"), false, () => InsertIntegration(PungentRichDocumentStreamNodeKind.DialogueChoice));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Script Flow/Yarn Option"), false, () => InsertRawSnippet("-> Speaker: Choice text"));
            menu.AddItem(new GUIContent("Script Flow/Yarn Command"), false, () => InsertRawSnippet("<<command args>>"));
            menu.AddItem(new GUIContent("Script Flow/Ink Choice"), false, () => InsertRawSnippet("* Choice text"));
            menu.AddItem(new GUIContent("Script Flow/Ink Knot"), false, () => InsertRawSnippet("=== knot_name ==="));
            menu.AddItem(new GUIContent("Script Flow/Ink Divert"), false, () => InsertRawSnippet("-> knot_name"));
            menu.AddItem(new GUIContent("Script Flow/Ink Tag"), false, () => InsertRawSnippet("Line text #tag"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Quest/Objective"), false, () => InsertIntegration(PungentRichDocumentStreamNodeKind.QuestObjective));
            menu.AddItem(new GUIContent("Tutorial/Step"), false, () => InsertIntegration(PungentRichDocumentStreamNodeKind.TutorialStep));
            menu.AddItem(new GUIContent("Command Placeholder"), false, () => InsertIntegration(PungentRichDocumentStreamNodeKind.CommandPlaceholder));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Checklist/Checklist Definition Metadata"), false, () => InsertRawSnippet("Checklist ID: example-checklist\nTarget Utility ID: qa-checklist-utility\nList Kind: quality-gate\nState Profile: quality-gate.pass-partial-fail\nTags: example, checklist"));
            menu.AddItem(new GUIContent("Checklist/Unchecked Item"), false, () => InsertRawSnippet("- [ ] Checklist item"));
            menu.AddItem(new GUIContent("Checklist/Item With Metadata"), false, () => InsertRawSnippet("- [ ] Checklist item | owner:Me | priority:High | due:2026-06-01 | optional:false"));
            menu.AddSeparator("Checklist/");
            menu.AddItem(new GUIContent("Checklist/Templates/QA Checklist"), false, () => InsertChecklistTemplate("checklist-definition"));
            menu.AddItem(new GUIContent("Checklist/Templates/To-Do List"), false, () => InsertChecklistTemplate("checklist-to-do-list"));
            menu.AddItem(new GUIContent("Checklist/Templates/Review Checklist"), false, () => InsertChecklistTemplate("checklist-review"));
            menu.AddItem(new GUIContent("Checklist/Templates/Release Readiness"), false, () => InsertChecklistTemplate("checklist-release-readiness"));
            menu.AddItem(new GUIContent("Checklist/Templates/Migration List"), false, () => InsertChecklistTemplate("checklist-migration"));
            menu.AddItem(new GUIContent("Checklist/Templates/Bug Triage List"), false, () => InsertChecklistTemplate("checklist-bug-triage"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Game Copy/Hint"), false, () => InsertGameCopy("Hint", "Loading screen hint text."));
            menu.AddItem(new GUIContent("Game Copy/Item Description"), false, () => InsertGameCopy("Item", "{itemName}: Item description text."));
            menu.AddItem(new GUIContent("Game Copy/Character Description"), false, () => InsertGameCopy("Character", "{characterName}: Character description text."));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Custom/Custom Chip"), false, () => InsertRawSnippet("{chip:Stage Direction}"));
            menu.AddItem(new GUIContent("Custom/Reference Token"), false, () => InsertRawSnippet("{ref Target:Component:Field}"));
            menu.AddItem(new GUIContent("Custom/Create From Current Selection"), false, CreateCustomInsertionFromSelection);
            foreach (PungentRichDocumentInsertionDefinition definition in PungentRichDocumentInsertionDefinitionRegistry.AllDefinitions.Where(definition => definition != null && !definition.builtIn))
            {
                string label = string.IsNullOrWhiteSpace(definition.category)
                    ? "Custom/" + definition.displayName
                    : "Custom/" + definition.category + "/" + definition.displayName;
                menu.AddItem(new GUIContent(label), false, () => InsertCustomDefinition(definition));
            }
            menu.AddItem(new GUIContent("Custom/Manage Insertions"), false, PungentRichDocumentInsertionDesignerWindow.Open);
            menu.ShowAsContext();
        }

        private void ShowAdvancedMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Sync Raw Source To Document"), false, SyncRawToDocument);
            menu.AddItem(new GUIContent("Derive Blocks From Source"), false, DeriveBlocksFromBody);
            menu.AddItem(new GUIContent("Copy Raw Source"), false, () => EditorGUIUtility.systemCopyBuffer = _document == null ? string.Empty : _document.bodyText ?? string.Empty);
            menu.AddSeparator(string.Empty);
            if (_document != null)
            {
                menu.AddItem(new GUIContent("Checklists/Create Or Update Checklist Definition"), false, CreateOrUpdateChecklistDefinition);
                menu.AddItem(new GUIContent("Checklists/Validate Checklist Definition"), false, ValidateChecklistDefinition);
                menu.AddItem(new GUIContent("Checklists/Open Linked Checklist"), false, OpenLinkedChecklist);
                menu.AddItem(new GUIContent("Checklists/Copy Definition JSON"), false, CopyChecklistDefinitionJson);
                menu.AddItem(new GUIContent("Checklists/Copy Linked Results Markdown"), false, CopyLinkedChecklistResultsMarkdown);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Checklists/Create Or Update Checklist Definition"));
                menu.AddDisabledItem(new GUIContent("Checklists/Validate Checklist Definition"));
                menu.AddDisabledItem(new GUIContent("Checklists/Open Linked Checklist"));
                menu.AddDisabledItem(new GUIContent("Checklists/Copy Definition JSON"));
                menu.AddDisabledItem(new GUIContent("Checklists/Copy Linked Results Markdown"));
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Bindings/Copy Shared Adapter Template"), false, CopyBindingAdapterTemplate);
            menu.AddItem(new GUIContent("Bindings/Open Propagation Preview"), false, OpenPropagationPreview);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Insertions/Manage Custom Insertions"), false, PungentRichDocumentInsertionDesignerWindow.Open);
            menu.AddItem(new GUIContent("Insertions/Create From Current Selection"), false, CreateCustomInsertionFromSelection);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Help/Binding Adapter Help"), false, () => OpenHelp("binding", "binding"));
            menu.ShowAsContext();
        }

        private void ShowValidationMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Run Local Validation"), false, RunValidation);
            menu.AddItem(new GUIContent("What This Checks..."), false, ShowValidationGuidance);

            if (_lastValidation == null || _lastValidation.status == PungentAuthoringValidationStatus.NotRun)
            {
                menu.AddDisabledItem(new GUIContent("No validation result yet"));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Status/" + _lastValidation.status));
                if (_lastValidation.issues == null || _lastValidation.issues.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("Issues/None"));
                }
                else
                {
                    foreach (PungentAuthoringValidationIssue issue in _lastValidation.issues.Where(issue => issue != null))
                    {
                        string label = "Issues/" + issue.severity + "/" + (string.IsNullOrWhiteSpace(issue.message) ? issue.issueCode : issue.message);
                        menu.AddDisabledItem(new GUIContent(label));
                    }
                }

                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Copy Validation Summary"), false, CopyValidationSummary);
            }

            menu.ShowAsContext();
        }

        private void ShowValidationGuidance()
        {
            string message =
                "Rich Document local validation checks only the current document." + Environment.NewLine + Environment.NewLine +
                "- Missing title" + Environment.NewLine +
                "- Empty body" + Environment.NewLine +
                "- Missing or stale target references" + Environment.NewLine +
                "- Invalid token syntax, for example {bad token!}" + Environment.NewLine +
                "- Empty speaker lines, choices, objectives, and commands" + Environment.NewLine +
                "- Broken or unsupported semantic bindings" + Environment.NewLine + Environment.NewLine +
                "It does not scan the project, mutate tokens, or apply propagation.";
            EditorUtility.DisplayDialog("Rich Document Validation", message, "OK");
        }

        private void CopyValidationSummary()
        {
            if (_lastValidation == null)
                return;

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("Rich Document Validation");
            builder.AppendLine("Status: " + _lastValidation.status);
            foreach (PungentAuthoringValidationIssue issue in _lastValidation.issues ?? new List<PungentAuthoringValidationIssue>())
                if (issue != null)
                    builder.AppendLine(issue.severity + ": " + issue.message);
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
            _status = "Copied validation summary.";
            RefreshStatusChrome();
        }

        private void CopyBindingAdapterTemplate()
        {
            EditorGUIUtility.systemCopyBuffer =
@"using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

// RDE/GUIDED-BINDING MIGRATION NOTE: Register project-specific endpoints with the shared Authoring binding layer
// so Rich Documents, Data Sheets, and BoardGraph can all use the same guided Object -> Component -> Endpoint picker.
[InitializeOnLoad]
public sealed class MyProjectAuthoringBindingAdapter :
    IPungentAuthoringBindingAdapter,
    IPungentAuthoringBindingPreviewAdapter,
    IPungentAuthoringBindingApplyAdapter
{
    private const string AdapterId = ""my-project-authoring-bindings"";

    static MyProjectAuthoringBindingAdapter()
    {
        PungentAuthoringBindingAdapterRegistry.Register(new MyProjectAuthoringBindingAdapter());
    }

    public string Id => AdapterId;
    public string DisplayName => ""My Project Bindings"";

    public IEnumerable<PungentAuthoringBindingEndpoint> GetEndpoints(UnityEngine.Object targetObject)
    {
        // Inspect only targetObject or components explicitly selected by the user.
        // Do not call AssetDatabase.FindAssets, scene searches, or broad reflection scans here.
        yield break;
    }

    public bool TryPreview(PungentAuthoringTarget target, out PungentAuthoringBindingPreview preview)
    {
        preview = new PungentAuthoringBindingPreview
        {
            adapterId = Id,
            adapterDisplayName = DisplayName,
            disabledReason = ""Preview is not implemented.""
        };
        return false;
    }

    public bool CanApply(PungentAuthoringTarget target, out string disabledReason)
    {
        disabledReason = ""Apply is not implemented."";
        return false;
    }

    public PungentAuthoringBindingApplyResult Apply(PungentAuthoringTarget target, string value, string undoName = null)
    {
        return new PungentAuthoringBindingApplyResult
        {
            adapterId = Id,
            adapterDisplayName = DisplayName,
            message = ""Apply is not implemented.""
        };
    }
}";
            _status = "Copied a shared authoring binding adapter template.";
            RefreshStatusChrome();
        }

        private void CreateFromTemplate(string templateId)
        {
            SaveDirty("Saved before creating a new document.", false);
            PungentRichDocument document = PungentRichDocumentTemplates.CreateDocument(templateId);
            PungentRichDocumentBlockSync.SyncRawToBlocks(document);
            _document = document;
            _selectedDocumentId = document.id;
            _surfaceMode = RichDocumentSurfaceMode.Document;
            _lastValidation = null;
            MarkDirty("Created " + document.title + ". Save when ready.");
            SavePrefs();
            RefreshDocumentArea();
        }

        private void InsertChecklistTemplate(string templateId)
        {
            PungentRichDocumentTemplateDefinition template = PungentRichDocumentTemplates.Find(templateId);
            string body = template != null && template.bodyFactory != null ? template.bodyFactory.Invoke() : string.Empty;
            if (string.IsNullOrWhiteSpace(body))
                return;

            InsertRawSnippet(Environment.NewLine + Environment.NewLine + body);
        }

        private void ValidateChecklistDefinition()
        {
            if (PungentRichDocumentChecklistBridge.TryBuildChecklistDefinition(_document, out PungentChecklistDefinition checklist, out _, out string error))
            {
                int sectionCount = checklist.sections == null ? 0 : checklist.sections.Count;
                int itemCount = PungentChecklistSerialization.EnumerateItems(checklist).Count();
                _status = "Checklist definition valid: " + checklist.checklistId + " (" + sectionCount + " sections, " + itemCount + " items).";
                RefreshStatusChrome();
                return;
            }

            _status = "Checklist definition invalid: " + error;
            RefreshStatusChrome();
            EditorUtility.DisplayDialog("Validate Checklist Definition", error, "OK");
        }

        private void CreateOrUpdateChecklistDefinition()
        {
            SaveDirty("Saved before creating checklist definition.", false);
            if (PungentRichDocumentChecklistBridge.CreateOrUpdateChecklistDefinition(_document, out string checklistId, out string error))
            {
                SaveDirty("Created/updated checklist definition '" + checklistId + "'.", false);
                PungentChecklistUtilityWindow.OpenChecklist(checklistId);
                return;
            }

            _status = "Checklist definition not saved: " + error;
            RefreshStatusChrome();
            EditorUtility.DisplayDialog("Create Or Update Checklist Definition", error, "OK");
        }

        private void OpenLinkedChecklist()
        {
            PungentAuthoringReference reference = _document?.references == null
                ? null
                : _document.references.FirstOrDefault(item => item != null && item.itemKind == PungentAuthoringItemKind.Checklist && !string.IsNullOrWhiteSpace(item.itemId));
            if (reference == null)
            {
                _status = "No linked checklist reference on this document.";
                RefreshStatusChrome();
                return;
            }

            PungentChecklistUtilityWindow.OpenChecklist(reference.itemId);
        }

        private void CopyChecklistDefinitionJson()
        {
            if (!PungentRichDocumentChecklistBridge.TryBuildChecklistDefinition(_document, out PungentChecklistDefinition checklist, out _, out string error))
            {
                _status = "Checklist definition not copied: " + error;
                RefreshStatusChrome();
                EditorUtility.DisplayDialog("Copy Checklist Definition JSON", error, "OK");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = PungentChecklistSerialization.ToJson(checklist, true);
            _status = "Copied checklist definition JSON for '" + checklist.checklistId + "'.";
            RefreshStatusChrome();
        }

        private void CopyLinkedChecklistResultsMarkdown()
        {
            PungentChecklistDefinition checklist;
            string error;
            if (!TryFindLinkedChecklist(out checklist, out error))
            {
                _status = "Checklist results not copied: " + error;
                RefreshStatusChrome();
                EditorUtility.DisplayDialog("Copy Checklist Results Markdown", error, "OK");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = BuildChecklistResultsMarkdown(checklist);
            _status = "Copied checklist results Markdown for '" + checklist.checklistId + "'.";
            RefreshStatusChrome();
        }

        private bool TryFindLinkedChecklist(out PungentChecklistDefinition checklist, out string error)
        {
            checklist = null;
            error = string.Empty;
            PungentAuthoringReference reference = _document?.references == null
                ? null
                : _document.references.FirstOrDefault(item => item != null && item.itemKind == PungentAuthoringItemKind.Checklist && !string.IsNullOrWhiteSpace(item.itemId));
            if (reference == null)
            {
                error = "No linked checklist reference on this document.";
                return false;
            }

            checklist = PungentChecklistDefinitionRegistry.Find(reference.itemId);
            if (checklist == null)
            {
                error = "Linked checklist '" + reference.itemId + "' could not be found.";
                return false;
            }

            return true;
        }

        private static string BuildChecklistResultsMarkdown(PungentChecklistDefinition checklist)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("# " + (string.IsNullOrWhiteSpace(checklist.title) ? checklist.checklistId : checklist.title));
            builder.AppendLine();
            builder.AppendLine("- Checklist ID: " + checklist.checklistId);
            builder.AppendLine("- List Kind: " + PungentChecklistListKinds.GetDisplayName(checklist.listKind));
            builder.AppendLine("- State Profile: " + PungentChecklistProfiles.ResolveProfile(checklist).label);
            builder.AppendLine();

            foreach (PungentChecklistSectionDefinition section in checklist.sections ?? new List<PungentChecklistSectionDefinition>())
            {
                if (section == null)
                    continue;

                builder.AppendLine("## " + (string.IsNullOrWhiteSpace(section.title) ? section.id : section.title));
                foreach (PungentChecklistItemDefinition item in section.items ?? new List<PungentChecklistItemDefinition>())
                {
                    if (item == null)
                        continue;

                    string stateId = PungentChecklistUtilityStateService.GetStateId(checklist, item, PungentChecklistDefinitionRegistry.Find);
                    PungentChecklistStateOptionDefinition state = PungentChecklistProfiles.ResolveState(checklist, stateId);
                    builder.AppendLine("- [" + state.shortLabel + "] " + item.label);
                    string comment = PungentChecklistUtilityStateService.GetComment(checklist, item.id);
                    if (!string.IsNullOrWhiteSpace(comment))
                        builder.AppendLine("  - Comment: " + comment.Replace(Environment.NewLine, " "));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private void SelectInitialDocument()
        {
            PungentRichDocumentStorage.Database.EnsureDefaults();
            _document = string.IsNullOrWhiteSpace(_selectedDocumentId) ? null : PungentRichDocumentStorage.Database.Find(_selectedDocumentId);
            _selectedDocumentId = _document == null ? string.Empty : _document.id;
            if (_document != null)
                EnsureBodyTextAvailable(_document);
            _dirty = false;
            _saveState = "Saved";
        }

        private void SelectDocument(string documentId)
        {
            if (string.IsNullOrWhiteSpace(documentId) || (_document != null && PungentAuthoringId.EqualsId(_document.id, documentId)))
                return;

            SaveDirty("Saved before switching document.", false);
            _document = PungentRichDocumentStorage.Database.Find(documentId);
            if (_document != null)
                EnsureBodyTextAvailable(_document);
            _selectedDocumentId = _document == null ? string.Empty : _document.id;
            _dirty = false;
            _saveState = "Saved";
            _lastValidation = null;
            SavePrefs();
            RefreshDocumentArea();
        }

        private void InsertRawSnippet(string rawSnippet)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource(rawSnippet);
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.InsertRawSnippet(rawSnippet);
        }

        private void ApplyBold()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("**bold text**");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyBold();
        }

        private void ApplyItalic()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("_italic text_");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyItalic();
        }

        private void ApplyHeading(int level)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource(new string('#', Mathf.Clamp(level, 1, 6)) + " Heading");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyHeading(level);
        }

        private void ApplyQuote()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("> Quote");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyQuote();
        }

        private void ApplyCodeBlock()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("```" + Environment.NewLine + "code" + Environment.NewLine + "```");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyCodeBlock();
        }

        private void ApplyDivider()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("---");
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ApplyDivider();
        }

        private void InsertIntegration(PungentRichDocumentStreamNodeKind kind)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource(PungentRichDocumentDocumentCanvasParser.CreateIntegration(kind).ToRawText());
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.InsertIntegration(kind);
        }

        private void InsertGameCopy(string label, string text)
        {
            if (!EnsureDocumentForInsert())
                return;

            PungentRichDocumentCanvasSegment node = PungentRichDocumentDocumentCanvasParser.CreateIntegration(PungentRichDocumentStreamNodeKind.GameCopy, text);
            node.label = label;
            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource(node.ToRawText());
            }
            else
            {
                if (_canvasView == null)
                    RefreshDocumentArea();
                _canvasView?.InsertRawSnippet(node.ToRawText());
            }
        }

        private void InsertCustomDefinition(PungentRichDocumentInsertionDefinition definition)
        {
            if (definition == null)
                return;

            if (!EnsureDocumentForInsert())
                return;

            if (definition.renderMode == PungentRichDocumentInsertionRenderMode.InlineChip)
            {
                InsertRawSnippet("{chip:" + definition.syntaxAlias + "}");
                return;
            }

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource(PungentRichDocumentInsertionDefinitionRegistry.BuildCustomInsertionRaw(definition));
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.InsertCustomInsertion(definition);
        }

        private void CreateCustomInsertionFromSelection()
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode != RichDocumentSurfaceMode.Document)
            {
                _status = "Switch to Document mode to capture selected text or elements as a custom insertion.";
                RefreshStatusChrome();
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.CreateCustomInsertionFromSelection();
        }

        private bool EnsureDocumentForInsert()
        {
            if (_document != null)
                return true;

            CreateFromTemplate("general-document");
            return _document != null;
        }

        private void InsertToken()
        {
            string key = PungentRichDocumentParser.NormalizeTokenKey(_tokenKey);
            if (string.IsNullOrWhiteSpace(key))
                key = "tokenKey";

            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.RawSource)
            {
                AppendRawSource("{" + key + "}");
            }
            else
            {
                if (_canvasView == null)
                    RefreshDocumentArea();
                _canvasView?.InsertToken(key);
            }

            AddTokenTarget(key);
            _status = "Inserted token {" + key + "}.";
            RefreshStatusChrome();
        }

        private void ConvertSelectionToSemantic(PungentRichDocumentSemanticKind kind)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode != RichDocumentSurfaceMode.Document)
            {
                _status = "Switch to Document mode to mark selected text as an integration.";
                RefreshStatusChrome();
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ConvertSelectionToSemantic(kind);
            RefreshPropertiesTray();
        }

        private void ConvertSelectionToCustomInsertion(PungentRichDocumentInsertionDefinition definition)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode != RichDocumentSurfaceMode.Document)
            {
                _status = "Switch to Document mode to apply custom insertions to selected text.";
                RefreshStatusChrome();
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();
            _canvasView?.ConvertSelectionToCustomInsertion(definition);
            RefreshPropertiesTray();
        }

        private void AddAnnotationAtActiveLine(PungentRichDocumentAnnotationKind kind)
        {
            if (!EnsureDocumentForInsert())
                return;

            if (_surfaceMode != RichDocumentSurfaceMode.Document)
            {
                _status = "Switch to Document mode to add comments or bookmarks.";
                RefreshStatusChrome();
                return;
            }

            if (_canvasView == null)
                RefreshDocumentArea();

            if (kind == PungentRichDocumentAnnotationKind.Bookmark)
                _canvasView?.AddBookmarkAtActiveLine();
            else
                _canvasView?.AddCommentAtActiveLine();

            RefreshPropertiesTray();
        }

        private void RefreshPropertiesTray()
        {
            if (_propertiesView == null)
                return;

            _propertiesView.Bind(_document, _canvasView == null ? null : _canvasView.SelectionState, _canvasView == null ? string.Empty : _canvasView.SelectedSemanticBindingId);
        }

        private void AppendRawSource(string text)
        {
            if (_document == null)
                return;

            string body = _document.bodyText ?? string.Empty;
            string separator = string.IsNullOrWhiteSpace(body) || body.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : Environment.NewLine;
            _document.bodyText = body + separator + (text ?? string.Empty);
            if (_rawSourceField != null)
                _rawSourceField.SetValueWithoutNotify(_document.bodyText);
            MarkDirty("Unsaved raw source changes");
        }

        private void AddTokenTarget(string key)
        {
            if (_document == null || string.IsNullOrWhiteSpace(key))
                return;

            if (_document.targets == null)
                _document.targets = new List<PungentAuthoringTarget>();
            if (!_document.targets.Any(target => target != null && target.targetKind == PungentAuthoringTargetKind.TokenKey && string.Equals(target.rawValue, key, StringComparison.OrdinalIgnoreCase)))
                _document.targets.Add(PungentAuthoringTarget.Create(PungentAuthoringTargetKind.TokenKey, key, "Token: " + key, "token-validator"));

            if (_document.references == null)
                _document.references = new List<PungentAuthoringReference>();
            if (!_document.references.Any(reference => reference != null && reference.itemKind == PungentAuthoringItemKind.TokenDefinition && string.Equals(reference.itemId, key, StringComparison.OrdinalIgnoreCase)))
                _document.references.Add(PungentAuthoringReference.Create(PungentAuthoringItemKind.TokenDefinition, key, "token-validator", "Token: " + key));
        }

        private void ToggleSurfaceMode()
        {
            SetSurfaceMode(_surfaceMode == RichDocumentSurfaceMode.Document ? RichDocumentSurfaceMode.RawSource : RichDocumentSurfaceMode.Document, true);
        }

        private void SetSurfaceMode(RichDocumentSurfaceMode mode, bool refresh)
        {
            if (_document != null)
            {
                if (_surfaceMode == RichDocumentSurfaceMode.Document)
                    _canvasView?.CommitBodyToDocument();
                if (mode == RichDocumentSurfaceMode.Document)
                    PungentRichDocumentBlockSync.SyncRawToBlocks(_document);
            }

            _surfaceMode = mode;
            UtilityWindowPrefs.SetInt(PrefSurfaceMode, (int)_surfaceMode);
            if (refresh)
                RefreshDocumentArea();
        }

        private void SyncRawToDocument()
        {
            if (_document == null)
                return;

            _surfaceMode = RichDocumentSurfaceMode.Document;
            PungentRichDocumentBlockSync.SyncRawToBlocks(_document);
            MarkDirty("Synced Raw Source to document canvas.");
            RefreshDocumentArea();
        }

        private void DeriveBlocksFromBody()
        {
            if (_document == null)
                return;

            PrepareDocumentForRead();
            _status = "Derived semantic blocks from body text.";
            RefreshStatusChrome();
        }

        private void OpenTokenValidator()
        {
            if (!HasTokenSystem())
            {
                _status = "Token Validator is not installed. Tokens remain visible as raw or unresolved chips.";
                RefreshStatusChrome();
                return;
            }

            PungentTokenValidatorWindow.OpenAndSelect(PungentRichDocumentParser.NormalizeTokenKey(_tokenKey));
        }

        private void OpenDocumentationLinks()
        {
            if (PungentUtilityRegistry.Find("documentation-links") == null)
            {
                _status = "Documentation Links utility is not installed.";
                RefreshStatusChrome();
                return;
            }

            DocumentationLinkEditorPopup.OpenForUtility(PungentRichDocumentProvider.UtilityId);
        }

        private void OpenStickyNotes()
        {
            if (PungentUtilityRegistry.Find("tooltip-notes") == null)
            {
                _status = "Sticky Notes utility is not installed.";
                RefreshStatusChrome();
                return;
            }

            PungentUtilityRegistry.Open("tooltip-notes");
        }

        private void OpenHelp()
        {
            OpenHelp("overview", "rich-document-editor");
        }

        private void OpenHelp(string sectionId, string topicId)
        {
            if (PungentUtilityRegistry.Find("help-browser") == null)
            {
                _status = "Help Browser is not installed.";
                RefreshStatusChrome();
                return;
            }

            PungentUtilityHelpRegistry.Open(PungentRichDocumentProvider.UtilityId, sectionId, topicId);
        }

        private void OpenPropagationPreview()
        {
            if (_document == null)
                return;

            PrepareDocumentForRead();
            PungentRichDocumentPropagationPreviewWindow.Open(_document, PungentRichDocumentParser.Parse(_document));
        }

        private void RunValidation()
        {
            PrepareDocumentForRead();
            _lastValidation = PungentRichDocumentEditorGUI.ValidateLocal(_document, PungentRichDocumentProvider.Id);
            _status = _lastValidation.IsValid ? "Local validation passed." : "Local validation found issues.";
            RefreshStatusChrome();
        }

        private void PrepareDocumentForRead()
        {
            if (_document == null)
                return;

            if (_surfaceMode == RichDocumentSurfaceMode.Document)
                _canvasView?.CommitBodyToDocument();
            PungentRichDocumentSemanticBindingService.RefreshBindingsFromBody(_document);
            PungentRichDocumentBlockSync.SyncRawToBlocks(_document);
        }

        private void EnsureBodyTextAvailable(PungentRichDocument document)
        {
            if (document == null || !string.IsNullOrWhiteSpace(document.bodyText))
                return;

            if (document.blocks != null && document.blocks.Any(block => block != null && block.HasReadableContent))
                document.bodyText = PungentRichDocumentBlockSync.GenerateBodyText(document.blocks);
        }

        private void MarkDirty(string saveState)
        {
            if (_document == null)
                return;

            _dirty = true;
            _saveState = string.IsNullOrWhiteSpace(saveState) ? "Unsaved changes" : saveState;
            _lastEditTime = EditorApplication.timeSinceStartup;
            _status = _saveState + ".";
            RefreshStatusChrome();
        }

        private void SetStatus(string status)
        {
            _status = string.IsNullOrWhiteSpace(status) ? _status : status;
            RefreshStatusChrome();
        }

        private void TogglePropertiesTray()
        {
            _showPropertiesTray = !_showPropertiesTray;
            UtilityWindowPrefs.SetBool(PrefShowPropertiesTray, _showPropertiesTray);
            _status = _showPropertiesTray ? "Properties shown." : "Properties collapsed.";
            RefreshDocumentArea();
        }

        private void ToggleInlinePropagation()
        {
            _showInlinePropagation = !_showInlinePropagation;
            UtilityWindowPrefs.SetBool(PrefShowInlinePropagation, _showInlinePropagation);
            _status = _showInlinePropagation ? "Document outputs shown." : "Document outputs hidden.";
            RefreshDocumentArea();
        }

        private void CollapsePropertiesTray()
        {
            if (!_showPropertiesTray)
                return;

            _showPropertiesTray = false;
            UtilityWindowPrefs.SetBool(PrefShowPropertiesTray, false);
            _status = "Properties collapsed.";
            RefreshDocumentArea();
        }

        private void ExpandPropertiesTray()
        {
            if (_showPropertiesTray)
                return;

            _showPropertiesTray = true;
            UtilityWindowPrefs.SetBool(PrefShowPropertiesTray, true);
            _status = "Properties shown.";
            RefreshDocumentArea();
        }

        private bool SaveDirty(string status, bool autosave)
        {
            if (!_dirty || _document == null)
                return false;

            PrepareDocumentForRead();
            _document.NormalizeInPlace();
            _document.Touch();
            PungentRichDocumentStorage.Database.AddOrUpdate(_document);
            _lastValidation = PungentRichDocumentEditorGUI.ValidateLocal(_document, PungentRichDocumentProvider.Id);

            if (PungentRichDocumentStorage.Save(out string error))
            {
                _dirty = false;
                _saveState = autosave ? "Autosaved" : "Saved";
                int autoApplied = TryAutoApplyBoundSemanticOutputs(out int autoSkipped);
                _status = autoApplied > 0
                    ? status + " Auto applied " + autoApplied + " bound text value(s)."
                    : status;
                if (autoApplied == 0 && autoSkipped > 0)
                    _status = status + " Auto apply skipped " + autoSkipped + " bound text value(s).";
                SavePrefs();
                RefreshStatusChrome();
                return true;
            }

            _saveState = "Save failed";
            _status = error;
            RefreshStatusChrome();
            return false;
        }

        private int TryAutoApplyBoundSemanticOutputs(out int skipped)
        {
            skipped = 0;
            if (_document == null)
                return 0;

            PungentRichDocumentPropagationProposal proposal = PungentRichDocumentExtractor.CreateProposal(_document, PungentRichDocumentParser.Parse(_document));
            int applied = 0;
            foreach (PungentRichDocumentExtractedEntry entry in proposal.entries ?? new List<PungentRichDocumentExtractedEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.semanticBindingId))
                    continue;

                PungentRichDocumentSemanticBinding binding = PungentRichDocumentSemanticBindingService.FindBinding(_document, entry.semanticBindingId);
                if (binding == null || binding.applyMode == PungentAuthoringBindingApplyMode.ManualApply)
                    continue;

                if (binding.applyMode == PungentAuthoringBindingApplyMode.AutoApplyEditMode && EditorApplication.isPlaying)
                {
                    skipped++;
                    continue;
                }

                if (!entry.canApply)
                {
                    skipped++;
                    continue;
                }

                if (string.Equals(entry.currentValue ?? string.Empty, entry.value ?? string.Empty, StringComparison.Ordinal))
                    continue;

                PungentRichDocumentBindingApplyResult result = PungentRichDocumentBindingApplicationService.Apply(binding, entry.value ?? string.Empty);
                if (result != null && result.applied)
                    applied++;
                else
                    skipped++;
            }

            return applied;
        }

        private void HandleEditorUpdate()
        {
            if (!_dirty || _document == null)
                return;

            if (EditorApplication.timeSinceStartup - _lastEditTime < AutosaveDelaySeconds)
                return;

            SaveDirty("Autosaved document.", true);
        }

        private void CommitBeforeReload()
        {
            SaveDirty("Saved document before domain reload.", false);
        }

        private void SavePrefs()
        {
            UtilityWindowPrefs.SetString(PrefSelectedDocument, _selectedDocumentId);
            UtilityWindowPrefs.SetString(PrefTokenKey, _tokenKey);
            UtilityWindowPrefs.SetString(PrefFontName, _fontName);
            UtilityWindowPrefs.SetInt(PrefFontSize, _fontSize);
            UtilityWindowPrefs.SetInt(PrefSurfaceMode, (int)_surfaceMode);
            UtilityWindowPrefs.SetBool(PrefShowLineNumbers, _showLineNumbers);
            UtilityWindowPrefs.SetBool(PrefShowAnnotationMarkers, _showAnnotationMarkers);
            UtilityWindowPrefs.SetBool(PrefShowSemanticTint, _showSemanticTint);
            UtilityWindowPrefs.SetBool(PrefShowTokenTechnicalText, _showTokenTechnicalText);
            UtilityWindowPrefs.SetBool(PrefCompactLineSpacing, _compactLineSpacing);
            UtilityWindowPrefs.SetBool(PrefShowRawSyntaxHints, _showRawSyntaxHints);
            UtilityWindowPrefs.SetBool(PrefShowPropertiesTray, _showPropertiesTray);
            UtilityWindowPrefs.SetBool(PrefShowInlinePropagation, _showInlinePropagation);
        }

        private void RefreshStatusChrome()
        {
            if (_surfaceModeButton != null)
                _surfaceModeButton.text = SurfaceModeLabel();
            if (_propertiesButton != null)
                _propertiesButton.text = _showPropertiesTray ? "Properties" : "Props >";
            if (_inlinePropagationButton != null)
                _inlinePropagationButton.text = _showInlinePropagation ? "Outputs" : "Outputs >";

            if (_savePill != null)
            {
                _savePill.text = _saveState;
                ApplyPillStyle(_savePill, _dirty ? UtilityWindowTheme.Amber : UtilityWindowTheme.Green, 104);
            }

            if (_statusLabel != null)
                _statusLabel.text = _status ?? string.Empty;

            if (_validationPill != null)
            {
                if (_lastValidation == null || _lastValidation.status == PungentAuthoringValidationStatus.NotRun)
                {
                    _validationPill.text = string.Empty;
                    _validationPill.style.display = DisplayStyle.None;
                }
                else
                {
                    _validationPill.style.display = DisplayStyle.Flex;
                    _validationPill.text = _lastValidation.status.ToString();
                    ApplyPillStyle(_validationPill, _lastValidation.IsValid ? UtilityWindowTheme.Green : UtilityWindowTheme.Amber, 136);
                }
            }
        }

        private void HandleKeyboardShortcut(KeyDownEvent evt)
        {
            if (evt == null)
                return;

            bool action = evt.ctrlKey || evt.commandKey;
            if (!action)
                return;

            if (evt.altKey && evt.keyCode == KeyCode.UpArrow)
            {
                _canvasView?.MoveActiveSegment(-1);
                evt.StopPropagation();
                return;
            }

            if (evt.altKey && evt.keyCode == KeyCode.DownArrow)
            {
                _canvasView?.MoveActiveSegment(1);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.S)
            {
                SaveDirty("Saved document.", false);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.P)
            {
                ToggleSurfaceMode();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.B)
            {
                ApplyBold();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.I)
            {
                ApplyItalic();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.K)
            {
                InsertToken();
                evt.StopPropagation();
            }
        }

        private bool HasTokenSystem()
        {
            return PungentAuthoringProviderRegistry.GetProvidersForKind(PungentAuthoringItemKind.TokenDefinition).Count > 0 ||
                   PungentUtilityRegistry.Find("token-validator") != null;
        }

        private int WordCountForDisplay()
        {
            string source = _document == null ? string.Empty : _document.bodyText ?? string.Empty;
            return string.IsNullOrWhiteSpace(source)
                ? 0
                : source.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private string SurfaceModeLabel()
        {
            return _surfaceMode == RichDocumentSurfaceMode.Document ? "Document" : "Raw Source";
        }

        private static void ApplyPillStyle(Label label, Color tint, float width)
        {
            if (label == null)
                return;

            label.style.width = width;
            label.style.marginLeft = 6;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 3;
            label.style.paddingBottom = 3;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            label.style.color = Color.white;
            label.style.backgroundColor = new StyleColor(new Color(tint.r, tint.g, tint.b, 0.88f));
            label.style.borderTopLeftRadius = 9;
            label.style.borderTopRightRadius = 9;
            label.style.borderBottomLeftRadius = 9;
            label.style.borderBottomRightRadius = 9;
        }
    }
#endif
}
