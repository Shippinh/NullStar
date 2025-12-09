using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SniperEnemy : MonoBehaviour
{
    [Header("Target")]
    public SpaceShooterController player;

    [Header("Chase Movement")]
    public float maxSpeed = 10f;
    public float maxAcceleration = 30f;
    public float jetpackAcceleration = 15f;

    [Header("Orbiting Movement")]
    public float orbitMaxSpeed = 6f;
    public float orbitMaxAcceleration = 15f;
    public float orbitVerticalAcceleration = 3f; // vertical accel during orbit (set 0 to ignore)
    [Tooltip("Fraction of orbitMaxSpeed used for sideways orbit movement")]
    public float orbitSpeedFactor = 0.6f;
    public float minRange = 100f;
    public float maxRange = 400f;
    public float baseMinRange, baseMaxRange;
    public float tiltSpeed = 45f; // degrees per second to rotate orbit plane axis
    float distToPlayer;

    [Header("Avoidance")]
    public float avoidanceForce = 5f;
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    [Header("Turret Reference")]
    public TurretBehavior turretRef;

    [Header("Other")]
    public bool canAct = true;
    private float actSlowdownTimer = 0f;
    [SerializeField] private float actSlowdownDuration = 1f; // how long it takes to fully stop/start acting again

    public bool canMove = true;

    [SerializeField] private Rigidbody rb;
    [SerializeField] private List<Collider> nearbyObstacles = new List<Collider>();
    [SerializeField] private Vector3 velocity;
    [SerializeField] private Vector3 desiredVelocity;
    [SerializeField] private Vector3 contactNormal = Vector3.up;
    [SerializeField] private float tiltAngle = 0f;

    // Current acceleration type in Update, either orbit of follow
    private float currentAcceleration;
    private float currentVerticalAcceleration;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        baseMinRange = minRange;
        baseMaxRange = maxRange;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;

        turretRef.InitializeTurret(player, minRange, maxRange);
    }

    void Update()
    {
        if (!player) return;

        (minRange, maxRange) = player.CalculateDynamicOrbit(baseMinRange, baseMaxRange, baseMaxRange - baseMinRange);

        // Smooth act slowdown independent of weapon timers
        float targetSlowdown = canAct ? 1f : 0f;
        actSlowdownTimer = Mathf.MoveTowards(actSlowdownTimer, targetSlowdown, Time.deltaTime / actSlowdownDuration);
        float actSlowdownFactor = Mathf.SmoothStep(0f, 1f, actSlowdownTimer);

        if (canAct)
        {
            CalculateDesiredVelocity(distToPlayer);
            turretRef.UpdateAiming();
            turretRef.HandleShooting(distToPlayer);

            if (turretRef.stopWhenShooting && (turretRef.isChargingShot || turretRef.isSendingShot))
            {
                float t = Mathf.Clamp01(turretRef.GetWeaponChargeDurationTimer() / turretRef.weaponChargeDuration);
                float easeFactor = 1f - Mathf.Pow(1f - t, 2f); // quadratic easing out
                desiredVelocity *= (1f - easeFactor); // gradually reduce to zero while charging
            }
        }
        else
        {
            // Don't update logic, just fade motion via actSlowdownFactor
            desiredVelocity *= actSlowdownFactor;
        }
    }

    void FixedUpdate()
    {
        if (!player) return;

        distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (canMove)
        {
            AdjustVelocity(currentAcceleration);
            AdjustAirVelocity(currentVerticalAcceleration);
            rb.velocity = velocity;
        }
    }

    // Calculates desiredVelocity and acceleration values based on chase or orbit behavior.
    void CalculateDesiredVelocity(float distanceToPlayer)
    {
        Vector3 directionToPlayer = player.transform.position - transform.position;
        Vector3 avoidanceVector = CalculateObstacleAvoidance();

        if (distanceToPlayer > maxRange * 1.2f) // Follow mode
        {
            Vector3 combinedDir = (directionToPlayer.normalized + avoidanceVector).normalized;
            desiredVelocity = combinedDir * maxSpeed;

            currentAcceleration = maxAcceleration;
            currentVerticalAcceleration = jetpackAcceleration;
        }
        else if (distanceToPlayer > minRange && distanceToPlayer <= maxRange) // Orbit mode
        {
            tiltAngle += tiltSpeed * Time.deltaTime;
            Vector3 orbitNormal = Quaternion.AngleAxis(tiltAngle, Vector3.forward) * Vector3.up;
            Vector3 tangent = Vector3.Cross(orbitNormal, directionToPlayer).normalized;

            // Smooth range correction
            float sweetSpotMidpoint = (minRange + maxRange) / 2f;
            float offset = distanceToPlayer - sweetSpotMidpoint;
            Vector3 rangeAdjust = directionToPlayer.normalized * orbitMaxSpeed * orbitSpeedFactor * (offset / (maxRange - minRange));

            Vector3 tangentVelocity = tangent * (orbitMaxSpeed * orbitSpeedFactor);

            // Combine everything
            desiredVelocity = tangentVelocity + rangeAdjust + avoidanceVector;

            // Clamp
            if (desiredVelocity.magnitude > orbitMaxSpeed)
                desiredVelocity = desiredVelocity.normalized * orbitMaxSpeed;

            currentAcceleration = orbitMaxAcceleration;
            currentVerticalAcceleration = orbitVerticalAcceleration;
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
                // Scale avoidance inversely by distance (closer obstacles push stronger)
                // Using a smooth falloff (distance / detectionRadius)
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

    void AdjustVelocity(float acceleration)
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), acceleration * Time.fixedDeltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), acceleration * Time.fixedDeltaTime);
    }

    void AdjustAirVelocity(float acceleration)
    {
        Vector3 yAxis = Vector3.up;

        float currentY = Vector3.Dot(velocity, yAxis);

        float deltaY = desiredVelocity.y - currentY;

        velocity += yAxis * Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), acceleration * Time.fixedDeltaTime);
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

    void OnDrawGizmos()
    {
        if (!player) return;

        // --- Sweet spot ranges ---
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(player.transform.position, minRange);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(player.transform.position, maxRange);

        // --- Orbit plane circle (midpoint) ---
        float sweetSpotMidpoint = (minRange + maxRange) / 2f;
        Vector3 orbitNormal = Quaternion.AngleAxis(tiltAngle, Vector3.forward) * Vector3.up;
        Vector3 startVector = Vector3.ProjectOnPlane(Vector3.right, orbitNormal).normalized * sweetSpotMidpoint;

        int segments = 64;
        Vector3 prevPoint = player.transform.position + startVector;
        Gizmos.color = Color.green;
        for (int i = 1; i <= segments; i++)
        {
            float angle = (360f / segments) * i;
            Quaternion rot = Quaternion.AngleAxis(angle, orbitNormal);
            Vector3 nextPoint = player.transform.position + rot * startVector;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        // --- Predicted orbit path (next few seconds) ---
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 simPos = transform.position;
            Vector3 simVel = desiredVelocity;

            float simStep = 0.2f;
            int simSteps = 40;

            for (int i = 0; i < simSteps; i++)
            {
                Vector3 nextPos = simPos + simVel * simStep;
                Gizmos.DrawLine(simPos, nextPos);
                simPos = nextPos;
            }
        }
    }
}
