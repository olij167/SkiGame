using UnityEngine;
using SkiGame.Runs;

[DisallowMultipleComponent]
public class NpcSkierProfile : MonoBehaviour, INpcDialogueNameSource
{
    public enum SkierArchetype
    {
        BeginnerCruiser,
        CasualTourist,
        ConfidentCarver,
        AggressiveLocal,
        ScenicWanderer,
        LiftLapper,
        SocialFollower,
        ErraticRookie
    }

    [Header("Identity")]
    [SerializeField] private string skierName = "Skier";
    [SerializeField] private SkierArchetype archetype = SkierArchetype.CasualTourist;

    [Header("Behaviour Traits")]
    [Range(0f, 1f)][SerializeField] private float skill01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float confidence01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float caution01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float poleUsage01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float jumpiness01 = 0.25f;
    [Range(0f, 1f)][SerializeField] private float crowdTolerance01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float roamResortBias01 = 0.35f;
    [Range(-1f, 1f)][SerializeField] private float laneBias = 0f;

    [Header("Personality Polish")]
    [Range(0f, 1f)][SerializeField] private float scenicPauseBias01 = 0.25f;
    [Range(0f, 1f)][SerializeField] private float lineVariation01 = 0.35f;
    [Range(0f, 1f)][SerializeField] private float assertiveness01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float hesitationOnSteeps01 = 0.35f;
    [Range(0f, 1f)][SerializeField] private float socialPresenceBias01 = 0.35f;

    [Header("Generic Activity Preferences")]
    [Range(0f, 1f)][SerializeField] private float socialness = 0.5f;
    [Range(0f, 1f)][SerializeField] private float riskTolerance = 0.5f;
    [Range(0f, 1f)][SerializeField] private float explorationWeight = 0.5f;
    [Range(0f, 1f)][SerializeField] private float raceInterest = 0.25f;
    [Range(0f, 1f)][SerializeField] private float trickInterest = 0.25f;
    [Range(0f, 1f)][SerializeField] private float liftUseWeight = 0.6f;
    [Range(0f, 1f)][SerializeField] private float kioskVisitWeight = 0.2f;
    [Range(0f, 1f)][SerializeField] private float lodgeRestWeight = 0.2f;
    [Range(0f, 1f)][SerializeField] private float medicVisitWeight = 0.05f;
    [Range(0f, 1f)][SerializeField] private float viewpointPauseWeight = 0.25f;
    [Range(0f, 1f)][SerializeField] private float idleWanderWeight = 0.15f;
    [Range(0f, 1f)][SerializeField] private float leaveAreaWeight = 0.08f;
    [Range(0f, 1f)][SerializeField] private float groupAffinity = 0.45f;
    [SerializeField] private Vector2 socialLoiterDurationRange = new Vector2(20f, 90f);
    [SerializeField] private Vector2 viewpointPauseDurationRange = new Vector2(8f, 25f);
    [SerializeField] private Vector2 lodgeRestDurationRange = new Vector2(30f, 120f);
    [SerializeField] private Vector2 idleWanderDurationRange = new Vector2(8f, 30f);
    [SerializeField] private Vector2 practiceTrickDurationRange = new Vector2(10f, 40f);

    [Header("Style Tuning")]
    [Range(0f, 1f)][SerializeField] private float turnRadiusPreference01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float edgePreference01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float mergeCaution01 = 0.5f;
    [Range(0f, 1f)][SerializeField] private float overtakeTendency01 = 0.35f;

    [Header("Run Preference")]
    [SerializeField] private bool autoDerivePreferredDifficulty = true;
    [SerializeField] private SkiRunDifficulty minPreferredDifficulty = SkiRunDifficulty.Green;
    [SerializeField] private SkiRunDifficulty maxPreferredDifficulty = SkiRunDifficulty.Blue;
    [SerializeField] private float repeatRunBias01 = 0.2f;

    [Header("Derived Runtime Values")]
    [SerializeField] private float cruiseSpeedMps = 10f;
    [SerializeField] private float reactionLookaheadMeters = 14f;
    [SerializeField] private float turnAggression = 0.65f;
    [SerializeField] private float recoveryDelaySeconds = 2f;
    [SerializeField] private float desiredRestSecondsMin = 1.5f;
    [SerializeField] private float desiredRestSecondsMax = 4f;
    [SerializeField] private float ambientPauseChance = 0.1f;
    [SerializeField] private float ambientPauseDurationMin = 0.5f;
    [SerializeField] private float ambientPauseDurationMax = 2.25f;

    [Header("Randomization Ranges")]
    [SerializeField] private Vector2 skillRange = new Vector2(0.15f, 1f);
    [SerializeField] private Vector2 confidenceRange = new Vector2(0.2f, 1f);
    [SerializeField] private Vector2 cautionRange = new Vector2(0.1f, 1f);
    [SerializeField] private Vector2 poleUsageRange = new Vector2(0.15f, 0.95f);
    [SerializeField] private Vector2 jumpinessRange = new Vector2(0.05f, 0.75f);
    [SerializeField] private Vector2 crowdToleranceRange = new Vector2(0.15f, 0.95f);
    [SerializeField] private Vector2 roamResortBiasRange = new Vector2(0.05f, 0.75f);
    [SerializeField] private Vector2 laneBiasRange = new Vector2(-0.75f, 0.75f);

    public string SkierName => skierName;
    public string DialogueDisplayName => string.IsNullOrWhiteSpace(skierName) ? "Skier" : skierName.Trim();
    public SkierArchetype Archetype => archetype;

    public float Skill01 => skill01;
    public float Confidence01 => confidence01;
    public float Caution01 => caution01;
    public float PoleUsage01 => poleUsage01;
    public float Jumpiness01 => jumpiness01;
    public float CrowdTolerance01 => crowdTolerance01;
    public float RoamResortBias01 => roamResortBias01;
    public float LaneBias => laneBias;

    public float ScenicPauseBias01 => scenicPauseBias01;
    public float LineVariation01 => lineVariation01;
    public float Assertiveness01 => assertiveness01;
    public float HesitationOnSteeps01 => hesitationOnSteeps01;
    public float SocialPresenceBias01 => socialPresenceBias01;
    public float Socialness => socialness;
    public float RiskTolerance => riskTolerance;
    public float ExplorationWeight => explorationWeight;
    public float RaceInterest => raceInterest;
    public float TrickInterest => trickInterest;
    public float LiftUseWeight => liftUseWeight;
    public float KioskVisitWeight => kioskVisitWeight;
    public float LodgeRestWeight => lodgeRestWeight;
    public float MedicVisitWeight => medicVisitWeight;
    public float ViewpointPauseWeight => viewpointPauseWeight;
    public float IdleWanderWeight => idleWanderWeight;
    public float LeaveAreaWeight => leaveAreaWeight;
    public float GroupAffinity => groupAffinity;
    public Vector2 SocialLoiterDurationRange => NormalizeDurationRange(socialLoiterDurationRange, 20f, 90f);
    public Vector2 ViewpointPauseDurationRange => NormalizeDurationRange(viewpointPauseDurationRange, 8f, 25f);
    public Vector2 LodgeRestDurationRange => NormalizeDurationRange(lodgeRestDurationRange, 30f, 120f);
    public Vector2 IdleWanderDurationRange => NormalizeDurationRange(idleWanderDurationRange, 8f, 30f);
    public Vector2 PracticeTrickDurationRange => NormalizeDurationRange(practiceTrickDurationRange, 10f, 40f);
    public float TurnRadiusPreference01 => turnRadiusPreference01;
    public float EdgePreference01 => edgePreference01;
    public float MergeCaution01 => mergeCaution01;
    public float OvertakeTendency01 => overtakeTendency01;

    public SkiRunDifficulty MinPreferredDifficulty => minPreferredDifficulty;
    public SkiRunDifficulty MaxPreferredDifficulty => maxPreferredDifficulty;
    public float RepeatRunBias01 => repeatRunBias01;

    public float CruiseSpeedMps => cruiseSpeedMps;
    public float ReactionLookaheadMeters => reactionLookaheadMeters;
    public float TurnAggression => turnAggression;
    public float RecoveryDelaySeconds => recoveryDelaySeconds;
    public float DesiredRestSecondsMin => desiredRestSecondsMin;
    public float DesiredRestSecondsMax => desiredRestSecondsMax;
    public float AmbientPauseChance => ambientPauseChance;
    public float AmbientPauseDurationMin => ambientPauseDurationMin;
    public float AmbientPauseDurationMax => ambientPauseDurationMax;

    [ContextMenu("Randomize Profile")]
    public void RandomizeProfile()
    {
        archetype = (SkierArchetype)Random.Range(0, System.Enum.GetValues(typeof(SkierArchetype)).Length);

        skill01 = Sample01(skillRange);
        confidence01 = Sample01(confidenceRange);
        caution01 = Sample01(cautionRange);
        poleUsage01 = Sample01(poleUsageRange);
        jumpiness01 = Sample01(jumpinessRange);
        crowdTolerance01 = Sample01(crowdToleranceRange);
        roamResortBias01 = Sample01(roamResortBiasRange);
        laneBias = Random.Range(laneBiasRange.x, laneBiasRange.y);

        scenicPauseBias01 = Random.Range(0.05f, 0.7f);
        lineVariation01 = Random.Range(0.1f, 0.85f);
        assertiveness01 = Random.Range(0.1f, 0.95f);
        hesitationOnSteeps01 = Random.Range(0.05f, 0.8f);
        socialPresenceBias01 = Random.Range(0.05f, 0.8f);
        socialness = Mathf.Clamp01(socialPresenceBias01 + Random.Range(-0.12f, 0.18f));
        riskTolerance = Mathf.Clamp01((skill01 * 0.45f) + (confidence01 * 0.35f) + ((1f - caution01) * 0.2f));
        explorationWeight = Mathf.Clamp01(roamResortBias01 + Random.Range(-0.15f, 0.15f));
        raceInterest = Mathf.Clamp01((confidence01 * 0.35f) + (assertiveness01 * 0.35f) + Random.Range(-0.12f, 0.15f));
        trickInterest = Mathf.Clamp01((jumpiness01 * 0.55f) + (riskTolerance * 0.3f) + Random.Range(-0.1f, 0.15f));
        liftUseWeight = Mathf.Clamp01(Mathf.Lerp(0.35f, 0.9f, confidence01) + Random.Range(-0.1f, 0.1f));
        kioskVisitWeight = Mathf.Clamp01(Mathf.Lerp(0.35f, 0.08f, confidence01) + Random.Range(-0.06f, 0.08f));
        lodgeRestWeight = Mathf.Clamp01(Mathf.Lerp(0.35f, 0.1f, skill01) + scenicPauseBias01 * 0.12f);
        medicVisitWeight = Mathf.Clamp01(Mathf.Lerp(0.1f, 0.02f, skill01) + Random.Range(-0.02f, 0.03f));
        viewpointPauseWeight = Mathf.Clamp01(scenicPauseBias01 + Random.Range(-0.08f, 0.12f));
        idleWanderWeight = Mathf.Clamp01(Mathf.Lerp(0.1f, 0.28f, roamResortBias01));
        leaveAreaWeight = Mathf.Clamp01(Mathf.Lerp(0.04f, 0.14f, roamResortBias01));
        groupAffinity = Mathf.Clamp01((socialness * 0.7f) + (crowdTolerance01 * 0.3f));
        turnRadiusPreference01 = Random.Range(0.15f, 0.85f);
        edgePreference01 = Random.Range(0.1f, 0.8f);
        mergeCaution01 = Random.Range(0.15f, 0.85f);
        overtakeTendency01 = Random.Range(0.05f, 0.75f);

        if (autoDerivePreferredDifficulty)
            DeriveDifficultyBand(skill01, confidence01, caution01, out minPreferredDifficulty, out maxPreferredDifficulty);

        repeatRunBias01 = Mathf.Clamp01(Mathf.Lerp(0.05f, 0.65f, Random.value * (1f - roamResortBias01)));
        ApplyArchetypeBias();

        float decisiveness = Mathf.Clamp01((skill01 * 0.45f) + (confidence01 * 0.35f) + ((1f - caution01) * 0.20f));

        cruiseSpeedMps = Mathf.Lerp(6f, 22f, skill01) * Mathf.Lerp(0.85f, 1.12f, confidence01);
        reactionLookaheadMeters = Mathf.Lerp(9f, 28f, skill01);
        turnAggression = Mathf.Lerp(0.45f, 1.15f, decisiveness);
        recoveryDelaySeconds = Mathf.Lerp(3.5f, 1.0f, skill01);

        desiredRestSecondsMin = Mathf.Lerp(2.5f, 0.75f, confidence01);
        desiredRestSecondsMax = Mathf.Max(desiredRestSecondsMin + 0.25f, Mathf.Lerp(6f, 2f, skill01));

        ambientPauseChance = Mathf.Lerp(0.02f, 0.22f, scenicPauseBias01);
        ambientPauseDurationMin = Mathf.Lerp(0.2f, 1.0f, scenicPauseBias01);
        ambientPauseDurationMax = Mathf.Lerp(0.75f, 3.0f, scenicPauseBias01);

        skierName = GenerateName();
    }

    private void ApplyArchetypeBias()
    {
        switch (archetype)
        {
            case SkierArchetype.BeginnerCruiser:
                skill01 = Mathf.Min(skill01, 0.35f);
                confidence01 = Mathf.Min(confidence01, 0.42f);
                caution01 = Mathf.Max(caution01, 0.68f);
                scenicPauseBias01 = Mathf.Max(scenicPauseBias01, 0.35f);
                lineVariation01 = Mathf.Min(lineVariation01, 0.35f);
                assertiveness01 = Mathf.Min(assertiveness01, 0.35f);
                hesitationOnSteeps01 = Mathf.Max(hesitationOnSteeps01, 0.65f);
                socialPresenceBias01 = Mathf.Max(socialPresenceBias01, 0.3f);
                socialness = Mathf.Max(socialness, 0.35f);
                riskTolerance = Mathf.Min(riskTolerance, 0.35f);
                kioskVisitWeight = Mathf.Max(kioskVisitWeight, 0.35f);
                lodgeRestWeight = Mathf.Max(lodgeRestWeight, 0.32f);
                trickInterest = Mathf.Min(trickInterest, 0.12f);
                turnRadiusPreference01 = Mathf.Max(turnRadiusPreference01, 0.7f);
                edgePreference01 = Mathf.Max(edgePreference01, 0.55f);
                mergeCaution01 = Mathf.Max(mergeCaution01, 0.72f);
                overtakeTendency01 = Mathf.Min(overtakeTendency01, 0.15f);
                roamResortBias01 = Mathf.Min(roamResortBias01, 0.35f);
                break;

            case SkierArchetype.CasualTourist:
                scenicPauseBias01 = Mathf.Max(scenicPauseBias01, 0.45f);
                socialPresenceBias01 = Mathf.Max(socialPresenceBias01, 0.4f);
                socialness = Mathf.Max(socialness, 0.55f);
                explorationWeight = Mathf.Max(explorationWeight, 0.55f);
                kioskVisitWeight = Mathf.Max(kioskVisitWeight, 0.28f);
                viewpointPauseWeight = Mathf.Max(viewpointPauseWeight, 0.42f);
                roamResortBias01 = Mathf.Max(roamResortBias01, 0.45f);
                repeatRunBias01 = Mathf.Min(repeatRunBias01, 0.35f);
                turnRadiusPreference01 = Mathf.Max(turnRadiusPreference01, 0.58f);
                mergeCaution01 = Mathf.Max(mergeCaution01, 0.58f);
                break;

            case SkierArchetype.ConfidentCarver:
                skill01 = Mathf.Max(skill01, 0.62f);
                confidence01 = Mathf.Max(confidence01, 0.66f);
                caution01 = Mathf.Min(caution01, 0.42f);
                lineVariation01 = Mathf.Min(lineVariation01, 0.45f);
                assertiveness01 = Mathf.Max(assertiveness01, 0.58f);
                liftUseWeight = Mathf.Max(liftUseWeight, 0.62f);
                raceInterest = Mathf.Max(raceInterest, 0.35f);
                lodgeRestWeight = Mathf.Min(lodgeRestWeight, 0.22f);
                hesitationOnSteeps01 = Mathf.Min(hesitationOnSteeps01, 0.28f);
                turnRadiusPreference01 = Mathf.Min(turnRadiusPreference01, 0.38f);
                edgePreference01 = Mathf.Min(edgePreference01, 0.42f);
                mergeCaution01 = Mathf.Min(mergeCaution01, 0.45f);
                overtakeTendency01 = Mathf.Max(overtakeTendency01, 0.48f);
                break;

            case SkierArchetype.AggressiveLocal:
                skill01 = Mathf.Max(skill01, 0.72f);
                confidence01 = Mathf.Max(confidence01, 0.78f);
                caution01 = Mathf.Min(caution01, 0.24f);
                crowdTolerance01 = Mathf.Max(crowdTolerance01, 0.72f);
                roamResortBias01 = Mathf.Min(roamResortBias01, 0.25f);
                lineVariation01 = Mathf.Max(lineVariation01, 0.45f);
                assertiveness01 = Mathf.Max(assertiveness01, 0.82f);
                riskTolerance = Mathf.Max(riskTolerance, 0.8f);
                raceInterest = Mathf.Max(raceInterest, 0.65f);
                trickInterest = Mathf.Max(trickInterest, 0.45f);
                kioskVisitWeight = Mathf.Min(kioskVisitWeight, 0.12f);
                lodgeRestWeight = Mathf.Min(lodgeRestWeight, 0.12f);
                hesitationOnSteeps01 = Mathf.Min(hesitationOnSteeps01, 0.18f);
                turnRadiusPreference01 = Mathf.Min(turnRadiusPreference01, 0.3f);
                edgePreference01 = Mathf.Min(edgePreference01, 0.3f);
                mergeCaution01 = Mathf.Min(mergeCaution01, 0.28f);
                overtakeTendency01 = Mathf.Max(overtakeTendency01, 0.74f);
                repeatRunBias01 = Mathf.Max(repeatRunBias01, 0.45f);
                break;

            case SkierArchetype.ScenicWanderer:
                confidence01 = Mathf.Min(confidence01, 0.6f);
                scenicPauseBias01 = Mathf.Max(scenicPauseBias01, 0.72f);
                crowdTolerance01 = Mathf.Min(crowdTolerance01, 0.45f);
                roamResortBias01 = Mathf.Max(roamResortBias01, 0.7f);
                lineVariation01 = Mathf.Max(lineVariation01, 0.48f);
                socialPresenceBias01 = Mathf.Max(socialPresenceBias01, 0.45f);
                explorationWeight = Mathf.Max(explorationWeight, 0.75f);
                viewpointPauseWeight = Mathf.Max(viewpointPauseWeight, 0.72f);
                raceInterest = Mathf.Min(raceInterest, 0.28f);
                trickInterest = Mathf.Min(trickInterest, 0.2f);
                turnRadiusPreference01 = Mathf.Max(turnRadiusPreference01, 0.62f);
                edgePreference01 = Mathf.Max(edgePreference01, 0.62f);
                mergeCaution01 = Mathf.Max(mergeCaution01, 0.55f);
                overtakeTendency01 = Mathf.Min(overtakeTendency01, 0.22f);
                break;

            case SkierArchetype.LiftLapper:
                skill01 = Mathf.Max(skill01, 0.48f);
                confidence01 = Mathf.Max(confidence01, 0.52f);
                roamResortBias01 = Mathf.Min(roamResortBias01, 0.28f);
                repeatRunBias01 = Mathf.Max(repeatRunBias01, 0.6f);
                scenicPauseBias01 = Mathf.Min(scenicPauseBias01, 0.3f);
                liftUseWeight = Mathf.Max(liftUseWeight, 0.9f);
                viewpointPauseWeight = Mathf.Min(viewpointPauseWeight, 0.18f);
                idleWanderWeight = Mathf.Min(idleWanderWeight, 0.12f);
                turnRadiusPreference01 = Mathf.Lerp(turnRadiusPreference01, 0.45f, 0.6f);
                mergeCaution01 = Mathf.Lerp(mergeCaution01, 0.45f, 0.6f);
                break;

            case SkierArchetype.SocialFollower:
                crowdTolerance01 = Mathf.Max(crowdTolerance01, 0.62f);
                scenicPauseBias01 = Mathf.Max(scenicPauseBias01, 0.38f);
                socialPresenceBias01 = Mathf.Max(socialPresenceBias01, 0.78f);
                socialness = Mathf.Max(socialness, 0.82f);
                groupAffinity = Mathf.Max(groupAffinity, 0.75f);
                lodgeRestWeight = Mathf.Max(lodgeRestWeight, 0.25f);
                edgePreference01 = Mathf.Max(edgePreference01, 0.55f);
                mergeCaution01 = Mathf.Max(mergeCaution01, 0.62f);
                overtakeTendency01 = Mathf.Min(overtakeTendency01, 0.25f);
                break;

            case SkierArchetype.ErraticRookie:
                skill01 = Mathf.Min(skill01, 0.45f);
                confidence01 = Mathf.Clamp01(confidence01 + Random.Range(-0.15f, 0.2f));
                caution01 = Mathf.Clamp01(caution01 + Random.Range(-0.15f, 0.2f));
                jumpiness01 = Mathf.Max(jumpiness01, 0.45f);
                lineVariation01 = Mathf.Max(lineVariation01, 0.72f);
                trickInterest = Mathf.Max(trickInterest, 0.35f);
                idleWanderWeight = Mathf.Max(idleWanderWeight, 0.24f);
                hesitationOnSteeps01 = Mathf.Max(hesitationOnSteeps01, 0.45f);
                turnRadiusPreference01 = Mathf.Clamp01(turnRadiusPreference01 + Random.Range(-0.25f, 0.25f));
                edgePreference01 = Mathf.Clamp01(edgePreference01 + Random.Range(-0.2f, 0.3f));
                mergeCaution01 = Mathf.Clamp01(mergeCaution01 + Random.Range(-0.2f, 0.25f));
                overtakeTendency01 = Mathf.Clamp01(overtakeTendency01 + Random.Range(-0.15f, 0.2f));
                break;
        }
    }

    private static float Sample01(Vector2 range)
    {
        float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
        float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
        return Random.Range(min, max);
    }

    private static Vector2 NormalizeDurationRange(Vector2 range, float fallbackMin, float fallbackMax)
    {
        float min = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        if (max <= 0.1f)
            return new Vector2(fallbackMin, Mathf.Max(fallbackMin, fallbackMax));

        return new Vector2(min, max);
    }

    private void DeriveDifficultyBand(float skill, float confidence, float caution, out SkiRunDifficulty minDiff, out SkiRunDifficulty maxDiff)
    {
        float composite = (skill * 0.55f) + (confidence * 0.30f) + ((1f - caution) * 0.15f);

        if (composite < 0.22f)
        {
            minDiff = SkiRunDifficulty.Green;
            maxDiff = SkiRunDifficulty.Green;
            return;
        }

        if (composite < 0.45f)
        {
            minDiff = SkiRunDifficulty.Green;
            maxDiff = SkiRunDifficulty.Blue;
            return;
        }

        if (composite < 0.72f)
        {
            minDiff = SkiRunDifficulty.Blue;
            maxDiff = SkiRunDifficulty.Red;
            return;
        }

        minDiff = SkiRunDifficulty.Red;
        maxDiff = SkiRunDifficulty.Black;
    }

    public bool PrefersDifficulty(SkiRunDifficulty difficulty)
    {
        return (int)difficulty >= (int)minPreferredDifficulty &&
               (int)difficulty <= (int)maxPreferredDifficulty;
    }

    private string GenerateName()
    {
        string[] first =
        {
            "Alex", "Sam", "Mia", "Luca", "Noah", "Ruby", "Eli", "Zoe",
            "Mason", "Ava", "Ben", "Sophie", "Finn", "Levi", "Aria", "Nina",
            "Owen", "Chloe", "Jude", "Skye", "Kai", "Ella", "Max", "Ivy"
        };

        string[] last =
        {
            "Reed", "Snow", "Vale", "Hart", "Pike", "Frost", "Marlow", "Quinn",
            "North", "Ridge", "Bishop", "Mercer", "Fox", "Blair", "Wren", "Hayes",
            "Lane", "Brooks", "Winter", "Stone", "Hale", "Knight", "Shaw", "Cole"
        };

        return $"{first[Random.Range(0, first.Length)]} {last[Random.Range(0, last.Length)]}";
    }
}
