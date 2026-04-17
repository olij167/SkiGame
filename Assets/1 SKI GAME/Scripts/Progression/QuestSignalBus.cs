using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SkiGame.Progression
{
    [Serializable]
    public sealed class QuestSignalData
    {
        public float numericValue;
        public bool hasNumericValue;
        public bool boolValue;
        public bool hasBoolValue;
        public string stringValue;
        public List<QuestConditionFilter> tags = new List<QuestConditionFilter>();

        public string GetTag(string key)
        {
            if (tags == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            for (int i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (tag == null || string.IsNullOrWhiteSpace(tag.key))
                    continue;

                if (string.Equals(tag.key.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase))
                    return tag.value ?? string.Empty;
            }

            return string.Empty;
        }

        public static QuestSignalData Create() => new QuestSignalData();

        public QuestSignalData WithTag(string key, string value)
        {
            tags ??= new List<QuestConditionFilter>();
            tags.Add(new QuestConditionFilter { key = key, value = value });
            return this;
        }

        public QuestSignalData WithNumeric(float value)
        {
            numericValue = value;
            hasNumericValue = true;
            return this;
        }

        public QuestSignalData WithBool(bool value)
        {
            boolValue = value;
            hasBoolValue = true;
            return this;
        }

        public QuestSignalData WithText(string value)
        {
            stringValue = value;
            return this;
        }
    }

    public readonly struct QuestSignalRecord
    {
        public readonly int Sequence;
        public readonly string Key;
        public readonly QuestSignalData Data;

        public QuestSignalRecord(int sequence, string key, QuestSignalData data)
        {
            Sequence = sequence;
            Key = key;
            Data = data;
        }
    }

    [DisallowMultipleComponent]
    public sealed class QuestSignalBus : MonoBehaviour
    {
        public event Action<QuestSignalRecord> OnEventRaised;

        private readonly List<QuestSignalRecord> _eventHistory = new List<QuestSignalRecord>();
        private readonly Dictionary<string, float> _stats = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private int _nextSequence = 1;

        public int CurrentSequence => _nextSequence - 1;

        public void RaiseEvent(string key, QuestSignalData data = null)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return;

            var record = new QuestSignalRecord(_nextSequence++, normalizedKey, data ?? QuestSignalData.Create());
            _eventHistory.Add(record);
            OnEventRaised?.Invoke(record);
        }

        public void SetStat(string key, float value)
        {
            string normalizedKey = NormalizeKey(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _stats[normalizedKey] = value;
        }

        public void AddStat(string key, float delta)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return;

            _stats.TryGetValue(normalizedKey, out float current);
            _stats[normalizedKey] = current + delta;
        }

        public void SetState(string key, bool value)
        {
            string normalizedKey = NormalizeKey(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _states[normalizedKey] = value;
        }

        public void SetText(string key, string value)
        {
            string normalizedKey = NormalizeKey(key);
            if (!string.IsNullOrEmpty(normalizedKey))
                _strings[normalizedKey] = value ?? string.Empty;
        }

        public float ReadStat(string key)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return 0f;

            return _stats.TryGetValue(normalizedKey, out float value) ? value : 0f;
        }

        public bool ReadState(string key)
        {
            string normalizedKey = NormalizeKey(key);
            return !string.IsNullOrEmpty(normalizedKey) && _states.TryGetValue(normalizedKey, out bool value) && value;
        }

        public string ReadText(string key)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return string.Empty;

            return _strings.TryGetValue(normalizedKey, out string value) ? value ?? string.Empty : string.Empty;
        }

        public int CountEvents(string key, int afterSequence, IList<QuestConditionFilter> filters, Func<QuestSignalData, bool> predicate = null)
        {
            string normalizedKey = NormalizeKey(key);
            if (string.IsNullOrEmpty(normalizedKey))
                return 0;

            int count = 0;
            for (int i = 0; i < _eventHistory.Count; i++)
            {
                var record = _eventHistory[i];
                if (record.Sequence <= afterSequence)
                    continue;

                if (!string.Equals(record.Key, normalizedKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!MatchesFilters(record.Data, filters))
                    continue;

                if (predicate != null && !predicate(record.Data))
                    continue;

                count++;
            }

            return count;
        }

        public static string NormalizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
        }

        public static string FormatValue(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static bool MatchesFilters(QuestSignalData data, IList<QuestConditionFilter> filters)
        {
            if (filters == null || filters.Count == 0)
                return true;

            if (data == null)
                return false;

            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                if (filter == null || string.IsNullOrWhiteSpace(filter.key))
                    continue;

                string actual = data.GetTag(filter.key);
                string expected = filter.value ?? string.Empty;
                if (!string.Equals(actual ?? string.Empty, expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }
}
