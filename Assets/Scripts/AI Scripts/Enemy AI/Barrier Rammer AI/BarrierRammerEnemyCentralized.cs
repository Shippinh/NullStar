using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BarrierRammerEnemyCentralized : MonoBehaviour
{
    [Header("References")]
    public SpaceShooterController player;
    public Transform pivot;          // pivot object for corkscrew rotation
    public Transform childPivot;
    public Transform enemyA;         // always assigned
    private Transform originalEnemyARef;
    public Transform enemyB;         // optional
    private Transform originalEnemyBRef;
    public Camera playerCamera;
    public GameObject barrierObj;
    public bool barrierIsColliding;
    public Transform barrierLeftAnchor;
    public Transform barrierRightAnchor;
    public BarrierRammerPreset duoStats;
    public BarrierRammerPreset soloStats;
    [SerializeField] private BarrierRammerPreset currentPreset;
    public bool isSolo;

    [Header("Movement Settings")]
    public bool randomizeInitialBurstTimer = false;
    public float maxRandomInitialBurstTimer = 0.1f;
    public float currentBurstCooldown = 0.5f;
    [SerializeField] private bool isClockwise = true;

    [Header("Pivot Point System (Paired Movement)")]
    public Transform[] pivotPoints;          // assign 4 transforms via inspector
    private Vector3[] initialPivotOffsets;
    private int currentPivotIndex = 0;       // current pivot index for cycling

    [Header("Pre-Attack Burst Settings")]
    public float burstDuration;       // how long the burst lasts
    private bool burstingToPivot = false;     // are we currently bursting?
    private float burstTimer = 0f;
    private Vector3 burstDir;

    [Header("Attack Pathfinding")]
    public bool canAttack = true;
    private Vector3 currentPivot;
    private int currentDirectionIndex = -1;
    private List<Vector3> availableDirections;
    private Vector3 lastPlayerForward = Vector3.forward;
    private float nextPivotTime = 0f;

    [Header("Attack Sequence Handling")]
    [SerializeField] private float alignTimer = 0f;
    [SerializeField] private float leftRightAlignTimer = 0f;
    [SerializeField] private float attackTimer = 0f;
    public bool isAttacking; // general state, tells if the enemy is within the attack sequence
    public bool randomizeABLeftRightPos; // if true - enemy a and b will assume left and right position randomly
    [SerializeField] private int currentCollisionsBeforePrematureStop = 0;
    private Quaternion alignStartRot;
    private Quaternion anchorStartRot;
    private Vector3 anchorStartAPos;
    private Vector3 anchorStartBPos;
    [SerializeField] private bool aligningToPivot = false;
    [SerializeField] private bool aligningToAnchors = false;
    [SerializeField] private bool enemiesAligned = false;

    [Header("Other")]
    public bool reinitializeOnEnable = true;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private Vector3 contactNormal = Vector3.up;
    private float nextBurstTime = 0f;
    private uint destructibleCount = 0;
    private uint indestructibleCount = 0;

    public int currentSpiralStep = 0;

    void Awake()
    {
        if (!player)
            player = FindObjectOfType<SpaceShooterController>();

        playerCamera = Camera.main;

        originalEnemyARef = enemyA;
        originalEnemyBRef = enemyB;

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;

        velocity = Vector3.zero;

        if (pivotPoints != null && pivotPoints.Length > 0)
        {
            initialPivotOffsets = new Vector3[pivotPoints.Length];
            for (int i = 0; i < pivotPoints.Length; i++)
                initialPivotOffsets[i] = pivotPoints[i].position - transform.position;
        }

        Reinitialize();
    }

    // Soft AI reinitialization
    private void OnEnable()
    {
        if (!reinitializeOnEnable)
            return;

        // --- Core motion reset ---
        velocity = Vector3.zero;
        desiredVelocity = Vector3.zero;

        ForceStopAttack();
        ResetBehaviorState();

        // --- Re-pick preset & collider settings ---
        Reinitialize();
    }



    private void Reinitialize()
    {
        if (enemyA != null && enemyB != null)
        {
            currentPreset = duoStats;
            isSolo = false;
        }
        else if (enemyA)
        {
            currentPreset = soloStats;
            isSolo = true;
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = currentPreset.detectionRadius;

        if (randomizeInitialBurstTimer)
            currentBurstCooldown = Random.Range(currentPreset.burstCooldown - maxRandomInitialBurstTimer, currentPreset.burstCooldown + maxRandomInitialBurstTimer);

        if (currentPreset.randomizeSpinDirection)
        {
            int val = Random.Range(0, 2); // 50/50 to be clockwise or counter clockwise
            if (val == 0)
                isClockwise = false;
            if (val == 1)
                isClockwise = true;
        }

        burstDuration = currentBurstCooldown;
    }

    private void LateUpdate()
    {
        if (player.GetPlayerOnGround())
            currentPivot = CheckForceTopPivot();
        else
            currentPivot = UpdatePivot();

        AttachPivot();
        if (childPivot != null)
            SynchronizePivots();
    }

    void FixedUpdate()
    {
        if (enemyA == null) return;

        if (!isAttacking) // if not attacking
        {
            UpdateDirections();

            CalculateDesiredVelocity();

            RotateTowardsCurrentPivot();

            if(canAttack && !isSolo)
                HandleAttackInitiation();
        }
        else
        {
            if (burstingToPivot)
                HandleAttackBurst();
            else
            {
                HandleAttackAlignment();
                HandleAttackEnemyAlignment();
                PerformAttack();
            }
        }

        if (burstingToPivot || !isAttacking)
            SwitchPivotPoints();

        AdjustVelocity();

        rb.velocity = velocity;
    }

    void HandleAttackInitiation()
    {
        float distToPivot = Vector3.Distance(transform.position, currentPivot);

        if (distToPivot >= currentPreset.minBurstDistance && distToPivot <= currentPreset.maxBurstDistance)
        {
            // Begin attack sequence
            isAttacking = true;

            // --- New burst phase ---
            burstingToPivot = true;
            burstTimer = 0f;
            burstDir = (currentPivot - transform.position).normalized;

            // Clear motion
            velocity = Vector3.zero;
            desiredVelocity = Vector3.zero;

            Debug.Log("Starting pre-attack burst!");
        }
    }

    void HandleAttackBurst()
    {
        if (!burstingToPivot) return;

        burstTimer += Time.deltaTime;
        transform.position += burstDir * currentPreset.burstSpeed * Time.deltaTime;
        // End of burst — transition to alignment
        if (burstTimer >= burstDuration)
        {
            burstingToPivot = false;
            aligningToPivot = true;
            alignTimer = 0f;
            alignStartRot = pivot.rotation;
            anchorStartAPos = enemyA.position;
            anchorStartBPos = enemyB.position;

            Debug.Log("Final burst complete, starting alignment phase!");
        }
    }

    private void AttachPivot()
    {
        pivot.transform.position = transform.position;
    }

    private void SynchronizePivots()
    {
        childPivot.transform.rotation = pivot.transform.rotation;
        childPivot.transform.position = pivot.transform.position;
    }

    private void ForceStopAttack()
    {
        if (!isAttacking && !burstingToPivot && !aligningToPivot && !aligningToAnchors && !enemiesAligned)
            return; // nothing to stop

        Debug.Log("Attack forcibly stopped!");

        // reset all attack-related states
        isAttacking = false;
        burstingToPivot = false;
        aligningToPivot = false;
        aligningToAnchors = false;
        enemiesAligned = false;

        // clear timers
        attackTimer = 0f;
        alignTimer = 0f;
        leftRightAlignTimer = 0f;
        burstTimer = 0f;

        // disable barrier visuals
        if (barrierObj != null)
            barrierObj.SetActive(false);

        // smoothly reset motion
       /* desiredVelocity = Vector3.zero;
        velocity = Vector3.Lerp(velocity, Vector3.zero, 10f * Time.deltaTime);

        // optional: reset pivot rotation toward current direction
        if (pivot != null)
        {
            Vector3 forward = transform.forward;
            if (forward.sqrMagnitude > 0.001f)
                pivot.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }*/
    }

    void HandleAttackAlignment()
    {
        if (aligningToPivot)
        {
            alignTimer += Time.deltaTime;
            float t = 1f - Mathf.Pow(0.01f, alignTimer / currentPreset.alignTime);

            if (enemyA != null && barrierLeftAnchor != null)
                enemyA.position = Vector3.Lerp(anchorStartAPos, barrierLeftAnchor.position, t);

            if (enemyB != null && barrierRightAnchor != null)
                enemyB.position = Vector3.Lerp(anchorStartBPos, barrierRightAnchor.position, t);

            // Also gradually face the player
            Vector3 dirToPlayer = (player.transform.position - pivot.position).normalized;
            if (dirToPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToPlayer, Vector3.up);
                pivot.rotation = Quaternion.Slerp(alignStartRot, targetRot, t);
            }
            // Done aligning
            if (alignTimer >= currentPreset.alignTime)
            {
                Debug.Log("Done aligning");
                aligningToPivot = false;
                aligningToAnchors = true;
                leftRightAlignTimer = 0f;
                barrierObj.SetActive(true);
                desiredVelocity = Vector3.zero;
                anchorStartRot = pivot.rotation;

                // Randomize which side is left/right
                if (randomizeABLeftRightPos && Random.value > 0.5f)
                {
                    (barrierLeftAnchor, barrierRightAnchor) = (barrierRightAnchor, barrierLeftAnchor);
                }
            }
        }
    }

    void HandleAttackEnemyAlignment()
    {
        if (aligningToAnchors)
        {
            leftRightAlignTimer += Time.deltaTime;

            Vector3 playerDir = (player.transform.position - pivot.position).normalized;
            Quaternion targetRot = anchorStartRot;

            if (playerDir.sqrMagnitude > 0.001f)
                targetRot = Quaternion.LookRotation(playerDir, Vector3.up);

            pivot.rotation = Quaternion.Slerp(anchorStartRot, targetRot, 10f);

            if (leftRightAlignTimer >= currentPreset.leftRightAlignTime)
            {
                aligningToAnchors = false;
                enemiesAligned = true;
                anchorStartRot = pivot.rotation;
                attackTimer = 0f;
            }
        }

    }

    void PerformAttack()
    {
        if (enemiesAligned)
        {
            //if avoidance detects indestructible ground or if the barrier directly hits something - stop attacking prematurely
            if(indestructibleCount > 0 || currentCollisionsBeforePrematureStop >= currentPreset.maxCollisionsBeforePrematureStop)
            {
                //barrierIsColliding = false;
                currentCollisionsBeforePrematureStop = 0;
                isAttacking = false;
                enemiesAligned = false;
                barrierObj.SetActive(false);
                Debug.Log("Finishing the attack prematurely!");
            }

            attackTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, attackTimer / currentPreset.attackTime);

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            float distanceScaler = Mathf.Clamp01(distanceToPlayer / currentPreset.attackRotationBreakoffDistance); //smooth fade when getting closer to player

            // Move forward relative to the pivot orientation
            desiredVelocity = pivot.forward * currentPreset.maxSpeed;

            Vector3 playerDir = (player.transform.position - pivot.position).normalized;
            if (playerDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(playerDir, Vector3.up);

                // dynamically scale rotation speed by distance
                float dynamicRotationSpeed = currentPreset.pivotRotationSpeed * distanceScaler;

                pivot.rotation = Quaternion.RotateTowards(
                    pivot.rotation,
                    targetRot,
                    dynamicRotationSpeed * Time.deltaTime
                );
            }

            // End the attack after attackTime expires
            if (attackTimer >= currentPreset.attackTime)
            {
                isAttacking = false;
                enemiesAligned = false;
                barrierObj.SetActive(false);
                Debug.Log("Attack finished!");
            }
        }
    }

    void SwitchPivotPoints()
    {
        if (isSolo == false && pivotPoints != null && pivotPoints.Length >= 2)
        {
            Transform pivotA = pivotPoints[currentPivotIndex];
            Transform pivotB = pivotPoints[(currentPivotIndex + 2) % pivotPoints.Length]; // opposite point for symmetry

            if (enemyA != null)
            {
                enemyA.position = Vector3.Lerp(
                    enemyA.position,
                    pivotA.position,
                    currentPreset.pivotMoveSpeed * Time.fixedDeltaTime
                );
            }

            if (enemyB != null)
            {
                enemyB.position = Vector3.Lerp(
                    enemyB.position,
                    pivotB.position,
                    currentPreset.pivotMoveSpeed * Time.fixedDeltaTime
                );
            }
        }
    }

    void RotateTowardsCurrentPivot()
    {
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
                    currentPreset.pivotRotationSpeed * Time.fixedDeltaTime
                );
            }
        }
    }

    void CalculateDesiredVelocity()
    {
        // unique noise seed per instance (for distinct motion patterns)
        float seed = transform.GetInstanceID() * 0.001f;

        // --- Chaotic offset for duo movement ---
            float sideNoise = Mathf.PerlinNoise(Time.time * 0.3f + seed, transform.position.x * 0.15f) - 0.5f;
            float upNoise = Mathf.PerlinNoise(Time.time * 0.33f + seed * 3f, transform.position.z * 0.15f + 11f) - 0.5f;

            float minChaos = 150f;
            float maxChaos = 300f;

        if (isSolo)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            float distanceToPlayer = toPlayer.magnitude;
            Vector3 toPlayerDir = toPlayer.normalized;

            Vector3 avoidanceVector = CalculateObstacleAvoidance();

            // --- Spiral distance scaling ---
            float distanceScaler = Mathf.Clamp01(distanceToPlayer / (currentPreset.spiralFadeDistance * 2f));
            if (distanceToPlayer < currentPreset.spiralFadeDistance)
                distanceScaler = 0f;

            float spiralMagnitude = currentPreset.maxSpiralOffset * distanceScaler;
            float directionSign = isClockwise ? 1f : -1f;
            float spiralAngle = currentSpiralStep * Mathf.PI / 2f * directionSign;

            // --- Base orientation ---
            Vector3 side = Vector3.Cross(Vector3.up, toPlayerDir).normalized;
            Vector3 up = Vector3.up;
            Quaternion tilt45 = Quaternion.AngleAxis(45f, toPlayerDir);
            side = tilt45 * side;
            up = tilt45 * up;

            float closeDist = 100f;
            float farDist = 200f;
            float chaosScaler = Mathf.Clamp01((distanceToPlayer - closeDist) / (farDist - closeDist));
            float chaosRange = Mathf.Lerp(minChaos, maxChaos, chaosScaler);

            Vector3 chaoticOffset = side * sideNoise * chaosRange
                                  + up * upNoise * chaosRange * 0.5f;

            // --- Spiral offset pattern ---
            Vector3 spiralOffset = side * Mathf.Cos(spiralAngle) * spiralMagnitude
                                 + up * Mathf.Sin(spiralAngle) * spiralMagnitude * currentPreset.verticalOffsetMultiplier;

            // --- Combine spiral + chaos ---
            Vector3 offsetCombined = spiralOffset + chaoticOffset;

            // --- Burst movement ---
            Vector3 targetDir = (toPlayerDir + avoidanceVector.normalized).normalized;
            float burstDistance = Mathf.Clamp(distanceToPlayer * 0.5f, currentPreset.minBurstDistance, currentPreset.maxBurstDistance);

            Vector3 burstTarget = transform.position + targetDir * burstDistance + offsetCombined;
            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > currentPreset.maxSpeed)
                desiredVelocity = desiredVelocity.normalized * currentPreset.maxSpeed;
        }
        else
        {
            // --- Pivot selection ---
            if (Time.time >= nextPivotTime)
            {
                currentPivot = ChoosePivot();
                nextPivotTime = Time.time + currentPreset.pivotChangeInterval;
            }

            // --- Movement toward pivot ---
            Vector3 toPivot = currentPivot - transform.position;
            float distanceToPivot = toPivot.magnitude;
            Vector3 toPivotDir = toPivot.normalized;

            Vector3 avoidanceVector = CalculateObstacleAvoidance();

            float chaosScaler = Mathf.Clamp01(distanceToPivot / 250f);
            float chaosRange = Mathf.Lerp(minChaos, maxChaos, chaosScaler);

            Vector3 side = Vector3.Cross(Vector3.up, toPivotDir).normalized;
            Vector3 up = Vector3.up;
            Vector3 chaoticOffset = side * sideNoise * chaosRange + up * upNoise * chaosRange * 0.5f;

            // --- Burst movement ---
            float burstDistance = Mathf.Clamp(distanceToPivot * 0.5f, currentPreset.minBurstDistance, currentPreset.maxBurstDistance);
            Vector3 burstTarget = transform.position + (toPivotDir + avoidanceVector.normalized).normalized * burstDistance + chaoticOffset;

            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > currentPreset.maxSpeed)
                desiredVelocity = desiredVelocity.normalized * currentPreset.maxSpeed;
        }
    }



    void AdjustVelocity()
    {
        if (Time.time >= nextBurstTime && !isAttacking)
        {
            velocity = desiredVelocity;
            nextBurstTime = Time.time + currentBurstCooldown;
            currentSpiralStep++;

            CalculateNextPivotPoint();
        }
        else
        {
            // velocity based movement towards forward
            if(enemiesAligned)
            {
                Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
                Vector3 yAxis = Vector3.up;
                Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

                float currentX = Vector3.Dot(velocity, xAxis);
                float currentY = Vector3.Dot(velocity, yAxis);
                float currentZ = Vector3.Dot(velocity, zAxis);

                float desiredX = Vector3.Dot(desiredVelocity, xAxis);
                float desiredY = Vector3.Dot(desiredVelocity, yAxis);
                float desiredZ = Vector3.Dot(desiredVelocity, zAxis);

                float deltaX = desiredX - currentX;
                float deltaY = desiredY - currentY;
                float deltaZ = desiredZ - currentZ;

                float accelStep = currentPreset.maxAcceleration * Time.fixedDeltaTime;

                velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), accelStep);
                velocity += yAxis * Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), accelStep);
                velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), accelStep);

                return;
            }
            velocity = Vector3.Lerp(velocity, Vector3.zero, 5f * Time.fixedDeltaTime);
        }
    }

    void CalculateNextPivotPoint()
    {
        if (!isSolo && pivotPoints != null && pivotPoints.Length > 0)
        {
            // Cycle through pivot points (clockwise / counter-clockwise)
            if (isClockwise)
                currentPivotIndex = (currentPivotIndex + 1) % pivotPoints.Length;
            else
            {
                currentPivotIndex = (currentPivotIndex - 1 + pivotPoints.Length) % pivotPoints.Length;
            }

            for (int i = 0; i < pivotPoints.Length; i++)
            {
                // Random offset per axis
                Vector3 randomOffset = new Vector3(
                    Random.Range(-currentPreset.pivotRandomOffset, currentPreset.pivotRandomOffset),
                    Random.Range(-currentPreset.pivotRandomOffset, currentPreset.pivotRandomOffset),
                    Random.Range(-currentPreset.pivotRandomOffset, currentPreset.pivotRandomOffset)
                );

                // Rotate the base offset by the current pivot rotation
                Vector3 rotatedOffset = pivot.rotation * initialPivotOffsets[i];

                // Apply to pivot position
                pivotPoints[i].position = transform.position + rotatedOffset + randomOffset;
            }
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
                float strength = Mathf.Clamp01((currentPreset.detectionRadius - distance) / currentPreset.detectionRadius);
                totalAvoidance += away.normalized * currentPreset.avoidanceForce * strength;
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
        if (((1 << other.gameObject.layer) & currentPreset.obstacleMask) != 0 && !other.isTrigger)
        {
            if (!nearbyObstacles.Contains(other))
            {
                nearbyObstacles.Add(other);
                if (other.tag == "Indestructible Ground")
                    indestructibleCount++;
                else
                    destructibleCount++;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (nearbyObstacles.Contains(other))
        {
            nearbyObstacles.Remove(other);
            if (other.tag == "Indestructible Ground")
                indestructibleCount--;
            else
                destructibleCount--;
        }
    }

    // we'll use this later when i decide to add destructible cover / asteroids, etc. We'll limit the hit count and reset after each attack or end the attack prematurely if we the enemy destroys too much
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.gameObject.name);
        

        EntityHealthController hpController = collision.gameObject.GetComponent<EntityHealthController>();
        if (hpController != null)
        {
            currentCollisionsBeforePrematureStop++;
            hpController.InstantlyDie();
        }
    }

    Vector3 ChoosePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        List<Vector3> validPivots = new List<Vector3>();
        foreach (var dir in availableDirections)
        {
            // Start with pure cardinal pivot
            Vector3 candidate = playerPos + dir * currentPreset.pivotDistance;

            // Then apply global "forward push"
            candidate += playerForward * currentPreset.pivotForwardPush;

            // Offset upwards from ground
            candidate.y += currentPreset.pivotHeightOffset;

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
        return playerPos + playerForward * (currentPreset.pivotDistance + currentPreset.pivotForwardPush) + Vector3.up * currentPreset.pivotHeightOffset;
    }

    Vector3 CheckForceTopPivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (currentPreset.pivotDistance + currentPreset.pivotForwardPush) + Vector3.up * currentPreset.pivotHeightOffset;

        Vector3 dir = availableDirections[3]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * currentPreset.pivotDistance;

        pivot += playerForward * currentPreset.pivotForwardPush;
        pivot.y += currentPreset.pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (currentPreset.pivotDistance + currentPreset.pivotForwardPush) + Vector3.up * currentPreset.pivotHeightOffset;
    }

    Vector3 UpdatePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (currentPreset.pivotDistance + currentPreset.pivotForwardPush) + Vector3.up * currentPreset.pivotHeightOffset;

        Vector3 dir = availableDirections[currentDirectionIndex]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * currentPreset.pivotDistance;

        pivot += playerForward * currentPreset.pivotForwardPush;
        pivot.y += currentPreset.pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (currentPreset.pivotDistance + currentPreset.pivotForwardPush) + Vector3.up * currentPreset.pivotHeightOffset;
    }


    void UpdateDirections()
    {
        availableDirections = new List<Vector3>
        {
            playerCamera.transform.forward,
            //-playerCamera.forward, // not the back of the player, feels really clunky
            playerCamera.transform.right,
            -playerCamera.transform.right,
            player.transform.up, // top direction
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
            return playerCamera.transform.forward;

        return lastPlayerForward != Vector3.zero ? lastPlayerForward : Vector3.forward;
    }

    bool HasLineOfSight(Vector3 point)
    {
        if (point == Vector3.zero) return false;

        Vector3 dir = (player.transform.position - point).normalized;
        float distance = Vector3.Distance(point, player.transform.position);

        // LayerMask can help avoid hitting the worm's own colliders
        if (Physics.Raycast(point, dir, out RaycastHit hit, distance, currentPreset.LOSMask))
        {
            // Make sure we hit the player's collider (even if it's a child)
            return hit.collider.GetComponentInParent<SpaceShooterController>() != null;
        }

        // No obstruction, line of sight is clear
        return true;
    }

    /// <summary>
    /// Handles the death of the sub enemy, properly replaces references and then reinitializes the whole enemy to use the specific AI pattern
    /// </summary>
    /// <param name="isA">Specifies which sub enemy died</param>
    public void HandlePartnerDeath(bool isA)
    {
        // Stop all current attack activity
        ForceStopAttack();

        // Determine which one died
        if (isA)
        {
            if (enemyB != null)
            {
                enemyA = enemyB;
                enemyB = null;
            }
        }
        else // enemy B died
        {
            if (enemyA != null)
            {
                enemyB = null;
            }
        }

        // Reinitialize params for solo and duo movements
        Reinitialize();

        // --- Reset paired behavior state ---
        currentPivotIndex = 0;
        nextPivotTime = 0f;
        burstingToPivot = false;
        aligningToPivot = false;
        aligningToAnchors = false;
        enemiesAligned = false;
        isAttacking = false;

        // --- Reinitialize solo movement pattern ---
        velocity = Vector3.zero;
        desiredVelocity = Vector3.zero;
        currentSpiralStep = 0;

        enemyA.transform.localPosition = Vector3.zero;
        enemyA.transform.localPosition = Vector3.zero;

        // Prevent paired pivot updates from running
        //pivotPoints = null;

        //Debug.Log("[BarrierRammerEnemyCentralized] Partner died — switching to SOLO mode.");
    }

    public void ResetBehaviorState()
    {
        enemyA = originalEnemyARef;
        enemyB = originalEnemyBRef;
        currentSpiralStep = 0;
        isAttacking = false;
        enemiesAligned = false;
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
