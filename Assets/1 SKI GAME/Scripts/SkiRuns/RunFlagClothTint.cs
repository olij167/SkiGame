using UnityEngine;

namespace SkiGame.Runs
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class RunFlagClothTint : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color tint = Color.white;

        // Optional cache to avoid repeated GetComponent calls.
        [SerializeField, HideInInspector] private Renderer cachedRenderer;

        // Reuse MPB to avoid per-apply allocations.
        private MaterialPropertyBlock _mpb;

        // Some prefab scripts/animators can overwrite renderer state in Start/Awake order;
        // force a couple of re-applies at runtime to win that race without doing per-frame work long-term.
        private int _forceApplyFrames;

        public Color Tint
        {
            get => tint;
            set
            {
                tint = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            if (cachedRenderer == null)
                cachedRenderer = TryResolveClothRenderer();

            _forceApplyFrames = Application.isPlaying ? 2 : 0;
            Apply();
        }

        private void LateUpdate()
        {
            if (_forceApplyFrames > 0)
            {
                _forceApplyFrames--;
                Apply();
            }
        }

        private Renderer TryResolveClothRenderer()
        {
            // Find the first child that has Cloth and a Renderer.
            var cloth = GetComponentInChildren<Cloth>(true);
            if (cloth != null)
            {
                var r = cloth.GetComponent<Renderer>();
                if (r != null) return r;
            }

            // Fallback: first Renderer under this root (not ideal, but prevents null behaviour).
            return GetComponentInChildren<Renderer>(true);
        }

        private void Apply()
        {
            if (cachedRenderer == null)
                cachedRenderer = TryResolveClothRenderer();

            if (cachedRenderer == null) return;

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            cachedRenderer.GetPropertyBlock(_mpb);

            // Support common shader property names.
            _mpb.SetColor(BaseColorId, tint);
            _mpb.SetColor(ColorId, tint);

            cachedRenderer.SetPropertyBlock(_mpb);
        }
    }
}
