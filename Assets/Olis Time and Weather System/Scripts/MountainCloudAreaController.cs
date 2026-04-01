using System.Collections.Generic;
using UnityEngine;

namespace TimeWeather
{
    public class MountainCloudAreaController : MonoBehaviour
    {
        [Header("References")]
        public WeatherController weatherController;
        public Transform fieldRoot;
        public List<MountainCloudVolume> cloudFields = new List<MountainCloudVolume>();
        public bool autoCollectChildFields = true;

        [Header("Area")]
        public Vector3 areaSize = new Vector3(1200f, 250f, 1200f);
        public Vector3 areaOffset = new Vector3(0f, 500f, 0f);

        [Header("Field Layout")]
        public bool autoFitFieldsToArea = true;
        [Range(1f, 2f)] public float horizontalFieldScaleMultiplier = 1.05f;
        [Range(0.5f, 2f)] public float verticalFieldScaleMultiplier = 1.0f;
        [Range(0f, 200f)] public float verticalLayerSpacing = 40f;
        [Range(0f, 1f)] public float layerHeightNormalizedOffset = 0.15f;
        public bool randomYawPerField = false;

        [Header("Field Motion")]
        public bool animateInternalFieldOffset = true;
        public float fieldDriftScale = 1.0f;
        public Vector2 manualDriftBias = Vector2.zero;

        [Header("Weather Response")]
        public bool autoDriveFromWeather = true;
        public float densityMultiplier = 1.0f;
        public float lightingMultiplier = 1.0f;
        public float absorptionMultiplier = 1.0f;
        public float softnessMultiplier = 1.0f;
        public float turbulenceMultiplier = 1.0f;
        public float warpMultiplier = 1.0f;
        public float alphaMultiplier = 1.0f;

        [Header("Visual Tuning")]
        public Color shadowTintOverride = new Color(0.62f, 0.68f, 0.76f, 1f);
        [Range(0f, 1f)] public float shadowTintBlend = 0.65f;

        [Header("Debug")]
        public bool drawBounds = true;

        private Bounds _worldBounds;
        private Vector2 _fieldOffset;
        private bool _collectedOnce;

        private void Awake()
        {
            if (weatherController == null)
                weatherController = WeatherController.instance;

            RebuildBounds();
            CollectFieldsIfNeeded(true);
        }

        private void Start()
        {
            RebuildBounds();
            CollectFieldsIfNeeded(true);
            LayoutFields();
            ForceRefreshAll();
        }

        private void OnValidate()
        {
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            areaSize.z = Mathf.Max(1f, areaSize.z);
            horizontalFieldScaleMultiplier = Mathf.Max(1f, horizontalFieldScaleMultiplier);
            verticalFieldScaleMultiplier = Mathf.Max(0.5f, verticalFieldScaleMultiplier);
            RebuildBounds();
        }

        private void Update()
        {
            if (weatherController == null)
                weatherController = WeatherController.instance;

            RebuildBounds();
            CollectFieldsIfNeeded(false);

            if (autoFitFieldsToArea)
                LayoutFields();

            if (autoDriveFromWeather)
                DriveFieldsFromWeather();
        }

        public void ForceRefreshAll()
        {
            for (int i = 0; i < cloudFields.Count; i++)
            {
                if (cloudFields[i] != null)
                    cloudFields[i].RefreshNow();
            }
        }

        public void RecollectFields()
        {
            _collectedOnce = false;
            CollectFieldsIfNeeded(true);
            LayoutFields();
            ForceRefreshAll();
        }

        private void CollectFieldsIfNeeded(bool force)
        {
            if (!autoCollectChildFields && !force && _collectedOnce)
                return;

            if (!autoCollectChildFields && !force)
                return;

            Transform root = fieldRoot != null ? fieldRoot : transform;

            cloudFields.RemoveAll(f => f == null);

            if (autoCollectChildFields)
            {
                var found = root.GetComponentsInChildren<MountainCloudVolume>(true);
                cloudFields.Clear();
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null)
                        cloudFields.Add(found[i]);
                }
            }

            _collectedOnce = true;
        }

        private void DriveFieldsFromWeather()
        {
            if (weatherController == null || cloudFields.Count == 0)
                return;

            if (!weatherController.TryGetCurrentVolumetricCloudState(
                out Color cloudTint,
                out float softness,
                out float coverageBias,
                out float turbulence,
                out float warpStrength,
                out float density,
                out float absorption,
                out float lightingStrength,
                out float volumeAlphaMultiplier,
                out float clusterScale,
                out float clusterDensity,
                out float detailStrength,
                out float edgeFade,
                out Vector2 drift))
            {
                return;
            }

            Vector2 totalDrift = (drift * fieldDriftScale) + manualDriftBias;
            if (animateInternalFieldOffset)
                _fieldOffset += totalDrift * Time.deltaTime;

            for (int i = 0; i < cloudFields.Count; i++)
            {
                var field = cloudFields[i];
                if (field == null) continue;

                Color shadowCol = Color.Lerp(cloudTint * 0.65f, shadowTintOverride, shadowTintBlend);

                Vector2 layerOffset = _fieldOffset + field.localFieldOffset;
                float seed = field.volumeSeed;

                field.SetFieldState(
                    cloudColour: cloudTint,
                    shadowColour: shadowCol,
                    density: density * densityMultiplier,
                    absorption: absorption * absorptionMultiplier,
                    lightingStrength: lightingStrength * lightingMultiplier,
                    cloudSpeed: new Vector4(totalDrift.x, totalDrift.y, 0f, 0f),
                    softness: Mathf.Max(0.01f, softness * softnessMultiplier),
                    coverageBias: coverageBias,
                    turbulence: Mathf.Clamp01(turbulence * turbulenceMultiplier),
                    warpStrength: Mathf.Clamp01(warpStrength * warpMultiplier),
                    alphaMultiplier: volumeAlphaMultiplier * alphaMultiplier,
                    clusterScale: clusterScale,
                    clusterDensity: clusterDensity,
                    detailStrength: detailStrength,
                    edgeFade: edgeFade,
                    fieldOffset: layerOffset,
                    seed: seed
                );
            }
        }

        private void LayoutFields()
        {
            if (cloudFields == null || cloudFields.Count == 0)
                return;

            Vector3 center = transform.position + areaOffset;

            float baseWidth = areaSize.x * horizontalFieldScaleMultiplier;
            float baseHeight = areaSize.y * verticalFieldScaleMultiplier;
            float baseDepth = areaSize.z * horizontalFieldScaleMultiplier;

            float normalizedYOffset = Mathf.Clamp01(layerHeightNormalizedOffset);
            float lowerY = center.y - areaSize.y * 0.5f;
            float upperY = center.y + areaSize.y * 0.5f;
            float baseY = Mathf.Lerp(lowerY, upperY, normalizedYOffset);

            int count = cloudFields.Count;
            float stackCenterOffset = (count - 1) * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var field = cloudFields[i];
                if (field == null) continue;

                Transform t = field.transform;

                float yOffset = (i - stackCenterOffset) * verticalLayerSpacing;
                t.position = new Vector3(center.x, baseY + yOffset, center.z);
                t.localScale = new Vector3(baseWidth, baseHeight, baseDepth);

                if (randomYawPerField)
                    t.rotation = Quaternion.Euler(0f, (i * 57.3f) % 360f, 0f);
                else
                    t.rotation = Quaternion.identity;

                field.localFieldOffset = new Vector2(i * 17.371f, i * 9.127f);
                field.volumeSeed = 31.73f + i * 101.11f;
            }
        }

        private void RebuildBounds()
        {
            Vector3 worldCenter = transform.position + areaOffset;
            _worldBounds = new Bounds(worldCenter, areaSize);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawBounds) return;

            RebuildBounds();
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.22f);
            Gizmos.DrawCube(_worldBounds.center, _worldBounds.size);
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireCube(_worldBounds.center, _worldBounds.size);
        }
    }
}