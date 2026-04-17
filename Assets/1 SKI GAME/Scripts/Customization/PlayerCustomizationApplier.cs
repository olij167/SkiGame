using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SkiGame.Progression;

public class PlayerCustomizationApplier : MonoBehaviour
{
    [Header("References (auto-found if null)")]
    [SerializeField] private CharacterCustomizer characterCustomizer;
    [SerializeField] private SkiGearLoadout gearLoadout;
    [SerializeField] private SkiController skiController;

    [Header("Catalog")]
    [SerializeField] private CustomizationCatalogSO catalog;

    [Header("Runtime Apply")]
    [Tooltip("If enabled, applies the saved profile customization automatically when runtime starts.")]
    [SerializeField] private bool autoApplyOnRuntimeStart = true;

    [Tooltip("If enabled, re-applies whenever PlayerStatsManager loads a profile.")]
    [SerializeField] private bool applyOnProfileLoadedEvent = true;

    private PlayerStatsManager _statsMgr;
    private bool _subscribed;
    private Coroutine _applyRoutine;
    private bool _hasAppliedInitialProfile;

    private void Awake()
    {
        if (characterCustomizer == null) characterCustomizer = GetComponentInChildren<CharacterCustomizer>(true);
        if (gearLoadout == null) gearLoadout = GetComponent<SkiGearLoadout>();
        if (skiController == null) skiController = GetComponent<SkiController>();
    }

    private void OnEnable()
    {
        TryHookStatsManager();

        // Only auto-apply on startup if this object belongs to the ACTIVE scene.
        // This prevents the gameplay scene loaded as a menu background from
        // prematurely applying a profile during menu mode.
        if (autoApplyOnRuntimeStart &&
            !_hasAppliedInitialProfile &&
            gameObject.scene == SceneManager.GetActiveScene())
        {
            if (_applyRoutine != null)
                StopCoroutine(_applyRoutine);

            _applyRoutine = StartCoroutine(ApplyCurrentProfileDeferred());
        }
    }

    private void OnDisable()
    {
        if (_applyRoutine != null)
        {
            StopCoroutine(_applyRoutine);
            _applyRoutine = null;
        }

        UnhookStatsManager();
    }

    private void TryHookStatsManager()
    {
        if (_subscribed) return;

        _statsMgr = PlayerStatsManager.Instance != null
            ? PlayerStatsManager.Instance
            : FindObjectOfType<PlayerStatsManager>();

        if (_statsMgr != null && applyOnProfileLoadedEvent)
        {
            _statsMgr.OnProfileLoaded += OnProfileLoaded;
            _subscribed = true;
        }
    }

    private void UnhookStatsManager()
    {
        if (!_subscribed) return;

        if (_statsMgr != null)
            _statsMgr.OnProfileLoaded -= OnProfileLoaded;

        _subscribed = false;
    }

    private void OnProfileLoaded(PlayerStatsProfile profile)
    {
        if (!isActiveAndEnabled) return;
        if (!applyOnProfileLoadedEvent) return;
        if (profile == null) return;

        if (_applyRoutine != null)
            StopCoroutine(_applyRoutine);

        _applyRoutine = StartCoroutine(ApplySpecificProfileDeferred(profile));
    }

    private IEnumerator ApplyCurrentProfileDeferred()
    {
        // Wait one frame so PlayerStatsManager initialization/load has completed.
        yield return null;

        if (_statsMgr == null)
        {
            _statsMgr = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance
                : FindObjectOfType<PlayerStatsManager>();
        }

        var profile = _statsMgr != null ? _statsMgr.Profile : null;
        if (profile == null)
        {
            _applyRoutine = null;
            yield break;
        }

        yield return ApplySpecificProfileDeferred(profile);
    }

    private IEnumerator ApplySpecificProfileDeferred(PlayerStatsProfile profile)
    {
        // Wait until end of frame so controller visuals / gear swaps / scene enable flow settle first.
        yield return new WaitForEndOfFrame();

        if (!isActiveAndEnabled || profile == null)
        {
            _applyRoutine = null;
            yield break;
        }

        ApplyFromProfile(profile);

        // Rebuild the skier's grounded state AFTER late profile/customization application.
        // This is the important load-from-menu fix.
        if (skiController != null)
        {
            skiController.TeleportToSpawn(
                skiController.transform.position,
                skiController.transform.rotation,
                snapToGround: true
            );
        }

        _hasAppliedInitialProfile = true;
        _applyRoutine = null;
    }

    public void ApplyFromProfile(PlayerStatsProfile profile)
    {
        if (profile == null || profile.customization == null) return;
        var state = profile.customization;

        // Defaults (only on first-time init; later empty hat/jacket means "none")
        if (catalog != null && state != null)
        {
            if (!state.customizationInitialized)
            {
                if (string.IsNullOrEmpty(state.equippedEyeIconId))
                    state.equippedEyeIconId = GetDefaultId(CustomizationOptionType.EyeIcon);

                if (string.IsNullOrEmpty(state.equippedSkisId))
                    state.equippedSkisId = GetDefaultGearId(CustomizationOptionType.Skis);

                if (string.IsNullOrEmpty(state.equippedPolesId))
                    state.equippedPolesId = GetDefaultGearId(CustomizationOptionType.Poles);

                // IMPORTANT: Leave these empty to mean "None"
                if (string.IsNullOrEmpty(state.equippedHatId))
                    state.equippedHatId = "";

                if (string.IsNullOrEmpty(state.equippedJacketId))
                    state.equippedJacketId = "";

                state.customizationInitialized = true;
            }
        }

        // Apply continuous values (skin/eyes always use saved values)
        if (characterCustomizer != null)
        {
            characterCustomizer.SetSkinColor(state.skinColor);
            characterCustomizer.SetEyeColor(state.eyeColor);
            characterCustomizer.SetEyeOutlineColor(state.eyeOutlineColor);
            characterCustomizer.SetEyeSize(state.eyeSize);

            // Eyes always resolve via catalog
            ApplyCosmetic(state.equippedEyeIconId);

            // Hat: empty = none
            if (string.IsNullOrEmpty(state.equippedHatId)) characterCustomizer.ClearHat();
            else ApplyCosmetic(state.equippedHatId);

            // Jacket: empty = none
            if (string.IsNullOrEmpty(state.equippedJacketId)) characterCustomizer.ClearJacket();
            else ApplyCosmetic(state.equippedJacketId);

            // Hat/Jacket colors are applied after cosmetics are ensured
            var hatApplied = ResolveAppliedGearColor(state.hatColor, state.hatUseDefaultColor, state.equippedHatId);
            var jacketApplied = ResolveAppliedGearColor(state.jacketColor, state.jacketUseDefaultColor, state.equippedJacketId);

            characterCustomizer.SetHatColor(hatApplied);
            characterCustomizer.SetJacketColor(jacketApplied);

            ApplyWearableExtraColors(state, CustomizationUIController.PatternTarget.Hat);
            ApplyWearableExtraColors(state, CustomizationUIController.PatternTarget.Jacket);
        }

        // Apply gear prefabs
        ApplyGear(state.equippedSkisId, isSkis: true);
        ApplyGear(state.equippedPolesId, isSkis: false);

        // Apply gear colors (resolved via toggle)
        if (gearLoadout != null)
        {
            var skisApplied = ResolveAppliedGearColor(state.skisColor, state.skisUseDefaultColor, state.equippedSkisId);
            var polesApplied = ResolveAppliedGearColor(state.polesColor, state.polesUseDefaultColor, state.equippedPolesId);

            gearLoadout.SetSkisColor(skisApplied);
            gearLoadout.SetPolesColor(polesApplied);
        }

        // Apply gear patterns (resolved via toggle)
        ApplyGearPatternsFromState(state);
    }

    public void PreviewGearFromOption(PlayerStatsProfile profile, string optionId)
    {
        if (profile == null || profile.customization == null) return;
        if (string.IsNullOrEmpty(optionId) || catalog == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null) return;

        // Preview should show the preview gear's DEFAULTS (even if player is in custom mode)
        if (opt.type == CustomizationOptionType.Skis)
        {
            if (gearLoadout != null && opt.gearProfile != null) gearLoadout.EquipSkis(opt.gearProfile);

            var previewColor = ResolveGearDefaultTint(optionId);
            gearLoadout?.SetSkisColor(previewColor);

            var previewPatternId = ResolveDefaultPatternIdForGear(optionId);
            skiController?.SetSkisPatternTexture(ResolvePatternTextureById(previewPatternId));
        }
        else if (opt.type == CustomizationOptionType.Poles)
        {
            if (gearLoadout != null && opt.gearProfile != null) gearLoadout.EquipPoles(opt.gearProfile);

            var previewColor = ResolveGearDefaultTint(optionId);
            gearLoadout?.SetPolesColor(previewColor);

            var previewPatternId = ResolveDefaultPatternIdForGear(optionId);
            skiController?.SetPolesPatternTexture(ResolvePatternTextureById(previewPatternId));
        }
    }

    public void ApplySkisColorToLoadout(PlayerStatsProfile profile, Color c)
    {
        if (profile?.customization == null) return;
        if (gearLoadout != null) gearLoadout.SetSkisColor(c);
    }

    public void ApplyPolesColorToLoadout(PlayerStatsProfile profile, Color c)
    {
        if (profile?.customization == null) return;
        if (gearLoadout != null) gearLoadout.SetPolesColor(c);
    }

    private void ApplyGearPatternsFromState(PlayerStatsProfile.CustomizationState state)
    {
        if (state == null) return;

        var skisPatternId = ResolveAppliedPatternId(state.equippedSkisPatternId, state.skisUseDefaultPattern, state.equippedSkisId);
        var polesPatternId = ResolveAppliedPatternId(state.equippedPolesPatternId, state.polesUseDefaultPattern, state.equippedPolesId);
        var hatPatternId = ResolveAppliedPatternId(state.equippedHatPatternId, state.hatUseDefaultPattern, state.equippedHatId);
        var jacketPatternId = ResolveAppliedPatternId(state.equippedJacketPatternId, state.jacketUseDefaultPattern, state.equippedJacketId);

        var skisTex = ResolvePatternTextureById(skisPatternId);
        var polesTex = ResolvePatternTextureById(polesPatternId);
        var hatTex = ResolvePatternTextureById(hatPatternId);
        var jacketTex = ResolvePatternTextureById(jacketPatternId);

        skiController?.SetSkisPatternTexture(skisTex);
        skiController?.SetPolesPatternTexture(polesTex);

        characterCustomizer?.SetHatPatternTexture(hatTex);
        characterCustomizer?.SetJacketPatternTexture(jacketTex);
    }

    private string GetDefaultId(CustomizationOptionType type)
    {
        if (catalog == null) return null;

        foreach (var o in catalog.GetByType(type))
            if (o != null && o.customizerIndex == 0)
                return o.id;

        foreach (var o in catalog.GetByType(type))
            if (o != null)
                return o.id;

        return null;
    }

    private string GetDefaultGearId(CustomizationOptionType type)
    {
        if (catalog == null) return null;

        foreach (var o in catalog.GetByType(type))
            if (o != null && o.gearProfile != null)
                return o.id;

        return null;
    }

    private void ApplyCosmetic(string optionId)
    {
        if (string.IsNullOrEmpty(optionId) || catalog == null || characterCustomizer == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null) return;

        switch (opt.type)
        {
            case CustomizationOptionType.EyeIcon:
                if (opt.eyeSprite != null) characterCustomizer.SetEyeSprite(opt.eyeSprite);
                else characterCustomizer.SetEyeStyle(opt.customizerIndex);
                break;

            case CustomizationOptionType.Hat:
                if (opt.hatPrefab != null) characterCustomizer.SetHatPrefab(opt.hatPrefab);
                else characterCustomizer.SetHat(opt.customizerIndex);
                break;

            case CustomizationOptionType.Jacket:
                if (opt.jacketPrefab != null) characterCustomizer.SetJacketPrefab(opt.jacketPrefab);
                else characterCustomizer.SetJacket(opt.customizerIndex);
                break;
        }
    }

    private void ApplyGear(string optionId, bool isSkis)
    {
        if (string.IsNullOrEmpty(optionId) || catalog == null || gearLoadout == null) return;

        var opt = catalog.FindById(optionId);
        if (opt == null || opt.gearProfile == null) return;

        if (isSkis) gearLoadout.EquipSkis(opt.gearProfile);
        else gearLoadout.EquipPoles(opt.gearProfile);
    }

    // ---------- Defaults & resolution helpers ----------
    private static readonly Color DefaultSkin = Color.yellowNice;
    private static readonly Color DefaultEyes = Color.black;

    private static readonly Color DefaultGearFallback = new Color(0.12f, 0.12f, 0.12f, 1f);

    private Color ResolveGearDefaultTint(string equippedGearId)
    {
        if (catalog == null || string.IsNullOrEmpty(equippedGearId))
            return DefaultGearFallback;

        var gearOpt = catalog.FindById(equippedGearId);
        if (gearOpt == null)
            return DefaultGearFallback;

        // If the gear declares a default tint, use it. Otherwise fallback.
        return gearOpt.HasTint ? gearOpt.DefaultTint : DefaultGearFallback;
    }

    private Color ResolveAppliedGearColor(Color savedCustomColor, bool useDefault, string equippedGearId)
    {
        return useDefault ? ResolveGearDefaultTint(equippedGearId) : savedCustomColor;
    }

    private string ResolveDefaultPatternIdForGear(string equippedGearId)
    {
        if (catalog == null || string.IsNullOrEmpty(equippedGearId))
            return null;

        var gearOpt = catalog.FindById(equippedGearId);
        if (gearOpt == null)
            return null;

        // Preferred: concrete option reference
        if (gearOpt.defaultPatternOption != null)
            return gearOpt.defaultPatternOption.id;

        // Back-compat: string id
        if (!string.IsNullOrEmpty(gearOpt.defaultPatternId))
            return gearOpt.defaultPatternId;

        return null;
    }

    private string ResolveAppliedPatternId(string savedCustomPatternId, bool useDefault, string equippedGearId)
    {
        if (!useDefault && !string.IsNullOrEmpty(savedCustomPatternId))
            return savedCustomPatternId;

        return ResolveDefaultPatternIdForGear(equippedGearId);
    }

    private Texture2D ResolvePatternTextureById(string patternId)
    {
        if (string.IsNullOrEmpty(patternId) || catalog == null)
            return null;

        var opt = catalog.FindById(patternId);
        if (opt == null || opt.type != CustomizationOptionType.SkinPattern)
            return null;

        // Preferred: direct payload
        if (opt.skinPatternTexture is Texture2D direct)
            return direct;

        // Back-compat: CharacterCustomizer array lookup
        if (characterCustomizer != null && opt.customizerIndex >= 0)
            return characterCustomizer.GetSkinPatternTexture2D(opt.customizerIndex);

        return null;
    }

    private void ApplyWearableExtraColors(PlayerStatsProfile.CustomizationState state, CustomizationUIController.PatternTarget target)
    {
        if (state == null || characterCustomizer == null)
            return;

        string slotKey;
        string equippedId;

        switch (target)
        {
            case CustomizationUIController.PatternTarget.Hat:
                slotKey = "Hat";
                equippedId = state.equippedHatId;
                break;

            case CustomizationUIController.PatternTarget.Jacket:
                slotKey = "Jacket";
                equippedId = state.equippedJacketId;
                break;

            default:
                return;
        }

        if (string.IsNullOrEmpty(equippedId) || catalog == null)
            return;

        var opt = catalog.FindById(equippedId);
        if (opt == null) return;

        GameObject prefab = null;
        if (target == CustomizationUIController.PatternTarget.Hat)
        {
            prefab = opt.hatPrefab != null
                ? opt.hatPrefab
                : characterCustomizer.GetHatPrefabAtIndex(opt.customizerIndex);
        }
        else
        {
            prefab = opt.jacketPrefab != null
                ? opt.jacketPrefab
                : characterCustomizer.GetJacketPrefabAtIndex(opt.customizerIndex);
        }

        var wearable = prefab != null ? prefab.GetComponent<WearableAttachment>() : null;
        var channels = wearable != null ? wearable.GetExtraChannels() : null;
        if (channels == null) return;

        for (int i = 0; i < channels.Count; i++)
        {
            var channel = channels[i];
            if (channel == null || string.IsNullOrEmpty(channel.id))
                continue;

            var color = channel.defaultColor;
            if (state.TryGetExtraColor(slotKey, channel.id, out var saved))
                color = saved;

            if (target == CustomizationUIController.PatternTarget.Hat)
                characterCustomizer.SetHatChannelColor(channel.id, color);
            else
                characterCustomizer.SetJacketChannelColor(channel.id, color);
        }
    }
}
