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

    #region Eyes (Decals)

    private void EnsureEyeMaterialInstance()
    {
        if (_eyeMaterialInstance != null) return;

        if (eyeDecalBaseMaterial == null)
        {
            Debug.LogWarning("CharacterCustomizer: eyeDecalBaseMaterial is not assigned.", this);
            return;
        }

        // Create per-character instance
        _eyeMaterialInstance = new Material(eyeDecalBaseMaterial);

        // Assign to both projectors
        ApplyEyeMaterialToProjectors();
    }

    private void ApplyEyeMaterialToProjectors()
    {
        if (_eyeMaterialInstance == null) return;

        if (leftEyeProjector != null)
            leftEyeProjector.material = _eyeMaterialInstance;

        if (rightEyeProjector != null)
            rightEyeProjector.material = _eyeMaterialInstance;
    }

    [ContextMenu("Update Eye Style")]
    public void SetEyeStyle()
    {
        SetEyeStyle(selectedEyeOption);
        SetEyeColor(eyeColour);
    }
    
    [ContextMenu("Randomise Eye Style")]
    public void SetRandomEyeStyle()
    {
        selectedEyeOption = Random.Range(0, eyeOptions.Length);
        eyeColour = Random.ColorHSV(0f,1f, 0.25f, 1f, 0.1f, .75f);

        SetEyeStyle();
    }
    
    
    public void SetEyeStyle(int index)
    {
        if (eyeOptions == null || eyeOptions.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, eyeOptions.Length - 1);
        var option = eyeOptions[index];

        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null) return;

        // Assign texture (Base_Map) from sprite
        if (option != null && _eyeBaseMapId != 0)
        {
            Texture2D tex = option.texture;
            _eyeMaterialInstance.SetTexture(_eyeBaseMapId, tex);

            // NOTE: if you're using atlased sprites, you'll probably also want to
            // set tiling/offset here using option.sprite.rect / texture size.
        }

        // Assign default eye color
        if (_eyeBaseColorId != 0)
        {
            _eyeMaterialInstance.SetColor(_eyeBaseColorId, eyeColour);
        }

        // Make sure projectors are using this material
        ApplyEyeMaterialToProjectors();
    }

    public void SetEyeColor(Color color)
    {
        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null || _eyeBaseColorId == 0) return;

        _eyeMaterialInstance.SetColor(_eyeBaseColorId, color);
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

        foreach (var renderer in skinRenderers)
        {
            var mat = renderer.material;
            if (mat != null && mat.HasProperty(_skinColorPropertyId))
            {
                mat.SetColor(_skinColorPropertyId, color);
            }
        }
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

        // Destroy existing
        if (currentInstance != null)
            Destroy(currentInstance);

        // Instantiate as child of the anchor
        currentInstance = Instantiate(prefab, anchor);
        currentInstance.transform.localRotation = Quaternion.identity;

        currentAttachment = currentInstance.GetComponent<WearableAttachment>();

        if (currentAttachment != null && currentAttachment.offset != null)
        {
            // Align the wearable so its offset sits on the anchor.
            // This assumes offset has no extra rotation relative to root.
            currentInstance.transform.localPosition = -currentAttachment.offset.localPosition;
        }
        else
        {
            currentInstance.transform.localPosition = Vector3.zero;
        }
    }

    #endregion
}

//[System.Serializable]
//public class EyeOption
//{
//    public string id;
//    public Sprite sprite;
//    public Color defaultColor = new Color(0,0,50);
//}
