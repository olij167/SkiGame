using PungentFunk.Utilities.SceneTools;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    [InitializeOnLoad]
    public static class ModularPathSpawnerEditorUtility
    {
        static ModularPathSpawnerEditorUtility()
        {
            ModularPathSpawnerEditorHooks.BeginEditGroup = BeginEditGroup;
            ModularPathSpawnerEditorHooks.EndEditGroup = EndEditGroup;
            ModularPathSpawnerEditorHooks.SetDirty = EditorUtility.SetDirty;
            ModularPathSpawnerEditorHooks.RegisterCreatedObject = go => Undo.RegisterCreatedObjectUndo(go, "Create Generated Path Object");
            ModularPathSpawnerEditorHooks.InstantiatePrefab = InstantiatePrefab;
            ModularPathSpawnerEditorHooks.DestroyObject = DestroyObject;
            ModularPathSpawnerEditorHooks.ComputePrefabLength = ComputePrefabLength;
        }

        private static int BeginEditGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(string.IsNullOrWhiteSpace(name) ? "Editor Operation" : name);
            return group;
        }

        private static void EndEditGroup(int group)
        {
            if (group >= 0)
                Undo.CollapseUndoOperations(group);
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            if (prefab == null || parent == null || Application.isPlaying)
                return null;

            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
                return null;

            return PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        }

        private static bool DestroyObject(GameObject go)
        {
            if (go == null || Application.isPlaying)
                return false;

            Undo.DestroyObjectImmediate(go);
            return true;
        }

        private static float ComputePrefabLength(GameObject prefab, ModularPathSpawner.ForwardAxis axis)
        {
            if (prefab == null)
                return -1f;

            GameObject temp = null;
            try
            {
                temp = PrefabUtility.IsPartOfPrefabAsset(prefab)
                    ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
                    : Object.Instantiate(prefab);

                if (temp == null)
                    return -1f;

                temp.hideFlags = HideFlags.HideAndDontSave;
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                temp.transform.localScale = Vector3.one;

                Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0)
                    return -1f;

                Bounds localBounds = new Bounds();
                bool initialized = false;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    Bounds bounds = renderer.bounds;
                    Vector3 center = bounds.center;
                    Vector3 extents = bounds.extents;

                    for (int xi = -1; xi <= 1; xi += 2)
                    for (int yi = -1; yi <= 1; yi += 2)
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 cornerWorld = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                        Vector3 cornerLocal = temp.transform.InverseTransformPoint(cornerWorld);

                        if (!initialized)
                        {
                            localBounds = new Bounds(cornerLocal, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(cornerLocal);
                        }
                    }
                }

                return axis == ModularPathSpawner.ForwardAxis.Z ? localBounds.size.z : localBounds.size.x;
            }
            catch
            {
                return -1f;
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);
            }
        }
    }
#endif
}
