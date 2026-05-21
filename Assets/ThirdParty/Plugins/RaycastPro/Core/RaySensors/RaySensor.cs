namespace RaycastPro.RaySensors
{
    using System.Collections.Generic;
    using Planers;
    using UnityEngine;

#if UNITY_EDITOR
    using Editor;
    using UnityEditor;
#endif

    /// <summary>
    /// A powerful Ray interaction component that provides detailed access to raycasting results, 
    /// including direction, hit data, physics interaction, terrain detection, and material access.
    /// Use this component to query hits, manipulate hit objects, and retrieve advanced spatial data.
    /// </summary>
    public abstract class RaySensor : BaseRaySensor<RaycastHit, RaycastEvent, Planar>
    {
        /// <summary>
        /// Indicates whether the ray has hit a valid transform.
        /// </summary>
        /// <remarks>
        /// Returns <c>true</c> if a valid transform was hit by the ray.
        /// </remarks>
        [Tooltip("Returns true if the ray has hit a valid transform.")]
        public override bool Performed
        {
            get => hit.transform;
            protected set { }
        }

        /// <summary>
        /// Checks if the hit object has the specified tag.
        /// </summary>
        /// <param name="_tag">The tag to check against.</param>
        /// <returns>True if the hit object has the tag.</returns>
        [Tooltip("Check if the hit object has the specified tag.")]
        public bool HitInTag(string _tag) => hit.transform.CompareTag(_tag);


        /// <summary>
        /// Gets the ray direction in the selected space (World or Local).
        /// </summary>
        /// <remarks>
        /// Automatically uses local or world direction based on the 'local' flag.
        /// </remarks>
        [Tooltip("Ray direction (not normalized), in local or world space.")]
        public bool HitInLayer(LayerMask mask)
        {
            return hit.transform && mask == (mask | 1 << hit.transform.gameObject.layer);
        }


        internal RaySensor baseRaySensor;
        internal RaySensor cloneRaySensor;

        /// <summary>
        /// Ray direction in World space.
        /// </summary>
        public Vector3 direction = Vector3.forward;

        #region Lambdas

        /// <summary>
        /// The final point of the ray in terms of the presence of Hit.
        /// </summary>
        public override Vector3 TipTarget => hit.transform ? hit.point : Tip;

        /// <summary>
        /// Direct length of the Ray from base to tip. Equivalent: (direction.magnitude)
        /// </summary>
        public float DirectionLength => direction.magnitude;

        /// <summary>
        /// The direct distance from the ray origin to the hit point. 
        /// Returns the full ray length if no hit occurred.
        /// </summary>
        [Tooltip("Returns the distance from the base to hit point, or full length if no hit.")]
        public float HitDistance => hit.transform ? (hit.point - Base).magnitude : TipLength;

        /// <summary>
        /// The length traveled from Base to Hit point regard breaking lines in path rays.
        /// </summary>
        public virtual float HitLength => HitDistance;

        /// <summary>
        /// The direction of the ray at the Hit Point. (not Normalized)
        /// </summary>
        public virtual Vector3 HitDirection => hit.transform ? hit.point - Base : Direction;

        /// <summary>
        /// Gets the ray direction in the selected space (World or Local).
        /// </summary>
        /// <remarks>
        /// Automatically uses local or world direction based on the 'local' flag.
        /// </remarks>
        [Tooltip("Ray direction (not normalized), in local or world space.")]
        public Vector3 Direction => local ? LocalDirection : direction;

        /// <summary>
        /// Ray direction in Selected Space with full scaling direction
        /// </summary>
        public Vector3 ScaledDirection => Vector3.Scale(transform.lossyScale, Direction);

        public float FlatScale => (transform.lossyScale.x + transform.lossyScale.y) / 2f;

        /// <summary>
        /// Ray direction in Local space. (not Normalized)
        /// </summary>
        public Vector3 LocalDirection => transform.TransformDirection(direction);

        /// <summary>
        /// The remaining distance from the ray trail to the Hit Point. Returns Length if there is not a hit.
        /// </summary>
        public virtual float ContinuesDistance => hit.transform ? (Tip - hit.point).magnitude : DirectionLength;

        /// <summary>
        /// In case of collision, it returns the direction of Flat normal, otherwise it returns the direction base to the tip.
        /// </summary>
        public override Vector3 TargetDirection => hit.transform ? -hit.normal : TipDirection;

        /// <summary>
        /// Calculate the angle of inclination based on the up axis
        /// </summary>
        public float EdgeSlop => Vector3.Angle(hit.normal, local ? transform.up : Vector3.up);

        public RaySensor LastClone
        {
            get
            {
                var sensor = this;

                while (true)
                {
                    var _clone = sensor.cloneRaySensor;

                    if (_clone)
                    {
                        sensor = _clone;
                        continue;
                    }

                    return sensor;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns a world position located at a specific distance along the path.
        /// Example: If the path is 10 meters long and you pass distance = 3,
        /// the returned position will be exactly 3 meters forward on the path,
        /// even if the path bends or turns.
        /// 
        /// Common use cases:
        /// - Moving a projectile smoothly along a curved path
        /// - Advancing an object step-by-step over time
        /// - Sampling future positions for prediction or preview
        /// </summary>
        /// <param name="distance">
        /// Distance from the start of the path, measured cumulatively across all segments.
        /// </param>
        /// <returns>
        /// A world-space position at the requested distance along the path.
        /// </returns>
        public Vector3 GetPositionOnPath(float distance)
        {
            var rayPath = new List<Vector3>();

            GetPath(ref rayPath);

            for (var i = 1; i < rayPath.Count; i++)
            {
                var edgeLength = rayPath.GetEdgeLength(i);
                if (distance <= edgeLength)
                {
                    return Vector3.Lerp(rayPath[i - 1], rayPath[i], distance / edgeLength);
                }

                distance -= edgeLength;
            }

            return Base;
        }

        /// Returns a position offset from the hit point along the surface normal.
        /// Example: After hitting a wall, pushing the position slightly outward
        /// prevents objects or effects from clipping into the surface.
        /// 
        /// Common use cases:
        /// - Spawning impact effects slightly off the surface
        /// - Placing decals without z-fighting
        /// - Offsetting objects away from collision geometry
        /// </summary>
        /// <param name="value">
        /// Offset distance applied in the direction of the hit normal.
        /// </param>
        /// <returns>
        /// A position slightly outside the hit surface.
        /// </returns>
        public Vector3 HitOffsetByNormal(float value) => hit.point + hit.normal * value;

        /// <summary>
        /// Returns a position offset backward from the hit point, opposite to the cast direction.
        /// Example: When a bullet hits a target, this can be used to move the bullet
        /// slightly backward along its travel direction.
        /// 
        /// Common use cases:
        /// - Pulling objects back after a hit
        /// - Computing a safe fallback position
        /// - Avoiding penetration artifacts after collision
        /// </summary>
        /// <param name="value">
        /// Offset distance applied opposite to the local direction.
        /// </param>
        /// <returns>
        /// A position behind the hit point relative to the cast direction.
        /// </returns>
        public Vector3 HitOffsetByReverseDirection(float value) => hit.point - LocalDirection * value;

        /// <summary>
        /// Replaces the current direction vector with a new one.
        /// Example: Resetting the cast direction when the player changes aim.
        /// 
        /// Common use cases:
        /// - Updating aim direction
        /// - Redirecting a ray or projectile
        /// - Overriding movement direction
        /// </summary>
        /// <param name="newDirection">
        /// The new direction vector to use.
        /// </param>
        public void SetDirection(Vector3 newDirection) => direction = newDirection;

        /// <summary>
        /// Adds a vector to the current direction, allowing gradual or additive changes.
        /// Example: Adding recoil, spread, or steering influence to an existing direction.
        /// 
        /// Common use cases:
        /// - Weapon recoil
        /// - Steering projectiles
        /// - Directional noise or randomness
        /// </summary>
        /// <param name="vector">
        /// The vector added to the current direction.
        /// </param>
        public void AddDirection(Vector3 vector) => direction += vector;

        /// <summary>
        /// Enables or disables the GameObject that was hit by the cast.
        /// Example: Temporarily hiding a hit target instead of destroying it.
        /// 
        /// Common use cases:
        /// - Toggle visibility of hit objects
        /// - Enable / disable interaction targets
        /// - Pool-friendly object management
        /// </summary>
        /// <param name="toggle">
        /// True to activate the hit object, false to deactivate it.
        /// </param>
        public void SetHitActive(bool toggle)
        {
            if (hit.transform) hit.transform.gameObject.SetActive(toggle);
        }

        /// <summary>
        /// Destroys the GameObject that was hit by the cast, optionally after a delay.
        /// Example: Destroying a destructible object a few seconds after impact.
        /// 
        /// Common use cases:
        /// - Breaking objects
        /// - Cleaning up temporary targets
        /// - Delayed destruction for effects or animations
        /// </summary>
        /// <param name="delay">
        /// Optional delay in seconds before destruction.
        /// </param>
        public void DestroyHit(float delay = 0f)
        {
            if (hit.transform) Destroy(hit.transform.gameObject, delay);
        }

        /// <summary>
        /// Sets the world position of the hit object.
        /// Example: Snapping a hit object to a grid or moving it to an exact point.
        /// 
        /// Common use cases:
        /// - Aligning objects after collision
        /// - Teleporting hit targets
        /// - Correcting placement errors
        /// </summary>
        /// <param name="newPosition">
        /// The new world position to assign.
        /// </param>
        public void SetTargetPosition(Vector3 newPosition)
        {
            if (hit.transform) hit.transform.position = newPosition;
        }

        /// <summary>
        /// Moves the hit object by a relative translation.
        /// Example: Nudging an object slightly upward or sideways after impact.
        /// 
        /// Common use cases:
        /// - Fine adjustments after collision
        /// - Push-back or knockback effects
        /// - Incremental movement logic
        /// </summary>
        /// <param name="vector">
        /// The translation vector applied to the hit object.
        /// </param>
        public void TranslateTargetPosition(Vector3 vector)
        {
            if (hit.transform) hit.transform.Translate(vector);
        }

        /// <summary>
        /// Instantiates a copy of the hit object at a given location,
        /// using the tip direction as its forward orientation.
        /// Example: Creating a duplicate target at a specific spawn point,
        /// aligned with the cast direction.
        /// 
        /// Common use cases:
        /// - Spawning projectiles or props based on a hit
        /// - Cloning interactable objects
        /// - Creating chained or recursive effects
        /// </summary>
        /// <param name="location">
        /// The world position where the new instance will be created.
        /// </param>
        public void InstantiateTargetObject(Vector3 location)
        {
            if (hit.transform) Instantiate(hit.transform, location, Quaternion.LookRotation(TipDirection));
        }

        /// <summary>
        /// Applies a physical force to the hit Rigidbody in the direction of the surface normal.
        /// 
        /// Example:
        /// When a bullet hits a wall or object, this pushes the object outward,
        /// directly away from the surface it was hit on.
        /// 
        /// Common use cases:
        /// - Impact reactions (objects pushed away from walls)
        /// - Explosion or shockwave effects
        /// - Preventing objects from sticking to surfaces
        /// </summary>
        /// <param name="force">
        /// The strength of the force applied along the surface normal.
        /// </param>
        public void AddForceAlongNormal(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(hit.normal * force);
            }
        }

        /// Applies a physical force to the hit Rigidbody in the direction of the hit.
        /// 
        /// Example:
        /// A projectile transfers its momentum to an object, pushing it forward
        /// in the same direction it was traveling.
        /// 
        /// Common use cases:
        /// - Bullet impact force
        /// - Knockback effects
        /// - Momentum transfer from fast-moving objects
        /// </summary>
        /// <param name="force">
        /// The strength of the force applied in the hit direction.
        /// </param>
        public void AddForceAlongHitDirection(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(HitDirection.normalized * force);
            }
        }

        /// <summary>
        /// Applies a force to the hit Rigidbody using the tip direction as the forward vector.
        /// 
        /// Example:
        /// Useful when the visual or logical "tip" of a tool or weapon determines
        /// the force direction rather than the actual hit ray.
        /// 
        /// Common use cases:
        /// - Melee weapons (swords, spears)
        /// - Tools or probes with a defined forward tip
        /// - Directionally controlled interactions
        /// </summary>
        /// <param name="force">
        /// The strength of the force applied along the tip direction.
        /// </param>
        public void AddForceAlongTipDirection(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(TipDirection.normalized * force);
            }
        }

        /// <summary>
        /// Applies a dynamic force along the tip direction, scaled by the continuous distance.
        /// 
        /// Example:
        /// The farther the object travels before hitting something,
        /// the stronger the applied force becomes.
        /// 
        /// Common use cases:
        /// - Charge-based attacks
        /// - Speed-scaled impacts
        /// - Variable-strength collisions
        /// </summary>
        /// <param name="force">
        /// Base force multiplier applied along the tip direction.
        /// </param>
        public void AddDynamicForceAlongTipDirection(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(TipDirection.normalized * ContinuesDistance * force);
            }
        }

        /// <summary>
        /// Applies a dynamic force along the surface normal,
        /// scaled by the continuous travel distance.
        /// 
        /// Example:
        /// A fast-moving object hitting a surface causes a stronger push
        /// directly away from the impact point.
        /// 
        /// Common use cases:
        /// - Physics-based collisions
        /// - Heavy impacts
        /// - Distance-based damage or reaction systems
        /// </summary>
        /// <param name="force">
        /// Base force multiplier applied along the surface normal.
        /// </param>
        public void AddDynamicForceAlongNormal(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(hit.normal * ContinuesDistance * force);
            }
        }

        /// <summary>
        /// Applies a dynamic force in the hit direction,
        /// scaled by the continuous travel distance.
        /// 
        /// Example:
        /// A projectile that travels a longer distance before impact
        /// transfers more energy forward to the hit object.
        /// 
        /// Common use cases:
        /// - Projectile physics
        /// - Velocity-based knockback
        /// - Energy accumulation systems
        /// </summary>
        /// <param name="force">
        /// Base force multiplier applied in the hit direction.
        /// </param>
        public void AddDynamicForceAlongHitDirection(float force)
        {
            if (hit.transform.TryGetComponent(out Rigidbody body))
            {
                body.AddForce(HitDirection.normalized * ContinuesDistance * force);
            }
        }

        /// <summary>
        /// Plays an audio clip at the hit point in world space.
        /// 
        /// Example:
        /// Playing an impact sound exactly where a bullet or tool hits a surface.
        /// 
        /// Common use cases:
        /// - Bullet or melee impact sounds
        /// - Environmental interaction audio
        /// - Spatial sound feedback
        /// </summary>
        /// <param name="clip">
        /// The audio clip to play at the hit location.
        /// </param>
        public void PlaySoundAtHitPoint(AudioClip clip) => AudioSource.PlayClipAtPoint(clip, hit.point, 1f);

        public void ChangeMaterial(Material material)
        {
            if (hit.transform.TryGetComponent(out MeshRenderer mesh))
            {
                mesh.material = material;
            }
        }

        /// <summary>
        /// Applies a random color to the material of the hit object (if MeshRenderer is found).
        /// </summary>
        [Tooltip("Applies a random color to the hit object's material.")]
        public void ChangeRandomColor()
        {
            if (hit.transform && hit.transform.TryGetComponent(out MeshRenderer mesh))
            {
                mesh.material.color = Random.ColorHSV();
            }
        }

        /// <summary>
        /// Gets a component of type T from the hit object.
        /// </summary>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>The component of type T if found; otherwise null.</returns>
        [Tooltip("Returns the component of type T attached to the hit object.")]
        public T GetHitComponent<T>()
        {
            if (hit.transform && hit.transform.TryGetComponent<T>(out var component))
                return component;

            return default;
        }

        /// <summary>
        /// Get Hit point Material Color 
        /// </summary>
        public Color HitColor => hit.GetColor();

        /// <summary>
        /// Get Hit point Material Alpha 
        /// </summary>
        public float HitAlpha => hit.GetColor().a;

        /// <summary>
        /// Get Hit point Sprite Alpha 
        /// </summary>
        public float HitSpriteAlpha => hit.GetSpriteColor().a;

        /// <summary>
        /// Directly get current hit material detection. #Detection
        /// </summary>
        public Material HitMaterial => hit.GetMaterial();

        /// <summary>
        /// Get Hit (Terrain) currently most alpha map value Index. (return's -1 in default)
        /// </summary>
        public int HitTerrainIndex => hit.GetTerrainIndex();

        /// <summary>
        /// Get Array of alpha map value on hit Point.
        /// </summary>
        /// <param name="alphasValues"></param>
        public void GetHitTerrainAlpha(ref float[] alphasValues) => hit.GetTerrainAlpha(ref alphasValues);

        internal static int GetSubMeshIndex(Mesh mesh, int triangleIndex)
        {
            if (!mesh.isReadable) return 0;
            var triangleCounter = 0;
            for (var subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                var indexCount = mesh.GetSubMesh(subMeshIndex).indexCount;
                triangleCounter += indexCount / 3;
                if (triangleIndex < triangleCounter) return subMeshIndex;
            }

            return 0;
        }

        #endregion

        public override bool ClonePerformed => CloneHit.transform;
        public RaycastHit CloneHit => cloneRaySensor ? cloneRaySensor.CloneHit : hit;
        public Vector3 Normal => hit.normal;
        public Vector3 HitPoint => hit.point;

        public virtual void GetPath(ref List<Vector3> path, bool onHit = false)
        {
            path = new List<Vector3>() {Base, onHit ? TipTarget : Tip};
        }

        /// <summary>
        /// Updates the stamp object's position and orientation based on the raycast hit or tip point,
        /// and applies axis alignment and offset if specified.
        /// </summary>
        public override void UpdateStamp()
        {
            if (!stamp || (cloneRaySensor && cloneRaySensor.enabled)) return;

            Transform stampTransform = stamp.transform;
            bool hasHit = hit.transform != null;
            bool sync = syncStamp.syncAxis;
            Vector3 basePosition = hasHit && stampOnHit ? TipTarget : Tip;
            Vector3 normalOrTip = hasHit && stampOnHit ? hit.normal : TipDirection;

            // Set base position
            stampTransform.position = basePosition;

            // Apply rotation according to selected axis
            if (sync)
            {
                Vector3 aligned = normalOrTip * (syncStamp.flipAxis ? -1f : 1f);
                switch (syncStamp.axis)
                {
                    case Axis.X:
                        stampTransform.right = aligned;
                        break;
                    case Axis.Y:
                        stampTransform.up = aligned;
                        break;
                    case Axis.Z:
                        stampTransform.forward = aligned;
                        break;
                }
            }

            // Apply offset
            if (sync)
                stampTransform.position += hit.normal * stampOffset;
            else
                stampTransform.position -= HitDirection.normalized * stampOffset;
        }


        /// <summary>
        /// Updates the attached LineRenderer to reflect the current ray state, 
        /// considering clamping, hit-cutting, and offset corrections.
        /// </summary>
        public override void UpdateLiner()
        {
            if (!liner) return;

            _base = Base;
            _tip  = Tip;

            // Default: two-point line
            liner.positionCount = 2;

            if (linerClamped)
            {
                UpdateClampedLiner();
            }
            else
            {
                UpdateUnclampedLiner();
            }
        }
        private void UpdateClampedLiner()
        {
            Vector3 clampedStart = Vector3.Lerp(_base, _tip, linerBasePosition);
            Vector3 clampedEnd   = Vector3.Lerp(_base, _tip, linerEndPosition);

            if (!linerCutOnHit)
            {
                liner.SetPosition(0, clampedStart);
                liner.SetPosition(1, clampedEnd);
                return;
            }

            // Cut on hit
            if (!hit.transform)
            {
                liner.SetPosition(0, clampedStart);
                liner.SetPosition(1, clampedEnd);
                return;
            }

            Vector3 hitPoint = LinerFixCut
                ? GetPointOnLine(_base, _tip, hit.point)
                : hit.point;

            float hitRatio = (hitPoint - _base).magnitude / RayLength;

            if (hitRatio < linerBasePosition)
            {
                liner.positionCount = 0;
                return;
            }

            Vector3 end = hitRatio < linerEndPosition
                ? hitPoint
                : clampedEnd;

            liner.SetPosition(0, clampedStart);
            liner.SetPosition(1, end);
        }
        private void UpdateUnclampedLiner()
        {
            liner.SetPosition(0, _base);

            if (!linerCutOnHit)
            {
                liner.SetPosition(1, _tip);
                return;
            }

            Vector3 targetTip = TipTarget;

            Vector3 end = LinerFixCut
                ? _base + Vector3.Project(targetTip - _base, Direction)
                : targetTip;

            liner.SetPosition(1, end);
        }

        /// <summary>/// <summary>
        /// Executes a path-based cast model and collects all detected hits into the provided list.
        /// Returns the index of the first path segment that produces a hit, or -1 if no hit occurs.
        /// </summary>
        /// <param name="raycastHits">
        /// Reusable hit buffer that will be cleared and populated by the cast implementation.
        /// </param>
        /// <param name="finalHit">
        /// The resolved hit selected by the implementation (usually the closest hit).
        /// </param>
        /// <returns>
        /// Index of the first path segment that generated a hit; -1 if no hit was detected.
        /// </returns>
        public virtual int AllCast(ref List<RaycastHit> raycastHits, out RaycastHit finalHit)
        {
            finalHit = default;
            return -1;
        }


        // ReSharper disable Unity.PerformanceAnalysis
        internal override void RuntimeUpdate()
        {
            OnCast();
            onCast?.Invoke();

            /// Liner will Going to modifiers at V2.0
            UpdateLiner();
            UpdateStamp();

            if (hit.transform) OnDetect();
            if (PreviousHit.transform != hit.transform)
            {
                // end Event most be top of begin
                if (PreviousHit.transform)
                {
                    onChange?.Invoke(PreviousHit);
                    OnEndDetect();
                }

                if (hit.transform)
                {
                    onChange?.Invoke(hit);
                    OnBeginDetect();
                }
            }

            PreviousHit = hit;
        }

        internal override void OnDetect()
        {
            if (planarSensitive)
            {
                if (anyPlanar)
                {
                    if (!_planar) return;

                    _planar.OnReceiveRay(this);
                    _planar.onReceiveRay?.Invoke(this);
                }
                else
                {
                    foreach (var p in planers)
                    {
                        if (!p || p.transform != hit.transform) continue;

                        p.OnReceiveRay(this);
                        p.onReceiveRay?.Invoke(this);
                    }
                }
            }

            onDetect?.Invoke(hit);
        }

        internal override void OnEndDetect()
        {
            if (stampAutoHide) stamp?.gameObject.SetActive(false);
            if (planarSensitive)
            {
                if (anyPlanar)
                {
                    if (!_planar) return;
                    _planar.OnEndReceiveRay(this);
                    _planar.onEndReceiveRay?.Invoke(this);
                    _planar = null;
                }
                else
                {
                    foreach (var p in planers)
                    {
                        if (!p || p.transform != PreviousHit.transform) continue;
                        p.OnEndReceiveRay(this);
                        p.onEndReceiveRay?.Invoke(this);
                    }
                }
            }

            onEndDetect?.Invoke(PreviousHit);
        }

        internal override void OnBeginDetect()
        {
            if (stampAutoHide) stamp?.gameObject.SetActive(true);
            if (planarSensitive)
            {
                if (anyPlanar)
                {
                    _planar = hit.transform.GetComponent<Planar>();
                    if (!_planar) return;
                    _planar.OnBeginReceiveRay(this);
                    _planar.onBeginReceiveRay?.Invoke(this);
                }
                else
                {
                    foreach (var p in planers)
                    {
                        if (!p || p.transform != hit.transform) continue;
                        p.OnBeginReceiveRay(this);
                        p.onBeginReceiveRay?.Invoke(this);
                    }
                }
            }

            onBeginDetect?.Invoke(hit);
        }

        public static void CloneDestroy(RaySensor sensor)
        {
            while (true)
            {
                if (!sensor || !sensor.gameObject) return;
                if (sensor.cloneRaySensor)
                {
                    sensor = sensor.cloneRaySensor;
                    continue;
                }

                Destroy(sensor.gameObject);
                break;
            }
        }

        // This function will destroy every clone before destroy the main
        internal override void SafeRemove()
        {
            if (cloneRaySensor && cloneRaySensor.gameObject)
            {
                Destroy(cloneRaySensor.gameObject);
            }
        }

#if UNITY_EDITOR
        protected override void EditorUpdate()
        {
            if (!RCProPanel.realtimeEditor)
            {
                GizmoGate = null;
                hit = default;
                return;
            }

            if (!IsPlaying && IsSceneView)
            {
                OnCast();
                UpdateStamp();
                UpdateLiner();
            }

            GizmoGate?.Invoke();

            if (cloneRaySensor && cloneRaySensor.gameObject) cloneRaySensor.OnGizmos();
        }

        protected void DrawNormal(RaycastHit hit, bool label = true, bool doubleDisc = false,
            Color color = default)
        {
            if (!hit.transform) return;
            Handles.color = color == default ? HelperColor : color;
            Handles.DrawWireDisc(hit.point, hit.normal, DiscSize);
            if (doubleDisc) Handles.DrawWireDisc(hit.point + hit.normal * DotSize, hit.normal, DiscSize);

            Handles.DrawLine(hit.point, hit.point + hit.normal * LineSize);
            if (RCProPanel.ShowLabels && label)
                Handles.Label(hit.point + hit.normal * DotSize, hit.transform.name, RCProEditor.HeaderStyle);
        }

        protected void GeneralField(SerializedObject _so)
        {
            DetectLayerField(_so);
            LinerField(_so);
            StampField(_so);
            PlanarField(_so);
            BaseField(_so);
        }

        protected void InformationField()
        {
            if (!hit.transform) return;
            InformationField(() =>
            {
                var ID = hit.transform.gameObject.GetInstanceID();
                GUILayout.Label($"Hit: {hit.transform.name}".ToContent(
                    $"Instance ID: {ID}, Located at: {hit.transform.position}, Offset from transform: {hit.transform.position - Hit.point}"));
                GUILayout.Label($"Continues Distance: {ContinuesDistance:F}".ToContent("Continues Distance"));
                if (this is PathRay pathRay)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Detect Index: ");
                    GUILayout.Label(pathRay.DetectIndex.ToString());
                    GUILayout.EndHorizontal();
                }
            });
        }
#endif
    }
}