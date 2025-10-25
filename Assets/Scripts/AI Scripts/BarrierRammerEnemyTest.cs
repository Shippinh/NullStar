using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BarrierRammerEnemyTest : MonoBehaviour
{
    [Header("Target & Movement")]
    public SpaceShooterController player;
    public float maxSpeed = 100f;
    public float maxAcceleration = 80f;
    public bool canMove = true;

    [Header("Burst Settings")]
    public float minBurstDistance = 10f;
    public float maxBurstDistance = 50f;
    public float burstCooldown = 0.5f;

    [Header("Spiral Settings")]
    public float maxSpiralOffset = 10f;
    public float verticalOffsetMultiplier = 0.5f;
    public float spiralFadeDistance = 50f;

    [Header("Avoidance")]
    public float avoidanceForce = 1000f;
    public float detectionRadius = 40f;
    public LayerMask obstacleMask;

    [Header("Pair Sync (Vortex)")]
    public BarrierRammerEnemyTest partner;
    public float syncDistance = 100f;
    public bool isLeader = false;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private Vector3 contactNormal = Vector3.up;
    private float nextBurstTime = 0f;

    public int currentSpiralStep = 0;
    private Vector3 vortexCenter;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        if (isLeader)
            currentSpiralStep = 2;

        velocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        if (!canMove) return;

        // Update forced pair sync
        if (partner)
            UpdatePairSync();

        // Burst movement logic
        if (Time.time >= nextBurstTime)
        {
            CalculateDesiredVelocity();

            velocity = desiredVelocity;
            nextBurstTime = Time.time + burstCooldown;
            currentSpiralStep++;
        }
        else
        {
            velocity = Vector3.Lerp(velocity, Vector3.zero, 5f * Time.fixedDeltaTime);
        }

        rb.velocity = velocity;
    }

    void UpdatePairSync()
    {
        if (!partner || !partner.canMove) return;

        float dist = Vector3.Distance(transform.position, partner.transform.position);
        if (dist > syncDistance) return;

        // Compute shared vortex center
        vortexCenter = (transform.position + partner.transform.position) * 0.5f;

        // Leader drives burst/movement; partner mirrors
        if (isLeader)
        {
            // Compute mirrored position for partner
            Vector3 offset = transform.position - vortexCenter;
            Vector3 mirroredOffset = -offset;
            Vector3 desiredPartnerPos = vortexCenter + mirroredOffset;

            // Corrective velocity to keep partner exactly mirrored
            Vector3 correction = (desiredPartnerPos - partner.transform.position) / Time.fixedDeltaTime;
            partner.rb.velocity = correction;

            // Smoothly align rotation
            Quaternion targetRot = Quaternion.LookRotation((player.transform.position - vortexCenter).normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.fixedDeltaTime);
            partner.transform.rotation = Quaternion.Slerp(partner.transform.rotation, targetRot, 5f * Time.fixedDeltaTime);
        }
        else
        {
            // Partner does nothing; leader handles movement
        }
    }

    void CalculateDesiredVelocity()
    {
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;
        Vector3 avoidanceVector = CalculateObstacleAvoidance();

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        float distanceScaler = Mathf.Clamp01(distanceToPlayer / spiralFadeDistance);
        float spiralMagnitude = maxSpiralOffset * distanceScaler;

        // Use vortex center influence when synced
        if (partner)
        {
            float dist = Vector3.Distance(transform.position, partner.transform.position);
            if (dist < syncDistance)
            {
                Vector3 toCenter = (vortexCenter - transform.position).normalized;
                toPlayer = Vector3.Lerp(toPlayer, toCenter, 0.5f); // bias toward vortex center
            }
        }

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
}
