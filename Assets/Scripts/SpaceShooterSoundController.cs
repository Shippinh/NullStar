using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public AudioSource blowerSoundLoop;
    public AudioSource airIntakeSoundLoop;
    public AudioSource hardBurnSoundLoop;
    [Range(0f, 1f)] public float hardBurnFadeDuration;
    public AudioSource overboostSwitchSound;
    public AudioSource overboostEngineImpactSound;
    public AudioSource overboostAccelerationSound;
    [Range(0f, 1f)] public float overboostAccelerationFadeDuration;

    AudioFadeData hardBurnFade;
    AudioFadeData overboostAccelerationFade;

    void Awake()
    {
        playerController.OnOverboostActivation += HandleOverboostActivation;
        playerController.OnOverboostStop += HandleOverboostStop;
        playerController.OnOverboostInitiation += HandleOverboostInitiation;
        playerController.OnOverboostInitiationCancel += HandleOverboostInitiationCancel;

        hardBurnFade = new AudioFadeData(hardBurnSoundLoop);
        overboostAccelerationFade = new AudioFadeData(overboostAccelerationSound);

    }

    void Update()
    {
        hardBurnFade.Update();
        overboostAccelerationFade.Update();
    }


    void HandleOverboostActivation()
    {
        Debug.Log("Overboost activated");
        overboostEngineImpactSound.Play();
        overboostAccelerationSound.Play();
        hardBurnSoundLoop.Play();
        hardBurnSoundLoop.Play();
    }

    void HandleOverboostStop()
    {
        Debug.Log("Overboost concluded");
        overboostAccelerationFade.FadeVolume(0f, overboostAccelerationFadeDuration);
        hardBurnFade.FadeVolume(0f, hardBurnFadeDuration);
    }

    void HandleOverboostInitiationCancel()
    {
        Debug.Log("Overboost initiation cancelled");
    }

    void HandleOverboostInitiation()
    {
        Debug.Log("Initiated overboost");
        overboostSwitchSound.Play();
    }
}

public class AudioFadeData
{
    public AudioSource source;
    public float targetVolume;
    public float targetPitch;
    public float defaultVolume;
    public float defaultPitch;
    public float fadeDuration;
    public float fadeElapsed;
    public bool isFadingVolume;
    public bool isFadingPitch;

    public AudioFadeData(AudioSource source)
    {
        this.source = source;
        this.targetVolume = source.volume;
        this.targetPitch = source.pitch;
        this.defaultVolume = source.volume;
        this.defaultPitch = source.pitch;
        this.fadeDuration = 0f;
        this.fadeElapsed = 0f;
        this.isFadingVolume = false;
        this.isFadingPitch = false;
    }

    // Combined Fade method (can be used for both Fade In and Fade Out)
    public void FadeVolume(float target, float duration)
    {
        if (!source.isPlaying) return;

        targetVolume = target;
        fadeDuration = Mathf.Max(duration, 0.0001f);
        fadeElapsed = 0f;
        isFadingVolume = true;
    }

    public void FadePitch(float target, float duration)
    {
        if (!source.isPlaying) return;

        targetPitch = target;
        fadeDuration = Mathf.Max(duration, 0.0001f);
        fadeElapsed = 0f;
        isFadingPitch = true;
    }

    public void Update()
    {
        if (source == null || (!isFadingVolume && !isFadingPitch) || !source.isPlaying)
            return;

        fadeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(fadeElapsed / fadeDuration);

        // Fade volume
        if (isFadingVolume)
        {
            source.volume = Mathf.Lerp(source.volume, targetVolume, t);
            if (t >= 1f)
                isFadingVolume = false;
        }

        // Fade pitch
        if (isFadingPitch)
        {
            source.pitch = Mathf.Lerp(source.pitch, targetPitch, t);
            if (t >= 1f)
                isFadingPitch = false;
        }

        // If both fades are complete, reset and stop
        if (!isFadingVolume && !isFadingPitch)
        {
            source.Stop();
            source.volume = defaultVolume; // Ensure we restore the volume to default
            source.pitch = defaultPitch;  // Ensure we restore the pitch to default
        }
    }
}
