using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    public enum PungentDataSheetGridInteraction
    {
        Idle = 0,
        EditingCell = 10,
        RenamingColumn = 20,
        ResizingColumn = 30,
        DraggingColumn = 40,
        DraggingRow = 50,
        SelectingRange = 60,
        DraggingFillHandle = 70
    }

    public sealed class PungentDataSheetGridState
    {
        public Vector2 scroll;
        public string selectedRowId = string.Empty;
        public string selectedColumnId = string.Empty;
        public string selectedCellRowId = string.Empty;
        public string selectedCellColumnId = string.Empty;
        public string anchorCellRowId = string.Empty;
        public string anchorCellColumnId = string.Empty;
        public string sortColumnId = string.Empty;
        public bool sortDescending;
        public string filterText = string.Empty;
        public string dateDisplayFormat = "dd/MM/yyyy";
        public string renamingColumnId = string.Empty;
        public string resizingColumnId = string.Empty;
        public float resizingStartMouseX;
        public float resizingStartWidth;

        public PungentDataSheetGridInteraction interaction = PungentDataSheetGridInteraction.Idle;
        public string editingCellRowId = string.Empty;
        public string editingCellColumnId = string.Empty;
        public string editBuffer = string.Empty;
        public string editControlName = string.Empty;
        public bool focusEditControl;
        public string renameBuffer = string.Empty;
        public string renameOriginal = string.Empty;
        public bool focusRenameControl;
        public string pendingColumnDragId = string.Empty;
        public string pendingRowDragId = string.Empty;
        public string pendingCellDragRowId = string.Empty;
        public string pendingCellDragColumnId = string.Empty;
        public string fillSourceRowId = string.Empty;
        public string fillSourceColumnId = string.Empty;
        public string enumSuggestionRowId = string.Empty;
        public string enumSuggestionColumnId = string.Empty;
        public int pendingColumnDragIndex = -1;
        public int pendingRowDragIndex = -1;
        public Vector2 dragStartMouse;
        public Rect enumSuggestionRect;
        public bool enumSuggestionActive;
        internal bool enumSuggestionUpdatedThisFrame;
        public bool inputBlocked;
        public string draggingColumnId = string.Empty;
        public string draggingRowId = string.Empty;
        public int dragColumnFromIndex = -1;
        public int dragColumnToIndex = -1;
        public int dragRowFromIndex = -1;
        public int dragRowToIndex = -1;

        internal string cacheKey = string.Empty;
        internal readonly List<PungentDataSheetGridColumnView> cachedColumns = new List<PungentDataSheetGridColumnView>();
        internal readonly List<PungentDataSheetGridRowView> cachedRows = new List<PungentDataSheetGridRowView>();
        internal readonly Dictionary<string, PungentDataSheetCell> cachedCells = new Dictionary<string, PungentDataSheetCell>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<string> enumSuggestions = new List<string>();

        public bool HasCellSelection => !string.IsNullOrWhiteSpace(selectedCellRowId) && !string.IsNullOrWhiteSpace(selectedCellColumnId);
        public bool IsEditingCell => interaction == PungentDataSheetGridInteraction.EditingCell &&
                                     !string.IsNullOrWhiteSpace(editingCellRowId) &&
                                     !string.IsNullOrWhiteSpace(editingCellColumnId);

        public void InvalidateCache()
        {
            cacheKey = string.Empty;
            cachedColumns.Clear();
            cachedRows.Clear();
            cachedCells.Clear();
            enumSuggestionActive = false;
            enumSuggestionUpdatedThisFrame = false;
            enumSuggestionRowId = string.Empty;
            enumSuggestionColumnId = string.Empty;
            enumSuggestionRect = Rect.zero;
            enumSuggestions.Clear();
        }
    }

    public struct PungentDataSheetCellAddress
    {
        public string rowId;
        public string columnId;

        public PungentDataSheetCellAddress(string rowId, string columnId)
        {
            this.rowId = PungentAuthoringId.Normalize(rowId);
            this.columnId = PungentAuthoringId.Normalize(columnId);
        }
    }

    internal sealed class PungentDataSheetGridColumnView
    {
        public PungentDataSheetColumn column;
        public int modelIndex;
        public int visibleIndex;
        public bool isVirtual;

        public string Id => isVirtual ? VirtualColumnId(modelIndex) : column != null ? column.id : string.Empty;
        public string DisplayName => isVirtual ? string.Empty : column != null ? column.displayName ?? string.Empty : string.Empty;
        public PungentDataSheetDataType DataType => column != null ? column.dataType : PungentDataSheetDataType.Text;
        public float Width => Mathf.Max(PungentDataSheetGridGUI.MinColumnWidth, column != null ? column.width : PungentDataSheetGridGUI.DefaultColumnWidth);
        public bool Locked => column != null && column.locked;
        public List<string> EnumOptions => column != null && column.enumOptions != null ? column.enumOptions : EmptyOptions;

        private static readonly List<string> EmptyOptions = new List<string>();

        public static string VirtualColumnId(int modelIndex)
        {
            return "__pungent_virtual_column_" + modelIndex.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class PungentDataSheetGridRowView
    {
        public PungentDataSheetRow row;
        public int modelIndex;
        public int visibleIndex;
        public bool isVirtual;

        public string Id => isVirtual ? VirtualRowId(modelIndex) : row != null ? row.id : string.Empty;
        public string DisplayName => isVirtual ? string.Empty : row != null ? row.displayName ?? string.Empty : string.Empty;
        public bool Locked => row != null && row.locked;

        public static string VirtualRowId(int modelIndex)
        {
            return "__pungent_virtual_row_" + modelIndex.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static class PungentDataSheetGridGUI
    {
        internal const float MinColumnWidth = 82f;
        internal const float DefaultColumnWidth = 140f;
        private const int DefaultVirtualColumns = 26;
        private const int DefaultVirtualRows = 100;
        private const int TrailingVirtualColumns = 5;
        private const int TrailingVirtualRows = 10;
        private const float RowHeaderWidth = 52f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 24f;
        private const float ResizeHandleWidth = 10f;
        private const float SortButtonWidth = 20f;
        private const float TypeBadgeWidth = 46f;
        private const float DragThreshold = 4f;
        private static readonly Color HeaderTint = new Color(0.18f, 0.18f, 0.18f, 0.95f);
        private static readonly Color VirtualHeaderTint = new Color(0.13f, 0.13f, 0.13f, 0.95f);
        private static readonly Color CellBorder = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color TextTypeTint = new Color(0.45f, 0.45f, 0.45f, 0.72f);
        private static readonly Color SelectedTint = new Color(0.20f, 0.43f, 0.92f, 0.22f);
        private static readonly Color ActiveBorder = new Color(0.14f, 0.38f, 0.9f, 1f);
        private static readonly Color ChangedTint = new Color(1f, 0.74f, 0.18f, 0.12f);
        private static readonly Color VirtualCellTint = new Color(1f, 1f, 1f, 0.018f);
        private static readonly Color InvalidTint = new Color(1f, 0.25f, 0.18f, 0.18f);
        private static readonly Color MissingReferenceTint = new Color(0.9f, 0.24f, 0.18f, 0.78f);
        private static readonly Color LinkedTint = new Color(0.08f, 0.55f, 0.62f, 0.16f);
        private static readonly Color NumberTypeTint = new Color(0.2f, 0.65f, 0.34f, 0.75f);
        private static readonly Color BooleanTypeTint = new Color(0.18f, 0.46f, 0.9f, 0.75f);
        private static readonly Color DateTypeTint = new Color(0.9f, 0.58f, 0.18f, 0.75f);
        private static readonly Color EnumTypeTint = new Color(0.52f, 0.34f, 0.82f, 0.75f);
        private static readonly Color ReferenceTypeTint = new Color(0.08f, 0.62f, 0.62f, 0.75f);
        private static GUIStyle _cellLabelStyle;
        private static GUIStyle _cellPlaceholderStyle;
        private static GUIStyle _cellEditStyle;
        private static GUIStyle _sortButtonStyle;
        private static GUIStyle _typeBadgeStyle;

        public static void Draw(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            if (state == null)
                return;

            if (sheet == null)
            {
                EditorGUILayout.HelpBox("No data sheet selected.", MessageType.Info);
                return;
            }

            sheet.columns = sheet.columns ?? new List<PungentDataSheetColumn>();
            sheet.rows = sheet.rows ?? new List<PungentDataSheetRow>();
            sheet.cells = sheet.cells ?? new List<PungentDataSheetCell>();

            EnsureStyles();
            RefreshCache(sheet, state);
            using (new EditorGUI.DisabledScope(state.inputBlocked))
                DrawFilterToolbar(sheet, state, onCellEdited);
            RefreshCache(sheet, state);

            float contentWidth = RowHeaderWidth;
            for (int i = 0; i < state.cachedColumns.Count; i++)
                contentWidth += state.cachedColumns[i].Width;

            float contentHeight = HeaderHeight + Mathf.Max(1, state.cachedRows.Count) * RowHeight;
            Rect viewport = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!state.inputBlocked)
                HandleKeyboard(sheet, state, onCellEdited);
            else
                HideEnumSuggestions(state);

            state.scroll = GUI.BeginScrollView(viewport, state.scroll, new Rect(0f, 0f, contentWidth, contentHeight));
            state.enumSuggestionUpdatedThisFrame = false;
            if (!state.inputBlocked)
                ProcessActivePointerInteraction(sheet, state, onCellEdited);
            DrawHeader(sheet, state, viewport, onCellEdited);
            DrawRows(sheet, state, viewport, onCellEdited);
            if (!state.inputBlocked)
                DrawEnumSuggestionOverlay(sheet, state, onCellEdited);
            if (!state.inputBlocked)
                DrawDragIndicator(state);
            GUI.EndScrollView();
        }

        public static PungentDataSheetCell SelectedCell(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            if (sheet == null || state == null || !state.HasCellSelection)
                return null;

            return sheet.FindCell(state.selectedCellRowId, state.selectedCellColumnId);
        }

        public static string GetSelectedCellRawValue(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            if (sheet == null || state == null || !state.HasCellSelection)
                return string.Empty;

            if (state.IsEditingCell &&
                PungentAuthoringId.EqualsId(state.editingCellRowId, state.selectedCellRowId) &&
                PungentAuthoringId.EqualsId(state.editingCellColumnId, state.selectedCellColumnId))
                return state.editBuffer ?? string.Empty;

            RefreshCache(sheet, state);
            return CellRawValue(state.cachedCells, state.selectedCellRowId, state.selectedCellColumnId);
        }

        public static bool CommitSelectedCellValue(PungentDataSheet sheet, PungentDataSheetGridState state, string value, out PungentDataSheetCell committedCell)
        {
            committedCell = null;
            if (sheet == null || state == null || !state.HasCellSelection)
                return false;

            RefreshCache(sheet, state);
            PungentDataSheetGridRowView row = FindRowView(state, state.selectedCellRowId);
            PungentDataSheetGridColumnView column = FindColumnView(state, state.selectedCellColumnId);
            if (row == null || column == null)
                return false;

            if (string.IsNullOrEmpty(value) && row.isVirtual && column.isVirtual)
                return false;

            PungentDataSheetRow materializedRow = MaterializeRow(sheet, row);
            PungentDataSheetColumn materializedColumn = MaterializeColumn(sheet, column);
            committedCell = sheet.GetOrCreateCell(materializedRow.id, materializedColumn.id);
            string before = committedCell.rawValue ?? string.Empty;
            string normalizedValue = NormalizeInputForColumn(value ?? string.Empty, column);
            SetCellValue(committedCell, normalizedValue);
            state.selectedRowId = materializedRow.id;
            state.selectedColumnId = materializedColumn.id;
            state.selectedCellRowId = materializedRow.id;
            state.selectedCellColumnId = materializedColumn.id;
            state.anchorCellRowId = materializedRow.id;
            state.anchorCellColumnId = materializedColumn.id;
            state.editingCellRowId = string.Empty;
            state.editingCellColumnId = string.Empty;
            state.editBuffer = string.Empty;
            state.interaction = PungentDataSheetGridInteraction.Idle;
            sheet.Touch();
            state.InvalidateCache();
            return !string.Equals(before, normalizedValue, StringComparison.Ordinal);
        }

        public static void CommitActiveEdit(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            if (sheet == null || state == null)
                return;

            if (state.IsEditingCell)
            {
                CommitCellEdit(sheet, state, onCellEdited, 0, 0);
                return;
            }

            if (state.interaction == PungentDataSheetGridInteraction.RenamingColumn)
            {
                CommitColumnRename(sheet, state, onCellEdited);
                return;
            }

            if (state.interaction == PungentDataSheetGridInteraction.ResizingColumn ||
                state.interaction == PungentDataSheetGridInteraction.DraggingColumn ||
                state.interaction == PungentDataSheetGridInteraction.DraggingRow ||
                state.interaction == PungentDataSheetGridInteraction.SelectingRange ||
                state.interaction == PungentDataSheetGridInteraction.DraggingFillHandle)
            {
                ClearDragState(state);
                state.resizingColumnId = string.Empty;
                state.interaction = PungentDataSheetGridInteraction.Idle;
            }
        }

        public static bool MaterializeSelectedCell(PungentDataSheet sheet, PungentDataSheetGridState state, out PungentDataSheetCell cell)
        {
            cell = null;
            if (sheet == null || state == null || !state.HasCellSelection)
                return false;

            RefreshCache(sheet, state);
            PungentDataSheetGridRowView row = FindRowView(state, state.selectedCellRowId);
            PungentDataSheetGridColumnView column = FindColumnView(state, state.selectedCellColumnId);
            if (row == null || column == null)
                return false;

            PungentDataSheetRow materializedRow = MaterializeRow(sheet, row);
            PungentDataSheetColumn materializedColumn = MaterializeColumn(sheet, column);
            cell = sheet.GetOrCreateCell(materializedRow.id, materializedColumn.id);
            state.selectedRowId = materializedRow.id;
            state.selectedColumnId = materializedColumn.id;
            state.selectedCellRowId = materializedRow.id;
            state.selectedCellColumnId = materializedColumn.id;
            state.anchorCellRowId = materializedRow.id;
            state.anchorCellColumnId = materializedColumn.id;
            state.InvalidateCache();
            return true;
        }

        public static string AddressFor(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            if (sheet == null || state == null || !state.HasCellSelection)
                return string.Empty;

            RefreshCache(sheet, state);
            int columnIndex = ColumnIndex(state, state.selectedCellColumnId);
            int rowIndex = RowIndex(state, state.selectedCellRowId);
            if (columnIndex < 0 || rowIndex < 0)
                return string.Empty;

            return ColumnLabel(columnIndex) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        public static List<PungentDataSheetCellAddress> GetSelectedCellAddresses(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            List<PungentDataSheetCellAddress> addresses = new List<PungentDataSheetCellAddress>();
            if (sheet == null || state == null || !state.HasCellSelection)
                return addresses;

            RefreshCache(sheet, state);
            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out int columnMax);
            if (rowMin < 0 || columnMin < 0)
                return addresses;

            for (int r = rowMin; r <= rowMax && r < state.cachedRows.Count; r++)
                for (int c = columnMin; c <= columnMax && c < state.cachedColumns.Count; c++)
                    addresses.Add(new PungentDataSheetCellAddress(state.cachedRows[r].Id, state.cachedColumns[c].Id));

            return addresses;
        }

        public static List<string> GetSelectedRowIds(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            List<string> rowIds = new List<string>();
            if (sheet == null || state == null)
                return rowIds;

            RefreshCache(sheet, state);
            if (!string.IsNullOrWhiteSpace(state.selectedRowId))
            {
                PungentDataSheetGridRowView rowView = FindRowView(state, state.selectedRowId);
                if (rowView != null && !rowView.isVirtual)
                    rowIds.Add(PungentAuthoringId.Normalize(state.selectedRowId));
                return rowIds;
            }

            foreach (PungentDataSheetCellAddress address in GetSelectedCellAddresses(sheet, state))
            {
                PungentDataSheetGridRowView rowView = FindRowView(state, address.rowId);
                if (rowView != null && !rowView.isVirtual && !rowIds.Exists(rowId => PungentAuthoringId.EqualsId(rowId, address.rowId)))
                    rowIds.Add(address.rowId);
            }

            return rowIds;
        }

        private static void EnsureStyles()
        {
            if (_cellLabelStyle == null)
            {
                _cellLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(4, 4, 0, 0)
                };
            }

            if (_cellEditStyle == null)
            {
                _cellEditStyle = new GUIStyle(EditorStyles.textField)
                {
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(3, 3, 0, 0)
                };
            }

            if (_cellPlaceholderStyle == null)
            {
                _cellPlaceholderStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Italic,
                    padding = new RectOffset(4, 4, 0, 0)
                };
            }

            if (_sortButtonStyle == null)
            {
                _sortButtonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0),
                    fontSize = 9
                };
            }

            if (_typeBadgeStyle == null)
            {
                _typeBadgeStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontSize = 8,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(1, 1, 0, 0)
                };
                _typeBadgeStyle.normal.textColor = Color.white;
                _typeBadgeStyle.hover.textColor = Color.white;
                _typeBadgeStyle.active.textColor = Color.white;
                _typeBadgeStyle.focused.textColor = Color.white;
            }
        }

        private static void DrawFilterToolbar(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Filter", GUILayout.Width(38f));
                EditorGUI.BeginChangeCheck();
                state.filterText = GUILayout.TextField(state.filterText ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField, GUILayout.MinWidth(120f), GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                    state.InvalidateCache();

                if (!string.IsNullOrWhiteSpace(state.filterText) && GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    state.filterText = string.Empty;
                    state.InvalidateCache();
                }

                GUILayout.Label(state.cachedRows.Count + " visible / " + sheet.rows.Count + " stored row(s)", EditorStyles.miniLabel, GUILayout.Width(154f));

                GUILayout.Space(6f);
                using (new EditorGUI.DisabledScope(!CanFillDown(state)))
                {
                    if (GUILayout.Button(new GUIContent("Fill v", "Copy the top selected cell value down through the selected range."), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                        FillDown(sheet, state, onCellEdited);
                }

                using (new EditorGUI.DisabledScope(!CanFillRight(state)))
                {
                    if (GUILayout.Button(new GUIContent("Fill >", "Copy the left selected cell value right through the selected range."), EditorStyles.toolbarButton, GUILayout.Width(48f)))
                        FillRight(sheet, state, onCellEdited);
                }

                using (new EditorGUI.DisabledScope(!state.HasCellSelection))
                {
                    if (GUILayout.Button(new GUIContent("Clear Cells", "Clear the selected cells."), EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        ClearSelection(sheet, state, onCellEdited);
                }
            }
        }

        private static void RefreshCache(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            string key = (sheet.id ?? string.Empty) + "|" +
                         (sheet.updatedUtc ?? string.Empty) + "|" +
                         (sheet.columns != null ? sheet.columns.Count : 0).ToString(CultureInfo.InvariantCulture) + "|" +
                         (sheet.rows != null ? sheet.rows.Count : 0).ToString(CultureInfo.InvariantCulture) + "|" +
                         (sheet.cells != null ? sheet.cells.Count : 0).ToString(CultureInfo.InvariantCulture) + "|" +
                         (state.filterText ?? string.Empty) + "|" +
                         (state.sortColumnId ?? string.Empty) + "|" +
                         state.sortDescending.ToString();
            if (string.Equals(state.cacheKey, key, StringComparison.Ordinal))
                return;

            state.cacheKey = key;
            state.cachedColumns.Clear();
            state.cachedRows.Clear();
            state.cachedCells.Clear();

            List<PungentDataSheetColumn> sourceColumns = sheet.columns ?? new List<PungentDataSheetColumn>();
            int visibleColumnIndex = 0;
            for (int i = 0; i < sourceColumns.Count; i++)
            {
                PungentDataSheetColumn column = sourceColumns[i];
                if (column == null || column.hidden)
                    continue;

                state.cachedColumns.Add(new PungentDataSheetGridColumnView
                {
                    column = column,
                    modelIndex = i,
                    visibleIndex = visibleColumnIndex++,
                    isVirtual = false
                });
            }

            int desiredColumnCount = sourceColumns.Count == 0
                ? DefaultVirtualColumns
                : state.cachedColumns.Count + TrailingVirtualColumns;
            while (state.cachedColumns.Count < desiredColumnCount)
            {
                int modelIndex = sourceColumns.Count + state.cachedColumns.Count - visibleColumnIndex;
                state.cachedColumns.Add(new PungentDataSheetGridColumnView
                {
                    modelIndex = modelIndex,
                    visibleIndex = state.cachedColumns.Count,
                    isVirtual = true
                });
            }

            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null)
                    continue;

                string cellKey = CellKey(cell.rowId, cell.columnId);
                if (!state.cachedCells.ContainsKey(cellKey))
                    state.cachedCells.Add(cellKey, cell);
            }

            List<PungentDataSheetRow> sourceRows = sheet.rows ?? new List<PungentDataSheetRow>();
            IEnumerable<PungentDataSheetRow> rows = sourceRows.Where(row => row != null && !row.hidden);
            string filter = (state.filterText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(filter))
                rows = rows.Where(row => RowMatchesFilter(row, state.cachedColumns, state.cachedCells, filter));

            PungentDataSheetColumn sortColumn = sheet.FindColumn(state.sortColumnId);
            if (sortColumn != null)
            {
                rows = state.sortDescending
                    ? rows.OrderByDescending(row => SortValue(CellRawValue(state.cachedCells, row.id, sortColumn.id), sortColumn.dataType, state))
                    : rows.OrderBy(row => SortValue(CellRawValue(state.cachedCells, row.id, sortColumn.id), sortColumn.dataType, state));
            }
            else
            {
                rows = rows.OrderBy(row => row.order);
            }

            int visibleRowIndex = 0;
            foreach (PungentDataSheetRow row in rows)
            {
                state.cachedRows.Add(new PungentDataSheetGridRowView
                {
                    row = row,
                    modelIndex = sourceRows.IndexOf(row),
                    visibleIndex = visibleRowIndex++,
                    isVirtual = false
                });
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                int desiredRowCount = sourceRows.Count == 0
                    ? DefaultVirtualRows
                    : state.cachedRows.Count + TrailingVirtualRows;
                while (state.cachedRows.Count < desiredRowCount)
                {
                    int modelIndex = sourceRows.Count + state.cachedRows.Count - visibleRowIndex;
                    state.cachedRows.Add(new PungentDataSheetGridRowView
                    {
                        modelIndex = modelIndex,
                        visibleIndex = state.cachedRows.Count,
                        isVirtual = true
                    });
                }
            }
        }

        private static void ProcessActivePointerInteraction(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            if (state.interaction == PungentDataSheetGridInteraction.ResizingColumn)
            {
                PungentDataSheetColumn column = sheet.FindColumn(state.resizingColumnId);
                if (column != null && evt.type == EventType.MouseDrag)
                {
                    float next = Mathf.Max(MinColumnWidth, state.resizingStartWidth + evt.mousePosition.x - state.resizingStartMouseX);
                    if (!Mathf.Approximately(column.width, next))
                    {
                        column.width = next;
                        sheet.Touch();
                        state.InvalidateCache();
                        onCellEdited?.Invoke(null);
                    }
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    state.resizingColumnId = string.Empty;
                    state.interaction = PungentDataSheetGridInteraction.Idle;
                    evt.Use();
                }
                return;
            }

            if (state.interaction == PungentDataSheetGridInteraction.DraggingFillHandle)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    int rowIndex = RowHitIndexAtY(state, evt.mousePosition.y);
                    int columnIndex = ColumnHitIndexAtX(state, evt.mousePosition.x);
                    if (rowIndex >= 0 && columnIndex >= 0)
                        SelectCell(state, state.cachedRows[rowIndex].Id, state.cachedColumns[columnIndex].Id, true);
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    ApplyFillHandle(sheet, state, onCellEdited);
                    ClearDragState(state);
                    state.interaction = PungentDataSheetGridInteraction.Idle;
                    evt.Use();
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.pendingCellDragRowId) && !string.IsNullOrWhiteSpace(state.pendingCellDragColumnId))
            {
                if (evt.type == EventType.MouseDrag && Vector2.Distance(evt.mousePosition, state.dragStartMouse) > DragThreshold)
                {
                    state.interaction = PungentDataSheetGridInteraction.SelectingRange;
                    state.pendingCellDragRowId = string.Empty;
                    state.pendingCellDragColumnId = string.Empty;
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    ClearDragState(state);
                    evt.Use();
                    return;
                }
            }

            if (state.interaction == PungentDataSheetGridInteraction.SelectingRange)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    int rowIndex = RowHitIndexAtY(state, evt.mousePosition.y);
                    int columnIndex = ColumnHitIndexAtX(state, evt.mousePosition.x);
                    if (rowIndex >= 0 && columnIndex >= 0)
                        SelectCell(state, state.cachedRows[rowIndex].Id, state.cachedColumns[columnIndex].Id, true);
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    ClearDragState(state);
                    state.interaction = PungentDataSheetGridInteraction.Idle;
                    evt.Use();
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(state.pendingColumnDragId))
            {
                if (evt.type == EventType.MouseDrag && Vector2.Distance(evt.mousePosition, state.dragStartMouse) > DragThreshold)
                    StartColumnDrag(sheet, state, onCellEdited);
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    PungentDataSheetGridColumnView column = FindColumnView(state, state.pendingColumnDragId);
                    if (column != null)
                        SelectColumn(state, column);
                    ClearDragState(state);
                    evt.Use();
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(state.pendingRowDragId))
            {
                if (evt.type == EventType.MouseDrag && Vector2.Distance(evt.mousePosition, state.dragStartMouse) > DragThreshold)
                    StartRowDrag(sheet, state, onCellEdited);
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    PungentDataSheetGridRowView row = FindRowView(state, state.pendingRowDragId);
                    if (row != null)
                        SelectRow(state, row);
                    ClearDragState(state);
                    evt.Use();
                    return;
                }
            }

            if (state.interaction == PungentDataSheetGridInteraction.DraggingColumn)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    state.dragColumnToIndex = ColumnInsertionIndexAtX(state, evt.mousePosition.x);
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    CompleteColumnDrag(sheet, state, onCellEdited);
                    evt.Use();
                }
            }
            else if (state.interaction == PungentDataSheetGridInteraction.DraggingRow)
            {
                if (evt.type == EventType.MouseDrag)
                {
                    state.dragRowToIndex = RowInsertionIndexAtY(state, evt.mousePosition.y);
                    evt.Use();
                }
                else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp)
                {
                    CompleteRowDrag(sheet, state, onCellEdited);
                    evt.Use();
                }
            }
        }

        private static void DrawHeader(PungentDataSheet sheet, PungentDataSheetGridState state, Rect viewport, Action<PungentDataSheetCell> onCellEdited)
        {
            Rect corner = new Rect(0f, 0f, RowHeaderWidth, HeaderHeight);
            EditorGUI.DrawRect(corner, HeaderTint);
            GUI.Label(corner, string.Empty, EditorStyles.centeredGreyMiniLabel);

            float x = RowHeaderWidth;
            for (int c = 0; c < state.cachedColumns.Count; c++)
            {
                PungentDataSheetGridColumnView column = state.cachedColumns[c];
                float width = column.Width;
                Rect rect = new Rect(x, 0f, width, HeaderHeight);
                if (rect.xMax >= state.scroll.x && rect.x <= state.scroll.x + viewport.width)
                    DrawColumnHeader(sheet, state, column, rect, onCellEdited);
                x += width;
            }
        }

        private static void DrawColumnHeader(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Rect rect, Action<PungentDataSheetCell> onCellEdited)
        {
            bool selected = PungentAuthoringId.EqualsId(state.selectedColumnId, column.Id);
            EditorGUI.DrawRect(rect, selected ? SelectedTint : column.isVirtual ? VirtualHeaderTint : HeaderTint);

            Rect resize = new Rect(rect.xMax - ResizeHandleWidth, rect.y, ResizeHandleWidth, rect.height);
            Rect sort = new Rect(rect.xMax - ResizeHandleWidth - SortButtonWidth - 2f, rect.y + 3f, SortButtonWidth, rect.height - 6f);
            float typeBadgeWidth = Mathf.Min(TypeBadgeWidth, Mathf.Max(32f, rect.width - SortButtonWidth - ResizeHandleWidth - 12f));
            Rect typeBadge = new Rect(rect.x + 3f, rect.y + 4f, typeBadgeWidth, rect.height - 8f);
            float labelX = typeBadge.xMax + 3f;
            Rect labelRect = new Rect(labelX, rect.y, Mathf.Max(18f, rect.xMax - labelX - SortButtonWidth - ResizeHandleWidth - 6f), rect.height);
            Event evt = Event.current;

            if (!state.inputBlocked && evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) &&
                (state.IsEditingCell || (state.interaction == PungentDataSheetGridInteraction.RenamingColumn && !IsRenamingColumn(state, column))))
                CommitActiveEdit(sheet, state, onCellEdited);

            if (!state.inputBlocked && evt.type == EventType.MouseDown && evt.button == 0 && resize.Contains(evt.mousePosition))
            {
                BeginColumnResize(sheet, state, column, evt.mousePosition.x, onCellEdited);
                evt.Use();
                return;
            }
            EditorGUIUtility.AddCursorRect(resize, MouseCursor.ResizeHorizontal);

            if (IsRenamingColumn(state, column))
            {
                Rect renameRect = new Rect(rect.x + 4f, rect.y + 2f, Mathf.Max(40f, rect.width - ResizeHandleWidth - 10f), rect.height - 4f);
                if (evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
                {
                    CommitColumnRename(sheet, state, onCellEdited);
                    evt.Use();
                    return;
                }

                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    CancelColumnRename(state);
                    evt.Use();
                    return;
                }

                GUI.SetNextControlName("PungentDataSheetColumnRename");
                EditorGUI.BeginChangeCheck();
                state.renameBuffer = EditorGUI.TextField(renameRect, state.renameBuffer ?? string.Empty);
                if (EditorGUI.EndChangeCheck())
                    state.focusRenameControl = false;
                if (state.focusRenameControl)
                {
                    EditorGUI.FocusTextInControl("PungentDataSheetColumnRename");
                    state.focusRenameControl = false;
                }
                return;
            }

            DrawHeaderTypeBadge(typeBadge, column);
            EditorGUIUtility.AddCursorRect(typeBadge, MouseCursor.Link);
            if (!state.inputBlocked && evt.type == EventType.MouseDown && evt.button == 0 && typeBadge.Contains(evt.mousePosition))
            {
                SelectColumn(state, column);
                ShowColumnTypeMenu(sheet, state, column, onCellEdited);
                evt.Use();
                return;
            }

            string label = ColumnHeaderLabel(column);
            GUI.Label(labelRect, new GUIContent(label, "Single click to select. Double click to rename. Drag horizontally to reorder."), EditorStyles.toolbarButton);
            string sortLabel = PungentAuthoringId.EqualsId(state.sortColumnId, column.Id)
                ? state.sortDescending ? "v" : "^"
                : "S";
            EditorGUI.BeginDisabledGroup(state.inputBlocked);
            bool sortClicked = GUI.Button(sort, new GUIContent(sortLabel, "Sort by this column."), _sortButtonStyle);
            EditorGUI.EndDisabledGroup();
            if (sortClicked)
            {
                bool wasVirtual = column.isVirtual;
                PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
                ToggleSort(state, materialized.id);
                state.InvalidateCache();
                if (wasVirtual)
                    onCellEdited?.Invoke(null);
            }

            if (!state.inputBlocked && evt.type == EventType.MouseDown && evt.button == 0 && labelRect.Contains(evt.mousePosition))
            {
                if (evt.clickCount >= 2)
                {
                    BeginColumnRename(sheet, state, column, onCellEdited);
                    evt.Use();
                    return;
                }

                ClearDragState(state);
                state.pendingColumnDragId = column.Id;
                state.pendingColumnDragIndex = ColumnIndex(state, column.Id);
                state.dragStartMouse = evt.mousePosition;
                evt.Use();
            }
            else if (!state.inputBlocked && ((evt.type == EventType.MouseDown && evt.button == 1) || evt.type == EventType.ContextClick) && rect.Contains(evt.mousePosition))
            {
                SelectColumn(state, column);
                ShowColumnMenu(sheet, state, column, onCellEdited);
                evt.Use();
            }
        }

        private static void DrawRows(PungentDataSheet sheet, PungentDataSheetGridState state, Rect viewport, Action<PungentDataSheetCell> onCellEdited)
        {
            int first = Mathf.Max(0, Mathf.FloorToInt((state.scroll.y - HeaderHeight) / RowHeight));
            int visibleCount = Mathf.CeilToInt(viewport.height / RowHeight) + 3;
            int last = Mathf.Min(state.cachedRows.Count - 1, first + visibleCount);
            if (state.cachedRows.Count == 0)
                return;

            for (int r = first; r <= last; r++)
                DrawRow(sheet, state, state.cachedRows[r], viewport, onCellEdited);
        }

        private static void DrawRow(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, Rect viewport, Action<PungentDataSheetCell> onCellEdited)
        {
            float y = HeaderHeight + row.visibleIndex * RowHeight;
            Rect rowHeader = new Rect(0f, y, RowHeaderWidth, RowHeight);
            bool rowSelected = PungentAuthoringId.EqualsId(state.selectedRowId, row.Id);
            EditorGUI.DrawRect(rowHeader, rowSelected ? SelectedTint : row.isVirtual ? VirtualHeaderTint : HeaderTint);
            GUI.Label(rowHeader, new GUIContent((row.visibleIndex + 1).ToString(CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(row.DisplayName) ? row.Id : row.DisplayName), EditorStyles.toolbarButton);
            if (row.row != null && row.row.linkedAuthoringRef != null && row.row.linkedAuthoringRef.HasItemId)
            {
                Rect rowLinkBadge = new Rect(rowHeader.xMax - 18f, rowHeader.y + 5f, 15f, rowHeader.height - 10f);
                EditorGUI.DrawRect(rowLinkBadge, ReferenceTypeTint);
                GUI.Label(rowLinkBadge, new GUIContent("ref", "This row has a linked authoring reference."), EditorStyles.centeredGreyMiniLabel);
            }

            Event evt = Event.current;
            if (!state.inputBlocked && evt.type == EventType.MouseDown && rowHeader.Contains(evt.mousePosition) &&
                (state.IsEditingCell || state.interaction == PungentDataSheetGridInteraction.RenamingColumn))
                CommitActiveEdit(sheet, state, onCellEdited);

            if (!state.inputBlocked && evt.type == EventType.MouseDown && evt.button == 0 && rowHeader.Contains(evt.mousePosition))
            {
                ClearDragState(state);
                state.pendingRowDragId = row.Id;
                state.pendingRowDragIndex = RowIndex(state, row.Id);
                state.dragStartMouse = evt.mousePosition;
                evt.Use();
            }
            else if (!state.inputBlocked && ((evt.type == EventType.MouseDown && evt.button == 1) || evt.type == EventType.ContextClick) && rowHeader.Contains(evt.mousePosition))
            {
                SelectRow(state, row);
                ShowRowMenu(sheet, state, row, onCellEdited);
                evt.Use();
            }

            float x = RowHeaderWidth;
            for (int c = 0; c < state.cachedColumns.Count; c++)
            {
                PungentDataSheetGridColumnView column = state.cachedColumns[c];
                float width = column.Width;
                Rect rect = new Rect(x, y, width, RowHeight);
                if (rect.xMax >= state.scroll.x && rect.x <= state.scroll.x + viewport.width)
                    DrawCell(sheet, state, row, column, rect, onCellEdited);
                x += width;
            }
        }

        private static void DrawCell(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column, Rect rect, Action<PungentDataSheetCell> onCellEdited)
        {
            string key = CellKey(row.Id, column.Id);
            state.cachedCells.TryGetValue(key, out PungentDataSheetCell cell);
            string rawValue = CurrentCellValue(state, row.Id, column.Id);
            bool selected = PungentAuthoringId.EqualsId(state.selectedCellRowId, row.Id) && PungentAuthoringId.EqualsId(state.selectedCellColumnId, column.Id);
            bool inRange = IsInSelectedRange(state, row.Id, column.Id);
            bool editing = IsEditingCell(state, row.Id, column.Id);
            bool linked = HasLinkedReference(cell) || PungentDataSheetAuthoringReferenceCodec.TryParseStructured(rawValue, out _);
            bool missingReference = linked && HasMissingReferenceProvider(cell, rawValue);
            string invalidTypeMessage = InvalidTypeMessage(rawValue, column, state);

            if (row.isVirtual || column.isVirtual)
                EditorGUI.DrawRect(rect, VirtualCellTint);
            if (inRange)
                EditorGUI.DrawRect(rect, SelectedTint);
            if (linked)
                EditorGUI.DrawRect(rect, LinkedTint);
            if (!string.IsNullOrEmpty(invalidTypeMessage))
                EditorGUI.DrawRect(rect, InvalidTint);
            else if (cell != null && cell.propagationStatus == PungentDataSheetPropagationStatus.Changed)
                EditorGUI.DrawRect(rect, ChangedTint);

            DrawTypeStripe(rect, column);
            Rect inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Rect fillHandle = new Rect(rect.xMax - 7f, rect.yMax - 7f, 6f, 6f);
            Event evt = Event.current;
            bool mouseBlockedByEnumOverlay = state.inputBlocked ||
                                             (state.enumSuggestionActive &&
                                              state.enumSuggestionRect.Contains(evt.mousePosition) &&
                                              !editing);
            if (!mouseBlockedByEnumOverlay && evt.type == EventType.MouseDown && evt.button == 0 && selected && !editing && fillHandle.Contains(evt.mousePosition))
            {
                BeginFillHandle(state, row.Id, column.Id);
                evt.Use();
            }
            else if (!mouseBlockedByEnumOverlay && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                if ((state.IsEditingCell && !editing) || state.interaction == PungentDataSheetGridInteraction.RenamingColumn)
                    CommitActiveEdit(sheet, state, onCellEdited);

                SelectCell(state, row.Id, column.Id, evt.shift);
                if (evt.clickCount == 1 && column.DataType == PungentDataSheetDataType.Boolean && !row.Locked && !column.Locked && !evt.shift)
                {
                    ToggleBooleanCell(sheet, state, row, column, onCellEdited);
                }
                else if (evt.clickCount >= 2 && column.DataType != PungentDataSheetDataType.Boolean && !row.Locked && !column.Locked)
                {
                    BeginCellEdit(sheet, state, row, column, CurrentCellValue(state, row.Id, column.Id));
                }
                else
                {
                    ClearDragState(state);
                    state.pendingCellDragRowId = row.Id;
                    state.pendingCellDragColumnId = column.Id;
                    state.dragStartMouse = evt.mousePosition;
                    ClearGuiFocus();
                }
                evt.Use();
            }
            else if (!mouseBlockedByEnumOverlay && ((evt.type == EventType.MouseDown && evt.button == 1) || evt.type == EventType.ContextClick) && rect.Contains(evt.mousePosition))
            {
                if (state.IsEditingCell || state.interaction == PungentDataSheetGridInteraction.RenamingColumn)
                    CommitActiveEdit(sheet, state, onCellEdited);
                if (!IsInSelectedRange(state, row.Id, column.Id))
                    SelectCell(state, row.Id, column.Id, evt.shift);
                ShowCellMenu(sheet, state, row, column, onCellEdited);
                evt.Use();
            }

            EditorGUI.BeginDisabledGroup(row.Locked || column.Locked);
            if (editing)
            {
                GUI.SetNextControlName(state.editControlName);
                EditorGUI.BeginChangeCheck();
                state.editBuffer = DrawCellEditField(inner, state.editBuffer ?? string.Empty, sheet, state, row, column);
                if (EditorGUI.EndChangeCheck())
                    state.focusEditControl = false;
                if (state.focusEditControl)
                {
                    EditorGUI.FocusTextInControl(state.editControlName);
                    state.focusEditControl = false;
                }
            }
            else
            {
                string display = string.IsNullOrEmpty(invalidTypeMessage)
                    ? DisplayCellValue(rawValue, column, state)
                    : invalidTypeMessage;
                if (linked && string.IsNullOrWhiteSpace(display))
                    display = LinkedDisplayValue(cell, rawValue);
                GUIStyle style = string.IsNullOrWhiteSpace(display) ? _cellPlaceholderStyle : _cellLabelStyle;
                if (string.IsNullOrWhiteSpace(display))
                    display = PlaceholderForColumn(column, state);
                if (column.DataType == PungentDataSheetDataType.Boolean &&
                    string.IsNullOrWhiteSpace(invalidTypeMessage) &&
                    !string.IsNullOrWhiteSpace(rawValue))
                {
                    Rect toggleRect = new Rect(inner.x + 3f, inner.y + 2f, 18f, inner.height - 4f);
                    Rect labelRect = new Rect(toggleRect.xMax + 2f, inner.y, inner.width - 23f, inner.height);
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUI.Toggle(toggleRect, IsTruthy(rawValue));
                    GUI.Label(labelRect, new GUIContent(display, CellTooltip(cell, column, rawValue, invalidTypeMessage, missingReference)), style);
                }
                else
                {
                    GUI.Label(inner, new GUIContent(display, CellTooltip(cell, column, rawValue, invalidTypeMessage, missingReference)), style);
                }
            }
            EditorGUI.EndDisabledGroup();

            if (linked)
            {
                Rect badge = new Rect(rect.xMax - 31f, rect.y + 2f, 28f, 13f);
                EditorGUI.DrawRect(badge, missingReference ? MissingReferenceTint : ReferenceTypeTint);
                GUI.Label(badge, new GUIContent(missingReference ? "miss" : "ref", missingReference ? "Linked reference provider is missing." : "Linked authoring reference."), EditorStyles.centeredGreyMiniLabel);
            }

            Handles.color = selected ? ActiveBorder : CellBorder;
            Handles.DrawLine(new Vector3(rect.x, rect.yMax), new Vector3(rect.xMax, rect.yMax));
            Handles.DrawLine(new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax));
            if (selected)
            {
                Handles.DrawAAPolyLine(2f, new Vector3(rect.x, rect.y), new Vector3(rect.xMax, rect.y), new Vector3(rect.xMax, rect.yMax), new Vector3(rect.x, rect.yMax), new Vector3(rect.x, rect.y));
                EditorGUI.DrawRect(fillHandle, ActiveBorder);
            }
        }

        private static void HandleKeyboard(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            Event evt = Event.current;
            if (evt.type != EventType.KeyDown || sheet == null || state.inputBlocked)
                return;

            if (state.interaction == PungentDataSheetGridInteraction.RenamingColumn)
                return;

            if (state.IsEditingCell)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    CommitCellEdit(sheet, state, onCellEdited, 1, 0);
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.Tab)
                {
                    CommitCellEdit(sheet, state, onCellEdited, 0, evt.shift ? -1 : 1);
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CancelCellEdit(state);
                    evt.Use();
                }
                else
                {
                    string editBufferText = PrintableText(evt);
                    if (!string.IsNullOrEmpty(editBufferText) &&
                        !string.Equals(GUI.GetNameOfFocusedControl(), state.editControlName, StringComparison.Ordinal))
                    {
                        state.editBuffer += editBufferText;
                        state.focusEditControl = true;
                        evt.Use();
                    }
                }
                return;
            }

            if (!state.HasCellSelection)
                return;

            RefreshCache(sheet, state);
            int row = RowIndex(state, state.selectedCellRowId);
            int column = ColumnIndex(state, state.selectedCellColumnId);
            if (row < 0 || column < 0)
                return;

            if (evt.control || evt.command)
            {
                if (evt.keyCode == KeyCode.C)
                {
                    EditorGUIUtility.systemCopyBuffer = BuildClipboard(sheet, state);
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.V)
                {
                    PasteClipboard(sheet, state, EditorGUIUtility.systemCopyBuffer, onCellEdited);
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.X)
                {
                    EditorGUIUtility.systemCopyBuffer = BuildClipboard(sheet, state);
                    ClearSelection(sheet, state, onCellEdited);
                    evt.Use();
                }
                return;
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                ClearSelection(sheet, state, onCellEdited);
                evt.Use();
                return;
            }

            PungentDataSheetGridRowView selectedRow = state.cachedRows[row];
            PungentDataSheetGridColumnView selectedColumn = state.cachedColumns[column];
            if (selectedColumn.DataType == PungentDataSheetDataType.Boolean &&
                (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.F2))
            {
                ToggleBooleanCell(sheet, state, selectedRow, selectedColumn, onCellEdited);
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.F2)
            {
                BeginCellEdit(sheet, state, selectedRow, selectedColumn, CurrentCellValue(state, state.selectedCellRowId, state.selectedCellColumnId));
                evt.Use();
                return;
            }

            string typed = PrintableText(evt);
            if (!string.IsNullOrEmpty(typed))
            {
                if (selectedColumn.DataType == PungentDataSheetDataType.Boolean)
                {
                    if (TryBooleanShortcut(typed, out bool boolValue))
                        SetBooleanCell(sheet, state, selectedRow, selectedColumn, boolValue, onCellEdited);
                    evt.Use();
                    return;
                }

                string initial = InitialTypedValueForColumn(typed, selectedColumn);
                if (!string.IsNullOrEmpty(initial))
                    BeginCellEdit(sheet, state, selectedRow, selectedColumn, initial);
                evt.Use();
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow:
                    MoveSelection(state, row, column - 1, evt.shift);
                    evt.Use();
                    break;
                case KeyCode.RightArrow:
                case KeyCode.Tab:
                    MoveSelection(state, row, column + 1, evt.shift);
                    evt.Use();
                    break;
                case KeyCode.UpArrow:
                    MoveSelection(state, row - 1, column, evt.shift);
                    evt.Use();
                    break;
                case KeyCode.DownArrow:
                    MoveSelection(state, row + 1, column, evt.shift);
                    evt.Use();
                    break;
                case KeyCode.Escape:
                    ClearGuiFocus();
                    evt.Use();
                    break;
            }
        }

        private static void BeginCellEdit(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column, string initialValue)
        {
            if (sheet == null || state == null || row == null || column == null)
                return;

            state.interaction = PungentDataSheetGridInteraction.EditingCell;
            state.editingCellRowId = row.Id;
            state.editingCellColumnId = column.Id;
            state.selectedRowId = row.Id;
            state.selectedColumnId = column.Id;
            state.selectedCellRowId = row.Id;
            state.selectedCellColumnId = column.Id;
            state.anchorCellRowId = row.Id;
            state.anchorCellColumnId = column.Id;
            state.editBuffer = initialValue ?? string.Empty;
            state.editControlName = "PungentDataSheetCellEdit_" + row.visibleIndex.ToString(CultureInfo.InvariantCulture) + "_" + column.visibleIndex.ToString(CultureInfo.InvariantCulture);
            state.focusEditControl = true;
            state.renamingColumnId = string.Empty;
            ClearDragState(state);
        }

        private static void BeginFillHandle(PungentDataSheetGridState state, string rowId, string columnId)
        {
            ClearDragState(state);
            state.interaction = PungentDataSheetGridInteraction.DraggingFillHandle;
            state.fillSourceRowId = rowId;
            state.fillSourceColumnId = columnId;
            state.anchorCellRowId = rowId;
            state.anchorCellColumnId = columnId;
            state.selectedRowId = rowId;
            state.selectedColumnId = columnId;
            state.selectedCellRowId = rowId;
            state.selectedCellColumnId = columnId;
        }

        private static void CommitCellEdit(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited, int rowDelta, int columnDelta)
        {
            if (sheet == null || state == null || !state.IsEditingCell)
                return;

            RefreshCache(sheet, state);
            PungentDataSheetGridRowView row = FindRowView(state, state.editingCellRowId);
            PungentDataSheetGridColumnView column = FindColumnView(state, state.editingCellColumnId);
            int startRow = RowIndex(state, state.editingCellRowId);
            int startColumn = ColumnIndex(state, state.editingCellColumnId);
            if (row == null || column == null)
            {
                CancelCellEdit(state);
                return;
            }

            PungentDataSheetCell cell = null;
            string value = NormalizeInputForColumn(state.editBuffer ?? string.Empty, column);
            bool shouldMaterialize = !string.IsNullOrEmpty(value) || !row.isVirtual || !column.isVirtual;
            if (shouldMaterialize)
            {
                PungentDataSheetRow materializedRow = MaterializeRow(sheet, row);
                PungentDataSheetColumn materializedColumn = MaterializeColumn(sheet, column);
                cell = sheet.GetOrCreateCell(materializedRow.id, materializedColumn.id);
                string before = cell.rawValue ?? string.Empty;
                SetCellValue(cell, value);
                state.selectedRowId = materializedRow.id;
                state.selectedColumnId = materializedColumn.id;
                state.selectedCellRowId = materializedRow.id;
                state.selectedCellColumnId = materializedColumn.id;
                state.anchorCellRowId = materializedRow.id;
                state.anchorCellColumnId = materializedColumn.id;
                if (!string.Equals(before, value, StringComparison.Ordinal))
                {
                    sheet.Touch();
                    onCellEdited?.Invoke(cell);
                }
            }

            state.interaction = PungentDataSheetGridInteraction.Idle;
            state.editingCellRowId = string.Empty;
            state.editingCellColumnId = string.Empty;
            state.editBuffer = string.Empty;
            state.editControlName = string.Empty;
            state.focusEditControl = false;
            ClearGuiFocus();
            state.InvalidateCache();
            RefreshCache(sheet, state);
            if (rowDelta != 0 || columnDelta != 0)
                MoveSelection(state, startRow + rowDelta, startColumn + columnDelta, false);
        }

        private static void CancelCellEdit(PungentDataSheetGridState state)
        {
            state.interaction = PungentDataSheetGridInteraction.Idle;
            state.editingCellRowId = string.Empty;
            state.editingCellColumnId = string.Empty;
            state.editBuffer = string.Empty;
            state.editControlName = string.Empty;
            state.focusEditControl = false;
            ClearGuiFocus();
        }

        private static void BeginColumnResize(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, float mouseX, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
            state.resizingColumnId = materialized.id;
            state.resizingStartMouseX = mouseX;
            state.resizingStartWidth = materialized.width;
            state.interaction = PungentDataSheetGridInteraction.ResizingColumn;
            state.renamingColumnId = string.Empty;
            state.selectedColumnId = materialized.id;
            state.selectedRowId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
            state.InvalidateCache();
            onCellEdited?.Invoke(null);
        }

        private static void BeginColumnRename(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            if (state.interaction == PungentDataSheetGridInteraction.RenamingColumn &&
                !PungentAuthoringId.EqualsId(state.renamingColumnId, column != null ? column.Id : string.Empty))
                CommitColumnRename(sheet, state, onCellEdited);

            state.interaction = PungentDataSheetGridInteraction.RenamingColumn;
            state.renamingColumnId = column != null ? column.Id : string.Empty;
            state.renameBuffer = column != null ? ColumnHeaderLabel(column) : string.Empty;
            state.renameOriginal = state.renameBuffer;
            state.focusRenameControl = true;
            state.selectedColumnId = state.renamingColumnId;
            state.selectedRowId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
        }

        private static void CommitColumnRename(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetColumn column = sheet.FindColumn(state.renamingColumnId);
            bool materialized = false;
            if (column == null)
            {
                PungentDataSheetGridColumnView view = FindColumnView(state, state.renamingColumnId);
                if (view != null)
                {
                    column = MaterializeColumn(sheet, view);
                    materialized = true;
                    state.selectedColumnId = column.id;
                }
            }

            if (column != null)
            {
                string next = string.IsNullOrWhiteSpace(state.renameBuffer) ? column.displayName : state.renameBuffer.Trim();
                if (materialized || !string.Equals(column.displayName, next, StringComparison.Ordinal))
                {
                    column.displayName = next;
                    column.NormalizeInPlace();
                    sheet.Touch();
                    state.InvalidateCache();
                    onCellEdited?.Invoke(null);
                }
            }

            CancelColumnRename(state);
        }

        private static void CancelColumnRename(PungentDataSheetGridState state)
        {
            state.interaction = PungentDataSheetGridInteraction.Idle;
            state.renamingColumnId = string.Empty;
            state.renameBuffer = string.Empty;
            state.renameOriginal = string.Empty;
            state.focusRenameControl = false;
            ClearGuiFocus();
        }

        private static void StartColumnDrag(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetGridColumnView column = FindColumnView(state, state.pendingColumnDragId);
            if (column == null)
                return;

            bool wasVirtual = column.isVirtual;
            PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
            if (wasVirtual)
                onCellEdited?.Invoke(null);
            state.draggingColumnId = materialized.id;
            state.dragColumnFromIndex = Mathf.Max(0, materializedIndex(sheet.columns, materialized.id));
            state.dragColumnToIndex = Mathf.Clamp(state.pendingColumnDragIndex, 0, state.cachedColumns.Count);
            state.pendingColumnDragId = string.Empty;
            state.pendingColumnDragIndex = -1;
            state.interaction = PungentDataSheetGridInteraction.DraggingColumn;
            state.selectedColumnId = materialized.id;
            state.selectedRowId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
            state.InvalidateCache();
            RefreshCache(sheet, state);
        }

        private static void StartRowDrag(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            if (!string.IsNullOrWhiteSpace(state.sortColumnId) || !string.IsNullOrWhiteSpace(state.filterText))
            {
                ClearDragState(state);
                return;
            }

            PungentDataSheetGridRowView row = FindRowView(state, state.pendingRowDragId);
            if (row == null)
                return;

            bool wasVirtual = row.isVirtual;
            PungentDataSheetRow materialized = MaterializeRow(sheet, row);
            if (wasVirtual)
                onCellEdited?.Invoke(null);
            state.draggingRowId = materialized.id;
            state.dragRowFromIndex = Mathf.Max(0, materializedIndex(sheet.rows, materialized.id));
            state.dragRowToIndex = Mathf.Clamp(state.pendingRowDragIndex, 0, state.cachedRows.Count);
            state.pendingRowDragId = string.Empty;
            state.pendingRowDragIndex = -1;
            state.interaction = PungentDataSheetGridInteraction.DraggingRow;
            state.selectedRowId = materialized.id;
            state.selectedColumnId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
            state.InvalidateCache();
            RefreshCache(sheet, state);
        }

        private static void CompleteColumnDrag(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetColumn column = sheet.FindColumn(state.draggingColumnId);
            int from = column != null ? materializedIndex(sheet.columns, column.id) : -1;
            int to = Mathf.Clamp(state.dragColumnToIndex, 0, sheet.columns.Count);
            if (from >= 0 && to > from)
                to--;
            if (from >= 0 && to >= 0 && from != to)
            {
                sheet.columns.RemoveAt(from);
                to = Mathf.Clamp(to, 0, sheet.columns.Count);
                sheet.columns.Insert(to, column);
                sheet.Touch();
                onCellEdited?.Invoke(null);
            }

            ClearDragState(state);
            state.interaction = PungentDataSheetGridInteraction.Idle;
            state.InvalidateCache();
        }

        private static void CompleteRowDrag(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetRow row = sheet.FindRow(state.draggingRowId);
            int from = row != null ? materializedIndex(sheet.rows, row.id) : -1;
            int to = Mathf.Clamp(state.dragRowToIndex, 0, sheet.rows.Count);
            if (from >= 0 && to > from)
                to--;
            if (from >= 0 && to >= 0 && from != to)
            {
                sheet.rows.RemoveAt(from);
                to = Mathf.Clamp(to, 0, sheet.rows.Count);
                sheet.rows.Insert(to, row);
                sheet.ReindexRows();
                sheet.Touch();
                onCellEdited?.Invoke(null);
            }

            ClearDragState(state);
            state.interaction = PungentDataSheetGridInteraction.Idle;
            state.InvalidateCache();
        }

        private static void ApplyFillHandle(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            if (sheet == null || state == null || string.IsNullOrWhiteSpace(state.fillSourceRowId) || string.IsNullOrWhiteSpace(state.fillSourceColumnId))
                return;

            RefreshCache(sheet, state);
            int sourceRow = RowIndex(state, state.fillSourceRowId);
            int sourceColumn = ColumnIndex(state, state.fillSourceColumnId);
            int targetRow = RowIndex(state, state.selectedCellRowId);
            int targetColumn = ColumnIndex(state, state.selectedCellColumnId);
            if (sourceRow < 0 || sourceColumn < 0 || targetRow < 0 || targetColumn < 0)
                return;

            string value = CellRawValue(state.cachedCells, state.fillSourceRowId, state.fillSourceColumnId);
            if (string.IsNullOrEmpty(value))
                return;

            int rowMin = Mathf.Min(sourceRow, targetRow);
            int rowMax = Mathf.Max(sourceRow, targetRow);
            int columnMin = Mathf.Min(sourceColumn, targetColumn);
            int columnMax = Mathf.Max(sourceColumn, targetColumn);
            bool changed = false;

            for (int r = rowMin; r <= rowMax; r++)
            {
                for (int c = columnMin; c <= columnMax; c++)
                {
                    if (r == sourceRow && c == sourceColumn)
                        continue;

                    PungentDataSheetRow row = MaterializeRow(sheet, state.cachedRows[r]);
                    PungentDataSheetColumn column = MaterializeColumn(sheet, state.cachedColumns[c]);
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
                    string before = cell.rawValue ?? string.Empty;
                    SetCellValue(cell, value);
                    if (!string.Equals(before, value, StringComparison.Ordinal))
                    {
                        changed = true;
                        onCellEdited?.Invoke(cell);
                    }
                }
            }

            if (changed)
            {
                sheet.Touch();
                state.InvalidateCache();
            }
        }

        private static void ToggleBooleanCell(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            PungentDataSheetCell existing = sheet != null && row != null && column != null
                ? sheet.FindCell(row.Id, column.Id)
                : null;
            SetBooleanCell(sheet, state, row, column, !IsTruthy(existing != null ? existing.rawValue : string.Empty), onCellEdited);
        }

        private static void SetBooleanCell(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column, bool value, Action<PungentDataSheetCell> onCellEdited)
        {
            if (sheet == null || state == null || row == null || column == null)
                return;

            PungentDataSheetRow materializedRow = MaterializeRow(sheet, row);
            PungentDataSheetColumn materializedColumn = MaterializeColumn(sheet, column);
            PungentDataSheetCell cell = sheet.GetOrCreateCell(materializedRow.id, materializedColumn.id);
            string next = value ? "true" : "false";
            if (!string.Equals(cell.rawValue, next, StringComparison.Ordinal))
            {
                SetCellValue(cell, next);
                sheet.Touch();
                state.selectedRowId = materializedRow.id;
                state.selectedColumnId = materializedColumn.id;
                state.selectedCellRowId = materializedRow.id;
                state.selectedCellColumnId = materializedColumn.id;
                state.anchorCellRowId = materializedRow.id;
                state.anchorCellColumnId = materializedColumn.id;
                state.InvalidateCache();
                onCellEdited?.Invoke(cell);
            }
        }

        private static bool TryBooleanShortcut(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "t" || normalized == "y" || normalized == "1")
            {
                result = true;
                return true;
            }

            if (normalized == "f" || normalized == "n" || normalized == "0")
            {
                result = false;
                return true;
            }

            return false;
        }

        private static void ClearDragState(PungentDataSheetGridState state)
        {
            state.pendingColumnDragId = string.Empty;
            state.pendingRowDragId = string.Empty;
            state.pendingCellDragRowId = string.Empty;
            state.pendingCellDragColumnId = string.Empty;
            state.fillSourceRowId = string.Empty;
            state.fillSourceColumnId = string.Empty;
            state.pendingColumnDragIndex = -1;
            state.pendingRowDragIndex = -1;
            state.draggingColumnId = string.Empty;
            state.draggingRowId = string.Empty;
            state.dragColumnFromIndex = -1;
            state.dragColumnToIndex = -1;
            state.dragRowFromIndex = -1;
            state.dragRowToIndex = -1;
        }

        private static void DrawDragIndicator(PungentDataSheetGridState state)
        {
            if (state.interaction == PungentDataSheetGridInteraction.DraggingColumn && state.dragColumnToIndex >= 0)
            {
                float x = ColumnXForIndex(state, state.dragColumnToIndex);
                Handles.color = ActiveBorder;
                Handles.DrawAAPolyLine(3f, new Vector3(x, 0f), new Vector3(x, HeaderHeight + state.cachedRows.Count * RowHeight));
            }
            else if (state.interaction == PungentDataSheetGridInteraction.DraggingRow && state.dragRowToIndex >= 0)
            {
                float y = HeaderHeight + Mathf.Clamp(state.dragRowToIndex, 0, state.cachedRows.Count) * RowHeight;
                Handles.color = ActiveBorder;
                Handles.DrawAAPolyLine(3f, new Vector3(0f, y), new Vector3(RowHeaderWidth + TotalColumnsWidth(state), y));
            }
        }

        private static void MoveSelection(PungentDataSheetGridState state, int rowIndex, int columnIndex, bool extend)
        {
            if (state.cachedRows.Count == 0 || state.cachedColumns.Count == 0)
                return;

            rowIndex = Mathf.Clamp(rowIndex, 0, state.cachedRows.Count - 1);
            columnIndex = Mathf.Clamp(columnIndex, 0, state.cachedColumns.Count - 1);
            SelectCell(state, state.cachedRows[rowIndex].Id, state.cachedColumns[columnIndex].Id, extend);
        }

        private static void SelectCell(PungentDataSheetGridState state, string rowId, string columnId, bool extend)
        {
            if (!extend || string.IsNullOrWhiteSpace(state.anchorCellRowId) || string.IsNullOrWhiteSpace(state.anchorCellColumnId))
            {
                state.anchorCellRowId = rowId;
                state.anchorCellColumnId = columnId;
            }

            state.selectedRowId = rowId;
            state.selectedColumnId = columnId;
            state.selectedCellRowId = rowId;
            state.selectedCellColumnId = columnId;
        }

        private static void SelectRow(PungentDataSheetGridState state, PungentDataSheetGridRowView row)
        {
            state.selectedRowId = row.Id;
            state.selectedColumnId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
            state.anchorCellRowId = string.Empty;
            state.anchorCellColumnId = string.Empty;
        }

        private static void SelectColumn(PungentDataSheetGridState state, PungentDataSheetGridColumnView column)
        {
            state.selectedColumnId = column.Id;
            state.selectedRowId = string.Empty;
            state.selectedCellRowId = string.Empty;
            state.selectedCellColumnId = string.Empty;
            state.anchorCellRowId = string.Empty;
            state.anchorCellColumnId = string.Empty;
        }

        private static bool IsInSelectedRange(PungentDataSheetGridState state, string rowId, string columnId)
        {
            if (!state.HasCellSelection)
                return false;

            int row = RowIndex(state, rowId);
            int column = ColumnIndex(state, columnId);
            int selectedRow = RowIndex(state, state.selectedCellRowId);
            int selectedColumn = ColumnIndex(state, state.selectedCellColumnId);
            int anchorRow = RowIndex(state, string.IsNullOrWhiteSpace(state.anchorCellRowId) ? state.selectedCellRowId : state.anchorCellRowId);
            int anchorColumn = ColumnIndex(state, string.IsNullOrWhiteSpace(state.anchorCellColumnId) ? state.selectedCellColumnId : state.anchorCellColumnId);
            if (row < 0 || column < 0 || selectedRow < 0 || selectedColumn < 0 || anchorRow < 0 || anchorColumn < 0)
                return false;

            return row >= Mathf.Min(selectedRow, anchorRow) &&
                   row <= Mathf.Max(selectedRow, anchorRow) &&
                   column >= Mathf.Min(selectedColumn, anchorColumn) &&
                   column <= Mathf.Max(selectedColumn, anchorColumn);
        }

        private static void GetSelectedRange(PungentDataSheetGridState state, out int rowMin, out int rowMax, out int columnMin, out int columnMax)
        {
            int selectedRow = RowIndex(state, state.selectedCellRowId);
            int selectedColumn = ColumnIndex(state, state.selectedCellColumnId);
            int anchorRow = RowIndex(state, string.IsNullOrWhiteSpace(state.anchorCellRowId) ? state.selectedCellRowId : state.anchorCellRowId);
            int anchorColumn = ColumnIndex(state, string.IsNullOrWhiteSpace(state.anchorCellColumnId) ? state.selectedCellColumnId : state.anchorCellColumnId);
            rowMin = Mathf.Min(selectedRow, anchorRow);
            rowMax = Mathf.Max(selectedRow, anchorRow);
            columnMin = Mathf.Min(selectedColumn, anchorColumn);
            columnMax = Mathf.Max(selectedColumn, anchorColumn);
        }

        private static string BuildClipboard(PungentDataSheet sheet, PungentDataSheetGridState state)
        {
            RefreshCache(sheet, state);
            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out int columnMax);
            if (rowMin < 0 || columnMin < 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (int r = rowMin; r <= rowMax; r++)
            {
                if (r > rowMin)
                    builder.AppendLine();
                for (int c = columnMin; c <= columnMax; c++)
                {
                    if (c > columnMin)
                        builder.Append('\t');
                    string value = CellRawValue(state.cachedCells, state.cachedRows[r].Id, state.cachedColumns[c].Id);
                    builder.Append(value);
                }
            }

            return builder.ToString();
        }

        private static void PasteClipboard(PungentDataSheet sheet, PungentDataSheetGridState state, string clipboard, Action<PungentDataSheetCell> onCellEdited)
        {
            if (string.IsNullOrEmpty(clipboard))
                return;

            RefreshCache(sheet, state);
            int startRow = RowIndex(state, state.selectedCellRowId);
            int startColumn = ColumnIndex(state, state.selectedCellColumnId);
            if (startRow < 0 || startColumn < 0)
                return;

            string[] lines = clipboard.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            PungentDataSheetRow firstRow = null;
            PungentDataSheetColumn firstColumn = null;
            for (int r = 0; r < lines.Length && startRow + r < state.cachedRows.Count; r++)
            {
                string[] values = lines[r].Split('\t');
                for (int c = 0; c < values.Length && startColumn + c < state.cachedColumns.Count; c++)
                {
                    PungentDataSheetRow row = MaterializeRow(sheet, state.cachedRows[startRow + r]);
                    PungentDataSheetColumn column = MaterializeColumn(sheet, state.cachedColumns[startColumn + c]);
                    if (firstRow == null)
                        firstRow = row;
                    if (firstColumn == null)
                        firstColumn = column;
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
                    SetCellValue(cell, values[c] ?? string.Empty);
                    onCellEdited?.Invoke(cell);
                }
            }

            if (firstRow != null && firstColumn != null)
                SelectCell(state, firstRow.id, firstColumn.id, false);

            sheet.Touch();
            state.InvalidateCache();
        }

        private static void ClearSelection(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            RefreshCache(sheet, state);
            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out int columnMax);
            if (rowMin < 0 || columnMin < 0)
                return;

            bool changed = false;
            for (int r = rowMin; r <= rowMax; r++)
            {
                for (int c = columnMin; c <= columnMax; c++)
                {
                    PungentDataSheetGridRowView row = state.cachedRows[r];
                    PungentDataSheetGridColumnView column = state.cachedColumns[c];
                    if (row.isVirtual || column.isVirtual)
                        continue;

                    PungentDataSheetCell cell = sheet.FindCell(row.Id, column.Id);
                    if (cell == null || string.IsNullOrEmpty(cell.rawValue))
                        continue;

                    SetCellValue(cell, string.Empty);
                    onCellEdited?.Invoke(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                sheet.Touch();
                state.InvalidateCache();
            }
        }

        private static void ShowCellMenu(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy"), false, () => EditorGUIUtility.systemCopyBuffer = BuildClipboard(sheet, state));
            menu.AddItem(new GUIContent("Paste"), false, () => PasteClipboard(sheet, state, EditorGUIUtility.systemCopyBuffer, onCellEdited));
            menu.AddItem(new GUIContent("Clear"), false, () => ClearSelection(sheet, state, onCellEdited));
            menu.AddSeparator(string.Empty);
            if (CanFillDown(state))
                menu.AddItem(new GUIContent("Fill Down"), false, () => FillDown(sheet, state, onCellEdited));
            else
                menu.AddDisabledItem(new GUIContent("Fill Down"));
            if (CanFillRight(state))
                menu.AddItem(new GUIContent("Fill Right"), false, () => FillRight(sheet, state, onCellEdited));
            else
                menu.AddDisabledItem(new GUIContent("Fill Right"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Add Row Above"), false, () => { InsertRowBefore(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Add Row Below"), false, () => { InsertRowAfter(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Add Column Left"), false, () => { InsertColumnBefore(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Add Column Right"), false, () => { InsertColumnAfter(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.ShowAsContext();
        }

        private static void ShowRowMenu(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, Action<PungentDataSheetCell> onCellEdited)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Row Above"), false, () => { InsertRowBefore(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Add Row Below"), false, () => { InsertRowAfter(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            if (row.isVirtual)
                menu.AddDisabledItem(new GUIContent("Duplicate Row"));
            else
                menu.AddItem(new GUIContent("Duplicate Row"), false, () => { DuplicateRow(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Clear Row"), false, () => { ClearRow(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            if (row.isVirtual)
                menu.AddDisabledItem(new GUIContent("Hide Row"));
            else
                menu.AddItem(new GUIContent("Hide Row"), false, () => { HideRow(sheet, row); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            if (HasHiddenRows(sheet))
                menu.AddItem(new GUIContent("Unhide All Rows"), false, () => { UnhideAllRows(sheet); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            else
                menu.AddDisabledItem(new GUIContent("Unhide All Rows"));
            menu.AddSeparator(string.Empty);
            if (row.isVirtual)
                menu.AddDisabledItem(new GUIContent("Delete Row"));
            else
            {
                menu.AddItem(new GUIContent("Delete Row"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Row", "Delete row '" + DisplayRow(row) + "' and its cells?", "Delete", "Cancel"))
                    {
                        sheet.DeleteRow(row.Id);
                        onCellEdited?.Invoke(null);
                    }
                    state.InvalidateCache();
                });
            }
            menu.ShowAsContext();
        }

        private static void ShowColumnMenu(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename Column"), false, () => BeginColumnRename(sheet, state, column, onCellEdited));
            menu.AddItem(new GUIContent("Add Column Left"), false, () => { InsertColumnBefore(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Add Column Right"), false, () => { InsertColumnAfter(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddSeparator(string.Empty);
            AddColumnTypeMenuItems(menu, sheet, state, column, onCellEdited);
            if (column != null && column.DataType == PungentDataSheetDataType.EnumText && !column.isVirtual)
                menu.AddItem(new GUIContent("Set Data Type/Harvest Enum Options From Column"), false, () => { HarvestEnumOptions(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddSeparator(string.Empty);
            if (column.isVirtual)
                menu.AddDisabledItem(new GUIContent("Duplicate Column"));
            else
                menu.AddItem(new GUIContent("Duplicate Column"), false, () => { DuplicateColumn(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Clear Column"), false, () => { ClearColumn(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            if (column.isVirtual)
                menu.AddDisabledItem(new GUIContent("Hide Column"));
            else
                menu.AddItem(new GUIContent("Hide Column"), false, () => { HideColumn(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            if (HasHiddenColumns(sheet))
                menu.AddItem(new GUIContent("Unhide All Columns"), false, () => { UnhideAllColumns(sheet); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            else
                menu.AddDisabledItem(new GUIContent("Unhide All Columns"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Sort Ascending"), false, () => { bool wasVirtual = column.isVirtual; PungentDataSheetColumn materialized = MaterializeColumn(sheet, column); state.sortColumnId = materialized.id; state.sortDescending = false; state.InvalidateCache(); if (wasVirtual) onCellEdited?.Invoke(null); });
            menu.AddItem(new GUIContent("Sort Descending"), false, () => { bool wasVirtual = column.isVirtual; PungentDataSheetColumn materialized = MaterializeColumn(sheet, column); state.sortColumnId = materialized.id; state.sortDescending = true; state.InvalidateCache(); if (wasVirtual) onCellEdited?.Invoke(null); });
            menu.AddSeparator(string.Empty);
            if (column.isVirtual)
                menu.AddDisabledItem(new GUIContent("Delete Column"));
            else
            {
                menu.AddItem(new GUIContent("Delete Column"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete Column", "Delete column '" + column.DisplayName + "' and its cells?", "Delete", "Cancel"))
                    {
                        sheet.DeleteColumn(column.Id);
                        onCellEdited?.Invoke(null);
                    }
                    state.InvalidateCache();
                });
            }
            menu.ShowAsContext();
        }

        private static void ShowColumnTypeMenu(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            GenericMenu menu = new GenericMenu();
            AddColumnTypeMenuItems(menu, sheet, state, column, onCellEdited, string.Empty);
            if (column != null && column.DataType == PungentDataSheetDataType.EnumText && !column.isVirtual)
                menu.AddItem(new GUIContent("Harvest Enum Options From Column"), false, () => { HarvestEnumOptions(sheet, column); state.InvalidateCache(); onCellEdited?.Invoke(null); });
            menu.ShowAsContext();
        }

        private static void AddColumnTypeMenuItems(GenericMenu menu, PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited)
        {
            AddColumnTypeMenuItems(menu, sheet, state, column, onCellEdited, "Set Data Type/");
        }

        private static void AddColumnTypeMenuItems(GenericMenu menu, PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, Action<PungentDataSheetCell> onCellEdited, string prefix)
        {
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.Text, onCellEdited, prefix);
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.Number, onCellEdited, prefix);
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.Boolean, onCellEdited, prefix);
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.Date, onCellEdited, prefix);
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.EnumText, onCellEdited, prefix);
            AddColumnTypeMenuItem(menu, sheet, state, column, PungentDataSheetDataType.AuthoringReference, onCellEdited, prefix);
        }

        private static void AddColumnTypeMenuItem(GenericMenu menu, PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column, PungentDataSheetDataType dataType, Action<PungentDataSheetCell> onCellEdited, string prefix)
        {
            menu.AddItem(new GUIContent((prefix ?? string.Empty) + dataType), column != null && column.DataType == dataType, () =>
            {
                PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
                if (materialized == null || materialized.dataType == dataType)
                    return;

                materialized.dataType = dataType;
                materialized.NormalizeInPlace();
                sheet.Touch();
                state.InvalidateCache();
                onCellEdited?.Invoke(null);
            });
        }

        private static void HarvestEnumOptions(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            if (sheet == null || column == null || column.column == null)
                return;

            List<string> options = PungentAuthoringMetadata.NormalizeTags(column.column.enumOptions);
            foreach (PungentDataSheetCell cell in sheet.cells ?? new List<PungentDataSheetCell>())
            {
                if (cell == null || !PungentAuthoringId.EqualsId(cell.columnId, column.Id))
                    continue;
                AddDistinct(options, cell.rawValue);
            }

            column.column.enumOptions = options;
            column.column.NormalizeInPlace();
            sheet.Touch();
        }

        private static void InsertRowBefore(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            PungentDataSheetRow materialized = MaterializeRow(sheet, row);
            int index = sheet.rows.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, materialized.id));
            PungentDataSheetRow created = PungentDataSheetRow.Create("Row " + (sheet.rows.Count + 1).ToString(CultureInfo.InvariantCulture), index);
            sheet.rows.Insert(Mathf.Clamp(index, 0, sheet.rows.Count), created);
            sheet.ReindexRows();
            sheet.Touch();
        }

        private static void InsertRowAfter(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            PungentDataSheetRow materialized = MaterializeRow(sheet, row);
            int index = sheet.rows.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, materialized.id));
            PungentDataSheetRow created = PungentDataSheetRow.Create("Row " + (sheet.rows.Count + 1).ToString(CultureInfo.InvariantCulture), index + 1);
            sheet.rows.Insert(Mathf.Clamp(index + 1, 0, sheet.rows.Count), created);
            sheet.ReindexRows();
            sheet.Touch();
        }

        private static void InsertColumnBefore(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
            int index = sheet.columns.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, materialized.id));
            PungentDataSheetColumn created = PungentDataSheetColumn.Create(ColumnLabel(sheet.columns.Count), PungentDataSheetDataType.Text);
            sheet.columns.Insert(Mathf.Clamp(index, 0, sheet.columns.Count), created);
            sheet.Touch();
        }

        private static void InsertColumnAfter(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            PungentDataSheetColumn materialized = MaterializeColumn(sheet, column);
            int index = sheet.columns.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, materialized.id));
            PungentDataSheetColumn created = PungentDataSheetColumn.Create(ColumnLabel(sheet.columns.Count), PungentDataSheetDataType.Text);
            sheet.columns.Insert(Mathf.Clamp(index + 1, 0, sheet.columns.Count), created);
            sheet.Touch();
        }

        private static void DuplicateRow(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            if (row == null || row.isVirtual)
                return;

            int index = sheet.rows.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, row.Id));
            PungentDataSheetRow copy = PungentDataSheetRow.Create(DisplayRow(row) + " Copy", index + 1);
            sheet.rows.Insert(Mathf.Clamp(index + 1, 0, sheet.rows.Count), copy);
            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                PungentDataSheetCell source = sheet.FindCell(row.Id, column.id);
                if (source == null)
                    continue;

                PungentDataSheetCell cell = sheet.GetOrCreateCell(copy.id, column.id);
                SetCellValue(cell, source.rawValue);
            }
            sheet.ReindexRows();
            sheet.Touch();
        }

        private static void DuplicateColumn(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            if (column == null || column.isVirtual || column.column == null)
                return;

            int index = sheet.columns.FindIndex(item => item != null && PungentAuthoringId.EqualsId(item.id, column.Id));
            PungentDataSheetColumn copy = PungentDataSheetColumn.Create(column.DisplayName + " Copy", column.DataType);
            copy.width = column.Width;
            copy.enumOptions = PungentAuthoringMetadata.NormalizeTags(column.EnumOptions);
            sheet.columns.Insert(Mathf.Clamp(index + 1, 0, sheet.columns.Count), copy);
            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
            {
                PungentDataSheetCell source = sheet.FindCell(row.id, column.Id);
                if (source == null)
                    continue;

                PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, copy.id);
                SetCellValue(cell, source.rawValue);
            }
            sheet.Touch();
        }

        private static void ClearRow(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            if (row == null || row.isVirtual)
                return;

            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
            {
                PungentDataSheetCell cell = sheet.FindCell(row.Id, column.id);
                if (cell != null)
                    SetCellValue(cell, string.Empty);
            }
            sheet.Touch();
        }

        private static void ClearColumn(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            if (column == null || column.isVirtual)
                return;

            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
            {
                PungentDataSheetCell cell = sheet.FindCell(row.id, column.Id);
                if (cell != null)
                    SetCellValue(cell, string.Empty);
            }
            sheet.Touch();
        }

        private static void HideRow(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            if (sheet == null || row == null || row.isVirtual || row.row == null)
                return;

            row.row.hidden = true;
            sheet.Touch();
        }

        private static void HideColumn(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            if (sheet == null || column == null || column.isVirtual || column.column == null)
                return;

            column.column.hidden = true;
            sheet.Touch();
        }

        private static bool HasHiddenRows(PungentDataSheet sheet)
        {
            return sheet != null && (sheet.rows ?? new List<PungentDataSheetRow>()).Any(row => row != null && row.hidden);
        }

        private static bool HasHiddenColumns(PungentDataSheet sheet)
        {
            return sheet != null && (sheet.columns ?? new List<PungentDataSheetColumn>()).Any(column => column != null && column.hidden);
        }

        private static void UnhideAllRows(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            foreach (PungentDataSheetRow row in sheet.rows ?? new List<PungentDataSheetRow>())
                if (row != null)
                    row.hidden = false;
            sheet.Touch();
        }

        private static void UnhideAllColumns(PungentDataSheet sheet)
        {
            if (sheet == null)
                return;

            foreach (PungentDataSheetColumn column in sheet.columns ?? new List<PungentDataSheetColumn>())
                if (column != null)
                    column.hidden = false;
            sheet.Touch();
        }

        private static void FillDown(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            RefreshCache(sheet, state);
            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out int columnMax);
            if (rowMin < 0 || rowMin == rowMax)
                return;

            bool changed = false;
            for (int c = columnMin; c <= columnMax; c++)
            {
                string value = CellRawValue(state.cachedCells, state.cachedRows[rowMin].Id, state.cachedColumns[c].Id);
                for (int r = rowMin + 1; r <= rowMax; r++)
                {
                    PungentDataSheetRow row = MaterializeRow(sheet, state.cachedRows[r]);
                    PungentDataSheetColumn column = MaterializeColumn(sheet, state.cachedColumns[c]);
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
                    string before = cell.rawValue ?? string.Empty;
                    SetCellValue(cell, value);
                    if (!string.Equals(before, value, StringComparison.Ordinal))
                    {
                        changed = true;
                        onCellEdited?.Invoke(cell);
                    }
                }
            }
            if (changed)
            {
                sheet.Touch();
                state.InvalidateCache();
            }
        }

        private static void FillRight(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            RefreshCache(sheet, state);
            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out int columnMax);
            if (columnMin < 0 || columnMin == columnMax)
                return;

            bool changed = false;
            for (int r = rowMin; r <= rowMax; r++)
            {
                string value = CellRawValue(state.cachedCells, state.cachedRows[r].Id, state.cachedColumns[columnMin].Id);
                for (int c = columnMin + 1; c <= columnMax; c++)
                {
                    PungentDataSheetRow row = MaterializeRow(sheet, state.cachedRows[r]);
                    PungentDataSheetColumn column = MaterializeColumn(sheet, state.cachedColumns[c]);
                    PungentDataSheetCell cell = sheet.GetOrCreateCell(row.id, column.id);
                    string before = cell.rawValue ?? string.Empty;
                    SetCellValue(cell, value);
                    if (!string.Equals(before, value, StringComparison.Ordinal))
                    {
                        changed = true;
                        onCellEdited?.Invoke(cell);
                    }
                }
            }
            if (changed)
            {
                sheet.Touch();
                state.InvalidateCache();
            }
        }

        private static PungentDataSheetRow MaterializeRow(PungentDataSheet sheet, PungentDataSheetGridRowView row)
        {
            if (row == null)
                return null;
            if (!row.isVirtual && row.row != null)
                return row.row;

            return EnsureRowAt(sheet, row.modelIndex);
        }

        private static PungentDataSheetColumn MaterializeColumn(PungentDataSheet sheet, PungentDataSheetGridColumnView column)
        {
            if (column == null)
                return null;
            if (!column.isVirtual && column.column != null)
                return column.column;

            return EnsureColumnAt(sheet, column.modelIndex);
        }

        private static PungentDataSheetRow EnsureRowAt(PungentDataSheet sheet, int modelIndex)
        {
            if (sheet.rows == null)
                sheet.rows = new List<PungentDataSheetRow>();

            modelIndex = Mathf.Max(0, modelIndex);
            while (sheet.rows.Count <= modelIndex)
                sheet.rows.Add(PungentDataSheetRow.Create("Row " + (sheet.rows.Count + 1).ToString(CultureInfo.InvariantCulture), sheet.rows.Count));

            sheet.ReindexRows();
            sheet.Touch();
            return sheet.rows[modelIndex];
        }

        private static PungentDataSheetColumn EnsureColumnAt(PungentDataSheet sheet, int modelIndex)
        {
            if (sheet.columns == null)
                sheet.columns = new List<PungentDataSheetColumn>();

            modelIndex = Mathf.Max(0, modelIndex);
            while (sheet.columns.Count <= modelIndex)
            {
                int index = sheet.columns.Count;
                PungentDataSheetColumn column = PungentDataSheetColumn.Create(ColumnLabel(index), PungentDataSheetDataType.Text);
                column.width = DefaultColumnWidth;
                sheet.columns.Add(column);
            }

            sheet.Touch();
            return sheet.columns[modelIndex];
        }

        private static void SetCellValue(PungentDataSheetCell cell, string value)
        {
            if (cell == null)
                return;

            cell.rawValue = value ?? string.Empty;
            cell.displayValue = cell.rawValue;
            cell.validationStatus = PungentDataSheetCellValidationStatus.NotRun;
            cell.propagationStatus = string.Equals(cell.rawValue, cell.lastPulledValue, StringComparison.Ordinal)
                ? PungentDataSheetPropagationStatus.Clean
                : PungentDataSheetPropagationStatus.Changed;
            cell.propagationMessage = string.Empty;
        }

        private static bool CanFillDown(PungentDataSheetGridState state)
        {
            if (state == null || !state.HasCellSelection)
                return false;

            GetSelectedRange(state, out int rowMin, out int rowMax, out int columnMin, out _);
            return rowMin >= 0 && columnMin >= 0 && rowMax > rowMin;
        }

        private static bool CanFillRight(PungentDataSheetGridState state)
        {
            if (state == null || !state.HasCellSelection)
                return false;

            GetSelectedRange(state, out int rowMin, out _, out int columnMin, out int columnMax);
            return rowMin >= 0 && columnMin >= 0 && columnMax > columnMin;
        }

        private static int RowIndex(PungentDataSheetGridState state, string rowId)
        {
            return state.cachedRows.FindIndex(row => row != null && PungentAuthoringId.EqualsId(row.Id, rowId));
        }

        private static int ColumnIndex(PungentDataSheetGridState state, string columnId)
        {
            return state.cachedColumns.FindIndex(column => column != null && PungentAuthoringId.EqualsId(column.Id, columnId));
        }

        private static PungentDataSheetGridRowView FindRowView(PungentDataSheetGridState state, string rowId)
        {
            return state.cachedRows.Find(row => row != null && PungentAuthoringId.EqualsId(row.Id, rowId));
        }

        private static PungentDataSheetGridColumnView FindColumnView(PungentDataSheetGridState state, string columnId)
        {
            return state.cachedColumns.Find(column => column != null && PungentAuthoringId.EqualsId(column.Id, columnId));
        }

        private static int RowHitIndexAtY(PungentDataSheetGridState state, float y)
        {
            return Mathf.Clamp(Mathf.FloorToInt((y - HeaderHeight) / RowHeight), 0, Mathf.Max(0, state.cachedRows.Count - 1));
        }

        private static int RowInsertionIndexAtY(PungentDataSheetGridState state, float y)
        {
            if (state.cachedRows.Count == 0)
                return 0;

            float local = y - HeaderHeight;
            for (int i = 0; i < state.cachedRows.Count; i++)
            {
                float midpoint = i * RowHeight + RowHeight * 0.5f;
                if (local < midpoint)
                    return i;
            }

            return state.cachedRows.Count;
        }

        private static int ColumnHitIndexAtX(PungentDataSheetGridState state, float x)
        {
            float cursor = RowHeaderWidth;
            for (int i = 0; i < state.cachedColumns.Count; i++)
            {
                float next = cursor + state.cachedColumns[i].Width;
                if (x < next)
                    return i;
                cursor = next;
            }

            return Mathf.Max(0, state.cachedColumns.Count - 1);
        }

        private static int ColumnInsertionIndexAtX(PungentDataSheetGridState state, float x)
        {
            float cursor = RowHeaderWidth;
            for (int i = 0; i < state.cachedColumns.Count; i++)
            {
                float width = state.cachedColumns[i].Width;
                if (x < cursor + width * 0.5f)
                    return i;
                cursor += width;
            }

            return state.cachedColumns.Count;
        }

        private static float ColumnXForIndex(PungentDataSheetGridState state, int index)
        {
            float x = RowHeaderWidth;
            index = Mathf.Clamp(index, 0, state.cachedColumns.Count);
            for (int i = 0; i < index && i < state.cachedColumns.Count; i++)
                x += state.cachedColumns[i].Width;
            return x;
        }

        private static float TotalColumnsWidth(PungentDataSheetGridState state)
        {
            float width = 0f;
            for (int i = 0; i < state.cachedColumns.Count; i++)
                width += state.cachedColumns[i].Width;
            return width;
        }

        private static int materializedIndex(List<PungentDataSheetColumn> columns, string columnId)
        {
            return columns == null ? -1 : columns.FindIndex(column => column != null && PungentAuthoringId.EqualsId(column.id, columnId));
        }

        private static int materializedIndex(List<PungentDataSheetRow> rows, string rowId)
        {
            return rows == null ? -1 : rows.FindIndex(row => row != null && PungentAuthoringId.EqualsId(row.id, rowId));
        }

        private static bool RowMatchesFilter(PungentDataSheetRow row, List<PungentDataSheetGridColumnView> columns, Dictionary<string, PungentDataSheetCell> cells, string filter)
        {
            if (row == null)
                return false;

            if (Contains(row.displayName, filter) || Contains(row.id, filter))
                return true;

            foreach (string tag in row.tags ?? new List<string>())
                if (Contains(tag, filter))
                    return true;

            for (int c = 0; c < columns.Count; c++)
            {
                PungentDataSheetGridColumnView column = columns[c];
                if (column == null || column.isVirtual)
                    continue;

                cells.TryGetValue(CellKey(row.id, column.Id), out PungentDataSheetCell cell);
                if (cell != null && (Contains(cell.rawValue, filter) || Contains(cell.displayValue, filter)))
                    return true;
            }

            return false;
        }

        private static IComparable SortValue(string value, PungentDataSheetDataType dataType, PungentDataSheetGridState state)
        {
            value = value ?? string.Empty;
            switch (dataType)
            {
                case PungentDataSheetDataType.Number:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                        return number;
                    return double.MinValue;
                case PungentDataSheetDataType.Boolean:
                    return IsTruthy(value);
                case PungentDataSheetDataType.Date:
                    if (TryParseDateValue(value, state, out DateTime date))
                        return date;
                    return DateTime.MinValue;
                default:
                    return value;
            }
        }

        private static void ToggleSort(PungentDataSheetGridState state, string columnId)
        {
            if (PungentAuthoringId.EqualsId(state.sortColumnId, columnId))
                state.sortDescending = !state.sortDescending;
            else
            {
                state.sortColumnId = columnId;
                state.sortDescending = false;
            }
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseDateValue(string value, PungentDataSheetGridState state, out DateTime date)
        {
            return TryParseDateValue(value, DateFormat(state), out date);
        }

        private static bool TryParseDateValue(string value, string format, out DateTime date)
        {
            date = DateTime.MinValue;
            string text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string preferred = string.IsNullOrWhiteSpace(format) ? "dd/MM/yyyy" : format.Trim();
            string[] exactFormats =
            {
                preferred,
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "MM/dd/yyyy",
                "M/d/yyyy",
                "o"
            };

            if (DateTime.TryParseExact(text, exactFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date))
                return true;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        private static string DateFormat(PungentDataSheetGridState state)
        {
            return state == null || string.IsNullOrWhiteSpace(state.dateDisplayFormat) ? "dd/MM/yyyy" : state.dateDisplayFormat;
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CurrentCellValue(PungentDataSheetGridState state, string rowId, string columnId)
        {
            if (state.IsEditingCell &&
                PungentAuthoringId.EqualsId(state.editingCellRowId, rowId) &&
                PungentAuthoringId.EqualsId(state.editingCellColumnId, columnId))
                return state.editBuffer ?? string.Empty;

            return CellRawValue(state.cachedCells, rowId, columnId);
        }

        private static string CellRawValue(Dictionary<string, PungentDataSheetCell> cells, string rowId, string columnId)
        {
            return cells.TryGetValue(CellKey(rowId, columnId), out PungentDataSheetCell cell) && cell != null
                ? cell.rawValue ?? string.Empty
                : string.Empty;
        }

        private static string DisplayCellValue(string rawValue, PungentDataSheetGridColumnView column)
        {
            return DisplayCellValue(rawValue, column, null);
        }

        private static string DisplayCellValue(string rawValue, PungentDataSheetGridColumnView column, PungentDataSheetGridState state)
        {
            rawValue = rawValue ?? string.Empty;
            if (column != null && column.DataType == PungentDataSheetDataType.Boolean && !string.IsNullOrWhiteSpace(rawValue))
                return IsTruthy(rawValue) ? "TRUE" : "FALSE";
            if (column != null && column.DataType == PungentDataSheetDataType.Date && !string.IsNullOrWhiteSpace(rawValue) &&
                TryParseDateValue(rawValue, state, out DateTime date))
                return date.ToString(string.IsNullOrWhiteSpace(state?.dateDisplayFormat) ? "dd/MM/yyyy" : state.dateDisplayFormat, CultureInfo.InvariantCulture);
            return rawValue ?? string.Empty;
        }

        private static string PlaceholderForColumn(PungentDataSheetGridColumnView column, PungentDataSheetGridState state)
        {
            if (column == null)
                return string.Empty;

            switch (column.DataType)
            {
                case PungentDataSheetDataType.Number:
                    return "number";
                case PungentDataSheetDataType.Boolean:
                    return "true/false";
                case PungentDataSheetDataType.Date:
                    return string.IsNullOrWhiteSpace(state?.dateDisplayFormat) ? "dd/MM/yyyy" : state.dateDisplayFormat;
                case PungentDataSheetDataType.EnumText:
                    return "enum";
                case PungentDataSheetDataType.AuthoringReference:
                    return "authoring ref";
                default:
                    return string.Empty;
            }
        }

        private static string DrawCellEditField(Rect rect, string value, PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridRowView row, PungentDataSheetGridColumnView column)
        {
            if (column == null)
                return EditorGUI.TextField(rect, value ?? string.Empty, _cellEditStyle);

            switch (column.DataType)
            {
                case PungentDataSheetDataType.Number:
                    return SanitizeNumberInput(EditorGUI.TextField(rect, value ?? string.Empty, _cellEditStyle));
                case PungentDataSheetDataType.Boolean:
                    bool nextBool = EditorGUI.Toggle(rect, IsTruthy(value));
                    return nextBool ? "true" : "false";
                case PungentDataSheetDataType.Date:
                    string dateValue = EditorGUI.TextField(rect, value ?? string.Empty, _cellEditStyle);
                    return dateValue;
                case PungentDataSheetDataType.EnumText:
                    List<string> options = EnumOptionsForColumn(sheet, state, column);
                    string enumValue = EditorGUI.TextField(rect, value ?? string.Empty, _cellEditStyle);
                    PrepareEnumSuggestions(state, row, column, rect, enumValue, options);
                    return enumValue;
                default:
                    return EditorGUI.TextField(rect, value ?? string.Empty, _cellEditStyle);
            }
        }

        private static void PrepareEnumSuggestions(
            PungentDataSheetGridState state,
            PungentDataSheetGridRowView row,
            PungentDataSheetGridColumnView column,
            Rect editRect,
            string value,
            List<string> options)
        {
            if (state == null || row == null || column == null || options == null || options.Count == 0)
                return;

            state.enumSuggestions.Clear();
            string filter = (value ?? string.Empty).Trim();
            for (int i = 0; i < options.Count; i++)
            {
                string option = options[i];
                if (string.IsNullOrWhiteSpace(option))
                    continue;
                if (!string.IsNullOrWhiteSpace(filter) &&
                    option.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    !option.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                AddDistinct(state.enumSuggestions, option);
                if (state.enumSuggestions.Count >= 8)
                    break;
            }

            if (state.enumSuggestions.Count == 0)
                return;

            state.enumSuggestionRowId = row.Id;
            state.enumSuggestionColumnId = column.Id;
            state.enumSuggestionRect = new Rect(editRect.x, editRect.yMax + 2f, Mathf.Max(editRect.width, 150f), state.enumSuggestions.Count * 20f + 6f);
            state.enumSuggestionActive = true;
            state.enumSuggestionUpdatedThisFrame = true;
        }

        private static void DrawEnumSuggestionOverlay(PungentDataSheet sheet, PungentDataSheetGridState state, Action<PungentDataSheetCell> onCellEdited)
        {
            if (state == null)
                return;

            if (!state.enumSuggestionUpdatedThisFrame ||
                !state.IsEditingCell ||
                !PungentAuthoringId.EqualsId(state.editingCellRowId, state.enumSuggestionRowId) ||
                !PungentAuthoringId.EqualsId(state.editingCellColumnId, state.enumSuggestionColumnId))
            {
                ClearEnumSuggestions(state);
                return;
            }

            Rect rect = state.enumSuggestionRect;
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            for (int i = 0; i < state.enumSuggestions.Count; i++)
            {
                string option = state.enumSuggestions[i];
                Rect optionRect = new Rect(rect.x + 3f, rect.y + 3f + i * 20f, rect.width - 6f, 19f);
                if (GUI.Button(optionRect, new GUIContent(option, "Apply enum value '" + option + "'."), EditorStyles.miniButton))
                {
                    state.editBuffer = option;
                    CommitCellEdit(sheet, state, onCellEdited, 0, 0);
                    ClearEnumSuggestions(state);
                    Event.current.Use();
                    return;
                }
            }
        }

        private static void HideEnumSuggestions(PungentDataSheetGridState state)
        {
            if (state == null)
                return;

            state.enumSuggestionUpdatedThisFrame = false;
            ClearEnumSuggestions(state);
        }

        private static void ClearEnumSuggestions(PungentDataSheetGridState state)
        {
            if (state == null)
                return;

            state.enumSuggestionActive = false;
            state.enumSuggestionRowId = string.Empty;
            state.enumSuggestionColumnId = string.Empty;
            state.enumSuggestionRect = Rect.zero;
            state.enumSuggestions.Clear();
        }

        private static List<string> EnumOptionsForColumn(PungentDataSheet sheet, PungentDataSheetGridState state, PungentDataSheetGridColumnView column)
        {
            List<string> options = new List<string>();
            if (column == null)
                return options;

            foreach (string option in column.EnumOptions ?? new List<string>())
                AddDistinct(options, option);

            if (state != null)
            {
                foreach (PungentDataSheetGridRowView row in state.cachedRows)
                {
                    if (row == null || row.isVirtual)
                        continue;

                    string value = CellRawValue(state.cachedCells, row.Id, column.Id);
                    AddDistinct(options, value);
                }
            }

            return options;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            value = value == null ? string.Empty : value.Trim();
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (!values.Exists(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
                values.Add(value);
        }

        private static string InitialTypedValueForColumn(string typed, PungentDataSheetGridColumnView column)
        {
            if (column == null || column.DataType != PungentDataSheetDataType.Number)
                return typed ?? string.Empty;

            return IsPotentialNumberInput(typed) ? typed : string.Empty;
        }

        private static string NormalizeInputForColumn(string value, PungentDataSheetGridColumnView column)
        {
            value = value ?? string.Empty;
            if (value.StartsWith("=", StringComparison.Ordinal))
                return value;

            if (column != null && column.DataType == PungentDataSheetDataType.Number)
                return SanitizeNumberInput(value);

            return value;
        }

        private static string SanitizeNumberInput(string value)
        {
            value = value ?? string.Empty;
            StringBuilder builder = new StringBuilder(value.Length);
            bool hasDecimal = false;
            bool hasExponent = false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsDigit(ch))
                {
                    builder.Append(ch);
                }
                else if ((ch == '-' || ch == '+') && (builder.Length == 0 || EndsWithExponent(builder)))
                {
                    builder.Append(ch);
                }
                else if (ch == '.' && !hasDecimal && !hasExponent)
                {
                    hasDecimal = true;
                    builder.Append(ch);
                }
                else if ((ch == 'e' || ch == 'E') && !hasExponent && builder.Length > 0)
                {
                    hasExponent = true;
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static bool EndsWithExponent(StringBuilder builder)
        {
            if (builder == null || builder.Length == 0)
                return false;
            char last = builder[builder.Length - 1];
            return last == 'e' || last == 'E';
        }

        private static bool IsPotentialNumberInput(string value)
        {
            return !string.IsNullOrEmpty(SanitizeNumberInput(value));
        }

        private static string InvalidTypeMessage(string rawValue, PungentDataSheetGridColumnView column, PungentDataSheetGridState state)
        {
            rawValue = rawValue ?? string.Empty;
            if (column == null || string.IsNullOrWhiteSpace(rawValue))
                return string.Empty;

            switch (column.DataType)
            {
                case PungentDataSheetDataType.Number:
                    return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? string.Empty : "invalid number";
                case PungentDataSheetDataType.Boolean:
                    return IsBooleanLiteral(rawValue) ? string.Empty : "invalid boolean";
                case PungentDataSheetDataType.Date:
                    return TryParseDateValue(rawValue, state, out _) ? string.Empty : "invalid date - " + DateFormat(state);
                case PungentDataSheetDataType.EnumText:
                    if (column.EnumOptions != null && column.EnumOptions.Count > 0 &&
                        !column.EnumOptions.Exists(option => string.Equals(option, rawValue, StringComparison.OrdinalIgnoreCase)))
                        return "invalid enum";
                    return string.Empty;
                case PungentDataSheetDataType.AuthoringReference:
                    return PungentDataSheetAuthoringReferenceCodec.TryParseStructured(rawValue, out _) ? string.Empty : "invalid ref";
                default:
                    return string.Empty;
            }
        }

        private static bool IsBooleanLiteral(string value)
        {
            string clean = (value ?? string.Empty).Trim();
            return string.Equals(clean, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "false", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "no", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(clean, "0", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasLinkedReference(PungentDataSheetCell cell)
        {
            return cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId;
        }

        private static bool HasMissingReferenceProvider(PungentDataSheetCell cell, string rawValue)
        {
            PungentAuthoringReference reference = null;
            if (cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId)
                reference = cell.linkedAuthoringRef;
            else
                PungentDataSheetAuthoringReferenceCodec.TryParseStructured(rawValue, out reference);

            if (reference == null || !reference.HasItemId)
                return false;

            if (!string.IsNullOrWhiteSpace(reference.providerId))
                return PungentAuthoringProviderRegistry.FindProvider(reference.providerId) == null;

            return PungentAuthoringProviderRegistry.GetProvidersForKind(reference.itemKind).Count == 0;
        }

        private static string LinkedDisplayValue(PungentDataSheetCell cell, string rawValue)
        {
            if (cell != null && cell.linkedAuthoringRef != null && cell.linkedAuthoringRef.HasItemId)
                return string.IsNullOrWhiteSpace(cell.linkedAuthoringRef.label) ? "ref: " + cell.linkedAuthoringRef.itemId : "ref: " + cell.linkedAuthoringRef.label;
            if (PungentDataSheetAuthoringReferenceCodec.TryParseStructured(rawValue, out PungentAuthoringReference reference) && reference != null)
                return string.IsNullOrWhiteSpace(reference.label) ? "ref: " + reference.itemId : "ref: " + reference.label;
            return "ref";
        }

        private static string CellTooltip(PungentDataSheetCell cell, PungentDataSheetGridColumnView column, string rawValue, string invalidTypeMessage, bool missingReference)
        {
            if (!string.IsNullOrWhiteSpace(invalidTypeMessage))
                return "Invalid type: this " + (column != null ? column.DataType.ToString() : "cell") + " cell does not accept '" + rawValue + "'.";
            if (missingReference)
                return "Linked reference provider is missing. The reference is preserved.";
            if (HasLinkedReference(cell))
                return "Linked reference: " + (string.IsNullOrWhiteSpace(cell.linkedAuthoringRef.label) ? cell.linkedAuthoringRef.itemId : cell.linkedAuthoringRef.label);
            return column != null ? column.DataType.ToString() : string.Empty;
        }

        private static void DrawTypeStripe(Rect rect, PungentDataSheetGridColumnView column)
        {
            Color color = TypeColor(column);
            if (color.a <= 0f)
                return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 1f, 3f, rect.height - 2f), color);
        }

        private static void DrawHeaderTypeBadge(Rect rect, PungentDataSheetGridColumnView column)
        {
            if (rect.width < 16f || column == null)
                return;

            Color color = TypeColor(column);
            if (color.a <= 0f)
                color = TextTypeTint;

            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, new GUIContent(TypeBadgeLabel(column), column.DataType.ToString()), _typeBadgeStyle);
        }

        private static string TypeBadgeLabel(PungentDataSheetGridColumnView column)
        {
            if (column == null)
                return string.Empty;

            switch (column.DataType)
            {
                case PungentDataSheetDataType.Number: return "num";
                case PungentDataSheetDataType.Boolean: return "bool";
                case PungentDataSheetDataType.Date: return "date";
                case PungentDataSheetDataType.EnumText: return "enum";
                case PungentDataSheetDataType.AuthoringReference: return "ref";
                default: return "txt";
            }
        }

        private static Color TypeColor(PungentDataSheetGridColumnView column)
        {
            if (column == null)
                return Color.clear;

            switch (column.DataType)
            {
                case PungentDataSheetDataType.Number: return NumberTypeTint;
                case PungentDataSheetDataType.Boolean: return BooleanTypeTint;
                case PungentDataSheetDataType.Date: return DateTypeTint;
                case PungentDataSheetDataType.EnumText: return EnumTypeTint;
                case PungentDataSheetDataType.AuthoringReference: return ReferenceTypeTint;
                default: return Color.clear;
            }
        }

        private static bool IsEditingCell(PungentDataSheetGridState state, string rowId, string columnId)
        {
            return state.IsEditingCell &&
                   PungentAuthoringId.EqualsId(state.editingCellRowId, rowId) &&
                   PungentAuthoringId.EqualsId(state.editingCellColumnId, columnId);
        }

        private static bool IsRenamingColumn(PungentDataSheetGridState state, PungentDataSheetGridColumnView column)
        {
            return state.interaction == PungentDataSheetGridInteraction.RenamingColumn &&
                   column != null &&
                   PungentAuthoringId.EqualsId(state.renamingColumnId, column.Id);
        }

        private static string PrintableText(Event evt)
        {
            if (evt == null || evt.character == '\0' || char.IsControl(evt.character))
                return string.Empty;

            return evt.character.ToString();
        }

        private static string CellKey(string rowId, string columnId)
        {
            return PungentAuthoringId.Normalize(rowId) + "\u001F" + PungentAuthoringId.Normalize(columnId);
        }

        private static string DisplayRow(PungentDataSheetGridRowView row)
        {
            return row == null || string.IsNullOrWhiteSpace(row.DisplayName) ? "Row" : row.DisplayName;
        }

        private static void ClearGuiFocus()
        {
            if (Event.current != null)
                GUI.FocusControl(null);
        }

        private static string ColumnHeaderLabel(PungentDataSheetGridColumnView column)
        {
            string coordinate = ColumnLabel(column.visibleIndex);
            string display = column.DisplayName;
            if (string.IsNullOrWhiteSpace(display) || string.Equals(display, coordinate, StringComparison.OrdinalIgnoreCase))
                return coordinate;
            return coordinate + "  " + display;
        }

        private static string ColumnLabel(int index)
        {
            if (index < 0)
                return string.Empty;

            string label = string.Empty;
            index++;
            while (index > 0)
            {
                int remainder = (index - 1) % 26;
                label = (char)('A' + remainder) + label;
                index = (index - 1) / 26;
            }

            return label;
        }
    }
#endif
}
