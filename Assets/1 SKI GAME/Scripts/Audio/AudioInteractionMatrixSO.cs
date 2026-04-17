using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct AudioCategoryFallbackRule
{
    public AudioMaterialCategory categoryA;
    public AudioMaterialCategory categoryB;
    public AudioInteractionProfileSO profile;

    public bool Matches(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
    {
        if (a == null || b == null)
            return false;

        return (a.Category == categoryA && b.Category == categoryB) ||
               (a.Category == categoryB && b.Category == categoryA);
    }
}

[CreateAssetMenu(
    fileName = "AudioInteractionMatrix",
    menuName = "Ski Game/Audio/Interaction Matrix")]
public sealed class AudioInteractionMatrixSO : ScriptableObject
{
    [SerializeField] private List<AudioInteractionProfileSO> exactProfiles = new List<AudioInteractionProfileSO>();
    [SerializeField] private List<AudioCategoryFallbackRule> categoryFallbacks = new List<AudioCategoryFallbackRule>();
    [SerializeField] private AudioInteractionProfileSO defaultSoftProfile;
    [SerializeField] private AudioInteractionProfileSO defaultHardProfile;
    [SerializeField] private AudioInteractionProfileSO defaultScrapeProfile;

    public IReadOnlyList<AudioInteractionProfileSO> ExactProfiles => exactProfiles;
    public IReadOnlyList<AudioCategoryFallbackRule> CategoryFallbacks => categoryFallbacks;

    public AudioInteractionProfileSO ResolveProfile(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b, ContactAudioEventType type)
    {
        AudioInteractionProfileSO profile = FindExact(a, b);
        if (profile != null)
            return profile;

        profile = FindCategoryFallback(a, b);
        if (profile != null)
            return profile;

        return GetDefaultFallback(a, b, type);
    }

    public AudioInteractionProfileSO FindExact(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
    {
        for (int i = 0; i < exactProfiles.Count; i++)
        {
            AudioInteractionProfileSO profile = exactProfiles[i];
            if (profile != null && profile.MatchesPair(a, b))
                return profile;
        }

        return null;
    }

    public AudioInteractionProfileSO FindCategoryFallback(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b)
    {
        for (int i = 0; i < categoryFallbacks.Count; i++)
        {
            AudioCategoryFallbackRule rule = categoryFallbacks[i];
            if (rule.profile != null && rule.Matches(a, b))
                return rule.profile;
        }

        return null;
    }

    public AudioInteractionProfileSO GetDefaultFallback(AudioSurfaceMaterialSO a, AudioSurfaceMaterialSO b, ContactAudioEventType type)
    {
        bool prefersScrape = type == ContactAudioEventType.Scrape ||
                             type == ContactAudioEventType.Drag ||
                             type == ContactAudioEventType.Slide;
        if (prefersScrape && defaultScrapeProfile != null)
            return defaultScrapeProfile;

        bool softPair = (a != null && a.IsSoftOrPowdery()) || (b != null && b.IsSoftOrPowdery());
        if (softPair)
            return defaultSoftProfile != null ? defaultSoftProfile : defaultHardProfile;

        return defaultHardProfile != null ? defaultHardProfile : defaultSoftProfile;
    }
}
