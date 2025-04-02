using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handle audio sources without touching them with preset data using this class
public class SoundPresetHandler : MonoBehaviour
{
    private AudioSource audioSource;
    public AudioSource AudioSource => audioSource;
    public SoundPreset soundPreset;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            Debug.LogError("AudioSource component is missing on the GameObject.");
            return;
        }

        // Subscribe to global volume updates
        SoundMasterController.Instance.OnVolumeChanged += ApplyGlobalVolume;

        ApplyPreset();
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (SoundMasterController.Instance != null)
            SoundMasterController.Instance.OnVolumeChanged -= ApplyGlobalVolume;
    }

    public SoundPreset ApplyPreset()
    {
        RemoveFilters();
        if (soundPreset != null)
        {
            audioSource.clip = soundPreset.audioClip;
            audioSource.volume = soundPreset.volume;
            audioSource.pitch = soundPreset.pitch;
            audioSource.loop = soundPreset.loop;
            audioSource.spatialBlend = soundPreset.spatialBlend;
            audioSource.rolloffMode = soundPreset.rolloffMode;
            audioSource.minDistance = soundPreset.minDistance;
            audioSource.maxDistance = soundPreset.maxDistance;

            // Apply initial global volume
            ApplyGlobalVolume();

            SetupFilters();
            
            return soundPreset;
        }
        else
        {
            //Debug.LogWarning($"{name}'s sound preset is null, can't apply preset");
            return null;
        }
    }

    private void ApplyGlobalVolume()
    {
        if (soundPreset == null || SoundMasterController.Instance == null) return;
        audioSource.volume = soundPreset.volume * SoundMasterController.Instance.masterVolume;
    }

    public SoundPresetHandler(AudioSource newSource, SoundPreset newPreset)
    {
        audioSource = newSource;
        soundPreset = newPreset;
        ApplyPreset();
    }

    public SoundPreset SetNewPreset(SoundPreset newPreset)
    {
        soundPreset = newPreset;
        return ApplyPreset();
    }

    /*private IEnumerator SwitchFilters(SoundPreset newPreset, bool includeTransition)
    {
        RemoveFilters(audioSource);
        yield return null; // Allow some frame delay if necessary during transitions
        SetupFilters(nextHandler.AudioSource, newPreset);
        nextHandler.SetNewPreset(newPreset);
    }*/

    private void RemoveFilters()
    {
        if(audioSource != null)
            SoundPreset.RemoveAllFilters(audioSource);
    }

    private void SetupFilters()
    {
        if(audioSource != null)
            soundPreset.ApplyFilters(audioSource);
    }
}
