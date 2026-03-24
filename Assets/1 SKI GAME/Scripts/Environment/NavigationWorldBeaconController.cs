using UnityEngine;

namespace SkiGame.Navigation
{
    [DisallowMultipleComponent]
    public sealed class NavigationWorldBeaconController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float verticalOffset = 14f;
        [SerializeField] private float bobAmplitude = 0.8f;
        [SerializeField] private float bobFrequency = 2.2f;

        private Renderer[] _renderers;
        private Canvas[] _canvases;
        private Vector3 _anchorPosition;
        private Color _accentColor = Color.cyan;

        public Vector3 AnchorPosition => _anchorPosition;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _canvases = GetComponentsInChildren<Canvas>(true);
        }

        public void Configure(Vector3 anchorPosition, string displayName, Color accentColor)
        {
            _anchorPosition = anchorPosition;
            _accentColor = accentColor.a > 0f ? accentColor : Color.cyan;
            ApplyAccentColor();
        }

        private void Update()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            float bob = Mathf.Sin(Time.unscaledTime * bobFrequency) * bobAmplitude;
            transform.position = _anchorPosition + Vector3.up * (verticalOffset + bob);

            if (targetCamera != null)
            {
                Vector3 toCamera = transform.position - targetCamera.transform.position;
                if (toCamera.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }

        private void ApplyAccentColor()
        {
            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null)
                    continue;

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", _accentColor);
                block.SetColor("_Color", _accentColor);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}