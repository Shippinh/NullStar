    using System.Collections.Generic;
    using UnityEngine;

    public class TurretBehavior : MonoBehaviour
    {
        public enum AimType
        {
            SmoothRotation = 0,
            InterceptPrediction = 1,
            LinearPrediction = 2,
            UpwardConePrediction = 3,
            SplineCardinal = 4,
            ScriptedFormation = 5,
            DirectShot = 6
        }

        public enum ShootingState { Idle = 0, Charging = 1, Shooting = 2, Cooldown = 3 }

        [Header("References")]
        public SpaceShooterController player;
        public ProjectileEmittersController projectileEmittersControllerRef;

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

        [Header("Spline Cardinal Settings")]
        [Tooltip("Direction in spline basis. (0,1,0) = SplineUp, (1,0,0) = SplineRight, (0,0,-1) = toward player on rail, etc.")]
        public Vector3 cardinalDir = Vector3.up;

        public enum CardinalBasis { EnemySpline = 0, PlayerSpline = 1 }
        [Tooltip("Which spline's basis vectors are used to interpret cardinalDir.")]
        public CardinalBasis cardinalBasis = CardinalBasis.PlayerSpline;

        [Header("Shooting Range")]
        public float minShootingRange;
        public float maxShootingRange;
        public float minShootingRangeIncrement = 0f;
        public float maxShootingRangeIncrement = 0f;

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

        [Header("State — Read Only")]
        [SerializeField] private ShootingState shootingState = ShootingState.Idle;
        [SerializeField] private bool hasAngle = true;
        [SerializeField] private bool canAct = true;
        [SerializeField] private float stateTimer = 0f;

        [Header("Line of Sight")]
        public LayerMask losCheck;

        private Transform[] gunsPositions;
        private Vector3[] aimedDirs;
        private float[] aimStrengths;
        private Vector3[] previousPredictedDirs;
        private Vector3 aimedDir;
        private float projectileSpeed;
        private Vector3[] _formationTargets;
        private EnemyRailController _enemyRail;
        private bool _singleShotPending = false;

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
            if (!player) player = FindObjectOfType<SpaceShooterController>();

            projectileEmittersControllerRef = GetComponentInChildren<ProjectileEmittersController>();
            gunsPositions = projectileEmittersControllerRef.GetGunsArray();
            weaponShootDuration = projectileEmittersControllerRef.GetSequenceDuration() + 0.1f;
            weaponCooldownDuration = projectileEmittersControllerRef.rechargeTime;

            if (slightlyRandomizeDurations)
            {
                weaponChargeDuration = Random.Range(weaponChargeDuration - randomDurationsRange, weaponChargeDuration + randomDurationsRange);
                weaponCooldownDuration = Random.Range(weaponCooldownDuration - randomDurationsRange, weaponCooldownDuration + randomDurationsRange);
            }

            if (randomizeInitialChargeTimer) stateTimer = Random.Range(0f, weaponChargeDuration);
            if (randomizeStopWhenShooting) stopWhenShooting = Random.Range(0, 2) == 1;

            projectileSpeed = ObjectPool.Instance
                .GetPooledObject(projectileEmittersControllerRef.projectileTag, true, false)
                .GetComponent<SimpleEnemyProjectile>().speed;

            _enemyRail = GetComponentInParent<EnemyRailController>();
            projectileEmittersControllerRef.onProjectileSpawned += OnProjectileSpawned;
        }

        private void OnDisable()
        {
            if (projectileEmittersControllerRef != null)
                projectileEmittersControllerRef.onProjectileSpawned -= OnProjectileSpawned;
        }

        private void OnEnable()
        {
            if (projectileEmittersControllerRef != null)
                projectileEmittersControllerRef.onProjectileSpawned += OnProjectileSpawned;
        }

        private void OnProjectileSpawned(GameObject obj, Vector3 spawnPos)
        {
            var proj = obj.GetComponent<SimpleEnemyProjectile>();
            if (proj == null) return;

            switch (proj.movementMode)
            {
                case SimpleEnemyProjectile.MovementMode.PlayerSpace:
                    if (player != null && player.railControllerRef != null && _enemyRail != null)
                    {
                        Vector3 localOffset = _enemyRail.transform.InverseTransformPoint(transform.position);
                        proj.InitializeInPlayerSpace(
                            _enemyRail,
                            localOffset,
                            player.railControllerRef,
                            spawnPos,
                            player.currentRightOffset,
                            player.currentUpOffset
                        );
                    }
                    break;
                case SimpleEnemyProjectile.MovementMode.SplineTracking:
                    if (_enemyRail != null)
                        proj.InitializeOnSpline(_enemyRail, spawnPos);
                    break;
            }
        }

        private void Update()
        {
            UpdateAiming();
            HandleShooting();
        }

        public void HandleShooting()
        {
            if (_singleShotPending)
            {
                _singleShotPending = false;
                shootingState = ShootingState.Shooting;
                stateTimer = 0f;
                projectileEmittersControllerRef.ForceStartSequence();
            }

            if (!hasAngle)
            {
                ResetShooting();
                projectileEmittersControllerRef.ForceStopSequence();
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            bool inRange = dist >= minShootingRange + minShootingRangeIncrement
                        && dist <= maxShootingRange + maxShootingRangeIncrement;
            bool allowCompletion = shootingState == ShootingState.Shooting || shootingState == ShootingState.Cooldown;

            if ((!inRange || !HasLineOfSight() || !canAct) && !allowCompletion)
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
                    if (stateTimer >= weaponChargeDuration) { stateTimer = 0f; shootingState = ShootingState.Shooting; }
                    break;
                case ShootingState.Shooting:
                    projectileEmittersControllerRef.RequestSoftStart();
                    if (stateTimer >= weaponShootDuration) { projectileEmittersControllerRef.ForceStopSequence(); stateTimer = 0f; shootingState = ShootingState.Cooldown; }
                    break;
                case ShootingState.Cooldown:
                    if (stateTimer >= weaponCooldownDuration) { stateTimer = 0f; shootingState = ShootingState.Idle; }
                    break;
            }
        }

        public void ResetShooting()
        {
            shootingState = ShootingState.Idle;
            stateTimer = 0f;
        }

        public void SyncState(bool canActPtr) => canAct = canActPtr;
        public float GetWeaponChargeDurationTimer() => stateTimer;
        public ShootingState GetShootingState() => shootingState;

        public void FireSingleShot()
        {
            shootingState = ShootingState.Shooting;
            stateTimer = 0f;
            projectileEmittersControllerRef.ForceStartSequence();
        }

        public void FireFormationShot(Vector3[] targets, string tag, float travelDuration)
        {
            var guns = projectileEmittersControllerRef.GetActiveGuns();
            for (int i = 0; i < targets.Length; i++)
            {
                Transform gun = guns[i % guns.Count];
                var obj = ObjectPool.Instance.GetPooledObject(tag, gun.position, gun.rotation, false);
                if (obj == null) continue;
                var proj = obj.GetComponent<ScriptedInflatableProjectile>();
                if (proj == null) continue;
                proj.travelDuration = travelDuration;
                proj.Initialize(gun.position, gun.position + gun.forward);
                proj.SetTarget(targets[i]);
            }
        }

        public void UpdateAiming()
        {
            if (!player || gunsPositions == null || gunsPositions.Length == 0) return;
            switch (aimType)
            {
                case AimType.InterceptPrediction:
                case AimType.LinearPrediction: PreciserAiming(); break;
                case AimType.UpwardConePrediction: UpwardConeAiming(); break;
                case AimType.SmoothRotation: SmoothRotationAiming(); break;
                case AimType.SplineCardinal: SplineCardinalAiming(); break;
                case AimType.ScriptedFormation: ScriptedFormationAiming(); break;
                case AimType.DirectShot: DirectShotAiming(); break;  // <-- add this
            }
        }

        private void EnsureAimArrays()
        {
            int count = gunsPositions.Length;
            if (aimedDirs == null || aimedDirs.Length != count) aimedDirs = new Vector3[count];
            if (aimStrengths == null || aimStrengths.Length != count) aimStrengths = new float[count];
            if (previousPredictedDirs == null || previousPredictedDirs.Length != count) previousPredictedDirs = new Vector3[count];
        }

        private void DirectShotAiming()
        {
            Vector3 playerPos = player.transform.position;

            for (int i = 0; i < gunsPositions.Length; i++)
            {
                Vector3 dir = (playerPos - gunsPositions[i].position).normalized;
                if (dir.sqrMagnitude < 0.001f) continue;
                gunsPositions[i].localRotation = Quaternion.Inverse(gunsPositions[i].parent.rotation)
                                               * Quaternion.LookRotation(dir);
            }
            hasAngle = true;
        }

        private void SplineCardinalAiming()
        {
            Vector3 right, up, forward;
            if (cardinalBasis == CardinalBasis.PlayerSpline)
            {
                var rail = player.railControllerRef;
                right = rail.InterpolatedSplineRight; up = rail.InterpolatedSplineUp; forward = rail.InterpolatedSplineForward;
            }
            else if (_enemyRail != null)
            {
                right = _enemyRail.InterpolatedSplineRight; up = _enemyRail.InterpolatedSplineUp; forward = _enemyRail.InterpolatedSplineForward;
            }
            else
            {
                Debug.LogWarning($"[TurretBehavior] SplineCardinal with EnemySpline basis on {name} but no EnemyRailController found. Falling back to world axes.");
                right = Vector3.right; up = Vector3.up; forward = Vector3.forward;
            }

            Vector3 worldDir = (right * cardinalDir.x + up * cardinalDir.y + forward * cardinalDir.z).normalized;
            if (worldDir.sqrMagnitude < 0.001f) { Debug.LogWarning($"[TurretBehavior] SplineCardinal on {name} produced a zero direction."); return; }

            for (int i = 0; i < gunsPositions.Length; i++)
            {
                Transform gun = gunsPositions[i];
                gun.localRotation = Quaternion.Inverse(gun.parent.rotation) * Quaternion.LookRotation(worldDir);
            }
            hasAngle = true;
        }

        private Vector3 GetPredictedDirection(Vector3 gunPos, Vector3 playerPos, Vector3 playerVel, bool useIntercept)
        {
            float dist = Vector3.Distance(gunPos, playerPos);
            if (useIntercept) return CalculateInterceptDirection(gunPos, playerPos, playerVel, projectileSpeed);
            return ((playerPos + playerVel * (dist / projectileSpeed + aimLeadTime)) - gunPos).normalized;
        }

        private float AdvanceAimStrength(int i, Vector3 predictedDir)
        {
            if (Vector3.Angle(previousPredictedDirs[i], predictedDir) > aimResetThreshold) aimStrengths[i] = 0f;
            aimStrengths[i] = aimTimeToPerfect > 0f ? Mathf.Clamp01(aimStrengths[i] + Time.deltaTime / aimTimeToPerfect) : 1f;
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
            float dist = Vector3.Distance(transform.position, player.transform.position);
            Vector3 predicted = player.transform.position + player.velocity * (dist / projectileSpeed);
            aimedDir = (predicted - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimedDir), Time.deltaTime * aimSmoothing);
        }

        private void UpwardConeAiming()
        {
            EnsureAimArrays();
            bool anyOutOfCone = false;

            for (int i = 0; i < gunsPositions.Length; i++)
            {
                Transform gun = gunsPositions[i];
                Vector3 predictedDir = GetPredictedDirection(gun.position, player.transform.position, player.velocity, false);
                Vector3 blendedDir = Vector3.Slerp(aimedDirs[i], predictedDir, AdvanceAimStrength(i, predictedDir));
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

        private void ScriptedFormationAiming()
        {
            if (_formationTargets == null) return;
            for (int i = 0; i < gunsPositions.Length && i < _formationTargets.Length; i++)
            {
                Vector3 dir = (_formationTargets[i] - gunsPositions[i].position).normalized;
                gunsPositions[i].localRotation = Quaternion.Inverse(gunsPositions[i].parent.rotation) * Quaternion.LookRotation(dir);
            }
            hasAngle = true;
        }

        private Vector3 ClampDirectionToCone(Vector3 direction, Vector3 coneAxis, float minAngle, float maxAngle)
        {
            float angle = Vector3.Angle(direction, coneAxis);
            if (angle >= minAngle && angle <= maxAngle) return direction.normalized;

            Vector3 rotAxis = Vector3.Cross(coneAxis, direction);
            if (rotAxis.sqrMagnitude < 0.0001f)
            {
                rotAxis = Vector3.Cross(coneAxis, Vector3.right);
                if (rotAxis.sqrMagnitude < 0.0001f) rotAxis = Vector3.Cross(coneAxis, Vector3.forward);
            }
            return Quaternion.AngleAxis(Mathf.Clamp(angle, minAngle, maxAngle), rotAxis.normalized) * coneAxis;
        }

        public void SetFormationTargets(Vector3[] targets)
        {
            _formationTargets = targets;
            aimType = AimType.ScriptedFormation;
        }

        private Vector3 CalculateInterceptDirection(Vector3 shooterPos, Vector3 targetPos, Vector3 targetVel, float projSpeed)
        {
            Vector3 displacement = targetPos - shooterPos;
            float a = Vector3.Dot(targetVel, targetVel) - projSpeed * projSpeed;
            float b = 2f * Vector3.Dot(displacement, targetVel);
            float c = Vector3.Dot(displacement, displacement);
            float disc = b * b - 4f * a * c;

            if (disc < 0 || Mathf.Approximately(a, 0f)) return displacement.normalized;

            float sqrtDisc = Mathf.Sqrt(disc);
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