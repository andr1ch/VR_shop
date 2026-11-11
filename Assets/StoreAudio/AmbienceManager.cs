using UnityEngine;

public sealed class AmbienceManager : MonoBehaviour
{
    [SerializeField] private AudioClip[] ambienceClips;
    [SerializeField] private AudioSource backgroundSource;
    [SerializeField] private AudioSource ventilationSource;
    [SerializeField] private AudioSource[] refrigeratorSources;
    [SerializeField] private Transform listener;
    [SerializeField, Range(0f, 1f)] private float backgroundVolume = 0.32f;
    [SerializeField, Range(0f, 1f)] private float ventilationVolume = 0.14f;
    [SerializeField, Range(0f, 1f)] private float refrigeratorVolume = 0.22f;
    [SerializeField, Min(0.1f)] private float refrigeratorFadeDistance = 6f;
    [SerializeField, Min(0.1f)] private float transitionDuration = 2f;

    private int selectedAmbience = -1;

    private void Start()
    {
        if (ambienceClips == null || ambienceClips.Length < 3 ||
            backgroundSource == null || ventilationSource == null || refrigeratorSources == null)
        {
            Debug.LogError("[StoreAudio] AmbienceManager needs three clips and its scene AudioSources.", this);
            enabled = false;
            return;
        }

        Configure(backgroundSource, ambienceClips[0], 0f, 1f, 10f);
        Configure(ventilationSource, ambienceClips[1], 1f, 2f, 18f);
        foreach (AudioSource source in refrigeratorSources)
            if (source != null) Configure(source, ambienceClips[2], 1f, 3f, 8f);

        backgroundSource.volume = 0f;
        ventilationSource.volume = 0f;
        backgroundSource.Play();
        ventilationSource.Play();
        for (int i = 0; i < refrigeratorSources.Length; i++)
        {
            AudioSource source = refrigeratorSources[i];
            if (source == null) continue;
            source.volume = 0f;
            if (source.clip.length > 0f)
                source.time = (i * 0.67f) % source.clip.length;
            source.Play();
        }
    }

    private static void Configure(AudioSource source, AudioClip clip, float spatialBlend, float minDistance, float maxDistance)
    {
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
    }

    private void Update()
    {
        float step = Time.deltaTime / transitionDuration;
        float backgroundTarget = selectedAmbience < 0 || selectedAmbience == 0 ? backgroundVolume : backgroundVolume * 0.3f;
        float ventilationTarget = selectedAmbience < 0 || selectedAmbience == 1 ? ventilationVolume : 0f;
        backgroundSource.volume = Mathf.MoveTowards(backgroundSource.volume, backgroundTarget, step);
        ventilationSource.volume = Mathf.MoveTowards(ventilationSource.volume, ventilationTarget, step);

        float totalWeight = 0f;
        float totalEmphasis = 0f;
        foreach (AudioSource source in refrigeratorSources)
        {
            if (source == null) continue;
            float weight = RefrigeratorWeight(source);
            totalWeight += weight;
            totalEmphasis += weight * weight * weight * weight;
        }

        foreach (AudioSource source in refrigeratorSources)
        {
            if (source == null) continue;
            float weight = RefrigeratorWeight(source);
            float emphasis = weight * weight * weight * weight;
            float target = (selectedAmbience < 0 || selectedAmbience == 2)
                ? refrigeratorVolume * Mathf.Min(1f, totalWeight) * emphasis / Mathf.Max(0.0001f, totalEmphasis)
                : 0f;
            source.volume = Mathf.MoveTowards(source.volume, target, step);
        }
    }

    private float RefrigeratorWeight(AudioSource source)
    {
        if (listener == null) return 1f;
        float distance = Vector3.Distance(listener.position, source.transform.position);
        return 1f - Mathf.Clamp01(distance / refrigeratorFadeDistance);
    }

    public void SwitchAmbience(int clipIndex)
    {
        if (ambienceClips == null || clipIndex < 0 || clipIndex >= ambienceClips.Length)
        {
            Debug.LogWarning("[StoreAudio] Invalid ambience index: " + clipIndex, this);
            return;
        }
        selectedAmbience = clipIndex;
    }

    public void SwitchAmbience(string clipName)
    {
        if (ambienceClips == null) return;
        for (int i = 0; i < ambienceClips.Length; i++)
            if (ambienceClips[i] != null && ambienceClips[i].name == clipName)
            {
                SwitchAmbience(i);
                return;
            }
        Debug.LogWarning("[StoreAudio] Ambience not found: " + clipName, this);
    }

    public void ResumeAutomaticAmbience() => selectedAmbience = -1;
}
