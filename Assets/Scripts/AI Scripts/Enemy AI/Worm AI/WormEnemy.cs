using System;
using System.Collections.Generic;
using UnityEngine;

// Describes Movement for the worm head
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class WormEnemy : MonoBehaviour
{
    public enum WormMovementPattern
    {
        Normal,
        SidewaysWiggle,
        Spiral
    }

    [Header("Target & Movement")]
    public SpaceShooterController player;
    public Transform playerCamera;

    public float maxSpeed = 10f;
    public float maxAcceleration = 30f;

    public LayerMask LOSMask;
    Vector3 currentTarget;

    public bool canMove = true;


    [Header("Oscillation")]
    public WormMovementPattern movementPattern = WormMovementPattern.Normal;
    public float oscillationAmplitude = 3f;
    public float oscillationFrequency = 2f;
    [SerializeField, Range(0f, 1f)] public float oscillationStrengthFactor = 1.0f;
    public bool invertSpiral = false;
    public bool invertWiggle = false;


    [Header("Pivot Timing")]
    public float pivotChangeInterval = 3f; // every 3 seconds
    private float nextPivotTime = 0f;
    public bool invertMovementOnNewPivot = false;
    public bool randomizeMovementPatternOnNewPivot = false;

    private float oscillationTime = 0f;

    [Header("Avoidance")]
    public float avoidanceForce = 5f;
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    [Header("Worm Behavior")]
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;

    private Vector3 contactNormal = Vector3.up;
    private Vector3 currentPivot;
    private int currentDirectionIndex = -1;
    private List<Vector3> availableDirections;

    private Vector3 lastPlayerForward = Vector3.forward;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;
        UpdateDirections();
        currentPivot = ChoosePivot();
    }

    private void LateUpdate()
    {
        if (player.GetPlayerOnGround())
            currentPivot = CheckForceTopPivot();
        else
            currentPivot = UpdatePivot();
    }

    void FixedUpdate()
    {
        // Smoothly accelerate toward desiredVelocity
        if (canMove)
        {
            UpdateDirections();
            CalculateDesiredVelocity();

            AdjustVelocity();
            AdjustAirVelocity();
            rb.velocity = velocity;


            // Rotation
            if (Vector3.Distance(transform.position, currentTarget) > 2f)
            {
                transform.rotation = Quaternion.LookRotation(currentTarget - transform.position, Vector3.up);
            }
            else
            {
                Vector3 playerDir = player.body.velocity.normalized;
                if (playerDir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(playerDir, Vector3.up);
            }
        }
    }

    void CalculateDesiredVelocity()
    {
        if (Time.time >= nextPivotTime)
        {
            currentPivot = ChoosePivot();

            if (invertMovementOnNewPivot)
            {
                invertWiggle = !invertWiggle;
                invertSpiral = !invertSpiral;
            }

            if (randomizeMovementPatternOnNewPivot)
            {
                movementPattern = (WormMovementPattern)UnityEngine.Random.Range(
                    0, Enum.GetValues(typeof(WormMovementPattern)).Length
                );
            }

            nextPivotTime = Time.time + pivotChangeInterval;
        }

        // Base target direction
        currentTarget = currentPivot != Vector3.zero ? currentPivot : player.transform.position;
        Vector3 toTarget = (currentTarget - transform.position).normalized;

        // Oscillation
        oscillationTime += Time.fixedDeltaTime;
        Vector3 offset = Vector3.zero;

        switch (movementPattern)
        {
            case WormMovementPattern.SidewaysWiggle:
                {
                    Vector3 side = Vector3.Cross(toTarget, Vector3.up).normalized;
                    float wiggle = Mathf.Sin(oscillationTime * oscillationFrequency) * oscillationAmplitude;
                    if (invertWiggle) wiggle *= -1f;
                    offset = side * wiggle;
                    break;
                }
            case WormMovementPattern.Spiral:
                {
                    float angle = oscillationTime * oscillationFrequency;
                    if (invertSpiral) angle *= -1f;
                    Vector3 side = Vector3.Cross(toTarget, Vector3.up).normalized;
                    Vector3 up = Vector3.Cross(toTarget, side).normalized;
                    offset = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * oscillationAmplitude;
                    break;
                }
            case WormMovementPattern.Normal:
            default:
                break;
        }

        // Primary movement direction
        Vector3 primaryDir = (toTarget + offset * oscillationStrengthFactor).normalized * maxSpeed;

        // Avoidance as additive steering
        Vector3 avoidanceVector = CalculateObstacleAvoidance();
        Vector3 combined = primaryDir + avoidanceVector;

        // Clamp final desired velocity
        desiredVelocity = Vector3.ClampMagnitude(combined, maxSpeed);
    }

    Vector3 ChoosePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        List<Vector3> validPivots = new List<Vector3>();
        foreach (var dir in availableDirections)
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
        {
            int randIndex = UnityEngine.Random.Range(0, validPivots.Count);
            currentDirectionIndex = randIndex; // just remember the index
            return validPivots[randIndex];
        }

        // fallback pivot directly in front of player
        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }

    Vector3 CheckForceTopPivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;

        Vector3 dir = availableDirections[2]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * pivotDistance;

        pivot += playerForward * pivotForwardPush;
        pivot.y += pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }

    Vector3 UpdatePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;

        Vector3 dir = availableDirections[currentDirectionIndex]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * pivotDistance;

        pivot += playerForward * pivotForwardPush;
        pivot.y += pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }


    void UpdateDirections()
    {
        availableDirections = new List<Vector3>
        {
            //Vector3.forward,
            //Vector3.back,
            playerCamera.right,
            -playerCamera.right,
            player.transform.up,
            -player.transform.up
        };
    }

    Vector3 GetPlayerForward()
    {
        if (player != null && player.body != null)
        {
            Vector3 vel = player.body.velocity;

            if (vel.sqrMagnitude > 0.01f)
            {
                lastPlayerForward = vel.normalized;
                return lastPlayerForward;
            }
        }

        // Fallbacks if no velocity or player/body is null
        if (playerCamera != null)
            return playerCamera.forward;

        return lastPlayerForward != Vector3.zero ? lastPlayerForward : Vector3.forward;
    }

    bool HasLineOfSight(Vector3 point)
    {
        if (point == Vector3.zero) return false;

        Vector3 dir = (player.transform.position - point).normalized;
        float distance = Vector3.Distance(point, player.transform.position);

        // LayerMask can help avoid hitting the worm's own colliders
        if (Physics.Raycast(point, dir, out RaycastHit hit, distance, LOSMask))
        {
            // Make sure we hit the player's collider (even if it's a child)
            return hit.collider.GetComponentInParent<SpaceShooterController>() != null;
        }

        // No obstruction, line of sight is clear
        return true;
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

        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), maxAcceleration * Time.fixedDeltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), maxAcceleration * Time.fixedDeltaTime);
    }

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;
        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;
        float deltaY = targetY - currentY;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), maxAcceleration * Time.fixedDeltaTime);
        velocity += yAxis * change;
    }

    Vector3 CalculateObstacleAvoidance()
    {
        Vector3 totalAvoidance = Vector3.zero;

        foreach (var col in nearbyObstacles)
        {
            if (!col) continue;

            Vector3 closestPoint = col.ClosestPoint(transform.position);
            Vector3 away = transform.position - closestPoint;
            float distance = away.magnitude;

            if (distance > 0f)
            {
                // Scale avoidance inversely by distance (closer obstacles push stronger)
                // Using a smooth falloff (distance / detectionRadius)
                float strength = Mathf.Clamp01((detectionRadius - distance) / detectionRadius);
                totalAvoidance += away.normalized * avoidanceForce * strength;
            }
        }

        return totalAvoidance;
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
        //if (player == null || player.body || playerCamera == null || availableDirections == null) return;

        // Draw detection radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw current pivot
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentPivot, 1f);

        if (Application.isPlaying) // only simulate in Play mode
        {
            // Draw all candidate pivots and line of sight
            Vector3 playerPos = player.transform.position;
            Vector3 playerForward = GetPlayerForward();

            foreach (var dir in availableDirections)
            {
                Vector3 candidate = playerPos + dir * pivotDistance;
                candidate += playerForward * pivotForwardPush;
                candidate.y += pivotHeightOffset;

                bool los = HasLineOfSight(candidate);

                Gizmos.color = los ? Color.green : Color.yellow;
                Gizmos.DrawWireSphere(candidate, 0.5f);
                Gizmos.DrawLine(candidate, playerPos);
            }

            Gizmos.color = Color.yellow;
            Vector3 simPos = transform.position;
            Vector3 simVel = desiredVelocity; // use current calculated orbit velocity

            float simStep = 0.2f;   // seconds per prediction step
            int simSteps = 40;      // number of steps (~8 seconds ahead)

            for (int i = 0; i < simSteps; i++)
            {
                Vector3 nextPos = simPos + simVel * simStep;

                Gizmos.DrawLine(simPos, nextPos);

                simPos = nextPos;
                // (Optional: refine with same logic as CalculateDesiredVelocity if you want adaptive orbiting)
            }
        }
    }

}
