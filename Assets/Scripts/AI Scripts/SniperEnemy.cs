using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class SniperEnemy : MonoBehaviour
{
    [Header("Target & Movement")]
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

    [Header("Shooting")]
    public float weaponChargeDuration = 1.5f; // THIS ONE IS INTERNAL, DEFINES HOW MUCH TIME BEFORE SHOOTING HAPPENS PASSES, used for aiming
    [SerializeField] private float weaponChargeDurationTimer;
    [SerializeField] private float weaponShootDuration = 0.5f; // this is external, defined by shot count * fire rate or sequenceDuration, depending on the limit. If no limit - just uses the current fire rate value
    [SerializeField] private float weaponShootDurationTimer;
    [SerializeField] private float weaponCooldownDuration = 3f; // this one is external, uses emitter's recharge duration
    [SerializeField] private float weaponCooldownDurationTimer;

    public bool slightyRandomizeDurations = true;
    public float randomDurationsRange = 0.1f;
    public bool randomizeInitialWeaponChargeTimers = true;

    public bool randomizeStopWhenShooting = false;
    public bool stopWhenShooting = false;

    public bool isShooting;
    public bool isChargingShot;
    public bool isSendingShot;
    public bool isShootingOnCD;

    [SerializeField] private float projectileSpeed = 300f;

    public LayerMask losCheck;

    [Header("Aiming")]
    private float[] aimStrengths;
    public float aimTimeToPerfect = 2f; // time in seconds to reach perfect aim
    [SerializeField, Range(0f, 180f)] public float aimResetThreshold = 30f;
    private Vector3[] previousPredictedDirs;
    private Vector3 aimedDir; // updated every frame in UpdateAiming()


    [Header("Other")]
    public bool canAct = true;
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

    [Header("Emitter Stuff")]
    public ProjectileEmittersController projectileEmittersControllerRef;
    [SerializeField] private Transform[] gunsPositions;
    private Vector3[] aimedDirs; // store per-gun aimed directions

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

        projectileEmittersControllerRef = GetComponentInChildren<ProjectileEmittersController>();
        gunsPositions = projectileEmittersControllerRef.GetGunsArray();

        //weaponChargeDuration = projectileEmittersControllerRef.rechargeTime; // handled internally, this makes no sense
        weaponShootDuration = projectileEmittersControllerRef.GetSequenceDuration() + 0.1f; // slight offset to properly finish shooting
        weaponCooldownDuration = projectileEmittersControllerRef.rechargeTime;

        weaponChargeDurationTimer = 0f;
        weaponShootDurationTimer = 0f;
        weaponCooldownDurationTimer = 0f;

        if (slightyRandomizeDurations)
        {
            weaponChargeDuration = Random.Range(weaponChargeDuration - randomDurationsRange, weaponChargeDuration + randomDurationsRange);
            weaponCooldownDuration = Random.Range(weaponCooldownDuration - randomDurationsRange, weaponCooldownDuration + randomDurationsRange);
        }

        if (randomizeInitialWeaponChargeTimers)
        {
            weaponChargeDurationTimer = Random.Range(0, weaponChargeDuration);
        }

        if (randomizeStopWhenShooting)
        {
            int rand50 = Random.Range(0, 2);
            if (rand50 == 1)
            {
                stopWhenShooting = true;
            }
            else
            {
                stopWhenShooting = false;
            }
        }

        projectileSpeed = ObjectPool.Instance.GetPooledObject(projectileEmittersControllerRef.projectileTag).GetComponent<SniperProjectile>().speed;
    }

    void Update()
    {
        if (!player) return;

        (minRange, maxRange) = player.CalculateDynamicOrbit(baseMinRange, baseMaxRange, baseMaxRange - baseMinRange);

        if (canAct)
        {
            CalculateDesiredVelocity(distToPlayer);

            UpdateAiming();
            HandleShooting(distToPlayer);

            if (stopWhenShooting)
            {
                // Gradual slowdown during charge
                if (isChargingShot || isSendingShot)
                {
                    float t = Mathf.Clamp01(weaponChargeDurationTimer / weaponChargeDuration);
                    float easeFactor = 1f - Mathf.Pow(1f - t, 2f); // quadratic easing out
                    desiredVelocity *= (1f - easeFactor); // gradually reduce to zero
                }
            }
        }
        else
        {
            float t = Mathf.Clamp01(weaponChargeDurationTimer / weaponChargeDuration);
            float easeFactor = 1f - Mathf.Pow(1f - t, 2f); // quadratic easing out
            desiredVelocity *= (1f - easeFactor); // gradually reduce to zero
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

    // handles all shooting related timers and the brief stop before shooting
    void HandleShooting(float distanceToPlayer)
    {
        // --- Check sweet spot and line of sight ---
        bool inSweetSpot = distanceToPlayer >= minRange && distanceToPlayer <= maxRange;
        bool hasLOS = HasLineOfSight();

        if (!inSweetSpot || !hasLOS)
        {
            if (isShooting) ResetShooting(); // cancel immediately if either condition fails
            return;
        }

        // --- Shooting sequence ---
        if (inSweetSpot && hasLOS && !isShooting)
        {
            isShooting = true;
            isChargingShot = true;
            weaponChargeDurationTimer = 0f;
            Debug.Log("Weapon charge initiated");
        }

        if (isShooting)
        {
            if (isChargingShot)
            {
                weaponChargeDurationTimer += Time.deltaTime;
                if (weaponChargeDurationTimer >= weaponChargeDuration)
                {
                    Debug.Log("Charge complete");
                    isChargingShot = false;
                    isSendingShot = true;
                    weaponShootDurationTimer = 0f;
                }
            }

            if (isSendingShot)
            {
                weaponShootDurationTimer += Time.deltaTime;

                projectileEmittersControllerRef.RequestSoftStart();

                if (weaponShootDurationTimer >= weaponShootDuration)
                {
                    projectileEmittersControllerRef.ForceStopSequence(); // NEW: use the emitter attack sequence
                    isSendingShot = false;
                    isShootingOnCD = true;
                    weaponCooldownDurationTimer = 0f;
                }
            }

            if (isShootingOnCD)
            {
                weaponCooldownDurationTimer += Time.deltaTime;
                if (weaponCooldownDurationTimer >= weaponCooldownDuration)
                {
                    Debug.Log("Enemy weapon reloaded");
                    isShootingOnCD = false;
                    isShooting = false;
                    weaponChargeDurationTimer = 0f;
                }
            }
        }
    }


    void ResetShooting()
    {
        // Reset state flags
        isShooting = false;
        isChargingShot = false;
        isSendingShot = false;
        isShootingOnCD = false;

        // Reset timers
        weaponChargeDurationTimer = 0f;
        weaponShootDurationTimer = 0f;
        weaponCooldownDurationTimer = 0f;
        Debug.Log("Shooting sequence reset.");
    }

    void UpdateAiming()
    {
        if (!player || gunsPositions == null || gunsPositions.Length == 0) return;

        if (aimedDirs == null || aimedDirs.Length != gunsPositions.Length)
            aimedDirs = new Vector3[gunsPositions.Length];

        if (aimStrengths == null || aimStrengths.Length != gunsPositions.Length)
            aimStrengths = new float[gunsPositions.Length];

        if (previousPredictedDirs == null || previousPredictedDirs.Length != gunsPositions.Length)
            previousPredictedDirs = new Vector3[gunsPositions.Length];

        for (int i = 0; i < gunsPositions.Length; i++)
        {
            Transform gun = gunsPositions[i];
            Vector3 gunPos = gun.position;

            Vector3 playerPos = player.transform.position;
            Vector3 playerVel = player.body ? player.body.velocity : Vector3.zero;

            // Predict position
            float distance = Vector3.Distance(gunPos, playerPos);
            float timeToHit = distance / projectileSpeed;
            Vector3 predictedPos = playerPos + playerVel * timeToHit;

            //Vector3 predictedDir = (predictedPos - gunPos).normalized; // linear prediction, should be balanced with lead aim time
            Vector3 predictedDir = CalculateInterceptDirection(gunPos, playerPos, playerVel, projectileSpeed); //perfect aim, 

            // Reset aim strength on sudden change
            if (Vector3.Angle(previousPredictedDirs[i], predictedDir) > aimResetThreshold) // 30° sudden change threshold
            {
                aimStrengths[i] = 0f;
            }

            // Calculate progress per frame based on total aim time
            if (aimTimeToPerfect > 0f)
            {
                float deltaStrength = Time.deltaTime / aimTimeToPerfect;
                aimStrengths[i] = Mathf.Clamp01(aimStrengths[i] + deltaStrength);
            }
            else
            {
                aimStrengths[i] = 1f; // instant perfect aim
            }

            // Interpolate towards predicted direction
            Vector3 finalDir = Vector3.Slerp(aimedDirs[i], predictedDir, aimStrengths[i]);
            aimedDirs[i] = finalDir;
            previousPredictedDirs[i] = predictedDir;

            // Rotate gun relative to its parent
            Quaternion localTargetRot = Quaternion.Inverse(gun.parent.rotation) * Quaternion.LookRotation(finalDir);
            gun.localRotation = localTargetRot;
        }
    }

    Vector3 CalculateInterceptDirection(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVel, float projectileSpeed)
    {
        Vector3 displacement = targetPos - shooterPos;
        float a = Vector3.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(displacement, targetVel);
        float c = Vector3.Dot(displacement, displacement);

        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0 || Mathf.Approximately(a, 0f))
        {
            // No solution or projectile speed equals target speed → aim directly at target
            return displacement.normalized;
        }

        float sqrtDisc = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDisc) / (2f * a);
        float t2 = (-b - sqrtDisc) / (2f * a);

        float t = Mathf.Min(t1, t2);
        if (t < 0f) t = Mathf.Max(t1, t2);
        if (t < 0f) return displacement.normalized; // target can't be hit → aim directly

        Vector3 interceptPoint = targetPos + targetVel * t;
        return (interceptPoint - shooterPos).normalized;
    }




    bool HasLineOfSight()
    {
        if (!player) return false;

        Vector3 origin = transform.position;
        Vector3 dir = (player.transform.position - origin).normalized;
        float dist = Vector3.Distance(origin, player.transform.position);

        // If ray hits something in obstacleMask before reaching the player → blocked
        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, losCheck, QueryTriggerInteraction.Ignore))
        {
           // Debug.Log("False");
            return false;
        }
        //Debug.Log("True");
        return true;
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

        // --- Aim direction gizmos ---
        if (gunsPositions != null && aimedDirs != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < gunsPositions.Length; i++)
            {
                if (gunsPositions[i] != null)
                {
                    Vector3 gunPos = gunsPositions[i].position;
                    Vector3 dir = aimedDirs[i].normalized;
                    Gizmos.DrawLine(gunPos, gunPos + dir * 300f); // length 10 units for visualization
                    Gizmos.DrawSphere(gunPos + dir * 300f, 0.2f);  // small sphere at tip
                }
            }
        }
    }
}
