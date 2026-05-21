using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcSocialActor : MonoBehaviour
{
    [SerializeField] private NpcDialogueAgent dialogueAgent;
    [SerializeField] private NpcIdentity identity;
    [SerializeField] private NpcGenericInteractionProfileSO genericProfile;
    [SerializeField] private string[] socialTags;
    [SerializeField] private string socialRoleOverride;
    [SerializeField] private bool allowSocialChatter = true;
    [SerializeField] private bool allowNpcToNpcDialogue = true;
    [SerializeField] private bool allowPlayerFacingSocialBarks = true;
    [SerializeField] private bool suppressWhileQuestOfferActive = true;
    [SerializeField] private bool suppressWhileInteracting = true;
    [SerializeField] private bool suppressWhileRidingLift = true;
    [SerializeField] private bool suppressWhileRacing = true;
    [SerializeField] private bool suppressWhileStacked = true;
    [SerializeField, Min(0f)] private float personalCooldownSeconds = 10f;
    [SerializeField] private Vector2 socialRejoinCooldownSeconds = new Vector2(30f, 90f);
    [SerializeField, Range(0f, 1f)] private float forceLeaveAfterConversationChance = 0.4f;
    [SerializeField] private bool rotateTowardSpeaker = true;
    [SerializeField, Min(0f)] private float lookAtTurnSpeed = 6f;
    [SerializeField] private bool logDebug;

    private NpcTalkInteractable _talkInteractable;
    private NpcQuestGiver _questGiver;
    private LiftRider _liftRider;
    private SkiController _skiController;
    private RaceCourseNpcRacer _raceRacer;
    private Transform _lookTarget;
    private float _cooldownUntil;
    private bool _inSocialExchange;
    private NpcSocialAnchor _lastSocialAnchor;
    private NpcSocialGroupSO _lastSocialGroup;
    private float _lastSocialSequenceTime = -999f;
    private float _anchorCooldownUntil;

    public NpcDialogueAgent DialogueAgent => dialogueAgent;
    public NpcIdentity Identity => identity;
    public bool IsInSocialExchange => _inSocialExchange;
    public bool AllowNpcToNpcDialogue => allowNpcToNpcDialogue;
    public bool AllowPlayerFacingSocialBarks => allowPlayerFacingSocialBarks;
    public NpcSocialAnchor LastSocialAnchor => _lastSocialAnchor;
    public NpcSocialGroupSO LastSocialGroup => _lastSocialGroup;
    public float LastSocialSequenceTime => _lastSocialSequenceTime;
    public string SocialRole => !string.IsNullOrWhiteSpace(socialRoleOverride) ? socialRoleOverride.Trim() : ResolveDefaultRole();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        NpcSocialDirector.RegisterActor(this);
    }

    private void OnDisable()
    {
        NpcSocialDirector.UnregisterActor(this);
    }

    private void Update()
    {
        if (!rotateTowardSpeaker || _lookTarget == null || _liftRider != null && _liftRider.IsAttached)
            return;

        Vector3 toTarget = _lookTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtTurnSpeed);
    }

    public bool IsAvailableForSocialDialogue()
    {
        if (!allowSocialChatter || _inSocialExchange || dialogueAgent == null || !isActiveAndEnabled)
            return false;

        if (!CanSpeakNow())
            return false;

        if (suppressWhileQuestOfferActive && _questGiver != null && _questGiver.HasActiveOfferSession)
            return false;

        if (suppressWhileQuestOfferActive && dialogueAgent.Presenter != null && dialogueAgent.Presenter.IsShowingPersistentContent)
            return false;

        if (suppressWhileInteracting && _talkInteractable != null && _talkInteractable.IsActivelyInteracting)
            return false;

        if (suppressWhileRidingLift && _liftRider != null && _liftRider.IsAttached)
            return false;

        if (suppressWhileRacing && _raceRacer != null && !_raceRacer.IsFinished)
            return false;

        if (suppressWhileStacked && _skiController != null && _skiController.IsStacked)
            return false;

        return true;
    }

    public bool IsCoolingDownForAnchor(NpcSocialAnchor anchor)
    {
        return anchor != null && _lastSocialAnchor == anchor && Time.unscaledTime < _anchorCooldownUntil;
    }

    public bool CanSpeakNow()
    {
        return Time.unscaledTime >= _cooldownUntil;
    }

    public void MarkSocialDialogueStarted(Transform lookTarget = null)
    {
        _inSocialExchange = true;
        _lookTarget = lookTarget;
        Log("Social dialogue started.");
    }

    public void MarkSocialDialogueEnded()
    {
        _inSocialExchange = false;
        _lookTarget = null;
        _cooldownUntil = Time.unscaledTime + personalCooldownSeconds;
        Log($"Social dialogue ended. Cooldown={personalCooldownSeconds:0.0}s");
    }

    public void MarkSocialSequenceCompleted(NpcSocialAnchor anchor, NpcSocialGroupSO group)
    {
        _lastSocialAnchor = anchor;
        _lastSocialGroup = group;
        _lastSocialSequenceTime = Time.unscaledTime;

        float min = Mathf.Max(0f, Mathf.Min(socialRejoinCooldownSeconds.x, socialRejoinCooldownSeconds.y));
        float max = Mathf.Max(min, Mathf.Max(socialRejoinCooldownSeconds.x, socialRejoinCooldownSeconds.y));
        _anchorCooldownUntil = Time.unscaledTime + UnityEngine.Random.Range(min, max);

        if (TryGetComponent(out NpcSkierBrain brain))
            brain.NotifySocialConversationCompleted(anchor, group, UnityEngine.Random.value <= forceLeaveAfterConversationChance);
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return true;

        string normalized = tag.Trim();
        if (!string.IsNullOrWhiteSpace(socialRoleOverride) &&
            string.Equals(socialRoleOverride.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (identity != null && string.Equals(identity.Role.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (genericProfile != null && string.Equals(genericProfile.ArchetypeId, normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (socialTags != null)
        {
            for (int i = 0; i < socialTags.Length; i++)
            {
                if (string.Equals(socialTags[i]?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public void AddSocialTags(IEnumerable<string> tags)
    {
        if (tags == null)
            return;

        var merged = new List<string>();
        if (socialTags != null)
        {
            for (int i = 0; i < socialTags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(socialTags[i]) && !ContainsTag(merged, socialTags[i]))
                    merged.Add(socialTags[i].Trim());
            }
        }

        foreach (string tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !ContainsTag(merged, tag))
                merged.Add(tag.Trim());
        }

        socialTags = merged.ToArray();
    }

    [ContextMenu("Print Social Actor State")]
    private void PrintSocialActorState()
    {
        ResolveReferences();
        Debug.Log(
            $"[NpcSocialActor] {name}\n" +
            $"agent={(dialogueAgent != null)} bank={(dialogueAgent != null && dialogueAgent.DialogueBank != null)} identity={(identity != null ? identity.IdentityId : "none")} role={SocialRole}\n" +
            $"available={IsAvailableForSocialDialogue()} inExchange={_inSocialExchange} cooldownRemaining={Mathf.Max(0f, _cooldownUntil - Time.unscaledTime):0.0}s anchorCooldownRemaining={Mathf.Max(0f, _anchorCooldownUntil - Time.unscaledTime):0.0}s attached={(_liftRider != null && _liftRider.IsAttached)}\n" +
            $"lastAnchor={(_lastSocialAnchor != null ? _lastSocialAnchor.DisplayName : "none")} lastGroup={(_lastSocialGroup != null ? _lastSocialGroup.DisplayName : "none")} lastSequenceAt={_lastSocialSequenceTime:0.0}",
            this);
    }

    private string ResolveDefaultRole()
    {
        if (identity != null)
            return identity.Role.ToString();

        if (genericProfile != null && !string.IsNullOrWhiteSpace(genericProfile.ArchetypeId))
            return genericProfile.ArchetypeId;

        return "Skier";
    }

    private static bool ContainsTag(List<string> tags, string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag))
            return false;

        string normalized = tag.Trim();
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (dialogueAgent == null)
            dialogueAgent = GetComponent<NpcDialogueAgent>();
        if (identity == null)
            identity = GetComponent<NpcIdentity>();
        if (_talkInteractable == null)
            _talkInteractable = GetComponent<NpcTalkInteractable>();
        if (_questGiver == null)
            _questGiver = GetComponent<NpcQuestGiver>();
        if (_liftRider == null)
            _liftRider = GetComponent<LiftRider>();
        if (_skiController == null)
            _skiController = GetComponent<SkiController>();
        if (_raceRacer == null)
            _raceRacer = GetComponent<RaceCourseNpcRacer>();
    }

    private void Log(string message)
    {
        if (logDebug)
            Debug.Log($"[NpcSocialActor] {name}: {message}", this);
    }

    private void OnValidate()
    {
        ResolveReferences();
        if (dialogueAgent == null)
            Debug.LogWarning($"[{nameof(NpcSocialActor)}] {name} has no NpcDialogueAgent.", this);
    }
}
