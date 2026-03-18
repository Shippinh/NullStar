using UnityEngine;
using UnityEngine.Splines;

public class RailAttachTrigger : MonoBehaviour
{
    [Header("Attach Parameters")]
    public SplineContainer newSplineContainer;
    public float transitionDuration = 3f;
    public float xOffset = 0f;
    public float yOffset = 0f;
    public float newSplineT = 0f;
    public float initialSpeed = 56f;


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        SpaceShooterController playerRef = other.GetComponent<SpaceShooterController>();
        playerRef.InitiateBoostModeAttach(newSplineContainer, transitionDuration, xOffset, yOffset, newSplineT, initialSpeed);

        gameObject.SetActive(false);
    }
}
