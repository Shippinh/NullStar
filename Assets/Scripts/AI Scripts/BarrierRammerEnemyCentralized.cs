using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BarrierRammerEnemyCentralized : MonoBehaviour
{
    [Header("References")]
    public SpaceShooterController player;
    public Transform pivot;          // pivot object for corkscrew rotation
    public Transform enemyA;         // always assigned
    public Transform enemyB;         // optional
    [SerializeField] private bool isSolo;

    [Header("Movement Settings")]
    public float maxSpeed = 500f;
    public float minBurstDistance = 50f;
    public float maxBurstDistance = 50f;
    public float burstCooldown = 0.5f;

    [Header("Spiral Settings (Solo)")]
    public float maxSpiralOffset = 30f;
    public float verticalOffsetMultiplier = 1f;
    public float spiralFadeDistance = 150f;

    [Header("Pivot Point System (Paired Movement)")]
    public Transform[] pivotPoints;          // assign 4 transforms via inspector
    private Vector3[] initialPivotOffsets;
    public float pivotMoveSpeed = 10f;       // how quickly enemies move toward their assigned pivot
    public float pivotRandomOffset = 3f;     // how much to randomize pivot position each burst
    private int currentPivotIndex = 0;       // current pivot index for cycling

    [Header("Pivot Rotation Settings")]
    public float pivotRotationSpeed = 180f; // degrees per second

    [Header("Avoidance")]
    public float avoidanceForce = 1000f;
    public float detectionRadius = 40f;
    public LayerMask obstacleMask;

    [Header("Attack Pathfinding")]
    public Transform playerCamera;
    private Vector3 currentPivot;
    private int currentDirectionIndex = -1;
    private List<Vector3> availableDirections;
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground
    private Vector3 lastPlayerForward = Vector3.forward;
    public float pivotChangeInterval = 3f; // every 3 seconds
    private float nextPivotTime = 0f;
    public LayerMask LOSMask;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private Vector3 contactNormal = Vector3.up;
    private float nextBurstTime = 0f;

    public int currentSpiralStep = 0;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;

        velocity = Vector3.zero;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        if (pivotPoints != null && pivotPoints.Length > 0)
        {
            initialPivotOffsets = new Vector3[pivotPoints.Length];
            for (int i = 0; i < pivotPoints.Length; i++)
                initialPivotOffsets[i] = pivotPoints[i].position - transform.position;
        }


        if (enemyA != null && enemyB != null)
            isSolo = false;
        else if (enemyA)
            isSolo = true;
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
        if (enemyA == null) return;

        UpdateDirections();

        CalculateDesiredVelocity();

        AdjustVelocity();

        if (pivot != null)
        {
            Vector3 lookTarget;

            if (isSolo)
            {
                // Solo still faces the player directly
                lookTarget = player != null ? player.transform.position : transform.position + transform.forward;
            }
            else
            {
                // Paired its current pivot point
                lookTarget = currentPivot;
            }

            Vector3 toTarget = (lookTarget - pivot.position).normalized;
            if (toTarget.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toTarget, Vector3.up);
                pivot.rotation = Quaternion.RotateTowards(
                    pivot.rotation,
                    targetRotation,
                    pivotRotationSpeed * Time.fixedDeltaTime
                );
            }
        }


        rb.velocity = velocity;

        if (!isSolo && pivotPoints != null && pivotPoints.Length >= 2)
        {
            Transform pivotA = pivotPoints[currentPivotIndex];
            Transform pivotB = pivotPoints[(currentPivotIndex + 2) % pivotPoints.Length]; // opposite point for symmetry

            if (enemyA != null)
            {
                enemyA.position = Vector3.Lerp(
                    enemyA.position,
                    pivotA.position,
                    pivotMoveSpeed * Time.fixedDeltaTime
                );
            }

            if (enemyB != null)
            {
                enemyB.position = Vector3.Lerp(
                    enemyB.position,
                    pivotB.position,
                    pivotMoveSpeed * Time.fixedDeltaTime
                );
            }
        }
    }

    void CalculateDesiredVelocity()
    {
        if (isSolo)
        {
            Vector3 toPlayer = (player.transform.position - transform.position).normalized;
            //Vector3 avoidanceVector = ProjectOnContactPlane(CalculateObstacleAvoidance());
            Vector3 avoidanceVector = CalculateObstacleAvoidance();

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            float distanceScaler = Mathf.Clamp01(distanceToPlayer / (spiralFadeDistance * 2f)); //smoothly fade spin when getting closer to player

            //force precision when close enough
            if (distanceToPlayer < spiralFadeDistance)
                distanceScaler = 0;

            float spiralMagnitude = maxSpiralOffset * distanceScaler;

            float spiralAngle = currentSpiralStep * Mathf.PI / 2f;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
            Vector3 up = Vector3.up;

            // Tilt spiral by 45 degrees around toPlayer
            Quaternion tilt45 = Quaternion.AngleAxis(45f, toPlayer);
            side = tilt45 * side;
            up = tilt45 * up;

            // Spiral offset pattern
            Vector3 spiralOffset = side * Mathf.Cos(spiralAngle) * spiralMagnitude
                                 + up * Mathf.Sin(spiralAngle) * spiralMagnitude * verticalOffsetMultiplier;

            // Combine movement
            Vector3 targetDir = (toPlayer + avoidanceVector.normalized).normalized;
            float burstDistance = Mathf.Clamp(distanceToPlayer * 0.5f, minBurstDistance, maxBurstDistance);

            Vector3 burstTarget = transform.position + targetDir * burstDistance + spiralOffset;
            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > maxSpeed)
                desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }
        else
        {
            // --- 1. Pick a new pivot periodically ---
            if (Time.time >= nextPivotTime)
            {
                currentPivot = ChoosePivot();
                nextPivotTime = Time.time + pivotChangeInterval;
            }

            // --- 2. Calculate movement toward pivot (same as before) ---
            Vector3 toPivot = (currentPivot - transform.position).normalized;
            Vector3 avoidanceVector = CalculateObstacleAvoidance();

            float distanceToPivot = Vector3.Distance(transform.position, currentPivot);
            float burstDistance = Mathf.Clamp(distanceToPivot * 0.5f, minBurstDistance, maxBurstDistance);

            Vector3 burstTarget = transform.position + (toPivot + avoidanceVector.normalized).normalized * burstDistance;
            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > maxSpeed)
                desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }
    }

    void AdjustVelocity()
    {
        if (Time.time >= nextBurstTime)
        {
            velocity = desiredVelocity;
            nextBurstTime = Time.time + burstCooldown;
            currentSpiralStep++;

            if (!isSolo && pivotPoints != null && pivotPoints.Length > 0)
            {
                // Cycle through pivot points
                currentPivotIndex = (currentPivotIndex + 1) % pivotPoints.Length;

                for (int i = 0; i < pivotPoints.Length; i++)
                {
                    // Random offset per axis
                    Vector3 randomOffset = new Vector3(
                        Random.Range(-pivotRandomOffset, pivotRandomOffset),
                        Random.Range(-pivotRandomOffset, pivotRandomOffset),
                        Random.Range(-pivotRandomOffset, pivotRandomOffset)
                    );

                    // Rotate the base offset by the current pivot rotation
                    Vector3 rotatedOffset = pivot.rotation * initialPivotOffsets[i];

                    // Apply to pivot position
                    pivotPoints[i].position = transform.position + rotatedOffset + randomOffset;
                }
            }

        }
        else
        {
            velocity = Vector3.Lerp(velocity, Vector3.zero, 5f * Time.fixedDeltaTime);
        }
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
                float strength = Mathf.Clamp01((detectionRadius - distance) / detectionRadius);
                totalAvoidance += away.normalized * avoidanceForce * strength;
            }
        }
        return totalAvoidance;
    }

    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
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

    void OnDrawGizmosSelected()
    {
        if (pivotPoints == null || pivotPoints.Length == 0)
            return;

        // Draw each pivot point
        for (int i = 0; i < pivotPoints.Length; i++)
        {
            if (pivotPoints[i] == null)
                continue;

            // Draw a sphere at the pivot point
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pivotPoints[i].position, 0.5f);

            // Optional: draw a line from the enemy center to the pivot
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, pivotPoints[i].position);
            }

            // Optional: draw index label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(pivotPoints[i].position + Vector3.up * 0.5f, i.ToString());
#endif
        }

        // Draw the enemy center
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.3f);
    }
}
