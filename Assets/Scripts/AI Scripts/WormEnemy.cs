using UnityEngine;
using System.Collections.Generic;

public class WormEnemy : RammerEnemy
{
    public Transform playerCamera;
    public float pivotDistance = 20f;
    public float laserCooldown = 8f;
    public GameObject laserPrefab;

    private Vector3 currentPivot;
    private float nextLaserTime;

    void Update()
    {
        // Pick or maintain pivot
        if (!HasLineOfSight(currentPivot))
        {
            currentPivot = ChoosePivot();
        }

        // Face pivot until reached
        if (Vector3.Distance(transform.position, currentPivot) > 2f)
        {
            desiredVelocity = (currentPivot - transform.position).normalized * maxSpeed;
            transform.rotation = Quaternion.LookRotation(currentPivot - transform.position);
        }
        else
        {
            // At pivot: face player movement dir
            Vector3 playerDir = player.velocity.normalized;
            if (playerDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(playerDir);

            // Laser attack
            if (Time.time > nextLaserTime)
            {
                FireLaser();
                nextLaserTime = Time.time + laserCooldown;
            }
        }
    }

    Vector3 ChoosePivot()
    {
        // Cardinal pivots relative to camera
        List<Vector3> pivots = new List<Vector3>
        {
            playerCamera.forward,
            -playerCamera.forward,
            playerCamera.right,
            -playerCamera.right,
            playerCamera.up,
            -playerCamera.up
        };

        foreach (var dir in pivots)
        {
            Vector3 candidate = player.transform.position + dir * pivotDistance;
            if (HasLineOfSight(candidate)) return candidate;
        }

        return player.transform.position + playerCamera.forward * pivotDistance;
    }

    bool HasLineOfSight(Vector3 point)
    {
        if (Physics.Raycast(point, player.transform.position - point, out RaycastHit hit))
        {
            return hit.collider.GetComponent<SpaceShooterController>() != null;
        }
        return false;
    }

    void FireLaser()
    {
        Instantiate(laserPrefab, transform.position, transform.rotation);
    }
}
