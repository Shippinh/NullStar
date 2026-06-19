using System;
using System.Collections.Generic;
using UnityEngine;


// THIS IS TECHNICALLY A RANGED ENEMY BUT THE MIN MAX RANGE VALUES ARE HARDCODED HERE
// Describes Movement for the worm head
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class WormEnemy : EnemyAIComponent
{
    public enum WormMovementPattern
    {
        Normal,
        SidewaysWiggle,
        Spiral
    }

    [Header("Target & Movement")]
    public SpaceShooterController player;
    public Camera playerCamera;

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

    [Header("Worm Behavior")]
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground

    [Header("Wall Avoidance")]
    public LayerMask wallMask;
    public float wallLookAheadDistance = 6f;

    [Header("Other")]
    public bool reinitializeOnEnable = true;

    private Vector3 currentPivot;
    private int currentDirectionIndex = -1;
    private List<Vector3> availableDirections;

    private Vector3 lastPlayerForward = Vector3.forward;

    public override void Start()
    {
        base.Start();

        if (!player)
            player = FindObjectOfType<SpaceShooterController>();

        playerCamera = Camera.main;

        velocity = Vector3.zero;
        UpdateDirections();
        currentPivot = ChoosePivot();
        nextPivotTime = Time.time + pivotChangeInterval * (UnityEngine.Random.value);
    }

    // Soft AI reinitialization
    private void OnEnable()
    {
        if (reinitializeOnEnable)
        {
            velocity = Vector3.zero;
            desiredVelocity = Vector3.zero;
            nextPivotTime = Time.time + pivotChangeInterval * (UnityEngine.Random.value);
        }
    }

    private void LateUpdate()
    {
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

        currentTarget = currentPivot != Vector3.zero ? currentPivot : player.transform.position;
        Vector3 toTarget = (currentTarget - transform.position).normalized;

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

        Vector3 primaryDir = (toTarget + offset * oscillationStrengthFactor).normalized * maxSpeed;
        Vector3 avoidanceVector = CalculateObstacleAvoidance();
        Vector3 combined = primaryDir + avoidanceVector;

        // Deflect away from walls before clamping
        combined = DeflectFromWalls(combined);

        desiredVelocity = Vector3.ClampMagnitude(combined, maxSpeed);
    }

    // SphereCast along intended movement, slide velocity along wall normal if hit
    Vector3 DeflectFromWalls(Vector3 intendedVelocity)
    {
        if (intendedVelocity.sqrMagnitude < 0.001f) return intendedVelocity;

        Vector3 dir = intendedVelocity.normalized;
        float speed = intendedVelocity.magnitude;
        float castRadius = 0.5f; // should roughly match worm head collider radius

        if (Physics.SphereCast(transform.position, castRadius, dir, out RaycastHit hit, wallLookAheadDistance, wallMask))
        {
            // Project intended velocity onto the wall plane — preserves speed, redirects direction
            Vector3 deflected = Vector3.ProjectOnPlane(intendedVelocity, hit.normal);

            // Blend toward full deflection as we get closer
            float t = 1f - (hit.distance / wallLookAheadDistance);
            return Vector3.Lerp(intendedVelocity, deflected, t);
        }

        return intendedVelocity;
    }

    Vector3 ChoosePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();
        bool playerGrounded = player.GetPlayerOnGround();

        List<int> shuffledIndices = new List<int>();
        for (int i = 0; i < availableDirections.Count; i++)
            shuffledIndices.Add(i);

        for (int i = shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (shuffledIndices[i], shuffledIndices[j]) = (shuffledIndices[j], shuffledIndices[i]);
        }

        List<(Vector3 pivot, int dirIndex)> validPivots = new List<(Vector3, int)>();

        foreach (int idx in shuffledIndices)
        {
            Vector3 dir = availableDirections[idx];

            // Skip downward pivots when grounded — they'll always fail LOS or be underground
            if (playerGrounded && Vector3.Dot(dir, Vector3.up) < -0.1f)
                continue;

            Vector3 candidate = playerPos + dir * pivotDistance;
            candidate += playerForward * pivotForwardPush;
            candidate += Vector3.up * pivotHeightOffset; // world-up offset, not raw Y

            if (HasLineOfSight(candidate) && !IsPivotUnderground(candidate))
                validPivots.Add((candidate, idx));
        }

        if (validPivots.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, validPivots.Count);
            currentDirectionIndex = validPivots[pick].dirIndex;
            return validPivots[pick].pivot;
        }

        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }

    bool IsPivotUnderground(Vector3 pivot)
    {
        // Cast downward from well above the pivot's XZ position to find the ground surface.
        // Starting FROM the pivot itself fails when the pivot is already inside geometry.
        Vector3 castOrigin = new Vector3(pivot.x, pivot.y + 500f, pivot.z);

        if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, LOSMask))
        {
            return pivot.y < hit.point.y;
        }

        // No ground found below at all — treat as safe (open sky / void level)
        return false;
    }

    Vector3 UpdatePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return ChoosePivot();

        Vector3 dir = availableDirections[currentDirectionIndex];
        Vector3 pivot = playerPos + dir * pivotDistance;
        pivot += playerForward * pivotForwardPush;
        pivot += Vector3.up * pivotHeightOffset;

        if (HasLineOfSight(pivot) && !IsPivotUnderground(pivot))
            return pivot;

        // Current pivot is invalid — immediately pick a new one and reset the timer
        nextPivotTime = Time.time + pivotChangeInterval;
        return ChoosePivot();
    }


    void UpdateDirections()
    {
        availableDirections = new List<Vector3>
        {
            //Vector3.forward,
            //Vector3.back,
            playerCamera.transform.right,
            -playerCamera.transform.right,
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
            return playerCamera.transform.forward;

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
        if (!enabled) return;

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
                candidate += Vector3.up * pivotHeightOffset;

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
