#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SkiGame.Map;
using SkiGame.POI;

[CustomEditor(typeof(MapData))]
public sealed class MapDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Authoring Helpers", EditorStyles.boldLabel);

        if (GUILayout.Button("Sync Marker Colours From PointOfInterestRegistry (open scene)"))
        {
            var reg = Object.FindObjectOfType<PointOfInterestRegistry>();
            if (reg == null)
            {
                EditorUtility.DisplayDialog("No Registry Found",
                    "No PointOfInterestRegistry exists in the currently open scene.", "OK");
                return;
            }

            reg.Refresh();

            var so = serializedObject;
            var markersProp = so.FindProperty("markers");
            if (markersProp == null || !markersProp.isArray)
                return;

            Undo.RecordObject(target, "Sync Marker Colours");

            int changed = 0;

            for (int i = 0; i < markersProp.arraySize; i++)
            {
                var m = markersProp.GetArrayElementAtIndex(i);
                var idProp = m.FindPropertyRelative("id");
                var colProp = m.FindPropertyRelative("color");

                string id = idProp != null ? idProp.stringValue : null;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (reg.TryGetById(id, out var info))
                {
                    if (colProp != null && colProp.colorValue != info.color)
                    {
                        colProp.colorValue = info.color;
                        changed++;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Debug.Log($"[MapDataEditor] Synced {changed} marker colours from POI registry.");
        }

        if (GUILayout.Button("Sync Race Marker + Polyline Colours From Open Scene"))
        {
            SyncRaceColoursFromScene((MapData)target, serializedObject);
        }

        if (GUILayout.Button("Append / Refresh Race Course Polylines From Open Scene"))
        {
            AppendOrRefreshRacePolylinesFromScene((MapData)target, serializedObject);
        }
    }

    private static void SyncRaceColoursFromScene(MapData mapData, SerializedObject so)
    {
        var reg = Object.FindObjectOfType<PointOfInterestRegistry>();
        if (reg != null)
            reg.Refresh();

        var races = Object.FindObjectsOfType<RaceCourseLine>(true);
        if (races == null || races.Length == 0)
        {
            EditorUtility.DisplayDialog("No Races Found",
                "No RaceCourseLine objects were found in the currently open scene.", "OK");
            return;
        }

        var markersProp = so.FindProperty("markers");
        var polylinesProp = so.FindProperty("polylines");
        if (markersProp == null || !markersProp.isArray || polylinesProp == null || !polylinesProp.isArray)
            return;

        Undo.RecordObject(mapData, "Sync Race Marker + Polyline Colours");

        int markerChanges = 0;
        int polylineChanges = 0;

        for (int i = 0; i < races.Length; i++)
        {
            var race = races[i];
            if (race == null)
                continue;

            Color fallback = new Color(1.00f, 0.55f, 0.20f, 1f);
            Color resolvedColor = race.GetResolvedMapLineColor(fallback);

            string raceId = !string.IsNullOrWhiteSpace(race.RaceId)
                ? race.RaceId
                : race.gameObject.name;

            string markerId = !string.IsNullOrWhiteSpace(race.RaceId)
                ? $"activity-race:{race.RaceId}"
                : null;

            for (int m = 0; m < markersProp.arraySize; m++)
            {
                var markerProp = markersProp.GetArrayElementAtIndex(m);
                var idProp = markerProp.FindPropertyRelative("id");
                var colorProp = markerProp.FindPropertyRelative("color");
                if (idProp == null || colorProp == null)
                    continue;

                string existingId = idProp.stringValue;
                bool idMatch =
                    (!string.IsNullOrWhiteSpace(markerId) && string.Equals(existingId, markerId, System.StringComparison.Ordinal)) ||
                    (!string.IsNullOrWhiteSpace(raceId) && string.Equals(existingId, raceId, System.StringComparison.Ordinal));

                if (!idMatch)
                    continue;

                if (colorProp.colorValue != resolvedColor)
                {
                    colorProp.colorValue = resolvedColor;
                    markerChanges++;
                }
            }

            for (int p = 0; p < polylinesProp.arraySize; p++)
            {
                var polyProp = polylinesProp.GetArrayElementAtIndex(p);
                var idProp = polyProp.FindPropertyRelative("id");
                var typeProp = polyProp.FindPropertyRelative("lineType");
                var colorProp = polyProp.FindPropertyRelative("color");
                if (idProp == null || typeProp == null || colorProp == null)
                    continue;

                if (typeProp.enumValueIndex != (int)MapLineType.RaceCourse)
                    continue;

                string existingId = idProp.stringValue;
                if (!string.Equals(existingId, raceId, System.StringComparison.Ordinal) &&
                    !string.Equals(existingId, race.RaceName, System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(existingId, race.gameObject.name, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (colorProp.colorValue != resolvedColor)
                {
                    colorProp.colorValue = resolvedColor;
                    polylineChanges++;
                }
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mapData);

        Debug.Log($"[MapDataEditor] Synced race colours. Markers={markerChanges}, Polylines={polylineChanges}");
    }

    private static void AppendOrRefreshRacePolylinesFromScene(MapData mapData, SerializedObject so)
    {
        var polylinesProp = so.FindProperty("polylines");
        if (polylinesProp == null || !polylinesProp.isArray)
            return;

        var races = Object.FindObjectsOfType<RaceCourseLine>(true);
        Undo.RecordObject(mapData, "Refresh Race Course Polylines");

        for (int i = polylinesProp.arraySize - 1; i >= 0; i--)
        {
            var elem = polylinesProp.GetArrayElementAtIndex(i);
            var typeProp = elem.FindPropertyRelative("lineType");
            if (typeProp != null && typeProp.enumValueIndex == (int)MapLineType.RaceCourse)
                polylinesProp.DeleteArrayElementAtIndex(i);
        }

        for (int i = 0; i < races.Length; i++)
        {
            var race = races[i];
            if (race == null || race.PointsWorld == null || race.PointsWorld.Count < 2)
                continue;

            int index = polylinesProp.arraySize;
            polylinesProp.InsertArrayElementAtIndex(index);

            var elem = polylinesProp.GetArrayElementAtIndex(index);

            string raceId = !string.IsNullOrWhiteSpace(race.RaceId) ? race.RaceId : race.gameObject.name;
            string raceName = string.IsNullOrWhiteSpace(race.RaceName) ? race.gameObject.name : race.RaceName;

            Color fallback = new Color(1.00f, 0.55f, 0.20f, 1f);
            Color resolvedColor = race.GetResolvedMapLineColor(fallback);

            elem.FindPropertyRelative("displayName").stringValue = raceName;
            elem.FindPropertyRelative("id").stringValue = raceId;
            elem.FindPropertyRelative("lineType").enumValueIndex = (int)MapLineType.RaceCourse;
            elem.FindPropertyRelative("color").colorValue = resolvedColor;
            elem.FindPropertyRelative("widthMeters").floatValue = Mathf.Max(6f, race.CourseWidthMeters * 0.35f);
            elem.FindPropertyRelative("difficultyRank").intValue = 0;
            elem.FindPropertyRelative("difficultyLabel").stringValue = "Race";

            var pointsWorldProp = elem.FindPropertyRelative("pointsWorld");
            var pointsWorldXZProp = elem.FindPropertyRelative("pointsWorldXZ");

            pointsWorldProp.arraySize = race.PointsWorld.Count;
            pointsWorldXZProp.arraySize = race.PointsWorld.Count;

            for (int p = 0; p < race.PointsWorld.Count; p++)
            {
                Vector3 world = race.PointsWorld[p];
                pointsWorldProp.GetArrayElementAtIndex(p).vector3Value = world;
                pointsWorldXZProp.GetArrayElementAtIndex(p).vector2Value = new Vector2(world.x, world.z);
            }
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(mapData);
        Debug.Log($"[MapDataEditor] Refreshed race course polylines from scene. Count={races.Length}");
    }
}
#endif