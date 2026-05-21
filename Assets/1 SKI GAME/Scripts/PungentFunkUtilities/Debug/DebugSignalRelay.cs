using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PungentFunk.Utilities.Debugging
{
    /// <summary>
    /// Optional bridge from DebugRouter named signals into inspector-configurable UnityEvents.
    /// Use this when a debug signal should trigger a temporary visual, capture tool, pause toggle,
    /// marker spawn, or other editor/test-only response without hard-wiring that logic into gameplay code.
    ///
    /// Suggested path:
    /// Assets/Scripts/Debug/DebugSignalRelay.cs
    /// </summary>
    public sealed class DebugSignalRelay : MonoBehaviour
    {
        [Serializable]
        public sealed class StringEvent : UnityEvent<string> { }

        [Serializable]
        public sealed class SignalRule
        {
            [Tooltip("Named router signal to listen for, e.g. Gameplay/Event or State/SomeState/Entered.")]
            public string signalName;

            [Tooltip("If enabled, this rule only responds when the signal owner matches Required Owner.")]
            public bool requireOwner;

            [Tooltip("Optional owner filter. Usually a component instance or GameObject.")]
            public UnityEngine.Object requiredOwner;

            [Tooltip("Invoked when the signal matches.")]
            public UnityEvent onSignal;

            [Tooltip("Invoked with the signal details string when the signal matches.")]
            public StringEvent onSignalDetails;
        }

        [SerializeField] private bool listenWhileInactive = false;
        [SerializeField] private bool logRelayMatches = false;
        [SerializeField] private List<SignalRule> rules = new List<SignalRule>();

        private void OnEnable()
        {
            DebugRouter.SignalRaised += HandleSignalRaised;
        }

        private void OnDisable()
        {
            DebugRouter.SignalRaised -= HandleSignalRaised;
        }

        private void HandleSignalRaised(DebugRouter.SignalRecord record)
        {
            if (!listenWhileInactive && !isActiveAndEnabled)
                return;

            if (record == null)
                return;

            for (int i = 0; i < rules.Count; i++)
            {
                SignalRule rule = rules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.signalName))
                    continue;

                if (!string.Equals(rule.signalName.Trim(), record.signalName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rule.requireOwner && rule.requiredOwner != record.owner)
                    continue;

                if (logRelayMatches)
                {
                    Debug.Log(
                        $"[{nameof(DebugSignalRelay)}] Matched signal '{record.signalName}' from '{record.ownerName}'. Details={record.details}",
                        this);
                }

                if (rule.onSignal != null)
                    rule.onSignal.Invoke();

                if (rule.onSignalDetails != null)
                    rule.onSignalDetails.Invoke(record.details ?? string.Empty);
            }
        }
    }

}