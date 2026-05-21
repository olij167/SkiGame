using RaycastPro.RaySensors;
using UnityEngine.Serialization;

namespace RaycastPro.Detectors
{
    using System.Collections.Generic;
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [AddComponentMenu("RaycastPro/Detectors/" + nameof(SteeringDetector))]
    public sealed class SteeringDetector : Detector, IRadius, IPulse
    {
        [Tooltip(
            "Target destination that the steering solver attempts to reach.\n" +
            "The solver continuously evaluates obstacles between the agent and this Transform and adjusts movement accordingly.\n\n" +
            "Example: An enemy navigating toward the player while avoiding walls and corners.")]
        public Transform destination;

        [Tooltip(
            "Ground-aligned ray sensor used to synchronize the steering direction with the surface plane.\n" +
            "Useful for slopes, uneven terrain, or grounded movement where forward direction must respect the ground normal.\n\n" +
            "Example: Keeping an AI character aligned to a hill instead of floating or tilting incorrectly.")]
        public RaySensor groundRay;


        [Tooltip(
            "Radius of the auxiliary sphere used in SphereCast for direct path validation.\n" +
            "This value should roughly match the physical width of the character to prevent clipping through obstacles.\n\n" +
            "Example: A large character requires a larger value to avoid squeezing through narrow gaps.")]
        public float colliderSize = 0.1f;

        [Tooltip(
            "Maximum steering detection radius.\n" +
            "Defines how far the solver scans the environment for obstacles and alternative paths.\n\n" +
            "Example: A higher value allows earlier obstacle anticipation but increases computation cost.")]
        public float radius = 20f;

        [Tooltip(
            "Distance from the destination at which the solver begins stopping behavior.\n" +
            "When the agent reaches this range, steering is smoothly damped to zero.\n\n" +
            "Example: Prevents overshooting or jittering when reaching a target.")]
        public float stoppingDistance = 2f;


        [Tooltip(
            "If enabled, all random steering rays are generated relative to the agent's local forward direction.\n" +
            "If disabled, rays are generated in world-space forward direction.\n\n" +
            "Example: Enable this for characters that rotate freely; disable for global navigation logic.")]
        public bool local;

        [Tooltip(
            "Horizontal steering cone angle in degrees.\n" +
            "Controls how widely the solver can search left and right for alternative paths.\n\n" +
            "Example: Larger values allow wider turns but may reduce directional focus.")]
        public float angleX = 120f;

        [Tooltip(
            "Vertical steering cone angle in degrees.\n" +
            "Controls how much the solver can probe upward or downward directions.\n\n" +
            "Example: Useful for flying agents or multi-level navigation.")]
        public float angleY = 90f;


        [Tooltip(
            "Number of random steering rays evaluated per update cycle.\n" +
            "Higher values increase obstacle detection accuracy but cost more performance.\n\n" +
            "Example: Low values feel faster but less precise; high values feel smarter but heavier.")]
        public int iteration = 8;

        [FormerlySerializedAs("sharpness")]
        [Tooltip(
            "Responsiveness of steering direction changes.\n" +
            "Higher values result in sharper, more immediate turns.\n" +
            "Lower values produce heavier, smoother, more inertial movement.\n\n" +
            "Example: Fast drones use high values; heavy creatures use low values.")]
        public float moveSharpness = 6;


        [Tooltip(
            "Number of previous positions stored by the Mark Solver.\n" +
            "These points act as short-term memory to prevent the agent from returning to recently visited locations.\n\n" +
            "Example: Helps avoid oscillation in tight corridors or corners.")]
        public int markSolverCount = 6;

        [Tooltip(
            "Strength of the Mark Solver's influence on the final steering direction.\n" +
            "Higher values enforce stronger bias away from previously visited positions.\n" +
            "A value of zero disables memory influence entirely.\n\n" +
            "Example: Increasing this makes movement more exploratory.")]
        public float markSolverInfluence = 1f;

        [Tooltip(
            "Time interval (in seconds) between Mark Solver memory updates.\n" +
            "Lower values update memory more frequently but may increase noise.\n\n" +
            "Example: Fast agents benefit from shorter refresh times.")]
        public float markSolverRefreshTime = 1f;


        [Tooltip(
            "Influence of obstacle surface normals on steering direction.\n" +
            "Higher values cause the agent to slide more aggressively along obstacle surfaces.\n\n" +
            "Example: Useful for wall-following or tight navigation behavior.")]
        public float obstacleNormalInfluence = 1f;

        [Tooltip(
            "Influence of obstacle hit distance on steering adjustment.\n" +
            "Closer obstacles generate stronger steering responses.\n\n" +
            "Example: Prevents late reactions when approaching walls at high speed.")]
        public float obstacleDistanceInfluence = 1f;

        [Tooltip(
            "Enables the Spider Solver, an additional reachability test.\n" +
            "When no obstacle is hit, the solver checks line-of-sight from sampled points to the destination.\n" +
            "This improves path selection but increases computational cost.\n\n" +
            "Example: Helps find narrow passages or smart detours around obstacles.")]
        public bool spiderSolver = true;

        public TimeMode timeMode = TimeMode.DeltaTime;

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0, value);
        }

        public override bool Performed
        {
            get => hitCounts > 0;
            protected set { }
        }

        #region cached;

        private int i;
        private float delta, _dis, _cRadius;
        private float F;
        private Vector3 _pos, _randomVector, _dir, _rRadiusVector, _qVec;
        private RaycastHit _raycastHit;

        #endregion

        private Transform currentDestination;
        private int hitCounts;
        private float distValue;
        private float zeroHitOverTime;
        private float weightLocateTimer;

        /// <summary>
        /// Average Point of all detected hits.
        /// </summary>
        private Vector3 averageWeight;

        /// <summary>
        /// Average Normal of all detected hits.
        /// </summary>
        private Vector3 averageNormal;

        public Vector3 Weight => averageWeight;

        /// <summary>
        /// Non-Normalized Steering Direction
        /// </summary>
        public Vector3 RawSteeringDirection => averageNormal + (_pos - averageWeight).normalized;

        [Tooltip("Normalized value of steering Direction")]
        public Vector3 calculatedDirection;

        /// <summary>
        /// Normalized Steering Direction
        /// </summary>
        public Vector3 SteeringDirection
        {
            get
            {
                if (groundRay)
                {
                    return Vector3.ProjectOnPlane(averageNormal + (_pos - averageWeight).normalized,
                        groundRay.Performed ? groundRay.Normal : groundRay.transform.up).normalized;
                }

                return (averageNormal + (_pos - averageWeight).normalized).normalized;
            }
        }

        /// <summary>
        /// Steering Direction as Quaternion
        /// </summary>
        public Quaternion SteeringRotation =>
            Quaternion.LookRotation(SteeringDirection, groundRay ? groundRay.Normal : Vector3.up);

        public float Distance => Vector3.Distance(transform.position, destination.position);

        private readonly Queue<Vector3> weightLocate = new Queue<Vector3>();

        private float _F;
        private Vector3 _DirN;

        private bool IsDirect
        {
            get
            {
                Physics.Linecast(_pos, destination.position, out _raycastHit, detectLayer.value, triggerInteraction);
                return !_raycastHit.transform || _raycastHit.transform == destination;
            }
        }

        public bool InDistance => Vector3.Distance(transform.position, destination.position) < stoppingDistance;

        /// <summary>
        /// Prepares frame-dependent values for the steering update.
        /// Computes delta time based on the selected time mode.
        /// This method must be called before any time-based smoothing.
        /// </summary>
        void SetupFrame()
        {
            delta = GetDelta(timeMode);
        }

        /// <summary>
        /// Performs early-exit conditions for the steering system.
        /// Handles missing destination and stopping-distance behavior.
        /// Smoothly damps the current steering direction when stopping.
        /// </summary>
        /// <returns>
        /// True if steering should continue this frame; otherwise false.
        /// </returns>
        bool CheckStop()
        {
            if (!destination)
                return false;

            if (Distance <= stoppingDistance)
            {
                float f = 1f - Mathf.Exp(-moveSharpness * delta);
                calculatedDirection = Vector3.Lerp(calculatedDirection, Vector3.zero, f);
                return false;
            }

            return true;
        }
        /// <summary>
        /// Updates all cached spatial values used by steering solvers.
        /// Computes position, direction to destination, normalized direction,
        /// distance, and effective sampling radius for this frame.
        /// </summary>
        void UpdateState()
        {
            _pos = transform.position;
            hitCounts = 0;

            _dir = destination.position - _pos;
            _dis = _dir.magnitude;
            _DirN = _dir / _dis;

            _cRadius = Mathf.Min(_dis, radius) * Random.value;
        }
        /// <summary>
        /// Attempts to resolve steering using a direct, unobstructed path
        /// toward the destination.
        /// Performs a sphere cast to detect blocking obstacles.
        /// If the path is clear, updates steering values and exits early.
        /// </summary>
        /// <returns>
        /// True if the direct path was valid and steering was resolved.
        /// </returns>
        bool TryDirectPath()
        {
            if (!IsDirect)
                return false;

            Physics.SphereCast(
                _pos - _DirN * colliderSize,
                colliderSize,
                _dir,
                out _raycastHit,
                _dir.magnitude,
                detectLayer.value,
                triggerInteraction
            );

            if (_raycastHit.transform && _raycastHit.transform != destination)
                return false;

            _F = 1f - Mathf.Exp(-moveSharpness * delta);

            averageWeight = Vector3.Lerp(averageWeight, _pos - _DirN, _F);
            averageNormal = Vector3.Lerp(averageNormal, _DirN, _F);
            calculatedDirection = Vector3.Lerp(calculatedDirection, SteeringDirection, _F);

#if UNITY_EDITOR
            GizmoGate += () =>
            {
                Handles.color = HelperColor;
                DrawCapsuleLine(_pos, destination.position, colliderSize);
            };
#endif
            return true;
        }
        /// <summary>
        /// Applies memory-based steering using previously visited positions.
        /// Maintains a rolling queue of recent locations and biases steering
        /// toward their averaged center to stabilize movement and reduce jitter.
        /// </summary>
        void RunMarkSolver()
        {
            if (weightLocateTimer >= markSolverRefreshTime)
            {
                weightLocateTimer = 0f;
                if (weightLocate.Count >= markSolverCount)
                    weightLocate.Dequeue();

                weightLocate.Enqueue(_pos);
            }
            else
            {
                weightLocateTimer += delta;
            }

            if (markSolverInfluence <= 0f || weightLocate.Count == 0)
                return;

            Vector3 sum = Vector3.zero;

#if UNITY_EDITOR
            _qVec = Vector3.up * (DotSize * 4f);
#endif
            foreach (var p in weightLocate)
            {
                sum += p;

#if UNITY_EDITOR
                GizmoGate += () =>
                {
                    Handles.color = HelperColor;
                    DrawLineZTest(p, p + _qVec);
                };
#endif
            }

            Vector3 avg = sum / weightLocate.Count;
            _F = 1f - Mathf.Exp(-delta * markSolverInfluence);

            averageWeight = Vector3.Lerp(averageWeight, avg, _F);
            averageNormal = Vector3.Lerp(averageNormal, (_pos - avg).normalized, _F);
            calculatedDirection = Vector3.Lerp(calculatedDirection, SteeringDirection, _F);
        }

        /// <summary>
        /// Executes the main obstacle avoidance solver.
        /// Samples multiple randomized directions within angular limits,
        /// raycasts against obstacles, and blends avoidance forces based
        /// on hit distance, hit frequency, and solver influence weights.
        /// </summary>
        void RunObstacleSolver()
        {
            for (i = 0; i < iteration; i++)
            {
                GenerateRandomDirection();

                if (Physics.Raycast(
                        _pos,
                        _randomVector,
                        out _raycastHit,
                        _cRadius,
                        detectLayer.value,
                        triggerInteraction))
                {
                    ResolveObstacleHit();
                }
                else if (spiderSolver)
                {
                    RunSpiderSolver();
                }
            }
        }
        
        /// <summary>
        /// Resolves a single obstacle hit during sampling.
        /// Computes avoidance influence based on hit distance,
        /// blends avoidance direction and weight, and updates
        /// the cumulative steering output.
        /// </summary>
        void ResolveObstacleHit()
        {
            hitCounts++;

            distValue = Mathf.Pow(_raycastHit.distance / _cRadius, 2f);
            F = -delta * hitCounts / iteration * (1f - distValue) * moveSharpness;
            _F = 1f - Mathf.Exp(F);

            averageWeight = Vector3.Lerp(
                averageWeight,
                _raycastHit.point,
                _F * obstacleDistanceInfluence
            );

            averageNormal = Vector3.Lerp(
                averageNormal,
                Vector3.Lerp(
                    _raycastHit.normal * (radius - _raycastHit.distance),
                    (destination.position - _raycastHit.point).normalized,
                    distValue
                ),
                _F * obstacleNormalInfluence
            );

            calculatedDirection = Vector3.Lerp(calculatedDirection, SteeringDirection, _F);

#if UNITY_EDITOR
            var p = _raycastHit.point;
            var rP = _pos + _rRadiusVector;
            GizmoGate += () =>
            {
                Handles.color = DetectColor;
                DrawLineZTest(_pos, p);

                Handles.color = BlockColor;
                DrawLineZTest(p, rP, true);
            };
#endif
        }

        /// <summary>
        /// Generates a randomized steering direction within configured
        /// horizontal and vertical angular limits.
        /// Used for obstacle sampling and spider probing.
        /// </summary>
        void GenerateRandomDirection()
        {
            _randomVector = Quaternion.Euler(
                Random.Range(-angleY, angleY) * 0.5f,
                Random.Range(-angleX, angleX) * 0.5f,
                0f) * (local ? transform.forward : Vector3.forward);

            _rRadiusVector = _randomVector * _cRadius;
        }

        /// <summary>
        /// Performs reachability probing when no immediate obstacle is hit.
        /// Tests whether an offset position can see the destination directly,
        /// biasing steering toward paths that maintain forward progress.
        /// </summary>
        void RunSpiderSolver()
        {
            if (Vector3.Distance(_pos + _rRadiusVector, destination.position) > _dis)
                return;

            Physics.Linecast(
                _pos + _rRadiusVector,
                destination.position,
                out _raycastHit,
                detectLayer.value,
                triggerInteraction
            );

            if (_raycastHit.transform && _raycastHit.transform != destination)
                return;

            _F = 1f - Mathf.Exp(-moveSharpness * delta);

            averageWeight = Vector3.Lerp(averageWeight, _pos - _randomVector, _F);
            averageNormal = Vector3.Lerp(averageNormal, _randomVector.normalized, _F);
            calculatedDirection = Vector3.Lerp(calculatedDirection, SteeringDirection, _F);
        }

        /// <summary>
        /// Applies relaxation toward the direct steering direction when
        /// no obstacles are detected for a sustained period.
        /// Gradually stabilizes movement after avoidance behavior.
        /// </summary>
        void ApplyFreeMoveRelax()
        {
            if (hitCounts == 0)
            {
                zeroHitOverTime = Mathf.Min(zeroHitOverTime + delta, 0.2f);

                if (zeroHitOverTime >= 0.2f)
                {
                    _F = 1f - Mathf.Exp(-moveSharpness * delta);

                    averageWeight = Vector3.Lerp(averageWeight, _pos, _F);
                    averageNormal = Vector3.Lerp(
                        averageNormal,
                        (destination.position - _pos).normalized,
                        _F
                    );

                    calculatedDirection = Vector3.Lerp(calculatedDirection, SteeringDirection, _F);
                }
            }
            else
            {
                zeroHitOverTime = Mathf.Max(zeroHitOverTime - delta, 0f);
            }
        }

        /// <summary>
        /// Main steering update entry point.
        /// Orchestrates all steering solvers in a deterministic order:
        /// setup, direct path check, memory-based steering,
        /// obstacle avoidance, spider probing, and free-move relaxation.
        /// </summary>
        protected override void OnCast()
        {
#if UNITY_EDITOR
            GizmoGate = null;
#endif
            SetupFrame();
            if (!CheckStop())
                return;

            UpdateState();

            if (TryDirectPath())
                return;

            RunMarkSolver();
            RunObstacleSolver();
            ApplyFreeMoveRelax();
        }
        
#if UNITY_EDITOR
        internal override string Info =>
            "An advanced, context-aware steering agent that computes a smooth navigation path. It avoids obstacles by probabilistically sampling the environment and leverages a positional memory to resolve complex blockages." +
            HDependent + HRDetector + HIRadius;

        internal override void OnGizmos()
        {
            EditorUpdate();

            if (IsGuide && IsPlaying)
            {
                DrawNormal(transform.position, calculatedDirection, "Steering Direction", DiscSize);
            }
        }

internal override void EditorPanel(
    SerializedObject _so,
    bool hasMain = true,
    bool hasGeneral = true,
    bool hasEvents = true,
    bool hasInfo = true)
{
    if (hasMain)
    {
        /* =========================================================
         * Core Navigation
         * ========================================================= */
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Core Navigation", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_so.FindProperty(nameof(destination)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(stoppingDistance)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(groundRay)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(moveSharpness)));

        /* =========================================================
         * Detection & Geometry
         * ========================================================= */
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Detection & Geometry", EditorStyles.boldLabel);

        DetectLayerField(_so);

        BeginHorizontal();
        RadiusField(_so);
        LocalField(_so.FindProperty(nameof(local)));
        EndHorizontal();

        EditorGUILayout.PropertyField(_so.FindProperty(nameof(colliderSize)));

        /* =========================================================
         * Sampling & Angles
         * ========================================================= */
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Sampling & Angles", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_so.FindProperty(nameof(iteration)));

        PropertySliderField(
            _so.FindProperty(nameof(angleX)),
            0f,
            360f,
            "Horizontal Arc (X)".ToContent("Horizontal spread of steering rays.")
        );

        PropertySliderField(
            _so.FindProperty(nameof(angleY)),
            0f,
            360f,
            "Vertical Arc (Y)".ToContent("Vertical spread of steering rays.")
        );

        /* =========================================================
         * Obstacle Avoidance
         * ========================================================= */
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Obstacle Avoidance", EditorStyles.boldLabel);

        PropertySliderField(
            _so.FindProperty(nameof(obstacleNormalInfluence)),
            0f,
            1f,
            "Normal Influence".ToContent("How strongly surface normals affect steering.")
        );

        PropertySliderField(
            _so.FindProperty(nameof(obstacleDistanceInfluence)),
            0f,
            1f,
            "Distance Influence".ToContent("How strongly obstacle distance affects steering.")
        );

        EditorGUILayout.PropertyField(_so.FindProperty(nameof(spiderSolver)));

        /* =========================================================
         * Mark Solver (Memory-Based Steering)
         * ========================================================= */
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Mark Solver (Memory)", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_so.FindProperty(nameof(markSolverRefreshTime)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(markSolverCount)));

        PropertySliderField(
            _so.FindProperty(nameof(markSolverInfluence)),
            0f,
            10f,
            "Mark Influence".ToContent("Strength of memory-based steering.")
        );

        /* =========================================================
         * Time & Simulation
         * ========================================================= */
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Time & Simulation", EditorStyles.boldLabel);

        PropertyTimeModeField(_so.FindProperty(nameof(timeMode)));
    }

    if (hasGeneral)
    {
        EditorGUILayout.Space(8);
        GeneralField(_so, layerField: false);
        BaseField(_so);
    }

    if (hasEvents)
    {
        EditorGUILayout.Space(6);
        EventField(_so);
    }

    if (hasInfo)
    {
        EditorGUILayout.Space(6);
        InformationField(PanelGate);
    }
}

        protected override void DrawDetectorGuide(Vector3 point)
        {
        }
#endif
    }
}