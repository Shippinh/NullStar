using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public SoundAmbienceManager blowerSoundLoop;
    public SoundAmbienceManager airIntakeSoundLoop;
    public SoundAmbienceManager hardBurnSoundLoop;
    public SoundAmbienceManager overboostSwitchSound;
    public SoundAmbienceManager overboostEngineImpactSound;
    public SoundAmbienceManager overboostAccelerationSound;

    void Awake()
    {
        playerController.OnOverboostActivation += HandleOverboostActivation;
        playerController.OnOverboostStop += HandleOverboostStop;
        playerController.OnOverboostInitiation += HandleOverboostInitiation;
        playerController.OnOverboostInitiationCancel += HandleOverboostInitiationCancel;
    }

    void HandleOverboostActivation()
    {
        Debug.Log("Overboost activated");
        overboostEngineImpactSound.PlayCurrentHandlerSound();
        overboostAccelerationSound.PlayCurrentHandlerSound();
        hardBurnSoundLoop.PlayCurrentHandlerSound();
        //hardBurnSoundLoop.GraduallyIncreasePitch(10);
    }

    void HandleOverboostStop()
    {
        Debug.Log("Overboost concluded");
        overboostAccelerationSound.currentHandler.AudioSource.Stop();
        hardBurnSoundLoop.currentHandler.AudioSource.Stop();
    }

    void HandleOverboostInitiationCancel()
    {
        Debug.Log("Overboost initiation cancelled");
    }

    void HandleOverboostInitiation()
    {
        Debug.Log("Initiated overboost");
        overboostSwitchSound.PlayCurrentHandlerSound();
    }
}
