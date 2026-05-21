namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Shared editor-performance helpers for utility windows that need throttled updates, cached filters, and incremental work queues.
    /// This is intentionally generic so heavier windows can migrate to it without changing their public workflows.
    /// </summary>
    public static class PungentEditorPerformanceUtility
    {
        public static bool TimeGate(ref double nextTime, double intervalSeconds)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextTime)
                return false;

            nextTime = now + Math.Max(0.01d, intervalSeconds);
            return true;
        }

        public static bool DrawProgress(string title, string info, float progress, Func<bool> cancelCallback = null)
        {
            bool cancel = EditorUtility.DisplayCancelableProgressBar(title, info, Mathf.Clamp01(progress));
            if (cancelCallback != null && cancelCallback.Invoke())
                cancel = true;
            return cancel;
        }

        public static IEnumerable<T> FilterCached<T>(IEnumerable<T> source, string search, Func<T, string> searchText)
        {
            if (source == null)
                return Enumerable.Empty<T>();
            if (string.IsNullOrWhiteSpace(search))
                return source;

            string lower = search.Trim().ToLowerInvariant();
            return source.Where(item =>
            {
                string text = searchText != null ? searchText(item) : Convert.ToString(item);
                return !string.IsNullOrEmpty(text) && text.ToLowerInvariant().Contains(lower);
            });
        }

        public sealed class IncrementalQueue<T>
        {
            private readonly Queue<T> _queue = new Queue<T>();
            public int Count => _queue.Count;

            public void Clear() => _queue.Clear();
            public void Enqueue(T item) => _queue.Enqueue(item);
            public void EnqueueRange(IEnumerable<T> items)
            {
                if (items == null)
                    return;
                foreach (T item in items)
                    _queue.Enqueue(item);
            }

            public int Process(int maxItems, Action<T> action)
            {
                if (action == null)
                    return 0;

                int processed = 0;
                while (_queue.Count > 0 && processed < maxItems)
                {
                    action(_queue.Dequeue());
                    processed++;
                }
                return processed;
            }
        }
    }
    #endif

}