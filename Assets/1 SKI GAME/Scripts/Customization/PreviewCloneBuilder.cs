using UnityEngine;

public class PreviewCloneBuilder : MonoBehaviour
{
    [Header("Clone Settings")]
    [Tooltip("If assigned, this layer will be applied to the clone + all children.")]
    [SerializeField] private int previewLayer = -1;

    [Tooltip("If true, removes Colliders/Rigidbody/SkiController from the clone.")]
    [SerializeField] private bool stripGameplayComponents = true;

    public GameObject BuildPreviewClone(GameObject livePlayer, Transform showcasePoint)
    {
        if (livePlayer == null) return null;

        var clone = Instantiate(livePlayer);
        clone.name = livePlayer.name + "_PreviewClone";

        if (showcasePoint != null)
        {
            clone.transform.position = showcasePoint.position;
            clone.transform.rotation = showcasePoint.rotation;
        }

        if (stripGameplayComponents)
            Strip(clone);

        if (previewLayer >= 0)
            SetLayerRecursively(clone.transform, previewLayer);

        return clone;
    }

    private void Strip(GameObject clone)
    {
        foreach (var c in clone.GetComponentsInChildren<Collider>(true))
            Destroy(c);

        var rb = clone.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        var ski = clone.GetComponent<SkiController>();
        if (ski != null) Destroy(ski);

        // Optional: strip other runtime systems you don’t want in preview
        // e.g., audio listeners, networking components, etc.
        // foreach (var n in clone.GetComponentsInChildren<NetworkObject>(true)) Destroy(n);
    }

    private void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i), layer);
    }
}
