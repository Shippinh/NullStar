using UnityEngine;
using UnityEngine.Splines;

public class RailAttachTrigger : MonoBehaviour
{
    public PlayerAttachParameters attachParameters;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        SpaceShooterController playerRef = other.GetComponent<SpaceShooterController>();
        if (playerRef == null) return;

        playerRef.StartCoroutine(DeferredAttach(playerRef));
        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator DeferredAttach(SpaceShooterController playerRef)
    {
        yield return null;
        playerRef.InitiateBoostModeAttach(attachParameters);
    }
}
