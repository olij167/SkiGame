using PungentFunk.Utilities.Editor.Developer;

namespace PungentFunk.Utilities.Editor.Core
{
#if UNITY_EDITOR
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;

    public enum PungentUtilityPackageSimulationState
    {
        Actual,
        Installed,
        OwnedNotInstalled,
        AvailableNotOwned,
        MissingRequiredPackage,
        Disabled,
        ComingSoonUnavailable
    }

    [Serializable]
    public sealed class PungentUtilityPackageSimulationEntry
    {
        public string packageId;
        public PungentUtilityPackageSimulationState state;
    }

    [FilePath("ProjectSettings/PungentFunkUtilities/PackageSimulation.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PungentUtilityPackageSimulationSettings : ScriptableSingleton<PungentUtilityPackageSimulationSettings>
    {
        public List<PungentUtilityPackageSimulationEntry> entries = new List<PungentUtilityPackageSimulationEntry>();

        public void Set(string packageId, PungentUtilityPackageSimulationState state)
        {
            packageId = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            NormalizeInMemory();
            entries.RemoveAll(entry => string.Equals(entry.packageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (state != PungentUtilityPackageSimulationState.Actual)
            {
                entries.Add(new PungentUtilityPackageSimulationEntry
                {
                    packageId = packageId,
                    state = state
                });
            }

            SaveStore();
        }

        public void Clear(string packageId)
        {
            packageId = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            NormalizeInMemory();
            entries.RemoveAll(entry => string.Equals(entry.packageId, packageId, StringComparison.OrdinalIgnoreCase));
            SaveStore();
        }

        public void ClearAll()
        {
            entries = new List<PungentUtilityPackageSimulationEntry>();
            SaveStore();
        }

        public PungentUtilityPackageSimulationState Get(string packageId)
        {
            packageId = PungentUtilityPackageCatalog.NormalizePackageId(packageId);
            NormalizeInMemory();
            PungentUtilityPackageSimulationEntry entry = entries.FirstOrDefault(item => string.Equals(item.packageId, packageId, StringComparison.OrdinalIgnoreCase));
            return entry == null ? PungentUtilityPackageSimulationState.Actual : entry.state;
        }

        public void SaveStore()
        {
            NormalizeInMemory();
            entries.Sort((a, b) => string.Compare(a.packageId, b.packageId, StringComparison.OrdinalIgnoreCase));
            Save(true);
            PungentUtilityPackageSimulation.NotifyChanged();
        }

        private void NormalizeInMemory()
        {
            if (entries == null)
                entries = new List<PungentUtilityPackageSimulationEntry>();

            entries.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.packageId) || entry.state == PungentUtilityPackageSimulationState.Actual);
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].packageId = PungentUtilityPackageCatalog.NormalizePackageId(entries[i].packageId);
                if (!Enum.IsDefined(typeof(PungentUtilityPackageSimulationState), entries[i].state))
                    entries[i].state = PungentUtilityPackageSimulationState.Actual;
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                string id = entries[i].packageId;
                int first = entries.FindIndex(item => string.Equals(item.packageId, id, StringComparison.OrdinalIgnoreCase));
                if (first >= 0 && first != i)
                    entries.RemoveAt(i);
            }
        }
    }

    public static class PungentUtilityPackageSimulation
    {
        public static event Action Changed;

        public static bool Active => PungentDeveloperMode.Enabled && PungentUtilityPackageSimulationSettings.instance.entries != null && PungentUtilityPackageSimulationSettings.instance.entries.Count > 0;

        public static PungentUtilityPackageSimulationState GetState(string packageId)
        {
            if (!PungentDeveloperMode.Enabled)
                return PungentUtilityPackageSimulationState.Actual;

            return PungentUtilityPackageSimulationSettings.instance.Get(packageId);
        }

        public static void SetState(string packageId, PungentUtilityPackageSimulationState state)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            Undo.RecordObject(PungentUtilityPackageSimulationSettings.instance, "Set Package Simulation");
            PungentUtilityPackageSimulationSettings.instance.Set(packageId, state);
        }

        public static void Clear(string packageId)
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            Undo.RecordObject(PungentUtilityPackageSimulationSettings.instance, "Clear Package Simulation");
            PungentUtilityPackageSimulationSettings.instance.Clear(packageId);
        }

        public static void ClearAll()
        {
            if (!PungentDeveloperMode.CanEditProjectMetadata)
                return;

            Undo.RecordObject(PungentUtilityPackageSimulationSettings.instance, "Clear Package Simulation");
            PungentUtilityPackageSimulationSettings.instance.ClearAll();
        }

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
#endif
}
