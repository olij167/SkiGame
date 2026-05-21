using System;
using System.IO;
using PungentFunk.Utilities.SceneTools;
using UnityEditor;
using UnityEngine;

namespace PungentFunk.Utilities.Editor.SceneTools
{
#if UNITY_EDITOR
    internal enum PungentSpatialBakeBoundsSource
    {
        Projection,
        Selection,
        RenderersInOpenScenes,
        CollidersInOpenScenes,
        TerrainsInOpenScenes,
        Manual
    }

    [Serializable]
    internal sealed class PungentSpatialBackgroundBakeSettings
    {
        public PungentSpatialBakeBoundsSource boundsSource = PungentSpatialBakeBoundsSource.Projection;
        public bool safeAlignToProjection = true;
        public Bounds manualBounds = new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
        public float padding;
        public LayerMask cullingMask = ~0;
        public Vector2Int textureSize = new Vector2Int(1024, 1024);
        public Color clearColor = new Color(0f, 0f, 0f, 0f);
        public string outputFolder = "Assets/SpatialAuthoring";
        public string outputName = "SpatialBackground.png";
    }

    internal static class PungentSpatialBackgroundBakeUtility
    {
        public static bool TryResolveBounds(PungentSpatialAuthoringAsset asset, PungentSpatialBackgroundBakeSettings settings, out Bounds bounds, out string summary)
        {
            bounds = default;
            summary = string.Empty;
            if (settings == null)
                return false;

            if (settings.safeAlignToProjection && asset != null && asset.projection.IsValid)
            {
                bounds = asset.projection.GetWorldBounds();
                summary = "Projection bounds";
                Expand(ref bounds, settings.padding);
                return true;
            }

            switch (settings.boundsSource)
            {
                case PungentSpatialBakeBoundsSource.Projection:
                    if (asset != null && asset.projection.IsValid)
                    {
                        bounds = asset.projection.GetWorldBounds();
                        summary = "Projection bounds";
                        Expand(ref bounds, settings.padding);
                        return true;
                    }
                    return false;
                case PungentSpatialBakeBoundsSource.Selection:
                    return TryBoundsFromSelection(settings.padding, out bounds, out summary);
                case PungentSpatialBakeBoundsSource.RenderersInOpenScenes:
                    return TryBoundsFromObjects(UnityEngine.Object.FindObjectsOfType<Renderer>(), settings.padding, out bounds, out summary);
                case PungentSpatialBakeBoundsSource.CollidersInOpenScenes:
                    return TryBoundsFromObjects(UnityEngine.Object.FindObjectsOfType<Collider>(), settings.padding, out bounds, out summary);
                case PungentSpatialBakeBoundsSource.TerrainsInOpenScenes:
                    return TryBoundsFromTerrains(settings.padding, out bounds, out summary);
                case PungentSpatialBakeBoundsSource.Manual:
                    bounds = settings.manualBounds;
                    summary = "Manual bounds";
                    Expand(ref bounds, settings.padding);
                    return bounds.size.sqrMagnitude > 0.0001f;
                default:
                    return false;
            }
        }

        public static bool Bake(PungentSpatialAuthoringAsset asset, PungentSpatialBackgroundBakeSettings settings, out string message)
        {
            message = string.Empty;
            if (asset == null)
            {
                message = "Assign a Spatial Authoring Asset before baking.";
                return false;
            }

            if (!TryResolveBounds(asset, settings, out Bounds captureBounds, out string boundsSummary))
            {
                message = "No valid bake bounds could be resolved.";
                return false;
            }

            string folder = string.IsNullOrWhiteSpace(settings.outputFolder) ? "Assets/SpatialAuthoring" : settings.outputFolder.Trim();
            string fileName = string.IsNullOrWhiteSpace(settings.outputName) ? "SpatialBackground.png" : settings.outputName.Trim();
            if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                fileName += ".png";

            if (!folder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                message = "Output folder must be inside Assets.";
                return false;
            }

            string assetPath = $"{folder.TrimEnd('/', '\\')}/{fileName}";
            string fullFolder = Path.Combine(Application.dataPath, folder.Substring("Assets".Length).TrimStart('/', '\\'));
            Directory.CreateDirectory(fullFolder);

            int width = Mathf.Clamp(settings.textureSize.x, 64, 8192);
            int height = Mathf.Clamp(settings.textureSize.y, 64, 8192);
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            GameObject cameraObject = null;

            try
            {
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                cameraObject = new GameObject("__PungentSpatialBackgroundBakeCamera__")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.hideFlags = HideFlags.HideAndDontSave;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = settings.clearColor;
                camera.cullingMask = settings.cullingMask;
                camera.targetTexture = renderTexture;

                Vector3 center = captureBounds.center;
                camera.transform.position = new Vector3(center.x, captureBounds.max.y + Mathf.Max(10f, captureBounds.size.y + 10f), center.z);
                camera.transform.rotation = Quaternion.Euler(90f, asset.projection.yawDegrees, 0f);
                camera.orthographicSize = Mathf.Max(captureBounds.size.x, captureBounds.size.z) * 0.5f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Mathf.Max(100f, captureBounds.size.y + 100f);
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(Path.Combine(fullFolder, fileName), texture.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath);
                Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

                Undo.RecordObject(asset, "Bake Spatial Background");
                asset.backgroundTexture = imported;
                ComputeBackgroundUv(asset.projection, captureBounds, settings.safeAlignToProjection, out asset.backgroundUvMin, out asset.backgroundUvMax);
                asset.lastBackgroundBake = new PungentSpatialBackgroundBakeMetadata
                {
                    sourceSummary = boundsSummary,
                    outputAssetPath = assetPath,
                    textureSize = new Vector2Int(width, height),
                    captureBounds = captureBounds,
                    safeAlignedToProjection = settings.safeAlignToProjection,
                    unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                message = $"Baked background to {assetPath}.";
                return true;
            }
            finally
            {
                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                if (renderTexture != null)
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool TryBoundsFromSelection(float padding, out Bounds bounds, out string summary)
        {
            bounds = default;
            summary = "Selection";
            bool initialized = false;
            Transform[] transforms = Selection.transforms;
            for (int i = 0; i < transforms.Length; i++)
            {
                Renderer renderer = transforms[i].GetComponentInChildren<Renderer>();
                Collider collider = transforms[i].GetComponentInChildren<Collider>();
                Bounds candidate = renderer != null ? renderer.bounds : collider != null ? collider.bounds : new Bounds(transforms[i].position, Vector3.one);
                if (!initialized)
                {
                    bounds = candidate;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            Expand(ref bounds, padding);
            return initialized;
        }

        private static bool TryBoundsFromObjects<T>(T[] objects, float padding, out Bounds bounds, out string summary) where T : Component
        {
            bounds = default;
            summary = typeof(T).Name + " bounds";
            bool initialized = false;
            for (int i = 0; i < objects.Length; i++)
            {
                Component component = objects[i];
                if (component == null || EditorUtility.IsPersistent(component))
                    continue;

                Bounds candidate;
                if (component is Renderer renderer)
                    candidate = renderer.bounds;
                else if (component is Collider collider)
                    candidate = collider.bounds;
                else
                    candidate = new Bounds(component.transform.position, Vector3.zero);

                if (!initialized)
                {
                    bounds = candidate;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            Expand(ref bounds, padding);
            return initialized;
        }

        private static bool TryBoundsFromTerrains(float padding, out Bounds bounds, out string summary)
        {
            bounds = default;
            summary = "Terrain bounds";
            Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
            bool initialized = false;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null || EditorUtility.IsPersistent(terrain))
                    continue;

                Bounds candidate = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
                if (!initialized)
                {
                    bounds = candidate;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }

            Expand(ref bounds, padding);
            return initialized;
        }

        private static void Expand(ref Bounds bounds, float padding)
        {
            if (padding <= 0f)
                return;
            bounds.Expand(new Vector3(padding * 2f, 0f, padding * 2f));
        }

        private static void ComputeBackgroundUv(PungentSpatialProjection projection, Bounds captureBounds, bool safeAligned, out Vector2 uvMin, out Vector2 uvMax)
        {
            uvMin = Vector2.zero;
            uvMax = Vector2.one;
            if (safeAligned || !projection.IsValid || captureBounds.size.x <= 0.001f || captureBounds.size.z <= 0.001f)
                return;

            Bounds projectionBounds = projection.GetWorldBounds(captureBounds.min.y, captureBounds.max.y);
            uvMin = new Vector2(
                Mathf.InverseLerp(captureBounds.min.x, captureBounds.max.x, projectionBounds.min.x),
                Mathf.InverseLerp(captureBounds.min.z, captureBounds.max.z, projectionBounds.min.z));
            uvMax = new Vector2(
                Mathf.InverseLerp(captureBounds.min.x, captureBounds.max.x, projectionBounds.max.x),
                Mathf.InverseLerp(captureBounds.min.z, captureBounds.max.z, projectionBounds.max.z));
        }
    }
#endif
}
