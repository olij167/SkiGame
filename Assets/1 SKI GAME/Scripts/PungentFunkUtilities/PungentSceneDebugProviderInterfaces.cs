using System;
using System.Collections.Generic;
using UnityEngine;

namespace PungentFunk.Utilities.SceneTools
{
    public interface IPungentSceneBeaconProvider
    {
        bool TryGetBeaconSnapshot(out PungentSceneBeaconSnapshot snapshot);
    }

    public interface IPungentCollisionSensorProvider
    {
        bool TryGetCollisionSensorSnapshot(out PungentCollisionSensorSnapshot snapshot);
    }

    public interface IPungentTriggerSensorProvider
    {
        bool TryGetTriggerSensorSnapshot(out PungentTriggerSensorSnapshot snapshot);
    }

    public interface IPungentTrajectoryProvider
    {
        bool TryGetTrajectorySnapshot(IList<PungentTrajectorySample> samples, out PungentTrajectorySnapshot snapshot);
    }

    public interface IPungentSceneGizmoPerformanceProvider
    {
        bool TryGetPerformanceEstimate(out PungentSceneGizmoPerformanceEstimate estimate);
    }

    [Serializable]
    public struct PungentSceneBeaconSnapshot
    {
        public Component owner;
        public bool drawInScene;
        public bool drawWhenSelectedOnly;
        public bool alwaysVisible;
        public bool pingable;
        public bool drawLabel;
        public bool drawRing;
        public bool drawVerticalLine;
        public bool drawDistanceToSceneCamera;
        public Vector3 worldPosition;
        public string label;
        public Color color;
        public float radius;
        public float verticalLineHeight;
        public float maxDrawDistance;
        public int priority;
        public double lastPingTime;
        public float pingDuration;
    }

    [Serializable]
    public struct PungentCollisionContactSnapshot
    {
        public Vector3 point;
        public Vector3 normal;
        public Vector3 relativeVelocity;
        public string otherObjectName;
        public GameObject otherObject;
        public float time;
        public bool recent;
    }

    [Serializable]
    public struct PungentCollisionSensorSnapshot
    {
        public Component owner;
        public Collider observedCollider;
        public bool drawInScene;
        public bool drawWhenSelectedOnly;
        public bool drawColliderBounds;
        public bool drawContactPoints;
        public bool drawContactNormals;
        public bool drawContactLabels;
        public bool hasCollider;
        public Bounds colliderBounds;
        public float maxDrawDistance;
        public int currentCollisionCount;
        public int contactCount;
        public int maxContacts;
        public float contactPointSize;
        public float normalLength;
        public Color idleColor;
        public Color activeColor;
        public Color contactColor;
        public Color normalColor;
        public string lastCollisionObjectName;
        public string lastEventType;
        public string warning;
        public PungentCollisionContactSnapshot[] contacts;
    }

    [Serializable]
    public struct PungentTriggerOverlapSnapshot
    {
        public Vector3 position;
        public Bounds bounds;
        public string objectName;
        public GameObject gameObject;
        public float time;
        public bool recentExit;
    }

    [Serializable]
    public struct PungentTriggerSensorSnapshot
    {
        public Component owner;
        public Collider observedTrigger;
        public bool drawInScene;
        public bool drawWhenSelectedOnly;
        public bool drawTriggerBounds;
        public bool drawOverlapLinks;
        public bool drawOverlapLabels;
        public bool drawRecentExits;
        public bool hasCollider;
        public bool colliderIsTrigger;
        public Bounds triggerBounds;
        public float maxDrawDistance;
        public int overlapCount;
        public int recentExitCount;
        public int maxOverlaps;
        public Color idleColor;
        public Color activeColor;
        public Color overlapColor;
        public Color recentExitColor;
        public string warning;
        public PungentTriggerOverlapSnapshot[] overlaps;
        public PungentTriggerOverlapSnapshot[] recentExits;
    }

    [Serializable]
    public struct PungentTrajectorySample
    {
        public Vector3 position;
        public Vector3 velocity;
        public float time;
        public bool hit;

        public PungentTrajectorySample(Vector3 position, Vector3 velocity, float time, bool hit = false)
        {
            this.position = position;
            this.velocity = velocity;
            this.time = time;
            this.hit = hit;
        }
    }

    [Serializable]
    public struct PungentTrajectorySnapshot
    {
        public Component owner;
        public bool drawInScene;
        public bool drawWhenSelectedOnly;
        public bool drawSamplePoints;
        public bool drawHitMarker;
        public bool drawLabels;
        public Vector3 origin;
        public Vector3 initialVelocity;
        public Vector3 gravity;
        public bool hitFound;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public int sampleCount;
        public int estimatedDrawOperations;
        public float maxDrawDistance;
        public Color trajectoryColor;
        public Color hitColor;
        public Color sampleColor;
        public string warning;
    }

    [Serializable]
    public struct PungentSceneGizmoPerformanceEstimate
    {
        public Component owner;
        public bool active;
        public bool drawInScene;
        public bool selectedOnly;
        public bool hasLabels;
        public bool alwaysVisible;
        public int estimatedDrawOperations;
        public int estimatedLabels;
        public int estimatedTrajectorySamples;
        public int priority;
        public string providerCategory;
        public string warning;
    }
}
