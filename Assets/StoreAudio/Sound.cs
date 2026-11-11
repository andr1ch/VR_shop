using System;
using UnityEngine;

[Serializable]
public sealed class Sound
{
    public string name;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    [Range(0f, 0.25f)] public float pitchVariance;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Min(0.01f)] public float minDistance = 1f;
    [Min(0.02f)] public float maxDistance = 10f;
    public bool loop;
    [Range(0, 256)] public int priority = 128;

    [NonSerialized] internal AudioSource source;
}
