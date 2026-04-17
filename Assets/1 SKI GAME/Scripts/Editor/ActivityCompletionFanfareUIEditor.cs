using UnityEditor;
using UnityEngine;
using SkiGame.UI;

[CustomEditor(typeof(ActivityCompletionFanfareUI))]
public sealed class ActivityCompletionFanfareUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ActivityCompletionFanfareUI ui = (ActivityCompletionFanfareUI)target;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Debug Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Use these buttons during Play Mode to preview fanfare states and tune typography, duration, and animation."
                : "Enter Play Mode to trigger debug fanfare previews.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Show Configured Debug Payload"))
                ui.DebugShowConfiguredPayload();

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Show Race Victory"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.RaceVictory);

            if (GUILayout.Button("Show Race Finish"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.RaceFinish);

            if (GUILayout.Button("Show Race Failure"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.RaceFailure);

            if (GUILayout.Button("Show Rescue Success"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.RescueSuccess);

            if (GUILayout.Button("Show Rescue Failure"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.RescueFailure);

            if (GUILayout.Button("Show Cancelled"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.Cancelled);

            if (GUILayout.Button("Show Quest Stage Advanced"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.QuestStageAdvanced);

            if (GUILayout.Button("Show Quest Completed"))
                ui.DebugShowState(ActivityCompletionFanfareUI.FanfareStateId.QuestCompleted);

            if (GUILayout.Button("Clear Queue / Hide"))
                ui.DebugClearQueueAndHide();
        }
    }
}