using UnityEngine;
using UnityEngine.Splines;

public class RailAttachTrigger : MonoBehaviour
{
    public AttachParameters attachParameters;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        SpaceShooterController playerRef = other.GetComponent<SpaceShooterController>();
        playerRef.InitiateBoostModeAttach(attachParameters.newSplineContainer, attachParameters.transitionDuration, attachParameters.xOffset, attachParameters.yOffset, attachParameters.newSplineT, attachParameters.initialSpeed);

        gameObject.SetActive(false);
    }
}
