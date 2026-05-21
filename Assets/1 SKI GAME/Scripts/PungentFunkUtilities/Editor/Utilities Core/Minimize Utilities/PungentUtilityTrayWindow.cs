namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    [Serializable]
    public sealed class PungentMinimizedUtilityEntry
    {
        public string EntryKind;
        public string UtilityId;
        public string DisplayName;
        public string WindowTypeName;
        public Rect PreviousRect;
        public long MinimizedTicks;
    }

    [Serializable]
    internal sealed class PungentMinimizedUtilityEntrySet
    {
        public List<PungentMinimizedUtilityEntry> Items = new List<PungentMinimizedUtilityEntry>();
    }

    internal struct PungentMinimizedEntryRestoreAvailability
    {
        public bool CanRestore;
        public string Reason;
        public string Tooltip;

        public static PungentMinimizedEntryRestoreAvailability Available(string tooltip)
        {
            return new PungentMinimizedEntryRestoreAvailability
            {
                CanRestore = true,
                Reason = string.Empty,
                Tooltip = string.IsNullOrWhiteSpace(tooltip) ? "Restore minimized utility." : tooltip
            };
        }

        public static PungentMinimizedEntryRestoreAvailability Unavailable(string reason)
        {
            reason = string.IsNullOrWhiteSpace(reason) ? "Unavailable" : reason.Trim();
            return new PungentMinimizedEntryRestoreAvailability
            {
                CanRestore = false,
                Reason = reason,
                Tooltip = "Unavailable: " + reason
            };
        }
    }

    public enum MinimizedStripAnchor
    {
        BottomLeft = 0,
        BottomCenter = 1,
        BottomRight = 2
    }

    public enum MinimizedOverlayFocusMode
    {
        AutoCollapse = 0,
        StayOpen = 1,
        AutoHide = 2
    }

    public enum MinimizedStripFocusMode
    {
        AutoCollapse = 0,
        StayOpen = 1,
        AutoHide = 2
    }

    /// <summary>
    /// Static minimizer API for registered PungentFunk utility windows.
    /// </summary>
    [InitializeOnLoad]
    public static class PungentUtilityMinimizer
    {
        private const string PrefEntries = "PungentFunkUtilities.MinimizedTray.Entries";
        private const string PrefBottomStripEnabled = "PungentFunkUtilities.MinimizedTray.BottomStripEnabled";
        private const string PrefStripAnchor = "PungentFunkUtilities.MinimizedTray.StripAnchor";
        private const string PrefStripOffsetX = "PungentFunkUtilities.MinimizedTray.StripOffsetX";
        private const string PrefStripOffsetY = "PungentFunkUtilities.MinimizedTray.StripOffsetY";
        private const string PrefStripFocusMode = "PungentFunkUtilities.MinimizedTray.FocusMode";
        private const string PrefStripCollapsed = "PungentFunkUtilities.MinimizedTray.Collapsed";
        private const string PrefTabPanelCollapsed = "PungentFunkUtilities.MinimizedTray.TabPanelCollapsed";
        private const string PrefOverlayFocusMode = "PungentFunkUtilities.MinimizedOverlay.FocusMode";
        private const string PrefOverlayCollapsed = "PungentFunkUtilities.MinimizedOverlay.Collapsed";
        private const string PrefOverlayHasPlacement = "PungentFunkUtilities.MinimizedOverlay.HasPlacement";
        private const string PrefOverlayX = "PungentFunkUtilities.MinimizedOverlay.X";
        private const string PrefOverlayY = "PungentFunkUtilities.MinimizedOverlay.Y";
        private const string PrefOverlayWidth = "PungentFunkUtilities.MinimizedOverlay.Width";
        private const string PrefOverlayHeight = "PungentFunkUtilities.MinimizedOverlay.Height";
        public static bool AllowGenericEditorWindowMinimization
        {
            get => EditorPrefs.GetBool(PrefAllowGenericEditorWindows, true);
            set
            {
                if (AllowGenericEditorWindowMinimization == value)
                    return;

                EditorPrefs.SetBool(PrefAllowGenericEditorWindows, value);
                NotifyChanged();
            }
        }

        private const float TrayFallbackMinTabWidth = 92f;
        private const float TrayFallbackMaxTabWidth = 240f;
        private const float TrayFallbackCharacterWidth = 7.25f;
        private const float TrayFallbackHorizontalPadding = 42f;

        private static bool TryGetToolbarButtonStyle(out GUIStyle style)
        {
            style = null;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return false;

            try
            {
                style = EditorStyles.toolbarButton;
                return style != null;
            }
            catch (System.NullReferenceException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
        }

        private static float EstimateTabWidthWithoutEditorStyles(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                label = "Utility";

            float estimated = label.Length * TrayFallbackCharacterWidth + TrayFallbackHorizontalPadding;
            return Mathf.Clamp(estimated, TrayFallbackMinTabWidth, TrayFallbackMaxTabWidth);
        }

        private const string PrefAllowGenericEditorWindows = "PungentFunkUtilities.MinimizedTray.AllowGenericEditorWindows";

        private const string TrayUtilityId = "utility-tray";
        private const string EntryKindPungentUtility = "pungent-utility";
        private const string EntryKindEditorWindow = "editor-window";
        private const string EntryKindPanelGroup = "panel-group";
        private const string GenericEntryPrefix = "editor-window:";

        private const double RepositionIntervalSeconds = 0.20d;
        private const double OverlayFocusGraceSeconds = 0.65d;
        private const double LastUtilityGraceSeconds = 45d;
        private const double LastEditorWindowGraceSeconds = 90d;
        private const double MenuFocusStealGuardSeconds = 1.25d;
        private const float TrayHeight = 30f;
        private const float TrayLeftMargin = 8f;
        private const float TrayBottomOffset = 26f;
        private const float TabSlimMinWidth = 26f;
        private const float TabSlimThreshold = 72f;
        private const float TrayActionButtonWidth = 34f;
        private const float TrayMenuButtonWidth = 44f;
        private const float TrayHandleWidth = 28f;
        private const float CollapsedStripSummaryWidth = 104f;
        private const float TrayControlSpacing = 3f;
        private const float OverlayHeaderHeight = 30f;
        private const float OverlayDefaultWidth = 380f;
        private const float OverlayCollapsedWidth = 318f;
        private const float OverlayRowHeight = 30f;
        private const float OverlayEmptyBodyHeight = 112f;
        private const float OverlayMaxRowsHeight = 260f;
        private const float OverlayFooterHeight = 178f;
        private const float OverlayMaxHeight = 560f;
        private const float OverlayMargin = 12f;

        private static readonly List<PungentMinimizedUtilityEntry> Entries = new List<PungentMinimizedUtilityEntry>();
        private static bool _loaded;
        private static bool _updateSubscribed;
        private static double _nextRepositionTime;
        private static Rect _lastTrayRect;
        private static Rect _lastOverlayActivatorRect;
        private static readonly Dictionary<Type, PungentUtilityDescriptor> DescriptorByWindowType = new Dictionary<Type, PungentUtilityDescriptor>();
        private static EditorWindow _lastUtilityWindow;
        private static EditorWindow _lastEditorWindow;
        private static EditorWindow _previousEditorWindow;
        private static EditorWindow _lastTrackedFocusedWindow;
        private static string _lastUtilityId;
        private static Type _lastEditorWindowType;
        private static Type _previousEditorWindowType;
        private static double _lastUtilitySeenAt;
        private static double _lastEditorWindowSeenAt;
        private static double _previousEditorWindowSeenAt;
        private static Type _containerWindowType;
        private static PropertyInfo _containerWindowShowModeProperty;
        private static PropertyInfo _containerWindowPositionProperty;
        private static FieldInfo _editorWindowParentField;
        private static Type _guiViewType;
        private static PropertyInfo _guiViewCurrentProperty;


        public static event Action EntriesChanged;

        static PungentUtilityMinimizer()
        {
            Load();
            PungentUtilityRegistry.Changed -= ClearDescriptorCache;
            PungentUtilityRegistry.Changed += ClearDescriptorCache;
            EditorApplication.delayCall += SyncTrayVisibility;

            EditorApplication.update -= TrackFocusedUtilityWindow;
            EditorApplication.update += TrackFocusedUtilityWindow;
        }

        public static IReadOnlyList<PungentMinimizedUtilityEntry> MinimizedEntries
        {
            get
            {
                Load();
                return Entries;
            }
        }

        public static int MinimizedCount
        {
            get
            {
                Load();
                return Entries.Count;
            }
        }

        public static bool BottomStripEnabled
        {
            get => EditorPrefs.GetBool(PrefBottomStripEnabled, true);
            set
            {
                if (BottomStripEnabled == value)
                    return;

                EditorPrefs.SetBool(PrefBottomStripEnabled, value);
                SyncTrayVisibility();
                NotifyChanged();
            }
        }

        internal static bool TabPanelCollapsed
        {
            get => EditorPrefs.GetBool(PrefTabPanelCollapsed, false);
            set => SetTabPanelCollapsed(value);
        }

        internal static void SetTabPanelCollapsed(bool collapsed)
        {
            if (TabPanelCollapsed == collapsed)
                return;

            EditorPrefs.SetBool(PrefTabPanelCollapsed, collapsed);
            SyncTrayVisibility();
            NotifyChanged();
        }

        internal static void ToggleTabPanelCollapsed()
        {
            SetTabPanelCollapsed(!TabPanelCollapsed);
        }

        public static MinimizedOverlayFocusMode OverlayFocusMode
        {
            get
            {
                int rawValue = EditorPrefs.HasKey(PrefOverlayFocusMode)
                    ? EditorPrefs.GetInt(PrefOverlayFocusMode, (int)MinimizedOverlayFocusMode.AutoCollapse)
                    : EditorPrefs.GetInt(PrefStripFocusMode, (int)MinimizedOverlayFocusMode.AutoCollapse);

                return Enum.IsDefined(typeof(MinimizedOverlayFocusMode), rawValue)
                    ? (MinimizedOverlayFocusMode)rawValue
                    : MinimizedOverlayFocusMode.AutoCollapse;
            }
            set
            {
                if (OverlayFocusMode == value)
                    return;

                EditorPrefs.SetInt(PrefOverlayFocusMode, (int)value);
                if (value == MinimizedOverlayFocusMode.StayOpen)
                    SetOverlayCollapsed(false);

                EnsureUpdateSubscription();
                RefreshOverlayWindowSize();
                NotifyChanged();
            }
        }

        public static bool OverlayCollapsed
        {
            get
            {
                return EditorPrefs.HasKey(PrefOverlayCollapsed)
                    ? EditorPrefs.GetBool(PrefOverlayCollapsed, false)
                    : EditorPrefs.GetBool(PrefStripCollapsed, false);
            }
            set => SetOverlayCollapsed(value);
        }

        [Obsolete("Use OverlayFocusMode. This compatibility alias now controls the minimized utilities overlay, not the tab panel.")]
        public static MinimizedStripFocusMode StripFocusMode
        {
            get => (MinimizedStripFocusMode)(int)OverlayFocusMode;
            set => OverlayFocusMode = (MinimizedOverlayFocusMode)(int)value;
        }

        [Obsolete("Use OverlayCollapsed. This compatibility alias now controls the minimized utilities overlay, not the tab panel.")]
        public static bool StripCollapsed
        {
            get => OverlayCollapsed;
            set => SetOverlayCollapsed(value);
        }

        public static MinimizedStripAnchor StripAnchor
        {
            get
            {
                int rawValue = EditorPrefs.GetInt(PrefStripAnchor, (int)MinimizedStripAnchor.BottomLeft);
                return Enum.IsDefined(typeof(MinimizedStripAnchor), rawValue)
                    ? (MinimizedStripAnchor)rawValue
                    : MinimizedStripAnchor.BottomLeft;
            }
            set
            {
                if (StripAnchor == value)
                    return;

                EditorPrefs.SetInt(PrefStripAnchor, (int)value);
                SyncTrayVisibility();
                NotifyChanged();
            }
        }

        public static Vector2 StripOffset
        {
            get => new Vector2(
                EditorPrefs.GetFloat(PrefStripOffsetX, 0f),
                EditorPrefs.GetFloat(PrefStripOffsetY, 0f));
            set
            {
                Vector2 current = StripOffset;
                if (Vector2.Distance(current, value) < 0.1f)
                    return;

                EditorPrefs.SetFloat(PrefStripOffsetX, value.x);
                EditorPrefs.SetFloat(PrefStripOffsetY, value.y);
                SyncTrayVisibility();
                NotifyChanged();
            }
        }

        public static void OpenTray()
        {
            ToggleOverlayTrayFromAccess(GetMenuPopupAnchorRect());
        }

        public static void SetOverlayCollapsed(bool collapsed)
        {
            if (OverlayCollapsed == collapsed)
                return;

            EditorPrefs.SetBool(PrefOverlayCollapsed, collapsed);
            RefreshOverlayWindowSize();
            EnsureUpdateSubscription();
            NotifyChanged();
        }

        public static void ToggleOverlayCollapsed()
        {
            SetOverlayCollapsed(!OverlayCollapsed);
        }

        public static void ShowOverlayTray(Rect activatorRect)
        {
            Load();

            if (activatorRect.width > 0f && activatorRect.height > 0f)
                _lastOverlayActivatorRect = activatorRect;

            PungentMinimizedUtilitiesOverlayWindow existing = PungentMinimizedUtilitiesOverlayWindow.FindOpenInstanceAndCloseDuplicates();
            Rect rect = existing != null
                ? GetOverlayRectForCurrentState(existing.position)
                : GetOverlayRectForOpen(activatorRect);

            PungentMinimizedUtilitiesOverlayWindow.ShowOrReposition(rect);
            EnsureUpdateSubscription();
        }

        public static void ToggleOverlayTrayFromAccess(Rect activatorRect)
        {
            Load();

            if (activatorRect.width > 0f && activatorRect.height > 0f)
                _lastOverlayActivatorRect = activatorRect;

            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.FindOpenInstanceAndCloseDuplicates();
            if (window == null)
            {
                ShowOverlayTray(activatorRect);
                return;
            }

            EnsureUpdateSubscription();

            switch (OverlayFocusMode)
            {
                case MinimizedOverlayFocusMode.AutoHide:
                    HideOverlayTray();
                    return;
                case MinimizedOverlayFocusMode.StayOpen:
                    window.FocusAndPulse();
                    return;
                default:
                    if (!OverlayCollapsed)
                        SetOverlayCollapsed(true);

                    window.FocusAndPulse();
                    return;
            }
        }

        public static void ShowOverlayTrayExpanded(Rect activatorRect)
        {
            SetOverlayCollapsed(false);
            ShowOverlayTray(activatorRect);
        }

        public static void ShowOverlayTrayCollapsed(Rect activatorRect)
        {
            SetOverlayCollapsed(true);
            ShowOverlayTray(activatorRect);
        }

        public static void HideOverlayTray()
        {
            CloseOverlayWindow();
            RemoveUpdateSubscriptionIfIdle();
            NotifyChanged();
        }

        public static void ResetOverlayPlacement()
        {
            EditorPrefs.DeleteKey(PrefOverlayHasPlacement);
            EditorPrefs.DeleteKey(PrefOverlayX);
            EditorPrefs.DeleteKey(PrefOverlayY);
            EditorPrefs.DeleteKey(PrefOverlayWidth);
            EditorPrefs.DeleteKey(PrefOverlayHeight);

            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.Instance;
            if (window != null)
            {
                window.ApplyOverlayRect(ComputeDefaultOverlayRect(new Rect(), OverlayCollapsed), true);
            }

            NotifyChanged();
        }

        [Obsolete("Use SetOverlayCollapsed. This compatibility alias now controls the minimized utilities overlay, not the tab panel.")]
        public static void SetStripCollapsed(bool collapsed)
        {
            SetOverlayCollapsed(collapsed);
        }

        [Obsolete("Use ToggleOverlayCollapsed. This compatibility alias now controls the minimized utilities overlay, not the tab panel.")]
        public static void ToggleStripCollapsed()
        {
            ToggleOverlayCollapsed();
        }

        [Obsolete("Use ShowOverlayTrayExpanded. This compatibility alias now opens the minimized utilities overlay expanded.")]
        public static void ShowBottomStripExpanded()
        {
            ShowOverlayTrayExpanded(GetMenuPopupAnchorRect());
        }

        [Obsolete("Use ShowOverlayTrayCollapsed. This compatibility alias now opens the minimized utilities overlay collapsed.")]
        public static void ShowBottomStripCollapsed()
        {
            ShowOverlayTrayCollapsed(GetMenuPopupAnchorRect());
        }

        [Obsolete("Use ShowOverlayTray. Minimized Utilities now opens as a movable overlay tray instead of PopupWindowContent.")]
        public static void ShowPopup(Rect activatorRect)
        {
            ShowOverlayTray(activatorRect);
        }

        public static void OpenMinimizedUtilitiesPopup(Rect activatorRect)
        {
            ShowOverlayTray(activatorRect);
        }

        public static void OpenMinimizedUtilitiesPopup()
        {
            ShowOverlayTray(GetMenuPopupAnchorRect());
        }

        [Obsolete("Use ShowOverlayTray. Minimized Utilities now opens as a movable overlay tray instead of PopupWindowContent.")]
        public static void ShowPopupFromMenu()
        {
            ShowOverlayTray(GetMenuPopupAnchorRect());
        }

        public static void ShowBottomStripIfNeeded()
        {
            Load();

            if (!BottomStripEnabled || Entries.Count == 0)
            {
                CloseTrayWindow();
                RemoveUpdateSubscriptionIfIdle();
                return;
            }

            Rect rect = ComputeTrayRect();
            PungentUtilityTrayWindow.ShowOrReposition(rect);
            _lastTrayRect = rect;
            EnsureUpdateSubscription();
        }

        public static void MinimizeFocusedUtility()
        {
            MinimizeCurrentEditorWindowFromShortcut();
        }

        public static void MinimizeFocusedUtility(bool preferPreMenuCandidate)
        {
            if (!TryResolveMinimizeCandidate(preferPreMenuCandidate, out EditorWindow window))
            {
                string source = preferPreMenuCandidate ? "pre-menu editor-window candidate" : "focused or hovered editor window";
                Debug.LogWarning($"PungentFunk Minimized Utilities: no eligible {source} was available to minimize. Use the overlay tray to restore existing minimized windows or enable generic editor-window minimization if needed.");
                ShowOverlayTray(GetMenuPopupAnchorRect());
                return;
            }

            if (!TryMinimizeWindow(window, true))
            {
                string title = window != null ? GetWindowDisplayName(window) : "selected window";
                Debug.LogWarning($"PungentFunk Minimized Utilities: '{title}' could not be minimized.");
                ShowOverlayTray(GetMenuPopupAnchorRect());
            }
        }

        public static void MinimizeLastEditorWindowFromCommand()
        {
            MinimizeFocusedUtility(true);
        }

        public static void MinimizeLastEditorWindowFromToolbar()
        {
            MinimizeFocusedUtility(true);
        }

        public static void MinimizeLastEditorWindowFromStrip()
        {
            MinimizeFocusedUtility(true);
        }

        public static void MinimizeCurrentEditorWindowFromShortcut()
        {
            MinimizeFocusedUtility(false);
        }

        public static void ResetStripPlacement()
        {
            EditorPrefs.SetInt(PrefStripAnchor, (int)MinimizedStripAnchor.BottomLeft);
            EditorPrefs.SetFloat(PrefStripOffsetX, 0f);
            EditorPrefs.SetFloat(PrefStripOffsetY, 0f);
            SyncTrayVisibility();
            NotifyChanged();
        }

        public static void ApplyStripPlacementPreset(MinimizedStripAnchor anchor)
        {
            EditorPrefs.SetInt(PrefStripAnchor, (int)anchor);
            EditorPrefs.SetFloat(PrefStripOffsetX, 0f);
            EditorPrefs.SetFloat(PrefStripOffsetY, 0f);
            SyncTrayVisibility();
            NotifyChanged();
        }

        private static void TrackFocusedUtilityWindow()
        {
            EditorWindow window = EditorWindow.focusedWindow;
            if (window == null)
                return;

            if (ReferenceEquals(window, _lastTrackedFocusedWindow))
                return;

            _lastTrackedFocusedWindow = window;
            RememberCandidateWindow(window);
            PruneOpenedUtility(window);
        }

        private static bool TryResolveMinimizeCandidate(bool preferPreMenuCandidate, out EditorWindow window)
        {
            window = null;

            if (preferPreMenuCandidate)
                return TryResolvePreMenuCandidate(out window);

            if (CanMinimizeWindow(EditorWindow.focusedWindow, out _))
            {
                window = EditorWindow.focusedWindow;
                RememberCandidateWindow(window);
                return true;
            }

            if (CanMinimizeWindow(EditorWindow.mouseOverWindow, out _))
            {
                window = EditorWindow.mouseOverWindow;
                RememberCandidateWindow(window);
                return true;
            }

            if (_lastEditorWindow != null &&
                EditorApplication.timeSinceStartup - _lastEditorWindowSeenAt <= LastEditorWindowGraceSeconds &&
                CanMinimizeWindow(_lastEditorWindow, out _))
            {
                window = _lastEditorWindow;
                return true;
            }

            if (_lastUtilityWindow != null &&
                !string.IsNullOrEmpty(_lastUtilityId) &&
                EditorApplication.timeSinceStartup - _lastUtilitySeenAt <= LastUtilityGraceSeconds &&
                CanMinimizeWindow(_lastUtilityWindow, out _))
            {
                window = _lastUtilityWindow;
                return true;
            }

            return false;
        }

        private static bool TryResolvePreMenuCandidate(out EditorWindow window)
        {
            window = null;
            double now = EditorApplication.timeSinceStartup;
            bool latestWasJustCaptured = _lastEditorWindow != null &&
                                         now - _lastEditorWindowSeenAt <= MenuFocusStealGuardSeconds;

            if (latestWasJustCaptured &&
                _previousEditorWindow != null &&
                now - _previousEditorWindowSeenAt <= LastEditorWindowGraceSeconds &&
                CanMinimizeWindow(_previousEditorWindow, out _))
            {
                window = _previousEditorWindow;
                return true;
            }

            if (_lastEditorWindow != null &&
                now - _lastEditorWindowSeenAt <= LastEditorWindowGraceSeconds &&
                CanMinimizeWindow(_lastEditorWindow, out _))
            {
                window = _lastEditorWindow;
                return true;
            }

            if (_lastUtilityWindow != null &&
                !string.IsNullOrEmpty(_lastUtilityId) &&
                now - _lastUtilitySeenAt <= LastUtilityGraceSeconds &&
                CanMinimizeWindow(_lastUtilityWindow, out _))
            {
                window = _lastUtilityWindow;
                return true;
            }

            return false;
        }

        private static bool TryGetUtilityDescriptor(EditorWindow window, out PungentUtilityDescriptor descriptor)
        {
            descriptor = null;

            if (window == null)
                return false;

            Type windowType = window.GetType();
            if (DescriptorByWindowType.TryGetValue(windowType, out descriptor))
                return descriptor != null && descriptor.CanOpen;

            descriptor = PungentUtilityRegistry.All.FirstOrDefault(u =>
            {
                Type type = u.ResolveWindowType();
                return type != null && type == windowType;
            });

            DescriptorByWindowType[windowType] = descriptor;
            return descriptor != null && descriptor.CanOpen;
        }

        private static void ClearDescriptorCache()
        {
            DescriptorByWindowType.Clear();
        }

        private static void RememberUtilityWindow(EditorWindow window, string utilityId)
        {
            if (window == null || string.IsNullOrWhiteSpace(utilityId))
                return;

            _lastUtilityWindow = window;
            _lastUtilityId = utilityId;
            _lastUtilitySeenAt = EditorApplication.timeSinceStartup;
        }

        private static void RememberCandidateWindow(EditorWindow window)
        {
            if (window == null)
                return;

            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor descriptor) &&
                !string.Equals(descriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
            {
                RememberUtilityWindow(window, descriptor.Id);
            }

            if (CanMinimizeGenericEditorWindow(window, out _))
                RememberEditorWindow(window);
        }

        private static void RememberEditorWindow(EditorWindow window)
        {
            if (window == null)
                return;

            if (!ReferenceEquals(window, _lastEditorWindow))
            {
                if (_lastEditorWindow != null &&
                    EditorApplication.timeSinceStartup - _lastEditorWindowSeenAt <= LastEditorWindowGraceSeconds &&
                    CanMinimizeWindow(_lastEditorWindow, out _))
                {
                    _previousEditorWindow = _lastEditorWindow;
                    _previousEditorWindowType = _lastEditorWindowType;
                    _previousEditorWindowSeenAt = _lastEditorWindowSeenAt;
                }
            }

            _lastEditorWindow = window;
            _lastEditorWindowType = window.GetType();
            _lastEditorWindowSeenAt = EditorApplication.timeSinceStartup;
        }

        public static bool TryMinimizeWindow(EditorWindow window, bool focusTray = false)
        {
            if (window == null)
                return false;

            Rect previousRect = window.position;

            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor descriptor) &&
                descriptor.CanOpen &&
                !string.Equals(descriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
            {
                AddOrReplaceEntry(descriptor, previousRect);
            }
            else if (CanMinimizeGenericEditorWindow(window, out _))
            {
                if (IsDocked(window) && !ConfirmMinimizeDockedGenericWindow(window))
                    return false;

                AddOrReplaceGenericEditorWindowEntry(window, previousRect);
            }
            else
            {
                return false;
            }

            window.Close();

            SyncTrayVisibility();

            if (focusTray)
                ShowOverlayTrayExpanded(GetMenuPopupAnchorRect());

            return true;
        }

        private static bool ConfirmMinimizeDockedGenericWindow(EditorWindow window)
        {
            string title = window != null ? GetWindowDisplayName(window) : "this editor window";
            return EditorUtility.DisplayDialog(
                "Minimize Docked Editor Window?",
                $"'{title}' is docked in the Unity layout.\n\nMinimizing a docked non-PungentFunk window may alter the current editor layout. Floating generic editor windows are unaffected.",
                "Minimize Anyway",
                "Cancel");
        }

        public static bool DrawHeaderMinimizeButton()
        {
            EditorWindow window = ResolveHeaderUtilityWindow();

            bool hasUtilityContext =
                TryGetUtilityDescriptor(window, out PungentUtilityDescriptor descriptor) &&
                !string.Equals(descriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase);

            bool canMinimize =
                hasUtilityContext &&
                CanMinimizeWindow(window, out _);

            GUIContent content = EditorGUIUtility.IconContent("winbtn_win_min");
            if (content == null || content.image == null)
                content = new GUIContent("-");

            content.tooltip = canMinimize ? "Minimize to Tray." : "Only registered PungentFunk utility windows can be minimized to tray.";

            using (new EditorGUI.DisabledScope(!canMinimize))
            {
                if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(30f), GUILayout.Height(22f)))
                {
                    EditorWindow captured = window;
                    EditorApplication.delayCall += () => TryMinimizeWindow(captured, false);
                    GUIUtility.ExitGUI();
                }
            }

            return true;
        }

        public static bool TryRestore(string entryKey)
        {
            if (string.IsNullOrWhiteSpace(entryKey))
                return false;

            Load();

            PungentMinimizedUtilityEntry entry = Entries.FirstOrDefault(e =>
                string.Equals(e.UtilityId, entryKey, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                return false;

            return TryRestoreAndRemove(entry, entry.PreviousRect, true, out _);
        }

        public static bool TryRestoreAt(string entryKey, Vector2 screenPosition)
        {
            return TryRestoreAt(entryKey, screenPosition, out _);
        }

        public static bool TryRestoreAt(string entryKey, Vector2 screenPosition, out EditorWindow restoredWindow)
        {
            restoredWindow = null;

            if (string.IsNullOrWhiteSpace(entryKey))
                return false;

            Load();

            PungentMinimizedUtilityEntry entry = Entries.FirstOrDefault(e =>
                string.Equals(e.UtilityId, entryKey, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                return false;

            Rect overrideRect = BuildRestoreRectNear(entry.PreviousRect, screenPosition);
            return TryRestoreAndRemove(entry, overrideRect, true, out restoredWindow);
        }

        private static bool TryRestoreAndRemove(PungentMinimizedUtilityEntry entry, Rect restoreRect, bool saveNotifySync, out EditorWindow restoredWindow)
        {
            restoredWindow = null;

            if (entry == null)
                return false;

            bool restored = TryRestoreEntry(entry, restoreRect, out restoredWindow);
            if (!restored)
                return false;

            Entries.Remove(entry);

            if (saveNotifySync)
            {
                Save();
                NotifyChanged();
                SyncTrayVisibility();
            }

            return true;
        }

        private static bool TryRestoreEntry(PungentMinimizedUtilityEntry entry, Rect restoreRect)
        {
            return TryRestoreEntry(entry, restoreRect, out _);
        }

        private static bool TryRestoreEntry(PungentMinimizedUtilityEntry entry, Rect restoreRect, out EditorWindow restoredWindow)
        {
            restoredWindow = null;

            if (entry == null)
                return false;

            if (!GetRestoreAvailability(entry).CanRestore)
                return false;

            if (IsGenericEditorWindowEntry(entry))
                return TryRestoreGenericEditorWindow(entry, restoreRect, out restoredWindow);

            if (IsPanelGroupEntry(entry))
                return false;

            return TryRestorePungentUtility(entry, restoreRect, out restoredWindow);
        }

        private static bool TryRestorePungentUtility(PungentMinimizedUtilityEntry entry, Rect restoreRect, out EditorWindow restoredWindow)
        {
            restoredWindow = null;

            if (entry == null)
                return false;

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(entry.UtilityId);
            if (descriptor == null || !descriptor.CanRun)
                return false;

            Type type = descriptor.ResolveWindowType();
            descriptor.OpenDirect();
            if (type != null)
            {
                restoredWindow = FindOpenWindow(type);
                if (restoredWindow != null)
                {
                    Rect sanitized = SanitizeRect(restoreRect);
                    restoredWindow.position = sanitized;
                    restoredWindow.Focus();
                }
            }

            RestoreWindowPositionDelayed(descriptor, restoreRect);
            return true;
        }

        private static bool TryRestoreGenericEditorWindow(PungentMinimizedUtilityEntry entry, Rect restoreRect, out EditorWindow restoredWindow)
        {
            restoredWindow = null;

            if (entry == null || string.IsNullOrWhiteSpace(entry.WindowTypeName))
                return false;

            Type type = Type.GetType(entry.WindowTypeName);
            if (type == null || !typeof(EditorWindow).IsAssignableFrom(type))
                return false;

            if (IsUnsafeGenericEditorWindowType(type))
                return false;

            try
            {
                Rect sanitized = SanitizeRect(restoreRect);
                string title = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? ObjectNames.NicifyVariableName(type.Name)
                    : entry.DisplayName;

                restoredWindow = EditorWindow.GetWindow(type, false, title, true);
                if (restoredWindow == null)
                    return false;

                restoredWindow.position = sanitized;
                restoredWindow.Focus();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"PungentFunk Minimized Utilities: failed to restore editor window '{entry.DisplayName}'. {ex.Message}");
                return false;
            }
        }

        internal static PungentMinimizedEntryRestoreAvailability GetRestoreAvailability(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Unavailable");

            if (IsPanelGroupEntry(entry))
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Panel group restore is not available yet");

            if (IsGenericEditorWindowEntry(entry))
                return GetGenericRestoreAvailability(entry);

            if (string.IsNullOrWhiteSpace(entry.UtilityId))
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Utility descriptor is missing");

            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(entry.UtilityId);
            if (descriptor == null)
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Utility descriptor is missing");

            if (!descriptor.CanOpen)
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Utility is not currently openable");

            if (!descriptor.CanRun)
            {
                string reason = string.IsNullOrWhiteSpace(descriptor.DisabledReason)
                    ? "Utility is not currently openable"
                    : descriptor.DisabledReason;
                return PungentMinimizedEntryRestoreAvailability.Unavailable(reason);
            }

            return PungentMinimizedEntryRestoreAvailability.Available("Restore minimized utility.");
        }

        private static PungentMinimizedEntryRestoreAvailability GetGenericRestoreAvailability(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.WindowTypeName))
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Editor window type could not be resolved");

            Type type = Type.GetType(entry.WindowTypeName);
            if (type == null)
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Editor window type could not be resolved");

            if (!typeof(EditorWindow).IsAssignableFrom(type))
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Editor window type is not an EditorWindow");

            if (IsUnsafeGenericEditorWindowType(type))
                return PungentMinimizedEntryRestoreAvailability.Unavailable("Editor window type is not restorable");

            return PungentMinimizedEntryRestoreAvailability.Available("Restore minimized editor window.");
        }

        public static bool IsMinimized(string utilityId)
        {
            Load();
            return Entries.Any(e => string.Equals(e.UtilityId, utilityId, StringComparison.OrdinalIgnoreCase));
        }

        public static void Remove(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId))
                return;

            Load();
            int removed = Entries.RemoveAll(e => string.Equals(e.UtilityId, utilityId, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0)
                return;

            Save();
            NotifyChanged();
            SyncTrayVisibility();
        }

        public static void CloseMinimizedEntry(string utilityId)
        {
            Remove(utilityId);
        }

        public static bool RestoreEntryFromTab(string utilityId)
        {
            return TryRestore(utilityId);
        }

        public static void PruneOpenedUtility(string utilityId)
        {
            if (string.IsNullOrWhiteSpace(utilityId) ||
                string.Equals(utilityId, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Remove(utilityId);
        }

        public static void PruneOpenedUtility(EditorWindow window)
        {
            if (window == null)
                return;

            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor descriptor))
                PruneOpenedUtility(descriptor.Id);
        }

        public static void RestoreAll()
        {
            Load();

            if (Entries.Count == 0)
                return;

            PungentMinimizedUtilityEntry[] snapshot = Entries.ToArray();
            bool changed = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                PungentMinimizedUtilityEntry entry = snapshot[i];
                if (entry == null || !Entries.Contains(entry))
                    continue;

                if (TryRestoreEntry(entry, entry.PreviousRect, out _))
                {
                    Entries.Remove(entry);
                    changed = true;
                }
            }

            if (!changed)
                return;

            Save();
            NotifyChanged();
            SyncTrayVisibility();
        }

        public static void ClearAll()
        {
            Load();

            if (Entries.Count == 0)
                return;

            Entries.Clear();
            Save();
            NotifyChanged();
            SyncTrayVisibility();
        }

        public static bool MinimizeUtilityById(string utilityId, bool focusTray = false)
        {
            PungentUtilityDescriptor descriptor = PungentUtilityRegistry.Find(utilityId);
            if (descriptor == null || string.Equals(descriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
                return false;

            Type type = descriptor.ResolveWindowType();
            EditorWindow window = type == null ? null : FindOpenWindow(type);
            if (window != null)
                return TryMinimizeWindow(window, focusTray);

            AddOrReplaceEntry(descriptor, new Rect(120f, 120f, 520f, 420f));
            SyncTrayVisibility();

            if (focusTray)
                ShowOverlayTrayExpanded(GetMenuPopupAnchorRect());

            return true;
        }

        internal static float GetTrayHeight() => TrayHeight;

        internal static float GetTrayActionButtonWidth() => TrayActionButtonWidth;

        internal static float GetTrayMenuButtonWidth() => TrayMenuButtonWidth;

        internal static float GetTrayHandleWidth() => TrayHandleWidth;

        internal static float GetCollapsedStripSummaryWidth() => CollapsedStripSummaryWidth;

        internal static float GetTrayControlSpacing() => TrayControlSpacing;

        internal static float GetTrayReservedControlWidth()
        {
            return TrayMenuButtonWidth + TrayActionButtonWidth + TrayHandleWidth + TrayControlSpacing * 4f;
        }

        internal static float GetCollapsedStripWidth()
        {
            return CollapsedStripSummaryWidth + GetTrayReservedControlWidth() + 2f;
        }

        internal static float GetTabWidth(PungentMinimizedUtilityEntry entry)
        {
            // Keep this line aligned with your entry model.
            // If your entry uses DisplayName/Name/WindowTitle instead of Title, swap only this expression.
            string label = entry != null ? entry.DisplayName : null;

            if (string.IsNullOrWhiteSpace(label))
                label = "Utility";

            if (!TryGetToolbarButtonStyle(out GUIStyle toolbarButton))
                return EstimateTabWidthWithoutEditorStyles(label);

            try
            {
                GUIContent content = new GUIContent(label);
                float measured = toolbarButton.CalcSize(content).x + TrayFallbackHorizontalPadding;
                return Mathf.Clamp(measured, TrayFallbackMinTabWidth, TrayFallbackMaxTabWidth);
            }
            catch (System.NullReferenceException)
            {
                return EstimateTabWidthWithoutEditorStyles(label);
            }
            catch (System.ArgumentException)
            {
                return EstimateTabWidthWithoutEditorStyles(label);
            }
            catch (System.InvalidOperationException)
            {
                return EstimateTabWidthWithoutEditorStyles(label);
            }
        }

        internal static float[] GetAdaptiveTabWidths(IReadOnlyList<PungentMinimizedUtilityEntry> entries, float availableWidth)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<float>();

            float[] desiredWidths = new float[entries.Count];
            float totalDesired = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                desiredWidths[i] = GetTabWidth(entries[i]);
                totalDesired += desiredWidths[i];
            }

            availableWidth = Mathf.Max(0f, availableWidth);
            if (totalDesired <= availableWidth)
                return desiredWidths;

            float[] widths = new float[entries.Count];
            float targetWidth = Mathf.Max(TabSlimMinWidth, availableWidth / entries.Count);
            float usedWidth = 0f;

            for (int i = 0; i < desiredWidths.Length; i++)
            {
                widths[i] = Mathf.Clamp(Mathf.Min(desiredWidths[i], targetWidth), TabSlimMinWidth, desiredWidths[i]);
                usedWidth += widths[i];
            }

            float remaining = availableWidth - usedWidth;
            while (remaining > 0.5f)
            {
                int expandableCount = 0;
                for (int i = 0; i < widths.Length; i++)
                {
                    if (widths[i] < desiredWidths[i] - 0.5f)
                        expandableCount++;
                }

                if (expandableCount == 0)
                    break;

                float share = remaining / expandableCount;
                float consumed = 0f;
                for (int i = 0; i < widths.Length; i++)
                {
                    if (widths[i] >= desiredWidths[i] - 0.5f)
                        continue;

                    float delta = Mathf.Min(share, desiredWidths[i] - widths[i]);
                    widths[i] += delta;
                    consumed += delta;
                }

                if (consumed <= 0.01f)
                    break;

                remaining -= consumed;
            }

            return widths;
        }

        internal static bool IsSlimTabWidth(float width)
        {
            return width < TabSlimThreshold;
        }

        internal static void TickTrayFollow()
        {
            Load();
            TickOverlayFocusMode();

            if (Entries.Count == 0 || !BottomStripEnabled)
            {
                CloseTrayWindow();
                RemoveUpdateSubscriptionIfIdle();
                return;
            }

            PungentUtilityTrayWindow trayWindow = PungentUtilityTrayWindow.Instance;
            if (trayWindow != null && trayWindow.IsInteracting)
                return;

            if (EditorApplication.timeSinceStartup < _nextRepositionTime)
                return;

            _nextRepositionTime = EditorApplication.timeSinceStartup + RepositionIntervalSeconds;
            Rect rect = ComputeTrayRect();
            if (Approximately(rect, _lastTrayRect))
                return;

            PungentUtilityTrayWindow.ShowOrReposition(rect);
            _lastTrayRect = rect;
        }

        private static void AddOrReplaceEntry(PungentUtilityDescriptor descriptor, Rect previousRect)
        {
            if (descriptor == null)
                return;

            Load();

            Entries.RemoveAll(e =>
                !IsGenericEditorWindowEntry(e) &&
                string.Equals(e.UtilityId, descriptor.Id, StringComparison.OrdinalIgnoreCase));

            Entries.Add(new PungentMinimizedUtilityEntry
            {
                EntryKind = EntryKindPungentUtility,
                UtilityId = descriptor.Id,
                DisplayName = descriptor.DisplayName,
                WindowTypeName = descriptor.WindowTypeName,
                PreviousRect = SanitizeRect(previousRect),
                MinimizedTicks = DateTime.UtcNow.Ticks
            });

            SortSaveAndNotify();
        }

        private static void AddOrReplaceGenericEditorWindowEntry(EditorWindow window, Rect previousRect)
        {
            if (window == null)
                return;

            Type type = window.GetType();
            string typeName = type.AssemblyQualifiedName;
            if (string.IsNullOrWhiteSpace(typeName))
                return;

            string key = BuildGenericEditorWindowKey(typeName);

            Load();

            Entries.RemoveAll(e =>
                IsGenericEditorWindowEntry(e) &&
                string.Equals(e.UtilityId, key, StringComparison.OrdinalIgnoreCase));

            Entries.Add(new PungentMinimizedUtilityEntry
            {
                EntryKind = EntryKindEditorWindow,
                UtilityId = key,
                DisplayName = GetWindowDisplayName(window),
                WindowTypeName = typeName,
                PreviousRect = SanitizeRect(previousRect),
                MinimizedTicks = DateTime.UtcNow.Ticks
            });

            SortSaveAndNotify();
        }

        private static void SortSaveAndNotify()
        {
            Entries.Sort((a, b) => a.MinimizedTicks.CompareTo(b.MinimizedTicks));
            Save();
            NotifyChanged();
        }

        private static bool CanMinimizeWindow(EditorWindow window, out PungentUtilityDescriptor descriptor)
        {
            descriptor = null;

            if (window == null)
                return false;

            if (TryGetUtilityDescriptor(window, out descriptor))
            {
                return descriptor.CanOpen &&
                       !string.Equals(descriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase);
            }

            return CanMinimizeGenericEditorWindow(window, out _);
        }

        private static bool CanMinimizeGenericEditorWindow(EditorWindow window, out string reason)
        {
            reason = null;

            if (!AllowGenericEditorWindowMinimization)
            {
                reason = "Generic editor-window minimization is disabled.";
                return false;
            }

            if (window == null)
            {
                reason = "No editor window is available.";
                return false;
            }

            Type type = window.GetType();

            if (type == typeof(PungentUtilityTrayWindow))
            {
                reason = "The Minimized Utilities tab panel cannot minimize itself.";
                return false;
            }

            if (type == typeof(PungentMinimizedUtilitiesOverlayWindow))
            {
                reason = "The Minimized Utilities overlay cannot minimize itself.";
                return false;
            }

            if (string.Equals(type.FullName, "PungentFunk.Utilities.Editor.Core.PungentMinimizedUtilitiesQuickAccessWindow", StringComparison.Ordinal) ||
                string.Equals(type.Name, "PungentMinimizedUtilitiesQuickAccessWindow", StringComparison.Ordinal))
            {
                reason = "The Minimized Utilities quick access surface cannot minimize itself.";
                return false;
            }

            if (TryGetUtilityDescriptor(window, out _))
            {
                reason = "Registered PungentFunk utilities use the registry restore path.";
                return false;
            }

            if (IsUnsafeGenericEditorWindowType(type))
            {
                reason = "Transient or modal editor windows cannot be minimized.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(type.AssemblyQualifiedName))
            {
                reason = "The editor window type cannot be restored by name.";
                return false;
            }

            return true;
        }

        private static bool IsUnsafeGenericEditorWindowType(Type type)
        {
            if (type == null)
                return true;

            string fullName = type.FullName ?? string.Empty;
            string name = type.Name ?? string.Empty;

            if (IndexOfIgnoreCase(fullName, "PopupWindow") >= 0 ||
                IndexOfIgnoreCase(fullName, "ObjectSelector") >= 0 ||
                IndexOfIgnoreCase(fullName, "ColorPicker") >= 0 ||
                IndexOfIgnoreCase(fullName, "AddComponent") >= 0 ||
                IndexOfIgnoreCase(fullName, "SearchWindow") >= 0 ||
                IndexOfIgnoreCase(fullName, "Menu") >= 0 ||
                IndexOfIgnoreCase(name, "Popup") >= 0)
            {
                return true;
            }

            return false;
        }

        private static int IndexOfIgnoreCase(string value, string search)
        {
            return string.IsNullOrEmpty(value)
                ? -1
                : value.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGenericEditorWindowEntry(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return false;

            if (string.Equals(entry.EntryKind, EntryKindEditorWindow, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(entry.UtilityId) &&
                   entry.UtilityId.StartsWith(GenericEntryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPanelGroupEntry(PungentMinimizedUtilityEntry entry)
        {
            return entry != null &&
                   string.Equals(entry.EntryKind, EntryKindPanelGroup, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetEntryKindLabel(PungentMinimizedUtilityEntry entry)
        {
            if (IsPanelGroupEntry(entry))
                return "Panel Group";

            return IsGenericEditorWindowEntry(entry) ? "Editor Window" : "PungentFunk";
        }

        private static string BuildGenericEditorWindowKey(string assemblyQualifiedTypeName)
        {
            return GenericEntryPrefix + assemblyQualifiedTypeName;
        }

        private static string GetWindowDisplayName(EditorWindow window)
        {
            if (window == null)
                return "Editor Window";

            string title = window.titleContent != null ? window.titleContent.text : null;
            if (!string.IsNullOrWhiteSpace(title))
                return title;

            return ObjectNames.NicifyVariableName(window.GetType().Name);
        }

        private static EditorWindow ResolveHeaderUtilityWindow()
        {
            EditorWindow window = GetCurrentOnGuiWindow();
            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor currentDescriptor) &&
                !string.Equals(currentDescriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
                return window;

            window = EditorWindow.mouseOverWindow;
            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor hoveredDescriptor) &&
                !string.Equals(hoveredDescriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
                return window;

            window = EditorWindow.focusedWindow;
            if (TryGetUtilityDescriptor(window, out PungentUtilityDescriptor focusedDescriptor) &&
                !string.Equals(focusedDescriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
                return window;

            if (_lastUtilityWindow != null &&
                TryGetUtilityDescriptor(_lastUtilityWindow, out PungentUtilityDescriptor cachedDescriptor) &&
                !string.Equals(cachedDescriptor.Id, TrayUtilityId, StringComparison.OrdinalIgnoreCase))
                return _lastUtilityWindow;

            return null;
        }

        private static EditorWindow GetCurrentOnGuiWindow()
        {
            try
            {
                if (_guiViewType == null)
                    _guiViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView");

                if (_guiViewCurrentProperty == null)
                    _guiViewCurrentProperty = _guiViewType?.GetProperty(
                        "current",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                object currentView = _guiViewCurrentProperty?.GetValue(null, null);
                if (currentView != null)
                {
                    PropertyInfo actualViewProperty = currentView
                        .GetType()
                        .GetProperty("actualView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (actualViewProperty?.GetValue(currentView, null) is EditorWindow currentWindow)
                        return currentWindow;
                }
            }
            catch
            {
                // Fall back below.
            }

            if (EditorWindow.mouseOverWindow != null)
                return EditorWindow.mouseOverWindow;

            return EditorWindow.focusedWindow;
        }

        private static bool IsDocked(EditorWindow window)
        {
            if (window == null)
                return false;

            try
            {
                if (_editorWindowParentField == null)
                    _editorWindowParentField = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
                object parent = _editorWindowParentField?.GetValue(window);
                return parent != null && string.Equals(parent.GetType().Name, "DockArea", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static void RestoreWindowPositionDelayed(PungentUtilityDescriptor descriptor, Rect rect)
        {
            if (descriptor == null)
                return;

            Type type = descriptor.ResolveWindowType();
            if (type == null)
                return;

            Rect sanitized = SanitizeRect(rect);
            ApplyWindowPositionDelayed(type, sanitized, 2);
        }

        private static void ApplyWindowPositionDelayed(Type type, Rect rect, int remainingPasses)
        {
            EditorApplication.delayCall += () =>
            {
                EditorWindow window = FindOpenWindow(type);
                if (window != null)
                {
                    window.position = rect;
                    window.Focus();
                }

                if (remainingPasses > 0)
                    ApplyWindowPositionDelayed(type, rect, remainingPasses - 1);
            };
        }

        private static EditorWindow FindOpenWindow(Type type)
        {
            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                if (window != null && window.GetType() == type)
                    return window;
            }

            return null;
        }

        private static void SyncTrayVisibility()
        {
            Load();

            if (Entries.Count == 0 || !BottomStripEnabled)
            {
                CloseTrayWindow();
                RemoveUpdateSubscriptionIfIdle();
                return;
            }

            ShowBottomStripIfNeeded();
        }

        private static void TickOverlayFocusMode()
        {
            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.Instance;
            if (window == null)
                return;

            if (IsOverlayActive())
                return;

            MinimizedOverlayFocusMode mode = OverlayFocusMode;
            if (mode == MinimizedOverlayFocusMode.AutoCollapse)
            {
                SetOverlayCollapsed(true);
                return;
            }

            if (mode == MinimizedOverlayFocusMode.AutoHide)
                CloseOverlayWindow();
        }

        private static bool IsOverlayActive()
        {
            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.Instance;
            if (window == null)
                return false;

            if (EditorWindow.focusedWindow == window || EditorWindow.mouseOverWindow == window)
                return true;

            if (window.IsInteracting)
                return true;

            return EditorApplication.timeSinceStartup - window.LastInteractionTime < OverlayFocusGraceSeconds;
        }

        private static void EnsureUpdateSubscription()
        {
            if (_updateSubscribed)
                return;

            EditorApplication.update += TickTrayFollow;
            _updateSubscribed = true;
        }

        private static void RemoveUpdateSubscription()
        {
            if (!_updateSubscribed)
                return;

            EditorApplication.update -= TickTrayFollow;
            _updateSubscribed = false;
        }

        private static void RemoveUpdateSubscriptionIfIdle()
        {
            if (PungentMinimizedUtilitiesOverlayWindow.Instance != null)
                return;

            if (BottomStripEnabled && Entries.Count > 0)
                return;

            RemoveUpdateSubscription();
        }

        private static void CloseTrayWindow()
        {
            PungentUtilityTrayWindow window = PungentUtilityTrayWindow.Instance;
            if (window != null)
                window.Close();
        }

        private static void CloseOverlayWindow()
        {
            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.Instance;
            if (window != null)
                window.Close();
        }

        private static Rect ComputeTrayRect()
        {
            Load();
            Rect main = GetMainEditorWindowRect();
            float usableLeft = main.x + TrayLeftMargin;
            float usableRight = main.xMax - TrayLeftMargin;
            float availableWidth = Mathf.Max(TabSlimMinWidth, usableRight - usableLeft);
            float minWidth = TabPanelCollapsed
                ? GetCollapsedStripWidth()
                : GetTrayReservedControlWidth() + TabSlimMinWidth;
            float desiredWidth = TabPanelCollapsed
                ? GetCollapsedStripWidth()
                : Entries.Sum(GetTabWidth) + GetTrayReservedControlWidth() + 2f;
            float width = Mathf.Min(availableWidth, Mathf.Max(minWidth, desiredWidth));
            float baseX = ComputeAnchorX(main, width, StripAnchor);
            float baseY = main.yMax - TrayBottomOffset - TrayHeight;
            Vector2 offset = StripOffset;
            Rect rect = new Rect(baseX + offset.x, baseY + offset.y, width, TrayHeight);
            return ClampTrayRect(rect, main);
        }

        internal static Rect GetMainEditorWindowRectForMinimizedUtilities()
        {
            return GetMainEditorWindowRect();
        }

        internal static float GetOverlayHeaderHeight() => OverlayHeaderHeight;

        internal static float GetOverlayRowHeight() => OverlayRowHeight;

        internal static float GetOverlayBodyHeight(int count)
        {
            return count == 0
                ? OverlayEmptyBodyHeight
                : Mathf.Min(OverlayMaxRowsHeight, count * OverlayRowHeight + 12f);
        }

        internal static float GetOverlayHeight(bool collapsed, int count)
        {
            if (collapsed)
                return OverlayHeaderHeight;

            return Mathf.Min(
                OverlayMaxHeight,
                OverlayHeaderHeight + GetOverlayBodyHeight(count) + OverlayFooterHeight);
        }

        internal static Rect GetOverlayRectForCurrentState(Rect currentRect)
        {
            float width = OverlayCollapsed
                ? OverlayCollapsedWidth
                : Mathf.Max(OverlayDefaultWidth, currentRect.width);
            float height = GetOverlayHeight(OverlayCollapsed, MinimizedCount);
            return ClampOverlayRect(new Rect(currentRect.x, currentRect.y, width, height));
        }

        internal static void SaveOverlayPlacement(Rect rect)
        {
            Rect clamped = ClampOverlayRect(rect);
            EditorPrefs.SetBool(PrefOverlayHasPlacement, true);
            EditorPrefs.SetFloat(PrefOverlayX, clamped.x);
            EditorPrefs.SetFloat(PrefOverlayY, clamped.y);
            EditorPrefs.SetFloat(PrefOverlayWidth, clamped.width);
            EditorPrefs.SetFloat(PrefOverlayHeight, clamped.height);
        }

        internal static Rect ClampOverlayRect(Rect rect)
        {
            Rect main = GetMainEditorWindowRect();
            float usableLeft = main.x + OverlayMargin;
            float usableRight = main.xMax - OverlayMargin;
            float usableTop = main.y + OverlayMargin;
            float usableBottom = main.yMax - OverlayMargin;
            float maxWidth = Mathf.Max(OverlayCollapsedWidth, usableRight - usableLeft);
            float width = Mathf.Min(Mathf.Max(OverlayCollapsedWidth, rect.width), maxWidth);
            float height = Mathf.Min(Mathf.Max(OverlayHeaderHeight, rect.height), Mathf.Max(OverlayHeaderHeight, usableBottom - usableTop));
            float x = Mathf.Clamp(rect.x, usableLeft, Mathf.Max(usableLeft, usableRight - width));
            float y = Mathf.Clamp(rect.y, usableTop, Mathf.Max(usableTop, usableBottom - height));
            return new Rect(x, y, width, height);
        }

        internal static void SetStripPlacementFromScreenRect(Rect requestedRect, bool inferNearestAnchor)
        {
            Rect main = GetMainEditorWindowRect();
            Rect clamped = ClampStripScreenRect(requestedRect);

            MinimizedStripAnchor anchor = inferNearestAnchor
                ? InferNearestAnchor(main, clamped)
                : StripAnchor;

            float baseX = ComputeAnchorX(main, clamped.width, anchor);
            float baseY = main.yMax - TrayBottomOffset - TrayHeight;
            EditorPrefs.SetInt(PrefStripAnchor, (int)anchor);
            EditorPrefs.SetFloat(PrefStripOffsetX, clamped.x - baseX);
            EditorPrefs.SetFloat(PrefStripOffsetY, clamped.y - baseY);
            SyncTrayVisibility();
            NotifyChanged();
        }

        internal static Rect ClampStripScreenRect(Rect requestedRect)
        {
            Rect main = GetMainEditorWindowRect();
            return ClampTrayRect(
                new Rect(requestedRect.x, requestedRect.y, Mathf.Max(1f, requestedRect.width), TrayHeight),
                main);
        }

        private static float ComputeAnchorX(Rect main, float width, MinimizedStripAnchor anchor)
        {
            switch (anchor)
            {
                case MinimizedStripAnchor.BottomCenter:
                    return main.x + (main.width - width) * 0.5f;
                case MinimizedStripAnchor.BottomRight:
                    return main.xMax - TrayLeftMargin - width;
                default:
                    return main.x + TrayLeftMargin;
            }
        }

        private static Rect ClampTrayRect(Rect rect, Rect main)
        {
            float usableLeft = main.x + TrayLeftMargin;
            float usableRight = main.xMax - TrayLeftMargin;
            float usableTop = main.y + TrayLeftMargin;
            float usableBottom = main.yMax - TrayBottomOffset;
            float width = Mathf.Min(Mathf.Max(1f, rect.width), Mathf.Max(1f, usableRight - usableLeft));
            float height = Mathf.Max(TrayHeight, rect.height);
            float x = Mathf.Clamp(rect.x, usableLeft, Mathf.Max(usableLeft, usableRight - width));
            float y = Mathf.Clamp(rect.y, usableTop, Mathf.Max(usableTop, usableBottom - height));
            return new Rect(x, y, width, height);
        }

        private static MinimizedStripAnchor InferNearestAnchor(Rect main, Rect stripRect)
        {
            float leftDistance = Mathf.Abs(stripRect.x - (main.x + TrayLeftMargin));
            float centerDistance = Mathf.Abs(stripRect.center.x - main.center.x);
            float rightDistance = Mathf.Abs(stripRect.xMax - (main.xMax - TrayLeftMargin));

            if (centerDistance <= leftDistance && centerDistance <= rightDistance)
                return MinimizedStripAnchor.BottomCenter;

            return rightDistance < leftDistance
                ? MinimizedStripAnchor.BottomRight
                : MinimizedStripAnchor.BottomLeft;
        }

        private static Rect GetMenuPopupAnchorRect()
        {
            EditorWindow anchorWindow = EditorWindow.focusedWindow != null
                ? EditorWindow.focusedWindow
                : _lastUtilityWindow;

            if (anchorWindow != null)
            {
                Rect position = anchorWindow.position;
                return new Rect(
                    position.x + Mathf.Min(180f, Mathf.Max(32f, position.width * 0.5f)),
                    position.y + 28f,
                    1f,
                    1f);
            }

            Rect main = GetMainEditorWindowRect();
            return new Rect(main.x + 72f, main.y + 48f, 1f, 1f);
        }

        private static Rect GetOverlayRectForOpen(Rect activatorRect)
        {
            if (TryGetSavedOverlayRect(out Rect saved))
                return GetOverlayRectForCurrentState(saved);

            return ComputeDefaultOverlayRect(activatorRect, OverlayCollapsed);
        }

        private static bool TryGetSavedOverlayRect(out Rect rect)
        {
            rect = default;
            if (!EditorPrefs.GetBool(PrefOverlayHasPlacement, false))
                return false;

            rect = new Rect(
                EditorPrefs.GetFloat(PrefOverlayX, 0f),
                EditorPrefs.GetFloat(PrefOverlayY, 0f),
                EditorPrefs.GetFloat(PrefOverlayWidth, OverlayDefaultWidth),
                EditorPrefs.GetFloat(PrefOverlayHeight, GetOverlayHeight(false, MinimizedCount)));

            return IsUsableRect(rect);
        }

        private static Rect ComputeDefaultOverlayRect(Rect activatorRect, bool collapsed)
        {
            Rect main = GetMainEditorWindowRect();
            float width = collapsed ? OverlayCollapsedWidth : OverlayDefaultWidth;
            float height = GetOverlayHeight(collapsed, MinimizedCount);

            return ClampOverlayRect(new Rect(
                main.xMax - width - 18f,
                main.y + 76f,
                width,
                height));
        }

        private static bool IsUsableRect(Rect rect)
        {
            return rect.width > 0f &&
                   rect.height > 0f &&
                   !float.IsNaN(rect.x) &&
                   !float.IsNaN(rect.y) &&
                   !float.IsNaN(rect.width) &&
                   !float.IsNaN(rect.height) &&
                   !float.IsInfinity(rect.x) &&
                   !float.IsInfinity(rect.y) &&
                   !float.IsInfinity(rect.width) &&
                   !float.IsInfinity(rect.height);
        }

        private static void RefreshOverlayWindowSize()
        {
            PungentMinimizedUtilitiesOverlayWindow window = PungentMinimizedUtilitiesOverlayWindow.Instance;
            if (window != null)
                window.ApplyOverlayRect(GetOverlayRectForCurrentState(window.position), true);
        }

        private static Rect GetMainEditorWindowRect()
        {
            try
            {
                if (_containerWindowType == null)
                    _containerWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");

                if (_containerWindowShowModeProperty == null)
                    _containerWindowShowModeProperty = _containerWindowType?.GetProperty(
                        "showMode",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (_containerWindowPositionProperty == null)
                    _containerWindowPositionProperty = _containerWindowType?.GetProperty(
                        "position",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (_containerWindowType != null &&
                    _containerWindowShowModeProperty != null &&
                    _containerWindowPositionProperty != null)
                {
                    UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(_containerWindowType);

                    Rect bestMainRect = default;
                    float bestMainArea = -1f;

                    Rect bestAnyRect = default;
                    float bestAnyArea = -1f;

                    for (int i = 0; i < windows.Length; i++)
                    {
                        object window = windows[i];
                        if (window == null)
                            continue;

                        object rawRect = _containerWindowPositionProperty.GetValue(window, null);
                        if (!(rawRect is Rect rect))
                            continue;

                        // Ignore tiny popups, utility popovers, and transient chrome.
                        if (rect.width < 320f || rect.height < 240f)
                            continue;

                        float area = rect.width * rect.height;
                        if (area > bestAnyArea)
                        {
                            bestAnyArea = area;
                            bestAnyRect = rect;
                        }

                        int showMode = -1;
                        try
                        {
                            object rawShowMode = _containerWindowShowModeProperty.GetValue(window, null);
                            showMode = Convert.ToInt32(rawShowMode);
                        }
                        catch
                        {
                            showMode = -1;
                        }

                        // Unity commonly uses showMode 4 for the main editor window.
                        // There can still be multiple candidates, so choose the largest.
                        if (showMode == 4 && area > bestMainArea)
                        {
                            bestMainArea = area;
                            bestMainRect = rect;
                        }
                    }

                    if (bestMainArea > 0f)
                        return bestMainRect;

                    if (bestAnyArea > 0f)
                        return bestAnyRect;
                }
            }
            catch
            {
                // Fall through to a stable editor-sized fallback.
            }

            return new Rect(
                0f,
                0f,
                Mathf.Max(900f, Screen.currentResolution.width),
                Mathf.Max(600f, Screen.currentResolution.height));
        }

        private static Rect SanitizeRect(Rect rect)
        {
            Rect main = GetMainEditorWindowRect();
            float x = IsFinite(rect.x) ? rect.x : main.x + 72f;
            float y = IsFinite(rect.y) ? rect.y : main.y + 72f;
            float width = IsFinite(rect.width) && rect.width > 0f ? rect.width : 520f;
            float height = IsFinite(rect.height) && rect.height > 0f ? rect.height : 420f;
            return new Rect(x, y, Mathf.Clamp(width, 180f, 1800f), Mathf.Clamp(height, 120f, 1200f));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Rect BuildRestoreRectNear(Rect previousRect, Vector2 screenPosition)
        {
            Rect sanitized = SanitizeRect(previousRect);
            return SanitizeRect(new Rect(
                screenPosition.x - sanitized.width * 0.5f,
                screenPosition.y - 18f,
                sanitized.width,
                sanitized.height));
        }

        internal static Rect GetRestoreRectNear(Rect previousRect, Vector2 screenPosition)
        {
            return BuildRestoreRectNear(previousRect, screenPosition);
        }

        private static bool Approximately(Rect a, Rect b)
        {
            return Mathf.Abs(a.x - b.x) < 0.5f &&
                   Mathf.Abs(a.y - b.y) < 0.5f &&
                   Mathf.Abs(a.width - b.width) < 0.5f &&
                   Mathf.Abs(a.height - b.height) < 0.5f;
        }

        private static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;
            Entries.Clear();
            string json = EditorPrefs.GetString(PrefEntries, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                PungentMinimizedUtilityEntrySet set = JsonUtility.FromJson<PungentMinimizedUtilityEntrySet>(json);
                if (set?.Items != null)
                    Entries.AddRange(set.Items.Where(IsValidEntry).OrderBy(e => e.MinimizedTicks));
            }
            catch
            {
                Entries.Clear();
            }
        }

        private static void Save()
        {
            _loaded = true;
            Entries.RemoveAll(e => !IsValidEntry(e));
            EditorPrefs.SetString(PrefEntries, JsonUtility.ToJson(new PungentMinimizedUtilityEntrySet { Items = Entries }));
        }

        private static bool IsValidEntry(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return false;

            if (IsGenericEditorWindowEntry(entry))
            {
                return !string.IsNullOrWhiteSpace(entry.UtilityId) &&
                       !string.IsNullOrWhiteSpace(entry.WindowTypeName);
            }

            return !string.IsNullOrWhiteSpace(entry.UtilityId) &&
                   !string.Equals(entry.UtilityId, TrayUtilityId, StringComparison.OrdinalIgnoreCase);
        }

        private static void NotifyChanged()
        {
            EntriesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Optional minimized utility tab strip. Intentionally avoids Unity docking internals.
    /// </summary>
    public sealed class PungentUtilityTrayWindow : EditorWindow
    {
        private const float ExpandedSlimHeight = 78f;
        private const float ExpandedSlimWidth = 270f;
        private const float DragRestoreThreshold = 18f;

        private static GUIStyle _tabStyle;
        private static GUIStyle _slimTabStyle;
        private static GUIStyle _tabLabelStyle;
        private static GUIStyle _slimTabLabelStyle;
        private static GUIStyle _tabActionStyle;
        private static GUIStyle _expandedSlimStyle;
        private static GUIStyle _stripActionStyle;
        private static GUIStyle _stripHandleStyle;

        private string _expandedSlimEntryKey;
        private Rect _expandedSlimSourceRect;
        private string _selectedEntryKey;
        private string _hoveredEntryKey;
        private string _dragEntryKey;
        private Vector2 _dragStartMousePosition;
        private bool _dragRestoreArmed;
        private EditorWindow _liveDragWindow;
        private Rect _liveDragPreviousRect;
        private Vector2 _handleDragStartScreenPosition;
        private Rect _handleDragStartStripRect;
        private bool _handleDragging;
        private bool _handleDragMoved;

        public static PungentUtilityTrayWindow Instance { get; private set; }

        public static int MinimizedCount => PungentUtilityMinimizer.MinimizedCount;

        public static void Open() => PungentUtilityMinimizer.OpenTray();

        public static void MinimizeFocusedUtility() => PungentUtilityMinimizer.MinimizeCurrentEditorWindowFromShortcut();

        public static void MinimizeFocusedUtility(bool preferPreMenuCandidate) => PungentUtilityMinimizer.MinimizeFocusedUtility(preferPreMenuCandidate);

        public static void ParkUtility(string utilityId, bool focusTray = false)
        {
            PungentUtilityMinimizer.MinimizeUtilityById(utilityId, focusTray);
        }

        public static bool TryOpenMinimized(string utilityId) => PungentUtilityMinimizer.TryRestore(utilityId);

        public static bool IsParked(string utilityId) => PungentUtilityMinimizer.IsMinimized(utilityId);

        internal bool IsInteracting => _handleDragging || _dragRestoreArmed || !string.IsNullOrEmpty(_dragEntryKey);

        internal static void ShowOrReposition(Rect rect)
        {
            PungentUtilityTrayWindow window = Instance;
            if (window == null)
            {
                window = CreateInstance<PungentUtilityTrayWindow>();
                window.titleContent = new GUIContent("PungentFunk Minimized Utilities");
                window.ApplyTrayRect(rect);
                window.ShowPopup();
            }

            window.ApplyTrayRect(rect);
            window.Repaint();
        }

        private void OnEnable()
        {
            Instance = this;
            titleContent = new GUIContent("Minimized Utilities");
            wantsMouseMove = true;
            PungentUtilityMinimizer.EntriesChanged += OnEntriesChanged;

            if (PungentUtilityMinimizer.MinimizedCount == 0)
                EditorApplication.delayCall += Close;
        }

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;

            PungentUtilityMinimizer.EntriesChanged -= OnEntriesChanged;
        }

        private void OnGUI()
        {
            IReadOnlyList<PungentMinimizedUtilityEntry> entries = PungentUtilityMinimizer.MinimizedEntries;
            if (entries.Count == 0)
            {
                EditorApplication.delayCall += Close;
                return;
            }

            EnsureStyles();
            _hoveredEntryKey = null;
            Rect full = new Rect(0f, 0f, position.width, position.height);
            Rect trayRect = new Rect(0f, position.height - PungentUtilityMinimizer.GetTrayHeight(), position.width, PungentUtilityMinimizer.GetTrayHeight());
            EditorGUI.DrawRect(full, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.12f, 0.13f, 0.96f) : new Color(0.78f, 0.78f, 0.80f, 0.96f));

            BuildControlRects(trayRect, out Rect contentRect, out Rect trayButtonRect, out Rect minimizeRect, out Rect handleRect);
            PungentMinimizedUtilityEntry expandedEntry = null;
            if (PungentUtilityMinimizer.TabPanelCollapsed)
            {
                ClearExpandedSlimTab(false);
                DrawCollapsedTabPanelSummary(contentRect, entries.Count);
            }
            else
            {
                float[] tabWidths = PungentUtilityMinimizer.GetAdaptiveTabWidths(entries, contentRect.width);
                float x = contentRect.x;
                for (int i = 0; i < entries.Count; i++)
                {
                    PungentMinimizedUtilityEntry entry = entries[i];
                    float width = i < tabWidths.Length ? tabWidths[i] : PungentUtilityMinimizer.GetTabWidth(entry);
                    if (x >= contentRect.xMax)
                        break;

                    width = Mathf.Min(width, Mathf.Max(0f, contentRect.xMax - x));
                    Rect tabRect = new Rect(x, trayRect.y + 1f, width, trayRect.height - 2f);
                    bool slim = PungentUtilityMinimizer.IsSlimTabWidth(width);
                    if (IsEntryKey(entry, _expandedSlimEntryKey))
                    {
                        expandedEntry = entry;
                        _expandedSlimSourceRect = tabRect;
                    }

                    DrawTab(entry, tabRect, slim);
                    x += width - 1f;
                }
            }

            DrawTrayMenuButton(trayButtonRect);
            DrawMinimizeLastButton(minimizeRect);
            DrawMoveHandle(handleRect);
            DrawStripContextMenu(trayRect);

            if (!PungentUtilityMinimizer.TabPanelCollapsed && !string.IsNullOrEmpty(_expandedSlimEntryKey))
            {
                if (expandedEntry != null)
                    DrawExpandedSlimTab(expandedEntry, _expandedSlimSourceRect, position.width, trayRect.y);
                else
                    ClearExpandedSlimTab(true);
            }

            HandleStripKeyboard(Event.current);
        }

        private static void DrawCollapsedTabPanelSummary(Rect rect, int count)
        {
            string label = count == 1 ? "Minimized 1" : "Minimized " + count;
            GUIContent content = new GUIContent(label, "Minimized tab panel is collapsed. Double-click the drag handle to expand it.");
            GUI.Box(rect, GUIContent.none, _tabStyle);
            GUI.Label(rect, content, _tabLabelStyle);
        }

        private static void BuildControlRects(Rect trayRect, out Rect contentRect, out Rect trayButtonRect, out Rect minimizeRect, out Rect handleRect)
        {
            float minWidth = PungentUtilityMinimizer.GetTrayActionButtonWidth();
            float trayWidth = PungentUtilityMinimizer.GetTrayMenuButtonWidth();
            float handleWidth = PungentUtilityMinimizer.GetTrayHandleWidth();
            float spacing = PungentUtilityMinimizer.GetTrayControlSpacing();

            handleRect = new Rect(trayRect.xMax - handleWidth - 1f, trayRect.y + 2f, handleWidth, trayRect.height - 4f);
            minimizeRect = new Rect(handleRect.x - spacing - minWidth, trayRect.y + 2f, minWidth, trayRect.height - 4f);
            trayButtonRect = new Rect(minimizeRect.x - spacing - trayWidth, trayRect.y + 2f, trayWidth, trayRect.height - 4f);
            contentRect = new Rect(1f, trayRect.y + 1f, Mathf.Max(0f, trayButtonRect.x - spacing - 1f), trayRect.height - 2f);
        }

        private void DrawTab(PungentMinimizedUtilityEntry entry, Rect rect, bool slim)
        {
            Event evt = Event.current;
            string displayName = GetDisplayName(entry);
            string kind = PungentUtilityMinimizer.GetEntryKindLabel(entry);
            PungentMinimizedEntryRestoreAvailability availability = PungentUtilityMinimizer.GetRestoreAvailability(entry);
            bool selected = IsEntryKey(entry, _selectedEntryKey);
            bool drawActions = !slim && rect.width >= 52f;
            Rect closeRect = drawActions ? new Rect(rect.xMax - 19f, rect.y + 5f, 14f, 14f) : default;
            Rect restoreRect = drawActions ? new Rect(closeRect.x - 17f, rect.y + 5f, 14f, 14f) : default;
            Rect labelRect = drawActions
                ? new Rect(rect.x, rect.y, Mathf.Max(0f, restoreRect.x - rect.x - 2f), rect.height)
                : rect;

            if (rect.Contains(evt.mousePosition))
                _hoveredEntryKey = entry.UtilityId;

            if (evt.type == EventType.MouseDown && evt.button == 1 && rect.Contains(evt.mousePosition))
            {
                ShowContextMenu(entry);
                evt.Use();
                return;
            }

            if (HandleDragRestoreEvent(entry, labelRect, evt, availability))
                return;

            if (HandleTabClickEvent(entry, labelRect, evt, slim, availability))
                return;

            string restoreHint = availability.CanRestore
                ? "Double-click, press Enter, or press - to restore."
                : "Restore unavailable: " + availability.Reason + ".";
            string removeHint = "Press x, Delete, or Backspace to close this minimized entry.";
            GUIContent label = slim
                ? new GUIContent(GetSlimLabel(displayName), $"{displayName}\n{kind}\nSingle-click to select. {restoreHint} Right-click for actions. Drag out to restore floating.")
                : new GUIContent(displayName, $"{kind}: {displayName}\nSingle-click to select. {restoreHint} {removeHint} Drag out to restore floating.");

            DrawTabSurface(rect, slim, selected);

            DrawTabLabel(labelRect, label, slim, selected);

            if (slim)
            {
                if (selected)
                    DrawSelectedTabBorder(rect);
                return;
            }

            using (new EditorGUI.DisabledScope(!availability.CanRestore))
            {
                if (DrawTabActionButton(restoreRect, new GUIContent("-", availability.Tooltip), selected))
                    RestoreEntryFromStrip(entry, availability);
            }

            if (DrawTabActionButton(closeRect, new GUIContent("x", "Close this minimized entry without restoring the window."), selected))
            {
                CloseEntryFromStrip(entry);
            }

            if (selected)
                DrawSelectedTabBorder(rect);
        }

        private bool HandleTabClickEvent(PungentMinimizedUtilityEntry entry, Rect rect, Event evt, bool slim, PungentMinimizedEntryRestoreAvailability availability)
        {
            if (entry == null ||
                evt.type != EventType.MouseDown ||
                evt.button != 0 ||
                !rect.Contains(evt.mousePosition))
            {
                return false;
            }

            Focus();

            if (evt.clickCount >= 2)
            {
                evt.Use();
                RestoreEntryFromStrip(entry, availability);
                return true;
            }

            if (IsEntryKey(entry, _selectedEntryKey))
            {
                evt.Use();
                RestoreEntryFromStrip(entry, availability);
                return true;
            }

            SelectEntry(entry, rect, slim);
            Repaint();
            evt.Use();
            return true;
        }

        private void SelectEntry(PungentMinimizedUtilityEntry entry, Rect rect, bool slim)
        {
            if (entry == null)
                return;

            _selectedEntryKey = entry.UtilityId;
            if (!slim)
                return;

            _expandedSlimEntryKey = entry.UtilityId;
            _expandedSlimSourceRect = rect;
            EditorApplication.delayCall += PungentUtilityMinimizer.ShowBottomStripIfNeeded;
        }

        private void RestoreEntryFromStrip(PungentMinimizedUtilityEntry entry, PungentMinimizedEntryRestoreAvailability availability)
        {
            if (entry == null)
                return;

            if (!availability.CanRestore)
            {
                _selectedEntryKey = entry.UtilityId;
                Repaint();
                return;
            }

            ClearDragState();
            ClearExpandedSlimTab(false);
            PungentUtilityMinimizer.RestoreEntryFromTab(entry.UtilityId);
            GUIUtility.ExitGUI();
        }

        private void CloseEntryFromStrip(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return;

            ClearDragState();
            ClearExpandedSlimTab(false);
            if (IsEntryKey(entry, _selectedEntryKey))
                _selectedEntryKey = null;

            PungentUtilityMinimizer.CloseMinimizedEntry(entry.UtilityId);
            GUIUtility.ExitGUI();
        }

        private static void DrawTabSurface(Rect rect, bool slim, bool selected)
        {
            GUI.Box(rect, GUIContent.none, slim ? _slimTabStyle : _tabStyle);

            if (selected)
                DrawSelectedTabFill(rect);
        }

        private static void DrawSelectedTabFill(Rect rect)
        {
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.20f, 0.36f, 0.68f, 0.26f)
                : new Color(0.20f, 0.43f, 0.86f, 0.18f);

            EditorGUI.DrawRect(rect, fill);
        }

        private static void DrawSelectedTabBorder(Rect rect)
        {
            Color border = GetSelectedTabTextColor();
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        private static void DrawTabLabel(Rect rect, GUIContent content, bool slim, bool selected)
        {
            Color previous = GUI.contentColor;
            if (selected)
                GUI.contentColor = GetSelectedTabTextColor();

            GUI.Label(rect, content, slim ? _slimTabLabelStyle : _tabLabelStyle);
            GUI.contentColor = previous;
        }

        private static bool DrawTabActionButton(Rect rect, GUIContent content, bool selected)
        {
            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
                DrawTabActionHover(rect, selected);

            Color previous = GUI.contentColor;
            if (selected)
                GUI.contentColor = GetSelectedTabTextColor();

            bool clicked = GUI.Button(rect, content, _tabActionStyle);
            GUI.contentColor = previous;
            return clicked;
        }

        private static void DrawTabActionHover(Rect rect, bool selected)
        {
            Color hover = selected
                ? GetSelectedTabTextColor()
                : (EditorGUIUtility.isProSkin ? Color.white : Color.black);

            hover.a = selected ? 0.10f : 0.06f;
            EditorGUI.DrawRect(rect, hover);
        }

        private static Color GetSelectedTabTextColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.74f, 0.88f, 1f, 1f)
                : new Color(0.04f, 0.24f, 0.72f, 1f);
        }

        private void DrawMinimizeLastButton(Rect rect)
        {
            GUIContent content = new GUIContent("Min", "Minimize focused editor window to tray.");
            if (GUI.Button(rect, content, _stripActionStyle))
            {
                ClearExpandedSlimTab(false);
                PungentUtilityMinimizer.MinimizeLastEditorWindowFromStrip();
                GUIUtility.ExitGUI();
            }
        }

        private void DrawTrayMenuButton(Rect rect)
        {
            GUIContent content = new GUIContent("Tray", "Open, focus, or toggle the movable minimized utilities overlay tray.");
            if (GUI.Button(rect, content, _stripActionStyle))
            {
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
                PungentUtilityMinimizer.ToggleOverlayTrayFromAccess(new Rect(screenPosition, rect.size));
                GUIUtility.ExitGUI();
            }
        }

        private void DrawMoveHandle(Rect rect)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            HandleMoveHandleEvent(controlId, rect, Event.current);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.MoveArrow);
            GUI.Label(rect, new GUIContent("|||", "Drag to move the minimized tab panel. Double-click to collapse or expand it."), _stripHandleStyle);
        }

        private void HandleMoveHandleEvent(int controlId, Rect rect, Event evt)
        {
            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        if (evt.clickCount >= 2)
                        {
                            ClearExpandedSlimTab(false);
                            ClearDragState();
                            PungentUtilityMinimizer.ToggleTabPanelCollapsed();
                            evt.Use();
                            GUIUtility.ExitGUI();
                            return;
                        }

                        GUIUtility.hotControl = controlId;
                        _handleDragging = true;
                        _handleDragMoved = false;
                        _handleDragStartScreenPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        _handleDragStartStripRect = GetCurrentStripScreenRect();
                        ClearExpandedSlimTab(false);
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && _handleDragging)
                    {
                        Vector2 currentScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        Vector2 delta = currentScreen - _handleDragStartScreenPosition;
                        if (delta.sqrMagnitude > 9f)
                            _handleDragMoved = true;
                        Rect next = new Rect(
                            _handleDragStartStripRect.x + delta.x,
                            _handleDragStartStripRect.y + delta.y,
                            _handleDragStartStripRect.width,
                            _handleDragStartStripRect.height);
                        ApplyTrayRect(PungentUtilityMinimizer.ClampStripScreenRect(next));
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId && _handleDragging)
                    {
                        Vector2 currentScreen = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                        Vector2 delta = currentScreen - _handleDragStartScreenPosition;
                        if (_handleDragMoved)
                        {
                            Rect next = new Rect(
                                _handleDragStartStripRect.x + delta.x,
                                _handleDragStartStripRect.y + delta.y,
                                _handleDragStartStripRect.width,
                                _handleDragStartStripRect.height);
                            PungentUtilityMinimizer.SetStripPlacementFromScreenRect(next, true);
                        }
                        else
                        {
                            // Click without drag keeps the tab panel stable; the Tray button owns overlay access.
                        }

                        GUIUtility.hotControl = 0;
                        _handleDragging = false;
                        _handleDragMoved = false;
                        evt.Use();
                    }
                    break;
            }
        }

        private void DrawStripContextMenu(Rect trayRect)
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown ||
                evt.button != 1 ||
                !trayRect.Contains(evt.mousePosition))
            {
                return;
            }

            PungentMinimizedUtilitiesMenu.ShowAsContext();
            evt.Use();
        }

        private void HandleStripKeyboard(Event evt)
        {
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (EditorWindow.focusedWindow != this && EditorWindow.mouseOverWindow != this)
                return;

            if (evt.keyCode == KeyCode.Escape)
            {
                if (string.IsNullOrEmpty(_expandedSlimEntryKey) && string.IsNullOrEmpty(_selectedEntryKey))
                    return;

                if (!string.IsNullOrEmpty(_expandedSlimEntryKey))
                    ClearExpandedSlimTab(true);
                else
                    _selectedEntryKey = null;

                evt.Use();
                Repaint();
                return;
            }

            PungentMinimizedUtilityEntry entry = GetKeyboardTargetEntry();
            if (entry == null)
                return;

            if (IsRestoreKey(evt))
            {
                RestoreEntryFromStrip(entry, PungentUtilityMinimizer.GetRestoreAvailability(entry));
                evt.Use();
                return;
            }

            if (IsRemoveKey(evt))
            {
                CloseEntryFromStrip(entry);
                evt.Use();
            }
        }

        private PungentMinimizedUtilityEntry GetKeyboardTargetEntry()
        {
            IReadOnlyList<PungentMinimizedUtilityEntry> entries = PungentUtilityMinimizer.MinimizedEntries;
            PungentMinimizedUtilityEntry entry = FindEntry(entries, _selectedEntryKey);
            if (entry != null)
                return entry;

            entry = FindEntry(entries, _expandedSlimEntryKey);
            if (entry != null)
                return entry;

            return FindEntry(entries, _hoveredEntryKey);
        }

        private static PungentMinimizedUtilityEntry FindEntry(IReadOnlyList<PungentMinimizedUtilityEntry> entries, string entryKey)
        {
            if (entries == null || string.IsNullOrEmpty(entryKey))
                return null;

            for (int i = 0; i < entries.Count; i++)
            {
                if (IsEntryKey(entries[i], entryKey))
                    return entries[i];
            }

            return null;
        }

        private static bool IsRestoreKey(Event evt)
        {
            return evt.keyCode == KeyCode.Return ||
                   evt.keyCode == KeyCode.KeypadEnter ||
                   evt.keyCode == KeyCode.Minus ||
                   evt.keyCode == KeyCode.KeypadMinus ||
                   evt.character == '-';
        }

        private static bool IsRemoveKey(Event evt)
        {
            return evt.keyCode == KeyCode.Delete ||
                   evt.keyCode == KeyCode.Backspace ||
                   char.ToLowerInvariant(evt.character) == 'x';
        }

        private static void DrawExpandedSlimTab(PungentMinimizedUtilityEntry entry, Rect sourceRect, float windowWidth, float trayTop)
        {
            string displayName = GetDisplayName(entry);
            string kind = PungentUtilityMinimizer.GetEntryKindLabel(entry);
            PungentMinimizedEntryRestoreAvailability availability = PungentUtilityMinimizer.GetRestoreAvailability(entry);
            float width = Mathf.Min(ExpandedSlimWidth, Mathf.Max(180f, windowWidth - 8f));
            float x = Mathf.Clamp(sourceRect.center.x - width * 0.5f, 4f, Mathf.Max(4f, windowWidth - width - 4f));
            Rect rect = new Rect(x, Mathf.Max(4f, trayTop - ExpandedSlimHeight + 5f), width, ExpandedSlimHeight - 10f);

            GUI.Box(rect, GUIContent.none, _expandedSlimStyle);

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, 18f);
            string rowTooltip = availability.CanRestore
                ? $"{kind}: {displayName}"
                : $"{kind}: {displayName}\nUnavailable: {availability.Reason}";
            GUI.Label(labelRect, new GUIContent(displayName, rowTooltip), EditorStyles.boldLabel);

            Rect kindRect = new Rect(rect.x + 8f, labelRect.yMax + 1f, rect.width - 16f, 16f);
            GUI.Label(kindRect, availability.CanRestore ? kind : "Unavailable", EditorStyles.miniLabel);

            Rect restoreRect = new Rect(rect.x + 8f, rect.yMax - 27f, Mathf.Max(78f, (rect.width - 24f) * 0.58f), 21f);
            Rect removeRect = new Rect(restoreRect.xMax + 8f, restoreRect.y, rect.xMax - restoreRect.xMax - 16f, 21f);

            using (new EditorGUI.DisabledScope(!availability.CanRestore))
            {
                if (GUI.Button(restoreRect, new GUIContent("Restore", availability.Tooltip), EditorStyles.miniButtonLeft))
                {
                    string id = entry.UtilityId;
                    EditorApplication.delayCall += () => PungentUtilityMinimizer.RestoreEntryFromTab(id);
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(removeRect, new GUIContent("Close", "Close this minimized entry without restoring the window."), EditorStyles.miniButtonRight))
            {
                PungentUtilityMinimizer.CloseMinimizedEntry(entry.UtilityId);
                GUIUtility.ExitGUI();
            }
        }

        private bool HandleDragRestoreEvent(PungentMinimizedUtilityEntry entry, Rect rect, Event evt, PungentMinimizedEntryRestoreAvailability availability)
        {
            if (evt.button != 0)
                return false;

            if (!availability.CanRestore)
                return false;

            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                _dragEntryKey = entry.UtilityId;
                _dragStartMousePosition = evt.mousePosition;
                _dragRestoreArmed = false;
                _liveDragWindow = null;
                _liveDragPreviousRect = entry.PreviousRect;
                return false;
            }

            if (string.IsNullOrEmpty(_dragEntryKey) ||
                !string.Equals(_dragEntryKey, entry.UtilityId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (evt.type == EventType.MouseDrag)
            {
                if (!_dragRestoreArmed &&
                    Vector2.Distance(evt.mousePosition, _dragStartMousePosition) >= DragRestoreThreshold &&
                    IsOutsideStrip(evt.mousePosition))
                {
                    Vector2 screenPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    string id = _dragEntryKey;
                    ClearExpandedSlimTab(false);
                    if (PungentUtilityMinimizer.TryRestoreAt(id, screenPosition, out _liveDragWindow))
                    {
                        _dragRestoreArmed = true;
                        MoveLiveDragWindow(screenPosition);
                    }
                    else
                    {
                        ClearDragState();
                    }
                }

                if (_dragRestoreArmed)
                {
                    MoveLiveDragWindow(GUIUtility.GUIToScreenPoint(evt.mousePosition));
                    evt.Use();
                    return true;
                }
            }

            if (evt.type == EventType.MouseUp)
            {
                if (!_dragRestoreArmed &&
                    Vector2.Distance(evt.mousePosition, _dragStartMousePosition) >= DragRestoreThreshold &&
                    IsOutsideStrip(evt.mousePosition))
                {
                    _dragRestoreArmed = true;
                }

                if (_dragRestoreArmed)
                {
                    Vector2 screenPosition = GUIUtility.GUIToScreenPoint(evt.mousePosition);
                    MoveLiveDragWindow(screenPosition);
                    ClearDragState();
                    evt.Use();
                    GUIUtility.ExitGUI();
                    return true;
                }

                ClearDragState();
            }

            return false;
        }

        private void MoveLiveDragWindow(Vector2 screenPosition)
        {
            if (_liveDragWindow == null)
                return;

            _liveDragWindow.position = PungentUtilityMinimizer.GetRestoreRectNear(_liveDragPreviousRect, screenPosition);
            _liveDragWindow.Focus();
        }

        private bool IsOutsideStrip(Vector2 mousePosition)
        {
            Rect stripRect = new Rect(
                0f,
                position.height - PungentUtilityMinimizer.GetTrayHeight(),
                position.width,
                PungentUtilityMinimizer.GetTrayHeight());

            return !stripRect.Contains(mousePosition);
        }

        private void ApplyTrayRect(Rect trayRect)
        {
            bool expanded = !string.IsNullOrEmpty(_expandedSlimEntryKey);
            Rect rect = expanded
                ? new Rect(trayRect.x, trayRect.y - ExpandedSlimHeight, trayRect.width, trayRect.height + ExpandedSlimHeight)
                : trayRect;

            position = rect;
            minSize = new Vector2(rect.width, rect.height);
            maxSize = new Vector2(rect.width, rect.height);
        }

        private void OnEntriesChanged()
        {
            if (!string.IsNullOrEmpty(_expandedSlimEntryKey) && !EntryExists(PungentUtilityMinimizer.MinimizedEntries, _expandedSlimEntryKey))
                ClearExpandedSlimTab(true);

            if (!string.IsNullOrEmpty(_selectedEntryKey) && !EntryExists(PungentUtilityMinimizer.MinimizedEntries, _selectedEntryKey))
                _selectedEntryKey = null;

            Repaint();
        }

        private void ClearExpandedSlimTab(bool syncTray)
        {
            if (string.IsNullOrEmpty(_expandedSlimEntryKey))
                return;

            _expandedSlimEntryKey = null;
            _expandedSlimSourceRect = default;

            if (syncTray)
                EditorApplication.delayCall += PungentUtilityMinimizer.ShowBottomStripIfNeeded;
        }

        private void ClearDragState()
        {
            _dragEntryKey = null;
            _dragStartMousePosition = default;
            _dragRestoreArmed = false;
            _liveDragWindow = null;
            _liveDragPreviousRect = default;
        }

        private Rect GetCurrentStripScreenRect()
        {
            return new Rect(
                position.x,
                position.y + position.height - PungentUtilityMinimizer.GetTrayHeight(),
                position.width,
                PungentUtilityMinimizer.GetTrayHeight());
        }

        private static bool EntryExists(IReadOnlyList<PungentMinimizedUtilityEntry> entries, string entryKey)
        {
            if (entries == null || string.IsNullOrEmpty(entryKey))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (IsEntryKey(entries[i], entryKey))
                    return true;
            }

            return false;
        }

        private static bool IsEntryKey(PungentMinimizedUtilityEntry entry, string entryKey)
        {
            return entry != null &&
                   !string.IsNullOrEmpty(entryKey) &&
                   string.Equals(entry.UtilityId, entryKey, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDisplayName(PungentMinimizedUtilityEntry entry)
        {
            if (entry == null)
                return "Utility";

            return string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.UtilityId : entry.DisplayName;
        }

        private static string GetSlimLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "?";

            for (int i = 0; i < displayName.Length; i++)
            {
                if (!char.IsWhiteSpace(displayName[i]))
                    return char.ToUpperInvariant(displayName[i]).ToString();
            }

            return "?";
        }

        private static void ShowContextMenu(PungentMinimizedUtilityEntry entry)
        {
            PungentMinimizedUtilitiesMenu.ShowEntryContext(entry);
        }

        private static void EnsureStyles()
        {
            if (_tabStyle != null)
                return;

            _tabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(9, 22, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Normal
            };

            _slimTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(0, 0, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold
            };

            _tabLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(9, 22, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Normal
            };

            _slimTabLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(0, 0, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Bold
            };

            _tabActionStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 1),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 9,
                fontStyle = FontStyle.Bold
            };

            _expandedSlimStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _stripActionStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(2, 2, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };

            _stripHandleStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                fixedHeight = 0f,
                padding = new RectOffset(0, 0, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
        }
    }
#endif
}
