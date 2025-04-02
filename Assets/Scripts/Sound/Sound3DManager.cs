using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Control 3D sound sources with this class (played at some 3D point in game space)
public class Sound3DManager : MonoBehaviour
{
    public SoundPresetHandler presetHandler; // Handles the preset logic
    public Transform player;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = presetHandler.GetComponent<AudioSource>();

        // Subscribe to global volume updates
        SoundMasterController.Instance.OnVolumeChanged += UpdateVolume;

        // Initialize the volume
        UpdateVolume();
    }

    private void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        if (SoundMasterController.Instance != null)
        {
            SoundMasterController.Instance.OnVolumeChanged -= UpdateVolume;
        }
    }

    private void UpdateVolume()
    {
        if (player == null || audioSource == null) return;

        // Calculate the distance-based volume
        float distance = Vector3.Distance(transform.position, player.position);
        float distanceVolume = Mathf.Clamp01(1f / (distance * distance));

        // Apply the global SFX volume
        audioSource.volume = distanceVolume * SoundMasterController.Instance.sfxVolume;
    }
}
