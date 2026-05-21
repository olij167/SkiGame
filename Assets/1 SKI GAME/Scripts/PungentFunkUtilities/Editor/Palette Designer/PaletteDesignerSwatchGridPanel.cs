using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Colour;

namespace PungentFunk.Utilities.Editor.Colour
{
#if UNITY_EDITOR
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public partial class PaletteDesignerWindow
    {
        private void DrawPaletteInfoPanel()
        {
            using (BeginStudioCard(string.IsNullOrWhiteSpace(_activePalette.paletteName) ? _activePalette.name : _activePalette.paletteName, UtilityWindowTheme.Blue, _activePalette.Count.ToString(), 0.08f, 0.04f))
            {
                DrawPaletteRibbon(_activePalette.swatches, 22f);

                _showPaletteSettings = EditorGUILayout.Foldout(_showPaletteSettings, "Palette metadata", true);
                if (!_showPaletteSettings)
                    return;

                EditorGUI.BeginChangeCheck();
                string paletteName = EditorGUILayout.TextField("Name", _activePalette.paletteName);
                string notes = EditorGUILayout.TextArea(_activePalette.notes, GUILayout.MinHeight(30f));
                if (EditorGUI.EndChangeCheck())
                {
                    ChangePalette("Edit Palette Metadata", () =>
                    {
                        _activePalette.paletteName = paletteName;
                        _activePalette.notes = notes;
                    });
                }
            }
        }

        private void DrawPaletteBoardPanel()
        {
            using (BeginStudioCard(null, UtilityWindowTheme.Blue, null, 0.11f, 0.055f, GUILayout.ExpandHeight(true)))
            {
                DrawPaletteGenerationHeader();

                if (_activePalette.swatches == null || _activePalette.swatches.Count == 0)
                {
                    DrawStudioHelpCard("No swatches yet", "Add a swatch or generate a starter palette from the command bar.", UtilityWindowTheme.Blue);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (StudioButton("Add Swatch", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Secondary, GUILayout.Height(28f)))
                            AddSwatch();
                        if (StudioButton("Generate Palette", ControlsTint(), PaletteDesignerButtonTone.Primary, GUILayout.Height(28f)))
                            IteratePalette();
                    }
                    return;
                }

                DrawPaletteActionRow();
                DrawPaletteSwatchGrid();
            }
        }

        private void DrawPaletteGenerationHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                UtilityWindowTheme.SectionTitle("Palette Workbench", UtilityWindowTheme.Blue, $"{_activePalette.Count} swatches");
                GUILayout.FlexibleSpace();
                UtilityWindowTheme.CountPill($"{CountLockedSwatches()} locked", CountLockedSwatches() > 0 ? UtilityWindowTheme.Amber : UtilityWindowTheme.Neutral, 82f);
            }
            DrawPaletteRibbon(_activePalette.swatches, 14f);
        }

        private void DrawPaletteActionRow()
        {
            using (new EditorGUILayout.HorizontalScope(UtilityWindowTheme.PanelStyle(UtilityWindowTheme.Neutral, 0.055f, 0.025f, 4, 2)))
            {
                if (StudioButton("Add Swatch", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Secondary, GUILayout.Width(98f), GUILayout.Height(24f)))
                    AddSwatch();

                if (StudioButton("Sort / Names", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Width(98f), GUILayout.Height(24f)))
                    ShowPaletteSortMenu();

                GUILayout.Space(4f);
                UtilityWindowTheme.CountPill("Auto " + CurrentSwatchLayoutLabel(), UtilityWindowTheme.Cyan, 96f);
                int selectedCount = SelectedSwatchCount();
                if (selectedCount > 1)
                {
                    UtilityWindowTheme.CountPill($"{selectedCount} selected", UtilityWindowTheme.Teal, 96f);
                    if (CurrentContentWidth() < 820f)
                    {
                        if (StudioButton("Selected Actions", UtilityWindowTheme.Teal, PaletteDesignerButtonTone.Secondary, GUILayout.Width(128f), GUILayout.Height(24f)))
                            ShowSelectedSwatchBulkMenu();
                    }
                    else
                    {
                        if (StudioButton("Lock", UtilityWindowTheme.Amber, PaletteDesignerButtonTone.Ghost, GUILayout.Width(52f), GUILayout.Height(24f)))
                            SetSelectedSwatchesLocked(true);
                        if (StudioButton("Unlock", UtilityWindowTheme.Green, PaletteDesignerButtonTone.Ghost, GUILayout.Width(64f), GUILayout.Height(24f)))
                            SetSelectedSwatchesLocked(false);
                        if (StudioButton("Regenerate", ControlsTint(), PaletteDesignerButtonTone.Secondary, GUILayout.Width(88f), GUILayout.Height(24f)))
                            RegenerateSelectedSwatches();
                        if (StudioButton("Copy HEX", UtilityWindowTheme.Cyan, PaletteDesignerButtonTone.Ghost, GUILayout.Width(76f), GUILayout.Height(24f)))
                            CopySelectedHexValues();
                        if (StudioButton("Clear", UtilityWindowTheme.Neutral, PaletteDesignerButtonTone.Ghost, GUILayout.Width(52f), GUILayout.Height(24f)))
                            ClearSwatchSelection();
                    }
                }
                else if (IsValidSelectedSwatch())
                {
                    PaletteSwatch selected = _activePalette.swatches[_selectedSwatch];
                    UtilityWindowTheme.CountPill("1 selected", UtilityWindowTheme.Teal, 86f);
                    EditorGUILayout.LabelField($"{selected.name} / {Nicify(selected.role.ToString())} / Alt+Arrow moves selection", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                else
                {
                    EditorGUILayout.LabelField("Click to select, Shift/Ctrl-click for ranges, drag to reorder, double-click to regenerate.", UtilityWindowTheme.MutedMiniLabelStyle);
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void ShowPaletteSortMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Sort By Role"), false, () => SortSwatches(0));
            menu.AddItem(new GUIContent("Sort By Hue"), false, () => SortSwatches(1));
            menu.AddItem(new GUIContent("Sort By Value"), false, () => SortSwatches(2));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Normalize Names"), false, NormalizeNames);
            menu.ShowAsContext();
        }

        private void DrawPaletteSwatchGrid()
        {
            Rect viewport = GUILayoutUtility.GetRect(100f, 180f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(180f));
            if (viewport.width <= 1f || viewport.height <= 1f)
                return;

            _lastSwatchCanvasRect = viewport;
            _lastSwatchRects.Clear();
            _resolvedSwatchLayoutMode = ResolveSwatchLayout(viewport);

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.06f, 0.06f, 0.065f, 0.48f) : new Color(0.76f, 0.77f, 0.79f, 0.34f));

            Rect swatchViewport = DrawSimulationCanvasBanner(viewport);
            switch (_resolvedSwatchLayoutMode)
            {
                case PaletteSwatchLayoutMode.VerticalStack:
                    DrawVerticalSwatchStack(swatchViewport);
                    break;
                default:
                    DrawAdaptiveSwatchGrid(swatchViewport);
                    break;
            }
        }

        private Rect DrawSimulationCanvasBanner(Rect viewport)
        {
            if (!SimulationPreviewActive())
                return viewport;

            float bannerHeight = 30f;
            Rect banner = new Rect(viewport.x + 5f, viewport.y + 5f, Mathf.Max(1f, viewport.width - 10f), bannerHeight);
            Rect remaining = new Rect(viewport.x, viewport.y + bannerHeight + 8f, viewport.width, Mathf.Max(1f, viewport.height - bannerHeight - 8f));

            if (Event.current.type == EventType.Repaint)
            {
                Color fill = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.145f, 0.065f, 0.92f) : new Color(0.96f, 0.88f, 0.64f, 0.96f);
                Color border = new Color(UtilityWindowTheme.Amber.r, UtilityWindowTheme.Amber.g, UtilityWindowTheme.Amber.b, 0.72f);
                DrawStudioBox(banner, fill, border);
            }

            string label = viewport.width < 520f
                ? SimulationPreviewLabel()
                : SimulationPreviewLabel() + " preview only. Palette data is unchanged.";
            Rect buttonRect = new Rect(banner.xMax - 104f, banner.y + 4f, 96f, banner.height - 8f);
            Rect labelRect = new Rect(banner.x + 9f, banner.y + 6f, Mathf.Max(1f, buttonRect.x - banner.x - 14f), banner.height - 12f);
            GUI.Label(labelRect, label, EditorStyles.miniBoldLabel);

            if (GUI.Button(buttonRect, "View Original", EditorStyles.miniButton))
            {
                DisableSimulationPreview();
                Event.current.Use();
            }

            return remaining;
        }

        private PaletteSwatchLayoutMode ResolveSwatchLayout(Rect viewport)
        {
            if (viewport.width < 420f)
                return PaletteSwatchLayoutMode.VerticalStack;
            return PaletteSwatchLayoutMode.AdaptiveGrid;
        }

        private void DrawAdaptiveSwatchGrid(Rect viewport)
        {
            int count = _activePalette.swatches.Count;
            if (count == 0)
                return;

            Rect inner = new Rect(viewport.x + 4f, viewport.y + 4f, Mathf.Max(1f, viewport.width - 8f), Mathf.Max(1f, viewport.height - 8f));
            int columns = BestGridColumnCount(count, inner.width, inner.height);
            int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
            float tileWidth = Mathf.Floor((inner.width - ((columns - 1) * SwatchGap)) / Mathf.Max(1, columns));
            float fittedTileHeight = Mathf.Floor((inner.height - ((rows - 1) * SwatchGap)) / Mathf.Max(1, rows));
            float tileHeight = fittedTileHeight < 148f ? 148f : Mathf.Clamp(fittedTileHeight, 148f, 300f);
            float contentHeight = Mathf.Max(inner.height, rows * tileHeight + Mathf.Max(0, rows - 1) * SwatchGap);

            Rect view = new Rect(0f, 0f, inner.width, contentHeight);
            _swatchStripScroll = GUI.BeginScrollView(inner, _swatchStripScroll, view, false, contentHeight > inner.height);
            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect card = new Rect(col * (tileWidth + SwatchGap), row * (tileHeight + SwatchGap), tileWidth, tileHeight);
                DrawSwatchTile(i, card);
            }
            DrawSwatchDragInsertionMarker(count);
            GUI.EndScrollView();
        }

        private int BestGridColumnCount(int count, float width, float height)
        {
            int maxColumns = Mathf.Clamp(count, 1, 12);
            int bestColumns = 1;
            float bestScore = float.MinValue;
            for (int columns = 1; columns <= maxColumns; columns++)
            {
                int rows = Mathf.CeilToInt(count / (float)columns);
                float tileWidth = (width - ((columns - 1) * SwatchGap)) / columns;
                float tileHeight = (height - ((rows - 1) * SwatchGap)) / rows;
                float heightForScore = Mathf.Max(86f, tileHeight);
                float aspect = tileWidth / Mathf.Max(1f, heightForScore);
                float score = Mathf.Min(tileWidth, heightForScore * 1.45f) - Mathf.Abs(aspect - 1.45f) * 24f;
                if (tileWidth < SwatchMinWidth)
                    score -= 120f;
                if (tileHeight < 140f)
                    score -= 50f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestColumns = columns;
                }
            }

            return Mathf.Max(1, bestColumns);
        }

        private void DrawVerticalSwatchStack(Rect viewport)
        {
            int count = _activePalette.swatches.Count;
            Rect inner = new Rect(viewport.x + 4f, viewport.y + 4f, Mathf.Max(1f, viewport.width - 8f), Mathf.Max(1f, viewport.height - 8f));
            float fitHeight = (inner.height - Mathf.Max(0, count - 1) * SwatchGap) / Mathf.Max(1, count);
            float tileHeight = Mathf.Max(128f, fitHeight);
            float contentHeight = Mathf.Max(inner.height, count * tileHeight + Mathf.Max(0, count - 1) * SwatchGap);

            Rect view = new Rect(0f, 0f, inner.width, contentHeight);
            _swatchStripScroll = GUI.BeginScrollView(inner, _swatchStripScroll, view, false, contentHeight > inner.height);
            for (int i = 0; i < count; i++)
            {
                Rect card = new Rect(0f, i * (tileHeight + SwatchGap), inner.width, tileHeight);
                DrawSwatchTile(i, card);
            }
            DrawSwatchDragInsertionMarker(count);
            GUI.EndScrollView();
        }

        private void DrawSwatchTile(int index, Rect card)
        {
            PaletteSwatch swatch = _activePalette.swatches[index];
            if (swatch == null)
                return;

            _lastSwatchRects[index] = card;
            bool selected = IsSwatchSelected(index);
            DrawSwatchCardPreview(index, swatch, card, selected);
        }

        private void DrawSwatchCardPreview(int index, PaletteSwatch swatch, Rect card, bool selected)
        {
            Color previewColor = ColourDeficiencyPreviewUtility.Apply(_deficiencyPreview, swatch.color);
            EditorGUI.DrawRect(card, previewColor);

            Color readable = ColourContrastUtility.GetReadableTextColor(swatch.color);
            Color shade = readable.maxColorComponent > 0.6f ? new Color(0f, 0f, 0f, 0.38f) : new Color(1f, 1f, 1f, 0.32f);
            bool compact = card.width < 225f;
            bool narrow = card.width < 198f;
            float pad = compact ? 4f : 6f;
            float topHeight = compact ? 24f : 26f;
            float bottomHeight = compact ? 52f : 56f;
            EditorGUI.DrawRect(new Rect(card.x, card.y, card.width, topHeight), shade);
            EditorGUI.DrawRect(new Rect(card.x, card.yMax - bottomHeight, card.width, bottomHeight), shade);

            float menuWidth = narrow ? 28f : 38f;
            float lockWidth = narrow ? 52f : 56f;
            Rect menuRect = new Rect(card.xMax - menuWidth - 4f, card.y + 4f, menuWidth, 19f);
            Rect lockRect = new Rect(menuRect.x - lockWidth - 4f, card.y + 4f, lockWidth, 19f);
            Rect dragRect = new Rect(card.x + pad, card.y + 4f, narrow ? 14f : 18f, 19f);
            Rect roleRect = new Rect(dragRect.xMax + 3f, card.y + 4f, Mathf.Max(48f, lockRect.x - dragRect.xMax - 7f), 19f);
            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.MoveArrow);

            DrawSwatchOverlayBadge(dragRect, "||", Color.clear, TextAnchor.MiddleCenter, readable);

            if (swatch.locked)
            {
                DrawSwatchOverlayBadge(roleRect, Nicify(swatch.role.ToString()), Color.clear, TextAnchor.MiddleLeft, readable);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                PaletteSwatchRole nextRole = (PaletteSwatchRole)EditorGUI.EnumPopup(roleRect, GUIContent.none, swatch.role);
                if (EditorGUI.EndChangeCheck())
                {
                    ChangePalette("Edit Swatch Role", () =>
                    {
                        swatch.role = nextRole;
                        if (string.IsNullOrWhiteSpace(swatch.name) || swatch.name.StartsWith("Swatch"))
                            swatch.name = Nicify(nextRole.ToString());
                    });
                }
            }

            if (GUI.Button(lockRect, new GUIContent(swatch.locked ? "Unlock" : "Lock", swatch.locked ? "Unlock this swatch." : "Lock this swatch for iterative regeneration."), EditorStyles.miniButton))
                ChangePalette("Toggle Swatch Lock", () => swatch.locked = !swatch.locked);

            if (GUI.Button(menuRect, new GUIContent(narrow ? "..." : "More", "Swatch actions, reorder, and copy formats."), EditorStyles.miniButton))
                ShowSwatchMenu(index);

            Rect nameRect = new Rect(card.x + pad, card.yMax - bottomHeight + 5f, card.width - pad * 2f, 18f);
            EditorGUI.BeginChangeCheck();
            string nextName = EditorGUI.TextField(nameRect, GUIContent.none, string.IsNullOrWhiteSpace(swatch.name) ? $"Swatch {index + 1}" : swatch.name);
            if (EditorGUI.EndChangeCheck())
                ChangePalette("Rename Swatch", () => swatch.name = nextName);

            string hex = ColourConversionUtility.ToHexRGB(swatch.color);
            Rect secondRow = new Rect(card.x + pad, nameRect.yMax + 4f, card.width - pad * 2f, 18f);
            float colorWidth = narrow ? 28f : 32f;
            float copyWidth = narrow ? 26f : compact ? 34f : 44f;
            float regenWidth = narrow ? 34f : compact ? 42f : 56f;
            Rect copyRect = new Rect(secondRow.xMax - copyWidth, secondRow.y, copyWidth, secondRow.height);
            Rect regenRect = swatch.locked ? Rect.zero : new Rect(copyRect.x - regenWidth - 4f, secondRow.y, regenWidth, secondRow.height);
            Rect hexRect = swatch.locked
                ? new Rect(secondRow.x, secondRow.y, Mathf.Max(44f, copyRect.x - secondRow.x - 4f), secondRow.height)
                : new Rect(secondRow.x + colorWidth + 4f, secondRow.y, Mathf.Max(44f, regenRect.x - (secondRow.x + colorWidth + 4f) - 4f), secondRow.height);

            Rect colorRect = swatch.locked ? Rect.zero : new Rect(secondRow.x, secondRow.y, colorWidth, secondRow.height);
            if (!swatch.locked)
            {
                EditorGUI.BeginChangeCheck();
                Color nextColor = EditorGUI.ColorField(colorRect, GUIContent.none, swatch.color, true, true, false);
                if (EditorGUI.EndChangeCheck())
                    ChangePalette("Edit Swatch Colour", () => swatch.color = nextColor);

                EditorGUI.BeginChangeCheck();
                string nextHex = EditorGUI.TextField(hexRect, GUIContent.none, hex);
                if (EditorGUI.EndChangeCheck() && !string.Equals(nextHex, hex, System.StringComparison.OrdinalIgnoreCase) && ColourConversionUtility.TryParseHex(nextHex, out Color parsed))
                    ChangePalette("Edit Swatch HEX", () => swatch.color = parsed);

                if (GUI.Button(regenRect, new GUIContent(narrow ? "Gen" : "Regen", "Regenerate this unlocked swatch."), EditorStyles.miniButton))
                    RegenerateSwatch(index);
            }
            else
            {
                DrawSwatchOverlayBadge(hexRect, hex, Color.clear, TextAnchor.MiddleLeft, readable);
            }

            if (GUI.Button(copyRect, new GUIContent(narrow ? "C" : "Copy", "Copy this swatch HEX value."), EditorStyles.miniButton))
                CopyText(hex, $"Copied {hex}.");

            if (Event.current.type == EventType.Repaint)
            {
                bool hover = card.Contains(Event.current.mousePosition);
                bool contextHover = index == _hoveredAutoContextSwatchIndex;
                Color border = contextHover ? ControlsTint() : selected ? UtilityWindowTheme.Cyan : hover ? new Color(1f, 1f, 1f, 0.55f) : new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.34f : 0.20f);
                Handles.DrawSolidRectangleWithOutline(card, Color.clear, border);
                if (selected)
                    Handles.DrawSolidRectangleWithOutline(new Rect(card.x + 2f, card.y + 2f, card.width - 4f, card.height - 4f), Color.clear, new Color(UtilityWindowTheme.Cyan.r, UtilityWindowTheme.Cyan.g, UtilityWindowTheme.Cyan.b, 0.48f));
                if (contextHover)
                {
                    Color contextOutline = ControlsTint();
                    contextOutline.a = 0.82f;
                    Handles.DrawSolidRectangleWithOutline(new Rect(card.x + 4f, card.y + 4f, card.width - 8f, card.height - 8f), Color.clear, contextOutline);
                }
            }

            HandleSwatchCardInput(index, swatch, card, roleRect, lockRect, menuRect, nameRect, colorRect, hexRect, copyRect, regenRect);
        }

        private void HandleSwatchCardInput(int index, PaletteSwatch swatch, Rect card, params Rect[] controlRects)
        {
            if (_suppressWorkbenchInputThisEvent)
                return;

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive, card);
            EventType type = current.GetTypeForControl(controlId);

            switch (type)
            {
                case EventType.MouseDown:
                    if (!card.Contains(current.mousePosition) || IsPointInAnyRect(current.mousePosition, controlRects))
                        return;

                    if (current.button == 1)
                    {
                        if (!IsSwatchSelected(index))
                            SelectSingleSwatch(index);
                        ShowSwatchMenu(index);
                        current.Use();
                        return;
                    }

                    if (current.button != 0)
                        return;

                    if (current.clickCount == 2)
                    {
                        SelectSingleSwatch(index);
                        if (swatch != null && !swatch.locked)
                            RegenerateSwatch(index);
                        current.Use();
                        return;
                    }

                    SelectSwatchFromEvent(index, current);
                    _dragSwatchIndex = index;
                    _dragSwatchStart = current.mousePosition;
                    _dragInsertIndex = index;
                    _dragSwatchControl = controlId;
                    _draggingSwatches = false;
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId || _dragSwatchControl != controlId)
                        return;

                    if (!_draggingSwatches && Vector2.Distance(current.mousePosition, _dragSwatchStart) > 5f)
                        _draggingSwatches = true;

                    if (_draggingSwatches)
                    {
                        _dragInsertIndex = ResolveSwatchInsertIndex(current.mousePosition);
                        Repaint();
                    }

                    current.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId || _dragSwatchControl != controlId)
                        return;

                    if (_draggingSwatches)
                        MoveSelectedSwatchesToIndex(_dragInsertIndex);

                    _dragSwatchIndex = -1;
                    _dragInsertIndex = -1;
                    _dragSwatchControl = 0;
                    _draggingSwatches = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }
        }

        private bool IsPointInAnyRect(Vector2 point, Rect[] rects)
        {
            if (rects == null)
                return false;

            for (int i = 0; i < rects.Length; i++)
            {
                if (rects[i].width > 0f && rects[i].height > 0f && rects[i].Contains(point))
                    return true;
            }

            return false;
        }

        private int ResolveSwatchInsertIndex(Vector2 mousePosition)
        {
            if (_activePalette == null || _activePalette.swatches == null || _activePalette.swatches.Count == 0)
                return 0;

            for (int i = 0; i < _activePalette.swatches.Count; i++)
            {
                if (!_lastSwatchRects.TryGetValue(i, out Rect rect))
                    continue;

                if (_resolvedSwatchLayoutMode == PaletteSwatchLayoutMode.VerticalStack)
                {
                    if (mousePosition.y < rect.center.y)
                        return i;
                }
                else if (mousePosition.y < rect.center.y || (mousePosition.y <= rect.yMax && mousePosition.x < rect.center.x))
                {
                    return i;
                }
            }

            return _activePalette.swatches.Count;
        }

        private void DrawSwatchDragInsertionMarker(int count)
        {
            if (!_draggingSwatches || _dragInsertIndex < 0 || count <= 0)
                return;

            Rect marker;
            if (_dragInsertIndex >= count)
            {
                if (!_lastSwatchRects.TryGetValue(count - 1, out Rect last))
                    return;
                marker = new Rect(last.x, last.yMax + 2f, last.width, 3f);
            }
            else
            {
                if (!_lastSwatchRects.TryGetValue(_dragInsertIndex, out Rect target))
                    return;
                marker = new Rect(target.x, target.y - 3f, target.width, 3f);
            }

            EditorGUI.DrawRect(marker, UtilityWindowTheme.Cyan);
        }

        private void DrawSwatchOverlayBadge(Rect rect, string text, Color background, TextAnchor alignment)
        {
            DrawSwatchOverlayBadge(rect, text, background, alignment, Color.white);
        }

        private void DrawSwatchOverlayBadge(Rect rect, string text, Color background, TextAnchor alignment, Color textColor)
        {
            if (background.a > 0f)
                EditorGUI.DrawRect(rect, background);
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = alignment,
                clipping = TextClipping.Clip,
                padding = new RectOffset(5, 5, 0, 0),
                normal = { textColor = textColor }
            };
            GUI.Label(rect, text, style);
        }
    }
#endif
}
