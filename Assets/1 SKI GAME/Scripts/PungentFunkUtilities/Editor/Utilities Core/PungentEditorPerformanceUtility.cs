namespace PungentFunk.Utilities.Editor.Core
{
    #if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Profiling;

    /// <summary>
    /// Shared editor-performance helpers for utility windows that need throttled updates, cached filters, and incremental work queues.
    /// This is intentionally generic so heavier windows can migrate to it without changing their public workflows.
    /// </summary>
    public static class PungentEditorPerformanceUtility
    {
        private const string DiagnosticsPrefKey = "PungentFunkUtilities.Performance.DiagnosticsEnabled";
        private static readonly Dictionary<int, WindowRepaintCounter> WindowRepaintCounters = new Dictionary<int, WindowRepaintCounter>();
        private static readonly Dictionary<string, Type> CachedTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);
        private static readonly HashSet<string> MissingTypeKeys = new HashSet<string>(StringComparer.Ordinal);
        private static int _typeResolutionFallbackScans;
        private static bool _loggedRepeatedTypeFallbackWarning;

        private struct WindowRepaintCounter
        {
            public double WindowStartTime;
            public int Count;
            public int LastCompletedCount;
        }

        public static bool DiagnosticsEnabled
        {
            get => EditorPrefs.GetBool(DiagnosticsPrefKey, false);
            set => EditorPrefs.SetBool(DiagnosticsPrefKey, value);
        }

        public static int TypeResolutionFallbackScans => _typeResolutionFallbackScans;

        public static IDisposable Sample(string name)
        {
            return new ProfilerSampleScope(name);
        }

        private struct ProfilerSampleScope : IDisposable
        {
            public ProfilerSampleScope(string name)
            {
                Profiler.BeginSample(string.IsNullOrWhiteSpace(name) ? "PungentFunk Utilities" : name);
            }

            public void Dispose()
            {
                Profiler.EndSample();
            }
        }

        public static bool TimeGate(ref double nextTime, double intervalSeconds)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextTime)
                return false;

            nextTime = now + Math.Max(0.01d, intervalSeconds);
            return true;
        }

        public static bool RequestWindowRepaintThrottled(EditorWindow window, ref double nextAllowedTime, double intervalSeconds = 0.10d)
        {
            if (window == null || !TimeGate(ref nextAllowedTime, intervalSeconds))
                return false;

            window.Repaint();
            return true;
        }

        public static bool RequestSceneViewRepaintThrottled(SceneView sceneView, ref double nextAllowedTime, double intervalSeconds = 0.10d)
        {
            if (sceneView == null || !TimeGate(ref nextAllowedTime, intervalSeconds))
                return false;

            sceneView.Repaint();
            return true;
        }

        public static bool RequestLastActiveSceneViewRepaintThrottled(ref double nextAllowedTime, double intervalSeconds = 0.10d)
        {
            return RequestSceneViewRepaintThrottled(SceneView.lastActiveSceneView, ref nextAllowedTime, intervalSeconds);
        }

        public static bool RequestAllSceneViewsRepaintThrottled(ref double nextAllowedTime, double intervalSeconds = 0.20d)
        {
            if (!TimeGate(ref nextAllowedTime, intervalSeconds))
                return false;

            SceneView.RepaintAll();
            return true;
        }

        public static void RecordWindowRepaint(EditorWindow window)
        {
            if (!DiagnosticsEnabled || window == null)
                return;

            int id = window.GetInstanceID();
            double now = EditorApplication.timeSinceStartup;
            WindowRepaintCounters.TryGetValue(id, out WindowRepaintCounter counter);
            if (counter.WindowStartTime <= 0d)
                counter.WindowStartTime = now;

            if (now - counter.WindowStartTime >= 1d)
            {
                counter.LastCompletedCount = counter.Count;
                counter.Count = 0;
                counter.WindowStartTime = now;
            }

            counter.Count++;
            WindowRepaintCounters[id] = counter;
        }

        public static int GetRecentWindowRepaintCount(EditorWindow window)
        {
            if (!DiagnosticsEnabled || window == null)
                return 0;

            return WindowRepaintCounters.TryGetValue(window.GetInstanceID(), out WindowRepaintCounter counter)
                ? Math.Max(counter.Count, counter.LastCompletedCount)
                : 0;
        }

        public static Type ResolveTypeCached(string typeName, Func<Type, bool> predicate = null)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            string trimmed = typeName.Trim();
            if (CachedTypesByKey.TryGetValue(trimmed, out Type cached))
                return predicate == null || predicate(cached) ? cached : null;

            if (MissingTypeKeys.Contains(trimmed))
                return null;

            Type direct = Type.GetType(trimmed);
            if (direct != null && (predicate == null || predicate(direct)))
            {
                CacheResolvedType(direct);
                return direct;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type exact = assemblies[i].GetType(trimmed, false);
                if (exact != null && (predicate == null || predicate(exact)))
                {
                    CacheResolvedType(exact);
                    return exact;
                }
            }

            RecordTypeResolutionFallbackScan(trimmed);
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try
                {
                    types = assemblies[i].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null)
                    continue;

                for (int t = 0; t < types.Length; t++)
                {
                    Type candidate = types[t];
                    if (candidate == null)
                        continue;

                    if ((candidate.Name == trimmed || candidate.FullName == trimmed) && (predicate == null || predicate(candidate)))
                    {
                        CacheResolvedType(candidate);
                        return candidate;
                    }
                }
            }

            MissingTypeKeys.Add(trimmed);
            return null;
        }

        public static void RecordTypeResolutionFallbackScan(string windowTypeKey)
        {
            if (!MissingTypeKeys.Add(windowTypeKey))
                return;

            _typeResolutionFallbackScans++;
            if (!DiagnosticsEnabled)
                return;

            if (_typeResolutionFallbackScans > 1 && !_loggedRepeatedTypeFallbackWarning)
            {
                _loggedRepeatedTypeFallbackWarning = true;
                Debug.LogWarning($"[PungentFunk Utilities] Repeated fallback window-type scans detected. Latest key: {windowTypeKey}");
            }
        }

        private static void CacheResolvedType(Type type)
        {
            if (type == null)
                return;

            if (!string.IsNullOrEmpty(type.AssemblyQualifiedName))
                CachedTypesByKey[type.AssemblyQualifiedName] = type;
            if (!string.IsNullOrEmpty(type.FullName))
                CachedTypesByKey[type.FullName] = type;
            if (!string.IsNullOrEmpty(type.Name))
                CachedTypesByKey[type.Name] = type;
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
