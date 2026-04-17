using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomizationShopEntryPoint : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string customizationSceneName = "CharacterCustomization";

    [Header("Arrival")]
    [SerializeField] private float arriveRadius = 1.2f;
    [SerializeField] private bool requireTriggerEnter = true;

    private GameObject _armedPlayer;
    private bool _busy;

    public Vector3 ApproachPosition => transform.position;

    public void ArmForPlayer(GameObject playerRoot)
    {
        _armedPlayer = playerRoot;
    }

    public void DisarmPlayer(GameObject playerRoot)
    {
        if (_armedPlayer == playerRoot)
            _armedPlayer = null;
    }

    public bool IsArmedFor(GameObject playerRoot)
    {
        return _armedPlayer != null && _armedPlayer == playerRoot;
    }

    public bool HasReached(GameObject playerRoot)
    {
        if (playerRoot == null)
            return false;

        Vector3 a = playerRoot.transform.position;
        Vector3 b = transform.position;
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= arriveRadius;
    }

    public IEnumerator LoadForPlayer(GameObject playerRoot)
    {
        if (_busy || playerRoot == null)
            yield break;

        _busy = true;

        if (!playerRoot.CompareTag("NPC"))
            CustomizationShopRuntime.SetPendingPlayerRoot(playerRoot);

        var scene = SceneManager.GetSceneByName(customizationSceneName);
        if (!(scene.IsValid() && scene.isLoaded))
        {
            var op = SceneManager.LoadSceneAsync(customizationSceneName, LoadSceneMode.Additive);
            while (op != null && !op.isDone)
                yield return null;
        }

        _busy = false;
        _armedPlayer = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        NpcSkierBrain npc = ResolveNpcBrain(other);
        if (npc != null)
        {
            npc.DespawnToPool("EnteredShopEntry");
            return;
        }

        if (!requireTriggerEnter || _busy || _armedPlayer == null)
            return;

        GameObject root = ResolvePlayerRoot(other);
        if (root == null || root != _armedPlayer)
            return;

        StartCoroutine(LoadForPlayer(root));
    }

    private GameObject ResolvePlayerRoot(Collider other)
    {
        if (other == null)
            return null;

        Transform t = other.transform;
        while (t != null)
        {
            if (t.CompareTag("Player"))
                return t.gameObject;
            if (t.CompareTag("NPC"))
                return null;
            t = t.parent;
        }

        return null;
    }

    private static NpcSkierBrain ResolveNpcBrain(Collider other)
    {
        if (other == null)
            return null;

        return other.GetComponentInParent<NpcSkierBrain>();
    }
}
