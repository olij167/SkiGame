using System;
using System.Collections.Generic;
using PungentFunk.Utilities.BoardGraph;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.BoardGraph
{
#if UNITY_EDITOR
    public abstract class PungentBoardEditorCommand
    {
        public string boardId = string.Empty;
        public string label = "Board Edit";

        public abstract void Apply(PungentBoardDocument document);
    }

    public sealed class PungentBoardEditorMutationContext
    {
        public string boardId = string.Empty;
        public string label = "Board Edit";
        public string beforeJson = string.Empty;

        public bool HasSnapshot => !string.IsNullOrWhiteSpace(boardId) && !string.IsNullOrWhiteSpace(beforeJson);

        public static PungentBoardEditorMutationContext Capture(PungentBoardDocument document, string label)
        {
            if (document == null)
                return new PungentBoardEditorMutationContext();

            return new PungentBoardEditorMutationContext
            {
                boardId = document.id ?? string.Empty,
                label = string.IsNullOrWhiteSpace(label) ? "Board Edit" : label.Trim(),
                beforeJson = JsonUtility.ToJson(document)
            };
        }
    }

    public sealed class PungentBoardEditorHistory
    {
        private const int MaxSnapshots = 80;

        private readonly Stack<Entry> _undo = new Stack<Entry>();
        private readonly Stack<Entry> _redo = new Stack<Entry>();
        private string _boardId = string.Empty;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string NextUndoLabel => _undo.Count > 0 ? _undo.Peek().label : string.Empty;
        public string NextRedoLabel => _redo.Count > 0 ? _redo.Peek().label : string.Empty;

        public void Reset(PungentBoardDocument document)
        {
            _boardId = document != null ? document.id ?? string.Empty : string.Empty;
            _undo.Clear();
            _redo.Clear();
        }

        public PungentBoardEditorMutationContext Capture(PungentBoardDocument document, string label)
        {
            EnsureBoard(document);
            return PungentBoardEditorMutationContext.Capture(document, label);
        }

        public void Commit(PungentBoardEditorMutationContext context, PungentBoardDocument currentDocument)
        {
            if (context == null || !context.HasSnapshot || currentDocument == null)
                return;

            RecordSnapshot(context.boardId, context.label, context.beforeJson, currentDocument);
        }

        public void RecordBefore(PungentBoardDocument document, string label)
        {
            if (document == null)
                return;

            EnsureBoard(document);
            PushUndo(new Entry(document.id, label, JsonUtility.ToJson(document)));
            _redo.Clear();
        }

        public void RecordSnapshot(string boardId, string label, string beforeJson, PungentBoardDocument currentDocument)
        {
            if (string.IsNullOrWhiteSpace(beforeJson) || currentDocument == null)
                return;

            EnsureBoard(currentDocument);
            string afterJson = JsonUtility.ToJson(currentDocument);
            if (string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
                return;

            PushUndo(new Entry(boardId, label, beforeJson));
            _redo.Clear();
        }

        public bool Undo(PungentBoardDocument document)
        {
            if (document == null || _undo.Count == 0)
                return false;

            EnsureBoard(document);
            Entry entry = _undo.Pop();
            _redo.Push(new Entry(document.id, entry.label, JsonUtility.ToJson(document)));
            Restore(document, entry.json);
            return true;
        }

        public bool Redo(PungentBoardDocument document)
        {
            if (document == null || _redo.Count == 0)
                return false;

            EnsureBoard(document);
            Entry entry = _redo.Pop();
            _undo.Push(new Entry(document.id, entry.label, JsonUtility.ToJson(document)));
            Restore(document, entry.json);
            return true;
        }

        private void EnsureBoard(PungentBoardDocument document)
        {
            string id = document != null ? document.id ?? string.Empty : string.Empty;
            if (string.Equals(_boardId, id, StringComparison.OrdinalIgnoreCase))
                return;

            _boardId = id;
            _undo.Clear();
            _redo.Clear();
        }

        private void PushUndo(Entry entry)
        {
            _undo.Push(entry);
            Trim(_undo);
        }

        private static void Trim(Stack<Entry> stack)
        {
            if (stack.Count <= MaxSnapshots)
                return;

            Entry[] entries = stack.ToArray();
            stack.Clear();
            for (int i = Math.Min(MaxSnapshots, entries.Length) - 1; i >= 0; i--)
                stack.Push(entries[i]);
        }

        private static void Restore(PungentBoardDocument document, string json)
        {
            if (document == null || string.IsNullOrWhiteSpace(json))
                return;

            JsonUtility.FromJsonOverwrite(json, document);
            document.NormalizeInPlace();
        }

        private struct Entry
        {
            public readonly string boardId;
            public readonly string label;
            public readonly string json;

            public Entry(string boardId, string label, string json)
            {
                this.boardId = boardId ?? string.Empty;
                this.label = string.IsNullOrWhiteSpace(label) ? "Board Edit" : label.Trim();
                this.json = json ?? string.Empty;
            }
        }
    }
#endif
}
