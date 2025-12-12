using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretBehavior : MonoBehaviour
{
    [Header("Target")]
    public SpaceShooterController player;

    public enum AimType
    {
        SmoothRotation = 0,         // The least expensive, the dumbest
        InterceptPrediction = 1,    // The most expensive, eventually hits the target if they choose not to change direction
        LinearPrediction = 2,       // The middle ground, hits the target when specific condition applied
        UpwardConePrediction = 3
    }

    [Header("Aim Type")]
    public AimType aimType;

    [Header("State Timers")]
    public float weaponChargeDuration = 1.5f;                   // This is set by the user
    [SerializeField] private float weaponChargeDurationTimer;
    [SerializeField] private float weaponShootDuration = 0.5f;  // defined by shot count * fire rate or sequenceDuration, depending on the limit. If no limit - just uses the current fire rate value
    [SerializeField] private float weaponShootDurationTimer;
    [SerializeField] private float weaponCooldownDuration = 3f; // uses emitter's recharge duration
    [SerializeField] private float weaponCooldownDurationTimer;

    [Header("Turret Range")]
    public float minShootingRange;
    public float maxShootingRange;
    public float minShootingRangeIncrement = 0f;
    public float maxShootingRangeIncrement = 500f;

    [Header("Randomization")]
    public bool slightyRandomizeDurations = true;
    public float randomDurationsRange = 0.1f;
    public bool randomizeInitialWeaponChargeTimers = true;

    public bool randomizeStopWhenShooting = false;

    [Header("States")]
    public bool isShooting;
    public bool isChargingShot;
    public bool isSendingShot;
    public bool isShootingOnCD;

    [Header("Line-of-Sight check")]
    public LayerMask losCheck;

    [Header("Aiming")]
    public float aimTimeToPerfect = 2f; // time in seconds to reach perfect aim
    private float[] aimStrengths;
    [SerializeField, Range(0f, 180f)] public float aimResetThreshold = 30f;
    private Vector3[] previousPredictedDirs;
    private Vector3 aimedDir; // updated every frame in UpdateAiming()
    [SerializeField] private float projectileSpeed = 300f;
    public float aimSmoothing = 2f;
    public float aimLeadTime = 0.8f;

    [Header("Upward Cone Aiming")]
    [Range(0f, 89f)] public float coneMinAngle = 10f;   // minimum upward pitch
    [Range(0f, 89f)] public float coneMaxAngle = 45f;   // maximum upward pitch
    public float coneBlendStrength = 1f;               // 1 = fully clamp, <1 = blend w/ predicted
    public bool hasAngle = true; // hard limit shooting

    [Header("Emitter Stuff")]
    public ProjectileEmittersController projectileEmittersControllerRef;
    [SerializeField] private Transform[] gunsPositions;
    private Vector3[] aimedDirs; // store per-gun aimed directions

    [Header("Extra Options")]
    public bool stopWhenShooting = false;
    public bool playerOverboostSimpleAim = false; // makes the turret use simple aiming when using InterceptPrediction
    [SerializeField] private bool canAct = true;

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (canAct)
        {
            UpdateAiming();
            HandleShooting();
        }
    }

    public void InitializeEnemyTurret(SpaceShooterController targetPtr, float minShootingRangePtr, float maxShootingRangePtr)
    {
        player = targetPtr;
        minShootingRange = minShootingRangePtr;
        maxShootingRange = maxShootingRangePtr;

        Initialize();
    }

    private void Initialize()
    {
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

    public void SyncState(bool canActPtr)
    {
        canAct = canActPtr;
    }

    public float GetWeaponChargeDurationTimer()
    {
        return weaponChargeDurationTimer;
    }

    // handles all shooting related timers and the brief stop before shooting
    public void HandleShooting()
    {
        if (!hasAngle)
        {
            if (isShooting) ResetShooting();
            projectileEmittersControllerRef.ForceStopSequence();
            return;
        }

        // --- Check sweet spot and line of sight ---
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        bool inSweetSpot = distanceToPlayer >= minShootingRange + minShootingRangeIncrement
                            && distanceToPlayer <= maxShootingRange + maxShootingRangeIncrement;
        bool hasLOS = HasLineOfSight();

        if (!inSweetSpot || !hasLOS)
        {
            if (isShooting) ResetShooting(); // cancel immediately if any condition fails
            projectileEmittersControllerRef.ForceStopSequence();
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
                    projectileEmittersControllerRef.ForceStopSequence();
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



    public void ResetShooting()
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

    public void UpdateAiming()
    {
        if (!player || gunsPositions == null || gunsPositions.Length == 0) return;

        switch (aimType)
        {
            case AimType.InterceptPrediction:
                PreciserAiming();
                break;

            case AimType.LinearPrediction:
                PreciserAiming();
                break;

            case AimType.UpwardConePrediction:
                UpwardConeAiming();
                break;

            case AimType.SmoothRotation:
                SmoothRotationAiming();
                break;
        }
    }


    private void PreciserAiming()
    {
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
            Vector3 playerVel = player.velocity;

            // Predict position
            float distance = Vector3.Distance(gunPos, playerPos);
            float timeToHit = distance / projectileSpeed;

            Vector3 predictedPos = Vector3.zero;
            Vector3 predictedDir = Vector3.zero;

            bool useIntercept =
            !playerOverboostSimpleAim && aimType == AimType.InterceptPrediction ||
            (playerOverboostSimpleAim && !player.overboostMode && aimType == AimType.InterceptPrediction);


            // aimLeadTime = 0.8 good vs multidirectional movement
            // aimLeadTime = 0.5 good vs constant cardinal direction movement
            if (useIntercept)
            {
                predictedPos = playerPos + playerVel * timeToHit;
                predictedDir = CalculateInterceptDirection(gunPos, playerPos, playerVel, projectileSpeed);
            }
            else
            {
                predictedPos = playerPos + playerVel * (timeToHit + aimLeadTime);
                predictedDir = (predictedPos - gunPos).normalized;
            }

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

    private void SmoothRotationAiming()
    {
        if (!player) return;

        Vector3 playerPos = player.transform.position;
        Vector3 playerVel = player.body ? player.body.velocity : Vector3.zero;

        float projectileSpeed = 300f;
        float distance = Vector3.Distance(transform.position, playerPos);
        float timeToHit = distance / projectileSpeed;
        Vector3 predictedPos = playerPos + playerVel * timeToHit;

        aimedDir = (predictedPos - transform.position).normalized;

        // Rotate smoothly toward aim
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(aimedDir),
            Time.deltaTime * aimSmoothing
        );
    }

    private void UpwardConeAiming()
    {
        if (aimedDirs == null || aimedDirs.Length != gunsPositions.Length)
            aimedDirs = new Vector3[gunsPositions.Length];

        if (aimStrengths == null || aimStrengths.Length != gunsPositions.Length)
            aimStrengths = new float[gunsPositions.Length];

        if (previousPredictedDirs == null || previousPredictedDirs.Length != gunsPositions.Length)
            previousPredictedDirs = new Vector3[gunsPositions.Length];

        bool anyGunOutOfCone = false; // flag for canShoot

        for (int i = 0; i < gunsPositions.Length; i++)
        {
            Transform gun = gunsPositions[i];
            Vector3 gunPos = gun.position;

            Vector3 targetPos = player.transform.position;
            Vector3 toTarget = targetPos - gunPos;

            // Predictive aiming (blend with simple direction)
            Vector3 predictedDir = toTarget.normalized;

            // Reset aim strength if sudden change
            if (Vector3.Angle(previousPredictedDirs[i], predictedDir) > aimResetThreshold)
                aimStrengths[i] = 0f;

            if (aimTimeToPerfect > 0f)
            {
                float deltaStrength = Time.deltaTime / aimTimeToPerfect;
                aimStrengths[i] = Mathf.Clamp01(aimStrengths[i] + deltaStrength);
            }
            else
            {
                aimStrengths[i] = 1f;
            }

            // Interpolate for smooth aiming
            Vector3 blendedDir = Vector3.Slerp(aimedDirs[i], predictedDir, aimStrengths[i]);

            // --- Clamp within cone ---
            Vector3 coneAxis = gun.parent.up; // cone points upward relative to parent
            float angleFromAxis = Vector3.Angle(blendedDir, coneAxis);

            if (angleFromAxis < coneMinAngle || angleFromAxis > coneMaxAngle)
            {
                anyGunOutOfCone = true; // mark that shooting should be blocked
                blendedDir = ClampDirectionToCone(blendedDir, coneAxis, coneMinAngle, coneMaxAngle);
            }

            Vector3 finalDir = blendedDir;

            // Store for next frame
            aimedDirs[i] = finalDir;
            previousPredictedDirs[i] = predictedDir;

            // Apply rotation locally
            Quaternion localTargetRot = Quaternion.Inverse(gun.parent.rotation) * Quaternion.LookRotation(finalDir);
            gun.localRotation = localTargetRot;
        }

        // Hard limit shooting if any gun is out of the cone
        hasAngle = !anyGunOutOfCone;
    }



    Vector3 ClampDirectionToCone(Vector3 direction, Vector3 coneAxis, float minAngle, float maxAngle)
    {
        float angle = Vector3.Angle(direction, coneAxis);

        // If inside the cone, return as-is
        if (angle >= minAngle && angle <= maxAngle)
            return direction.normalized;

        // Clamp to nearest limit
        float clampedAngle = Mathf.Clamp(angle, minAngle, maxAngle);

        // Compute rotation axis
        Vector3 rotationAxis = Vector3.Cross(coneAxis, direction);
        if (rotationAxis.sqrMagnitude < 0.0001f)
        {
            // direction is parallel to coneAxis; pick any perpendicular axis
            rotationAxis = Vector3.Cross(coneAxis, Vector3.right);
            if (rotationAxis.sqrMagnitude < 0.0001f)
                rotationAxis = Vector3.Cross(coneAxis, Vector3.forward);
        }
        rotationAxis.Normalize();

        // Rotate the cone axis toward the original direction by the clamped angle
        return Quaternion.AngleAxis(clampedAngle, rotationAxis) * coneAxis;
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

    void OnDrawGizmos()
    {
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

    // use this for debug of UpwardConePrediction
    /*void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Vector3 origin = transform.position;
        Vector3 coneAxis = transform.up; // upward direction of the turret

        float minHeight = minShootingRange + minShootingRangeIncrement;
        float maxHeight = maxShootingRange + maxShootingRangeIncrement;

        // Compute circle radii from cone angles
        float minRadius = Mathf.Tan(coneMinAngle * Mathf.Deg2Rad) * minHeight;
        float maxRadius = Mathf.Tan(coneMaxAngle * Mathf.Deg2Rad) * maxHeight;

        // Compute circle centers
        Vector3 minCenter = origin + coneAxis * minHeight;
        Vector3 maxCenter = origin + coneAxis * maxHeight;

        // Draw line connecting circle centers
        Gizmos.DrawLine(minCenter, maxCenter);

        // Draw bottom circle
        DrawCircleGizmo(minCenter, coneAxis, minRadius);

        // Draw top circle
        DrawCircleGizmo(maxCenter, coneAxis, maxRadius);

        // Optional: draw a line in the middle to visualize the cone axis
        Vector3 middlePoint = origin + coneAxis * ((minHeight + maxHeight) * 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, middlePoint);
    }

    // Helper function to draw a circle
    void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius, int segments = 24)
    {
        Vector3 perp = Vector3.Cross(normal, Vector3.right);
        if (perp.sqrMagnitude < 0.01f)
            perp = Vector3.Cross(normal, Vector3.forward);
        perp.Normalize();

        Vector3 lastPoint = center + perp * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            Vector3 nextPoint = center + (Quaternion.AngleAxis(angle * Mathf.Rad2Deg, normal) * perp) * radius;
            Gizmos.DrawLine(lastPoint, nextPoint);
            lastPoint = nextPoint;
        }
    }*/
}
