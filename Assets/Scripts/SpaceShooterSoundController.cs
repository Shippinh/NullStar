using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterSoundController : MonoBehaviour
{
    public SpaceShooterController playerController;

    public AudioSource blowerSoundLoop;
    public AudioSource airIntakeSoundLoop;
    public AudioSource hardBurnSoundLoop;
    public AudioSource overboostSwitchSound;
    public AudioSource overboostEngineImpactSound;
    public AudioSource overboostAccelerationSound;

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
    }

    void HandleOverboostStop()
    {
        Debug.Log("Overboost concluded");
    }

    void HandleOverboostInitiationCancel()
    {
        Debug.Log("Overboost initiation cancelled");
    }

    void HandleOverboostInitiation()
    {
        Debug.Log("Initiated overboost");
    }
}
