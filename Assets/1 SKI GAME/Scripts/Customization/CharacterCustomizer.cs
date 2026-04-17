using UnityEngine;
using UnityEngine.Rendering.Universal; // for DecalProjector

public class CharacterCustomizer : MonoBehaviour
{
    [Header("Base Renderers (3D URP)")]
    [SerializeField] private Renderer[] skinRenderers;

    [Header("Skin Materials")]
    [SerializeField] private Texture[] skinPatterns;
    [SerializeField] private int selectedTextureIndex;
    [SerializeField] private Color skinColor;
    [Tooltip("Color property used by the skin material (e.g., _BaseColor or Base_Colour).")]
    [SerializeField] private string skinTexturePropertyName = "_BaseMap";
    [SerializeField] private string skinColorPropertyName = "_BaseColor";

    [Header("Eye Decals")]
    [Tooltip("Left eye URP DecalProjector.")]
    [SerializeField] private DecalProjector leftEyeProjector;

    [Tooltip("Right eye URP DecalProjector.")]
    [SerializeField] private DecalProjector rightEyeProjector;

    [Tooltip("Base eye decal material. Will be cloned at runtime per character.")]
    [SerializeField] private Material eyeDecalBaseMaterial;

    [Tooltip("Texture property for the eye sprite (e.g., Base_Map or _BaseMap).")]
    [SerializeField] private string eyeBaseMapPropertyName = "Base_Map";

    [Tooltip("Color property for the eye color (e.g., Base_Colour or _BaseColor).")]
    [SerializeField] private string eyeBaseColorPropertyName = "Base_Colour";

    [Header("Eye Presentation")]
    [SerializeField] private float eyeSizeScale = 3f;
    [SerializeField] private Color eyeOutlineColour = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private float eyeOutlineScaleMultiplier = 1.035f;

    [Header("Eye Options")]
    [SerializeField] private int selectedEyeOption;
    [SerializeField] private Color eyeColour;
    [SerializeField] private Sprite[] eyeOptions;

    [Header("Anchors for Wearables")]
    [SerializeField] private Transform headAnchor; // hats
    [SerializeField] private Transform bodyAnchor; // jackets

    [Header("Hat Options")]
    [SerializeField] private GameObject[] hatPrefabs;

    [Header("Jacket Options")]
    [SerializeField] private GameObject[] jacketPrefabs;

    // Runtime state
    private Material _eyeMaterialInstance;
    private Material _eyeOutlineMaterialInstance;
    private int _eyeBaseMapId;
    private int _eyeBaseColorId;
    private int _skinColorPropertyId;
    private int _eyeOutlineBaseMapId;
    private int _eyeOutlineBaseColorId;
    private DecalProjector _leftEyeOutlineProjector;
    private DecalProjector _rightEyeOutlineProjector;
    private Vector3 _leftEyeBaseSize;
    private Vector3 _rightEyeBaseSize;

    private GameObject _currentHatInstance;
    private GameObject _currentJacketInstance;
    private WearableAttachment _currentHatAttachment;
    private WearableAttachment _currentJacketAttachment;

    private void Awake()
    {
        CacheEyeProjectorBaseSizes();

        if (!string.IsNullOrEmpty(eyeBaseMapPropertyName))
            _eyeBaseMapId = Shader.PropertyToID(eyeBaseMapPropertyName);

        if (!string.IsNullOrEmpty(eyeBaseColorPropertyName))
            _eyeBaseColorId = Shader.PropertyToID(eyeBaseColorPropertyName);

        if (!string.IsNullOrEmpty(skinColorPropertyName))
            _skinColorPropertyId = Shader.PropertyToID(skinColorPropertyName);
    }

    [ContextMenu("Randomise All")]
    public void SetRandomStyle()
    {
        SetRandomEyeStyle();
        SetRandomSkinStyle();
    }

    // CharacterCustomizer.cs  (inside class)

    #region Eyes (Decals)

    private static readonly string[] EyeColorPropCandidates = { "Base_Colour", "_BaseColor", "BaseColor", "_Color" };
    private static readonly string[] EyeMapPropCandidates = { "Base_Map", "_BaseMap", "BaseMap", "_MainTex" };

    private void EnsureEyeMaterialInstance()
    {
        if (_eyeMaterialInstance != null) return;

        if (eyeDecalBaseMaterial == null)
        {
            Debug.LogWarning("CharacterCustomizer: eyeDecalBaseMaterial is not assigned.", this);
            return;
        }

        _eyeMaterialInstance = new Material(eyeDecalBaseMaterial);

        ResolveEyePropertyIds();
        ApplyEyeMaterialToProjectors();
        EnsureEyeOutlineResources();
    }

    private void ResolveEyePropertyIds()
    {
        if (_eyeMaterialInstance == null) return;

        // Resolve Base Map
        _eyeBaseMapId = 0;
        foreach (var prop in EyeMapPropCandidates)
        {
            if (!string.IsNullOrEmpty(prop) && _eyeMaterialInstance.HasProperty(prop))
            {
                eyeBaseMapPropertyName = prop;
                _eyeBaseMapId = Shader.PropertyToID(prop);
                break;
            }
        }

        // Resolve Base Color
        _eyeBaseColorId = 0;
        foreach (var prop in EyeColorPropCandidates)
        {
            if (!string.IsNullOrEmpty(prop) && _eyeMaterialInstance.HasProperty(prop))
            {
                eyeBaseColorPropertyName = prop;
                _eyeBaseColorId = Shader.PropertyToID(prop);
                break;
            }
        }

#if UNITY_EDITOR
        if (_eyeBaseColorId == 0)
            Debug.LogWarning($"CharacterCustomizer: Eye decal material has no supported color property. Checked: {string.Join(", ", EyeColorPropCandidates)}", this);
        if (_eyeBaseMapId == 0)
            Debug.LogWarning($"CharacterCustomizer: Eye decal material has no supported map property. Checked: {string.Join(", ", EyeMapPropCandidates)}", this);
#endif
    }

    private void ApplyEyeMaterialToProjectors()
    {
        if (_eyeMaterialInstance == null) return;

        if (leftEyeProjector != null)
            leftEyeProjector.material = _eyeMaterialInstance;

        if (rightEyeProjector != null)
            rightEyeProjector.material = _eyeMaterialInstance;
    }

    private void CacheEyeProjectorBaseSizes()
    {
        _leftEyeBaseSize = leftEyeProjector != null ? leftEyeProjector.size : Vector3.one * 0.06f;
        _rightEyeBaseSize = rightEyeProjector != null ? rightEyeProjector.size : _leftEyeBaseSize;
    }

    private void EnsureEyeOutlineResources()
    {
        EnsureEyeOutlineProjector(ref _leftEyeOutlineProjector, leftEyeProjector, "LeftEyeOutline");
        EnsureEyeOutlineProjector(ref _rightEyeOutlineProjector, rightEyeProjector, "RightEyeOutline");

        if (_eyeOutlineMaterialInstance == null && eyeDecalBaseMaterial != null)
        {
            _eyeOutlineMaterialInstance = new Material(eyeDecalBaseMaterial);
            ResolveEyeOutlinePropertyIds();
        }

        ApplyEyeOutlineMaterialToProjectors();
        ApplyEyeOutlineColorInternal();
        ApplyEyeSizeInternal();
    }

    private void EnsureEyeOutlineProjector(ref DecalProjector outlineProjector, DecalProjector sourceProjector, string objectName)
    {
        if (outlineProjector != null || sourceProjector == null)
            return;

        var outlineObject = new GameObject(objectName);
        outlineObject.transform.SetParent(sourceProjector.transform.parent, false);
        outlineObject.transform.localPosition = sourceProjector.transform.localPosition;
        outlineObject.transform.localRotation = sourceProjector.transform.localRotation;
        outlineObject.transform.localScale = sourceProjector.transform.localScale;

        outlineProjector = outlineObject.AddComponent<DecalProjector>();
        outlineProjector.pivot = sourceProjector.pivot;
        outlineProjector.size = ScaleEyeProjectorSize(sourceProjector.size, eyeOutlineScaleMultiplier);
    }

    private void ResolveEyeOutlinePropertyIds()
    {
        if (_eyeOutlineMaterialInstance == null) return;

        _eyeOutlineBaseMapId = 0;
        foreach (var prop in EyeMapPropCandidates)
        {
            if (!string.IsNullOrEmpty(prop) && _eyeOutlineMaterialInstance.HasProperty(prop))
            {
                _eyeOutlineBaseMapId = Shader.PropertyToID(prop);
                break;
            }
        }

        _eyeOutlineBaseColorId = 0;
        foreach (var prop in EyeColorPropCandidates)
        {
            if (!string.IsNullOrEmpty(prop) && _eyeOutlineMaterialInstance.HasProperty(prop))
            {
                _eyeOutlineBaseColorId = Shader.PropertyToID(prop);
                break;
            }
        }
    }

    private void ApplyEyeOutlineMaterialToProjectors()
    {
        if (_eyeOutlineMaterialInstance == null) return;

        if (_leftEyeOutlineProjector != null)
            _leftEyeOutlineProjector.material = _eyeOutlineMaterialInstance;

        if (_rightEyeOutlineProjector != null)
            _rightEyeOutlineProjector.material = _eyeOutlineMaterialInstance;
    }

    private void ApplyEyeOutlineColorInternal()
    {
        if (_eyeOutlineMaterialInstance == null) return;
        if (_eyeOutlineBaseColorId == 0)
            ResolveEyeOutlinePropertyIds();

        if (_eyeOutlineBaseColorId != 0)
            _eyeOutlineMaterialInstance.SetColor(_eyeOutlineBaseColorId, eyeOutlineColour);
    }

    private void ApplyEyeTextureToMaterials(Texture texture)
    {
        if (texture == null)
            return;

        if (_eyeBaseMapId != 0 && _eyeMaterialInstance != null)
            _eyeMaterialInstance.SetTexture(_eyeBaseMapId, texture);

        if (_eyeOutlineBaseMapId != 0 && _eyeOutlineMaterialInstance != null)
            _eyeOutlineMaterialInstance.SetTexture(_eyeOutlineBaseMapId, texture);
    }

    private void ApplyEyeSizeInternal()
    {
        float sizeStep = Mathf.Clamp(Mathf.Round(eyeSizeScale), 1f, 10f);
        float xyScale = ResolveEyeSizeStepToScale(sizeStep);
        float outlineScale = Mathf.Min(xyScale * eyeOutlineScaleMultiplier, Mathf.Min(0.985f, xyScale + 0.01f));

        if (leftEyeProjector != null)
            leftEyeProjector.size = ScaleEyeProjectorSize(_leftEyeBaseSize, xyScale);

        if (rightEyeProjector != null)
            rightEyeProjector.size = ScaleEyeProjectorSize(_rightEyeBaseSize, xyScale);

        if (_leftEyeOutlineProjector != null)
            _leftEyeOutlineProjector.size = ScaleEyeProjectorSize(_leftEyeBaseSize, outlineScale);

        if (_rightEyeOutlineProjector != null)
            _rightEyeOutlineProjector.size = ScaleEyeProjectorSize(_rightEyeBaseSize, outlineScale);
    }

    private static Vector3 ScaleEyeProjectorSize(Vector3 baseSize, float xyScale)
    {
        return new Vector3(baseSize.x * xyScale, baseSize.y * xyScale, baseSize.z);
    }

    private static float ResolveEyeSizeStepToScale(float sizeStep)
    {
        switch (Mathf.Clamp(Mathf.RoundToInt(sizeStep), 1, 5))
        {
            case 1: return 0.78f;
            case 2: return 0.84f;
            case 3: return 0.89f;
            case 4: return 0.94f;
            default: return 0.975f;
        }
    }

    [ContextMenu("Randomise Eye Style")]
    public void SetRandomEyeStyle()
    {
        selectedEyeOption = Random.Range(0, eyeOptions.Length);
        eyeColour = Random.ColorHSV(0f, 1f, 0.25f, 1f, 0.1f, .75f);

        SetEyeStyle(selectedEyeOption);
    }

    public void SetEyeStyle(int index)
    {
        if (eyeOptions == null || eyeOptions.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, eyeOptions.Length - 1);
        var option = eyeOptions[index];

        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null) return;

        // Safety: if base material changed at runtime, re-resolve once
        if (_eyeBaseColorId == 0 || _eyeBaseMapId == 0)
            ResolveEyePropertyIds();

        EnsureEyeOutlineResources();

        selectedEyeOption = index;

        // Texture
        if (option != null && _eyeBaseMapId != 0)
        {
            ApplyEyeTextureToMaterials(option.texture);
        }

        // Color (uses current field)
        if (_eyeBaseColorId != 0)
        {
            _eyeMaterialInstance.SetColor(_eyeBaseColorId, eyeColour);
        }

        ApplyEyeMaterialToProjectors();
        ApplyEyeOutlineMaterialToProjectors();
        ApplyEyeOutlineColorInternal();
        ApplyEyeSizeInternal();
    }

    public void SetEyeColor(Color color)
    {
        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null) return;

        if (_eyeBaseColorId == 0)
            ResolveEyePropertyIds();

        eyeColour = color;

        if (_eyeBaseColorId != 0)
            _eyeMaterialInstance.SetColor(_eyeBaseColorId, color);

        // Projectors already share the same instance, but keeping this is harmless.
        ApplyEyeMaterialToProjectors();
    }

    public void SetEyeOutlineColor(Color color)
    {
        EnsureEyeMaterialInstance();
        EnsureEyeOutlineResources();

        eyeOutlineColour = color;
        ApplyEyeOutlineColorInternal();
    }

    public void SetEyeSize(float scale)
    {
        eyeSizeScale = Mathf.Clamp(Mathf.Round(scale), 1f, 10f);
        ApplyEyeSizeInternal();
    }

    #endregion

    #region Skin
    [ContextMenu("Update Skin Style")]
    public void SetSkinStyle()
    {
        SetSkinTexture(selectedTextureIndex);
        if (Application.isPlaying)
        {
            SetSkinColor(skinColor);
        }
    }

    [ContextMenu("Randomise Skin Style")]
    public void SetRandomSkinStyle()
    {
        selectedTextureIndex = Random.Range(0, skinPatterns.Length);
        skinColor = Random.ColorHSV(0f, 1f, 0.75f, 1f, 0.75f, 1f);

        SetSkinStyle();
    }

    [ContextMenu("Set Skin Texture")]
    public void SetSkinTexture(int index)
    {
        if (skinRenderers == null || skinPatterns == null || skinPatterns.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, skinPatterns.Length - 1);
        var tex = skinPatterns[index];
        if (tex == null) return;
        selectedTextureIndex = index;
        // Assign a shared material or an instance depending on your needs.
        // Here we use .material to get a unique instance per character.
        foreach ( var renderer in skinRenderers ) 
            renderer.material.SetTexture(skinTexturePropertyName, tex);
    }

    [ContextMenu("Set Skin Colour (during play only")]
    public void SetSkinColor(Color color)
    {
        if (!Application.isPlaying) return;
        if (skinRenderers == null) return;
        if (_skinColorPropertyId == 0) return;

        skinColor = color;

        foreach (var renderer in skinRenderers)
        {
            var mat = renderer.material;
            if (mat != null && mat.HasProperty(_skinColorPropertyId))
            {
                mat.SetColor(_skinColorPropertyId, color);
            }
        }
    }

    public Texture2D GetSkinPatternTexture2D(int index)
    {
        if (skinPatterns == null || skinPatterns.Length == 0) return null;
        index = Mathf.Clamp(index, 0, skinPatterns.Length - 1);
        return skinPatterns[index] as Texture2D;
    }

    public Texture2D GetCurrentSkinPatternTexture2D()
    {
        return GetSkinPatternTexture2D(selectedTextureIndex);
    }

    #endregion

    #region Hats

    public void SetHat(int index)
    {
        AttachWearable(
            hatPrefabs,
            index,
            headAnchor,
            ref _currentHatInstance,
            ref _currentHatAttachment);
    }

    public void ClearHat()
    {
        if (_currentHatInstance != null)
            Destroy(_currentHatInstance);

        _currentHatInstance = null;
        _currentHatAttachment = null;
    }

    public void SetHatColor(Color color)
    {
        if (_currentHatAttachment == null) return;
        _currentHatAttachment.SetColor(color);
    }

    public void SetHatMaterial(Material material)
    {
        if (_currentHatAttachment == null || material == null) return;
        _currentHatAttachment.SetMaterial(material);
    }

    public void SetHatPatternTexture(Texture tex)
    {
        if (_currentHatAttachment == null) return;
        _currentHatAttachment.SetTexture(tex);
    }


    #endregion

    #region Jackets

    public void SetJacket(int index)
    {
        AttachWearable(
            jacketPrefabs,
            index,
            bodyAnchor,
            ref _currentJacketInstance,
            ref _currentJacketAttachment);
    }

    public void ClearJacket()
    {
        if (_currentJacketInstance != null)
            Destroy(_currentJacketInstance);

        _currentJacketInstance = null;
        _currentJacketAttachment = null;
    }

    public void SetJacketColor(Color color)
    {
        if (_currentJacketAttachment == null) return;
        _currentJacketAttachment.SetColor(color);
    }

    public void SetHatChannelColor(string channelId, Color color)
    {
        if (_currentHatAttachment == null) return;
        _currentHatAttachment.SetChannelColor(channelId, color);
    }
    public void SetJacketChannelColor(string channelId, Color color)
    {
        if (_currentJacketAttachment == null) return;
        _currentJacketAttachment.SetChannelColor(channelId, color);
    }

    public void SetJacketMaterial(Material material)
    {
        if (_currentJacketAttachment == null || material == null) return;
        _currentJacketAttachment.SetMaterial(material);
    }

    public void SetJacketPatternTexture(Texture tex)
    {
        if (_currentJacketAttachment == null) return;
        _currentJacketAttachment.SetTexture(tex);
    }

    #endregion

    #region Wearable attachment helper

    private void AttachWearable(
     GameObject[] prefabArray,
     int index,
     Transform anchor,
     ref GameObject currentInstance,
     ref WearableAttachment currentAttachment)
    {
        if (anchor == null || prefabArray == null || prefabArray.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, prefabArray.Length - 1);
        var prefab = prefabArray[index];
        if (prefab == null) return;

        if (currentInstance != null)
            Destroy(currentInstance);

        // Instantiate under anchor in local space
        currentInstance = Instantiate(prefab, anchor, worldPositionStays: false);
        currentAttachment = currentInstance.GetComponent<WearableAttachment>();

        if (currentAttachment == null)
            return;

        var mount = currentAttachment.mount;
        if (mount == null)
            return; // allow prefab-authored root pose as fallback

        // IMPORTANT: compute mount pose relative to the wearable ROOT, even if mount is nested.
        var rootT = currentInstance.transform;

        // mount pose expressed in root-local space
        Vector3 mountPosRoot = rootT.InverseTransformPoint(mount.position);
        Quaternion mountRotRoot = Quaternion.Inverse(rootT.rotation) * mount.rotation;

        // we want: rootLocal * mountLocal == identity
        Quaternion rootLocalRot = Quaternion.Inverse(mountRotRoot);
        Vector3 rootLocalPos = -(rootLocalRot * mountPosRoot);

        rootT.localRotation = rootLocalRot;
        rootT.localPosition = rootLocalPos;
    }


    #endregion

    public Color GetSkinColor() => skinColor;
    public Color GetEyeColor() => eyeColour;
    public int GetSkinPatternIndex() => selectedTextureIndex;
    public int GetEyeOptionIndex() => selectedEyeOption;

    public void SetEyeSprite(Sprite sprite)
    {
        if (sprite == null) return;
        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null) return;

        if (_eyeBaseMapId == 0) ResolveEyePropertyIds();
        EnsureEyeOutlineResources();
        ApplyEyeTextureToMaterials(sprite.texture);

        ApplyEyeMaterialToProjectors();
        ApplyEyeOutlineMaterialToProjectors();
    }

    public void SetSkinPatternTexture(Texture tex)
    {
        if (skinRenderers == null || tex == null) return;

        foreach (var r in skinRenderers)
        {
            if (r == null) continue;
            var m = r.material;
            if (m == null) continue;
            if (!string.IsNullOrEmpty(skinTexturePropertyName))
                m.SetTexture(skinTexturePropertyName, tex);
        }
    }

    public void SetHatPrefab(GameObject prefab)
    {
        AttachWearablePrefab(prefab, headAnchor, ref _currentHatInstance, ref _currentHatAttachment);
    }

    public void SetJacketPrefab(GameObject prefab)
    {
        AttachWearablePrefab(prefab, bodyAnchor, ref _currentJacketInstance, ref _currentJacketAttachment);
    }

    private void AttachWearablePrefab(
        GameObject prefab,
        Transform anchor,
        ref GameObject currentInstance,
        ref WearableAttachment currentAttachment)
    {
        // Clear if null => "None"
        if (prefab == null)
        {
            if (currentInstance != null) Destroy(currentInstance);
            currentInstance = null;
            currentAttachment = null;
            return;
        }

        if (anchor == null) return;

        if (currentInstance != null)
            Destroy(currentInstance);

        currentInstance = Instantiate(prefab, anchor, worldPositionStays: false);
        currentAttachment = currentInstance.GetComponent<WearableAttachment>();

        if (currentAttachment == null) return;

        var mount = currentAttachment.mount;
        if (mount == null) return;

        var rootT = currentInstance.transform;
        Vector3 mountPosRoot = rootT.InverseTransformPoint(mount.position);
        Quaternion mountRotRoot = Quaternion.Inverse(rootT.rotation) * mount.rotation;

        Quaternion rootLocalRot = Quaternion.Inverse(mountRotRoot);
        Vector3 rootLocalPos = -(rootLocalRot * mountPosRoot);

        rootT.localRotation = rootLocalRot;
        rootT.localPosition = rootLocalPos;
    }

    public GameObject GetHatPrefabAtIndex(int index)
    {
        if (hatPrefabs == null || hatPrefabs.Length == 0)
            return null;

        if (index < 0 || index >= hatPrefabs.Length)
            return null;

        return hatPrefabs[index];
    }

    public GameObject GetJacketPrefabAtIndex(int index)
    {
        if (jacketPrefabs == null || jacketPrefabs.Length == 0)
            return null;

        if (index < 0 || index >= jacketPrefabs.Length)
            return null;

        return jacketPrefabs[index];
    }

}

//[System.Serializable]
//public class EyeOption
//{
//    public string id;
//    public Sprite sprite;
//    public Color defaultColor = new Color(0,0,50);
//}
