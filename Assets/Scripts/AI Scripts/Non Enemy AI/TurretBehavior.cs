using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretBehavior : MonoBehaviour
{
    private SpaceShooterController player;

    [Header("Shooting")]
    public float weaponChargeDuration = 1.5f;                   // This is set by the user
    [SerializeField] private float weaponChargeDurationTimer;
    [SerializeField] private float weaponShootDuration = 0.5f;  // defined by shot count * fire rate or sequenceDuration, depending on the limit. If no limit - just uses the current fire rate value
    [SerializeField] private float weaponShootDurationTimer;
    [SerializeField] private float weaponCooldownDuration = 3f; // uses emitter's recharge duration
    [SerializeField] private float weaponCooldownDurationTimer;

    public float minShootingRange;
    public float maxShootingRange;

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

    [Header("Emitter Stuff")]
    public ProjectileEmittersController projectileEmittersControllerRef;
    [SerializeField] private Transform[] gunsPositions;
    private Vector3[] aimedDirs; // store per-gun aimed directions


    public void InitializeTurret(SpaceShooterController targetPtr, float minShootingRangePtr, float maxShootingRangePtr)
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

    public float GetWeaponChargeDurationTimer()
    {
        return weaponChargeDurationTimer;
    }

    // handles all shooting related timers and the brief stop before shooting
    public void HandleShooting(float distanceToPlayer)
    {
        // --- Check sweet spot and line of sight ---
        bool inSweetSpot = distanceToPlayer >= minShootingRange && distanceToPlayer <= maxShootingRange;
        bool hasLOS = HasLineOfSight();

        if (!inSweetSpot || !hasLOS)
        {
            if (isShooting) ResetShooting(); // cancel immediately if either condition fails
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
            Vector3 predictedPos = playerPos + playerVel * timeToHit;

            Vector3 predictedDir = Vector3.zero;

            if (player.overboostMode)
                predictedDir = (predictedPos - gunPos).normalized; // linear prediction, should be balanced with lead aim time
            else
                predictedDir = CalculateInterceptDirection(gunPos, playerPos, playerVel, projectileSpeed); //perfect aim 

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
}
