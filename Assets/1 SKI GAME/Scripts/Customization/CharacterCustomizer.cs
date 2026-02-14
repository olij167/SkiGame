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

    [Header("Eye Options")]
    [SerializeField] private int selectedEyeOption;
    [SerializeField] private Color eyeColour;
    [SerializeField] private Sprite[] eyeOptions;

    [Header("Anchors for Wearables")]
    [SerializeField] private Transform headAnchor; // hats
    [SerializeField] private Transform bodyAnchor; // cloaks

    [Header("Hat Options")]
    [SerializeField] private GameObject[] hatPrefabs;

    [Header("Cloak Options")]
    [SerializeField] private GameObject[] cloakPrefabs;

    // Runtime state
    private Material _eyeMaterialInstance;
    private int _eyeBaseMapId;
    private int _eyeBaseColorId;
    private int _skinColorPropertyId;

    private GameObject _currentHatInstance;
    private GameObject _currentCloakInstance;
    private WearableAttachment _currentHatAttachment;
    private WearableAttachment _currentCloakAttachment;

    private void Awake()
    {
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

        ResolveEyePropertyIds();     // ✅ NEW
        ApplyEyeMaterialToProjectors();
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

        selectedEyeOption = index;

        // Texture
        if (option != null && _eyeBaseMapId != 0)
        {
            _eyeMaterialInstance.SetTexture(_eyeBaseMapId, option.texture);
        }

        // Color (uses current field)
        if (_eyeBaseColorId != 0)
        {
            _eyeMaterialInstance.SetColor(_eyeBaseColorId, eyeColour);
        }

        ApplyEyeMaterialToProjectors();
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

    #region Cloaks

    public void SetCloak(int index)
    {
        AttachWearable(
            cloakPrefabs,
            index,
            bodyAnchor,
            ref _currentCloakInstance,
            ref _currentCloakAttachment);
    }

    public void ClearCloak()
    {
        if (_currentCloakInstance != null)
            Destroy(_currentCloakInstance);

        _currentCloakInstance = null;
        _currentCloakAttachment = null;
    }

    public void SetCloakColor(Color color)
    {
        if (_currentCloakAttachment == null) return;
        _currentCloakAttachment.SetColor(color);
    }

    public void SetCloakMaterial(Material material)
    {
        if (_currentCloakAttachment == null || material == null) return;
        _currentCloakAttachment.SetMaterial(material);
    }

    public void SetCloakPatternTexture(Texture tex)
    {
        if (_currentCloakAttachment == null) return;
        _currentCloakAttachment.SetTexture(tex);
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

}

//[System.Serializable]
//public class EyeOption
//{
//    public string id;
//    public Sprite sprite;
//    public Color defaultColor = new Color(0,0,50);
//}
