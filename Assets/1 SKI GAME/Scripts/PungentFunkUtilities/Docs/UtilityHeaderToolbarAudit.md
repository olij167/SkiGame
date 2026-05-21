# Utility Header Toolbar Audit

Core-first migration completed in this pass:

- Utilities Browser: shared compact toolbar with contextual help, tray, minimize, and status.
- Help Browser: shared compact toolbar with help, tray, minimize, and status.
- Developer Tools: shared compact toolbar in both gated and enabled states.
- Design Validation Audit: shared compact toolbar with status.
- Appearance Lab / Theme Customizer: shared compact toolbar with active theme status.
- Documentation Links: shared compact toolbar with backlog/status text.
- Palette Designer: shared compact toolbar with palette status.
- Debug Control Center: existing compact strip replaced with the shared toolbar.

Deferred migration groups:

- Authoring/data/document windows: Data Sheets, Rich Documents, Board/Node Graphs, Environment Simulation, Scene Gizmo Browser.
- Asset/scene/production windows: Asset Placement Lab, Prefab Exporter, Procedural Texture Lab, Texture Array Baker, Audio Coverage, Terrain Usage Scanner, Scene Issue Scanner, Bulk Rename, Font Preview, Input Prompt Icon Library.
- Remaining specialty windows should be migrated after Unity validation of the Core-first toolbar behavior to avoid broad duplicate-header regressions.

Required verification for each migrated window:

- Toolbar appears once.
- Title remains visible at narrow width.
- Status truncates or compresses without clipping controls.
- `[?]` opens contextual help or the fallback Help Browser route.
- Minimized utilities tray access is visible.
- Minimize button still works.
