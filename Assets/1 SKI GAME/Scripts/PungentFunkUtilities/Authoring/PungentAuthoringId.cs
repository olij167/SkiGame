using System;

namespace PungentFunk.Utilities.Authoring
{
    [Serializable]
    public struct PungentAuthoringId : IEquatable<PungentAuthoringId>
    {
        public string value;

        public PungentAuthoringId(string value)
        {
            this.value = Normalize(value);
        }

        public bool IsValid => IsValidId(value);

        public static PungentAuthoringId NewId()
        {
            return new PungentAuthoringId(NewValue());
        }

        public static string NewValue()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string Normalize(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        public static bool IsValidId(string id)
        {
            return !string.IsNullOrWhiteSpace(Normalize(id));
        }

        public static bool EqualsId(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryNormalize(string id, out string normalized)
        {
            normalized = Normalize(id);
            return IsValidId(normalized);
        }

        public override string ToString()
        {
            return Normalize(value);
        }

        public bool Equals(PungentAuthoringId other)
        {
            return EqualsId(value, other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is PungentAuthoringId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Normalize(value));
        }
    }
}
