using System;
using System.Collections.Generic;
using System.Linq;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.Editor.Authoring;

namespace PungentFunk.Utilities.Editor.ProjectAudit
{
#if UNITY_EDITOR
    using PungentFunk.Utilities.Editor.Core;
    using PungentFunk.Utilities.Editor.Theme;
    using UnityEditor;
    using UnityEngine;

    internal sealed class PungentStickyNoteMiniFinderOverlayWindow : EditorWindow
    {
        private enum MiniInterface
        {
            StickyNotes,
            RichDocuments,
            DataSheets,
            NodeGraphs
        }

        private sealed class MiniItem
        {
            public string key = string.Empty;
            public MiniInterface interfaceKind;
            public PungentAuthoringMetadata metadata;
            public PungentAuthoringReference reference;
            public PungentAuthoringPreview preview;
            public bool previewResolved;
            public string providerName = string.Empty;

            public string Title => metadata == null || string.IsNullOrWhiteSpace(metadata.title) ? reference?.itemId ?? "Untitled" : metadata.title;
            public string Id => metadata?.id ?? reference?.itemId ?? string.Empty;
            public bool IsStickyNote => interfaceKind == MiniInterface.StickyNotes;
        }

        private const string PrefPrefix = "PungentFunkUtilities.StickyNotesMiniFinder.";
        private const string PrefFocusMode = PrefPrefix + "FocusMode";
        private const string PrefCollapsed = PrefPrefix + "Collapsed";
        private const string PrefHasPlacement = PrefPrefix + "HasPlacement";
        private const string PrefX = PrefPrefix + "X";
        private const string PrefY = PrefPrefix + "Y";
        private const string PrefSearch = PrefPrefix + "Search";
        private const string PrefShowSticky = PrefPrefix + "ShowSticky";
        private const string PrefShowDocs = PrefPrefix + "ShowDocs";
        private const string PrefShowSheets = PrefPrefix + "ShowSheets";
        private const string PrefShowGraphs = PrefPrefix + "ShowGraphs";
        private const string SearchControlName = "PungentStickyNotesMiniFinderSearch";

        private const float HeaderHeight = 30f;
        private const float Width = 430f;
        private const float Height = 520f;
        private const float CollapsedWidth = 360f;
        private const float Margin = 12f;
        private const double FocusGraceSeconds = 0.65d;
        private const double AttentionPulseSeconds = 0.55d;

        private static readonly string[] FocusModeLabels =
        {
            "Auto Collapse",
            "Stay Open",
            "Auto Hide"
        };

        private static GUIStyle _headerStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _rowStyle;
        private static GUIStyle _typeStyle;

        private readonly List<MiniItem> _allItems = new List<MiniItem>();
        private readonly List<MiniItem> _visibleItems = new List<MiniItem>();
        private Vector2 _scroll;
        private Vector2 _headerDragStartScreenPosition;
        private Rect _headerDragStartWindowRect;
        private bool _headerDragging;
        private bool _headerDragMoved;
        private bool _dirty = true;
        private bool _focusSearch;
        private string _search = string.Empty;
        private string _selectedKey = string.Empty;
        private double _menuGuardUntil;
        private double _attentionUntil;

        internal static PungentStickyNoteMiniFinderOverlayWindow Instance { get; private set; }

        private double LastInteractionTime { get; set; }

        private bool IsInteracting => _headerDragging || EditorApplication.timeSinceStartup < _menuGuardUntil;

        private static MinimizedOverlayFocusMode FocusMode
        {
            get
            {
                int raw = UtilityWindowPrefs.GetInt(PrefFocusMode, (int)MinimizedOverlayFocusMode.AutoCollapse);
                return Enum.IsDefined(typeof(MinimizedOverlayFocusMode), raw)
                    ? (MinimizedOverlayFocusMode)raw
                    : MinimizedOverlayFocusMode.AutoCollapse;
            }
            set
            {
                UtilityWindowPrefs.SetInt(PrefFocusMode, (int)value);
                if (value == MinimizedOverlayFocusMode.StayOpen)
                    SetCollapsed(false);
                RefreshSize();
            }
        }

        private static bool Collapsed
        {
            get { return UtilityWindowPrefs.GetBool(PrefCollapsed, false); }
            set { SetCollapsed(value); }
        }

        [MenuItem(PungentUtilityMenuPaths.Root + "/Project Audit and Authoring/Sticky Notes Overlay", priority = 843)]
        public static void Open()
        {
            ShowOverlay(new Rect());
        }

        internal static void ShowOverlay(Rect activatorRect)
        {
            SetCollapsed(false);
            PungentStickyNoteMiniFinderOverlayWindow window = FindOpenInstanceAndCloseDuplicates();
            Rect rect = window != null ? GetRectForCurrentState(window.position) : GetRectForOpen(activatorRect);
            if (window == null)
            {
                window = CreateInstance<PungentStickyNoteMiniFinderOverlayWindow>();
                window.titleContent = new GUIContent("Sticky Notes Overlay");
                window.ApplyOverlayRect(rect, true);
                window.ShowPopup();
            }

            window.ApplyOverlayRect(rect, true);
            window.FocusAndPulse(false);
            window.Repaint();
        }

        private static PungentStickyNoteMiniFinderOverlayWindow FindOpenInstanceAndCloseDuplicates()
        {
            PungentStickyNoteMiniFinderOverlayWindow[] windows = Resources.FindObjectsOfTypeAll<PungentStickyNoteMiniFinderOverlayWindow>();
            PungentStickyNoteMiniFinderOverlayWindow primary = Instance;
            if (primary == null || Array.IndexOf(windows, primary) < 0)
                primary = windows.Length > 0 ? windows[0] : null;

            for (int i = 0; i < windows.Length; i++)
            {
                PungentStickyNoteMiniFinderOverlayWindow window = windows[i];
                if (window != null && window != primary)
                    window.Close();
            }

            Instance = primary;
            return primary;
        }

        private void OnEnable()
        {
            Instance = this;
            wantsMouseMove = true;
            titleContent = new GUIContent("Sticky Notes Overlay");
            _search = UtilityWindowPrefs.GetString(PrefSearch, string.Empty);
            LastInteractionTime = EditorApplication.timeSinceStartup;
            PungentAuthoringProviderRegistry.Changed += MarkDirty;
            EditorApplication.update += TickFocusMode;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
            PungentAuthoringProviderRegistry.Changed -= MarkDirty;
            EditorApplication.update -= TickFocusMode;
        }

        private void OnGUI()
        {
            EnsureStyles();
            TrackInteraction(Event.current);

            if (Event.current.type == EventType.KeyDown &&
                ((Event.current.control || Event.current.command) && Event.current.keyCode == KeyCode.F ||
                 (!EditorGUIUtility.editingTextField && Event.current.keyCode == KeyCode.Slash)))
            {
                _focusSearch = true;
                Event.current.Use();
                Repaint();
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                GUIUtility.ExitGUI();
            }

            Rect full = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(full, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.13f, 0.985f) : new Color(0.82f, 0.82f, 0.84f, 0.985f));

            Rect headerRect = new Rect(0f, 0f, position.width, HeaderHeight);
            DrawHeader(headerRect);
            if (Collapsed)
                return;

            RefreshItemsIfNeeded();
            Rect bodyRect = new Rect(7f, headerRect.yMax + 6f, position.width - 14f, Mathf.Max(20f, position.height - HeaderHeight - 12f));
            GUILayout.BeginArea(bodyRect);
            try
            {
                DrawToolbar();
                _scroll = EditorGUILayout.BeginScrollView(_scroll, false, true);
                if (_visibleItems.Count == 0)
                {
                    EditorGUILayout.HelpBox("No matching authoring items. Adjust search or type filters.", MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_search)))
                        {
                            if (GUILayout.Button("Clear Search", EditorStyles.miniButton, GUILayout.Width(92f)))
                            {
                                _search = string.Empty;
                                UtilityWindowPrefs.SetString(PrefSearch, _search);
                                RebuildVisibleItems();
                            }
                        }

                        using (new EditorGUI.DisabledScope(AllTypesShown()))
                        {
                            if (GUILayout.Button("Show All Types", EditorStyles.miniButton, GUILayout.Width(98f)))
                                SetAllTypes(true);
                        }

                        if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(62f)))
                            MarkDirty();
                    }
                }
                for (int i = 0; i < _visibleItems.Count; i++)
                    DrawItemRow(_visibleItems[i]);
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _headerStyle);
            if (EditorApplication.timeSinceStartup < _attentionUntil)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                    ? new Color(0.34f, 0.56f, 0.95f, 0.28f)
                    : new Color(0.20f, 0.43f, 0.90f, 0.24f));
                Repaint();
            }

            Rect toggleRect = new Rect(rect.x + 4f, rect.y + 4f, 28f, rect.height - 8f);
            Rect closeRect = new Rect(rect.xMax - 30f, rect.y + 4f, 26f, rect.height - 8f);
            Rect browserRect = new Rect(closeRect.x - 62f, rect.y + 4f, 58f, rect.height - 8f);
            Rect menuRect = new Rect(browserRect.x - 38f, rect.y + 4f, 34f, rect.height - 8f);
            Rect titleRect = new Rect(toggleRect.xMax + 4f, rect.y + 3f, Mathf.Max(40f, menuRect.x - toggleRect.xMax - 8f), rect.height - 6f);

            if (GUI.Button(toggleRect, new GUIContent(Collapsed ? ">" : "v", "Expand or collapse the Sticky Notes mini finder."), _toolbarButtonStyle))
            {
                ToggleCollapsed();
                GUIUtility.ExitGUI();
            }

            HandleHeaderDrag(titleRect, Event.current);
            GUI.Label(titleRect, new GUIContent("Sticky Notes Overlay", "Drag to move. Click to collapse or expand."), _titleStyle);

            if (GUI.Button(menuRect, new GUIContent("...", "Mini finder actions and settings."), _toolbarButtonStyle))
            {
                GuardMenuFocus();
                ShowHeaderMenu();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(browserRect, new GUIContent("Browser", "Open the full Sticky Notes browser."), _toolbarButtonStyle))
            {
                PungentNotesRoadmapWindow.Open();
                GUIUtility.ExitGUI();
            }

            if (GUI.Button(closeRect, new GUIContent("x", "Close this mini finder."), _toolbarButtonStyle))
            {
                Close();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(SearchControlName);
                string next = EditorGUILayout.TextField(_search, UtilityWindowTheme.ToolbarSearchStyle, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                if (_focusSearch && Event.current != null && Event.current.type == EventType.Repaint)
                {
                    GUI.FocusControl(SearchControlName);
                    EditorGUI.FocusTextInControl(SearchControlName);
                    _focusSearch = false;
                }

                if (EditorGUI.EndChangeCheck())
                {
                    _search = next ?? string.Empty;
                    UtilityWindowPrefs.SetString(PrefSearch, _search);
                    RebuildVisibleItems();
                }

                if (GUILayout.Button(new GUIContent("Types", "Show or hide authoring interfaces."), EditorStyles.toolbarDropDown, GUILayout.Width(58f)))
                    ShowTypeMenu();
                UtilityWindowTheme.CountPill(_visibleItems.Count.ToString(), UtilityWindowTheme.Cyan, 38f);
                if (GUILayout.Button(new GUIContent("R", "Refresh authoring items."), EditorStyles.miniButton, GUILayout.Width(24f)))
                    MarkDirty();
            }
        }

        private void DrawItemRow(MiniItem item)
        {
            if (item == null)
                return;

            bool selected = string.Equals(_selectedKey, item.key, StringComparison.OrdinalIgnoreCase);
            Rect rowRect = GUILayoutUtility.GetRect(0f, 25f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                _rowStyle.Draw(rowRect, GUIContent.none, false, false, selected, false);

            string type = InterfaceShortLabel(item.interfaceKind);
            float typeWidth = Mathf.Min(54f, _typeStyle.CalcSize(new GUIContent(type)).x + 12f);
            Rect typeRect = new Rect(rowRect.xMax - typeWidth - 8f, rowRect.y + 5f, typeWidth, 16f);
            GUI.Label(typeRect, new GUIContent(type, InterfaceLabel(item.interfaceKind)), _typeStyle);

            Rect titleRect = new Rect(rowRect.x + 7f, rowRect.y + 4f, Mathf.Max(40f, typeRect.x - rowRect.x - 12f), 18f);
            if (GUI.Button(titleRect, GUIContent.none, GUIStyle.none))
            {
                _selectedKey = selected ? string.Empty : item.key;
                Repaint();
            }
            GUI.Label(titleRect, new GUIContent(Ellipsize(item.Title, EditorStyles.label, titleRect.width), item.Title), EditorStyles.label);

            if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
            {
                ShowItemMenu(item);
                Event.current.Use();
            }

            if (selected)
                DrawSelectedActions(item);
        }

        private void DrawSelectedActions(MiniItem item)
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.08f, 0.03f, 4, 1)))
            {
                GUILayout.Space(8f);
                if (item.IsStickyNote)
                {
                    if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(62f)))
                        PreviewSticky(item);
                    if (GUILayout.Button("Edit", EditorStyles.miniButton, GUILayout.Width(46f)))
                        EditSticky(item);
                    if (position.width >= 300f && GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(76f)))
                        DuplicateSticky(item);
                    if (position.width >= 360f && GUILayout.Button("Archive", EditorStyles.miniButton, GUILayout.Width(62f)))
                        ArchiveSticky(item);
                    if (position.width >= 420f && GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48f)))
                        CopyItem(item);
                }
                else
                {
                    if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(64f)))
                        PreviewAuthoring(item);
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(52f)))
                        OpenAuthoring(item);
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(48f)))
                        CopyItem(item);
                    if (GUILayout.Button("Linked Note", EditorStyles.miniButton, GUILayout.Width(84f)))
                        CreateLinkedNote(item);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("More Actions", "More actions."), EditorStyles.miniButton, GUILayout.Width(92f)))
                    ShowItemMenu(item);
            }
        }

        private void ShowItemMenu(MiniItem item)
        {
            GenericMenu menu = new GenericMenu();
            if (item.IsStickyNote)
            {
                menu.AddItem(new GUIContent("Preview"), false, () => PreviewSticky(item));
                menu.AddItem(new GUIContent("Edit"), false, () => EditSticky(item));
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateSticky(item));
                menu.AddItem(new GUIContent("Archive / Unarchive"), false, () => ArchiveSticky(item));
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteSticky(item));
            }
            else
            {
                menu.AddItem(new GUIContent("Preview"), false, () => PreviewAuthoring(item));
                menu.AddItem(new GUIContent("Open"), false, () => OpenAuthoring(item));
                menu.AddItem(new GUIContent("Create Linked Note"), false, () => CreateLinkedNote(item));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy ID"), false, () => CopyItem(item));
            menu.AddItem(new GUIContent("Open Full Browser"), false, PungentNotesRoadmapWindow.Open);
            menu.ShowAsContext();
        }

        private void ShowHeaderMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(Collapsed ? "Expand Overlay" : "Collapse To Header"), false, ToggleCollapsed);
            menu.AddItem(new GUIContent("Reset Overlay Position"), false, ResetPlacement);
            menu.AddSeparator(string.Empty);
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoCollapse, "Focus Behavior/Auto Collapse");
            AddFocusMode(menu, MinimizedOverlayFocusMode.StayOpen, "Focus Behavior/Stay Open");
            AddFocusMode(menu, MinimizedOverlayFocusMode.AutoHide, "Focus Behavior/Auto Hide");
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Full Browser"), false, PungentNotesRoadmapWindow.Open);
            menu.AddItem(new GUIContent("Refresh"), false, MarkDirty);
            menu.ShowAsContext();
        }

        private void ShowTypeMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("All Interfaces"), AllTypesShown(), () => SetAllTypes(true));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Sticky Notes"), ShowSticky, () => ToggleType(MiniInterface.StickyNotes));
            AddProviderAwareType(menu, "Rich Documents", MiniInterface.RichDocuments, PungentAuthoringItemKind.RichDocument, ShowDocs);
            AddProviderAwareType(menu, "Data Sheets", MiniInterface.DataSheets, PungentAuthoringItemKind.DataSheet, ShowSheets);
            AddProviderAwareType(menu, "Node Graphs", MiniInterface.NodeGraphs, PungentAuthoringItemKind.Board, ShowGraphs);
            menu.ShowAsContext();
        }

        private void AddProviderAwareType(GenericMenu menu, string label, MiniInterface type, PungentAuthoringItemKind kind, bool shown)
        {
            if (PungentAuthoringProviderRegistry.GetProvidersForKind(kind).Count == 0)
                menu.AddDisabledItem(new GUIContent(label + " (" + PungentAuthoringItemKinds.GetMissingProviderMessage(kind) + ")"));
            else
                menu.AddItem(new GUIContent(label), shown, () => ToggleType(type));
        }

        private void EditSticky(MiniItem item)
        {
            PungentNote note = FindSticky(item);
            if (note == null)
                return;

            PungentStickyNoteOverlayController.OpenEdit(note.id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Mini Finder");
        }

        private void PreviewSticky(MiniItem item)
        {
            PungentNote note = FindSticky(item);
            if (note == null)
                return;

            PungentStickyNoteOverlayController.OpenPreview(note.id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Mini Finder");
        }

        private void DuplicateSticky(MiniItem item)
        {
            PungentNote note = FindSticky(item);
            if (note == null)
                return;

            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before duplicate.");
            PungentNote copy = PungentNoteStorage.Database.Duplicate(note);
            if (copy == null)
                return;

            PungentStickyNoteOverlayController.OpenEdit(copy.id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Mini Finder");
            MarkDirty();
        }

        private void ArchiveSticky(MiniItem item)
        {
            PungentNote note = FindSticky(item);
            if (note == null)
                return;

            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before archive change.");
            PungentNoteStorage.Archive(note, !note.archived);
            MarkDirty();
        }

        private void DeleteSticky(MiniItem item)
        {
            PungentNote note = FindSticky(item);
            if (note == null)
                return;

            if (!EditorUtility.DisplayDialog("Delete Note", "Delete \"" + (string.IsNullOrWhiteSpace(note.title) ? "this note" : note.title) + "\"?", "Delete", "Cancel"))
                return;

            PungentStickyNoteOverlayController.CommitActiveDraft("Saved draft before delete.");
            PungentNoteStorage.Delete(note);
            if (string.Equals(_selectedKey, item.key, StringComparison.OrdinalIgnoreCase))
                _selectedKey = string.Empty;
            MarkDirty();
        }

        private void PreviewAuthoring(MiniItem item)
        {
            if (item == null || item.reference == null)
                return;

            PungentStickyNoteOverlayController.OpenAuthoringPreview(item.reference, EnsurePreview(item), Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, InterfaceLabel(item.interfaceKind));
        }

        private PungentAuthoringPreview EnsurePreview(MiniItem item)
        {
            if (item == null || item.reference == null)
                return null;

            if (item.previewResolved)
                return item.preview;

            item.previewResolved = true;
            if (PungentAuthoringProviderRegistry.TryGetPreview(item.reference, out PungentAuthoringPreview preview) && preview != null)
                item.preview = preview;
            return item.preview;
        }

        private void OpenAuthoring(MiniItem item)
        {
            if (item == null || item.reference == null)
                return;

            PungentAuthoringProviderRegistry.TryOpen(item.reference);
        }

        private void CopyItem(MiniItem item)
        {
            if (item == null)
                return;

            if (!item.IsStickyNote && item.reference != null && PungentAuthoringProviderRegistry.TryCopy(item.reference, out string copied, out _))
                EditorGUIUtility.systemCopyBuffer = string.IsNullOrWhiteSpace(copied) ? item.Id : copied;
            else
                EditorGUIUtility.systemCopyBuffer = item.Id;
        }

        private void CreateLinkedNote(MiniItem item)
        {
            if (item == null || item.reference == null)
                return;

            string title = item.Title;
            PungentNote note = PungentNoteStorage.Database.CreateNote("Note: " + title, PungentNoteKind.ProjectNote);
            note.body = "Linked authoring item: " + title +
                        "\nKind: " + item.reference.KindLabel +
                        "\nProvider: " + (item.reference.providerId ?? string.Empty) +
                        "\nID: " + (item.reference.itemId ?? string.Empty);
            note.stableKey = "authoring:" + (item.reference.providerId ?? string.Empty) + ":" + item.reference.itemKind + ":" + (item.reference.itemId ?? string.Empty);
            note.targets.Add(new PungentNoteTargetLink
            {
                type = PungentNoteTargetType.ExternalPath,
                label = item.reference.KindLabel + ": " + title,
                externalPathOrUrl = "authoring://" + (item.reference.providerId ?? string.Empty) + "/" + item.reference.itemKind + "/" + (item.reference.itemId ?? string.Empty)
            });
            PungentNoteStorage.Save();
            PungentStickyNoteOverlayController.OpenEdit(note.id, Rect.zero, PungentStickyNoteOverlayOwner.BrowserWindow, "Linked Note");
            MarkDirty();
        }

        private PungentNote FindSticky(MiniItem item)
        {
            return item == null ? null : PungentStickyNoteOverlayController.FindNote(item.Id);
        }

        private void RefreshItemsIfNeeded()
        {
            if (!_dirty)
                return;

            _dirty = false;
            _allItems.Clear();
            foreach (IPungentAuthoringProvider provider in PungentAuthoringProviderRegistry.GetProviders())
            {
                if (provider == null)
                    continue;

                IEnumerable<PungentAuthoringMetadata> items;
                try
                {
                    items = provider.EnumerateItems() ?? Enumerable.Empty<PungentAuthoringMetadata>();
                }
                catch
                {
                    continue;
                }

                foreach (PungentAuthoringMetadata metadata in items)
                    AddMetadata(provider, metadata);
            }

            RebuildVisibleItems();
        }

        private void AddMetadata(IPungentAuthoringProvider provider, PungentAuthoringMetadata metadata)
        {
            if (provider == null || metadata == null || !TryMapInterface(metadata.kind, out MiniInterface interfaceKind))
                return;

            PungentAuthoringReference reference = metadata.ToReference();
            if (string.IsNullOrWhiteSpace(reference.providerId))
                reference.providerId = provider.ProviderId;

            string key = (reference.providerId ?? string.Empty) + "|" + reference.itemKind + "|" + (reference.itemId ?? string.Empty);
            _allItems.Add(new MiniItem
            {
                key = key,
                interfaceKind = interfaceKind,
                metadata = metadata,
                reference = reference,
                preview = null,
                previewResolved = false,
                providerName = provider.DisplayName
            });
        }

        private void RebuildVisibleItems()
        {
            string query = (_search ?? string.Empty).Trim();
            _visibleItems.Clear();
            _visibleItems.AddRange(_allItems
                .Where(item => item != null && TypeShown(item.interfaceKind) && MatchesSearch(item, query))
                .OrderByDescending(item => ParseDateTicks(item.metadata?.updatedUtc, item.metadata?.createdUtc))
                .ThenBy(item => item.Title));
            Repaint();
        }

        private static bool MatchesSearch(MiniItem item, string query)
        {
            if (item == null || string.IsNullOrWhiteSpace(query))
                return true;

            return Contains(item.Title, query) ||
                   Contains(item.Id, query) ||
                   Contains(InterfaceLabel(item.interfaceKind), query) ||
                   Contains(item.providerName, query) ||
                   Contains(item.metadata?.summary, query);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.IsNullOrWhiteSpace(search) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MarkDirty()
        {
            _dirty = true;
            Repaint();
        }

        private static bool TryMapInterface(PungentAuthoringItemKind kind, out MiniInterface interfaceKind)
        {
            switch (kind)
            {
                case PungentAuthoringItemKind.LegacyNote:
                case PungentAuthoringItemKind.Task:
                case PungentAuthoringItemKind.FutureUtility:
                    interfaceKind = MiniInterface.StickyNotes;
                    return true;
                case PungentAuthoringItemKind.RichDocument:
                    interfaceKind = MiniInterface.RichDocuments;
                    return true;
                case PungentAuthoringItemKind.DataSheet:
                    interfaceKind = MiniInterface.DataSheets;
                    return true;
                case PungentAuthoringItemKind.Board:
                    interfaceKind = MiniInterface.NodeGraphs;
                    return true;
                default:
                    interfaceKind = MiniInterface.StickyNotes;
                    return false;
            }
        }

        private static string InterfaceLabel(MiniInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case MiniInterface.RichDocuments: return "Rich Documents";
                case MiniInterface.DataSheets: return "Data Sheets";
                case MiniInterface.NodeGraphs: return "Node Graphs";
                default: return "Sticky Notes";
            }
        }

        private static string InterfaceShortLabel(MiniInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case MiniInterface.RichDocuments: return "Doc";
                case MiniInterface.DataSheets: return "Sheet";
                case MiniInterface.NodeGraphs: return "Graph";
                default: return "Note";
            }
        }

        private bool TypeShown(MiniInterface interfaceKind)
        {
            switch (interfaceKind)
            {
                case MiniInterface.RichDocuments: return ShowDocs;
                case MiniInterface.DataSheets: return ShowSheets;
                case MiniInterface.NodeGraphs: return ShowGraphs;
                default: return ShowSticky;
            }
        }

        private bool ShowSticky
        {
            get { return UtilityWindowPrefs.GetBool(PrefShowSticky, true); }
            set { UtilityWindowPrefs.SetBool(PrefShowSticky, value); }
        }

        private bool ShowDocs
        {
            get { return UtilityWindowPrefs.GetBool(PrefShowDocs, true); }
            set { UtilityWindowPrefs.SetBool(PrefShowDocs, value); }
        }

        private bool ShowSheets
        {
            get { return UtilityWindowPrefs.GetBool(PrefShowSheets, true); }
            set { UtilityWindowPrefs.SetBool(PrefShowSheets, value); }
        }

        private bool ShowGraphs
        {
            get { return UtilityWindowPrefs.GetBool(PrefShowGraphs, true); }
            set { UtilityWindowPrefs.SetBool(PrefShowGraphs, value); }
        }

        private bool AllTypesShown()
        {
            return ShowSticky && ShowDocs && ShowSheets && ShowGraphs;
        }

        private void SetAllTypes(bool value)
        {
            ShowSticky = value;
            ShowDocs = value;
            ShowSheets = value;
            ShowGraphs = value;
            RebuildVisibleItems();
        }

        private void ToggleType(MiniInterface type)
        {
            switch (type)
            {
                case MiniInterface.RichDocuments: ShowDocs = !ShowDocs; break;
                case MiniInterface.DataSheets: ShowSheets = !ShowSheets; break;
                case MiniInterface.NodeGraphs: ShowGraphs = !ShowGraphs; break;
                default: ShowSticky = !ShowSticky; break;
            }

            RebuildVisibleItems();
        }

        private void TickFocusMode()
        {
            if (Instance != this)
                return;

            if (IsOverlayActive())
                return;

            switch (FocusMode)
            {
                case MinimizedOverlayFocusMode.AutoCollapse:
                    SetCollapsed(true);
                    break;
                case MinimizedOverlayFocusMode.AutoHide:
                    Close();
                    break;
            }
        }

        private bool IsOverlayActive()
        {
            if (EditorWindow.focusedWindow == this || EditorWindow.mouseOverWindow == this)
                return true;
            if (IsInteracting)
                return true;
            return EditorApplication.timeSinceStartup - LastInteractionTime < FocusGraceSeconds;
        }

        private static void AddFocusMode(GenericMenu menu, MinimizedOverlayFocusMode mode, string path)
        {
            menu.AddItem(new GUIContent(path), FocusMode == mode, () => FocusMode = mode);
        }

        private void HandleHeaderDrag(Rect rect, Event evt)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        _headerDragging = true;
                        _headerDragMoved = false;
                        _headerDragStartScreenPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        _headerDragStartWindowRect = position;
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && _headerDragging)
                    {
                        Vector2 currentScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        Vector2 delta = currentScreen - _headerDragStartScreenPosition;
                        if (delta.sqrMagnitude > 9f)
                            _headerDragMoved = true;
                        ApplyOverlayRect(new Rect(_headerDragStartWindowRect.x + delta.x, _headerDragStartWindowRect.y + delta.y, _headerDragStartWindowRect.width, _headerDragStartWindowRect.height), true);
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && _headerDragging)
                    {
                        GUIUtility.hotControl = 0;
                        _headerDragging = false;
                        if (!_headerDragMoved)
                            ToggleCollapsed();
                        _headerDragMoved = false;
                        MarkInteraction();
                        evt.Use();
                    }
                    break;
            }
        }

        private void ApplyOverlayRect(Rect rect, bool persist)
        {
            Rect clamped = ClampRect(rect);
            position = clamped;
            minSize = new Vector2(clamped.width, clamped.height);
            maxSize = new Vector2(clamped.width, clamped.height);
            if (persist)
                SavePlacement(clamped);
        }

        private static void ToggleCollapsed()
        {
            SetCollapsed(!Collapsed);
        }

        private static void SetCollapsed(bool collapsed)
        {
            if (UtilityWindowPrefs.GetBool(PrefCollapsed, false) == collapsed)
                return;
            UtilityWindowPrefs.SetBool(PrefCollapsed, collapsed);
            RefreshSize();
        }

        private static void RefreshSize()
        {
            PungentStickyNoteMiniFinderOverlayWindow window = Instance;
            if (window != null)
                window.ApplyOverlayRect(GetRectForCurrentState(window.position), true);
        }

        private static void ResetPlacement()
        {
            EditorPrefs.DeleteKey(PrefHasPlacement);
            EditorPrefs.DeleteKey(PrefX);
            EditorPrefs.DeleteKey(PrefY);
            RefreshSize();
        }

        private static Rect GetRectForCurrentState(Rect current)
        {
            Vector2 size = DesiredSize();
            return ClampRect(new Rect(current.x, current.y, size.x, size.y));
        }

        private static Rect GetRectForOpen(Rect activatorRect)
        {
            if (TryGetSavedRect(out Rect saved))
                return GetRectForCurrentState(saved);

            Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
            Vector2 size = DesiredSize();
            return ClampRect(new Rect(main.xMax - size.x - 18f, main.y + 118f, size.x, size.y));
        }

        private static Vector2 DesiredSize()
        {
            return Collapsed ? new Vector2(CollapsedWidth, HeaderHeight) : new Vector2(Width, Height);
        }

        private static bool TryGetSavedRect(out Rect rect)
        {
            rect = default;
            if (!UtilityWindowPrefs.GetBool(PrefHasPlacement, false))
                return false;

            rect = new Rect(
                UtilityWindowPrefs.GetFloat(PrefX, 0f),
                UtilityWindowPrefs.GetFloat(PrefY, 0f),
                Width,
                Height);
            return rect.width > 1f && rect.height > 1f && !float.IsNaN(rect.x) && !float.IsNaN(rect.y);
        }

        private static Rect ClampRect(Rect rect)
        {
            Rect main = PungentUtilityMinimizer.GetMainEditorWindowRectForMinimizedUtilities();
            float usableLeft = main.x + Margin;
            float usableRight = main.xMax - Margin;
            float usableTop = main.y + Margin;
            float usableBottom = main.yMax - Margin;
            Vector2 size = DesiredSize();
            float width = Mathf.Min(size.x, Mathf.Max(260f, usableRight - usableLeft));
            float height = Mathf.Min(size.y, Mathf.Max(HeaderHeight, usableBottom - usableTop));
            float x = Mathf.Clamp(rect.x, usableLeft, Mathf.Max(usableLeft, usableRight - width));
            float y = Mathf.Clamp(rect.y, usableTop, Mathf.Max(usableTop, usableBottom - height));
            return new Rect(x, y, width, height);
        }

        private static void SavePlacement(Rect rect)
        {
            UtilityWindowPrefs.SetBool(PrefHasPlacement, true);
            UtilityWindowPrefs.SetFloat(PrefX, rect.x);
            UtilityWindowPrefs.SetFloat(PrefY, rect.y);
        }

        private void TrackInteraction(Event evt)
        {
            if (evt == null)
                return;
            if (evt.type == EventType.MouseMove ||
                evt.type == EventType.MouseDown ||
                evt.type == EventType.MouseDrag ||
                evt.type == EventType.ScrollWheel ||
                evt.type == EventType.KeyDown)
                MarkInteraction();
        }

        private void GuardMenuFocus()
        {
            MarkInteraction();
            _menuGuardUntil = EditorApplication.timeSinceStartup + 1.25d;
        }

        private void FocusAndPulse(bool pulse = true)
        {
            Focus();
            MarkInteraction();
            if (pulse)
                _attentionUntil = EditorApplication.timeSinceStartup + AttentionPulseSeconds;
        }

        private void MarkInteraction()
        {
            LastInteractionTime = EditorApplication.timeSinceStartup;
        }

        private static long ParseDateTicks(string updatedUtc, string createdUtc)
        {
            if (DateTime.TryParse(updatedUtc, out DateTime updated))
                return updated.Ticks;
            if (DateTime.TryParse(createdUtc, out DateTime created))
                return created.Ticks;
            return 0L;
        }

        private static string Ellipsize(string value, GUIStyle style, float width)
        {
            string text = string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim();
            if (style == null || width <= 12f || style.CalcSize(new GUIContent(text)).x <= width)
                return text;

            const string suffix = "...";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid).TrimEnd() + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? suffix : text.Substring(0, low).TrimEnd() + suffix;
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.toolbar)
            {
                fixedHeight = 0f,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0)
            };
            _toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };
            _rowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(4, 4, 2, 2),
                margin = new RectOffset(0, 0, 1, 1)
            };
            _typeStyle = new GUIStyle(UtilityWindowTheme.MutedMiniLabelStyle)
            {
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
        }
    }
#endif
}
