using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal; // for DecalProjector

public class CharacterCustomizer : MonoBehaviour
{
    private static readonly string[] SkinColorPropCandidates = { "_BaseColor", "_Color", "Base_Colour", "BaseColor" };
    private static readonly string[] SkinMapPropCandidates = { "_BaseMap", "_MainTex", "Base_Map", "BaseMap" };

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
    [SerializeField] private Transform accessoryAnchor; // accessories
    [SerializeField] private Transform leftGloveAnchor;
    [SerializeField] private Transform rightGloveAnchor;
    [SerializeField] private Transform leftBootAnchor;
    [SerializeField] private Transform rightBootAnchor;

    [Header("Hat Options")]
    [SerializeField] private GameObject[] hatPrefabs;

    [Header("Jacket Options")]
    [SerializeField] private GameObject[] jacketPrefabs;

    [Header("Accessory Options")]
    [SerializeField] private GameObject[] accessoryPrefabs;

    [Header("Glove Options")]
    [SerializeField] private GameObject[] glovePrefabs;

    [Header("Boot Options")]
    [SerializeField] private GameObject[] bootPrefabs;

    // Runtime state
    private Material _eyeMaterialInstance;
    private Material _eyeOutlineMaterialInstance;
    private int _eyeBaseMapId;
    private int _eyeBaseColorId;
    private int _skinColorPropertyId;
    private int _eyeOutlineBaseMapId;
    private int _eyeOutlineBaseColorId;
    private int _skinTexturePropertyId;
    private DecalProjector _leftEyeOutlineProjector;
    private DecalProjector _rightEyeOutlineProjector;
    private Vector3 _leftEyeBaseSize;
    private Vector3 _rightEyeBaseSize;
    private Texture _currentSkinTexture;

    private GameObject _currentHatInstance;
    private GameObject _currentJacketInstance;
    private GameObject _currentAccessoryInstance;
    private GameObject _currentLeftGloveInstance;
    private GameObject _currentRightGloveInstance;
    private GameObject _currentLeftBootInstance;
    private GameObject _currentRightBootInstance;
    private WearableAttachment _currentHatAttachment;
    private WearableAttachment _currentJacketAttachment;
    private WearableAttachment _currentAccessoryAttachment;
    private WearableAttachment _currentLeftGloveAttachment;
    private WearableAttachment _currentRightGloveAttachment;
    private WearableAttachment _currentLeftBootAttachment;
    private WearableAttachment _currentRightBootAttachment;

    private CustomizationOptionSO _currentJacketOption;
    private CustomizationOptionSO _currentAccessoryOption;
    private Color _currentGlovePrimaryColor = Color.white;
    private Color _currentBootPrimaryColor = Color.white;
    private Color _currentJacketPrimaryColor = Color.white;
    private Color _currentAccessoryPrimaryColor = Color.white;
    private readonly Dictionary<string, Color> _currentGloveChannelColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> _currentBootChannelColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> _currentJacketChannelColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> _currentAccessoryChannelColors = new Dictionary<string, Color>();

    private void Awake()
    {
        CacheEyeProjectorBaseSizes();

        if (!string.IsNullOrEmpty(eyeBaseMapPropertyName))
            _eyeBaseMapId = Shader.PropertyToID(eyeBaseMapPropertyName);

        if (!string.IsNullOrEmpty(eyeBaseColorPropertyName))
            _eyeBaseColorId = Shader.PropertyToID(eyeBaseColorPropertyName);

        if (!string.IsNullOrEmpty(skinColorPropertyName))
            _skinColorPropertyId = Shader.PropertyToID(skinColorPropertyName);

        if (!string.IsNullOrEmpty(skinTexturePropertyName))
            _skinTexturePropertyId = Shader.PropertyToID(skinTexturePropertyName);

        _currentSkinTexture = GetSelectedSkinTexture();
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

    private void RefreshEyePresentation(Sprite spriteOverride = null)
    {
        EnsureEyeMaterialInstance();
        if (_eyeMaterialInstance == null)
            return;

        if (_eyeBaseMapId == 0 || _eyeBaseColorId == 0)
            ResolveEyePropertyIds();

        EnsureEyeOutlineResources();

        Texture eyeTexture = null;

        if (spriteOverride != null)
        {
            eyeTexture = spriteOverride.texture;
        }
        else if (eyeOptions != null && eyeOptions.Length > 0)
        {
            int clampedIndex = Mathf.Clamp(selectedEyeOption, 0, eyeOptions.Length - 1);
            var option = eyeOptions[clampedIndex];
            if (option != null)
                eyeTexture = option.texture;
        }

        if (eyeTexture != null)
            ApplyEyeTextureToMaterials(eyeTexture);

        if (_eyeBaseColorId != 0)
            _eyeMaterialInstance.SetColor(_eyeBaseColorId, eyeColour);

        ApplyEyeMaterialToProjectors();
        ApplyEyeOutlineMaterialToProjectors();
        ApplyEyeOutlineColorInternal();
        ApplyEyeSizeInternal();
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

        selectedEyeOption = Mathf.Clamp(index, 0, eyeOptions.Length - 1);
        RefreshEyePresentation();
    }

    public void SetEyeColor(Color color)
    {
        eyeColour = color;
        RefreshEyePresentation();
    }

    public void SetEyeOutlineColor(Color color)
    {
        eyeOutlineColour = color;
        RefreshEyePresentation();
    }

    public void SetEyeSize(float scale)
    {
        eyeSizeScale = Mathf.Clamp(Mathf.Round(scale), 1f, 10f);
        RefreshEyePresentation();
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
        _currentSkinTexture = tex;
        ApplyCurrentSkinStateToAllRenderers();
    }

    [ContextMenu("Set Skin Colour (during play only")]
    public void SetSkinColor(Color color)
    {
        if (!Application.isPlaying) return;

        skinColor = color;
        ApplyCurrentSkinStateToAllRenderers();
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
        _currentJacketOption = null;
        _currentJacketPrimaryColor = Color.white;
        _currentJacketChannelColors.Clear();
    }

    public void SetJacketColor(Color color)
    {
        _currentJacketPrimaryColor = color;

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
        if (!string.IsNullOrEmpty(channelId))
            _currentJacketChannelColors[channelId] = color;

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

    #region Gloves

    public void SetGloves(int index)
    {
        _currentGlovePrimaryColor = Color.white;
        _currentGloveChannelColors.Clear();
        AttachMirroredWearable(
            glovePrefabs,
            index,
            leftGloveAnchor,
            rightGloveAnchor,
            ref _currentLeftGloveInstance,
            ref _currentRightGloveInstance,
            ref _currentLeftGloveAttachment,
            ref _currentRightGloveAttachment);
    }

    public void ClearGloves()
    {
        ClearMirroredWearable(
            ref _currentLeftGloveInstance,
            ref _currentRightGloveInstance,
            ref _currentLeftGloveAttachment,
            ref _currentRightGloveAttachment);
        _currentGlovePrimaryColor = Color.white;
        _currentGloveChannelColors.Clear();
    }

    public void SetGlovesColor(Color color)
    {
        _currentGlovePrimaryColor = color;
        _currentLeftGloveAttachment?.SetColor(color);
        _currentRightGloveAttachment?.SetColor(color);
    }

    public void SetGlovesPrefab(GameObject prefab)
    {
        _currentGlovePrimaryColor = Color.white;
        _currentGloveChannelColors.Clear();
        AttachMirroredWearablePrefab(
            prefab,
            leftGloveAnchor,
            rightGloveAnchor,
            ref _currentLeftGloveInstance,
            ref _currentRightGloveInstance,
            ref _currentLeftGloveAttachment,
            ref _currentRightGloveAttachment);
    }

    public void RefreshGloves()
    {
        _currentLeftGloveAttachment?.SetColor(_currentGlovePrimaryColor);
        _currentRightGloveAttachment?.SetColor(_currentGlovePrimaryColor);

        foreach (var pair in _currentGloveChannelColors)
        {
            _currentLeftGloveAttachment?.SetChannelColor(pair.Key, pair.Value);
            _currentRightGloveAttachment?.SetChannelColor(pair.Key, pair.Value);
        }
    }

    public void SetGlovesChannelColor(string channelId, Color color)
    {
        if (!string.IsNullOrEmpty(channelId))
            _currentGloveChannelColors[channelId] = color;

        _currentLeftGloveAttachment?.SetChannelColor(channelId, color);
        _currentRightGloveAttachment?.SetChannelColor(channelId, color);
    }

    public void SetGlovesPatternTexture(Texture tex)
    {
        _currentLeftGloveAttachment?.SetTexture(tex);
        _currentRightGloveAttachment?.SetTexture(tex);
    }

    public GameObject GetGlovePrefabAtIndex(int index)
    {
        if (glovePrefabs == null || glovePrefabs.Length == 0)
            return null;

        if (index < 0 || index >= glovePrefabs.Length)
            return null;

        return glovePrefabs[index];
    }

    #endregion

    #region Boots

    public void SetBoots(int index)
    {
        _currentBootPrimaryColor = Color.white;
        _currentBootChannelColors.Clear();
        AttachMirroredWearable(
            bootPrefabs,
            index,
            leftBootAnchor,
            rightBootAnchor,
            ref _currentLeftBootInstance,
            ref _currentRightBootInstance,
            ref _currentLeftBootAttachment,
            ref _currentRightBootAttachment);
    }

    public void ClearBoots()
    {
        ClearMirroredWearable(
            ref _currentLeftBootInstance,
            ref _currentRightBootInstance,
            ref _currentLeftBootAttachment,
            ref _currentRightBootAttachment);
        _currentBootPrimaryColor = Color.white;
        _currentBootChannelColors.Clear();
    }

    public void SetBootsColor(Color color)
    {
        _currentBootPrimaryColor = color;
        _currentLeftBootAttachment?.SetColor(color);
        _currentRightBootAttachment?.SetColor(color);
    }

    public void SetBootsPrefab(GameObject prefab)
    {
        _currentBootPrimaryColor = Color.white;
        _currentBootChannelColors.Clear();
        AttachMirroredWearablePrefab(
            prefab,
            leftBootAnchor,
            rightBootAnchor,
            ref _currentLeftBootInstance,
            ref _currentRightBootInstance,
            ref _currentLeftBootAttachment,
            ref _currentRightBootAttachment);
    }

    public void RefreshBoots()
    {
        _currentLeftBootAttachment?.SetColor(_currentBootPrimaryColor);
        _currentRightBootAttachment?.SetColor(_currentBootPrimaryColor);

        foreach (var pair in _currentBootChannelColors)
        {
            _currentLeftBootAttachment?.SetChannelColor(pair.Key, pair.Value);
            _currentRightBootAttachment?.SetChannelColor(pair.Key, pair.Value);
        }
    }

    public void SetBootsChannelColor(string channelId, Color color)
    {
        if (!string.IsNullOrEmpty(channelId))
            _currentBootChannelColors[channelId] = color;

        _currentLeftBootAttachment?.SetChannelColor(channelId, color);
        _currentRightBootAttachment?.SetChannelColor(channelId, color);
    }

    public void SetBootsPatternTexture(Texture tex)
    {
        _currentLeftBootAttachment?.SetTexture(tex);
        _currentRightBootAttachment?.SetTexture(tex);
    }

    public GameObject GetBootPrefabAtIndex(int index)
    {
        if (bootPrefabs == null || bootPrefabs.Length == 0)
            return null;

        if (index < 0 || index >= bootPrefabs.Length)
            return null;

        return bootPrefabs[index];
    }

    #endregion

    #region Accessories

    public void SetAccessory(int index)
    {
        AttachWearable(
            accessoryPrefabs,
            index,
            accessoryAnchor,
            ref _currentAccessoryInstance,
            ref _currentAccessoryAttachment);
    }

    public void ClearAccessory()
    {
        if (_currentAccessoryInstance != null)
            Destroy(_currentAccessoryInstance);

        _currentAccessoryInstance = null;
        _currentAccessoryAttachment = null;
        _currentAccessoryOption = null;
        _currentAccessoryPrimaryColor = Color.white;
        _currentAccessoryChannelColors.Clear();
    }

    public void SetAccessoryColor(Color color)
    {
        _currentAccessoryPrimaryColor = color;

        if (_currentAccessoryAttachment == null) return;
        _currentAccessoryAttachment.SetColor(color);
    }

    public void SetAccessoryChannelColor(string channelId, Color color)
    {
        if (!string.IsNullOrEmpty(channelId))
            _currentAccessoryChannelColors[channelId] = color;

        if (_currentAccessoryAttachment == null) return;
        _currentAccessoryAttachment.SetChannelColor(channelId, color);
    }

    public void SetAccessoryPrefab(GameObject prefab)
    {
        _currentAccessoryPrimaryColor = Color.white;
        _currentAccessoryChannelColors.Clear();
        _currentAccessoryOption = null;

        AttachWearablePrefab(prefab, accessoryAnchor, ref _currentAccessoryInstance, ref _currentAccessoryAttachment);
    }

    public GameObject GetAccessoryPrefabAtIndex(int index)
    {
        if (accessoryPrefabs == null || accessoryPrefabs.Length == 0)
            return null;

        if (index < 0 || index >= accessoryPrefabs.Length)
            return null;

        return accessoryPrefabs[index];
    }

    public void SetCurrentAccessoryOption(CustomizationOptionSO option)
    {
        _currentAccessoryOption = option;
    }

    public void SetAccessoryPatternTexture(Texture tex)
    {
        if (_currentAccessoryAttachment == null) return;
        _currentAccessoryAttachment.SetTexture(tex);
    }

    public WearableAttachment GetCurrentAccessoryAttachment() => _currentAccessoryAttachment;
    public CustomizationOptionSO GetCurrentAccessoryOption() => _currentAccessoryOption;

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

    private void AttachMirroredWearable(
        GameObject[] prefabArray,
        int index,
        Transform leftAnchor,
        Transform rightAnchor,
        ref GameObject leftInstance,
        ref GameObject rightInstance,
        ref WearableAttachment leftAttachment,
        ref WearableAttachment rightAttachment)
    {
        if (prefabArray == null || prefabArray.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, prefabArray.Length - 1);
        var prefab = prefabArray[index];
        AttachMirroredWearablePrefab(
            prefab,
            leftAnchor,
            rightAnchor,
            ref leftInstance,
            ref rightInstance,
            ref leftAttachment,
            ref rightAttachment);
    }

    private void AttachMirroredWearablePrefab(
        GameObject prefab,
        Transform leftAnchor,
        Transform rightAnchor,
        ref GameObject leftInstance,
        ref GameObject rightInstance,
        ref WearableAttachment leftAttachment,
        ref WearableAttachment rightAttachment)
    {
        AttachWearablePrefab(prefab, leftAnchor, ref leftInstance, ref leftAttachment);
        AttachWearablePrefab(prefab, rightAnchor, ref rightInstance, ref rightAttachment);
    }

    private static void ClearMirroredWearable(
        ref GameObject leftInstance,
        ref GameObject rightInstance,
        ref WearableAttachment leftAttachment,
        ref WearableAttachment rightAttachment)
    {
        if (leftInstance != null)
            UnityEngine.Object.Destroy(leftInstance);
        if (rightInstance != null)
            UnityEngine.Object.Destroy(rightInstance);

        leftInstance = null;
        rightInstance = null;
        leftAttachment = null;
        rightAttachment = null;
    }


    #endregion

    public Color GetSkinColor() => skinColor;
    public Color GetEyeColor() => eyeColour;
    public int GetSkinPatternIndex() => selectedTextureIndex;
    public int GetEyeOptionIndex() => selectedEyeOption;

    public bool HasJacketEquipped() => _currentJacketAttachment != null;
    public CustomizationOptionSO GetCurrentJacketOption() => _currentJacketOption;

    public void SetCurrentJacketOption(CustomizationOptionSO option)
    {
        _currentJacketOption = option;
    }

    public Color GetCurrentJacketPrimaryColor(Color fallback)
    {
        return _currentJacketAttachment != null ? _currentJacketPrimaryColor : fallback;
    }

    public bool TryGetCurrentJacketChannelColor(string channelId, out Color color)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            color = default;
            return false;
        }

        return _currentJacketChannelColors.TryGetValue(channelId, out color);
    }
    public bool TryGetCurrentGloveChannelColor(string channelId, out Color color)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            color = default;
            return false;
        }

        return _currentGloveChannelColors.TryGetValue(channelId, out color);
    }
    public bool TryGetCurrentBootChannelColor(string channelId, out Color color)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            color = default;
            return false;
        }

        return _currentBootChannelColors.TryGetValue(channelId, out color);
    }
    public bool TryGetCurrentAccessoryChannelColor(string channelId, out Color color)
    {
        if (string.IsNullOrEmpty(channelId))
        {
            color = default;
            return false;
        }

        return _currentAccessoryChannelColors.TryGetValue(channelId, out color);
    }
    public WearableAttachment GetCurrentJacketAttachment() => _currentJacketAttachment;
    public WearableAttachment GetCurrentLeftGloveAttachment() => _currentLeftGloveAttachment;
    public WearableAttachment GetCurrentRightGloveAttachment() => _currentRightGloveAttachment;
    public WearableAttachment GetCurrentLeftBootAttachment() => _currentLeftBootAttachment;
    public WearableAttachment GetCurrentRightBootAttachment() => _currentRightBootAttachment;

    public void SetEyeSprite(Sprite sprite)
    {
        if (sprite == null)
            return;

        RefreshEyePresentation(sprite);
    }

    public void SetSkinPatternTexture(Texture tex)
    {
        if (tex == null) return;
        _currentSkinTexture = tex;
        ApplyCurrentSkinStateToAllRenderers();
    }

    public void SetHatPrefab(GameObject prefab)
    {
        AttachWearablePrefab(prefab, headAnchor, ref _currentHatInstance, ref _currentHatAttachment);
    }

        public void SetJacketPrefab(GameObject prefab)
    {
        _currentJacketPrimaryColor = Color.white;
        _currentJacketChannelColors.Clear();
        _currentJacketOption = null;

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

    public void AddSkinRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (skinRenderers == null || skinRenderers.Length == 0)
        {
            skinRenderers = new[] { renderer };
        }
        else
        {
            for (int i = 0; i < skinRenderers.Length; i++)
            {
                if (skinRenderers[i] == renderer)
                    return;
            }

            Renderer[] newArray = new Renderer[skinRenderers.Length + 1];
            for (int i = 0; i < skinRenderers.Length; i++)
                newArray[i] = skinRenderers[i];

            newArray[skinRenderers.Length] = renderer;
            skinRenderers = newArray;
        }

        ApplyCurrentSkinStateToRenderer(renderer);
    }

    public void RemoveSkinRenderer(Renderer renderer)
    {
        if (renderer == null || skinRenderers == null || skinRenderers.Length == 0)
            return;

        int removeIndex = -1;
        for (int i = 0; i < skinRenderers.Length; i++)
        {
            if (skinRenderers[i] == renderer)
            {
                removeIndex = i;
                break;
            }
        }

        if (removeIndex < 0)
            return;

        if (skinRenderers.Length == 1)
        {
            skinRenderers = System.Array.Empty<Renderer>();
            return;
        }

        Renderer[] newArray = new Renderer[skinRenderers.Length - 1];
        int dst = 0;
        for (int i = 0; i < skinRenderers.Length; i++)
        {
            if (i == removeIndex)
                continue;

            newArray[dst++] = skinRenderers[i];
        }

        skinRenderers = newArray;
    }

    private void ApplyCurrentSkinStateToAllRenderers()
    {
        if (skinRenderers == null)
            return;

        foreach (var renderer in skinRenderers)
            ApplyCurrentSkinStateToRenderer(renderer);
    }

    private void ApplyCurrentSkinStateToRenderer(Renderer renderer)
    {
        ApplyToSkinRendererMaterials(renderer, ApplyCurrentSkinStateToMaterial);
    }

    private void ApplyCurrentSkinStateToMaterial(Material mat)
    {
        if (mat == null)
            return;

        Texture texture = _currentSkinTexture != null ? _currentSkinTexture : GetSelectedSkinTexture();
        if (texture != null)
            ApplySkinTextureToMaterial(mat, texture);

        if (Application.isPlaying)
            ApplySkinColorToMaterial(mat, skinColor);
    }

    private void ApplySkinTextureToMaterial(Material mat, Texture texture)
    {
        ApplyTextureToCandidateProperties(mat, texture, skinTexturePropertyName, _skinTexturePropertyId, SkinMapPropCandidates);
    }

    private void ApplySkinColorToMaterial(Material mat, Color color)
    {
        ApplyColorToCandidateProperties(mat, color, skinColorPropertyName, _skinColorPropertyId, SkinColorPropCandidates);
    }

    private static void ApplyTextureToCandidateProperties(Material mat, Texture texture, string configuredPropertyName, int configuredPropertyId, string[] fallbackPropertyNames)
    {
        if (texture == null || mat == null)
            return;

        if (!string.IsNullOrEmpty(configuredPropertyName) && mat.HasProperty(configuredPropertyName))
            mat.SetTexture(configuredPropertyId != 0 ? configuredPropertyId : Shader.PropertyToID(configuredPropertyName), texture);

        for (int i = 0; i < fallbackPropertyNames.Length; i++)
        {
            string propertyName = fallbackPropertyNames[i];
            if (string.IsNullOrEmpty(propertyName) || propertyName == configuredPropertyName || !mat.HasProperty(propertyName))
                continue;

            mat.SetTexture(propertyName, texture);
        }
    }

    private static void ApplyColorToCandidateProperties(Material mat, Color color, string configuredPropertyName, int configuredPropertyId, string[] fallbackPropertyNames)
    {
        if (mat == null)
            return;

        if (!string.IsNullOrEmpty(configuredPropertyName) && mat.HasProperty(configuredPropertyName))
            mat.SetColor(configuredPropertyId != 0 ? configuredPropertyId : Shader.PropertyToID(configuredPropertyName), color);

        for (int i = 0; i < fallbackPropertyNames.Length; i++)
        {
            string propertyName = fallbackPropertyNames[i];
            if (string.IsNullOrEmpty(propertyName) || propertyName == configuredPropertyName || !mat.HasProperty(propertyName))
                continue;

            mat.SetColor(propertyName, color);
        }
    }

    private Texture GetSelectedSkinTexture()
    {
        if (skinPatterns == null || skinPatterns.Length == 0)
            return null;

        int index = Mathf.Clamp(selectedTextureIndex, 0, skinPatterns.Length - 1);
        return skinPatterns[index];
    }

    private void ApplyToSkinRendererMaterials(Renderer renderer, System.Action<Material> apply)
    {
        if (renderer == null || apply == null)
            return;

        if (!Application.isPlaying)
        {
            ApplySkinStateToRendererPropertyBlocks(renderer);
            return;
        }

        var materials = renderer.materials;
        if (materials == null || materials.Length == 0)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            var mat = materials[i];
            if (mat == null)
                continue;

            apply(mat);
        }
    }

    private void ApplySkinStateToRendererPropertyBlocks(Renderer renderer)
    {
        if (renderer == null)
            return;

        var materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return;

        Texture texture = _currentSkinTexture != null ? _currentSkinTexture : GetSelectedSkinTexture();
        if (texture == null)
            return;

        var propertyBlock = new MaterialPropertyBlock();
        for (int i = 0; i < materials.Length; i++)
        {
            renderer.GetPropertyBlock(propertyBlock, i);
            ApplyTextureToPropertyBlock(propertyBlock, materials[i], texture, skinTexturePropertyName, _skinTexturePropertyId, SkinMapPropCandidates);
            renderer.SetPropertyBlock(propertyBlock, i);
            propertyBlock.Clear();
        }
    }

    private static void ApplyTextureToPropertyBlock(
        MaterialPropertyBlock propertyBlock,
        Material referenceMaterial,
        Texture texture,
        string configuredPropertyName,
        int configuredPropertyId,
        string[] fallbackPropertyNames)
    {
        if (propertyBlock == null || referenceMaterial == null || texture == null)
            return;

        if (!string.IsNullOrEmpty(configuredPropertyName) && referenceMaterial.HasProperty(configuredPropertyName))
            propertyBlock.SetTexture(configuredPropertyId != 0 ? configuredPropertyId : Shader.PropertyToID(configuredPropertyName), texture);

        for (int i = 0; i < fallbackPropertyNames.Length; i++)
        {
            string propertyName = fallbackPropertyNames[i];
            if (string.IsNullOrEmpty(propertyName) || propertyName == configuredPropertyName || !referenceMaterial.HasProperty(propertyName))
                continue;

            propertyBlock.SetTexture(propertyName, texture);
        }
    }

}

//[System.Serializable]
//public class EyeOption
//{
//    public string id;
//    public Sprite sprite;
//    public Color defaultColor = new Color(0,0,50);
//}
