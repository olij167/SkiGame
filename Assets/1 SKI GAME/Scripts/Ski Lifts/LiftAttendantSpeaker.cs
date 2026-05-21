using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LiftAttendantSpeaker : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private NpcDialogueAgent attendantDialogueAgent;
    [SerializeField] private NpcDialogueBankSO dialogueBankOverride;
    [SerializeField] private NpcDialogueSequencePlayer sequencePlayer;

    [Header("Association")]
    [SerializeField] private LiftBoardGate associatedGate;
    [SerializeField] private Component associatedLift;
    [SerializeField] private string associatedLiftId;
    [SerializeField] private Transform associatedStation;
    [SerializeField, Min(0f)] private float maxEventDistance = 20f;
    [SerializeField] private bool requireMatchingGate = true;
    [SerializeField] private bool allowGateNameFallback = true;
    [SerializeField] private bool allowLiftIdFallback = true;
    [SerializeField] private bool allowCarrierAttachEvents = true;
    [SerializeField] private bool rejectCarrierAttachEventsWithoutGate = false;
    [SerializeField] private bool requirePlayerNearby = true;
    [SerializeField, Min(0f)] private float playerNearbyRadius = 25f;

    [Header("Speech Rules")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 3f;
    [SerializeField] private bool speakAllowedResults = true;
    [SerializeField] private bool speakDeniedResults = true;
    [SerializeField] private bool speakOnAccessAllowed = false;
    [SerializeField] private bool speakOnCarrierAttached = true;
    [SerializeField] private bool suppressAmbientBrieflyAfterSpeaking = true;

    [Header("Optional First-Time Hints")]
    [SerializeField] private bool useSequencesForFirstTimeHints;
    [SerializeField] private NpcDialogueSequenceSO firstNoPassSequence;
    [SerializeField] private NpcDialogueSequenceSO firstWrongPassSequence;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private NpcAmbientDialogueEmitter _ambientEmitter;
    private float _nextSpeakTime;
    private bool _lastResultWasAllowed;
    private bool _playedNoPassSequence;
    private bool _playedWrongPassSequence;

    private void Awake()
    {
        if (attendantDialogueAgent == null)
            attendantDialogueAgent = GetComponent<NpcDialogueAgent>();
        if (sequencePlayer == null)
            sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();

        _ambientEmitter = GetComponent<NpcAmbientDialogueEmitter>();
    }

    private void OnEnable()
    {
        LiftAccessResultBus.AccessResult += HandleAccessResult;
    }

    private void OnDisable()
    {
        LiftAccessResultBus.AccessResult -= HandleAccessResult;
    }

    private void HandleAccessResult(LiftAccessResultEvent evt)
    {
        TrySpeak(evt, bypassFilters: false);
    }

    private bool TrySpeak(LiftAccessResultEvent evt, bool bypassFilters)
    {
        LogDebug($"Received event reason={evt.reason} phase={evt.phase} allowed={evt.allowed} gate={DescribeGate(evt.gate)} expectedGate={DescribeGate(associatedGate)} liftId='{evt.liftId}' associatedLiftId='{associatedLiftId}'.");

        if (!bypassFilters && !ShouldRespond(evt))
            return false;

        bool interruptCooldown = _lastResultWasAllowed && !evt.allowed;
        if (!bypassFilters && Time.unscaledTime < _nextSpeakTime && !interruptCooldown)
        {
            LogDebug($"Rejected by cooldown. nextSpeakIn={_nextSpeakTime - Time.unscaledTime:0.00}s");
            return false;
        }

        if (TryPlayFirstTimeSequence(evt))
        {
            StampSpoken(evt);
            return true;
        }

        return TrySpeakBankLine(evt);
    }

    private bool TrySpeakBankLine(LiftAccessResultEvent evt)
    {
        if (attendantDialogueAgent == null)
        {
            LogDebug("Cannot speak: no attendant dialogue agent.");
            return false;
        }

        DialogueContext context = BuildDialogueContext(evt);
        NpcDialogueBankSO originalBank = attendantDialogueAgent.DialogueBank;
        bool spoke = false;
        try
        {
            if (dialogueBankOverride != null)
                attendantDialogueAgent.SetDialogueBank(dialogueBankOverride);

            LogDebug($"Attempting topic='{context.topicId}' trigger={context.trigger} audience={context.audience} bank={(attendantDialogueAgent.DialogueBank != null ? attendantDialogueAgent.DialogueBank.name : "none")}.");
            if (attendantDialogueAgent.DialogueBank != null)
                LogDebug(attendantDialogueAgent.DialogueBank.ExplainCandidateFailure(context.topicId, context));
            spoke = evt.allowed
                ? attendantDialogueAgent.SayContextual(context)
                : attendantDialogueAgent.InterruptAndSayContextual(context);
        }
        finally
        {
            if (dialogueBankOverride != null)
                attendantDialogueAgent.SetDialogueBank(originalBank);
        }

        if (spoke)
            StampSpoken(evt);
        else
            LogDebug($"No line spoke for topic='{context.topicId}' trigger={context.trigger}. Check bank assignment, exact topic match, trigger, requirements, and dialogue budgets.");

        return spoke;
    }

    private bool ShouldRespond(LiftAccessResultEvent evt)
    {
        if (attendantDialogueAgent == null)
        {
            LogDebug("Rejected: no attendant dialogue agent.");
            return false;
        }

        if (evt.allowed && !speakAllowedResults)
        {
            LogDebug("Rejected: speakAllowedResults is false.");
            return false;
        }
        if (!evt.allowed && !speakDeniedResults)
        {
            LogDebug("Rejected: speakDeniedResults is false.");
            return false;
        }

        if (evt.allowed && evt.phase == LiftAccessResultPhase.QueueJoined && !speakOnAccessAllowed)
        {
            LogDebug("Rejected: allowed queue/access phase is disabled to avoid duplicate welcome lines.");
            return false;
        }

        bool carrierPhase = evt.phase == LiftAccessResultPhase.CarrierAttached || evt.phase == LiftAccessResultPhase.DirectCarrierAttach;
        if (evt.allowed && carrierPhase && !speakOnCarrierAttached)
        {
            LogDebug("Rejected: allowed carrier-attached phase is disabled.");
            return false;
        }

        if (carrierPhase && !allowCarrierAttachEvents)
        {
            LogDebug("Rejected: carrier attach events are disabled.");
            return false;
        }

        if (carrierPhase && evt.gate == null && rejectCarrierAttachEventsWithoutGate)
        {
            LogDebug("Rejected: carrier attach event has no gate and rejectCarrierAttachEventsWithoutGate is true.");
            return false;
        }

        if (associatedGate != null)
        {
            if (requireMatchingGate && !GateMatches(evt))
            {
                LogDebug($"Rejected: wrong gate. expected={DescribeGate(associatedGate)} got={DescribeGate(evt.gate)} associatedLiftId='{associatedLiftId}' receivedLiftId='{evt.liftId}'");
                return false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(associatedLiftId))
        {
            if (!string.Equals(associatedLiftId.Trim(), evt.liftId?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"Rejected: wrong lift id. expected='{associatedLiftId}' got='{evt.liftId}'");
                return false;
            }
        }
        else if (associatedLift != null)
        {
            if (evt.lift != associatedLift)
            {
                LogDebug("Rejected: wrong associated lift component.");
                return false;
            }
        }

        if (associatedStation != null && evt.station != null)
        {
            float distance = Vector3.Distance(associatedStation.position, evt.station.position);
            if (distance > maxEventDistance + 0.05f)
            {
                LogDebug($"Rejected: station too far. distance={distance:0.000} max={maxEventDistance:0.000}");
                return false;
            }
        }
        else if (maxEventDistance > 0f)
        {
            Vector3 eventPosition = ResolveEventPosition(evt);
            float distance = Vector3.Distance(transform.position, eventPosition);
            if (distance > maxEventDistance + 0.05f)
            {
                LogDebug($"Rejected: event too far. distance={distance:0.000} max={maxEventDistance:0.000}");
                return false;
            }
        }

        if (requirePlayerNearby)
        {
            Transform player = evt.player != null ? evt.player.transform : (evt.rider != null ? evt.rider.transform : null);
            if (player == null)
                player = FindObjectOfType<SkiController>()?.transform;

            if (player == null)
            {
                LogDebug("Rejected: no player found for nearby check.");
                return false;
            }

            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > playerNearbyRadius)
            {
                LogDebug($"Rejected: player too far. distance={distance:0.000} max={playerNearbyRadius:0.000}");
                return false;
            }
        }

        return true;
    }

    private bool GateMatches(LiftAccessResultEvent evt)
    {
        if (associatedGate == null)
            return true;

        if (evt.gate == associatedGate)
            return true;

        if (evt.gate != null && allowGateNameFallback &&
            string.Equals(evt.gate.name, associatedGate.name, StringComparison.OrdinalIgnoreCase))
        {
            LogDebug($"Gate matched by name fallback. expected={DescribeGate(associatedGate)} got={DescribeGate(evt.gate)}");
            return true;
        }

        if (allowLiftIdFallback && EventMatchesAssociatedLift(evt))
        {
            LogDebug($"Gate mismatch tolerated by lift-id fallback. expected={DescribeGate(associatedGate)} got={DescribeGate(evt.gate)}");
            return true;
        }

        if (evt.gate == null && allowCarrierAttachEvents && EventMatchesAssociatedLift(evt))
        {
            LogDebug("Carrier event without gate matched by associated lift.");
            return true;
        }

        if (associatedStation != null && evt.station != null)
        {
            float distance = Vector3.Distance(associatedStation.position, evt.station.position);
            if (distance <= maxEventDistance + 0.05f)
            {
                LogDebug($"Gate mismatch tolerated by station proximity. distance={distance:0.000}");
                return true;
            }
        }

        return false;
    }

    private bool EventMatchesAssociatedLift(LiftAccessResultEvent evt)
    {
        if (associatedLift != null && evt.lift == associatedLift)
            return true;

        string expectedId = !string.IsNullOrWhiteSpace(associatedLiftId)
            ? associatedLiftId.Trim()
            : associatedLift != null ? associatedLift.name.Trim() : associatedGate != null && associatedGate.line != null ? associatedGate.line.name.Trim() : string.Empty;

        return !string.IsNullOrWhiteSpace(expectedId) &&
               !string.IsNullOrWhiteSpace(evt.liftId) &&
               string.Equals(expectedId, evt.liftId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeGate(LiftBoardGate gate)
    {
        return gate != null ? $"{gate.name}#{gate.GetInstanceID()}" : "none";
    }

    private bool TryPlayFirstTimeSequence(LiftAccessResultEvent evt)
    {
        if (!useSequencesForFirstTimeHints || sequencePlayer == null)
            return false;

        NpcDialogueSequenceSO sequence = null;
        if (evt.reason == LiftAccessResultReason.NoPass && !_playedNoPassSequence)
        {
            sequence = firstNoPassSequence;
            bool started = sequence != null && sequencePlayer.Play(sequence, BuildDialogueContext(evt), attendantDialogueAgent);
            _playedNoPassSequence = started;
            return started;
        }
        else if ((evt.reason == LiftAccessResultReason.WrongPassTier || evt.reason == LiftAccessResultReason.WrongPassRegion) && !_playedWrongPassSequence)
        {
            sequence = firstWrongPassSequence;
            bool started = sequence != null && sequencePlayer.Play(sequence, BuildDialogueContext(evt), attendantDialogueAgent);
            _playedWrongPassSequence = started;
            return started;
        }

        return false;
    }

    private DialogueContext BuildDialogueContext(LiftAccessResultEvent evt)
    {
        LiftLine line = ResolveContextLift(evt);
        Transform station = ResolveContextStation(evt, line);

        SkiPassManager passMgr = SkiPassManager.Instance;
        SkiPassConfigSO config = passMgr != null ? passMgr.Config : null;

        string liftId = FirstUseful(
            evt.liftId,
            line != null ? line.name : null,
            associatedLiftId);

        string liftName = FirstUseful(
            evt.liftName,
            line != null ? line.name : null,
            "this lift");

        string stationName = FirstUseful(
            evt.stationName,
            station != null ? station.name : null,
            associatedStation != null ? associatedStation.name : null,
            "this station");

        string requiredPassId = FirstUseful(
            evt.requiredPassId,
            line != null && config != null ? line.GetResolvedRequiredPassId(config) : null,
            line != null ? line.RequiredPassId : null);

        string requiredPassName = ResolveRequiredPassName(evt, line, config, requiredPassId);

        int requiredPassTier = evt.requiredPassTier >= 0
            ? evt.requiredPassTier
            : ResolveRequiredPassTier(line, config, requiredPassId);

        string currentPassId = FirstUseful(
            evt.currentPassId,
            passMgr != null ? passMgr.GetCurrentPassId() : null);

        string currentPassName = FirstUseful(
            evt.currentPassName,
            passMgr != null ? passMgr.GetCurrentPassDisplayName() : null,
            "your pass");

        string passExpiryText = FirstUseful(
            evt.passExpiryText,
            passMgr != null ? passMgr.GetRemainingTimeString() : null,
            "soon");

        float passExpirySeconds = evt.passExpirySeconds;
        if (passExpirySeconds <= 0f && passMgr != null)
        {
            double remainingHours = passMgr.GetRemainingHours();
            passExpirySeconds = double.IsInfinity(remainingHours)
                ? float.PositiveInfinity
                : Mathf.Max(0f, (float)(remainingHours * 3600.0));
        }

        string regionName = FirstUseful(
            evt.regionName,
            ResolveRegionNameFromRequiredPass(config, requiredPassId),
            "this area");

        var context = new DialogueContext
        {
            topicId = ResolveTopic(evt.reason),
            trigger = ResolveTrigger(evt.reason),
            audience = NpcDialogueAudience.Player,
            preferImportantStyle = !evt.allowed,
            importanceOverride = evt.allowed ? NpcDialogueImportance.Interaction : NpcDialogueImportance.Critical,
            priorityOverride = evt.allowed ? 300 : 800,

            liftId = liftId,
            liftName = liftName,
            stationName = stationName,
            regionName = regionName,

            requiredPassId = requiredPassId,
            requiredPassName = requiredPassName,
            requiredPassTier = requiredPassTier,

            currentPassId = currentPassId,
            currentPassName = currentPassName,
            currentPassTier = evt.currentPassTier,

            passName = currentPassName,
            passExpiry = passExpiryText,
            passExpirySeconds = passExpirySeconds,

            kioskHint = FirstUseful(evt.kioskHint),
            upgradeHint = FirstUseful(evt.upgradeHint),
        };

        context.values = BuildValueSet(
            evt,
            liftId,
            liftName,
            stationName,
            regionName,
            requiredPassId,
            requiredPassName,
            requiredPassTier,
            currentPassId,
            currentPassName,
            context.currentPassTier,
            passExpiryText,
            passExpirySeconds);

        if (logDebug)
        {
            Debug.Log(
                $"[{nameof(LiftAttendantSpeaker)}] Built context: " +
                $"liftId='{context.liftId}' liftName='{context.liftName}' station='{context.stationName}' " +
                $"requiredPassId='{context.requiredPassId}' requiredPassName='{context.requiredPassName}' " +
                $"currentPass='{context.currentPassName}' expiry='{context.passExpiry}' region='{context.regionName}'",
                this);
        }

        return context;
    }

    public bool TryBuildAmbientDialogueContext(
    string topicId,
    NpcDialogueTrigger trigger,
    NpcDialogueAudience audience,
    out DialogueContext context)
    {
        LiftAccessResultEvent evt = BuildAmbientContextEvent();

        context = BuildDialogueContext(evt);
        context.topicId = topicId;
        context.trigger = trigger;
        context.audience = audience;

        // Ambient lines should not inherit boarding/access urgency styling.
        context.preferImportantStyle = false;
        context.priorityOverride = 0;
        context.durationOverride = 0f;
        context.importanceOverride = null;
        context.styleOverride = null;

        return !string.IsNullOrWhiteSpace(context.liftName) ||
               !string.IsNullOrWhiteSpace(context.requiredPassName) ||
               !string.IsNullOrWhiteSpace(context.regionName);
    }

    private LiftAccessResultEvent BuildAmbientContextEvent()
    {
        LiftLine line = ResolveContextLift(default);
        Transform station = ResolveContextStation(default, line);

        SkiPassManager passMgr = SkiPassManager.Instance;
        SkiPassConfigSO config = passMgr != null ? passMgr.Config : null;

        string requiredPassId = line != null && config != null
            ? line.GetResolvedRequiredPassId(config)
            : line != null ? line.RequiredPassId : string.Empty;

        string requiredPassName = ResolveRequiredPassName(default, line, config, requiredPassId);

        string currentPassId = passMgr != null ? passMgr.GetCurrentPassId() : string.Empty;
        string currentPassName = passMgr != null ? passMgr.GetCurrentPassDisplayName() : string.Empty;
        string passExpiry = passMgr != null ? passMgr.GetRemainingTimeString() : string.Empty;

        float passExpirySeconds = 0f;
        if (passMgr != null)
        {
            double remainingHours = passMgr.GetRemainingHours();
            passExpirySeconds = double.IsInfinity(remainingHours)
                ? float.PositiveInfinity
                : Mathf.Max(0f, (float)(remainingHours * 3600.0));
        }

        return new LiftAccessResultEvent
        {
            player = FindObjectOfType<SkiController>()?.gameObject,
            rider = FindObjectOfType<SkiController>()?.gameObject,
            gate = associatedGate,
            lift = line,
            station = station,

            // This is only used as a context carrier. It should not imply the player is boarding.
            allowed = true,
            reason = LiftAccessResultReason.Allowed,
            phase = LiftAccessResultPhase.AccessChecked,

            liftId = line != null ? line.name : FirstNonEmpty(associatedLiftId, string.Empty),
            liftName = line != null ? line.name : FirstNonEmpty(associatedLiftId, "this lift"),
            stationName = station != null ? station.name : FirstNonEmpty(associatedStation != null ? associatedStation.name : null, "this station"),

            regionName = string.Empty,

            requiredPassId = requiredPassId,
            requiredPassName = requiredPassName,
            requiredPassTier = ResolveRequiredPassTier(line, config, requiredPassId),

            currentPassId = currentPassId,
            currentPassName = currentPassName,
            currentPassTier = config != null && !string.IsNullOrWhiteSpace(currentPassId)
                ? config.GetLevelIndexByPassId(currentPassId)
                : -1,

            passExpirySeconds = passExpirySeconds,
            passExpiryText = passExpiry,

            kioskHint = "Check the nearest ski pass kiosk.",
            upgradeHint = "Upgrade your pass at the kiosk."
        };
    }

    private static DialogueContextValueSet BuildValueSet(
    LiftAccessResultEvent evt,
    string liftId,
    string liftName,
    string stationName,
    string regionName,
    string requiredPassId,
    string requiredPassName,
    int requiredPassTier,
    string currentPassId,
    string currentPassName,
    int currentPassTier,
    string passExpiryText,
    float passExpirySeconds)
    {
        return new DialogueContextValueSet
        {
            values =
        {
            new DialogueContextValue { key = "lift", value = liftName },
            new DialogueContextValue { key = "liftId", value = liftId },
            new DialogueContextValue { key = "liftName", value = liftName },

            new DialogueContextValue { key = "station", value = stationName },
            new DialogueContextValue { key = "stationName", value = stationName },

            new DialogueContextValue { key = "region", value = regionName },
            new DialogueContextValue { key = "regionName", value = regionName },

            new DialogueContextValue { key = "requiredPass", value = requiredPassName },
            new DialogueContextValue { key = "requiredPassId", value = requiredPassId },
            new DialogueContextValue { key = "requiredPassName", value = requiredPassName },
            new DialogueContextValue { key = "requiredPassTier", value = requiredPassTier >= 0 ? requiredPassTier.ToString() : string.Empty },

            new DialogueContextValue { key = "currentPass", value = currentPassName },
            new DialogueContextValue { key = "currentPassId", value = currentPassId },
            new DialogueContextValue { key = "currentPassName", value = currentPassName },
            new DialogueContextValue { key = "currentPassTier", value = currentPassTier >= 0 ? currentPassTier.ToString() : string.Empty },

            new DialogueContextValue { key = "pass", value = currentPassName },
            new DialogueContextValue { key = "passName", value = currentPassName },
            new DialogueContextValue { key = "passExpiry", value = passExpiryText },
            new DialogueContextValue { key = "passExpirySeconds", value = FormatSeconds(passExpirySeconds) },

            new DialogueContextValue { key = "kioskHint", value = evt.kioskHint },
            new DialogueContextValue { key = "upgradeHint", value = evt.upgradeHint }
        }
        };
    }

    private LiftLine ResolveContextLift(LiftAccessResultEvent evt)
    {
        if (evt.lift is LiftLine eventLine)
            return eventLine;

        if (associatedLift is LiftLine associatedLine)
            return associatedLine;

        if (associatedGate != null && associatedGate.line != null)
            return associatedGate.line;

        if (!string.IsNullOrWhiteSpace(associatedLiftId))
        {
#if UNITY_2023_1_OR_NEWER
            var lines = FindObjectsByType<LiftLine>(FindObjectsSortMode.None);
#else
        var lines = FindObjectsOfType<LiftLine>();
#endif
            string expected = associatedLiftId.Trim();
            for (int i = 0; i < lines.Length; i++)
            {
                LiftLine line = lines[i];
                if (line != null && string.Equals(line.name.Trim(), expected, StringComparison.OrdinalIgnoreCase))
                    return line;
            }
        }

        return null;
    }

    private Transform ResolveContextStation(LiftAccessResultEvent evt, LiftLine line)
    {
        if (evt.station != null)
            return evt.station;

        if (associatedStation != null)
            return associatedStation;

        if (associatedGate != null)
        {
            if (associatedGate.boardingPoint != null)
                return associatedGate.boardingPoint;

            return associatedGate.transform;
        }

        if (line == null)
            return null;

        Transform bottom = line.bottomStation;
        Transform top = line.topStation;

        if (bottom == null)
            return top;
        if (top == null)
            return bottom;

        float bottomSq = (transform.position - bottom.position).sqrMagnitude;
        float topSq = (transform.position - top.position).sqrMagnitude;
        return bottomSq <= topSq ? bottom : top;
    }

    private static string ResolveRequiredPassName(
        LiftAccessResultEvent evt,
        LiftLine line,
        SkiPassConfigSO config,
        string requiredPassId)
    {
        string eventName = FirstUseful(evt.requiredPassName);
        if (!IsGenericRequiredPassName(eventName))
            return eventName;

        if (config != null && !string.IsNullOrWhiteSpace(requiredPassId))
        {
            var byId = config.GetByPassId(requiredPassId);
            if (byId != null && !string.IsNullOrWhiteSpace(byId.displayName))
                return byId.displayName.Trim();
        }

        if (line != null)
        {
            string lineName = line.GetRequiredPassDisplayName();
            if (!IsGenericRequiredPassName(lineName))
                return lineName;
        }

        if (!string.IsNullOrWhiteSpace(requiredPassId))
            return requiredPassId.Trim();

        return "the right pass";
    }

    private static int ResolveRequiredPassTier(LiftLine line, SkiPassConfigSO config, string requiredPassId)
    {
        if (config != null && !string.IsNullOrWhiteSpace(requiredPassId))
        {
            int level = config.GetLevelIndexByPassId(requiredPassId);
            if (level >= 0)
                return level;
        }

        return line != null ? line.RequiredPassLevel : -1;
    }

    private static string ResolveRegionNameFromRequiredPass(SkiPassConfigSO config, string requiredPassId)
    {
        if (config == null || string.IsNullOrWhiteSpace(requiredPassId))
            return string.Empty;

        var pass = config.GetByPassId(requiredPassId);
        return pass != null ? FirstUseful(pass.regionId) : string.Empty;
    }

    private static bool IsGenericRequiredPassName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = value.Trim();

        return string.Equals(normalized, "the right pass", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "the required pass", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "Ski pass required", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstUseful(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string trimmed = value.Trim();

            if (string.Equals(trimmed, "this lift", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "this station", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "this area", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "the right pass", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "the required pass", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "your pass", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(trimmed, "soon", StringComparison.OrdinalIgnoreCase))
                continue;

            return trimmed;
        }

        return string.Empty;
    }

    private void StampSpoken(LiftAccessResultEvent evt)
    {
        _lastResultWasAllowed = evt.allowed;
        _nextSpeakTime = Time.unscaledTime + cooldownSeconds;

        if (suppressAmbientBrieflyAfterSpeaking && _ambientEmitter != null)
            _ambientEmitter.PauseForSeconds(Mathf.Max(cooldownSeconds, 1.5f));
    }

    private Vector3 ResolveEventPosition(LiftAccessResultEvent evt)
    {
        if (evt.gate != null)
            return evt.gate.transform.position;
        if (evt.station != null)
            return evt.station.position;
        if (evt.lift != null)
            return evt.lift.transform.position;

        return transform.position;
    }

    private static NpcDialogueTrigger ResolveTrigger(LiftAccessResultReason reason)
    {
        return reason switch
        {
            LiftAccessResultReason.Allowed => NpcDialogueTrigger.LiftAccessAllowed,
            LiftAccessResultReason.PassExpired => NpcDialogueTrigger.PassExpired,
            _ => NpcDialogueTrigger.LiftAccessDenied
        };
    }

    private static string FormatSeconds(float seconds)
    {
        if (float.IsPositiveInfinity(seconds))
            return "infinite";

        return seconds > 0f ? Mathf.CeilToInt(seconds).ToString() : "0";
    }

    private static string ResolveTopic(LiftAccessResultReason reason)
    {
        return reason switch
        {
            LiftAccessResultReason.Allowed => "lift.allowed",
            LiftAccessResultReason.NoPass => "lift.no_pass",
            LiftAccessResultReason.PassExpired => "lift.pass_expired",
            LiftAccessResultReason.WrongPassTier => "lift.wrong_pass",
            LiftAccessResultReason.WrongPassRegion => "lift.wrong_region",
            LiftAccessResultReason.LiftClosed => "lift.closed",
            LiftAccessResultReason.AlreadyBoarding => "lift.already_boarding",
            LiftAccessResultReason.AlreadyRiding => "lift.already_riding",
            LiftAccessResultReason.TooFar => "lift.too_far",
            _ => "lift.unknown_denied"
        };
    }

    [ContextMenu("Print Ambient Lift Dialogue Context")]
    private void PrintAmbientLiftDialogueContext()
    {
        if (!TryBuildAmbientDialogueContext(
                "ambient",
                NpcDialogueTrigger.Ambient,
                NpcDialogueAudience.Player,
                out DialogueContext context))
        {
            Debug.LogWarning($"[{nameof(LiftAttendantSpeaker)}] {name}: failed to build ambient lift context.", this);
            return;
        }

        Debug.Log(
            $"[{nameof(LiftAttendantSpeaker)}] {name} ambient lift context\n" +
            $"topic='{context.topicId}' trigger={context.trigger} audience={context.audience}\n" +
            $"liftId='{context.liftId}' liftName='{context.liftName}' station='{context.stationName}'\n" +
            $"requiredPassId='{context.requiredPassId}' requiredPassName='{context.requiredPassName}' requiredPassTier={context.requiredPassTier}\n" +
            $"currentPassId='{context.currentPassId}' currentPassName='{context.currentPassName}' expiry='{context.passExpiry}'\n" +
            $"region='{context.regionName}' kioskHint='{context.kioskHint}' upgradeHint='{context.upgradeHint}'",
            this);
    }

    [ContextMenu("Test Allowed Line")]
    private void TestAllowedLine()
    {
        TrySpeak(BuildDebugEvent(true, LiftAccessResultReason.Allowed), bypassFilters: true);
    }

    [ContextMenu("Test No Pass Line")]
    private void TestNoPassLine()
    {
        TrySpeak(BuildDebugEvent(false, LiftAccessResultReason.NoPass), bypassFilters: true);
    }

    [ContextMenu("Test Expired Pass Line")]
    private void TestExpiredPassLine()
    {
        TrySpeak(BuildDebugEvent(false, LiftAccessResultReason.PassExpired), bypassFilters: true);
    }

    [ContextMenu("Test Wrong Pass Line")]
    private void TestWrongPassLine()
    {
        TrySpeak(BuildDebugEvent(false, LiftAccessResultReason.WrongPassTier), bypassFilters: true);
    }

    [ContextMenu("Test Allowed Line Through Filters")]
    private void TestAllowedLineThroughFilters()
    {
        TrySpeak(BuildDebugEvent(true, LiftAccessResultReason.Allowed), bypassFilters: false);
    }

    private LiftAccessResultEvent BuildDebugEvent(bool allowed, LiftAccessResultReason reason)
    {
        LiftLine line = associatedLift as LiftLine;
        if (line == null && associatedGate != null)
            line = associatedGate.line;

        Transform station = associatedStation != null
            ? associatedStation
            : (associatedGate != null ? associatedGate.transform : transform);

        return new LiftAccessResultEvent
        {
            player = FindObjectOfType<SkiController>()?.gameObject,
            rider = FindObjectOfType<SkiController>()?.gameObject,
            gate = associatedGate,
            lift = line,
            station = station,
            allowed = allowed,
            reason = reason,
            phase = allowed ? LiftAccessResultPhase.CarrierAttached : LiftAccessResultPhase.AccessChecked,
            liftId = line != null ? line.name : FirstNonEmpty(associatedLiftId, "debug_lift"),
            liftName = line != null ? line.name : "Debug Lift",
            stationName = station != null ? station.name : "Debug Station",
            regionName = "this area",
            requiredPassId = "advanced",
            requiredPassName = "Advanced Pass",
            requiredPassTier = 1,
            currentPassId = "starter",
            currentPassName = "Starter Pass",
            currentPassTier = 0,
            passExpirySeconds = 3600f,
            passExpiryText = "Expires in 1h",
            kioskHint = "Head to the kiosk near the lodge.",
            upgradeHint = "Upgrade your pass at the kiosk."
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private void LogDebug(string message)
    {
        if (!logDebug)
            return;

        Debug.Log($"[{nameof(LiftAttendantSpeaker)}] {name}: {message}", this);
    }

    private void OnValidate()
    {
        if (attendantDialogueAgent == null)
            attendantDialogueAgent = GetComponent<NpcDialogueAgent>();
        if (sequencePlayer == null)
            sequencePlayer = GetComponent<NpcDialogueSequencePlayer>();

        if (attendantDialogueAgent == null)
            Debug.LogWarning($"[{nameof(LiftAttendantSpeaker)}] {name} has no attendant dialogue agent.", this);

        if (requireMatchingGate && associatedGate == null && associatedLift == null && string.IsNullOrWhiteSpace(associatedLiftId) && associatedStation == null)
            Debug.LogWarning($"[{nameof(LiftAttendantSpeaker)}] {name} requires matching lift context but has no gate/lift/station/id association.", this);

        if (attendantDialogueAgent != null && attendantDialogueAgent.DialogueBank == null && dialogueBankOverride == null)
            Debug.LogWarning($"[{nameof(LiftAttendantSpeaker)}] {name} has no dialogue bank on agent and no override bank.", this);
    }
}
