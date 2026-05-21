using PungentFunk.Utilities.Editor.Theme;
using PungentFunk.Utilities.Generation;

namespace PungentFunk.Utilities.Editor.Generation
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    public sealed partial class ProceduralTextureLabWindow
    {
        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
                return;

            if ((_inspectorOpen || _showViewPopup || _textureEditOverlayOpen) && current.keyCode == KeyCode.Escape)
            {
                if (_textureEditOverlayOpen)
                    CloseTextureEditOverlay();
                else
                    _inspectorOpen = false;
                _showViewPopup = false;
                GUI.FocusControl(null);
                current.Use();
                Repaint();
                return;
            }

            if (EditorGUIUtility.editingTextField)
                return;

            if ((current.control || current.command) && current.keyCode == KeyCode.A)
            {
                SelectAllVariants();
                current.Use();
                return;
            }

            if (current.alt && (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.UpArrow))
            {
                MoveSelectedBasesByKeyboard(-1);
                current.Use();
                return;
            }

            if (current.alt && (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.DownArrow))
            {
                MoveSelectedBasesByKeyboard(1);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Space && GUIUtility.hotControl == 0)
            {
                if (!_generationRunning)
                    RunPrimaryGenerateAction();
                current.Use();
            }
        }

        private void HandleBaseCardInput(int index, Rect card, params Rect[] controlRects)
        {
            if (_suppressWorkbenchInputThisEvent)
                return;

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive, card);
            EventType type = current.GetTypeForControl(controlId);
            EditorGUIUtility.AddCursorRect(card, MouseCursor.Link);

            switch (type)
            {
                case EventType.MouseDown:
                    if (!card.Contains(current.mousePosition) || IsPointInAnyRect(current.mousePosition, controlRects))
                        return;

                    if (current.button == 1)
                    {
                        if (!IsBaseSelected(index))
                            SelectSingleBase(index);
                        ShowBaseMenu(index);
                        current.Use();
                        return;
                    }

                    if (current.button != 0)
                        return;

                    if (current.clickCount == 2)
                    {
                        SelectSingleBase(index);
                        if (_bases[index].locks == null || !_bases[index].locks.seed)
                        {
                            _bases[index].generation.seed = NewEditorSeed(index);
                            MarkDirty("Regenerated base seed.");
                        }
                        else
                        {
                            _lastStatus = "Seed is locked for this base.";
                        }
                        current.Use();
                        return;
                    }

                    SelectBaseFromEvent(index, current);
                    _dragBaseIndex = index;
                    _dragBaseStart = current.mousePosition;
                    _dragInsertIndex = index;
                    _dragBaseControl = controlId;
                    _draggingBase = false;
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId || _dragBaseControl != controlId)
                        return;

                    if (!_draggingBase && Vector2.Distance(current.mousePosition, _dragBaseStart) > 5f)
                        _draggingBase = true;

                    if (_draggingBase)
                    {
                        _dragInsertIndex = ResolveBaseInsertIndex(current.mousePosition);
                        Repaint();
                    }
                    current.Use();
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId || _dragBaseControl != controlId)
                        return;

                    if (_draggingBase)
                        MoveSelectedBasesToIndex(_dragInsertIndex);

                    _dragBaseIndex = -1;
                    _dragInsertIndex = -1;
                    _dragBaseControl = 0;
                    _draggingBase = false;
                    GUIUtility.hotControl = 0;
                    current.Use();
                    break;
            }
        }

        private void SelectBaseFromEvent(int index, Event current)
        {
            if (current.shift && _selectionAnchor >= 0)
            {
                _selectedBases.Clear();
                int min = Mathf.Min(_selectionAnchor, index);
                int max = Mathf.Max(_selectionAnchor, index);
                for (int i = min; i <= max; i++)
                    _selectedBases.Add(i);
                _selectedBase = index;
                return;
            }

            if (current.control || current.command)
            {
                if (_selectedBases.Contains(index))
                    _selectedBases.Remove(index);
                else
                    _selectedBases.Add(index);
                _selectedBase = index;
                _selectionAnchor = index;
                if (_selectedBases.Count == 0)
                    _selectedBases.Add(index);
                return;
            }

            SelectSingleBase(index);
        }

        private void SelectSingleBase(int index)
        {
            _selectedBase = Mathf.Clamp(index, 0, Mathf.Max(0, _bases.Count - 1));
            _selectionAnchor = _selectedBase;
            _selectedBases.Clear();
            _selectedBases.Add(_selectedBase);
        }

        private void SelectAllBases()
        {
            _selectedBases.Clear();
            for (int i = 0; i < _bases.Count; i++)
                _selectedBases.Add(i);
            _selectionAnchor = 0;
            _lastStatus = $"Selected {_selectedBases.Count} bases.";
            Repaint();
        }

        private bool IsBaseSelected(int index)
        {
            return _selectedBases.Contains(index);
        }

        private int ResolveBaseInsertIndex(Vector2 mousePosition)
        {
            for (int i = 0; i < _bases.Count; i++)
            {
                if (!_lastBaseRects.TryGetValue(i, out Rect rect))
                    continue;
                if (mousePosition.y < rect.center.y || (mousePosition.y <= rect.yMax && mousePosition.x < rect.center.x))
                    return i;
            }
            return _bases.Count;
        }

        private void MoveSelectedBasesByKeyboard(int direction)
        {
            if (_selectedBases.Count == 0)
                return;

            int target = Mathf.Clamp(_selectedBase + direction, 0, _bases.Count - 1);
            MoveSelectedBasesToIndex(target);
        }

        private void MoveSelectedBasesToIndex(int insertIndex)
        {
            if (_selectedBases.Count == 0 || insertIndex < 0)
                return;

            AddHistorySnapshot(_baseHistory, "Before Reorder Bases", false);
            var selected = new System.Collections.Generic.List<ProceduralTextureBaseSettings>();
            var remaining = new System.Collections.Generic.List<ProceduralTextureBaseSettings>();
            for (int i = 0; i < _bases.Count; i++)
            {
                if (_selectedBases.Contains(i))
                    selected.Add(_bases[i]);
                else
                    remaining.Add(_bases[i]);
            }

            int removedBefore = 0;
            foreach (int selectedIndex in _selectedBases)
            {
                if (selectedIndex < insertIndex)
                    removedBefore++;
            }

            int target = Mathf.Clamp(insertIndex - removedBefore, 0, remaining.Count);
            remaining.InsertRange(target, selected);
            _bases.Clear();
            _bases.AddRange(remaining);

            _selectedBases.Clear();
            for (int i = 0; i < selected.Count; i++)
                _selectedBases.Add(target + i);
            _selectedBase = Mathf.Clamp(target, 0, _bases.Count - 1);
            _selectionAnchor = _selectedBase;
            MarkDirty("Reordered bases.");
        }

        private void DrawBaseDragMarker()
        {
            if (!_draggingBase || _dragInsertIndex < 0 || _bases.Count == 0)
                return;

            Rect marker;
            if (_dragInsertIndex >= _bases.Count)
            {
                if (!_lastBaseRects.TryGetValue(_bases.Count - 1, out Rect last))
                    return;
                marker = new Rect(last.x, last.yMax + 2f, last.width, 3f);
            }
            else
            {
                if (!_lastBaseRects.TryGetValue(_dragInsertIndex, out Rect target))
                    return;
                marker = new Rect(target.x, target.y - 3f, target.width, 3f);
            }
            EditorGUI.DrawRect(marker, ComposeTint());
        }

        private void ToggleCandidateSelection(int index, bool additive)
        {
            if (_activeInspector == TextureInspectorTab.Refine)
            {
                SelectSingleVariantForRefine(index, additive);
                return;
            }

            if (!additive)
                _selectedCandidates.Clear();

            if (_selectedCandidates.Contains(index))
                _selectedCandidates.Remove(index);
            else
                _selectedCandidates.Add(index);

            if (index >= 0 && index < _candidates.Count)
                _candidates[index].selected = _selectedCandidates.Contains(index);
            if (_textureEditOverlayOpen && _textureEditOverlayIndex != index && !additive)
                CloseTextureEditOverlay();
            Repaint();
        }

        private void SelectSingleVariantForRefine(int index, bool attemptedMulti)
        {
            if (index < 0 || index >= _candidates.Count)
                return;

            for (int i = 0; i < _candidates.Count; i++)
                _candidates[i].selected = i == index;
            _selectedCandidates.Clear();
            _selectedCandidates.Add(index);
            SetActiveCandidate(index, true);
            if (attemptedMulti)
                _lastStatus = "Only one texture can be refined at a time.";
            Repaint();
        }

        private void SelectAllVariants()
        {
            if (_activeInspector == TextureInspectorTab.Refine)
            {
                if (IsValidActiveCandidate())
                    SelectSingleVariantForRefine(_activeCandidateIndex, true);
                return;
            }

            _selectedCandidates.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                _selectedCandidates.Add(i);
                _candidates[i].selected = true;
            }
            _lastStatus = $"Selected {_selectedCandidates.Count} textures.";
            Repaint();
        }

        private void ShowBaseMenu(int index)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Regenerate Seed"), false, () =>
            {
                if (_bases[index].locks == null || !_bases[index].locks.seed)
                {
                    _bases[index].generation.seed = NewEditorSeed(index);
                    MarkDirty("Regenerated base seed.");
                }
                else
                {
                    _lastStatus = "Seed is locked for this base.";
                    Repaint();
                }
            });
            menu.AddItem(new GUIContent("Randomize Unlocked Parameters"), false, () => RandomizeBaseParameters(_bases[index]));
            menu.AddItem(new GUIContent("Locks/Edit Locks"), false, () => ShowBaseLockMenu(index));
            menu.AddItem(new GUIContent(_bases[index].enabled ? "Disable" : "Enable"), false, () =>
            {
                _bases[index].enabled = !_bases[index].enabled;
                MarkDirty(_bases[index].enabled ? "Enabled base." : "Disabled base.");
            });
            menu.AddSeparator("");
            if (_bases.Count >= ProceduralTextureCombinationUtility.MaxBaseCount)
                menu.AddDisabledItem(new GUIContent("Duplicate"));
            else
                menu.AddItem(new GUIContent("Duplicate"), false, DuplicateSelectedBase);
            if (_bases.Count <= ProceduralTextureCombinationUtility.MinBaseCount)
                menu.AddDisabledItem(new GUIContent("Delete"));
            else
                menu.AddItem(new GUIContent("Delete"), false, DeleteSelectedBase);
            menu.ShowAsContext();
        }

        private void ShowBaseLockMenu(int index)
        {
            if (index < 0 || index >= _bases.Count)
                return;

            ProceduralTextureBaseSettings textureBase = _bases[index];
            if (textureBase.locks == null)
                textureBase.locks = new ProceduralTextureBaseLockSettings();

            ProceduralTextureBaseLockSettings locks = textureBase.locks;
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Seed"), locks.seed, () => ToggleBaseLock(index, "seed"));
            menu.AddItem(new GUIContent("Placement"), locks.pattern, () => ToggleBaseLock(index, "pattern"));
            menu.AddItem(new GUIContent("Amount / Spacing"), locks.density, () => ToggleBaseLock(index, "density"));
            menu.AddItem(new GUIContent("Shape / Radius / Intensity"), locks.stamp, () => ToggleBaseLock(index, "stamp"));
            menu.AddItem(new GUIContent("Placement Details"), locks.patternSpecific, () => ToggleBaseLock(index, "patternSpecific"));
            menu.AddItem(new GUIContent("Texture Inputs"), locks.source, () => ToggleBaseLock(index, "source"));
            menu.AddItem(new GUIContent("Blend / Opacity"), locks.blend, () => ToggleBaseLock(index, "blend"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Lock All"), false, () => SetAllBaseLocks(index, true));
            menu.AddItem(new GUIContent("Unlock All"), false, () => SetAllBaseLocks(index, false));
            menu.ShowAsContext();
        }

        private void ToggleBaseLock(int index, string key)
        {
            if (index < 0 || index >= _bases.Count)
                return;

            ProceduralTextureBaseLockSettings locks = _bases[index].locks;
            switch (key)
            {
                case "seed":
                    locks.seed = !locks.seed;
                    break;
                case "pattern":
                    locks.pattern = !locks.pattern;
                    break;
                case "density":
                    locks.density = !locks.density;
                    break;
                case "stamp":
                    locks.stamp = !locks.stamp;
                    break;
                case "patternSpecific":
                    locks.patternSpecific = !locks.patternSpecific;
                    break;
                case "source":
                    locks.source = !locks.source;
                    break;
                case "blend":
                    locks.blend = !locks.blend;
                    break;
            }
            MarkDirty("Updated base locks.");
        }

        private void SetAllBaseLocks(int index, bool locked)
        {
            if (index < 0 || index >= _bases.Count)
                return;

            ProceduralTextureBaseLockSettings locks = _bases[index].locks;
            locks.seed = locked;
            locks.pattern = locked;
            locks.density = locked;
            locks.stamp = locked;
            locks.patternSpecific = locked;
            locks.source = locked;
            locks.blend = locked;
            MarkDirty(locked ? "Locked base parameters." : "Unlocked base parameters.");
        }

        private void ShowMoreMenu(bool mutateVisible, bool inspectorVisible, bool viewVisible)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Generate Active Workflow"), false, RunPrimaryGenerateAction);
            menu.AddItem(new GUIContent("Generate Texture Grid"), false, GenerateCandidates);
            menu.AddItem(new GUIContent("Randomize Unlocked Textures"), false, RandomizeUnlockedVariants);
            if (_selectedCandidates.Count == 0)
                menu.AddDisabledItem(new GUIContent("Use Selected as Guides"));
            else
                menu.AddItem(new GUIContent("Use Selected as Guides"), false, ToggleSelectedVariantLocks);
            menu.AddItem(new GUIContent("Refresh Preview"), false, () => GeneratePreview(true));
            menu.AddItem(new GUIContent("Randomize Seeds"), false, RandomizeSeeds);
            menu.AddItem(new GUIContent("Reset Recipe"), false, ResetRecipe);
            menu.AddSeparator("");
            if (!inspectorVisible)
            {
                menu.AddItem(new GUIContent("Inspector/Compose"), _activeInspector == TextureInspectorTab.Compose, () => OpenInspector(TextureInspectorTab.Compose));
                menu.AddItem(new GUIContent("Inspector/Refine"), _activeInspector == TextureInspectorTab.Refine, () => OpenInspector(TextureInspectorTab.Refine));
                menu.AddItem(new GUIContent("Inspector/Export"), _activeInspector == TextureInspectorTab.Export, () => OpenInspector(TextureInspectorTab.Export));
                menu.AddItem(new GUIContent("Inspector/History"), _activeInspector == TextureInspectorTab.History, () => OpenInspector(TextureInspectorTab.History));
            }
            if (!viewVisible)
                menu.AddItem(new GUIContent("View Settings"), _showViewPopup, ToggleViewPopup);
            menu.AddSeparator("");
            if (_previewValues == null)
                menu.AddDisabledItem(new GUIContent("Export Maps"));
            else
                menu.AddItem(new GUIContent("Export Maps"), false, ExportMaps);
            menu.ShowAsContext();
        }

        private void OpenInspector(TextureInspectorTab tab)
        {
            _activeInspector = tab;
            _inspectorOpen = true;
            _inspectorScroll = Vector2.zero;
            SavePrefs();
            SaveSession();
            Repaint();
        }

        private void ToggleViewPopup()
        {
            _showViewPopup = !_showViewPopup;
            Repaint();
        }

        private void ToggleInspector()
        {
            _inspectorOpen = !_inspectorOpen;
            SaveSession();
            Repaint();
        }

        private bool ShouldDrawInlineInspector()
        {
            return position.width >= InlineInspectorMinWidth && position.height >= InlineInspectorMinHeight;
        }

        private float ClampInspectorWidth(float width)
        {
            float maxFromWorkspace = Mathf.Max(InspectorMinWidth, position.width - InspectorHandleWidth - WorkspaceMinWidth);
            float max = Mathf.Min(InspectorMaxWidth, maxFromWorkspace);
            float min = Mathf.Min(InspectorMinWidth, max);
            return Mathf.Clamp(width, min, max);
        }

        private void DrawInspectorResizeHandle()
        {
            Rect rect = GUILayoutUtility.GetRect(InspectorHandleWidth, 10f, GUILayout.Width(InspectorHandleWidth), GUILayout.ExpandHeight(true));
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event current = Event.current;

            if (current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.19f, 0.92f) : new Color(0.62f, 0.63f, 0.66f, 0.92f));
                EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y + 10f, 1f, Mathf.Max(1f, rect.height - 20f)), UtilityWindowTheme.ResizeHandleTint);
            }

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button == 0 && rect.Contains(current.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        _inspectorWidth = ClampInspectorWidth(_inspectorWidth - current.delta.x);
                        SavePrefs();
                        Repaint();
                        current.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;
            }
        }
    }
#endif
}
