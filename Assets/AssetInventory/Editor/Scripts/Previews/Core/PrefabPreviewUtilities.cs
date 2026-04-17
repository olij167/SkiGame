using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
#if USE_TEXTMESHPRO || UNITY_2023_1_OR_NEWER
using TMPro;
#endif

namespace AssetInventory
{
    /// <summary>
    /// Static utility methods for prefab preview generation.
    /// Contains common functionality shared across UI, VFX, Particle, Model, and FBX generators.
    /// </summary>
    public static class PrefabPreviewUtilities
    {
        // Prefab type enum (matches CustomPrefabPreviewGenerator)
        public enum PrefabType { UI, VFX, Particles, Model, FBX }

        // ====== Detection Methods ======

        public static PrefabType DetectPrefabType(GameObject prefab)
        {
            // Count 3D renderers once - used for Canvas, VFX, and Particles checks
            MeshRenderer[] meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            SkinnedMeshRenderer[] skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int total3DRenderers = meshRenderers.Length + skinnedMeshRenderers.Length;
            
            // Check for Canvas in children or parents (for temporary canvas case)
            bool hasCanvas = prefab.GetComponentInChildren<Canvas>() != null || prefab.GetComponentInParent<Canvas>() != null;
            
            if (hasCanvas)
            {
                // Check if prefab also has significant 3D content (MeshRenderers or SkinnedMeshRenderers)
                // Prefabs with both Canvas and 3D renderers should be treated as Model type,
                // as they're likely 3D scenes with UI overlays (e.g., adventure games, FPS demos)
                
                // If there are 3D renderers, treat as Model to use perspective camera and 3D bounds
                // Pure UI prefabs (Canvas only, no 3D content) will still be treated as UI
                if (total3DRenderers > 0)
                {
                    return PrefabType.Model;
                }
                
                return PrefabType.UI;
            }
            
            // Check for VFX, but also check for significant 3D content
            // Prefabs with both VFX AND substantial mesh content should be treated as Model
            if (prefab.GetComponentInChildren<VisualEffect>())
            {
                // If there are more than a few 3D renderers, this is primarily a 3D model
                // with VFX effects for decoration (magic effects, glows, etc.)
                if (total3DRenderers > 3)
                {
                    return PrefabType.Model;
                }
                
                return PrefabType.VFX;
            }
            
            // Check for particle systems, but also check for significant 3D content
            // Prefabs with both particles AND substantial mesh content (like a space station
            // with some particle effects for lighting/smoke) should be treated as Model
            if (prefab.GetComponentInChildren<ParticleSystem>())
            {
                // If there are more than a few 3D renderers, this is primarily a 3D model
                // with particle effects for decoration (dust, lights, smoke, etc.)
                if (total3DRenderers > 3)
                {
                    return PrefabType.Model;
                }
                
                return PrefabType.Particles;
            }
            
            return PrefabType.Model;
        }

        private static bool HasTrails(GameObject go)
        {
            // Check for TrailRenderer components
            if (go.GetComponentInChildren<TrailRenderer>() != null) return true;

            // Check for ParticleSystem Trails module
            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps.trails.enabled) return true;
            }

            return false;
        }

        private static bool HasSkinnedMeshRenderer(GameObject go)
        {
            return go.GetComponentInChildren<SkinnedMeshRenderer>() != null;
        }

        private static bool HasTextMeshPro(GameObject go)
        {
#if USE_TEXTMESHPRO || UNITY_2023_1_OR_NEWER
            return go.GetComponentInChildren<TMP_Text>(true) != null;
#else
            return false;
#endif
        }

        // ====== UI Utilities ======

        public static bool NeedsTemporaryCanvas(GameObject prefab)
        {
            // First check if this is primarily a 3D prefab with MeshRenderers/SkinnedMeshRenderers
            // 3D prefabs with UI overlays should NOT get a temporary canvas - they should be treated as Model type
            MeshRenderer[] meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            SkinnedMeshRenderer[] skinnedMeshRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int total3DRenderers = meshRenderers.Length + skinnedMeshRenderers.Length;
            
            if (total3DRenderers > 0)
            {
                // This is a 3D prefab (possibly with UI overlays) - don't create temporary canvas
                return false;
            }
            
            // Check if the prefab has any RectTransform components
            RectTransform[] rectTransforms = prefab.GetComponentsInChildren<RectTransform>(true);
            if (rectTransforms.Length == 0)
            {
                return false; // No UI elements at all
            }

            // Check if any of the RectTransforms lack a Canvas ancestor
            foreach (RectTransform rectTransform in rectTransforms)
            {
                Canvas canvasAncestor = rectTransform.GetComponentInParent<Canvas>();
                if (canvasAncestor == null)
                {
                    return true; // Found a RectTransform without Canvas - needs temporary canvas
                }
            }

            return false; // All RectTransforms have Canvas ancestors
        }

        public static GameObject CreateTemporaryCanvas(GameObject uiPrefab, Camera previewCamera, Scene targetScene)
        {
            // Create Canvas GameObject
            GameObject canvasGO = new GameObject("TemporaryCanvas");
            StageUtility.PlaceGameObjectInCurrentStage(canvasGO);
            SceneManager.MoveGameObjectToScene(canvasGO, targetScene);

            // Add Canvas component - use ScreenSpaceCamera for proper ContentSizeFitter support
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = previewCamera; // Assign the preview camera
            canvas.planeDistance = 1f; // Set canvas plane very close to camera (1 unit distance)

            // Add CanvasScaler for proper UI scaling
            UnityEngine.UI.CanvasScaler scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            // Set canvas RectTransform to a reasonable size
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920, 1080); // Standard resolution for layout calculations
            canvasRect.localScale = Vector3.one;

            // Add GraphicRaycaster (required for proper UI rendering)
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Reparent the UI prefab under the canvas
            // Use 'false' to preserve local position/rotation/scale
            uiPrefab.transform.SetParent(canvasGO.transform, false);

            // DON'T modify the prefab's anchor configuration!
            // Many UI elements (like navigation bars) rely on specific anchoring
            // (e.g., anchored to top and stretched horizontally) for correct layout.
            // Forcing center anchors breaks their layout and child element sizing.
            // The bounds calculation will handle framing the visible content correctly.

            return canvasGO;
        }

        public static void ConfigureCanvasForPreview(GameObject prefab, Camera previewCamera)
        {
            // Find all Canvas components in the prefab (including in children AND parents)
            // This is important for temporary canvases that parent UI elements
            List<Canvas> canvasList = new List<Canvas>();

            // Check children
            canvasList.AddRange(prefab.GetComponentsInChildren<Canvas>(true));

            // Check parents (for temporary canvas case)
            Canvas parentCanvas = prefab.GetComponentInParent<Canvas>();
            if (parentCanvas != null && !canvasList.Contains(parentCanvas))
            {
                canvasList.Add(parentCanvas);
            }

            Canvas[] canvases = canvasList.ToArray();

            foreach (Canvas canvas in canvases)
            {
                RenderMode originalMode = canvas.renderMode;

                switch (originalMode)
                {
                    case RenderMode.ScreenSpaceCamera:
                        // Screen Space - Camera: Convert to World Space after layout is complete
                        // (We use ScreenSpaceCamera initially for ContentSizeFitter support)
                        ConvertScreenSpaceCameraToWorldSpace(canvas, previewCamera);
                        break;

                    case RenderMode.ScreenSpaceOverlay:
                        // Screen Space - Overlay: Must convert to World Space
                        ConvertOverlayToWorldSpace(canvas, previewCamera);
                        break;

                    case RenderMode.WorldSpace:
                        // World Space: Already compatible, no changes needed
                        break;
                }
            }
        }

        private static void ConvertScreenSpaceCameraToWorldSpace(Canvas canvas, Camera previewCamera)
        {
            // Convert from ScreenSpaceCamera to WorldSpace for rendering
            // This preserves the layout calculated by ContentSizeFitter while using world units for camera positioning
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                // Scale the canvas down to make UI elements reasonably sized in world space
                // Canvas was 1920x1080 units (pixels), scale down by 1/100 to make it ~19x10 world units
                // This makes individual UI elements (which are typically 50-200 pixels) become 0.5-2 world units
                float pixelToWorldScale = 0.01f; // 1 world unit = 100 pixels
                canvasRect.localScale = new Vector3(pixelToWorldScale, pixelToWorldScale, 1f);
            }
        }

        private static void ConvertOverlayToWorldSpace(Canvas canvas, Camera previewCamera)
        {
            // Get the RectTransform
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                Debug.LogWarning("[ConvertOverlayToWorldSpace] Canvas has no RectTransform, cannot convert");
                return;
            }

            // Determine the reference resolution to use
            Vector2 referenceResolution = new Vector2(1920, 1080); // Default HD resolution

            // Check for CanvasScaler to get the original reference resolution
            UnityEngine.UI.CanvasScaler scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                referenceResolution = scaler.referenceResolution;
            }

            // Convert to World Space
            canvas.renderMode = RenderMode.WorldSpace;

            // Set the canvas size based on reference resolution
            canvasRect.sizeDelta = referenceResolution;

            // Position canvas at origin facing the camera direction
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;

            // Scale down to make it a reasonable size in world space
            float worldScale = 0.01f;
            canvasRect.localScale = new Vector3(worldScale, worldScale, worldScale);

            // Important: Update the canvas immediately to recalculate layout
            Canvas.ForceUpdateCanvases();
        }

        // ====== Particle System Utilities ======

        public static void HandleParticleSystems(GameObject go)
        {
            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length == 0) return;

            // Check if any system is prewarmed
            bool hasPrewarmedSystem = false;
            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps.main.prewarm)
                {
                    hasPrewarmedSystem = true;
                    break;
                }
            }

            // Calculate optimal time for all particle systems
            float optimalTime = CalculateOptimalParticleTime(go);

            // For prewarmed systems, simulate like normal systems for preview
            if (hasPrewarmedSystem)
            {
                foreach (ParticleSystem ps in particleSystems)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(false);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = AI.Config.cpParticleSeed;

                    ParticleSystem.MainModule main = ps.main;
                    float realTime = main.simulationSpeed > 0f ? optimalTime / main.simulationSpeed : optimalTime;

                    ps.Simulate(realTime, true, true, false);
                    ps.Play(true);
                }
                return;
            }

            // For non-prewarmed systems
            foreach (ParticleSystem ps in particleSystems)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(false);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = AI.Config.cpParticleSeed;

                ParticleSystem.MainModule main = ps.main;
                float realTime = main.simulationSpeed > 0f ? optimalTime / main.simulationSpeed : optimalTime;

                ps.Simulate(realTime, true, true, false);
                ps.Play(true);
            }
        }

        public static float CalculateOptimalParticleTime(GameObject go)
        {
            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length == 0) return 0f;

            float maxStartLifetime = 0f;
            float maxStartDelay = 0f;
            float minDuration = float.MaxValue;

            foreach (ParticleSystem ps in particleSystems)
            {
                ParticleSystem.MainModule main = ps.main;

                // Get start lifetime
                float startLifetime = main.startLifetime.constant;
                if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    startLifetime = (main.startLifetime.constantMin + main.startLifetime.constantMax) / 2f;
                }
                maxStartLifetime = Mathf.Max(maxStartLifetime, startLifetime);

                // Get start delay
                float startDelay = main.startDelay.constant;
                if (main.startDelay.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    startDelay = (main.startDelay.constantMin + main.startDelay.constantMax) / 2f;
                }
                maxStartDelay = Mathf.Max(maxStartDelay, startDelay);

                // Get duration
                float duration = main.duration;
                minDuration = Mathf.Min(minDuration, duration);
            }

            // Calculate optimal time
            float optimalTime;
            if (minDuration < maxStartLifetime)
            {
                // Short burst effect
                optimalTime = maxStartDelay + (maxStartLifetime * 0.5f);
            }
            else
            {
                // Continuous effect
                optimalTime = maxStartDelay + Mathf.Min(minDuration * 0.5f, maxStartLifetime);
            }

            return Mathf.Max(0.1f, optimalTime); // At least 0.1s
        }

        public static float GetParticleSystemDuration(GameObject go)
        {
            ParticleSystem[] particleSystems = go.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems.Length == 0) return 1f;

            float maxDuration = 0f;
            foreach (ParticleSystem ps in particleSystems)
            {
                float duration = ps.main.duration + ps.main.startLifetime.constant + ps.main.startDelay.constant;
                maxDuration = Mathf.Max(maxDuration, duration);
            }

            return Mathf.Max(0.5f, maxDuration);
        }

        // ====== Other Utilities ======
        // Note: VFX utilities (HandleVFXSystems, GetVFXDuration) are in CustomPrefabPreviewGenerator

        public static void ForceLODLevel0(GameObject go)
        {
            LODGroup[] lodGroups = go.GetComponentsInChildren<LODGroup>();
            if (lodGroups.Length == 0) return;

            foreach (LODGroup lodGroup in lodGroups)
            {
                lodGroup.ForceLOD(0);
            }
        }

        /// <summary>
        /// Converts materials on a GameObject to use shaders compatible with the current render pipeline.
        /// Unity's built-in avatar models use the Standard shader which appears purple in URP/HDRP.
        /// Replaces incompatible materials with ones using the appropriate Lit shader.
        /// </summary>
        /// <param name="go">The GameObject whose materials should be converted.</param>
        public static void ConvertMaterialsToCurrentPipeline(GameObject go)
        {
            if (go == null) return;

            // Only need to convert if we're on SRP (URP or HDRP)
            bool isOnURP = AssetUtils.IsOnURP();
            bool isOnHDRP = AssetUtils.IsOnHDRP();

            if (!isOnURP && !isOnHDRP)
            {
                // Already on BIRP, no conversion needed
                return;
            }

            // Determine the correct shader for the current render pipeline
            Shader targetShader;
            if (isOnURP)
            {
                targetShader = Shader.Find("Universal Render Pipeline/Lit");
            }
            else
            {
                targetShader = Shader.Find("HDRP/Lit");
            }

            if (targetShader == null)
            {
                Debug.LogWarning("[Preview] Could not find Lit shader for current render pipeline");
                return;
            }

            // Replace materials on all renderers (including SkinnedMeshRenderer)
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                // Use sharedMaterials to avoid material leak warnings in edit mode
                Material[] sharedMats = renderer.sharedMaterials;
                Material[] newMaterials = new Material[sharedMats.Length];
                bool materialsChanged = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    Material originalMaterial = sharedMats[i];
                    if (originalMaterial == null)
                    {
                        // Null material slots render as magenta, which trips the error-shader
                        // detector (IsErrorShader). Replace with a neutral material that matches
                        // the preview background so null submeshes blend in instead.
                        Material bgMat = new Material(targetShader);
                        bgMat.hideFlags = HideFlags.HideAndDontSave;
                        Color bgColor = Color.gray; // fallback
                        string hex = AssetUtils.IsOnHDRP() ? AI.Config.cpBackgroundColorHDRP : AI.Config.cpBackgroundColor;
                        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color parsed))
                        {
                            bgColor = parsed;
                        }
                        if (bgMat.HasProperty("_BaseColor"))
                        {
                            bgMat.SetColor("_BaseColor", bgColor);
                        }
                        newMaterials[i] = bgMat;
                        materialsChanged = true;
                        continue;
                    }

                    // Check if the material uses an incompatible shader
                    Shader shader = originalMaterial.shader;
                    string shaderName = shader != null ? shader.name : "";
                    
                    // Check if shader is already compatible with the current render pipeline
                    bool isAlreadyCompatible = false;
                    if (isOnURP)
                    {
                        isAlreadyCompatible = shaderName.StartsWith("Universal Render Pipeline/") ||
                                             shaderName.StartsWith("Shader Graphs/");
                    }
                    else if (isOnHDRP)
                    {
                        isAlreadyCompatible = shaderName.StartsWith("HDRP/") ||
                                             shaderName.StartsWith("Shader Graphs/");
                    }
                    
                    // Replace any shader that isn't already compatible (including error shaders, Standard, etc.)
                    if (!isAlreadyCompatible)
                    {
                        // Create a new material with the compatible shader
                        Material newMaterial = new Material(targetShader);
                        newMaterial.hideFlags = HideFlags.HideAndDontSave; // Prevent leak warnings

                        // --- Color & albedo ---
                        if (originalMaterial.HasProperty("_Color") && newMaterial.HasProperty("_BaseColor"))
                        {
                            newMaterial.SetColor("_BaseColor", originalMaterial.GetColor("_Color"));
                        }

                        // --- Main texture ---
                        if (originalMaterial.HasProperty("_MainTex"))
                        {
                            Texture mainTex = originalMaterial.GetTexture("_MainTex");
                            if (mainTex != null && newMaterial.HasProperty("_BaseMap"))
                            {
                                newMaterial.SetTexture("_BaseMap", mainTex);
                            }

                            // Preserve tiling & offset
                            if (newMaterial.HasProperty("_BaseMap"))
                            {
                                newMaterial.SetTextureScale("_BaseMap", originalMaterial.GetTextureScale("_MainTex"));
                                newMaterial.SetTextureOffset("_BaseMap", originalMaterial.GetTextureOffset("_MainTex"));
                            }
                        }

                        // --- Metallic / Smoothness ---
                        if (originalMaterial.HasProperty("_Metallic") && newMaterial.HasProperty("_Metallic"))
                        {
                            newMaterial.SetFloat("_Metallic", originalMaterial.GetFloat("_Metallic"));
                        }
                        if (originalMaterial.HasProperty("_Glossiness") && newMaterial.HasProperty("_Smoothness"))
                        {
                            newMaterial.SetFloat("_Smoothness", originalMaterial.GetFloat("_Glossiness"));
                        }
                        if (originalMaterial.HasProperty("_MetallicGlossMap"))
                        {
                            Texture metallicMap = originalMaterial.GetTexture("_MetallicGlossMap");
                            if (metallicMap != null && newMaterial.HasProperty("_MetallicGlossMap"))
                            {
                                newMaterial.SetTexture("_MetallicGlossMap", metallicMap);
                            }
                        }

                        // --- Normal map ---
                        if (originalMaterial.HasProperty("_BumpMap"))
                        {
                            Texture bumpMap = originalMaterial.GetTexture("_BumpMap");
                            if (bumpMap != null && newMaterial.HasProperty("_BumpMap"))
                            {
                                newMaterial.SetTexture("_BumpMap", bumpMap);
                                newMaterial.EnableKeyword("_NORMALMAP");
                            }
                            if (originalMaterial.HasProperty("_BumpScale") && newMaterial.HasProperty("_BumpScale"))
                            {
                                newMaterial.SetFloat("_BumpScale", originalMaterial.GetFloat("_BumpScale"));
                            }
                        }

                        // --- Occlusion ---
                        if (originalMaterial.HasProperty("_OcclusionMap"))
                        {
                            Texture occlusionMap = originalMaterial.GetTexture("_OcclusionMap");
                            if (occlusionMap != null && newMaterial.HasProperty("_OcclusionMap"))
                            {
                                newMaterial.SetTexture("_OcclusionMap", occlusionMap);
                            }
                            if (originalMaterial.HasProperty("_OcclusionStrength") && newMaterial.HasProperty("_OcclusionStrength"))
                            {
                                newMaterial.SetFloat("_OcclusionStrength", originalMaterial.GetFloat("_OcclusionStrength"));
                            }
                        }

                        // --- Emission ---
                        if (originalMaterial.HasProperty("_EmissionColor") && newMaterial.HasProperty("_EmissionColor"))
                        {
                            Color emission = originalMaterial.GetColor("_EmissionColor");
                            if (emission.maxColorComponent > 0f)
                            {
                                newMaterial.SetColor("_EmissionColor", emission);
                                newMaterial.EnableKeyword("_EMISSION");
                                newMaterial.globalIlluminationFlags = originalMaterial.globalIlluminationFlags;
                            }
                        }
                        if (originalMaterial.HasProperty("_EmissionMap"))
                        {
                            Texture emissionMap = originalMaterial.GetTexture("_EmissionMap");
                            if (emissionMap != null && newMaterial.HasProperty("_EmissionMap"))
                            {
                                newMaterial.SetTexture("_EmissionMap", emissionMap);
                                newMaterial.EnableKeyword("_EMISSION");
                            }
                        }

                        // --- Alpha / transparency ---
                        if (originalMaterial.HasProperty("_Mode"))
                        {
                            int mode = (int)originalMaterial.GetFloat("_Mode");
                            // Standard shader: 0=Opaque, 1=Cutout, 2=Fade, 3=Transparent
                            if (mode == 1) // Cutout
                            {
                                newMaterial.SetFloat("_Surface", 0); // Opaque
                                newMaterial.EnableKeyword("_ALPHATEST_ON");
                                newMaterial.SetOverrideTag("RenderType", "TransparentCutout");
                                newMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                                if (originalMaterial.HasProperty("_Cutoff") && newMaterial.HasProperty("_Cutoff"))
                                {
                                    newMaterial.SetFloat("_Cutoff", originalMaterial.GetFloat("_Cutoff"));
                                }
                            }
                            else if (mode >= 2) // Fade or Transparent
                            {
                                newMaterial.SetFloat("_Surface", 1); // Transparent
                                newMaterial.SetFloat("_Blend", mode == 2 ? 0 : 1); // Alpha/Premultiply
                                newMaterial.SetOverrideTag("RenderType", "Transparent");
                                newMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                                newMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                newMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                newMaterial.SetInt("_ZWrite", 0);
                            }
                        }

                        newMaterials[i] = newMaterial;
                        materialsChanged = true;
                    }
                    else
                    {
                        newMaterials[i] = originalMaterial;
                    }
                }

                if (materialsChanged)
                {
                    renderer.sharedMaterials = newMaterials;
                }
            }
        }
    }
}
