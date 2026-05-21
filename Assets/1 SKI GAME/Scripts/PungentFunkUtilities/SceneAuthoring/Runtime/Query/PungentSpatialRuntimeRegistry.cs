using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PungentFunk.Utilities.SceneTools
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Utilities/Spatial/Runtime Registry")]
    public class PungentSpatialRuntimeRegistry : MonoBehaviour, IPungentSpatialQueryProvider
    {
        private static readonly List<PungentSpatialRuntimeRegistry> ActiveRegistries = new List<PungentSpatialRuntimeRegistry>();
        private static readonly List<Component> GlobalRegistered = new List<Component>();

        [Tooltip("If enabled, disabled scene objects are included when this registry refreshes from loaded scenes.")]
        public bool includeInactive;

        [Tooltip("If enabled, refreshes provider lists when this registry becomes active.")]
        public bool refreshOnEnable = true;

        [SerializeField] private List<Component> pathSources = new List<Component>();
        [SerializeField] private List<Component> areaSources = new List<Component>();

        public IReadOnlyList<Component> PathSources => pathSources;
        public IReadOnlyList<Component> AreaSources => areaSources;

        private void OnEnable()
        {
            if (!ActiveRegistries.Contains(this))
                ActiveRegistries.Add(this);

            if (refreshOnEnable)
                RefreshFromLoadedScenes();
        }

        private void OnDisable()
        {
            ActiveRegistries.Remove(this);
        }

        public void RefreshFromLoadedScenes()
        {
            pathSources.Clear();
            areaSources.Clear();
            DiscoverSceneProviders(pathSources, areaSources, includeInactive);
        }

        public int CopyPathSources(IList<Component> results)
        {
            return CopyList(pathSources, results);
        }

        public int CopyAreaSources(IList<Component> results)
        {
            return CopyList(areaSources, results);
        }

        public static void RegisterGlobal(Component component)
        {
            if (component == null)
                return;
            if (!GlobalRegistered.Contains(component))
                GlobalRegistered.Add(component);
        }

        public static void UnregisterGlobal(Component component)
        {
            if (component == null)
                return;
            GlobalRegistered.Remove(component);
        }

        public static int CollectPathProviders(IList<Component> results, bool includeInactive = false, bool includeSceneDiscovery = true)
        {
            if (results == null)
                return 0;

            results.Clear();
            HashSet<int> seen = new HashSet<int>();
            AddRegisteredProviders(results, seen, wantPaths: true, includeInactive);

            for (int i = 0; i < ActiveRegistries.Count; i++)
            {
                PungentSpatialRuntimeRegistry registry = ActiveRegistries[i];
                if (registry == null)
                    continue;
                AddFromList(registry.pathSources, results, seen, includeInactive);
            }

            if (includeSceneDiscovery)
                DiscoverSceneProviders(results, null, includeInactive, seen);

            return results.Count;
        }

        public static int CollectAreaProviders(IList<Component> results, bool includeInactive = false, bool includeSceneDiscovery = true)
        {
            if (results == null)
                return 0;

            results.Clear();
            HashSet<int> seen = new HashSet<int>();
            AddRegisteredProviders(results, seen, wantPaths: false, includeInactive);

            for (int i = 0; i < ActiveRegistries.Count; i++)
            {
                PungentSpatialRuntimeRegistry registry = ActiveRegistries[i];
                if (registry == null)
                    continue;
                AddFromList(registry.areaSources, results, seen, includeInactive);
            }

            if (includeSceneDiscovery)
                DiscoverSceneProviders(null, results, includeInactive, seen);

            return results.Count;
        }

        private static int CopyList(IReadOnlyList<Component> source, IList<Component> destination)
        {
            if (destination == null)
                return 0;

            destination.Clear();
            if (source == null)
                return 0;

            for (int i = 0; i < source.Count; i++)
            {
                Component component = source[i];
                if (component != null)
                    destination.Add(component);
            }

            return destination.Count;
        }

        private static void AddRegisteredProviders(IList<Component> results, HashSet<int> seen, bool wantPaths, bool includeInactive)
        {
            for (int i = GlobalRegistered.Count - 1; i >= 0; i--)
            {
                Component component = GlobalRegistered[i];
                if (component == null)
                {
                    GlobalRegistered.RemoveAt(i);
                    continue;
                }

                if (!IsSceneComponentUsable(component, includeInactive))
                    continue;

                bool matches = wantPaths
                    ? component is IPungentPathPointProvider || component is IPungentPathQueryProvider
                    : component is IPungentAreaShapeProvider || component is IPungentAreaVolumeProvider;
                if (matches)
                    AddUnique(results, seen, component);
            }
        }

        private static void AddFromList(IReadOnlyList<Component> source, IList<Component> results, HashSet<int> seen, bool includeInactive)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                Component component = source[i];
                if (IsSceneComponentUsable(component, includeInactive))
                    AddUnique(results, seen, component);
            }
        }

        private static void DiscoverSceneProviders(
            IList<Component> pathResults,
            IList<Component> areaResults,
            bool includeInactive,
            HashSet<int> seen = null)
        {
            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>(includeInactive);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (!IsSceneComponentUsable(behaviour, includeInactive))
                    continue;

                if (pathResults != null && (behaviour is IPungentPathPointProvider || behaviour is IPungentPathQueryProvider))
                    AddUnique(pathResults, seen, behaviour);
                if (areaResults != null && (behaviour is IPungentAreaShapeProvider || behaviour is IPungentAreaVolumeProvider))
                    AddUnique(areaResults, seen, behaviour);
            }
        }

        private static bool IsSceneComponentUsable(Component component, bool includeInactive)
        {
            if (component == null || component.gameObject == null)
                return false;

            Scene scene = component.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            if (includeInactive)
                return true;

            return component.gameObject.activeInHierarchy &&
                   (!(component is Behaviour behaviour) || behaviour.isActiveAndEnabled);
        }

        private static void AddUnique(IList<Component> results, HashSet<int> seen, Component component)
        {
            if (results == null || component == null)
                return;

            int id = component.GetInstanceID();
            if (seen != null && !seen.Add(id))
                return;
            if (seen == null && results.Contains(component))
                return;

            results.Add(component);
        }
    }
}
