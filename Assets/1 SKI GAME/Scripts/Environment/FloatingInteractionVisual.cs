using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FloatingInteractionVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int HeightMinId = Shader.PropertyToID("_HeightMin");
    private static readonly int HeightMaxId = Shader.PropertyToID("_HeightMax");

    [Header("Motion")]
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 45f, 0f);
    [SerializeField] private Vector3 hoverAxis = Vector3.up;
    [SerializeField] private float hoverAmplitude = 0.15f;
    [SerializeField] private float hoverFrequency = 1.25f;
    [SerializeField] private float hoverPhaseOffset = 0f;

    [Header("Source")]
    [SerializeField] private Renderer parentRendererSource;
    [SerializeField] private bool searchParentsIfMissing = true;

    [Header("Race Line Support")]
    [SerializeField] private bool preferRaceCourseLineColor = true;
    [SerializeField] private bool preserveAlphaFromRaceColor = true;

    [Header("Targets")]
    [SerializeField] private bool includeInactiveChildren = true;
    [SerializeField] private bool includeThisObjectRenderer = false;
    [SerializeField] private bool preserveTargetPropertyBlockData = true;
    [SerializeField] private bool alsoSetMaterialInstanceColor = true;
    [SerializeField] private bool updateHeightRangeForInteractionFadeShader = true;

    [Header("Sync")]
    [SerializeField] private bool syncOnStart = true;
    [SerializeField] private bool continuousSync = true;
    [SerializeField] private float syncInterval = 0.05f;
    [SerializeField] private bool syncInLateUpdate = true;

    private Renderer[] targetRenderers;
    private Vector3 initialLocalPosition;
    private float randomTimeOffset;
    private float syncTimer;

    private MaterialPropertyBlock sourceBlock;
    private MaterialPropertyBlock targetBlock;

    private RaceCourseLine cachedRaceCourseLine;
    private readonly Dictionary<Renderer, MeshFilter> meshFilterCache = new Dictionary<Renderer, MeshFilter>();

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        randomTimeOffset = Random.Range(0f, 1000f);

        sourceBlock = new MaterialPropertyBlock();
        targetBlock = new MaterialPropertyBlock();

        CacheTargetRenderers();

        if (parentRendererSource == null && searchParentsIfMissing)
            parentRendererSource = FindParentRenderer();

        CacheRaceCourseLine();
    }

    private void OnEnable()
    {
        syncTimer = 0f;

        if (sourceBlock == null)
            sourceBlock = new MaterialPropertyBlock();

        if (targetBlock == null)
            targetBlock = new MaterialPropertyBlock();

        CacheRaceCourseLine();
    }

    private void Start()
    {
        if (syncOnStart)
            SyncVisualsNow();
    }

    private void Update()
    {
        UpdateHover();
        UpdateRotation();

        if (!syncInLateUpdate)
            TickSync();
    }

    private void LateUpdate()
    {
        if (syncInLateUpdate)
            TickSync();
    }

    [ContextMenu("Sync Visuals Now")]
    public void SyncVisualsNow()
    {
        if (parentRendererSource == null && searchParentsIfMissing)
            parentRendererSource = FindParentRenderer();

        if (parentRendererSource == null)
            return;

        if (targetRenderers == null || targetRenderers.Length == 0)
            CacheTargetRenderers();

        CacheRaceCourseLine();

        if (!TryResolveSourceColor(out Color sourceColor))
            return;

        ApplyColorToTargets(sourceColor);
    }

    [ContextMenu("Refresh Child Renderers")]
    public void RefreshChildRenderers()
    {
        CacheTargetRenderers();
        CacheRaceCourseLine();
    }

    private void TickSync()
    {
        if (!continuousSync)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        syncTimer -= dt;

        if (syncTimer <= 0f)
        {
            syncTimer = Mathf.Max(0.01f, syncInterval);
            SyncVisualsNow();
        }
    }

    private void UpdateHover()
    {
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        float wave = Mathf.Sin((t + randomTimeOffset + hoverPhaseOffset) * hoverFrequency * Mathf.PI * 2f);

        Vector3 axis = hoverAxis.sqrMagnitude > 0.0001f ? hoverAxis.normalized : Vector3.up;
        transform.localPosition = initialLocalPosition + axis * (wave * hoverAmplitude);
    }

    private void UpdateRotation()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        transform.Rotate(rotationSpeed * dt, Space.Self);
    }

    private void CacheTargetRenderers()
    {
        meshFilterCache.Clear();

        Renderer[] found = GetComponentsInChildren<Renderer>(includeInactiveChildren);

        if (includeThisObjectRenderer)
        {
            targetRenderers = found;
        }
        else
        {
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject != gameObject)
                    count++;
            }

            targetRenderers = new Renderer[count];

            int index = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null && found[i].gameObject != gameObject)
                    targetRenderers[index++] = found[i];
            }
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null)
                continue;

            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null)
                meshFilterCache[r] = mf;
        }
    }

    private Renderer FindParentRenderer()
    {
        Transform current = transform.parent;

        while (current != null)
        {
            Renderer renderer = current.GetComponent<Renderer>();
            if (renderer != null)
                return renderer;

            current = current.parent;
        }

        return null;
    }

    private void CacheRaceCourseLine()
    {
        cachedRaceCourseLine = null;

        Transform current = transform;
        while (current != null)
        {
            RaceCourseLine race = current.GetComponent<RaceCourseLine>();
            if (race != null)
            {
                cachedRaceCourseLine = race;
                return;
            }

            current = current.parent;
        }
    }

    private bool TryResolveSourceColor(out Color color)
    {
        if (preferRaceCourseLineColor && cachedRaceCourseLine != null)
        {
            color = cachedRaceCourseLine.MapLineColor;
            return true;
        }

        color = ResolveColorFromRenderer(parentRendererSource);
        return true;
    }

    private Color ResolveColorFromRenderer(Renderer sourceRenderer)
    {
        if (sourceRenderer == null)
            return Color.white;

        Material sourceMaterial = null;
        if (sourceRenderer.sharedMaterials != null && sourceRenderer.sharedMaterials.Length > 0)
            sourceMaterial = sourceRenderer.sharedMaterials[0];

        sourceBlock.Clear();
        sourceRenderer.GetPropertyBlock(sourceBlock);

        if (sourceMaterial != null && sourceMaterial.HasProperty(BaseColorId))
        {
            Color value = sourceMaterial.GetColor(BaseColorId);
            Color blockValue = sourceBlock.GetColor(BaseColorId);
            if (blockValue != default)
                value = blockValue;
            return value;
        }

        if (sourceMaterial != null && sourceMaterial.HasProperty(ColorId))
        {
            Color value = sourceMaterial.GetColor(ColorId);
            Color blockValue = sourceBlock.GetColor(ColorId);
            if (blockValue != default)
                value = blockValue;
            return value;
        }

        return Color.white;
    }

    private void ApplyColorToTargets(Color sourceColor)
    {
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer target = targetRenderers[i];
            if (target == null || target == parentRendererSource)
                continue;

            Material[] targetShared = target.sharedMaterials;
            if (targetShared == null || targetShared.Length == 0)
                continue;

            Material[] targetRuntimeMaterials = alsoSetMaterialInstanceColor ? target.materials : null;

            for (int slot = 0; slot < targetShared.Length; slot++)
            {
                Material targetSharedMat = targetShared[slot];
                Material targetRuntimeMat = null;

                if (targetRuntimeMaterials != null && targetRuntimeMaterials.Length > 0)
                    targetRuntimeMat = targetRuntimeMaterials[Mathf.Min(slot, targetRuntimeMaterials.Length - 1)];

                if (preserveTargetPropertyBlockData)
                    target.GetPropertyBlock(targetBlock, slot);
                else
                    targetBlock.Clear();

                ApplyColorToMaterialAndBlock(targetSharedMat, targetRuntimeMat, sourceColor);

                if (updateHeightRangeForInteractionFadeShader)
                    ApplyOwnHeightRangeIfNeeded(target, targetSharedMat, targetRuntimeMat);

                target.SetPropertyBlock(targetBlock, slot);
            }
        }
    }

    private void ApplyColorToMaterialAndBlock(Material targetSharedMat, Material targetRuntimeMat, Color sourceColor)
    {
        if (targetSharedMat == null && targetRuntimeMat == null)
            return;

        bool supportsBaseColor =
            (targetRuntimeMat != null && targetRuntimeMat.HasProperty(BaseColorId)) ||
            (targetSharedMat != null && targetSharedMat.HasProperty(BaseColorId));

        bool supportsColor =
            (targetRuntimeMat != null && targetRuntimeMat.HasProperty(ColorId)) ||
            (targetSharedMat != null && targetSharedMat.HasProperty(ColorId));

        bool supportsEmission =
            (targetRuntimeMat != null && targetRuntimeMat.HasProperty(EmissionColorId)) ||
            (targetSharedMat != null && targetSharedMat.HasProperty(EmissionColorId));

        Color resolvedBase = sourceColor;
        Color resolvedColor = sourceColor;
        Color resolvedEmission = sourceColor;

        if (!preserveAlphaFromRaceColor)
        {
            if (supportsBaseColor)
            {
                Color existing = GetExistingColor(targetRuntimeMat, targetSharedMat, BaseColorId);
                resolvedBase.a = existing.a;
            }

            if (supportsColor)
            {
                Color existing = GetExistingColor(targetRuntimeMat, targetSharedMat, ColorId);
                resolvedColor.a = existing.a;
            }

            if (supportsEmission)
            {
                Color existing = GetExistingColor(targetRuntimeMat, targetSharedMat, EmissionColorId);
                resolvedEmission.a = existing.a;
            }
        }

        if (supportsBaseColor)
        {
            targetBlock.SetColor(BaseColorId, resolvedBase);
            if (targetRuntimeMat != null && targetRuntimeMat.HasProperty(BaseColorId))
                targetRuntimeMat.SetColor(BaseColorId, resolvedBase);
        }

        if (supportsColor)
        {
            targetBlock.SetColor(ColorId, resolvedColor);
            if (targetRuntimeMat != null && targetRuntimeMat.HasProperty(ColorId))
                targetRuntimeMat.SetColor(ColorId, resolvedColor);
        }

        if (supportsEmission)
        {
            targetBlock.SetColor(EmissionColorId, resolvedEmission);
            if (targetRuntimeMat != null && targetRuntimeMat.HasProperty(EmissionColorId))
                targetRuntimeMat.SetColor(EmissionColorId, resolvedEmission);
        }
    }

    private void ApplyOwnHeightRangeIfNeeded(Renderer target, Material targetSharedMat, Material targetRuntimeMat)
    {
        bool supportsHeightMin =
            (targetRuntimeMat != null && targetRuntimeMat.HasProperty(HeightMinId)) ||
            (targetSharedMat != null && targetSharedMat.HasProperty(HeightMinId));

        bool supportsHeightMax =
            (targetRuntimeMat != null && targetRuntimeMat.HasProperty(HeightMaxId)) ||
            (targetSharedMat != null && targetSharedMat.HasProperty(HeightMaxId));

        if (!supportsHeightMin || !supportsHeightMax)
            return;

        if (!meshFilterCache.TryGetValue(target, out MeshFilter mf) || mf == null || mf.sharedMesh == null)
            return;

        Bounds localBounds = mf.sharedMesh.bounds;
        targetBlock.SetFloat(HeightMinId, localBounds.min.y);
        targetBlock.SetFloat(HeightMaxId, localBounds.max.y);
    }

    private Color GetExistingColor(Material runtimeMat, Material sharedMat, int propertyId)
    {
        if (runtimeMat != null && runtimeMat.HasProperty(propertyId))
            return runtimeMat.GetColor(propertyId);

        if (sharedMat != null && sharedMat.HasProperty(propertyId))
            return sharedMat.GetColor(propertyId);

        return Color.white;
    }
}