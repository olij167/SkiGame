using UnityEngine;

[DisallowMultipleComponent]
public class SkiResortZone : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Resort Identity")]
    [SerializeField] private SkiResortConfigSO resortConfig;
    [SerializeField] private string resortId = string.Empty;

    [Header("Resort Points")]
    [Tooltip("Where the player auto-walks to when entering.")]
    public Transform entrancePoint;

    [Tooltip("Optional: where the player is placed/held while in resort.")]
    public Transform insidePoint;

    [Tooltip("Where the player auto-walks to when exiting.")]
    public Transform exitPoint;

    [Tooltip("Where blackout/startup returns should place the player. Falls back to exit/inside/entrance.")]
    [SerializeField] private Transform respawnPoint;

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

    public SkiResortConfigSO ResortConfig => resortConfig;

    public string ResortId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(resortId))
                return resortId.Trim();

            return string.Empty;
        }
    }

    public Transform RespawnPoint
    {
        get
        {
            if (respawnPoint != null)
                return respawnPoint;
            if (exitPoint != null)
                return exitPoint;
            if (insidePoint != null)
                return insidePoint;
            if (entrancePoint != null)
                return entrancePoint;
            return transform;
        }
    }

    public bool MatchesResortId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               string.Equals(ResortId, id.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private void OnTriggerEnter(Collider other)
    {
        NpcSkierBrain npc = ResolveNpcBrain(other);
        if (npc != null)
        {
            npc.DespawnToPool("EnteredResort");
            return;
        }

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

    private static NpcSkierBrain ResolveNpcBrain(Collider other)
    {
        if (other == null)
            return null;

        return other.GetComponentInParent<NpcSkierBrain>();
    }
}
