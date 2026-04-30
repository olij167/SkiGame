using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAppearanceBootstrap : MonoBehaviour
{
    [SerializeField] private NpcSkierAppearanceGenerator appearanceGenerator;
    [SerializeField] private NpcSkierProfile profile;
    [SerializeField] private bool runOnStart = true;

    private IEnumerator Start()
    {
        if (!runOnStart)
            yield break;

        yield return null; // let refs settle
        Apply();
    }

    public void Apply()
    {
        if (TryGetComponent(out NpcIdentity identity) && identity.IsAuthored && identity.PreserveAuthoredAppearance)
            return;

        if (appearanceGenerator != null)
            appearanceGenerator.ApplyRandomAppearance(profile);
    }
}
