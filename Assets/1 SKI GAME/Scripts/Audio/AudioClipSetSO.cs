using System;
using UnityEngine;

[Serializable]
public struct AudioClipEntry
{
    public AudioClip clip;
    [Min(0f)] public float weight;
}

[CreateAssetMenu(
    fileName = "AudioClipSet",
    menuName = "Ski Game/Audio/Clip Set")]
public sealed class AudioClipSetSO : ScriptableObject
{
    [SerializeField] private AudioClipEntry[] clips = Array.Empty<AudioClipEntry>();
    [SerializeField] private Vector2 volumeRange = new Vector2(0.95f, 1f);
    [SerializeField] private Vector2 pitchRange = new Vector2(0.97f, 1.03f);
    [SerializeField, Min(0f)] private float cooldownSeconds;
    [SerializeField] private bool avoidImmediateRepeat = true;
    [SerializeField] private bool sequential;

    private float _nextAllowedTime;
    private int _lastClipIndex = -1;
    private int _lastSequentialIndex = -1;

    public AudioClipEntry[] Clips => clips;
    public Vector2 VolumeRange => volumeRange;
    public Vector2 PitchRange => pitchRange;
    public float CooldownSeconds => cooldownSeconds;
    public bool AvoidImmediateRepeat => avoidImmediateRepeat;
    public bool Sequential => sequential;

    public bool CanPlay(float timeNow)
    {
        return timeNow >= _nextAllowedTime && HasPlayableClip();
    }

    public int GetNextClip(System.Random rng, float timeNow)
    {
        if (!CanPlay(timeNow))
            return -1;

        return sequential
            ? GetNextSequentialIndex()
            : GetNextWeightedIndex(rng ?? new System.Random());
    }

    public void NotifyPlayed(int clipIndex, float timeNow)
    {
        if (clipIndex < 0 || clipIndex >= clips.Length)
            return;

        _lastClipIndex = clipIndex;
        if (sequential)
            _lastSequentialIndex = clipIndex;

        _nextAllowedTime = timeNow + Mathf.Max(0f, cooldownSeconds);
    }

    private bool HasPlayableClip()
    {
        if (clips == null || clips.Length == 0)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip != null)
                return true;
        }

        return false;
    }

    private int GetNextSequentialIndex()
    {
        if (clips == null || clips.Length == 0)
            return -1;

        int start = Mathf.Clamp(_lastSequentialIndex + 1, 0, clips.Length);
        for (int offset = 0; offset < clips.Length; offset++)
        {
            int index = (start + offset) % clips.Length;
            if (clips[index].clip == null)
                continue;

            if (avoidImmediateRepeat && clips.Length > 1 && index == _lastClipIndex)
                continue;

            return index;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip != null)
                return i;
        }

        return -1;
    }

    private int GetNextWeightedIndex(System.Random rng)
    {
        float totalWeight = 0f;
        int playableCount = 0;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip == null)
                continue;

            if (avoidImmediateRepeat && clips.Length > 1 && i == _lastClipIndex)
                continue;

            totalWeight += Mathf.Max(0.0001f, clips[i].weight <= 0f ? 1f : clips[i].weight);
            playableCount++;
        }

        if (playableCount <= 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null)
                    return i;
            }

            return -1;
        }

        double pick = rng.NextDouble() * totalWeight;
        double running = 0d;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].clip == null)
                continue;

            if (avoidImmediateRepeat && clips.Length > 1 && i == _lastClipIndex)
                continue;

            running += Mathf.Max(0.0001f, clips[i].weight <= 0f ? 1f : clips[i].weight);
            if (pick <= running)
                return i;
        }

        return -1;
    }
}
