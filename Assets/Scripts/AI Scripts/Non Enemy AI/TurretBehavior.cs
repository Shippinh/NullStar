using System.Collections.Generic;
using UnityEngine;

public class TurretBehavior : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Enums
    // ─────────────────────────────────────────────

    public enum AimType
    {
        SmoothRotation = 0,
        InterceptPrediction = 1,
        LinearPrediction = 2,
        UpwardConePrediction = 3,
        SplineCardinal = 4,   // Fires along a fixed direction in spline space
        ScriptedFormation = 5
    }

    public enum ShootingState
    {
        Idle = 0,
        Charging = 1,
        Shooting = 2,
        Cooldown = 3
    }

    // ─────────────────────────────────────────────
    //  References
    // ─────────────────────────────────────────────

    [Header("References")]
    public SpaceShooterController player;
    public ProjectileEmittersController projectileEmittersControllerRef;

    // ─────────────────────────────────────────────
    //  Aim Settings
    // ─────────────────────────────────────────────

    [Header("Aim Settings")]
    public AimType aimType;
    public float aimTimeToPerfect = 2f;
    [Range(0f, 180f)] public float aimResetThreshold = 30f;
    public float aimSmoothing = 2f;
    public float aimLeadTime = 0.8f;
    public bool playerOverboostSimpleAim = false;

    [Header("Upward Cone Settings")]
    [Range(0f, 89f)] public float coneMinAngle = 10f;
    [Range(0f, 89f)] public float coneMaxAngle = 45f;
    public float coneBlendStrength = 1f;

    // ─────────────────────────────────────────────
    //  Spline Cardinal Settings
    // ─────────────────────────────────────────────

    [Header("Spline Cardinal Settings")]
    [Tooltip("Direction in spline basis. (0,1,0) = SplineUp, (1,0,0) = SplineRight, (0,0,-1) = toward player on rail, etc.")]
    public Vector3 cardinalDir = Vector3.up;

    public enum CardinalBasis
    {
        /// <summary>
        /// Uses the enemy's own spline basis (EnemyRailController on this or a parent).
        /// Best when the passby spline is roughly co-planar with the player rail.
        /// </summary>
        EnemySpline = 0,

        /// <summary>
        /// Uses the player's rail basis sampled at the player's current splineT.
        /// Always relative to what the player sees as up/right regardless of how
        /// the passby spline is oriented.
        /// </summary>
        PlayerSpline = 1,
    }

    [Tooltip("Which spline's basis vectors are used to interpret cardinalDir.")]
    public CardinalBasis cardinalBasis = CardinalBasis.PlayerSpline;

    // ─────────────────────────────────────────────
    //  Range Settings
    // ─────────────────────────────────────────────

    [Header("Shooting Range")]
    public float minShootingRange;
    public float maxShootingRange;
    public float minShootingRangeIncrement = 0f;
    public float maxShootingRangeIncrement = 0f;

    // ─────────────────────────────────────────────
    //  Shooting Timings
    // ─────────────────────────────────────────────

    [Header("Shooting Timings")]
    public float weaponChargeDuration = 1.5f;
    [SerializeField] private float weaponCooldownDuration = 3f;
    [SerializeField] private float weaponShootDuration = 0.5f;

    [Header("Randomization")]
    public bool slightlyRandomizeDurations = true;
    public float randomDurationsRange = 0.1f;
    public bool randomizeInitialChargeTimer = true;
    public bool randomizeStopWhenShooting = false;
    public bool stopWhenShooting = false;

    // ─────────────────────────────────────────────
    //  State (Read Only in Inspector)
    // ─────────────────────────────────────────────

    [Header("State — Read Only")]
    [SerializeField] private ShootingState shootingState = ShootingState.Idle;
    [SerializeField] private bool hasAngle = true;
    [SerializeField] private bool canAct = true;
    [SerializeField] private float stateTimer = 0f;

    // ─────────────────────────────────────────────
    //  Line of Sight
    // ─────────────────────────────────────────────

    [Header("Line of Sight")]
    public LayerMask losCheck;

    // ─────────────────────────────────────────────
    //  Internals
    // ─────────────────────────────────────────────

    private Transform[] gunsPositions;
    private Vector3[] aimedDirs;
    private float[] aimStrengths;
    private Vector3[] previousPredictedDirs;
    private Vector3 aimedDir;
    private float projectileSpeed;
    private Vector3[] _formationTargets;

    // Cached for SplineCardinal — resolved once in Initialize()
    private EnemyRailController _enemyRail;

    // Single-shot burst: overrides the normal state machine for exactly one
    // shoot cycle, ignoring range/LOS/canAct checks.
    private bool _singleShotPending = false;

    // ─────────────────────────────────────────────
    //  Init
    // ─────────────────────────────────────────────

    private void Start() => Initialize();

    public void InitializeEnemyTurret(SpaceShooterController target, float minRange, float maxRange)
    {
        player = target;
        minShootingRange = minRange;
        maxShootingRange = maxRange;
        Initialize();
    }

    private void Initialize()
    {
        if (!player)
            player = FindObjectOfType<SpaceShooterController>();

        projectileEmittersControllerRef = GetComponentInChildren<ProjectileEmittersController>();
        gunsPositions = projectileEmittersControllerRef.GetGunsArray();

        weaponShootDuration = projectileEmittersControllerRef.GetSequenceDuration() + 0.1f;
        weaponCooldownDuration = projectileEmittersControllerRef.rechargeTime;

        if (slightlyRandomizeDurations)
        {
            weaponChargeDuration = Random.Range(weaponChargeDuration - randomDurationsRange, weaponChargeDuration + randomDurationsRange);
            weaponCooldownDuration = Random.Range(weaponCooldownDuration - randomDurationsRange, weaponCooldownDuration + randomDurationsRange);
        }

        if (randomizeInitialChargeTimer)
            stateTimer = Random.Range(0f, weaponChargeDuration);

        if (randomizeStopWhenShooting)
            stopWhenShooting = Random.Range(0, 2) == 1;

        projectileSpeed = ObjectPool.Instance
            .GetPooledObject(projectileEmittersControllerRef.projectileTag, true, false)
            .GetComponent<SimpleEnemyProjectile>().speed;

        // Cache enemy rail for SplineCardinal — walk up the hierarchy from this turret
        _enemyRail = GetComponentInParent<EnemyRailController>();
    }

    // ─────────────────────────────────────────────
    //  Update
    // ─────────────────────────────────────────────

    private void Update()
    {
        UpdateAiming();
        HandleShooting();
    }

    // ─────────────────────────────────────────────
    //  Shooting State Machine
    // ─────────────────────────────────────────────

    public void HandleShooting()
    {
        // ── Single-shot override path ─────────────────────────────────────
        if (_singleShotPending)
        {
            _singleShotPending = false;
            shootingState = ShootingState.Shooting;
            stateTimer = 0f;
            projectileEmittersControllerRef.ForceStartSequence();
        }

        // ── Normal gating ─────────────────────────────────────────────────
        if (!hasAngle)
        {
            ResetShooting();
            projectileEmittersControllerRef.ForceStopSequence();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
        bool inRange = distToPlayer >= minShootingRange + minShootingRangeIncrement
                    && distToPlayer <= maxShootingRange + maxShootingRangeIncrement;
        bool hasLOS = HasLineOfSight();

        bool allowCompletion = shootingState == ShootingState.Shooting
                            || shootingState == ShootingState.Cooldown;

        if ((!inRange || !hasLOS || !canAct) && !allowCompletion)
        {
            ResetShooting();
            projectileEmittersControllerRef.ForceStopSequence();
            return;
        }

        stateTimer += Time.deltaTime;

        switch (shootingState)
        {
            case ShootingState.Idle:
                stateTimer = 0f;
                shootingState = ShootingState.Charging;
                break;

            case ShootingState.Charging:
                if (stateTimer >= weaponChargeDuration)
                {
                    stateTimer = 0f;
                    shootingState = ShootingState.Shooting;
                }
                break;

            case ShootingState.Shooting:
                projectileEmittersControllerRef.RequestSoftStart();
                if (stateTimer >= weaponShootDuration)
                {
                    projectileEmittersControllerRef.ForceStopSequence();
                    stateTimer = 0f;
                    shootingState = ShootingState.Cooldown;
                }
                break;

            case ShootingState.Cooldown:
                if (stateTimer >= weaponCooldownDuration)
                {
                    stateTimer = 0f;
                    shootingState = ShootingState.Idle;
                }
                break;
        }
    }

    public void ResetShooting()
    {
        shootingState = ShootingState.Idle;
        stateTimer = 0f;
    }

    public void SyncState(bool canActPtr)
    {
        canAct = canActPtr;
    }

    public float GetWeaponChargeDurationTimer() => stateTimer;
    public ShootingState GetShootingState() => shootingState;

    public void FireSingleShot()
    {
        shootingState = ShootingState.Shooting;
        stateTimer = 0f;
        projectileEmittersControllerRef.ForceStartSequence();
    }
    public void FireFormationShot(Vector3[] targets, string tag, float speed, float steerStrength, float snapDistance)
    {
        var guns = projectileEmittersControllerRef.GetActiveGuns();

        for (int i = 0; i < targets.Length; i++)
        {
            Transform gun = guns[i % guns.Count];
            var obj = ObjectPool.Instance.GetPooledObject(tag, gun.position, gun.rotation, false);
            if (obj == null) continue;

            var proj = obj.GetComponent<ScriptedInflatableProjectile>();
            if (proj == null) continue;

            proj.speed = speed;
            proj.snapDistance = snapDistance;
            proj.Initialize(gun.position, gun.position + gun.forward);
            proj.SetTarget(targets[i]);
        }
    }

    // ─────────────────────────────────────────────
    //  Aiming
    // ─────────────────────────────────────────────

    public void UpdateAiming()
    {
        if (!player || gunsPositions == null || gunsPositions.Length == 0) return;

        switch (aimType)
        {
            case AimType.InterceptPrediction:
            case AimType.LinearPrediction:
                PreciserAiming();
                break;
            case AimType.UpwardConePrediction:
                UpwardConeAiming();
                break;
            case AimType.SmoothRotation:
                SmoothRotationAiming();
                break;
            case AimType.SplineCardinal:
                SplineCardinalAiming();
                break;
            case AimType.ScriptedFormation:
                ScriptedFormationAiming();
                break;
        }
    }

    private void EnsureAimArrays()
    {
        int count = gunsPositions.Length;
        if (aimedDirs == null || aimedDirs.Length != count) aimedDirs = new Vector3[count];
        if (aimStrengths == null || aimStrengths.Length != count) aimStrengths = new float[count];
        if (previousPredictedDirs == null || previousPredictedDirs.Length != count) previousPredictedDirs = new Vector3[count];
    }

    // ─────────────────────────────────────────────
    //  Spline Cardinal Aiming
    // ─────────────────────────────────────────────

    private void SplineCardinalAiming()
    {
        Vector3 right, up, forward;

        if (cardinalBasis == CardinalBasis.PlayerSpline)
        {
            // Player rail basis — always matches what the player sees as up/right
            var rail = player.railControllerRef;
            right = rail.InterpolatedSplineRight;
            up = rail.InterpolatedSplineUp;
            forward = rail.InterpolatedSplineForward;
        }
        else
        {
            // Enemy's own spline basis — good when passby spline is co-planar with player rail
            if (_enemyRail == null)
            {
                // Fallback: no EnemyRailController found, use world axes
                Debug.LogWarning($"[TurretBehavior] SplineCardinal with EnemySpline basis on {name} " +
                                  "but no EnemyRailController found in parents. Falling back to world axes.");
                right = Vector3.right;
                up = Vector3.up;
                forward = Vector3.forward;
            }
            else
            {
                right = _enemyRail.InterpolatedSplineRight;
                up = _enemyRail.InterpolatedSplineUp;
                forward = _enemyRail.InterpolatedSplineForward;
            }
        }

        // Build the world-space direction from the cardinal components
        Vector3 worldDir = (right * cardinalDir.x
                          + up * cardinalDir.y
                          + forward * cardinalDir.z).normalized;

        if (worldDir.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"[TurretBehavior] SplineCardinal on {name} produced a zero direction. " +
                              "Check cardinalDir is not (0,0,0).");
            return;
        }

        // Apply to every gun — no per-gun variation needed for cardinal aiming
        for (int i = 0; i < gunsPositions.Length; i++)
        {
            Transform gun = gunsPositions[i];
            gun.localRotation = Quaternion.Inverse(gun.parent.rotation)
                              * Quaternion.LookRotation(worldDir);
        }

        // Cardinal aiming always has a valid angle
        hasAngle = true;
    }

    // ─────────────────────────────────────────────
    //  Existing Aim Methods (unchanged)
    // ─────────────────────────────────────────────

    private Vector3 GetPredictedDirection(Vector3 gunPos, Vector3 playerPos, Vector3 playerVel, bool useIntercept)
    {
        float distance = Vector3.Distance(gunPos, playerPos);
        float timeToHit = distance / projectileSpeed;

        if (useIntercept)
            return CalculateInterceptDirection(gunPos, playerPos, playerVel, projectileSpeed);

        Vector3 predictedPos = playerPos + playerVel * (timeToHit + aimLeadTime);
        return (predictedPos - gunPos).normalized;
    }

    private float AdvanceAimStrength(int i, Vector3 predictedDir)
    {
        if (Vector3.Angle(previousPredictedDirs[i], predictedDir) > aimResetThreshold)
            aimStrengths[i] = 0f;

        aimStrengths[i] = aimTimeToPerfect > 0f
            ? Mathf.Clamp01(aimStrengths[i] + Time.deltaTime / aimTimeToPerfect)
            : 1f;

        return aimStrengths[i];
    }

    private void PreciserAiming()
    {
        EnsureAimArrays();

        bool useIntercept = aimType == AimType.InterceptPrediction
            && (!playerOverboostSimpleAim || player.playerState != PlayerState.OverboostActive);

        for (int i = 0; i < gunsPositions.Length; i++)
        {
            Transform gun = gunsPositions[i];
            Vector3 predictedDir = GetPredictedDirection(gun.position, player.transform.position, player.velocity, useIntercept);
            float strength = AdvanceAimStrength(i, predictedDir);

            aimedDirs[i] = Vector3.Slerp(aimedDirs[i], predictedDir, strength);
            previousPredictedDirs[i] = predictedDir;

            gun.localRotation = Quaternion.Inverse(gun.parent.rotation) * Quaternion.LookRotation(aimedDirs[i]);
        }
    }

    private void SmoothRotationAiming()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        float timeToHit = distance / projectileSpeed;
        Vector3 predicted = player.transform.position + player.velocity * timeToHit;

        aimedDir = (predicted - transform.position).normalized;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(aimedDir), Time.deltaTime * aimSmoothing);
    }

    private void UpwardConeAiming()
    {
        EnsureAimArrays();
        bool anyOutOfCone = false;

        for (int i = 0; i < gunsPositions.Length; i++)
        {
            Transform gun = gunsPositions[i];
            Vector3 predictedDir = GetPredictedDirection(gun.position, player.transform.position, player.velocity, false);
            float strength = AdvanceAimStrength(i, predictedDir);

            Vector3 blendedDir = Vector3.Slerp(aimedDirs[i], predictedDir, strength);
            Vector3 coneAxis = gun.parent.up;
            float angleFromAxis = Vector3.Angle(blendedDir, coneAxis);

            if (angleFromAxis < coneMinAngle || angleFromAxis > coneMaxAngle)
            {
                anyOutOfCone = true;
                blendedDir = ClampDirectionToCone(blendedDir, coneAxis, coneMinAngle, coneMaxAngle);
            }

            aimedDirs[i] = blendedDir;
            previousPredictedDirs[i] = predictedDir;
            gun.localRotation = Quaternion.Inverse(gun.parent.rotation) * Quaternion.LookRotation(blendedDir);
        }

        hasAngle = !anyOutOfCone;
    }

    // New method — just points guns at cone center, no prediction, no checks
    private void ScriptedFormationAiming()
    {
        if (_formationTargets == null) return;
        for (int i = 0; i < gunsPositions.Length; i++)
        {
            if (i >= _formationTargets.Length) break;
            Vector3 dir = (_formationTargets[i] - gunsPositions[i].position).normalized;
            gunsPositions[i].localRotation = Quaternion.Inverse(gunsPositions[i].parent.rotation)
                                           * Quaternion.LookRotation(dir);
        }
        hasAngle = true;
    }

    // ─────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────

    private Vector3 ClampDirectionToCone(Vector3 direction, Vector3 coneAxis, float minAngle, float maxAngle)
    {
        float angle = Vector3.Angle(direction, coneAxis);
        if (angle >= minAngle && angle <= maxAngle) return direction.normalized;

        float clampedAngle = Mathf.Clamp(angle, minAngle, maxAngle);
        Vector3 rotAxis = Vector3.Cross(coneAxis, direction);

        if (rotAxis.sqrMagnitude < 0.0001f)
        {
            rotAxis = Vector3.Cross(coneAxis, Vector3.right);
            if (rotAxis.sqrMagnitude < 0.0001f)
                rotAxis = Vector3.Cross(coneAxis, Vector3.forward);
        }

        return Quaternion.AngleAxis(clampedAngle, rotAxis.normalized) * coneAxis;
    }

    public void SetFormationTargets(Vector3[] targets)
    {
        _formationTargets = targets;
        aimType = AimType.ScriptedFormation;
    }

    private Vector3 CalculateInterceptDirection(Vector3 shooterPos, Vector3 targetPos,
                                                Vector3 targetVel, float projSpeed)
    {
        Vector3 displacement = targetPos - shooterPos;
        float a = Vector3.Dot(targetVel, targetVel) - projSpeed * projSpeed;
        float b = 2f * Vector3.Dot(displacement, targetVel);
        float c = Vector3.Dot(displacement, displacement);
        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0 || Mathf.Approximately(a, 0f))
            return displacement.normalized;

        float sqrtDisc = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDisc) / (2f * a);
        float t2 = (-b - sqrtDisc) / (2f * a);
        float t = Mathf.Min(t1, t2);
        if (t < 0f) t = Mathf.Max(t1, t2);
        if (t < 0f) return displacement.normalized;

        return ((targetPos + targetVel * t) - shooterPos).normalized;
    }

    private bool HasLineOfSight()
    {
        if (!player) return false;
        Vector3 dir = (player.transform.position - transform.position).normalized;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        return !Physics.Raycast(transform.position, dir, out _, dist, losCheck, QueryTriggerInteraction.Ignore);
    }
}