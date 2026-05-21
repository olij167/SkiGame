using System.Collections.Generic;
using PungentFunk.Utilities.Authoring;
using PungentFunk.Utilities.DataSheets;
using PungentFunk.Utilities.Editor.Authoring;
using PungentFunk.Utilities.Editor.ProjectAudit;
using PungentFunk.Utilities.Editor.Theme;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.DataSheets
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    internal sealed class PungentDataSheetOverlayPreviewDrawer : IPungentAuthoringOverlayPreviewDrawer
    {
        private const float HeaderHeight = 24f;
        private const float RowHeight = 23f;
        private const float RowHeaderWidth = 118f;
        private const int MaxCachedSheets = 8;
        private const int MaxCachedFiltersPerSheet = 24;
        private const double SearchDebounceSeconds = 0.16d;

        private static GUIStyle _headerStyle;
        private static GUIStyle _rowHeaderStyle;
        private static GUIStyle _cellStyle;
        private static readonly Dictionary<string, CachedSheetPreviewData> SheetPreviewCache = new Dictionary<string, CachedSheetPreviewData>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> CacheUseOrder = new List<string>();

        private sealed class CachedSheetPreviewData
        {
            public string key = string.Empty;
            public List<PungentDataSheetColumn> allColumns = new List<PungentDataSheetColumn>();
            public List<PungentDataSheetColumn> visibleColumns = new List<PungentDataSheetColumn>();
            public List<PungentDataSheetRow> allRows = new List<PungentDataSheetRow>();
            public List<PungentDataSheetRow> visibleRows = new List<PungentDataSheetRow>();
            public Dictionary<string, PungentDataSheetCell> cells = new Dictionary<string, PungentDataSheetCell>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, PungentDataSheetColumn> columnById = new Dictionary<string, PungentDataSheetColumn>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, PungentDataSheetRow> rowById = new Dictionary<string, PungentDataSheetRow>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> columnSearchText = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> rowSearchText = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> cellSearchText = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, CachedFilterResult> filterResults = new Dictionary<string, CachedFilterResult>(System.StringComparer.OrdinalIgnoreCase);
            public List<string> filterUseOrder = new List<string>();
            public int lastVisibleCells;
        }

        private sealed class CachedFilterResult
        {
            public List<string> columnIds = new List<string>();
            public List<string> rowIds = new List<string>();
        }

        static PungentDataSheetOverlayPreviewDrawer()
        {
            PungentAuthoringOverlayPreviewRegistry.Register(new PungentDataSheetOverlayPreviewDrawer());
            PungentStickyNoteOverlayController.OverlayUpdate -= HandleOverlayUpdate;
            PungentStickyNoteOverlayController.OverlayUpdate += HandleOverlayUpdate;
        }

        public bool CanDraw(PungentAuthoringReference reference)
        {
            return reference != null &&
                   (reference.itemKind == PungentAuthoringItemKind.DataSheet ||
                    string.Equals(reference.providerId, PungentDataSheetProvider.Id, System.StringComparison.OrdinalIgnoreCase));
        }

        public void Draw(PungentAuthoringReference reference, PungentAuthoringPreview preview, PungentStickyNoteOverlayState state)
        {
            long drawSample = PungentAuthoringOverlayPerformance.BeginSample();
            PungentDataSheet sheet = PungentDataSheetEditorStorage.Database.FindSheet(reference.itemId);
            if (sheet == null)
            {
                EditorGUILayout.LabelField("Data sheet missing", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The linked data sheet could not be found.", UtilityWindowTheme.MutedMiniLabelStyle);
                PungentAuthoringOverlayPerformance.EndSample("Data Sheet Overlay Draw", drawSample);
                return;
            }

            EnsureStyles();
            CachedSheetPreviewData cache = GetSheetPreviewCache(sheet);
            List<PungentDataSheetColumn> columns = state.dataSheetShowHiddenColumns ? cache.allColumns : cache.visibleColumns;
            List<PungentDataSheetRow> rows = state.dataSheetShowHiddenRows ? cache.allRows : cache.visibleRows;
            ApplyPreviewFilters(sheet, state, cache, ref columns, ref rows);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Search", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(44f));
                string nextSearch = GUILayout.TextField(state.dataSheetSearch ?? string.Empty, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80f), GUILayout.ExpandWidth(true));
                if (!string.Equals(nextSearch, state.dataSheetSearch, System.StringComparison.Ordinal))
                {
                    state.dataSheetSearch = nextSearch ?? string.Empty;
                    state.dataSheetLastSearchEditTime = EditorApplication.timeSinceStartup;
                    state.dataSheetFilterPending = true;
                }
                if (!string.IsNullOrWhiteSpace(state.dataSheetSearch) && GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
                {
                    state.dataSheetSearch = string.Empty;
                    state.dataSheetLastSearchEditTime = EditorApplication.timeSinceStartup;
                    state.dataSheetFilterPending = true;
                }
                if (GUILayout.Button(new GUIContent("Rows", "Preview row filters."), EditorStyles.toolbarDropDown, GUILayout.Width(52f)))
                    ShowRowFilterMenu(state);
                if (GUILayout.Button(new GUIContent("Columns", "Preview column filters."), EditorStyles.toolbarDropDown, GUILayout.Width(72f)))
                    ShowColumnFilterMenu(state);
                GUILayout.FlexibleSpace();
                if (state.dataSheetFilterPending)
                    EditorGUILayout.LabelField("Updating...", UtilityWindowTheme.PathLabelStyle, GUILayout.Width(68f));
                PungentAuthoringOverlayPerformance.DrawToolbarReadout("Data Sheet Overlay Draw");
                EditorGUILayout.LabelField(rows.Count + " x " + columns.Count, UtilityWindowTheme.PathLabelStyle, GUILayout.Width(64f));
            }

            Rect gridRect = GUILayoutUtility.GetRect(120f, 10000f, 140f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            cache.lastVisibleCells = DrawGrid(gridRect, columns, rows, cache.cells, state);
            PungentAuthoringOverlayPerformance.EndSample("Data Sheet Overlay Draw", drawSample, cache.lastVisibleCells);
        }

        private static void ApplyPreviewFilters(PungentDataSheet sheet, PungentStickyNoteOverlayState state, CachedSheetPreviewData cache, ref List<PungentDataSheetColumn> columns, ref List<PungentDataSheetRow> rows)
        {
            Dictionary<string, PungentDataSheetCell> cells = cache != null ? cache.cells : null;
            string filterKey = BuildFilterKey(sheet, state, columns, rows);
            if (cache != null && cache.filterResults.TryGetValue(filterKey, out CachedFilterResult cached))
            {
                MarkFilterUsed(cache, filterKey);
                columns = ResolveColumns(cache, cached.columnIds);
                rows = ResolveRows(cache, cached.rowIds);
                return;
            }

            List<PungentDataSheetColumn> filteredColumns = columns;
            List<PungentDataSheetRow> filteredRows = rows;
            string search = (state.dataSheetAppliedSearch ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                List<PungentDataSheetColumn> sourceColumns = filteredColumns;
                List<PungentDataSheetRow> sourceRows = filteredRows;
                filteredRows = new List<PungentDataSheetRow>();
                for (int r = 0; r < sourceRows.Count; r++)
                {
                    PungentDataSheetRow row = sourceRows[r];
                    if (RowMatches(cache, row, search))
                    {
                        filteredRows.Add(row);
                        continue;
                    }

                    for (int c = 0; c < sourceColumns.Count; c++)
                    {
                        if (MatchesCell(cache, row, sourceColumns[c], search))
                        {
                            filteredRows.Add(row);
                            break;
                        }
                    }
                }

                List<PungentDataSheetRow> searchedRows = filteredRows;
                filteredColumns = new List<PungentDataSheetColumn>();
                for (int c = 0; c < sourceColumns.Count; c++)
                {
                    PungentDataSheetColumn column = sourceColumns[c];
                    if (ColumnMatches(cache, column, search))
                    {
                        filteredColumns.Add(column);
                        continue;
                    }

                    for (int r = 0; r < searchedRows.Count; r++)
                    {
                        if (MatchesCell(cache, searchedRows[r], column, search))
                        {
                            filteredColumns.Add(column);
                            break;
                        }
                    }
                }
            }

            if (state.dataSheetHideEmptyRows)
            {
                List<PungentDataSheetRow> nonEmptyRows = new List<PungentDataSheetRow>();
                for (int r = 0; r < filteredRows.Count; r++)
                {
                    PungentDataSheetRow row = filteredRows[r];
                    for (int c = 0; c < filteredColumns.Count; c++)
                    {
                        if (HasCellValue(cells, row, filteredColumns[c]))
                        {
                            nonEmptyRows.Add(row);
                            break;
                        }
                    }
                }
                filteredRows = nonEmptyRows;
            }
            if (state.dataSheetHideEmptyColumns)
            {
                List<PungentDataSheetColumn> nonEmptyColumns = new List<PungentDataSheetColumn>();
                for (int c = 0; c < filteredColumns.Count; c++)
                {
                    PungentDataSheetColumn column = filteredColumns[c];
                    for (int r = 0; r < filteredRows.Count; r++)
                    {
                        if (HasCellValue(cells, filteredRows[r], column))
                        {
                            nonEmptyColumns.Add(column);
                            break;
                        }
                    }
                }
                filteredColumns = nonEmptyColumns;
            }

            columns = filteredColumns;
            rows = filteredRows;

            if (cache != null)
            {
                cache.filterResults[filterKey] = CaptureFilterResult(columns, rows);
                MarkFilterUsed(cache, filterKey);
                TrimFilterCache(cache);
            }
        }

        private static bool MatchesCell(Dictionary<string, PungentDataSheetCell> cells, PungentDataSheetRow row, PungentDataSheetColumn column, string search)
        {
            if (cells == null || row == null || column == null)
                return false;

            cells.TryGetValue(CellKey(row.id, column.id), out PungentDataSheetCell cell);
            return cell != null && Matches(cell.VisibleValue, search);
        }

        private static bool MatchesCell(CachedSheetPreviewData cache, PungentDataSheetRow row, PungentDataSheetColumn column, string search)
        {
            if (cache == null || row == null || column == null)
                return false;

            cache.cellSearchText.TryGetValue(CellKey(row.id, column.id), out string text);
            return Matches(text, search);
        }

        private static bool RowMatches(CachedSheetPreviewData cache, PungentDataSheetRow row, string search)
        {
            if (cache == null || row == null)
                return false;
            cache.rowSearchText.TryGetValue(row.id ?? string.Empty, out string text);
            return Matches(text, search);
        }

        private static bool ColumnMatches(CachedSheetPreviewData cache, PungentDataSheetColumn column, string search)
        {
            if (cache == null || column == null)
                return false;
            cache.columnSearchText.TryGetValue(column.id ?? string.Empty, out string text);
            return Matches(text, search);
        }

        private static bool HasCellValue(Dictionary<string, PungentDataSheetCell> cells, PungentDataSheetRow row, PungentDataSheetColumn column)
        {
            if (cells == null || row == null || column == null)
                return false;

            cells.TryGetValue(CellKey(row.id, column.id), out PungentDataSheetCell cell);
            return cell != null && !string.IsNullOrWhiteSpace(cell.VisibleValue);
        }

        private static bool Matches(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ShowRowFilterMenu(PungentStickyNoteOverlayState state)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Hidden Rows"), state.dataSheetShowHiddenRows, () => state.dataSheetShowHiddenRows = !state.dataSheetShowHiddenRows);
            menu.AddItem(new GUIContent("Hide Empty Rows"), state.dataSheetHideEmptyRows, () => state.dataSheetHideEmptyRows = !state.dataSheetHideEmptyRows);
            menu.ShowAsContext();
        }

        private static void ShowColumnFilterMenu(PungentStickyNoteOverlayState state)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show Hidden Columns"), state.dataSheetShowHiddenColumns, () => state.dataSheetShowHiddenColumns = !state.dataSheetShowHiddenColumns);
            menu.AddItem(new GUIContent("Hide Empty Columns"), state.dataSheetHideEmptyColumns, () => state.dataSheetHideEmptyColumns = !state.dataSheetHideEmptyColumns);
            menu.ShowAsContext();
        }

        private static int DrawGrid(Rect rect, List<PungentDataSheetColumn> columns, List<PungentDataSheetRow> rows, Dictionary<string, PungentDataSheetCell> cells, PungentStickyNoteOverlayState state)
        {
            if (rect.width <= 8f || rect.height <= 8f)
                return 0;

            float contentWidth = RowHeaderWidth;
            for (int i = 0; i < columns.Count; i++)
                contentWidth += ColumnWidth(columns[i]);
            float contentHeight = HeaderHeight + rows.Count * RowHeight;
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(contentWidth, rect.width - 18f), Mathf.Max(contentHeight, rect.height - 18f));

            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.105f, 0.112f, 1f) : new Color(0.86f, 0.87f, 0.89f, 1f));
            state.dataSheetScroll = GUI.BeginScrollView(rect, state.dataSheetScroll, viewRect, true, true);
            try
            {
                DrawHeaders(columns, state.dataSheetScroll.x, rect.width);
                return DrawRows(columns, rows, cells, state.dataSheetScroll, rect);
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private static void DrawHeaders(List<PungentDataSheetColumn> columns, float scrollX, float viewportWidth)
        {
            Rect corner = new Rect(0f, 0f, RowHeaderWidth, HeaderHeight);
            DrawCell(corner, "#", _headerStyle, true);
            float x = RowHeaderWidth;
            for (int i = 0; i < columns.Count; i++)
            {
                float width = ColumnWidth(columns[i]);
                Rect columnRect = new Rect(x, 0f, width, HeaderHeight);
                if (columnRect.xMax >= scrollX - 4f && columnRect.x <= scrollX + viewportWidth + 4f)
                    DrawCell(columnRect, string.IsNullOrWhiteSpace(columns[i].displayName) ? "Column" : columns[i].displayName, _headerStyle, true);
                x += width;
                if (x > scrollX + viewportWidth + 8f)
                    break;
            }
        }

        private static int DrawRows(List<PungentDataSheetColumn> columns, List<PungentDataSheetRow> rows, Dictionary<string, PungentDataSheetCell> cells, Vector2 scroll, Rect viewport)
        {
            int firstRow = Mathf.Clamp(Mathf.FloorToInt((scroll.y - HeaderHeight) / RowHeight), 0, Mathf.Max(0, rows.Count - 1));
            int visibleRows = Mathf.CeilToInt(viewport.height / RowHeight) + 3;
            int lastRow = Mathf.Min(rows.Count, firstRow + visibleRows);
            int drawn = 0;

            for (int rowIndex = firstRow; rowIndex < lastRow; rowIndex++)
            {
                PungentDataSheetRow row = rows[rowIndex];
                float y = HeaderHeight + rowIndex * RowHeight;
                string rowLabel = string.IsNullOrWhiteSpace(row.displayName) ? "Row " + (rowIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : row.displayName;
                DrawCell(new Rect(0f, y, RowHeaderWidth, RowHeight), rowLabel, _rowHeaderStyle, true);

                float x = RowHeaderWidth;
                for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
                {
                    PungentDataSheetColumn column = columns[columnIndex];
                    float width = ColumnWidth(column);
                    Rect cellRect = new Rect(x, y, width, RowHeight);
                    if (cellRect.xMax >= scroll.x - 4f && cellRect.x <= scroll.x + viewport.width + 4f)
                    {
                        cells.TryGetValue(CellKey(row.id, column.id), out PungentDataSheetCell cell);
                        DrawCell(cellRect, cell == null ? string.Empty : cell.VisibleValue, _cellStyle, false);
                        drawn++;
                    }

                    x += width;
                    if (x > scroll.x + viewport.width + 8f)
                        break;
                }
            }

            return drawn;
        }

        private static Dictionary<string, PungentDataSheetCell> BuildCellLookup(PungentDataSheet sheet)
        {
            Dictionary<string, PungentDataSheetCell> cells = new Dictionary<string, PungentDataSheetCell>(System.StringComparer.OrdinalIgnoreCase);
            if (sheet == null || sheet.cells == null)
                return cells;

            for (int i = 0; i < sheet.cells.Count; i++)
            {
                PungentDataSheetCell cell = sheet.cells[i];
                if (cell == null || string.IsNullOrWhiteSpace(cell.rowId) || string.IsNullOrWhiteSpace(cell.columnId))
                    continue;

                string key = CellKey(cell.rowId, cell.columnId);
                if (!cells.ContainsKey(key))
                    cells.Add(key, cell);
            }

            return cells;
        }

        private static CachedSheetPreviewData GetSheetPreviewCache(PungentDataSheet sheet)
        {
            string key = BuildSheetCacheKey(sheet);
            if (SheetPreviewCache.TryGetValue(key, out CachedSheetPreviewData cached) && cached != null)
            {
                MarkCacheUsed(key);
                return cached;
            }

            cached = new CachedSheetPreviewData
            {
                key = key,
                allColumns = BuildColumnList(sheet, true),
                visibleColumns = BuildColumnList(sheet, false),
                allRows = BuildRowList(sheet, true),
                visibleRows = BuildRowList(sheet, false),
                cells = BuildCellLookup(sheet)
            };
            BuildSearchIndexes(sheet, cached);
            SheetPreviewCache[key] = cached;
            MarkCacheUsed(key);
            TrimSheetPreviewCache();
            return cached;
        }

        private static void BuildSearchIndexes(PungentDataSheet sheet, CachedSheetPreviewData cache)
        {
            if (sheet == null || cache == null)
                return;

            cache.columnById.Clear();
            cache.rowById.Clear();
            cache.columnSearchText.Clear();
            cache.rowSearchText.Clear();
            cache.cellSearchText.Clear();

            if (sheet.columns != null)
            {
                for (int i = 0; i < sheet.columns.Count; i++)
                {
                    PungentDataSheetColumn column = sheet.columns[i];
                    if (column == null || string.IsNullOrWhiteSpace(column.id))
                        continue;
                    cache.columnById[column.id] = column;
                    cache.columnSearchText[column.id] = NormalizeSearchText(column.displayName + " " + column.id);
                }
            }

            if (sheet.rows != null)
            {
                for (int i = 0; i < sheet.rows.Count; i++)
                {
                    PungentDataSheetRow row = sheet.rows[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                        continue;
                    cache.rowById[row.id] = row;
                    cache.rowSearchText[row.id] = NormalizeSearchText(row.displayName + " " + row.id);
                }
            }

            if (sheet.cells != null)
            {
                for (int i = 0; i < sheet.cells.Count; i++)
                {
                    PungentDataSheetCell cell = sheet.cells[i];
                    if (cell == null || string.IsNullOrWhiteSpace(cell.rowId) || string.IsNullOrWhiteSpace(cell.columnId))
                        continue;
                    cache.cellSearchText[CellKey(cell.rowId, cell.columnId)] = NormalizeSearchText(cell.VisibleValue);
                }
            }
        }

        private static string NormalizeSearchText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.ToLowerInvariant();
        }

        private static string BuildSheetCacheKey(PungentDataSheet sheet)
        {
            if (sheet == null)
                return "missing";

            return string.Join("|",
                sheet.id ?? string.Empty,
                sheet.updatedUtc ?? string.Empty,
                sheet.columns == null ? "c0" : "c" + sheet.columns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sheet.rows == null ? "r0" : "r" + sheet.rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                sheet.cells == null ? "v0" : "v" + sheet.cells.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string BuildFilterKey(PungentDataSheet sheet, PungentStickyNoteOverlayState state, List<PungentDataSheetColumn> columns, List<PungentDataSheetRow> rows)
        {
            return string.Join("|",
                BuildSheetCacheKey(sheet),
                (state.dataSheetAppliedSearch ?? string.Empty).Trim().ToLowerInvariant(),
                state.dataSheetShowHiddenRows ? "shr1" : "shr0",
                state.dataSheetShowHiddenColumns ? "shc1" : "shc0",
                state.dataSheetHideEmptyRows ? "her1" : "her0",
                state.dataSheetHideEmptyColumns ? "hec1" : "hec0",
                columns == null ? "c0" : "c" + columns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                rows == null ? "r0" : "r" + rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static CachedFilterResult CaptureFilterResult(List<PungentDataSheetColumn> columns, List<PungentDataSheetRow> rows)
        {
            CachedFilterResult result = new CachedFilterResult();
            if (columns != null)
            {
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i] != null && !string.IsNullOrWhiteSpace(columns[i].id))
                        result.columnIds.Add(columns[i].id);
                }
            }

            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i] != null && !string.IsNullOrWhiteSpace(rows[i].id))
                        result.rowIds.Add(rows[i].id);
                }
            }

            return result;
        }

        private static List<PungentDataSheetColumn> ResolveColumns(CachedSheetPreviewData cache, List<string> ids)
        {
            if (cache == null || ids == null || ids.Count == 0)
                return new List<PungentDataSheetColumn>();

            List<PungentDataSheetColumn> result = new List<PungentDataSheetColumn>();
            for (int i = 0; i < ids.Count; i++)
                if (cache.columnById.TryGetValue(ids[i], out PungentDataSheetColumn column) && column != null)
                    result.Add(column);
            return result;
        }

        private static List<PungentDataSheetRow> ResolveRows(CachedSheetPreviewData cache, List<string> ids)
        {
            if (cache == null || ids == null || ids.Count == 0)
                return new List<PungentDataSheetRow>();

            List<PungentDataSheetRow> result = new List<PungentDataSheetRow>();
            for (int i = 0; i < ids.Count; i++)
                if (cache.rowById.TryGetValue(ids[i], out PungentDataSheetRow row) && row != null)
                    result.Add(row);
            return result;
        }

        private static void MarkCacheUsed(string key)
        {
            CacheUseOrder.Remove(key);
            CacheUseOrder.Add(key);
        }

        private static void TrimSheetPreviewCache()
        {
            while (CacheUseOrder.Count > MaxCachedSheets)
            {
                string oldest = CacheUseOrder[0];
                CacheUseOrder.RemoveAt(0);
                SheetPreviewCache.Remove(oldest);
            }
        }

        private static void TrimFilterCache(CachedSheetPreviewData cache)
        {
            if (cache == null || cache.filterResults.Count <= MaxCachedFiltersPerSheet)
                return;

            while (cache.filterResults.Count > MaxCachedFiltersPerSheet && cache.filterUseOrder.Count > 0)
            {
                string key = cache.filterUseOrder[0];
                cache.filterUseOrder.RemoveAt(0);
                cache.filterResults.Remove(key);
            }
        }

        private static void MarkFilterUsed(CachedSheetPreviewData cache, string key)
        {
            if (cache == null || string.IsNullOrWhiteSpace(key))
                return;
            cache.filterUseOrder.Remove(key);
            cache.filterUseOrder.Add(key);
        }

        private static List<PungentDataSheetColumn> BuildColumnList(PungentDataSheet sheet, bool includeHidden)
        {
            List<PungentDataSheetColumn> columns = new List<PungentDataSheetColumn>();
            if (sheet == null || sheet.columns == null)
                return columns;

            for (int i = 0; i < sheet.columns.Count; i++)
            {
                PungentDataSheetColumn column = sheet.columns[i];
                if (column != null && (includeHidden || !column.hidden))
                    columns.Add(column);
            }
            return columns;
        }

        private static List<PungentDataSheetRow> BuildRowList(PungentDataSheet sheet, bool includeHidden)
        {
            List<PungentDataSheetRow> rows = new List<PungentDataSheetRow>();
            if (sheet == null || sheet.rows == null)
                return rows;

            for (int i = 0; i < sheet.rows.Count; i++)
            {
                PungentDataSheetRow row = sheet.rows[i];
                if (row != null && (includeHidden || !row.hidden))
                    rows.Add(row);
            }
            rows.Sort((a, b) => a.order.CompareTo(b.order));
            return rows;
        }

        private static string CellKey(string rowId, string columnId)
        {
            return PungentAuthoringId.Normalize(rowId) + "\u001F" + PungentAuthoringId.Normalize(columnId);
        }

        private static void DrawCell(Rect rect, string text, GUIStyle style, bool header)
        {
            Color fill = header
                ? (EditorGUIUtility.isProSkin ? new Color(0.16f, 0.17f, 0.18f, 1f) : new Color(0.78f, 0.80f, 0.82f, 1f))
                : (EditorGUIUtility.isProSkin ? new Color(0.125f, 0.13f, 0.138f, 1f) : new Color(0.94f, 0.95f, 0.96f, 1f));
            EditorGUI.DrawRect(rect, fill);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.14f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.35f) : new Color(0f, 0f, 0f, 0.12f));
            GUI.Label(new Rect(rect.x + 5f, rect.y + 2f, Mathf.Max(8f, rect.width - 10f), rect.height - 4f), text ?? string.Empty, style);
        }

        private static float ColumnWidth(PungentDataSheetColumn column)
        {
            if (column == null)
                return 120f;
            return Mathf.Clamp(column.width <= 0f ? 140f : column.width, 80f, 260f);
        }

        private static void HandleOverlayUpdate(PungentStickyNoteOverlayState state)
        {
            if (state == null || !state.dataSheetFilterPending)
                return;
            if (EditorApplication.timeSinceStartup - state.dataSheetLastSearchEditTime < SearchDebounceSeconds)
                return;

            state.dataSheetAppliedSearch = state.dataSheetSearch ?? string.Empty;
            state.dataSheetFilterPending = false;
            PungentStickyNoteOverlayTrayWindow.RepaintIfOpen();
        }

        private static void EnsureStyles()
        {
            if (_cellStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft
            };
            _rowHeaderStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft
            };
            _cellStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
#endif
}
