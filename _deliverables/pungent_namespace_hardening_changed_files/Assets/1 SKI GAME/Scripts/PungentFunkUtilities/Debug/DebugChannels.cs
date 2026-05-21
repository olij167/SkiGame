namespace PungentFunk.Utilities.Debugging
{
    /// <summary>
    /// Optional shared channel/signal names for DebugRouter.
    ///
    /// Keep this file project-agnostic: add project-specific channel constants in a separate
    /// file if a particular game/tool needs domain-specific names.
    ///
    /// Suggested path:
    /// Assets/Scripts/Debug/DebugChannels.cs
    /// </summary>
    public static class DebugChannels
    {
        public const string General = "General";

        public const string Runtime = "Runtime";
        public const string Editor = "Editor";
        public const string Input = "Input";
        public const string UI = "UI";
        public const string Audio = "Audio";
        public const string Animation = "Animation";

        public const string Physics = "Physics";
        public const string PhysicsContacts = "Physics/Contacts";
        public const string PhysicsGrounding = "Physics/Grounding";
        public const string PhysicsMovement = "Physics/Movement";

        public const string Gameplay = "Gameplay";
        public const string GameplayState = "Gameplay/State";
        public const string GameplayEvents = "Gameplay/Events";
        public const string GameplayInteractions = "Gameplay/Interactions";

        public const string AI = "AI";
        public const string AIState = "AI/State";
        public const string AIDecision = "AI/Decision";
        public const string AINavigation = "AI/Navigation";

        public const string Networking = "Networking";
        public const string SaveLoad = "SaveLoad";
        public const string Diagnostics = "Diagnostics";
    }

    public static class DebugSignals
    {
        public const string RuntimeStarted = "Runtime/Started";
        public const string RuntimeStopped = "Runtime/Stopped";

        public const string StateEntered = "State/Entered";
        public const string StateExited = "State/Exited";
        public const string StateChanged = "State/Changed";

        public const string InputReceived = "Input/Received";
        public const string InteractionStarted = "Gameplay/Interaction/Started";
        public const string InteractionCompleted = "Gameplay/Interaction/Completed";
        public const string InteractionFailed = "Gameplay/Interaction/Failed";

        public const string PhysicsContactStarted = "Physics/Contact/Started";
        public const string PhysicsContactEnded = "Physics/Contact/Ended";
        public const string GroundedEntered = "Physics/Grounding/Entered";
        public const string GroundedExited = "Physics/Grounding/Exited";

        public const string AIStateChanged = "AI/State/Changed";
        public const string NavigationFailed = "AI/Navigation/Failed";

        public const string UIOpened = "UI/Opened";
        public const string UIClosed = "UI/Closed";
    }

}