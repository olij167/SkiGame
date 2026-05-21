using PungentFunk.Utilities.Editor.Core;

namespace PungentFunk.Utilities.Editor.EnvironmentSimulation
{
#if UNITY_EDITOR
    using UnityEditor;

    public static class PungentEnvironmentSimulationMenus
    {
        [MenuItem(PungentUtilityMenuPaths.EnvironmentSimulationWorkbench)]
        public static void OpenWorkbench() => PungentEnvironmentSimulationWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.CalendarClock)]
        public static void OpenCalendarClock() => PungentCalendarClockWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.Weather)]
        public static void OpenWeather() => PungentWeatherUtilityWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.DeltaTime)]
        public static void OpenDeltaTime() => PungentDeltaTimeUtilityWindow.Open();

        [MenuItem(PungentUtilityMenuPaths.EnvironmentSimulationLayoutQA)]
        public static void OpenLayoutQA() => PungentEnvironmentSimulationLayoutQAWindow.Open();
    }
#endif
}
