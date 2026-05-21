using UnityEngine;

[CreateAssetMenu(fileName = "SocialPopulationProfile", menuName = "SkiGame/NPC/Social Population Profile")]
public sealed class SocialPopulationProfileSO : ScriptableObject
{
    [SerializeField] private NpcSocialAnchorType anchorType = NpcSocialAnchorType.Generic;
    [SerializeField, Min(0)] private int desiredMinActors = 1;
    [SerializeField, Min(0)] private int desiredMaxActors = 3;
    [SerializeField, Min(0f)] private float densityWeight = 1f;
    [SerializeField, Min(1f)] private float activationRadius = 120f;
    [SerializeField, Min(0f)] private float spawnRadiusMin = 8f;
    [SerializeField, Min(0f)] private float spawnRadiusMax = 24f;
    [SerializeField] private bool allowSpawnClusters = true;
    [SerializeField] private NpcSocialGroupSO[] preferredGroups;

    public NpcSocialAnchorType AnchorType => anchorType;
    public int DesiredMinActors => desiredMinActors;
    public int DesiredMaxActors => Mathf.Max(desiredMinActors, desiredMaxActors);
    public float DensityWeight => densityWeight;
    public float ActivationRadius => activationRadius;
    public float SpawnRadiusMin => spawnRadiusMin;
    public float SpawnRadiusMax => Mathf.Max(spawnRadiusMin, spawnRadiusMax);
    public bool AllowSpawnClusters => allowSpawnClusters;
    public NpcSocialGroupSO[] PreferredGroups => preferredGroups;

    private void OnValidate()
    {
        desiredMinActors = Mathf.Max(0, desiredMinActors);
        desiredMaxActors = Mathf.Max(desiredMinActors, desiredMaxActors);
        spawnRadiusMax = Mathf.Max(spawnRadiusMin, spawnRadiusMax);
    }
}
