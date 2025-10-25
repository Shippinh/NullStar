using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BarrierRammerEnemyCentralized : MonoBehaviour
{
    [Header("References")]
    public Transform player;
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

    void FixedUpdate()
    {
        if (enemyA == null) return;

        CalculateDesiredVelocity();

        AdjustVelocity();

        if (pivot != null && player != null)
        {
            Vector3 toPlayer = (player.position - pivot.position).normalized;
            if (toPlayer.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(toPlayer, Vector3.up);
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
            Vector3 avoidanceVector = ProjectOnContactPlane(CalculateObstacleAvoidance());
            //Vector3 avoidanceVector = CalculateObstacleAvoidance();

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
            Vector3 toPlayer = (player.transform.position - transform.position).normalized;
            //Vector3 avoidanceVector = ProjectOnContactPlane(CalculateObstacleAvoidance());
            Vector3 avoidanceVector = CalculateObstacleAvoidance();

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            // Combine movement
            Vector3 targetDir = (toPlayer + avoidanceVector.normalized).normalized;
            float burstDistance = Mathf.Clamp(distanceToPlayer * 0.5f, minBurstDistance, maxBurstDistance);

            Vector3 burstTarget = transform.position + targetDir * burstDistance;
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
