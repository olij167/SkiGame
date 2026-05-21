using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.Debugging
{
    /// <summary>
    /// Runtime debug routing layer for centralised, channel-based debug logs, runtime states,
    /// and named debug signals. This intentionally coexists with existing inspector bools:
    /// pass those bools as fallbackEnabled so old behaviour keeps working while the Debug
    /// Control Center can enable the same channel centrally.
    ///
    /// Suggested path:
    /// Assets/Scripts/Debug/DebugRouter.cs
    /// </summary>
    public static class DebugRouter
    {
        public enum Level
        {
            Verbose,
            Info,
            Warning,
            Error
        }

        public sealed class ChannelSnapshot
        {
            public string channel;
            public bool enabled;
            public int sourceCount;
            public int logCount;
            public double lastLogRealtime;
            public string lastOwner;
            public string lastMessage;
            public Level lastLevel;
        }

        public sealed class SourceSnapshot
        {
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string channel;
            public bool enabled;
            public int logCount;
            public double lastLogRealtime;
            public string lastMessage;
            public Level lastLevel;
        }

        public sealed class StateSnapshot
        {
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string stateName;
            public bool value;
            public int changeCount;
            public double lastChangedRealtime;
            public string details;
        }

        public sealed class SignalSnapshot
        {
            public string signalName;
            public bool consoleEnabled;
            public int fireCount;
            public double lastFiredRealtime;
            public string lastOwner;
            public string lastDetails;
        }

        public sealed class LogRecord
        {
            public UnityEngine.Object owner;
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string channel;
            public string message;
            public Level level;
            public float time;
            public double realtime;
        }

        public sealed class SignalRecord
        {
            public UnityEngine.Object owner;
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string signalName;
            public string details;
            public float time;
            public double realtime;
        }

        private sealed class ChannelState
        {
            public string channel;
            public bool enabled;
            public readonly HashSet<int> sourceIds = new HashSet<int>();
            public int logCount;
            public double lastLogRealtime;
            public string lastOwner;
            public string lastMessage;
            public Level lastLevel;
        }

        private sealed class SourceState
        {
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string channel;
            public bool enabled;
            public int logCount;
            public double lastLogRealtime;
            public string lastMessage;
            public Level lastLevel;
        }

        private sealed class RuntimeState
        {
            public int ownerInstanceId;
            public string ownerName;
            public string ownerType;
            public string hierarchyPath;
            public string stateName;
            public bool value;
            public int changeCount;
            public double lastChangedRealtime;
            public string details;
        }

        private sealed class SignalState
        {
            public string signalName;
            public bool consoleEnabled;
            public int fireCount;
            public double lastFiredRealtime;
            public string lastOwner;
            public string lastDetails;
        }

        private static readonly Dictionary<string, ChannelState> Channels = new Dictionary<string, ChannelState>();
        private static readonly Dictionary<string, SourceState> Sources = new Dictionary<string, SourceState>();
        private static readonly Dictionary<string, RuntimeState> States = new Dictionary<string, RuntimeState>();
        private static readonly Dictionary<string, SignalState> Signals = new Dictionary<string, SignalState>();
        private static readonly Dictionary<string, float> NextAllowedLogTime = new Dictionary<string, float>();

        public static bool GlobalEnabled = true;
        public static bool PrefixMessages = true;
        public static bool SignalsEnabled = true;

        public static event Action<LogRecord> LogEmitted;
        public static event Action<SignalRecord> SignalRaised;

        public static bool IsEnabled(UnityEngine.Object owner, string channel, bool fallbackEnabled = false)
        {
            if (fallbackEnabled)
                return true;

            if (!GlobalEnabled)
                return false;

            channel = NormalizeKey(channel, "General");
            Register(owner, channel);

            ChannelState channelState;
            if (Channels.TryGetValue(channel, out channelState) && channelState.enabled)
                return true;

            SourceState sourceState;
            if (Sources.TryGetValue(GetSourceKey(owner, channel), out sourceState) && sourceState.enabled)
                return true;

            return false;
        }

        public static bool Log(UnityEngine.Object owner, string channel, string message, Level level = Level.Info, bool fallbackEnabled = false, float minIntervalSeconds = 0f)
        {
            return Log(owner, channel, () => message, level, fallbackEnabled, minIntervalSeconds);
        }

        public static bool Log(UnityEngine.Object owner, string channel, Func<string> messageFactory, Level level = Level.Info, bool fallbackEnabled = false, float minIntervalSeconds = 0f)
        {
            channel = NormalizeKey(channel, "General");
            Register(owner, channel);

            if (!IsEnabled(owner, channel, fallbackEnabled))
                return false;

            string sourceKey = GetSourceKey(owner, channel);
            if (!PassesRateLimit(sourceKey, minIntervalSeconds))
                return false;

            string message;
            try
            {
                message = messageFactory != null ? messageFactory() : string.Empty;
            }
            catch (Exception ex)
            {
                message = $"[DebugRouter] Message factory failed for channel '{channel}'.\n{ex}";
                level = Level.Error;
            }

            if (string.IsNullOrEmpty(message))
                message = "(empty debug message)";

            string ownerName = GetOwnerName(owner);
            string output = PrefixMessages
                ? $"[{channel}] {ownerName}: {message}"
                : message;

            switch (level)
            {
                case Level.Warning:
                    Debug.LogWarning(output, owner);
                    break;
                case Level.Error:
                    Debug.LogError(output, owner);
                    break;
                default:
                    Debug.Log(output, owner);
                    break;
            }

            double realtime = GetRealtime();
            float time = GetGameTime();

            ChannelState channelState = Channels[channel];
            channelState.logCount++;
            channelState.lastLogRealtime = realtime;
            channelState.lastOwner = ownerName;
            channelState.lastMessage = message;
            channelState.lastLevel = level;

            SourceState sourceState = Sources[sourceKey];
            sourceState.logCount++;
            sourceState.lastLogRealtime = realtime;
            sourceState.lastMessage = message;
            sourceState.lastLevel = level;

            Action<LogRecord> handler = LogEmitted;
            if (handler != null)
            {
                handler(new LogRecord
                {
                    owner = owner,
                    ownerInstanceId = GetOwnerId(owner),
                    ownerName = ownerName,
                    ownerType = GetOwnerType(owner),
                    hierarchyPath = GetHierarchyPath(owner),
                    channel = channel,
                    message = message,
                    level = level,
                    time = time,
                    realtime = realtime
                });
            }

            return true;
        }

        public static void Signal(UnityEngine.Object owner, string signalName, string details = null, bool fallbackConsoleLog = false)
        {
            if (!SignalsEnabled)
                return;

            signalName = NormalizeKey(signalName, "Signal");
            SignalState signal = GetOrCreateSignal(signalName);

            double realtime = GetRealtime();
            float time = GetGameTime();
            string ownerName = GetOwnerName(owner);

            signal.fireCount++;
            signal.lastFiredRealtime = realtime;
            signal.lastOwner = ownerName;
            signal.lastDetails = details;

            Action<SignalRecord> handler = SignalRaised;
            if (handler != null)
            {
                handler(new SignalRecord
                {
                    owner = owner,
                    ownerInstanceId = GetOwnerId(owner),
                    ownerName = ownerName,
                    ownerType = GetOwnerType(owner),
                    hierarchyPath = GetHierarchyPath(owner),
                    signalName = signalName,
                    details = details,
                    time = time,
                    realtime = realtime
                });
            }

            if (fallbackConsoleLog || signal.consoleEnabled)
            {
                Log(owner, "Signals/" + signalName, details ?? "Signal raised.", Level.Info, true);
            }
        }

        public static bool SetState(UnityEngine.Object owner, string stateName, bool value, string details = null, bool signalOnChange = true)
        {
            stateName = NormalizeKey(stateName, "State");
            int ownerId = GetOwnerId(owner);
            string key = ownerId + "|" + stateName;

            RuntimeState state;
            bool existed = States.TryGetValue(key, out state);
            bool changed = !existed || state.value != value;

            if (!existed)
            {
                state = new RuntimeState
                {
                    ownerInstanceId = ownerId,
                    ownerName = GetOwnerName(owner),
                    ownerType = GetOwnerType(owner),
                    hierarchyPath = GetHierarchyPath(owner),
                    stateName = stateName
                };
                States[key] = state;
            }

            state.ownerName = GetOwnerName(owner);
            state.ownerType = GetOwnerType(owner);
            state.hierarchyPath = GetHierarchyPath(owner);
            state.value = value;
            state.details = details;

            if (changed)
            {
                state.changeCount++;
                state.lastChangedRealtime = GetRealtime();

                if (signalOnChange)
                {
                    string suffix = value ? "Entered" : "Exited";
                    Signal(owner, "State/" + stateName + "/" + suffix, details, false);
                }
            }

            return changed;
        }

        public static bool GetState(UnityEngine.Object owner, string stateName, bool defaultValue = false)
        {
            stateName = NormalizeKey(stateName, "State");
            RuntimeState state;
            if (States.TryGetValue(GetOwnerId(owner) + "|" + stateName, out state))
                return state.value;

            return defaultValue;
        }

        public static void Register(UnityEngine.Object owner, string channel)
        {
            channel = NormalizeKey(channel, "General");
            ChannelState channelState = GetOrCreateChannel(channel);

            int ownerId = GetOwnerId(owner);
            if (ownerId == 0)
                return;

            channelState.sourceIds.Add(ownerId);

            string sourceKey = GetSourceKey(owner, channel);
            SourceState sourceState;
            if (!Sources.TryGetValue(sourceKey, out sourceState))
            {
                sourceState = new SourceState
                {
                    ownerInstanceId = ownerId,
                    channel = channel
                };
                Sources[sourceKey] = sourceState;
            }

            sourceState.ownerName = GetOwnerName(owner);
            sourceState.ownerType = GetOwnerType(owner);
            sourceState.hierarchyPath = GetHierarchyPath(owner);
        }

        public static void RegisterSignal(string signalName)
        {
            GetOrCreateSignal(signalName);
        }

        public static void SetChannelEnabled(string channel, bool enabled)
        {
            GetOrCreateChannel(channel).enabled = enabled;
        }

        public static void SetSourceEnabled(UnityEngine.Object owner, string channel, bool enabled)
        {
            channel = NormalizeKey(channel, "General");
            Register(owner, channel);
            SourceState source;
            if (Sources.TryGetValue(GetSourceKey(owner, channel), out source))
                source.enabled = enabled;
        }

        public static void SetSignalConsoleEnabled(string signalName, bool enabled)
        {
            GetOrCreateSignal(signalName).consoleEnabled = enabled;
        }

        public static List<ChannelSnapshot> GetChannels()
        {
            List<ChannelSnapshot> result = new List<ChannelSnapshot>(Channels.Count);
            foreach (ChannelState state in Channels.Values)
            {
                result.Add(new ChannelSnapshot
                {
                    channel = state.channel,
                    enabled = state.enabled,
                    sourceCount = state.sourceIds.Count,
                    logCount = state.logCount,
                    lastLogRealtime = state.lastLogRealtime,
                    lastOwner = state.lastOwner,
                    lastMessage = state.lastMessage,
                    lastLevel = state.lastLevel
                });
            }

            result.Sort((a, b) => string.Compare(a.channel, b.channel, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static List<SourceSnapshot> GetSources()
        {
            List<SourceSnapshot> result = new List<SourceSnapshot>(Sources.Count);
            foreach (SourceState state in Sources.Values)
            {
                result.Add(new SourceSnapshot
                {
                    ownerInstanceId = state.ownerInstanceId,
                    ownerName = state.ownerName,
                    ownerType = state.ownerType,
                    hierarchyPath = state.hierarchyPath,
                    channel = state.channel,
                    enabled = state.enabled,
                    logCount = state.logCount,
                    lastLogRealtime = state.lastLogRealtime,
                    lastMessage = state.lastMessage,
                    lastLevel = state.lastLevel
                });
            }

            result.Sort((a, b) =>
            {
                int c = string.Compare(a.channel, b.channel, StringComparison.OrdinalIgnoreCase);
                if (c != 0)
                    return c;
                return string.Compare(a.hierarchyPath, b.hierarchyPath, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        public static List<StateSnapshot> GetStates()
        {
            List<StateSnapshot> result = new List<StateSnapshot>(States.Count);
            foreach (RuntimeState state in States.Values)
            {
                result.Add(new StateSnapshot
                {
                    ownerInstanceId = state.ownerInstanceId,
                    ownerName = state.ownerName,
                    ownerType = state.ownerType,
                    hierarchyPath = state.hierarchyPath,
                    stateName = state.stateName,
                    value = state.value,
                    changeCount = state.changeCount,
                    lastChangedRealtime = state.lastChangedRealtime,
                    details = state.details
                });
            }

            result.Sort((a, b) =>
            {
                int c = string.Compare(a.stateName, b.stateName, StringComparison.OrdinalIgnoreCase);
                if (c != 0)
                    return c;
                return string.Compare(a.hierarchyPath, b.hierarchyPath, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        public static List<SignalSnapshot> GetSignals()
        {
            List<SignalSnapshot> result = new List<SignalSnapshot>(Signals.Count);
            foreach (SignalState state in Signals.Values)
            {
                result.Add(new SignalSnapshot
                {
                    signalName = state.signalName,
                    consoleEnabled = state.consoleEnabled,
                    fireCount = state.fireCount,
                    lastFiredRealtime = state.lastFiredRealtime,
                    lastOwner = state.lastOwner,
                    lastDetails = state.lastDetails
                });
            }

            result.Sort((a, b) => string.Compare(a.signalName, b.signalName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static void ClearRuntimeData()
        {
            Channels.Clear();
            Sources.Clear();
            States.Clear();
            Signals.Clear();
            NextAllowedLogTime.Clear();
        }

        private static ChannelState GetOrCreateChannel(string channel)
        {
            channel = NormalizeKey(channel, "General");
            ChannelState state;
            if (!Channels.TryGetValue(channel, out state))
            {
                state = new ChannelState { channel = channel };
                Channels[channel] = state;
            }

            return state;
        }

        private static SignalState GetOrCreateSignal(string signalName)
        {
            signalName = NormalizeKey(signalName, "Signal");
            SignalState state;
            if (!Signals.TryGetValue(signalName, out state))
            {
                state = new SignalState { signalName = signalName };
                Signals[signalName] = state;
            }

            return state;
        }

        private static bool PassesRateLimit(string key, float minIntervalSeconds)
        {
            if (minIntervalSeconds <= 0f)
                return true;

            float now = GetGameTime();
            float next;
            if (NextAllowedLogTime.TryGetValue(key, out next) && now < next)
                return false;

            NextAllowedLogTime[key] = now + minIntervalSeconds;
            return true;
        }

        private static string GetSourceKey(UnityEngine.Object owner, string channel)
        {
            return GetOwnerId(owner) + "|" + NormalizeKey(channel, "General");
        }

        private static int GetOwnerId(UnityEngine.Object owner)
        {
            return owner != null ? owner.GetInstanceID() : 0;
        }

        private static string GetOwnerName(UnityEngine.Object owner)
        {
            return owner != null ? owner.name : "Global";
        }

        private static string GetOwnerType(UnityEngine.Object owner)
        {
            return owner != null ? owner.GetType().Name : "Global";
        }

        private static string GetHierarchyPath(UnityEngine.Object owner)
        {
            Component component = owner as Component;
            GameObject gameObject = owner as GameObject;

            Transform transform = component != null ? component.transform : (gameObject != null ? gameObject.transform : null);
            if (transform == null)
                return GetOwnerName(owner);

            Stack<string> names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string NormalizeKey(string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            key = key.Trim().Replace('\\', '/');
            while (key.Contains("//"))
                key = key.Replace("//", "/");

            return key;
        }

        private static float GetGameTime()
        {
            return Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        }

        private static double GetRealtime()
        {
            return (double)Time.realtimeSinceStartup;
        }
    }

}