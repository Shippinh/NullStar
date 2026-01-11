using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public AudioSource blowerSoundLoop;
    public AudioSource airIntakeSoundLoop;
    [Header("Movement Pitch Settings")]
    [Range(0.5f, 3f)] public float baseMovementPitch = 1f;
    [Range(0.5f, 3f)] public float targetMovementPitch = 1.5f;
    [Range(0f, 3f)] public float movementPitchChangeDuration = 0.5f;

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

    public AudioSource dodgeSound1;
    public AudioSource dodgeSound2;
    public AudioSource dodgeSound3;
    public AudioSource dodgeSound4;
    public AudioSource dodgeSound5;
    public int alternateDodgeSource = 0;

    public AudioSource dodgeRechargeIndicator;
    public AudioSource dodgeChargeGainedIndicator;

    public float perDodgeExtraPitch = 0.3f;
    [SerializeField] private float totalExtraPitch = 0f;
    private float defaultDodgePitch = 1f;

    [Header("Shooting Sounds")]
    public AudioSource plasmaShootingLoop;
    public AudioSource plasmaGeneratorLoop;
    public AudioClip plasmaShootingEnd;
    public AudioSource plasmaGeneratorEndOneShot;
    public AudioSource plasmaEndOneShotSource;
    private bool wasShootingPlasmaLastFrame = false;
    private float plasmaShootEndDelay = 0.1f;
    private float plasmaShootEndTimer = 0f;
    public float plasmaPitchExtraMagnitude = 0.08f;
    [Range(0f, 1f)] public float plasmaGeneratorVolume = 0.5f;

    [HideInInspector] public bool isShootingPlasma;


    AudioTransitionController hardBurnFade;
    AudioTransitionController overboostAccelerationFade;
    AudioTransitionController overboostSwitchFade;
    AudioTransitionController overheatAlarmFade;
    AudioTransitionController overheatCoolingFade;
    AudioTransitionController dodgeRechargeIndicatorFade;
    AudioTransitionController dodgeChargeGainedIndicatorFade;
    AudioTransitionController shootingFade;
    AudioTransitionController generatorFade;

    void Awake()
    {
        playerController.OnOverboostActivation += HandleOverboostActivation;
        playerController.OnOverboostStop += HandleOverboostStop;
        playerController.OnOverboostInitiation += HandleOverboostInitiation;
        playerController.OnOverboostInitiationCancel += HandleOverboostInitiationCancel;
        playerController.OnOverboostOverheat += HandleOverboostOverheat;
        playerController.OnOverheatCoolingInitiated += HandleOverboostCooling;
        playerController.OnOverheatCoolingConcluded += HandleOverboostCoolingConcluded;
        playerController.OnDodgeUsed += HandleDodgeUsed;
        playerController.OnDodgeActualRechargeStart += HandleDodgeActualRechargeStart;
        playerController.OnDodgeChargeGain += HandleDodgeChargeGain;

        hardBurnFade = new AudioTransitionController(hardBurnSoundLoop);
        overboostAccelerationFade = new AudioTransitionController(overboostAccelerationSound);
        overboostSwitchFade = new AudioTransitionController(overboostSwitchSound);
        overheatAlarmFade = new AudioTransitionController(overboostOverheatAlarmSound);
        overheatCoolingFade = new AudioTransitionController(overboostOverheatCoolingIndicatorSound);
        dodgeRechargeIndicatorFade = new AudioTransitionController(dodgeRechargeIndicator);
        dodgeChargeGainedIndicatorFade = new AudioTransitionController(dodgeChargeGainedIndicator);
        shootingFade = new AudioTransitionController(plasmaShootingLoop);
        generatorFade = new AudioTransitionController(plasmaGeneratorLoop);

        defaultDodgePitch = dodgeSound1.pitch;
    }

    void Update()
    {
        isShootingPlasma = Input.GetKey(playerController.inputConfig.Shoot);

        hardBurnFade.Update();
        overboostAccelerationFade.Update();
        overboostSwitchFade.Update();
        overheatAlarmFade.Update();
        overheatCoolingFade.Update();
        dodgeRechargeIndicatorFade.Update();
        dodgeChargeGainedIndicatorFade.Update();
        shootingFade.Update();
        generatorFade.Update();

        MovementBasedPitch();
        totalExtraPitch = (playerController.maxDodgeCharges - playerController.dodgeCharges) * perDodgeExtraPitch;

        HandlePlasmaShootingSound();
    }

    void MovementBasedPitch()
    {
        float extraMovementPitch = 0f;
        float finalMovementPitch = baseMovementPitch;

        if (playerController.boostInitiated) // boost
        {
            extraMovementPitch += 0.5f; // Taking in account constant forward movement during boost mode

            if (!playerController.cameraControllerRef.LookingSideways)
                if (playerController.AnySidewaysMovementInput()) extraMovementPitch += 0.5f;

            if (playerController.AnyForwardMovementInput()) extraMovementPitch += 0.5f;

            if (extraMovementPitch >= 1.5f)
                extraMovementPitch = 1.25f;
        }
        else if (playerController.overboostInitiated) // overboost
        {
            Vector3 inputDir = playerController.lastExclusiveDirectionalInput;

            if ((inputDir == Vector3.left || inputDir == Vector3.right) && playerController.AnyForwardMovementInput()) extraMovementPitch += 0.5f;
            if ((inputDir == Vector3.back || inputDir == Vector3.forward) && playerController.AnySidewaysMovementInput()) extraMovementPitch += 0.5f;
        }
        else // normal
        {
            if (playerController.jumpInput) extraMovementPitch += 0.5f;

            if (playerController.AnyMovementInput()) finalMovementPitch = targetMovementPitch;
        }

        finalMovementPitch += extraMovementPitch;

        //Debug.Log(finalMovementPitch);

        float delta = Time.deltaTime;
        float pitchDelta = (targetMovementPitch - baseMovementPitch) / movementPitchChangeDuration;

        blowerSoundLoop.pitch = Mathf.MoveTowards(blowerSoundLoop.pitch, finalMovementPitch, pitchDelta * delta);
        airIntakeSoundLoop.pitch = Mathf.MoveTowards(airIntakeSoundLoop.pitch, finalMovementPitch, pitchDelta * delta);
    }

    void HandlePlasmaShootingSound()
    {
        float desiredPitch = 1.2f;

        if (playerController.rageActive && playerController.adrenalineActive)
        {
            desiredPitch = 1.9f;
        }
        else if (playerController.adrenalineActive)
        {
            desiredPitch = 1.75f;
        }
        else if (playerController.rageActive)
        {
            desiredPitch = 1.5f;
        }

        desiredPitch = desiredPitch + Random.Range(plasmaPitchExtraMagnitude, -plasmaPitchExtraMagnitude);
        float desiredOscialtionVolume = plasmaGeneratorVolume + Random.Range(plasmaPitchExtraMagnitude, -plasmaPitchExtraMagnitude);


        if (isShootingPlasma)
        {
            shootingFade.SetPitchOverTime(desiredPitch, 0.1f);
            generatorFade.SetVolumeOverTime(desiredOscialtionVolume, 0.01f);
            if (!wasShootingPlasmaLastFrame)
            {
                // Just started firing
                plasmaShootingLoop.Play();
                plasmaGeneratorLoop.Play();
            }

            // Reset end timer
            plasmaShootEndTimer = 0f;
        }
        else
        {
            if (wasShootingPlasmaLastFrame)
            {
                // Just stopped firing
                plasmaShootEndTimer = plasmaShootEndDelay;
            }

            if (plasmaShootEndTimer > 0f)
            {
                plasmaShootEndTimer -= Time.deltaTime;
                if (plasmaShootEndTimer <= 0f)
                {
                    // Stop loop and play end
                    plasmaShootingLoop.Stop();
                    generatorFade.SetVolumeOverTime(0.0001f, 0.2f);
                    plasmaGeneratorEndOneShot.PlayOneShot(plasmaGeneratorEndOneShot.clip);
                    plasmaEndOneShotSource.PlayOneShot(plasmaShootingEnd);
                }
            }
        }

        wasShootingPlasmaLastFrame = isShootingPlasma;
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
        overboostAccelerationFade.SetVolumeOverTime(0f, playerController.overboostActivationDelay);
        
        hardBurnFade.SetVolumeOverTime(0f, hardBurnFadeOutDuration);
        hardBurnFade.SetPitchOverTime(1f, hardBurnFadeOutDuration);
        overheatAlarmFade.SetVolumeOverTime(0f, 0.1f);

        if(!playerController.isCooled)
        {
            overboostOverheatCoolingIndicatorSound.volume = 0.8f;
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
        overheatCoolingFade.SetVolumeOverTime(0.1f, 0.01f);
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
        overboostOverheatCoolingIndicatorSound.volume = 0.8f;
        overboostOverheatCoolingIndicatorSound.Play();
    }

    void HandleOverboostCoolingConcluded()
    {
        Debug.Log("Overboost Overheat Cooling Concluded");
        overheatCoolingFade.SetVolumeOverTime(0.1f, 0.01f);
        overboostOverheatCoolingIndicatorSound.Stop();
    }

    void HandleDodgeUsed()
    {
        float desiredPitch = defaultDodgePitch + totalExtraPitch;

        dodgeSound1.pitch = desiredPitch; 
        dodgeSound2.pitch = desiredPitch;
        dodgeSound3.pitch = desiredPitch;
        dodgeSound4.pitch = desiredPitch;
        dodgeSound5.pitch = desiredPitch;

        switch (alternateDodgeSource)
        {
            case 0:
            {
                dodgeSound1.Play();
                alternateDodgeSource++;
                break;
            }
            case 1:
            {
                dodgeSound2.Play();
                alternateDodgeSource++;
                break;
            }
            case 2:
            {
                dodgeSound3.Play();
                alternateDodgeSource++;
                break;
            }
            case 3:
            {
                dodgeSound4.Play();
                alternateDodgeSource++;
                break;
            }
            case 4:
            {
                dodgeSound5.Play();
                alternateDodgeSource = 0;
                break;
            }
        }
        Debug.Log("Dodge initiated");
        dodgeRechargeIndicatorFade.SetVolumeOverTime(0f, 0.3f);
        dodgeRechargeIndicatorFade.SetPitchOverTime(0.8f, 0.3f);
    }

    void HandleDodgeActualRechargeStart()
    {
        Debug.Log("Dodge recharge has actually begun");
        dodgeRechargeIndicator.pitch = 1f;
        dodgeRechargeIndicator.Play();
        dodgeRechargeIndicatorFade.SetPitchOverTime(1.5f, 2f);
    }

    void HandleDodgeChargeGain()
    {
        Debug.Log("Dodge charge gained");
        dodgeChargeGainedIndicator.Play();
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


