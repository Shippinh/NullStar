using UnityEngine;
using System;

// Controll sound volume globally
public class SoundMasterController : MonoBehaviour
{
    public static SoundMasterController Instance;

    [Header("Volume Controls")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // Events for volume updates
    public event Action OnVolumeChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else
        {
            Destroy(gameObject); // Prevent duplicates
        }

        // Trigger initial volume setup
        NotifyVolumeChanged();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        NotifyVolumeChanged();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        NotifyVolumeChanged();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        NotifyVolumeChanged();
    }

    public void MuteAll()
    {
        SetMasterVolume(0f);
    }

    public void UnmuteAll()
    {
        SetMasterVolume(1f);
    }

    private void NotifyVolumeChanged()
    {
        OnVolumeChanged?.Invoke(); // Notify listeners when volume changes
    }
}
