using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public AudioSource blowerSoundLoop;
    public AudioSource airIntakeSoundLoop;
    public AudioSource hardBurnSoundLoop;
    [Range(0f, 1f)] public float hardBurnFadeOutDuration;
    [Range(0f, 15f)] public float hardBurnFadeInDuration;
    [Range(0f, 3f)] public float hardBurnFadePitch;

    public AudioSource overboostSwitchSound;
    [Range(0f, 1f)] public float overboostSwitchFadeOutDuration;
    [Range(0f, 1f)] public float overboostSwitchFadeInDuration;
    public AudioSource overboostEngineImpactSound;
    public AudioSource overboostCancelSound;
    public AudioSource overboostAccelerationSound;
    [Range(0f, 1f)] public float overboostAccelerationFadeDuration;

    public AudioSource overboostOverheatAlarmSound;
    public AudioSource overboostOverheatDamageSound;

    AudioTransitionController hardBurnFade;
    AudioTransitionController overboostAccelerationFade;
    AudioTransitionController overboostSwitchFade;

    void Awake()
    {
        playerController.OnOverboostActivation += HandleOverboostActivation;
        playerController.OnOverboostStop += HandleOverboostStop;
        playerController.OnOverboostInitiation += HandleOverboostInitiation;
        playerController.OnOverboostInitiationCancel += HandleOverboostInitiationCancel;
        playerController.OnOverboostOverheat += HandleOverboostOverheat;

        hardBurnFade = new AudioTransitionController(hardBurnSoundLoop);
        overboostAccelerationFade = new AudioTransitionController(overboostAccelerationSound);
        overboostSwitchFade = new AudioTransitionController(overboostSwitchSound);
    }

    void Update()
    {
        hardBurnFade.Update();
        overboostAccelerationFade.Update();
        overboostSwitchFade.Update();
    }


    void HandleOverboostActivation()
    {
        Debug.Log("Overboost activated");
        overboostEngineImpactSound.Play();
        overboostAccelerationSound.Play();
        hardBurnSoundLoop.Play();
        hardBurnFade.SetPitchOverTime(hardBurnFadePitch, hardBurnFadeInDuration);
        hardBurnFade.SetVolumeOverTime(1f, hardBurnFadeInDuration);
    }

    void HandleOverboostStop()
    {
        Debug.Log("Overboost concluded");
        overboostAccelerationFade.SetVolumeOverTime(0f, overboostAccelerationFadeDuration);
        
        hardBurnFade.SetVolumeOverTime(0f, hardBurnFadeOutDuration);
        hardBurnFade.SetPitchOverTime(1f, hardBurnFadeOutDuration);
    }

    void HandleOverboostInitiationCancel()
    {
        Debug.Log("Overboost initiation cancelled");
        overboostSwitchFade.SetPitchOverTime(0.5f, overboostSwitchFadeOutDuration);
        overboostSwitchFade.SetVolumeOverTime(0f, overboostSwitchFadeOutDuration);
    }

    void HandleOverboostInitiation()
    {
        Debug.Log("Initiated overboost");
        overboostSwitchFade.SetPitchOverTime(1f, 0f);
        overboostSwitchSound.Play();
    }

    void HandleOverboostOverheat()
    {
        Debug.Log("Overboost Overeating");
    }
}

class AudioTransitionController
{
    public AudioSource source;

    // Volume
    float volumeTarget;
    float volumeSpeed;
    bool volumeRunning;

    // Pitch
    float pitchTarget;
    float pitchSpeed;
    bool pitchRunning;

    float defaultVolume;
    float defaultPitch;

    public AudioTransitionController(AudioSource source)
    {
        this.source = source;
        defaultVolume = source.volume;
        defaultPitch = source.pitch;
    }

    public void SetVolumeOverTime(float target, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = target;
            volumeRunning = false;
            return;
        }

        volumeTarget = target;
        volumeSpeed = (target - source.volume) / duration;
        volumeRunning = true;
    }

    public void SetPitchOverTime(float target, float duration)
    {
        if (duration <= 0f)
        {
            source.pitch = target;
            pitchRunning = false;
            return;
        }

        pitchTarget = target;
        pitchSpeed = (target - source.pitch) / duration;
        pitchRunning = true;
    }

    public void Update()
    {
        if (volumeRunning)
        {
            float delta = volumeSpeed * Time.deltaTime;
            float remaining = volumeTarget - source.volume;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                source.volume = volumeTarget;
                volumeRunning = false;

                if (volumeTarget == 0f)
                {
                    source.Stop();
                    source.volume = defaultVolume;
                }
            }
            else
            {
                source.volume += delta;
            }
        }

        if (pitchRunning)
        {
            float delta = pitchSpeed * Time.deltaTime;
            float remaining = pitchTarget - source.pitch;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                source.pitch = pitchTarget;
                pitchRunning = false;
            }
            else
            {
                source.pitch += delta;
            }
        }
    }
}


