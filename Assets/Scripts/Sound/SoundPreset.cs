using UnityEngine;
using System;

[Flags]
public enum AudioFilterType
{
    None = 0,
    LowPass = 1 << 0,
    HighPass = 1 << 1,
    Reverb = 1 << 2,
    Echo = 1 << 3,
    Distortion = 1 << 4,
    Chorus = 1 << 5
}


// Create custom sound presets with this class
[CreateAssetMenu(fileName = "NewSoundPreset", menuName = "Audio/Sound Preset")]
public class SoundPreset : ScriptableObject
{
    public AudioClip audioClip;
    [Range(0f, 1f)] public float volume = 1f;
    [Space(10)]
    [Range(-3f, 3f)] public float pitch = 1f;
    public bool isVariablePitch = false;
    [Range(0f, 1f)]public float variationRange = 0.1f;
    [Range(0.2f, 1f)]public float variationFrequency = 0.5f;
    public bool randomiseFrequency = false;
    [Space(10)]
    public bool loop = false;
    [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public float minDistance = 1f;
    public float maxDistance = 500f;

    // The selected filter type for this preset
    public AudioFilterType filterTypes;

    // Low-Pass Filter Parameters
    [Header("Low-Pass Filter Settings")]
    public float lowPassCutoffFrequency = 5000f;
    public float lowPassResonanceQ = 1.0f;

    // High-Pass Filter Parameters
    [Header("High-Pass Filter Settings")]
    public float highPassCutoffFrequency = 5000f;
    public float highPassResonanceQ = 1.0f;

    // Reverb Filter Parameters
    [Header("Reverb Filter Settings")]
    public const AudioReverbPreset ReverbPreset = AudioReverbPreset.User;
    public float dryLevel = 0f;
    public float room = 0.0f;
    public float roomHF = 0.0f;
    public float roomLF = 0.0f;
    public float decayTime = 1.0f;
    public float decayHFRatio = 0.5f;
    public float reflectionsLevel = -10000.0f;
    public float reflectionsDelay = 0.0f;
    public float reverbLevel = 0.0f;
    public float reverbDelay = 0.04f;
    public float hfReference = 5000f;
    public float lfReference = 250f;
    public float diffusion = 100.0f;
    public float density = 100.0f;

    // Echo Filter Parameters
    [Header("Echo Filter Settings")]
    public float echoDelay = 500f;
    public float echoDecayRatio = 0.5f;
    public float echoDryMix = 1f;
    public float echoWetMix = 1f;

    // Distortion Filter Parameters
    [Header("Distortion Filter Settings")]
    public float distortionLevel = 0.5f;

    // Chorus Filter Parameters
    [Header("Chorus Filter Settings")]
    public float chorusDryMix = 0.5f;
    public float chorusWetMix1 = 0.5f;
    public float chorusWetMix2 = 0.5f;
    public float chorusWetMix3 = 0.5f;
    public float chorusDelay = 40f;
    public float chorusRate = 0.8f;
    public float chorusDepth = 0.03f;

    // Method to apply the selected filter type
    public void ApplyFilters(AudioSource audioSource)
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource is missing. Cannot apply filters.");
            return;
        }

        if ((filterTypes & AudioFilterType.LowPass) != 0)
            ApplyLowPassFilter(audioSource);
        
        if ((filterTypes & AudioFilterType.HighPass) != 0)
            ApplyHighPassFilter(audioSource);

        if ((filterTypes & AudioFilterType.Reverb) != 0)
            ApplyReverbFilter(audioSource);

        if ((filterTypes & AudioFilterType.Echo) != 0)
            ApplyEchoFilter(audioSource);

        if ((filterTypes & AudioFilterType.Distortion) != 0)
            ApplyDistortionFilter(audioSource);

        if ((filterTypes & AudioFilterType.Chorus) != 0)
            ApplyChorusFilter(audioSource);
    }

    // Method to remove the selected filter types
    public void RemoveFilters(AudioSource audioSource)
    {
        if ((filterTypes & AudioFilterType.LowPass) != 0)
            RemoveLowPassFilter(audioSource);

        if ((filterTypes & AudioFilterType.HighPass) != 0)
            RemoveHighPassFilter(audioSource);

        if ((filterTypes & AudioFilterType.Reverb) != 0)
            RemoveReverbFilter(audioSource);

        if ((filterTypes & AudioFilterType.Echo) != 0)
            RemoveEchoFilter(audioSource);

        if ((filterTypes & AudioFilterType.Distortion) != 0)
            RemoveDistortionFilter(audioSource);

        if ((filterTypes & AudioFilterType.Chorus) != 0)
            RemoveChorusFilter(audioSource);
    }

    public static void RemoveAllFilters(AudioSource audioSource)
    {
        RemoveLowPassFilter(audioSource);
        RemoveHighPassFilter(audioSource);
        RemoveReverbFilter(audioSource);
        RemoveEchoFilter(audioSource);
        RemoveDistortionFilter(audioSource);
        RemoveChorusFilter(audioSource);
    }

    private void ApplyLowPassFilter(AudioSource audioSource)
    {
        var lowPass = audioSource.gameObject.GetComponent<AudioLowPassFilter>();
        if (lowPass == null) // Add only if it doesn't exist
        {
            lowPass = audioSource.gameObject.AddComponent<AudioLowPassFilter>();
        }
        lowPass.cutoffFrequency = lowPassCutoffFrequency;
        lowPass.lowpassResonanceQ = lowPassResonanceQ;
    }

    private void ApplyHighPassFilter(AudioSource audioSource)
    {
        var highPass = audioSource.gameObject.GetComponent<AudioHighPassFilter>();
        if (highPass == null) // Add only if it doesn't exist
        {
            highPass = audioSource.gameObject.AddComponent<AudioHighPassFilter>();
        }
        highPass.cutoffFrequency = highPassCutoffFrequency;
        highPass.highpassResonanceQ = highPassResonanceQ;
    }

    private void ApplyReverbFilter(AudioSource audioSource)
    {
        var reverb = audioSource.gameObject.GetComponent<AudioReverbFilter>();
        if (reverb == null) // Add only if it doesn't exist
        {
            reverb = audioSource.gameObject.AddComponent<AudioReverbFilter>();
        }
        reverb.reverbPreset = ReverbPreset;
        reverb.dryLevel = dryLevel;
        reverb.room = room;
        reverb.roomHF = roomHF;
        reverb.roomLF = roomLF;
        reverb.decayTime = decayTime;
        reverb.decayHFRatio = decayHFRatio;
        reverb.reflectionsLevel = reflectionsLevel;
        reverb.reflectionsDelay = reflectionsDelay;
        reverb.reverbDelay = reverbDelay;
        reverb.hfReference = hfReference;
        reverb.lfReference = lfReference;
        reverb.reverbLevel = reverbLevel;
        reverb.diffusion = diffusion;
        reverb.density = density;
    }

    private void ApplyEchoFilter(AudioSource audioSource)
    {
        var echo = audioSource.gameObject.GetComponent<AudioEchoFilter>();
        if (echo == null) // Add only if it doesn't exist
        {
            echo = audioSource.gameObject.AddComponent<AudioEchoFilter>();
        }
        echo.delay = echoDelay;
        echo.decayRatio = echoDecayRatio;
        echo.dryMix = echoDryMix;
        echo.wetMix = echoWetMix;
    }

    private void ApplyDistortionFilter(AudioSource audioSource)
    {
        var distortion = audioSource.gameObject.GetComponent<AudioDistortionFilter>();
        if (distortion == null) // Add only if it doesn't exist
        {
            distortion = audioSource.gameObject.AddComponent<AudioDistortionFilter>();
        }
        distortion.distortionLevel = distortionLevel;
    }

    private void ApplyChorusFilter(AudioSource audioSource)
    {
        var chorus = audioSource.gameObject.GetComponent<AudioChorusFilter>();
        if (chorus == null) // Add only if it doesn't exist
        {
            chorus = audioSource.gameObject.AddComponent<AudioChorusFilter>();
        }
        chorus.dryMix = chorusDryMix;
        chorus.wetMix1 = chorusWetMix1;
        chorus.wetMix2 = chorusWetMix2;
        chorus.wetMix3 = chorusWetMix3;
        chorus.delay = chorusDelay;
        chorus.rate = chorusRate;
        chorus.depth = chorusDepth;
    }

    private static void RemoveLowPassFilter(AudioSource audioSource)
    {
        var lowPass = audioSource.GetComponent<AudioLowPassFilter>();
        if (lowPass != null) DestroyImmediate(lowPass);
    }

    private static void RemoveHighPassFilter(AudioSource audioSource)
    {
        var highPass = audioSource.GetComponent<AudioHighPassFilter>();
        if (highPass != null) DestroyImmediate(highPass);
    }

    private static void RemoveReverbFilter(AudioSource audioSource)
    {
        var reverb = audioSource.GetComponent<AudioReverbFilter>();
        if (reverb != null) DestroyImmediate(reverb);
    }

    private static void RemoveEchoFilter(AudioSource audioSource)
    {
        var echo = audioSource.GetComponent<AudioEchoFilter>();
        if (echo != null) DestroyImmediate(echo);
    }

    private static void RemoveDistortionFilter(AudioSource audioSource)
    {
        var distortion = audioSource.GetComponent<AudioDistortionFilter>();
        if (distortion != null) DestroyImmediate(distortion);
    }

    private static void RemoveChorusFilter(AudioSource audioSource)
    {
        var chorus = audioSource.GetComponent<AudioChorusFilter>();
        if (chorus != null) DestroyImmediate(chorus);
    }
}

