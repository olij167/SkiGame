using UnityEngine;

[DisallowMultipleComponent]
public class SkiResortZone : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Resort Points")]
    [Tooltip("Where the player auto-walks to when entering.")]
    public Transform entrancePoint;

    [Tooltip("Optional: where the player is placed/held while in resort.")]
    public Transform insidePoint;

    [Tooltip("Where the player auto-walks to when exiting.")]
    public Transform exitPoint;

    private bool _playerInside;
    private GameObject _playerRoot;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public bool IsPlayerInZone(GameObject playerRoot)
    {
        return _playerInside && _playerRoot == playerRoot;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolvePlayerRoot(other, out var root))
            return;

        _playerInside = true;
        _playerRoot = root;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryResolvePlayerRoot(other, out var root))
            return;

        if (_playerRoot == root)
        {
            _playerInside = false;
            _playerRoot = null;
        }
    }

    private bool TryResolvePlayerRoot(Collider other, out GameObject playerRoot)
    {
        playerRoot = null;
        if (other == null)
            return false;

        var t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("NPC"))
                return false;
            t = t.parent;
        }

        t = other.transform;
        while (t != null)
        {
            if (!string.IsNullOrEmpty(requiredTag))
            {
                if (t.CompareTag(requiredTag))
                {
                    playerRoot = t.gameObject;
                    return true;
                }
            }
            else if (t.CompareTag("Player"))
            {
                playerRoot = t.gameObject;
                return true;
            }

            t = t.parent;
        }

        return false;
    }
}
