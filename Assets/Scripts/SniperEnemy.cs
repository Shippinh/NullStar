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
    public float minRange = 1000f;
    public float maxRange = 1500f;
    public float tiltSpeed = 45f; // degrees per second to rotate orbit plane axis
    float distToPlayer;

    [Header("Avoidance")]
    public float avoidanceForce = 5f;
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    [Header("Shooting")]
    public float weaponChargeDuration = 1.5f;
    public float weaponChargeDurationTimer;
    public float weaponShootDuration = 0.5f;
    public float weaponShootDurationTimer;
    public float weaponCooldownDuration = 3f;
    public float weaponCooldownDurationTimer;

    public bool slightyRandomizeDurations = true;
    public float randomDurationsRange = 0.1f;
    public bool randomizeInitialWeaponChargeTimers = true;

    public bool randomizeStopWhenShooting = false;
    public bool stopWhenShooting = false;

    public bool isShooting;
    public bool isChargingShot;
    public bool isSendingShot;
    public bool isShootingOnCD;

    [Header("Aiming")]
    public LineRenderer aimLine;
    public float aimSmoothing = 2f;       // how fast the aim strength grows
    public float aimResetThreshold = 0.7f; // dot threshold for detecting drastic player direction changes
    public float aimStrength = 0f;         // grows from 0 to 1
    private Vector3 aimedDir; // updated every frame in UpdateAiming()


    [Header("Other")]
    public bool canAct = true;
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

        aimLine = GetComponent<LineRenderer>();

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;

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
    }

    void Update()
    {
        if (!player) return;

        player.CalculateAISafeOrbitCustom(minRange, maxRange);

        if (canAct)
        {
            CalculateDesiredVelocity(distToPlayer);

            HandleShooting(distToPlayer);

            if (stopWhenShooting)
            {
                // Gradual slowdown during charge
                if (isChargingShot)
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

        AdjustVelocity(currentAcceleration);
        AdjustAirVelocity(currentVerticalAcceleration);
        if (isChargingShot || isSendingShot)
        {
            UpdateAiming();
        }
        else
        {
            if (aimLine) aimLine.enabled = false;
            aimStrength = 0f; // reset after done shooting
        }

        rb.velocity = velocity;
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
        else if (distanceToPlayer > minRange && distanceToPlayer <= maxRange) // Orbit mode (inside the sweet spot)
        {
            tiltAngle += tiltSpeed * Time.deltaTime;

            // Always rotate around the player's up axis, not world forward
            Vector3 orbitNormal = Quaternion.AngleAxis(tiltAngle, Vector3.forward) * Vector3.up;

            // Tangent direction = perpendicular to player vector in a stable frame
            Vector3 tangent = Vector3.Cross(orbitNormal, directionToPlayer).normalized;

            Vector3 rangeAdjust = Vector3.zero;
            float rangeSpeedFactor = 0.4f;

            // **MODIFIED:** Instead of targeting maxRange, target the midpoint of the sweet spot
            float sweetSpotMidpoint = (minRange + maxRange) / 2f;

            // **MODIFIED:** If the player is closer than the midpoint, move away
            if (distanceToPlayer < sweetSpotMidpoint)
            {
                rangeAdjust = -directionToPlayer.normalized * orbitMaxSpeed * rangeSpeedFactor;
            }
            // **MODIFIED:** If the player is farther than the midpoint, move closer
            else if (distanceToPlayer > sweetSpotMidpoint)
            {
                rangeAdjust = directionToPlayer.normalized * orbitMaxSpeed * rangeSpeedFactor;
            }

            Vector3 tangentVelocity = tangent * (orbitMaxSpeed * orbitSpeedFactor);
            Vector3 combinedVelocity = tangentVelocity + rangeAdjust + avoidanceVector;

            desiredVelocity = combinedVelocity.normalized * orbitMaxSpeed;

            currentAcceleration = orbitMaxAcceleration;
            currentVerticalAcceleration = orbitVerticalAcceleration;
        }

        /*se if (distanceToPlayer <= maxRange)
        {
            tiltAngle += tiltSpeed * Time.deltaTime;

            Vector3 rotationAxis = Vector3.forward;
            Vector3 orbitNormal = Quaternion.AngleAxis(tiltAngle, rotationAxis) * Vector3.up;

            orbitNormal.Normalize();

            Vector3 tangent = Vector3.Cross(orbitNormal, directionToPlayer).normalized;
            Vector3 rangeAdjust = Vector3.zero;

            float rangeSpeedFactor = 0.4f;
            if (distanceToPlayer < minRange)
                rangeAdjust = -directionToPlayer.normalized * orbitMaxSpeed * rangeSpeedFactor;
            else if (distanceToPlayer > maxRange * 0.95f)
                rangeAdjust = directionToPlayer.normalized * orbitMaxSpeed * rangeSpeedFactor;

            Vector3 tangentVelocity = tangent * (orbitMaxSpeed * orbitSpeedFactor);
            Vector3 combinedVelocity = tangentVelocity + rangeAdjust + avoidanceVector;
            desiredVelocity = combinedVelocity.normalized * orbitMaxSpeed;

            currentAcceleration = orbitMaxAcceleration;
            currentVerticalAcceleration = orbitVerticalAcceleration;
        }*/
    }

    Vector3 CalculateObstacleAvoidance()
    {
        Vector3 avoidanceVector = Vector3.zero;

        foreach (var col in nearbyObstacles)
        {
            if (!col) continue;
            Vector3 point = col.ClosestPoint(transform.position);
            Vector3 away = (transform.position - point);
            float dist = away.magnitude;

            if (dist > 0f)
            {
                avoidanceVector += away.normalized * (1f / dist);
            }
        }

        if (avoidanceVector != Vector3.zero)
        {
            avoidanceVector = avoidanceVector.normalized * avoidanceForce;
        }

        return avoidanceVector;
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
        //if in the sweet spot
        if (distanceToPlayer >= minRange && distanceToPlayer <= maxRange && !isShooting)
        {
            isShooting = true;
            isChargingShot = true;
            weaponChargeDurationTimer = 0f;
            Debug.Log("Weapon charge initiated");
        }
        // also need condition to prevent shooting when the player gets away from the sweet spot

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
                if (weaponShootDurationTimer >= weaponShootDuration)
                {
                    Debug.Log("Enemy shooting!");

                    isSendingShot = false;
                    
                    Shoot();
                    
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

    }

    void Shoot()
    {
        Vector3 origin = transform.position;

        if (Physics.Raycast(origin, aimedDir, out RaycastHit hit, 5000f))
        {
            if (hit.collider.CompareTag("Player"))
            {
                Debug.Log("Sniper hit the player!");
                // player.TakeDamage(damageAmount);
            }
        }

        // Visualize shot
        if (aimLine)
        {
            aimLine.startColor = Color.red;
            aimLine.endColor = Color.red;
            aimLine.SetPosition(0, origin);
            aimLine.SetPosition(1, origin + aimedDir * 5000f);
            StartCoroutine(ResetAimLineColor());
        }
    }


    IEnumerator ResetAimLineColor()
    {
        yield return new WaitForSeconds(0.2f);
        if (aimLine)
        {
            aimLine.startColor = Color.green;
            aimLine.endColor = Color.green;
        }
    }

    void UpdateAiming()
    {
        if (!player || !aimLine) return;

        Vector3 playerPos = player.transform.position;
        Vector3 playerVel = player.body ? player.body.velocity : Vector3.zero;

        float projectileSpeed = 300f; // tune this
        float distance = Vector3.Distance(transform.position, playerPos);
        float timeToHit = distance / projectileSpeed;
        Vector3 predictedPos = playerPos + playerVel * timeToHit;

        // Save aimedDir for shooting
        aimedDir = (predictedPos - transform.position).normalized;

        // Update line renderer
        aimLine.enabled = true;
        aimLine.SetPosition(0, transform.position);

        if (Physics.Raycast(transform.position, aimedDir, out RaycastHit hit, 1200f))
        {
            aimLine.SetPosition(1, hit.point);
        }
        else
        {
            aimLine.SetPosition(1, transform.position + aimedDir * 1200f);
        }

        // Smoothly rotate toward aim
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(aimedDir),
            Time.deltaTime * 3f
        );
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

    /*void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, minRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, maxRange);
    }*/
}
