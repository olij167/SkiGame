using UnityEngine;
using UnityEngine.UIElements;
using SkiGame.Navigation;

namespace SkiGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class NavigationHudUI : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private int documentSortOrder = 2450;

        [Header("Behaviour")]
        [SerializeField] private float edgePadding = 54f;
        [SerializeField] private float centerHideRadiusPixels = 140f;
        [SerializeField] private float beaconWorldVerticalOffset = 12f;

        private VisualElement _root;
        private VisualElement _indicator;
        private Label _title;
        private Label _distance;
        private Label _arrow;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (document == null || document.rootVisualElement == null)
            {
                enabled = false;
                return;
            }

            document.sortingOrder = documentSortOrder;

            _root = document.rootVisualElement;
            _root.pickingMode = PickingMode.Ignore;
            _root.style.flexGrow = 1f;

            _indicator = _root.Q<VisualElement>("NavigationEdgeIndicator");
            _title = _root.Q<Label>("Lbl_NavTitle");
            _distance = _root.Q<Label>("Lbl_NavDistance");
            _arrow = _root.Q<Label>("NavigationArrow");

            ResolveRuntimeReferences();
            SetVisible(false);
        }

        private void Update()
        {
            ResolveRuntimeReferences();

            if (_root == null || _indicator == null || _title == null || _distance == null || _arrow == null)
            {
                SetVisible(false);
                return;
            }

            if (!NavigationTargetController.TryGetActiveState(out var state) || !state.request.showHud)
            {
                SetVisible(false);
                return;
            }

            if (targetCamera == null)
            {
                SetVisible(false);
                return;
            }

            float width = Mathf.Max(1f, _root.resolvedStyle.width);
            float height = Mathf.Max(1f, _root.resolvedStyle.height);
            if (width <= 1f || height <= 1f)
            {
                SetVisible(false);
                return;
            }

            Vector3 beaconWorld = state.beaconWorldPosition + Vector3.up * beaconWorldVerticalOffset;
            Vector3 screen = targetCamera.WorldToScreenPoint(beaconWorld);

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            Vector2 screenPoint = new Vector2(screen.x, height - screen.y);

            bool isInFront = screen.z > 0f;
            bool insideScreen =
                screen.x >= 0f && screen.x <= width &&
                screen.y >= 0f && screen.y <= height;

            Vector2 fromCenter = screenPoint - center;

            if (!isInFront)
                fromCenter = -fromCenter;

            if (fromCenter.sqrMagnitude < 0.0001f)
                fromCenter = Vector2.up;

            bool lookingAtBeacon = isInFront && insideScreen && fromCenter.magnitude <= centerHideRadiusPixels;
            if (lookingAtBeacon)
            {
                SetVisible(false);
                return;
            }

            Vector2 dir = fromCenter.normalized;
            Vector2 edgePos = GetClampedEdgePosition(center, dir, width, height, edgePadding);

            SetVisible(true);

            _indicator.style.left = edgePos.x - 90f;
            _indicator.style.top = edgePos.y - 30f;

            float displayDistance = GetDisplayDistance(state.beaconWorldPosition);
            _title.text = state.request.displayName;
            _distance.text = $"{displayDistance:0} m";

            _arrow.text = GetArrowGlyph(dir);

            Color arrowColor = state.request.accentColor.a > 0f
                ? state.request.accentColor
                : new Color(0.36f, 0.74f, 1f, 1f);

            _arrow.style.color = new StyleColor(arrowColor);
        }

        private void ResolveRuntimeReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (playerTransform == null)
            {
                var ski = FindObjectOfType<SkiController>();
                if (ski != null)
                {
                    playerTransform = ski.transform;
                    return;
                }

                var walk = FindObjectOfType<WalkingController>();
                if (walk != null)
                    playerTransform = walk.transform;
            }
        }

        private float GetDisplayDistance(Vector3 beaconWorldPosition)
        {
            Vector3 origin = playerTransform != null
                ? playerTransform.position
                : (targetCamera != null ? targetCamera.transform.position : Vector3.zero);

            Vector3 a = origin;
            Vector3 b = beaconWorldPosition;
            a.y = 0f;
            b.y = 0f;

            float planar = Vector3.Distance(a, b);
            if (planar > 0.05f)
                return planar;

            return Vector3.Distance(origin, beaconWorldPosition);
        }

        private static Vector2 GetClampedEdgePosition(Vector2 center, Vector2 dir, float width, float height, float padding)
        {
            float halfW = Mathf.Max(1f, width * 0.5f - padding);
            float halfH = Mathf.Max(1f, height * 0.5f - padding);

            float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
            float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
            float scale = Mathf.Min(scaleX, scaleY);

            return center + dir * scale;
        }

        private static string GetArrowGlyph(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            if (angle >= -22.5f && angle < 22.5f) return "▶";
            if (angle >= 22.5f && angle < 67.5f) return "◥";
            if (angle >= 67.5f && angle < 112.5f) return "▲";
            if (angle >= 112.5f && angle < 157.5f) return "◤";
            if (angle >= 157.5f || angle < -157.5f) return "◀";
            if (angle >= -157.5f && angle < -112.5f) return "◣";
            if (angle >= -112.5f && angle < -67.5f) return "▼";

            return "◢";
        }

        private void SetVisible(bool visible)
        {
            if (_indicator != null)
                _indicator.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}