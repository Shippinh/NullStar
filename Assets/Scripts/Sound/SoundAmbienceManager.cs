using System.Collections;
using UnityEngine;
using System;

// Controll ambient tracks with this class (played in ears directly)
public class SoundAmbienceManager : MonoBehaviour
{
    [Header("Default Settings")]
    public SoundPresetHandler currentHandler;
    public SoundPresetHandler nextHandler;

    private Coroutine currentHandlerCoroutine;
    private Coroutine nextHandlerCoroutine;

    public float fadeDuration = 2f;
    public bool useAttachedHandler = true;
    public bool playOnStart = false;

    private Coroutine pitchVariationCoroutine;

    private void StopPitchVariation()
    {
        if (pitchVariationCoroutine != null)
        {
            StopCoroutine(pitchVariationCoroutine);
            pitchVariationCoroutine = null;
        }
    }

    public void PlayCurrentHandlerSound()
    {
        if(currentHandler != null)
        {
            currentHandler.AudioSource.Play();
        }
    }

    public void GraduallyIncreasePitch(float increaseDuration)
    {
        var defaultPitchValue = currentHandler.soundPreset.pitch;
        var timer = 0f;
        while(timer < increaseDuration)
        {
            timer += Time.deltaTime;
            currentHandler.soundPreset.pitch += Time.deltaTime/2;
        }
    }

    private void StartPitchVariation()
    {
        // Check if the current handler or its sound preset is null
        if (currentHandler == null || currentHandler.soundPreset == null)
        {
            Debug.LogWarning($"StartPitchVariation: Current handler or its sound preset is null. Skipping pitch variation.");
            return;
        }

        if (currentHandler.soundPreset.isVariablePitch)
        {
            StopPitchVariation();
            pitchVariationCoroutine = StartCoroutine(VariatePitchCoroutine(currentHandler.soundPreset));
        }
    }

    private IEnumerator VariatePitchCoroutine(SoundPreset preset)
    {
        if (preset == null)
        {
            Debug.LogWarning("VariatePitchCoroutine: SoundPreset is null. Exiting coroutine.");
            yield break;
        }

        AudioSource audioSource = currentHandler?.AudioSource;
        if (audioSource == null)
        {
            Debug.LogWarning("VariatePitchCoroutine: AudioSource is null. Exiting coroutine.");
            yield break;
        }

        float originalPitch = preset.pitch;

        while (true)
        {
            float frequency = preset.variationFrequency;
            if (preset.randomiseFrequency)
            {
                frequency = UnityEngine.Random.Range(0.19f, preset.variationFrequency);
            }

            yield return new WaitForSeconds(frequency);

            audioSource.pitch = originalPitch + UnityEngine.Random.Range(-preset.variationRange, preset.variationRange);
        }
    }

    void Start()
    {
        // Subscribe to global volume updates
        if (SoundMasterController.Instance != null)
        {
            SoundMasterController.Instance.OnVolumeChanged += ApplyGlobalVolume;
        }

        if(useAttachedHandler)
        {
            currentHandler = GetComponent<SoundPresetHandler>();

            if(currentHandler == null)
                Debug.LogWarning($"{name}'s current handler is null, no track will be played");
        }

        ApplyGlobalVolume();

        if(currentHandler != null)
        {
            if(currentHandler.soundPreset != null)
            {
                if(currentHandler.soundPreset.isVariablePitch)
                {
                    StartPitchVariation();
                }
            }
            if(playOnStart)
                currentHandler.AudioSource.Play();
        }

    }

    private void OnDestroy()
    {
        if (SoundMasterController.Instance != null)
        {
            SoundMasterController.Instance.OnVolumeChanged -= ApplyGlobalVolume;
        }

        // Unsubscribe from local event to prevent memory leaks
        StopPitchVariation();
    }

    public void ChangeTrack(SoundPreset newPreset)
    {
        if (currentHandlerCoroutine != null)
        {
            StopCoroutine(currentHandlerCoroutine);
            currentHandlerCoroutine = null;
        }

        if (nextHandlerCoroutine != null)
        {
            StopCoroutine(nextHandlerCoroutine);
            nextHandlerCoroutine = null;
        }

        if (currentHandler != null)
        {
            currentHandlerCoroutine = StartCoroutine(FadeOutAndChangeTrack(newPreset));
        }
        else
        {
            currentHandler.SetNewPreset(newPreset);
            StartFadeIn();
        }
    }

    private IEnumerator FadeOutAndChangeTrack(SoundPreset newPreset)
    {
        if (currentHandler == null || currentHandler.soundPreset == null)
        {
            if(currentHandler != null)
            {
                currentHandler.SetNewPreset(newPreset);

                if(currentHandler.soundPreset != null)
                    StartFadeIn();
            }
            //Debug.LogWarning("FadeOutAndChangeTrack: Current handler or its sound preset is null. Skipping fade out.");
            yield break;
        }

        StopPitchVariation(); // Ensure no pitch variation during transition

        float elapsed = 0f;
        AudioSource audioSource = currentHandler.AudioSource;

        // Gradually reduce volume for fade out
        while (elapsed < fadeDuration)
        {
            audioSource.volume = Mathf.Lerp(currentHandler.soundPreset.volume, 0f, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        audioSource.Stop();

        audioSource.volume = 0f;

        currentHandler.SetNewPreset(newPreset);

        if(currentHandler.soundPreset != null)
            StartFadeIn();
    }

    private void StartFadeIn()
    {
        //Debug.Log($"Starting fade in for {name}");
        StartPitchVariation(); // Start variable pitch with fade-in
        nextHandlerCoroutine = StartCoroutine(FadeInCoroutine());
    }

    private IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;
        AudioSource audioSource = currentHandler.AudioSource;

        audioSource.Play();
        while (elapsed < fadeDuration)
        {
            audioSource.volume = Mathf.Lerp(0f, currentHandler.soundPreset.volume, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        audioSource.volume = currentHandler.soundPreset.volume;
    }


    private void ApplyGlobalVolume()
    {
        if (currentHandler != null)
        {
            currentHandler.GetComponent<AudioSource>().volume = SoundMasterController.Instance.musicVolume;
        }
    }
    
}
