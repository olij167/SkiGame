using System;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    /// <summary>
    /// Runtime-safe delegate bridge used by editor assemblies to provide edit grouping, dirty, prefab, and measurement services.
    /// This file intentionally has no editor API references so ModularPathSpawner remains build-safe.
    /// </summary>
    public static class ModularPathSpawnerEditorHooks
    {
        public static Func<string, int> BeginEditGroup;
        public static Action<int> EndEditGroup;
        public static Action<UnityEngine.Object> SetDirty;
        public static Action<GameObject> RegisterCreatedObject;
        public static Func<GameObject, Transform, GameObject> InstantiatePrefab;
        public static Func<GameObject, bool> DestroyObject;
        public static Func<GameObject, ModularPathSpawner.ForwardAxis, float> ComputePrefabLength;
    }
}
