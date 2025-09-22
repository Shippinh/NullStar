using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class WormEnemy : MonoBehaviour
{
    [Header("Target & Movement")]
    public SpaceShooterController player;
    public Transform playerCamera;

    public float maxSpeed = 10f;
    public float maxAcceleration = 30f;
    public float maxAirAcceleration = 10f;
    public float jetpackAcceleration = 15f;

    [Header("Avoidance")]
    public float avoidanceForce = 5f;
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    [Header("Worm Behavior")]
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground
    public GameObject laserPrefab;
    public float laserCooldown = 8f;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private bool OnGround = true;

    private Vector3 contactNormal = Vector3.up;
    private Vector3 currentPivot;
    private float nextLaserTime;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;
        currentPivot = ChoosePivot();
    }

    void FixedUpdate()
    {
        if (!HasLineOfSight(currentPivot))
        {
            currentPivot = ChoosePivot();
        }

        // Avoidance
        Vector3 avoidanceVector = Vector3.zero;
        foreach (var col in nearbyObstacles)
        {
            if (!col) continue;
            Vector3 point = col.ClosestPoint(transform.position);
            Vector3 away = (transform.position - point);
            float dist = away.magnitude;

            if (dist > 0f)
                avoidanceVector += away.normalized * (1f / dist);
        }

        if (avoidanceVector != Vector3.zero)
            avoidanceVector = avoidanceVector.normalized * avoidanceForce;

        // Movement
        Vector3 target = currentPivot != Vector3.zero ? currentPivot : player.transform.position;
        Vector3 toTarget = (target - transform.position).normalized;

        Vector3 combinedDir = (toTarget + avoidanceVector).normalized;
        desiredVelocity = combinedDir * maxSpeed;

        AdjustVelocity();
        AdjustAirVelocity();
        rb.velocity = velocity;

        // Rotation
        if (Vector3.Distance(transform.position, target) > 2f)
        {
            transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
        }
        else
        {
            Vector3 playerDir = player.body.velocity.normalized;
            if (playerDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(playerDir, Vector3.up);

            if (Time.time > nextLaserTime)
            {
                FireLaser();
                nextLaserTime = Time.time + laserCooldown;
            }
        }
    }

    Vector3 ChoosePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = player.body.velocity.normalized;
        if (playerForward == Vector3.zero) playerForward = playerCamera.forward;

        List<Vector3> dirs = new List<Vector3>
    {
        //Vector3.forward,
        //Vector3.back,
        playerCamera.right,
        -playerCamera.right,
        player.transform.up,
        -player.transform.up
    };

        List<Vector3> validPivots = new List<Vector3>();
        foreach (var dir in dirs)
        {
            // Start with pure cardinal pivot
            Vector3 candidate = playerPos + dir * pivotDistance;

            // Then apply global "forward push"
            candidate += playerForward * pivotForwardPush;

            // Offset upwards from ground
            candidate.y += pivotHeightOffset;

            if (HasLineOfSight(candidate))
                validPivots.Add(candidate);
        }

        if (validPivots.Count > 0)
            return validPivots[Random.Range(0, validPivots.Count)];

        // fallback pivot directly in front of player
        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }


    bool HasLineOfSight(Vector3 point)
    {
        if (point == Vector3.zero) return false;

        Vector3 dir = (player.transform.position - point).normalized;
        float distance = Vector3.Distance(point, player.transform.position);

        // LayerMask can help avoid hitting the worm's own colliders
        if (Physics.Raycast(point, dir, out RaycastHit hit, distance, ~LayerMask.GetMask("Worm")))
        {
            // Make sure we hit the player's collider (even if it's a child)
            return hit.collider.GetComponentInParent<SpaceShooterController>() != null;
        }

        // No obstruction, line of sight is clear
        return true;
    }


    void FireLaser()
    {
        //Instantiate(laserPrefab, transform.position, transform.rotation);
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;

        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), acceleration * Time.fixedDeltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), acceleration * Time.fixedDeltaTime);
    }

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;
        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;
        float deltaY = targetY - currentY;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), jetpackAcceleration * Time.fixedDeltaTime);
        velocity += yAxis * change;
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & obstacleMask) != 0 && !other.isTrigger)
        {
            if (!nearbyObstacles.Contains(other))
                nearbyObstacles.Add(other);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (nearbyObstacles.Contains(other))
            nearbyObstacles.Remove(other);
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // Draw detection radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw current pivot
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentPivot, 1f);

        // Draw all candidate pivots and line of sight
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = player.body != null ? player.body.velocity.normalized : playerCamera.forward;
        if (playerForward == Vector3.zero) playerForward = playerCamera.forward;

        List<Vector3> dirs = new List<Vector3>
    {
        //Vector3.forward,
        //Vector3.back,
        playerCamera.right,
        -playerCamera.right,
        player.transform.up,
        -player.transform.up
    };

        foreach (var dir in dirs)
        {
            Vector3 candidate = playerPos + dir * pivotDistance;
            candidate += playerForward * pivotForwardPush;
            candidate.y += pivotHeightOffset;

            bool los = HasLineOfSight(candidate);

            Gizmos.color = los ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(candidate, 0.5f);
            Gizmos.DrawLine(candidate, playerPos);
        }
    }

}
