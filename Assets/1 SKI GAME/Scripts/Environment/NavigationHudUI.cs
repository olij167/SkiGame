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
        [SerializeField] private float edgePadding = 28f;
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

            if (!NavigationTargetController.TryGetActiveState(out var state) || !state.request.showHud || state.request.preferMiniMapIndicator)
            {
                SetVisible(false);
                return;
            }

            if (targetCamera == null)
            {
                SetVisible(false);
                return;
            }

            float rootWidth = Mathf.Max(1f, _root.resolvedStyle.width);
            float rootHeight = Mathf.Max(1f, _root.resolvedStyle.height);
            if (rootWidth <= 1f || rootHeight <= 1f)
            {
                SetVisible(false);
                return;
            }

            float indicatorWidth = Mathf.Max(1f, _indicator.resolvedStyle.width);
            float indicatorHeight = Mathf.Max(1f, _indicator.resolvedStyle.height);

            float halfIndicatorWidth = indicatorWidth * 0.5f;
            float halfIndicatorHeight = indicatorHeight * 0.5f;

            float minX = edgePadding + halfIndicatorWidth;
            float maxX = rootWidth - edgePadding - halfIndicatorWidth;
            float minY = edgePadding + halfIndicatorHeight;
            float maxY = rootHeight - edgePadding - halfIndicatorHeight;

            Vector3 beaconWorld = state.beaconWorldPosition + Vector3.up * beaconWorldVerticalOffset;
            Vector3 screen = targetCamera.WorldToScreenPoint(beaconWorld);

            // Convert camera screen space (origin bottom-left) to UI Toolkit space (origin top-left).
            Vector2 uiPoint = new Vector2(screen.x, rootHeight - screen.y);
            Vector2 uiCenter = new Vector2(rootWidth * 0.5f, rootHeight * 0.5f);

            bool isInFront = screen.z > 0f;
            bool insideScreen =
                uiPoint.x >= 0f && uiPoint.x <= rootWidth &&
                uiPoint.y >= 0f && uiPoint.y <= rootHeight;

            // Hide as soon as the beacon is actually visible on-screen.
            if (isInFront && insideScreen)
            {
                SetVisible(false);
                return;
            }

            Vector2 targetIndicatorCenter;
            Vector2 arrowDirection;

            if (isInFront)
            {
                // Front-facing but off-screen:
                // place the indicator on the closest point of the padded screen rect.
                targetIndicatorCenter = new Vector2(
                    Mathf.Clamp(uiPoint.x, minX, maxX),
                    Mathf.Clamp(uiPoint.y, minY, maxY));

                arrowDirection = (targetIndicatorCenter - uiCenter);
                if (arrowDirection.sqrMagnitude < 0.0001f)
                    arrowDirection = Vector2.up;
            }
            else
            {
                // Behind camera:
                // mirror direction around center and intersect with padded rect.
                Vector2 mirrored = uiCenter - (uiPoint - uiCenter);
                Vector2 dirFromCenter = mirrored - uiCenter;

                if (dirFromCenter.sqrMagnitude < 0.0001f)
                    dirFromCenter = Vector2.up;

                arrowDirection = dirFromCenter.normalized;
                targetIndicatorCenter = GetRayRectIntersection(uiCenter, arrowDirection, minX, maxX, minY, maxY);
            }

            SetVisible(true);

            _indicator.style.left = targetIndicatorCenter.x - halfIndicatorWidth;
            _indicator.style.top = targetIndicatorCenter.y - halfIndicatorHeight;

            _title.text = state.request.displayName;
            _distance.text = $"{GetDisplayDistance(state.beaconWorldPosition):0} m";
            _arrow.text = GetArrowGlyph(arrowDirection.normalized);

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

        private static Vector2 GetRayRectIntersection(Vector2 center, Vector2 dir, float minX, float maxX, float minY, float maxY)
        {
            float tx = float.MaxValue;
            float ty = float.MaxValue;

            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                float targetX = dir.x > 0f ? maxX : minX;
                tx = (targetX - center.x) / dir.x;
            }

            if (Mathf.Abs(dir.y) > 0.0001f)
            {
                float targetY = dir.y > 0f ? maxY : minY;
                ty = (targetY - center.y) / dir.y;
            }

            float t = Mathf.Min(tx, ty);
            if (float.IsInfinity(t) || float.IsNaN(t) || t < 0f)
                t = 0f;

            Vector2 hit = center + dir * t;
            hit.x = Mathf.Clamp(hit.x, minX, maxX);
            hit.y = Mathf.Clamp(hit.y, minY, maxY);
            return hit;
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