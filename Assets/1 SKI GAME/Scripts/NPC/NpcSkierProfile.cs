using UnityEngine;
using SkiGame.Runs;

[DisallowMultipleComponent]
public class NpcSkierProfile : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string skierName = "Skier";

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

        if (autoDerivePreferredDifficulty)
            DeriveDifficultyBand(skill01, confidence01, caution01, out minPreferredDifficulty, out maxPreferredDifficulty);

        repeatRunBias01 = Mathf.Clamp01(Mathf.Lerp(0.05f, 0.65f, Random.value * (1f - roamResortBias01)));

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

    private static float Sample01(Vector2 range)
    {
        float min = Mathf.Clamp01(Mathf.Min(range.x, range.y));
        float max = Mathf.Clamp01(Mathf.Max(range.x, range.y));
        return Random.Range(min, max);
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