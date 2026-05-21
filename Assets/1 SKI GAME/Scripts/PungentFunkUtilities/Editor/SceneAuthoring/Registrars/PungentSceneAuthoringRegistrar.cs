using PungentFunk.Utilities.Editor.Scanning;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneAuthoring
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class PungentSceneAuthoringRegistrar
    {
        public const string PackageId = "com.pungentfunk.utilities.scene-authoring";
        public const string PackageDisplayName = "PungentFunk Utilities Scene Authoring";
        public const string LegacySceneGizmosPackageId = "com.pungentfunk.utilities.scene-gizmos";

        static PungentSceneAuthoringRegistrar()
        {
            RegisterAuditProviders();
        }

        public static void RegisterAuditProviders()
        {
            PungentAuditScanProviderRegistry.Register(new PungentSceneAuthoringAuditProvider());
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Create Spatial Path Instance", false, 27)]
        public static void CreateSpatialPathInstance()
        {
            GameObject go = new GameObject("Spatial Path Instance");
            go.transform.position = GetSceneCreationPosition();
            PungentPathInstance path = go.AddComponent<PungentPathInstance>();
            path.EnsureDefaultData();
            Undo.RegisterCreatedObjectUndo(go, "Create Spatial Path Instance");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("GameObject/PungentFunk Utilities/Scene Tools/Create Spatial Area Instance", false, 28)]
        public static void CreateSpatialAreaInstance()
        {
            GameObject go = new GameObject("Spatial Area Instance");
            go.transform.position = GetSceneCreationPosition();
            PungentAreaInstance area = go.AddComponent<PungentAreaInstance>();
            area.EnsureDefaultData();
            Undo.RegisterCreatedObjectUndo(go, "Create Spatial Area Instance");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Create Spatial Path Instance", priority = 112)]
        private static void CreateSpatialPathInstanceFromTools()
        {
            CreateSpatialPathInstance();
        }

        [MenuItem("Tools/PungentFunk Utilities/Scene Tools/Create Spatial Area Instance", priority = 113)]
        private static void CreateSpatialAreaInstanceFromTools()
        {
            CreateSpatialAreaInstance();
        }

        private static Vector3 GetSceneCreationPosition()
        {
            if (Selection.activeTransform != null)
                return Selection.activeTransform.position;

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                return sceneView.pivot;

            return Vector3.zero;
        }
    }
#endif
}
