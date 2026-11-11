using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private Sound[] sounds;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
    private readonly Dictionary<string, Sound> byName = new Dictionary<string, Sound>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[StoreAudio] Duplicate AudioManager disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (sounds == null) return;
        foreach (Sound sound in sounds)
        {
            if (sound == null || string.IsNullOrWhiteSpace(sound.name) || sound.clip == null)
            {
                Debug.LogWarning("[StoreAudio] A sound entry is incomplete.", this);
                continue;
            }
            if (byName.ContainsKey(sound.name))
            {
                Debug.LogWarning("[StoreAudio] Duplicate sound name: " + sound.name, this);
                continue;
            }
            byName.Add(sound.name, sound);
            sound.source = gameObject.AddComponent<AudioSource>();
            Configure(sound.source, sound);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static void Configure(AudioSource source, Sound sound)
    {
        source.clip = sound.clip;
        source.playOnAwake = false;
        source.loop = sound.loop;
        source.pitch = sound.pitch;
        source.priority = sound.priority;
        source.spatialBlend = sound.spatialBlend;
        source.minDistance = Mathf.Max(0.01f, sound.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.01f, sound.maxDistance);
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    private Sound FindSound(string soundName)
    {
        if (string.IsNullOrWhiteSpace(soundName)) return null;
        if (byName.TryGetValue(soundName, out Sound sound)) return sound;
        Debug.LogWarning("[StoreAudio] Sound not found: " + soundName, this);
        return null;
    }

    public void Play(string soundName)
    {
        Sound sound = FindSound(soundName);
        if (sound == null || sound.source == null) return;
        sound.source.pitch = Mathf.Max(0.1f, sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance));
        sound.source.volume = sound.volume * masterVolume;
        sound.source.Play();
    }

    public void PlayAtPosition(string soundName, Vector3 position)
    {
        Sound sound = FindSound(soundName);
        if (sound == null) return;
        GameObject emitter = new GameObject("StoreSound_" + soundName);
        emitter.transform.position = position;
        AudioSource source = emitter.AddComponent<AudioSource>();
        Configure(source, sound);
        source.loop = false;
        source.spatialBlend = 1f;
        source.pitch = Mathf.Max(0.1f, sound.pitch + Random.Range(-sound.pitchVariance, sound.pitchVariance));
        source.volume = sound.volume * masterVolume;
        source.Play();
        Destroy(emitter, sound.clip.length / source.pitch + 0.2f);
    }

    public void Stop(string soundName)
    {
        Sound sound = FindSound(soundName);
        if (sound != null && sound.source != null) sound.source.Stop();
    }

    public bool IsPlaying(string soundName)
    {
        Sound sound = FindSound(soundName);
        return sound != null && sound.source != null && sound.source.isPlaying;
    }

    public void SetVolume(string soundName, float volume)
    {
        Sound sound = FindSound(soundName);
        if (sound == null) return;
        sound.volume = Mathf.Clamp01(volume);
        if (sound.source != null) sound.source.volume = sound.volume * masterVolume;
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        foreach (Sound sound in byName.Values)
            if (sound.source != null) sound.source.volume = sound.volume * masterVolume;
    }
}
