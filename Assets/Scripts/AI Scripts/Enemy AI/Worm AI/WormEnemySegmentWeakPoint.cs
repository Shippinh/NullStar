using System.Collections;
using UnityEngine;

public class WormEnemySegmentWeakPoint : MonoBehaviour
{
    [Header("Target & Aiming")]
    public SpaceShooterController player;
    public float aimSmoothing = 2f;
    private Vector3 aimedDir;

    void Update()
    {
        if (!player) return;

        Vector3 playerPos = player.transform.position;
        Vector3 playerVel = player.body ? player.body.velocity : Vector3.zero;

        float projectileSpeed = 300f;
        float distance = Vector3.Distance(transform.position, playerPos);
        float timeToHit = distance / projectileSpeed;
        Vector3 predictedPos = playerPos + playerVel * timeToHit;

        aimedDir = (predictedPos - transform.position).normalized;

        // Rotate smoothly toward aim
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(aimedDir),
            Time.deltaTime * aimSmoothing
        );
    }
}
