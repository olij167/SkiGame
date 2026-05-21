using System.Collections.Generic;

namespace RaycastPro.RaySensors
{
#if UNITY_EDITOR
    using UnityEditor;
    using System.Threading.Tasks;
#endif

    using UnityEngine;

    struct MeshCache
    {
        public MeshRenderer renderer;
        public MeshFilter filter;
        public Mesh mesh;
        public Material[] materials;
    }
    struct SpriteCache
    {
        public SpriteRenderer renderer;
        public Sprite sprite;
        public Texture2D texture;
    }
    
    static class RaycastMeshCache
    {
        // Set the CacheSize here >>>
        static readonly Dictionary<Collider, MeshCache> cache =
            new Dictionary<Collider, MeshCache>(64);

        public static bool TryGet(Collider col, out MeshCache entry)
        {
            if (cache.TryGetValue(col, out entry))
            {
                // اگر renderer یا mesh نابود شده باشد
                if (!entry.renderer || !entry.filter || !entry.mesh)
                {
                    cache.Remove(col);
                    return false;
                }

                return true;
            }

            return false;
        }

        public static MeshCache Resolve(Collider col)
        {
            var t = col.transform;

            var renderer = t.GetComponent<MeshRenderer>();
            var filter = t.GetComponent<MeshFilter>();

            MeshCache entry = default;

            if (renderer && filter && filter.sharedMesh)
            {
                entry.renderer = renderer;
                entry.filter = filter;
                entry.mesh = filter.sharedMesh;
                entry.materials = renderer.sharedMaterials;

                cache[col] = entry;
            }

            return entry;
        }
    }
    static class RaycastSpriteCache
    {
        // Set the CacheSize here >>>
        static readonly Dictionary<Collider, SpriteCache> cache =
            new Dictionary<Collider, SpriteCache>(64);

        public static bool TryGet(Collider col, out SpriteCache entry)
        {
            if (cache.TryGetValue(col, out entry))
            {
                if (!entry.renderer || !entry.sprite || !entry.texture)
                {
                    cache.Remove(col);
                    return false;
                }
                return true;
            }
            return false;
        }

        public static SpriteCache Resolve(Collider col)
        {
            SpriteCache entry = default;

            var sr = col.GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite)
                return entry;

            entry.renderer = sr;
            entry.sprite   = sr.sprite;
            entry.texture  = sr.sprite.texture;

            cache[col] = entry;
            return entry;
        }
    }


    [IsNew]
    [AddComponentMenu("RaycastPro/Rey Sensors/" + nameof(AlphaRay))]
    public sealed class AlphaRay : RaySensor
    {
        [Tooltip("Minimum alpha value required for the raycast hit to be considered valid. " +
                 "Pixels with alpha below this threshold will be treated as transparent and ignored.")]
        public float alphaThreshold = 0.5f;

        [Tooltip("When enabled, the raycast evaluates alpha using Sprite sampling instead of Mesh/Material sampling. " +
                 "Use this for SpriteRenderer-based objects to allow the ray to pass through transparent sprite areas.")]
        public bool spriteThrough = false;
        
        [Tooltip("Enables component caching to avoid repeated GetComponent calls during frequent raycasts. " +
                 "Improves performance when raycasting every frame, especially with alpha-based penetration.")]
        public bool cacheComponent = true;
        protected override void OnCast() => AlphaCast(alphaThreshold, out hit);

        public bool AlphaCast(float alphaThreshold, out RaycastHit finalHit)
        {
            finalHit = default;

            var origin = Base;
            var remainingLength = DirectionLength;
            var nDir = Direction.normalized;
            const float offset = 0.001f;

            while (remainingLength > 0f)
            {
                if (!Physics.Raycast(origin,
                        Direction,
                        out var hit,
                        remainingLength,
                        detectLayer.value,
                        triggerInteraction))
                {
                    return false;
                }

                if (cacheComponent)
                {
                    // اگر آلفا قابل قبول است → برخورد نهایی
                    if ((spriteThrough ? hit.GetSpriteColorCached().a : hit.GetColorCached().a) >= alphaThreshold)
                    {
                        finalHit = hit;
                        return true;
                    }
                }
                else
                {
                    if ((spriteThrough ? hit.GetSpriteColor().a : hit.GetColor().a) >= alphaThreshold)
                    {
                        finalHit = hit;
                        return true;
                    }
                }
                
                // آلفا شفاف است → ادامه Raycast
                var traveled = hit.distance + offset;
                remainingLength -= traveled;

                if (remainingLength <= 0f)
                    return false;

                origin = hit.point + nDir * offset;
            }
            return false;
        }


#if UNITY_EDITOR
        internal override string Info =>
            "An advanced raycasting system designed to intelligently penetrate transparent and semi-transparent surfaces by iteratively casting ray segments and evaluating each hit based on texture alpha, ignoring surfaces below a defined opacity threshold until a sufficiently opaque surface is found, making it ideal for shooting through glass, foliage-safe line-of-sight checks, particle-agnostic targeting, and advanced AI vision systems while remaining optimized for live editor visualization."
            + HAccurate +
            HDirectional;

        /// <summary>
        /// Hint: This command will make your references missing.
        /// </summary>
        [ContextMenu("Convert To PipeRay")]
        private async void ConvertToPipeRay()
        {
            var _ray = Undo.AddComponent<PipeRay>(gameObject);
            _ray.direction = direction;
            await Task.Delay(1);
            Undo.DestroyObjectImmediate(this);
        }

        [ContextMenu("Convert To BoxRay")]
        private async void ConvertToBoxRay()
        {
            var _ray = Undo.AddComponent<BoxRay>(gameObject);
            _ray.direction = direction;
            await Task.Delay(1);
            Undo.DestroyObjectImmediate(this);
        }

        private Vector3 _p;

        internal override void OnGizmos()
        {
            EditorUpdate();

            _p = transform.position;
            Gizmos.color = Performed ? DetectColor : DefaultColor;
            
            if (IsManuelMode)
            {
                GizmoColor = DefaultColor;
                Gizmos.DrawRay(transform.position, Direction);
            }
            else
            {
                DrawBlockLine(_p, _p + Direction, hit);
            }

            DrawNormal(hit);
        }

        internal override void EditorPanel(SerializedObject _so, bool hasMain = true, bool hasGeneral = true,
            bool hasEvents = true,
            bool hasInfo = true)
        {
            if (hasMain)
            {
                DirectionField(_so);
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(cacheComponent)));
                EditorGUILayout.PropertyField(_so.FindProperty(nameof(spriteThrough)));
                PropertySliderField(_so.FindProperty(nameof(alphaThreshold)), 0, 1, "Alpha Threshold".ToContent());
            }

            if (hasGeneral) GeneralField(_so);
            if (hasEvents) EventField(_so);
            if (hasInfo) InformationField();
        }
#endif
        public override Vector3 Tip => transform.position + Direction;

        public override Vector3 RawTip => Tip;
        public override float RayLength => direction.magnitude;
        public override Vector3 Base => transform.position;
    }
}