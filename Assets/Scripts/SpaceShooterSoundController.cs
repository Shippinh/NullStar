using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public AudioSource blowerSoundLoop;
    public AudioSource airIntakeSoundLoop;
    [Header("Movement Pitch Settings")]
    [Range(0.5f, 3f)] public float basePitch = 1f;
    [Range(0.5f, 3f)] public float targetPitch = 1.5f;
    [Range(0f, 3f)] public float pitchChangeDuration = 0.5f;



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
    public AudioSource overboostOverheatCoolingIndicatorSound;
    public AudioSource overboostOverheatDamageSound;

    AudioTransitionController hardBurnFade;
    AudioTransitionController overboostAccelerationFade;
    AudioTransitionController overboostSwitchFade;
    AudioTransitionController overheatAlarmFade;
    AudioTransitionController overheatCoolingFade;

    void Awake()
    {
        playerController.OnOverboostActivation += HandleOverboostActivation;
        playerController.OnOverboostStop += HandleOverboostStop;
        playerController.OnOverboostInitiation += HandleOverboostInitiation;
        playerController.OnOverboostInitiationCancel += HandleOverboostInitiationCancel;
        playerController.OnOverboostOverheat += HandleOverboostOverheat;
        playerController.OnOverheatCoolingInitiated += HandleOverboostCooling;
        playerController.OnOverheatCoolingConcluded += HandleOverboostCoolingConcluded;

        hardBurnFade = new AudioTransitionController(hardBurnSoundLoop);
        overboostAccelerationFade = new AudioTransitionController(overboostAccelerationSound);
        overboostSwitchFade = new AudioTransitionController(overboostSwitchSound);
        overheatAlarmFade = new AudioTransitionController(overboostOverheatAlarmSound);
        overheatCoolingFade = new AudioTransitionController(overboostOverheatCoolingIndicatorSound);
    }

    void Update()
    {
        hardBurnFade.Update();
        overboostAccelerationFade.Update();
        overboostSwitchFade.Update();
        overheatAlarmFade.Update();
        overheatCoolingFade.Update();

        MovementBasedPitch();
    }

    void MovementBasedPitch()
    {
        float delta = Time.deltaTime;
        float pitchDelta = (targetPitch - basePitch) / pitchChangeDuration;

        // Player is providing movement input
        if (playerController.AnyMovementInput() || playerController.jumpInput)
        {
            if(playerController.AnyMovementInput() && playerController.jumpInput)
            {
                blowerSoundLoop.pitch = Mathf.MoveTowards(blowerSoundLoop.pitch, targetPitch + 0.5f, pitchDelta * delta);
                airIntakeSoundLoop.pitch = Mathf.MoveTowards(airIntakeSoundLoop.pitch, targetPitch + 0.5f, pitchDelta * delta);
            }
            else
            {
                blowerSoundLoop.pitch = Mathf.MoveTowards(blowerSoundLoop.pitch, targetPitch, pitchDelta * delta);
                airIntakeSoundLoop.pitch = Mathf.MoveTowards(airIntakeSoundLoop.pitch, targetPitch, pitchDelta * delta);
            }
        }
        // No input, return to base pitch
        else
        {
            blowerSoundLoop.pitch = Mathf.MoveTowards(blowerSoundLoop.pitch, basePitch, pitchDelta * delta);
            airIntakeSoundLoop.pitch = Mathf.MoveTowards(airIntakeSoundLoop.pitch, basePitch, pitchDelta * delta);
        }

        
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
        overheatAlarmFade.SetVolumeOverTime(0f, 0.1f);

        if(!playerController.isCooled)
        {
            overboostOverheatCoolingIndicatorSound.volume = 0.5f;
            overboostOverheatCoolingIndicatorSound.Play();
        }
    }

    void HandleOverboostInitiationCancel()
    {
        Debug.Log("Overboost initiation cancelled");
        overboostSwitchFade.SetPitchOverTime(0.5f, overboostSwitchFadeOutDuration);
        overboostSwitchFade.SetVolumeOverTime(0.1f, overboostSwitchFadeOutDuration);
    }

    void HandleOverboostInitiation()
    {
        Debug.Log("Initiated overboost");
        overheatCoolingFade.SetVolumeOverTime(0.1f, 0.1f);
        overboostOverheatCoolingIndicatorSound.Stop();
        overboostSwitchFade.SetPitchOverTime(1f, 0f);
        overboostSwitchFade.SetVolumeOverTime(0.6f, 0f);
        overboostSwitchSound.Play();
    }

    void HandleOverboostOverheat()
    {
        Debug.Log("Overboost Overheating");
        overboostOverheatAlarmSound.Play();
    }

    void HandleOverboostCooling()
    {
        Debug.Log("Overboost Overheat Cooling Initiated");
        overboostOverheatCoolingIndicatorSound.volume = 0.5f;
        overboostOverheatCoolingIndicatorSound.Play();
    }

    void HandleOverboostCoolingConcluded()
    {
        Debug.Log("Overboost Overheat Cooling Concluded");
        overheatCoolingFade.SetVolumeOverTime(0.1f, 0.1f);
        overboostOverheatCoolingIndicatorSound.Stop();
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


