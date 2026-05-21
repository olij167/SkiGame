namespace PungentFunk.Utilities.Editor.Core.Help
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;

    internal static class PungentUtilityHelpSeedData
    {
        public static List<PungentUtilityHelpTopic> CreateTopics()
        {
            string now = DateTime.UtcNow.ToString("o");
            return new List<PungentUtilityHelpTopic>
            {
                Topic("overview", "Debug Control Center Overview",
                    "A scene-facing debug workspace for finding debug toggles, context actions, static flags, scheduled diagnostics, and DebugRouter state.",
                    "Press Refresh to build the scene cache, then use Search and View filters to narrow the component list. Auto Refresh only refreshes after hierarchy changes and remains throttled. Broad controls affect many objects, so review the filtered count before applying them.",
                    now,
                    Features(
                        Feature("refresh", "Refresh / Auto Refresh", "Refresh rebuilds the cached scene debug view on demand. Auto Refresh responds to hierarchy changes on a throttle.", "Top command strip", "Refresh is safe; Auto Refresh can be noisy in very large scenes."),
                        Feature("search", "Search and filters", "Filters components, paths, fields, context actions, router rows, static toggles, and scheduled actions.", "Search strip", "Filtering changes what broad controls can target when filtered mode is enabled.")),
                    Troubleshooting(
                        Trouble("empty-scan", "No components appear after opening.", "The scene cache has not been refreshed yet or the filters are too narrow.", "Press Refresh, clear Search, or disable Show Only Debuggable.")),
                    new[] { "debug-control/components", "debug-control/router", "debug-control/scripting-index" }),

                Topic("components", "Discovered Components",
                    "The component browser groups scanned scene objects and exposes reflected debug bools, ContextMenu actions, and snapshots.",
                    "Start by searching for the object, component type, field, or action you need. Expand a component row to inspect bool toggles and action buttons. Snapshot tools are read-only.",
                    now,
                    Features(
                        Feature("bools", "Bool toggles", "Shows reflected debug/log/gizmo bools on scanned components.", "Component rows", "Changing toggles mutates scene component state."),
                        Feature("actions", "Context actions", "Runs discovered ContextMenu-style debug actions on individual components.", "Expanded component rows", "Actions may mutate scene state depending on the component implementation."),
                        Feature("snapshots", "Snapshots", "Copies plain-text component snapshots for debugging handoff.", "Expanded component rows", "Read-only.")),
                    Troubleshooting(
                        Trouble("missing-component", "A component is missing from the list.", "It may be inactive, filtered out, or not expose debuggable fields/actions.", "Enable Include Inactive, clear Search, or disable Show Only Debuggable.")),
                    new[] { "debug-control/component-chips", "debug-control/bulk-controls" }),

                Topic("component-chips", "Component Chips",
                    "Chips summarize component capabilities and keep dense debug rows scannable without relying on colour alone.",
                    "Use chips to spot whether a component has bools, actions, snapshots, router data, or static support before expanding it.",
                    now,
                    Features(
                        Feature("capability-chips", "Capability chips", "Compact labels identify available bools, actions, snapshots, router links, and static controls.", "Component list rows", "Chips are indicators; buttons and toggles remain the action surfaces."),
                        Feature("filter-context", "Filtered operations", "Some broad controls can operate on the currently filtered component set.", "Bulk Controls", "Confirm the visible count before applying broad changes.")),
                    Troubleshooting(),
                    new[] { "debug-control/components", "debug-control/bulk-controls" }),

                Topic("router", "DebugRouter",
                    "Router panels inspect channel, source, state, and signal snapshots reported through the runtime DebugRouter.",
                    "Use Router when debug output should be controlled centrally by channel, source, or signal instead of by scattered inspector bools.",
                    now,
                    Features(
                        Feature("channels", "Channels", "Enable or inspect named logging channels and their recent activity.", "Router panel", "Enabling channels can increase console volume."),
                        Feature("signals", "Signals", "Inspect named signal fire counts and optional console forwarding.", "Router panel", "Console forwarding should be used intentionally."),
                        Feature("states", "Runtime states", "View registered runtime state snapshots.", "Router panel", "Read-only from the Help Browser; state mutation happens through DebugRouter callers.")),
                    Troubleshooting(
                        Trouble("router-empty", "Router rows are empty.", "No runtime code has registered channels, states, or signals in the current editor session.", "Enter Play Mode or call DebugRouter methods from runtime/debug scripts.")),
                    new[] { "debug-control/scripting-index" }),

                Topic("scheduler", "Scheduler",
                    "The scheduler repeats selected debug actions while the editor is open, useful for periodic diagnostics.",
                    "Enable only the actions you need and keep intervals modest. Scheduled calls are editor tooling, not gameplay automation.",
                    now,
                    Features(
                        Feature("scheduled-calls", "Scheduled calls", "Runs selected calls at configured intervals while the Debug Control Center is active.", "Scheduler panel", "Broad or mutating actions can repeatedly change scene state."),
                        Feature("cleanup", "Cleanup", "Removes broken or stale scheduled targets.", "Scheduler tools", "Review targets before cleanup.")),
                    Troubleshooting(
                        Trouble("schedule-not-running", "A scheduled action is not running.", "The target is missing, disabled, or the interval has not elapsed.", "Check the Scheduler panel and resolve missing targets.")),
                    new[] { "debug-control/overview" }),

                Topic("static-toggles", "Static Toggles",
                    "Static toggle controls expose package-owned static debug/log/gizmo bools discovered by the scan.",
                    "Use static toggles for global debug switches. Prefer narrow component toggles when you only need one object or component type.",
                    now,
                    Features(
                        Feature("static-flags", "Static flags", "Controls static debug/log/gizmo bools without selecting source files.", "Static tray/panel", "Static values can affect all matching runtime/editor code until changed again."),
                        Feature("tray", "Static tray", "Keeps broad static controls reachable without overloading the main component list.", "Toolbar static button", "Use Search to reduce noise before applying global changes.")),
                    Troubleshooting(),
                    new[] { "debug-control/bulk-controls" }),

                Topic("bulk-controls", "Bulk Controls",
                    "Bulk controls apply category-level debug bool changes across the scanned or filtered component set.",
                    "Turn on filtered mode when you only want the visible search result set. Read the visible count before applying broad changes.",
                    now,
                    Features(
                        Feature("filtered-bulk", "Filtered bulk operations", "Applies broad bool changes to the currently filtered component list when enabled.", "Bulk Controls", "Broad and potentially disruptive."),
                        Feature("category-bulk", "Category toggles", "Targets debug/log/gizmo categories rather than individual fields.", "Bulk Controls", "Can affect many components at once.")),
                    Troubleshooting(
                        Trouble("too-many-targets", "A bulk action touches more components than expected.", "The filter is broad or filtered mode is off.", "Undo if possible, narrow Search, and confirm the count before retrying.")),
                    new[] { "debug-control/components", "debug-control/static-toggles" }),

                Topic("scripting-index", "Scripting Index",
                    "Generated scripting entries document package-owned public API surfaces that are useful from debug scripts.",
                    "In Developer Mode, press Refresh Generated Index to rebuild cached scripting entries. Curated help text and manual overrides are preserved over generated descriptions.",
                    now,
                    Features(
                        Feature("explicit-refresh", "Explicit refresh", "Reflection scanning only runs when Refresh Generated Index is pressed.", "Help Browser developer tools", "No reflection indexing runs during repaint."),
                        Feature("curation", "Manual curation", "Generated entries can be edited, marked shippable, hidden, or reset in Developer Mode.", "Scripting Index tab", "Manual overrides are project-local.")),
                    Troubleshooting(
                        Trouble("no-scripting", "No scripting entries appear.", "The generated index has not been built or generated entries are hidden by filters.", "Enable Developer Mode and press Refresh Generated Index.")),
                    new[] { "debug-control/router" }),

                Topic("search-filter", "Search and Filter Strip",
                    "Search, scope, visibility, view toggles, refresh, and broad controls shape what the Debug Control Center shows and what bulk actions can affect.",
                    "Start with Refresh, then narrow Search before enabling filtered bulk controls. Auto Refresh responds to hierarchy changes on a throttle and should be used intentionally in large scenes.",
                    now,
                    Features(
                        Feature("scope", "Scope and visibility", "Include inactive objects, selected hierarchy scope, and debuggable-only filters change the cached browser set.", "Search/filter strip", "Filters can change the target set for broad actions."),
                        Feature("refresh-actions", "Refresh controls", "Refresh rebuilds cached scene data; Auto Refresh waits for hierarchy changes and uses a throttle.", "Search/filter strip", "Explicit Refresh is safest before broad edits.")),
                    Troubleshooting(),
                    new[] { "debug-control/overview", "debug-control/bulk-controls" }),

                HelpTopic("environment-simulation", "overview", "Environment Simulation Overview",
                    "A consolidated scene control panel for PungentFunk calendar clocks, weather state, forecasts, environmental outputs, and delta-time channels.",
                    "Use the hub to check system health, create missing scene controllers, run quick preview actions, and open the focused Calendar Clock, Weather, or Delta Time utility windows for configuration.",
                    now,
                    Features(
                        Feature("system-health", "System Health", "Summarizes missing controllers, profiles, output appliers, forecasts, and delta-time profile setup.", "Environment Simulation hub", "Health cards are advisory; optional outputs can remain unassigned."),
                        Feature("quick-preview", "Quick Preview", "Provides safe scene-level actions for hour changes, day advancement, forecast regeneration, pause, slow motion, and restore.", "Environment Simulation hub", "Preview actions mutate scene component state and support Undo where applicable."),
                        Feature("focused-tools", "Focused Tools", "Calendar Clock, Weather, and Delta Time each have their own configuration window.", "Open buttons and menu entries", "Use focused tools for profile editing and advanced setup.")),
                    new[] { "calendar-clock/overview", "weather-utility/overview", "delta-time-controller/overview" }),

                HelpTopic("calendar-clock", "overview", "Calendar Clock",
                    "Runtime calendar/time progression with configurable months, weekdays, seasons, sky output, and date/time sequence triggers.",
                    "Create or assign Calendar and Sky profiles, edit start date/time, configure sun/moon outputs, then preview hours and day changes from the focused Calendar Clock utility.",
                    now,
                    Features(
                        Feature("calendar-profile", "Calendar Profile", "Defines month names, day counts, season names, sunrise/sunset, and seasonal sun tilt.", "Calendar Clock utility and Calendar Profile asset", "Keep month day counts above zero."),
                        Feature("sequence-rules", "Time Sequence Rules", "Trigger UnityEvents from reusable date, month, weekday, season, year, hour, and interval rules.", "PungentTimeSequenceListener", "Fast-forward behaviour should be tested against event expectations.")),
                    new[] { "environment-simulation/overview" }),

                HelpTopic("weather-utility", "overview", "Weather Utility",
                    "Runtime weather controller with simple-to-advanced presets, hourly forecasts, interpolation, and optional environmental output adapters.",
                    "Create or assign a Weather Profile, edit season rules and presets, assign optional output adapters, then regenerate forecasts or preview presets from the Weather utility.",
                    now,
                    Features(
                        Feature("simple-authoring", "Simple Authoring", "Cloud amount, density, softness, intensity, fog, gloom, wind, tint, wetness, and snowiness resolve into richer output state.", "Weather Utility profile authoring", "Use advanced overrides only when shader-facing values need direct control."),
                        Feature("outputs", "Output Applier", "Optionally writes RenderSettings fog, shader globals, cloud renderer property blocks, precipitation particles, and ambient audio.", "PungentWeatherOutputApplier", "Missing references are ignored safely.")),
                    new[] { "environment-simulation/overview" }),

                HelpTopic("delta-time-controller", "overview", "Delta Time Controller",
                    "Centralized pause, slow-motion, speed-up, and opt-in channel scale control for gameplay and simulation systems.",
                    "Create or assign a Delta Time Profile, inspect key channel scales, push/pop pause requests, and edit channel definitions from the Delta Time utility.",
                    now,
                    Features(
                        Feature("pause-stack", "Pause Stack", "Multiple systems can push pauses and must pop them independently, preventing early unpause.", "PungentDeltaTimeController", "Clear pause intentionally when recovering from interrupted flows."),
                        Feature("channels", "Channels", "Global, Gameplay, UI, Physics, Animation, Audio, Particles, Clock, Weather, and Custom channels can blend separately.", "Delta Time Profile", "Only channels marked for Unity time scale write to Time.timeScale.")),
                    new[] { "environment-simulation/overview" }),

                HelpTopic("help-browser", "overview", "Help Browser Overview",
                    "The Core documentation browser for utility guides, generated references, troubleshooting, source badges, related notes, and contextual help destinations.",
                    "Use the left directory to choose a category, utility, or topic. Search narrows the tree while preserving context. Developer Mode adds generation, curation, and visibility tools.",
                    now,
                    Features(
                        Feature("directory", "Directory navigation", "Category, module, utility, and topic pages are selectable before choosing an exact topic.", "Left documentation sidebar", "Normal browsing is read-only."),
                        Feature("topic-tabs", "Topic tabs", "Quick Use, Feature Index, Scripting Index, Troubleshooting, and Related keep dense help scannable.", "Topic content panel", "Generated entries are labelled for review.")),
                    new[] { "help-browser/navigation", "help-browser/generation-dashboard" }),

                HelpTopic("help-browser", "navigation", "Directory Navigation",
                    "Directory navigation groups help by category, module, utility, and topic so users can browse before knowing an exact topic name.",
                    "Click parent rows for directory pages. Expand rows when you need child topics. Search filters results without removing the surrounding context.",
                    now,
                    Features(Feature("directory-pages", "Directory pages", "Parent selections open useful overview pages with links and empty states.", "Left sidebar and main panel", "No scans run while browsing.")),
                    new[] { "help-browser/breadcrumbs" }),

                HelpTopic("help-browser", "breadcrumbs", "Breadcrumbs",
                    "Breadcrumbs show the active help path and let users jump back to category, module, utility, or topic pages.",
                    "Use breadcrumbs above the content panel to move up the documentation hierarchy. Narrow layouts truncate labels but keep useful segments clickable.",
                    now,
                    Features(Feature("compact-path", "Compact path", "Narrow windows show Directory Home plus the final path segments to avoid horizontal scrolling.", "Content header", "Truncated labels keep full-path tooltips.")),
                    new[] { "help-browser/navigation" }),

                HelpTopic("help-browser", "annotations", "Notes, Bookmarks, and Markers",
                    "Help annotations let users bookmark topics and attach optional Notes bridge notes without editing official help metadata.",
                    "Use Show Notes and Show Bookmarks to control marker visibility. Bookmark actions work through Core storage; note actions enable when the optional Notes bridge is available.",
                    now,
                    Features(
                        Feature("bookmarks", "Local bookmarks", "Bookmarks are stored in Core help annotation storage and appear as markers.", "Topic and block toolbars", "Bookmarks do not edit official help content."),
                        Feature("notes-bridge", "Optional Notes bridge", "Note creation and opening are delegated through neutral Core bridge delegates.", "Annotation toolbar", "Help still works when Notes are absent.")),
                    new[] { "tooltip-notes/overview" }),

                HelpTopic("help-browser", "generated-scripting-index", "Generated Scripting Index",
                    "The scripting index scans package-owned public APIs only when a developer explicitly refreshes it.",
                    "Developer Mode exposes Refresh Generated Index. Generated entries can be edited, hidden, marked shippable, reset, or removed when stale.",
                    now,
                    Features(
                        Feature("explicit-refresh", "Explicit refresh", "Reflection and AssetDatabase lookups run only from the refresh action.", "Developer generation tools", "No reflection scan runs during repaint."),
                        Feature("stale-detection", "Stale detection", "Generated entries missing from the next source refresh are marked stale for review/removal.", "Generation Dashboard", "Manual overrides remain separate.")),
                    new[] { "help-browser/generation-dashboard" }),

                HelpTopic("help-browser", "tooltip-index", "Tooltip Index",
                    "The tooltip index scans existing editor GUIContent tooltips and likely untooltiped controls, then surfaces them as generated Controls & Tooltips draft topics.",
                    "Run Refresh Tooltip Index in Developer Mode, then review generated feature entries, weak-tooltip candidates, and false positives before promoting useful descriptions.",
                    now,
                    Features(
                        Feature("tooltip-scan", "Tooltip scan", "Source parsing is explicit and cached in generated help topics.", "Generation Dashboard", "The scan does not modify source files."),
                        Feature("tooltip-review", "Tooltip review", "Generated entries carry source confidence and can be marked shippable, hidden, ignored, or needing better wording.", "Feature Index developer controls", "Generated entries remain draft until curated.")),
                    new[] { "help-browser/generation-dashboard" }),

                HelpTopic("help-browser", "generation-dashboard", "Generation Dashboard",
                    "Developer-only dashboard for generating registry overview drafts, contextual topic stubs, tooltip indexes, scripting references, and Help Coverage reports.",
                    "Use generation actions to create reviewable drafts. Review coverage rows, weak tooltip candidates, hidden entries, and stale APIs; curated seed data and manual overrides always win over generated content.",
                    now,
                    Features(
                        Feature("registry-overviews", "Registry overview generation", "Creates generated overview drafts from registered utility descriptors.", "Generation Dashboard", "Generated drafts are developer-only."),
                        Feature("context-stubs", "Contextual topic stubs", "Indexes HelpButton usage and creates missing topic drafts.", "Generation Dashboard", "Missing topics open safely before stubs exist."),
                        Feature("coverage-report", "Coverage report", "Summarises registered utilities, overview/header/section help, tooltip coverage, scripting entries, generated review debt, and hidden/developer-only entries.", "Generation Dashboard", "Report generation reads cached help/registry data.")),
                    new[] { "help-browser/tooltip-index", "help-browser/generated-scripting-index", "help-browser/coverage-report" }),

                HelpTopic("help-browser", "coverage-report", "Help Coverage Report",
                    "The Help Coverage report classifies utilities, separates actionable gaps from acceptable command-style utilities, and surfaces generated-help review queues using cached data.",
                    "Open Developer Mode in the Help Browser and show the Generation Dashboard. Use coverage filters, review queues, export modes, and row actions to promote, hide, ignore, or mark generated coverage decisions without broad repaint scans.",
                    now,
                    Features(
                        Feature("cached-coverage", "Cached coverage", "The report reads registered utilities, cached help topics, cached contextual help contexts, generated index data, and persisted coverage decisions.", "Generation Dashboard", "It does not scan project files while drawing."),
                        Feature("classified-gaps", "Classified gaps", "Window and popup utilities can require header/section help, while command-style or provider-only utilities can be accepted as overview-only or not applicable.", "Help Coverage panel", "Do not add noisy help buttons just to silence a warning."),
                        Feature("review-queues", "Review queues", "Generated draft topics, tooltip entries, weak tooltips, scripting entries, missing headers/sections, docs issues, hidden/ignored rows, and complete coverage rows are reviewable in Developer Mode.", "Generated Review Queues", "Bulk actions require explicit selection and confirmation.")),
                    new[] { "help-browser/generation-dashboard" }),

                HelpTopic("help-browser", "developer-curation", "Developer Curation",
                    "Developer curation tools let package authors edit generated descriptions/examples, hide draft entries, mark entries shippable, and reset generated content.",
                    "Enable Developer Mode to review generated help without exposing metadata editing to normal users.",
                    now,
                    Features(Feature("manual-overrides", "Manual overrides", "Project-local manual topics and entries override generated descriptions.", "Topic developer actions", "Generated docs never overwrite manual or curated content.")),
                    new[] { "help-browser/source-badges" }),

                HelpTopic("help-browser", "source-badges", "Source Badges",
                    "Source badges identify curated, generated, manual override, developer-only, hidden, draft, stale, or missing help content.",
                    "Use badges to decide what can ship and what needs review. Do not rely on colour alone; each badge includes text.",
                    now,
                    Features(Feature("badge-text", "Text labels", "Badges use explicit words such as Generated, Draft, Hidden, or Stale.", "Topic and entry headers", "Review generated/draft content before shipping.")),
                    new[] { "help-browser/developer-curation" }),

                HelpTopic("help-browser", "quick-help-tray", "Quick Help Tray",
                    "Quick Help is the compact contextual tray opened by normal left-clicks on shared [?] buttons.",
                    "Use the tray for quick summaries, local topic navigation, Previous/Next movement, and Copy Link. Open Full Help Browser jumps to the same utility, section, and topic when you need the complete documentation surface.",
                    now,
                    Features(
                        Feature("topic-glossary", "Topic glossary", "The tray lists overview first, local utility topics, same-section topics, related topics, and generated Controls & Tooltips when available.", "Quick Help Topics view", "The list uses cached help registry data only."),
                        Feature("missing-topic", "Missing topic state", "Unknown destinations show a calm missing state and Developer Mode can create a topic stub.", "Quick Help body", "Missing topics never throw from help buttons.")),
                    new[] { "help-browser/contextual-help-buttons", "help-browser/overview" }),

                HelpTopic("help-browser", "contextual-help-buttons", "Contextual Help Buttons",
                    "Shared contextual help buttons preserve utility, section, and topic IDs while opening Quick Help or the full Help Browser.",
                    "Left-click opens the Quick Help tray. Alt/Ctrl-click opens the full Help Browser directly. Right-click exposes Quick Help, full Help Browser, copy, stub, edit, existing generation actions, and Developer Mode issue reporting.",
                    now,
                    Features(
                        Feature("shared-button", "Shared [?] button", "All integrations use PungentUtilityHelpButton instead of lab-specific helpers.", "Utility headers and section headers", "Buttons open missing-topic states gracefully."),
                        Feature("full-browser-link", "Full browser fallback", "The tray and context menu keep the full Help Browser one click away for complete tabs, related docs, annotations, and Developer Mode tools.", "Quick Help footer and context menu", "Quick Help is a lightweight entry point, not a replacement for the Help Browser.")),
                    new[] { "help-browser/quick-help-tray", "help-browser/generation-dashboard", "help-browser/bug-reporting" }),

                HelpTopic("help-browser", "bug-reporting", "Bug Report Relay",
                    "The bug report overlay captures Help Browser, Quick Help, generated-entry, and documentation-link context, then sends user-approved reports to the configured Wix relay.",
                    "Use Report Issue from a Help Browser topic, missing-topic page, review queue row, Quick Help tray, Utilities Browser welcome page, or help-button context menu. Send the report, copy the payload, save a draft, or queue it locally. Discord delivery is handled by the Wix backend so the Unity package never stores webhook secrets.",
                    now,
                    Features(
                        Feature("user-facing", "User-facing report form", "Normal users can submit reports, while Developer Mode reveals source and backend settings.", "Help Browser, Quick Help, and Utilities Browser actions", "Do not expose source paths or generated-entry internals outside Developer Mode."),
                        Feature("local-queue", "Local drafts and queue", "Payloads can be copied, saved as drafts, or queued locally if relay delivery fails.", "Bug Report overlay footer", "Queued reports remain local until resent or copied manually."),
                        Feature("diagnostic-consent", "Diagnostic consent", "Safe diagnostics and console summary are opt-in fields in the overlay.", "Bug Report overlay consent section", "Do not collect project-sensitive data by default.")),
                    new[] { "help-browser/bug-report-backend-contract", "help-browser/contextual-help-buttons" }),

                HelpTopic("help-browser", "bug-report-backend-contract", "Bug Report Backend Contract",
                    "The Wix bug report endpoint receives editor payloads at pungentfunk.net, stores them server-side, and forwards notifications to Discord.",
                    "The Unity package posts JSON payloads to the Wix relay and queues failed submissions locally. The backend validates schemas, rate-limits, stores reports, sends Discord notifications, and owns all private credentials. Never embed SMTP passwords, Discord webhooks, GitHub tokens, or private API keys in editor scripts.",
                    now,
                    Features(
                        Feature("wix-endpoint", "Wix endpoint", "The relay points toward https://www.pungentfunk.net/_functions/bugReport as a configurable intake endpoint.", "Relay settings", "Use /_functions-dev/bugReport while testing Wix before publishing."),
                        Feature("secret-boundary", "Server-owned secrets", "Email, Discord, and tracker credentials belong only on the server.", "Backend contract notes", "Unity editor scripts must ship without private secrets."),
                        Feature("schema-validation", "Schema validation", "The backend should validate schemaVersion, context, contact fields, diagnostics consent, and payload sizes.", "Backend contract notes", "Reject malformed or oversized reports before forwarding.")),
                    new[] { "help-browser/bug-reporting" }),

                HelpTopic("utilities-browser", "overview", "Utilities Browser Overview",
                    "The Utilities Browser is the package control panel for finding, filtering, opening, and reviewing registered utilities.",
                    "Search by name, tag, category, package metadata, docs, or actions. Use Filters, Favourites, History, and Settings to narrow the launcher without changing utility registrations.",
                    now,
                    Features(Feature("launcher", "Utility launcher", "Utility cards and list rows open installed utilities through the Core registry, while package-gated cards route to Info or Install.", "Main browser pane", "Opening a utility does not mutate assets or scenes.")),
                    new[] { "help-browser/overview" }),

                HelpTopic("utilities-browser", "filters-search", "Utilities Browser Filters and Search",
                    "Search and secondary filters narrow registered utility cards without changing registry metadata.",
                    "Use Search for names, tags, categories, modules, menu paths, package IDs, package names, docs, and descriptions. Toggle filter panels when you need package state, favourites, history, or display settings.",
                    now,
                    Features(Feature("filters", "Filter panels", "Filters, Favourites, History, Settings, and package-state controls are optional browser aids.", "Toolbar", "Filtering does not hide registered utilities permanently.")),
                    new[] { "utilities-browser/overview" }),

                HelpTopic("documentation-links", "overview", "Documentation Links Overview",
                    "Documentation Links assigns external or project documentation references to registered utilities and keeps backlog versions visible.",
                    "Use Documentation Links to connect guides, PDFs, markdown files, Unity assets, local files, or web references to one or more utilities. Links are metadata; they do not edit the original documentation file.",
                    now,
                    Features(
                        Feature("metadata-links", "Documentation metadata", "Each record stores a display name, description, category, current target, assignments, and optional backlog versions.", "Documentation Links window", "Editing a record changes link metadata only."),
                        Feature("help-browser-surface", "Help Browser related docs", "Assigned and global documentation links appear in Help Browser related-document panels for matching utilities.", "Help Browser Related tab", "Related docs supplement help topics; they do not overwrite help content.")),
                    new[] { "documentation-links/linked-docs-list", "documentation-links/help-browser-related-docs", "help-browser/overview" }),

                HelpTopic("documentation-links", "linked-docs-list", "Linked Docs List",
                    "The linked docs list is the searchable index of Documentation Links records.",
                    "Search by name, description, category, version, or assigned utility. Use Add New Doc or Create Link For Utility to start a new metadata record.",
                    now,
                    Features(
                        Feature("search", "Search and filtering", "The list respects the current search and utility context without changing stored assignments.", "Left linked docs panel", "Filtering is temporary and safe."),
                        Feature("open-current", "Open current target", "Open launches the current Unity asset, local file, or web URL when the target is valid.", "Linked docs rows", "Missing or unsupported targets stay visible with a calm status.")),
                    new[] { "documentation-links/overview", "documentation-links/selected-document-details" }),

                HelpTopic("documentation-links", "selected-document-details", "Selected Document Details",
                    "The details panel edits the selected documentation link metadata and shows its current target, assignments, version tools, backlog, and note actions.",
                    "Select a link from the list, update the text metadata, then use the target/version sections to manage where the documentation points.",
                    now,
                    Features(
                        Feature("display-fields", "Display metadata", "Display name, description, category, and version label make documentation links easier to scan in Help Browser and utility panels.", "Details panel", "These fields do not modify the target document itself."),
                        Feature("save-remove", "Save and remove", "Save persists metadata changes; Remove deletes the documentation-link record after confirmation.", "Details panel footer", "Removing a link removes metadata only, not the original file or web page.")),
                    new[] { "documentation-links/current-target", "documentation-links/utility-assignments" }),

                HelpTopic("documentation-links", "current-target", "Current Target",
                    "The current target is the active Unity asset, local file path, or web URL opened from a documentation link.",
                    "Set a target on new records, then use Open, Copy, or Ping Asset where available. Bare web domains are normalised to HTTPS when valid.",
                    now,
                    Features(
                        Feature("target-status", "Target status", "Status badges show whether the current target is an asset, local file, web URL, missing file, unsupported scheme, invalid target, or empty target.", "Current Target section", "Status text accompanies colour; do not rely on colour alone."),
                        Feature("no-file-mutation", "Target-safe metadata", "Setting a target stores a reference or path in Documentation Links storage.", "Current Target section", "The original documentation file is not edited.")),
                    new[] { "documentation-links/current-version", "documentation-links/replace-current-target" }),

                HelpTopic("documentation-links", "utility-assignments", "Utility Assignments",
                    "Utility assignments decide where documentation links appear in Help Browser and utility-specific documentation surfaces.",
                    "Leave a link unassigned to make it global, or assign it to registered utilities in Developer Mode when the link belongs to specific tools.",
                    now,
                    Features(
                        Feature("global-links", "Global links", "Unassigned documentation links appear as global references where the Help Browser includes global docs.", "Utility Assignment section", "Use global links for package-wide references only."),
                        Feature("assigned-links", "Assigned links", "Assigned documentation links appear for matching utilities and in related docs for topics with the same utility ID.", "Utility Assignment section", "Assignments do not change registry metadata.")),
                    new[] { "documentation-links/help-browser-related-docs", "documentation-links/linked-docs-list" }),

                HelpTopic("documentation-links", "current-version", "Update / Add New Version",
                    "Update / Add New Version changes the current target while preserving the previous target in Backlog.",
                    "Use this path when a guide, PDF, markdown file, asset, local path, or web URL has a newer version and you want the older target retained for reference.",
                    now,
                    Features(
                        Feature("preserve-current", "Preserve previous target", "Updating archives the old current target before saving the new one.", "Update / Add New Version section", "Use Replace only when the old target should not be archived."),
                        Feature("backlog-notes", "Backlog notes", "Optional notes explain why an older target was archived.", "Update / Add New Version section", "Backlog notes are documentation metadata only.")),
                    new[] { "documentation-links/backlog", "documentation-links/replace-current-target" }),

                HelpTopic("documentation-links", "replace-current-target", "Replace Current Target",
                    "Replace Current Target changes the current target without adding the old target to Backlog.",
                    "Use Replace only for corrections or accidental target changes. Use Update / Add New Version when the previous target should remain available.",
                    now,
                    Features(
                        Feature("replace-warning", "Replace warning", "The replace section is separated and warning-tinted because it skips backlog preservation.", "Replace Current Target section", "Replacing does not delete files, but it can lose the previous target metadata unless already archived."),
                        Feature("update-instead", "Update instead", "The confirmation can route replacement input into Update / Add New Version when preservation is safer.", "Replace confirmation", "Choose Update Instead when in doubt.")),
                    new[] { "documentation-links/current-version", "documentation-links/backlog" }),

                HelpTopic("documentation-links", "backlog", "Documentation Backlog",
                    "Backlog stores previous documentation targets for a link after Update / Add New Version.",
                    "Open, copy, restore, or remove archived versions from the backlog when reviewing old references or rolling a link back.",
                    now,
                    Features(
                        Feature("restore", "Restore as current", "Restore moves an archived target back into the current slot.", "Backlog section", "Restoring changes link metadata; it does not edit the original file."),
                        Feature("remove-backlog", "Remove archived version", "Remove deletes the archived metadata entry for that old target.", "Backlog section", "This does not delete the asset, local file, or web page.")),
                    new[] { "documentation-links/current-version", "documentation-links/replace-current-target" }),

                HelpTopic("documentation-links", "notes-integration", "Documentation Notes Integration",
                    "Documentation Links can create, attach, and open related notes when the optional Notes bridge is available.",
                    "Use Create Note, Attach to Note, or Open Notes from a documentation link. If Notes integration is unavailable, documentation links still work normally.",
                    now,
                    Features(
                        Feature("optional-bridge", "Optional bridge", "Core stores delegates for note actions without depending on concrete Notes classes.", "Current Target note actions", "Missing bridge actions are hidden or calmly disabled."),
                        Feature("related-notes", "Related notes", "Notes can reference documentation link IDs so authoring context stays connected to docs.", "Notes bridge", "Do not rely on Notes being present for core Documentation Links workflows.")),
                    new[] { "documentation-links/overview", "help-browser/annotations" }),

                HelpTopic("documentation-links", "help-browser-related-docs", "Help Browser Related Documentation",
                    "The Help Browser shows explicit topic documentation links, utility-assigned links, and global documentation links in related-document areas.",
                    "Use the Related tab or a utility directory page to open docs. Developer Mode can manage or create links for the current utility.",
                    now,
                    Features(
                        Feature("dynamic-related-docs", "Dynamic related docs", "Related docs are collected from topic IDs, utility assignments, and global links with duplicate IDs removed.", "Help Browser Related tab", "Generated and curated help topics are not overwritten by documentation-link metadata."),
                        Feature("source-labels", "Source labels", "Rows label whether a link came from a related topic ID, a utility assignment, or the global link list.", "Help Browser documentation rows", "Missing or stale targets remain visible with status text.")),
                    new[] { "documentation-links/utility-assignments", "help-browser/overview" }),

                HelpTopic("qa-checklist-utility", "overview", "Checklist Utility Overview",
                    "Checklist Utility runs package, project, and imported checklist definitions with local result state, notes, guidance prompts, and JSON export.",
                    "Choose a checklist, use Pass, Partial, Fail, or Clear on each item, and add notes where follow-up is needed. Create menu actions copy JSON templates or open Data Sheet and Rich Document checklist-definition templates; Import JSON stores definitions in project checklist storage while result progress remains local.",
                    now,
                    Features(
                        Feature("project-definitions", "Project definitions", "Schema v3 checklist definitions are stored in ProjectSettings/PungentFunkUtilities/Checklists.json.", "Checklist picker and import/export actions", "Definitions are shared project data; review before committing them."),
                        Feature("local-results", "Local results and notes", "Pass, partial, fail, clear state, linked state results, and item notes remain in UtilityWindowPrefs.", "Checklist rows and result export", "Result progress is user-local unless exported deliberately."),
                        Feature("authoring-bridges", "Authoring bridges", "JSON is the canonical exchange format, while Data Sheets and Rich Documents provide template-driven checklist authoring.", "Create menu and source actions", "Optional bridges open calmly when their owning tool is available.")),
                    new[] { "data-sheet-editor/overview", "help-browser/overview", "documentation-links/overview" }),

                HelpTopic("design-validation-audit", "overview", "Design Validation Audit Overview",
                    "Design Validation Audit runs cached, explicit project/package checks and summarizes findings for review.",
                    "Run enabled audits when you need fresh findings. Refresh Results reads cached state without starting heavy scan work.",
                    now,
                    Features(Feature("scan-pipeline", "Scan pipeline", "Audit actions are explicit and report cached/running/stale status.", "Audit toolbar", "No audit scan should run during repaint.")),
                    new[] { "help-browser/generation-dashboard" }),

                HelpTopic("design-validation-audit", "scan-pipeline", "Audit Scan Pipeline",
                    "The audit scan pipeline separates explicit scan actions from cached result display.",
                    "Use Run Enabled Audits or Start Background Scan for fresh work. Use Refresh Results when you only want to reread cached provider output.",
                    now,
                    Features(Feature("cached-results", "Cached results", "Findings panels consume scan runner state and cached provider summaries.", "Audit toolbar", "Scan work is user-triggered, not repaint-driven.")),
                    new[] { "design-validation-audit/overview" }),

                HelpTopic("tooltip-notes", "overview", "Notes and Roadmap Overview",
                    "Notes & Roadmap tracks utility notes, future work, audit follow-ups, and project annotations.",
                    "Use notes for personal or project commentary linked to utilities, targets, tokens, or help topics. Help annotations use the optional bridge when available.",
                    now,
                    Features(Feature("help-annotations", "Help annotations", "Help topics can create or open related notes through the optional bridge.", "Help Browser annotation toolbar", "Core help works without Notes installed.")),
                    new[] { "help-browser/annotations" }),

                new PungentUtilityHelpTopic
                {
                    utilityId = "help-browser",
                    sectionId = "notes-authoring-roadmap",
                    topicId = "notes-authoring-roadmap",
                    title = "Notes Authoring Roadmap",
                    summary = "Developer roadmap note for future rich document, notes, and visual authoring work. This pass only captures direction; it does not implement the larger authoring system.",
                    quickUseMarkdown = "Future work should split Notes browsing and organisation from rich text editing, move metadata editing into an overlay tray, explore one document model for notes, dialogue drafts, quest writing, token-rich design docs, and consider a separate whiteboard or node graph canvas. Yarn Spinner, Twine, and Ink are useful inspiration sources, not dependencies or systems to recreate directly.",
                    featureEntries = Features(
                        Feature("rich-documents", "Rich document direction", "A stronger editor could support notes, dialogue drafts, quest writing, design docs, token-rich text, and eventual image-token insertion.", "Future Notes workspace", "Future pass only."),
                        Feature("metadata-tray", "Metadata overlay tray", "Metadata editing should move out of the primary writing surface when richer text editing arrives.", "Future Notes workspace", "Keep normal note writing uncluttered."),
                        Feature("whiteboard-graph", "Whiteboard / graph extension", "Node and whiteboard workflows likely need their own canvas or graph extension rather than being forced into the Help Browser.", "Future visual authoring canvas", "Not part of this Help Browser pass.")),
                    troubleshootingEntries = Troubleshooting(),
                    relatedUtilityIds = new List<string> { "tooltip-notes", "token-validator", "coverage-matrix" },
                    tags = new List<string> { "roadmap", "notes", "authoring", "whiteboard", "dialogue", "quest", "developer" },
                    developerOnly = true,
                    sourceOwner = "Core seed data",
                    lastUpdatedUtc = now
                }
            };
        }

        private static PungentUtilityHelpTopic Topic(string topicId, string title, string summary, string quickUse, string now, List<PungentUtilityHelpFeatureEntry> features, List<PungentUtilityHelpTroubleshootingEntry> troubleshooting, string[] related)
        {
            return new PungentUtilityHelpTopic
            {
                utilityId = "debug-control",
                sectionId = topicId,
                topicId = topicId,
                title = title,
                summary = summary,
                quickUseMarkdown = quickUse,
                featureEntries = features,
                troubleshootingEntries = troubleshooting,
                relatedTopicIds = new List<string>(related ?? Array.Empty<string>()),
                relatedUtilityIds = new List<string> { "help-browser" },
                tags = new List<string> { "debug", "debug control", "router", "scheduler", topicId },
                sourceOwner = "Core seed data",
                lastUpdatedUtc = now
            };
        }

        private static PungentUtilityHelpTopic HelpTopic(string utilityId, string topicId, string title, string summary, string quickUse, string now, List<PungentUtilityHelpFeatureEntry> features, string[] related)
        {
            return new PungentUtilityHelpTopic
            {
                utilityId = utilityId,
                sectionId = topicId,
                topicId = topicId,
                title = title,
                summary = summary,
                quickUseMarkdown = quickUse,
                featureEntries = features,
                troubleshootingEntries = Troubleshooting(),
                relatedTopicIds = new List<string>(related ?? Array.Empty<string>()),
                relatedUtilityIds = new List<string> { "help-browser" },
                tags = new List<string> { utilityId, topicId, "help", "core" },
                sourceOwner = "Core seed data",
                lastUpdatedUtc = now
            };
        }

        private static List<PungentUtilityHelpFeatureEntry> Features(params PungentUtilityHelpFeatureEntry[] entries)
        {
            return new List<PungentUtilityHelpFeatureEntry>(entries ?? Array.Empty<PungentUtilityHelpFeatureEntry>());
        }

        private static PungentUtilityHelpFeatureEntry Feature(string id, string label, string description, string location, string safety)
        {
            return new PungentUtilityHelpFeatureEntry
            {
                id = id,
                label = label,
                description = description,
                location = location,
                safetyNotes = safety
            };
        }

        private static List<PungentUtilityHelpTroubleshootingEntry> Troubleshooting(params PungentUtilityHelpTroubleshootingEntry[] entries)
        {
            return new List<PungentUtilityHelpTroubleshootingEntry>(entries ?? Array.Empty<PungentUtilityHelpTroubleshootingEntry>());
        }

        private static PungentUtilityHelpTroubleshootingEntry Trouble(string id, string symptom, string cause, string next)
        {
            return new PungentUtilityHelpTroubleshootingEntry
            {
                id = id,
                symptom = symptom,
                likelyCause = cause,
                nextStep = next
            };
        }
    }
#endif
}
