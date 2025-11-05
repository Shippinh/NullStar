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
    public bool isSolo;

    [Header("Movement Settings")]
    public float maxSpeed = 500f;
    public float minBurstDistance = 50f;
    public float maxBurstDistance = 50f;
    public float burstCooldown = 0.5f;
    public bool randomizeInitialBurstTimer = false;
    public float maxRandomInitialBurstTimer = 0.1f;

    public bool randomizeSpinDirection = true;
    [SerializeField] private bool isClockwise = true;

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

    [Header("Pre-Attack Burst Settings")]
    public float burstDuration;       // how long the burst lasts
    public float burstSpeed = 200f;           // how fast the enemy moves during burst
    private bool burstingToPivot = false;     // are we currently bursting?
    private float burstTimer = 0f;
    private Vector3 burstDir;

    [Header("Attack Pathfinding")]
    public Transform playerCamera;
    public bool canAttack = true;
    private Vector3 currentPivot;
    private int currentDirectionIndex = -1;
    private List<Vector3> availableDirections;
    public float pivotDistance = 25f;
    public float pivotForwardPush = 10f;   // worm goes past player
    public float pivotHeightOffset = 3f;   // lift pivot slightly above ground
    private Vector3 lastPlayerForward = Vector3.forward;
    public float pivotChangeInterval = 3f; // every 3 seconds
    private float nextPivotTime = 0f;
    public LayerMask LOSMask;

    [Header("Attack Sequence Handling")]
    public GameObject barrierObj;
    public bool barrierIsColliding;
    public Transform barrierLeftAnchor;
    public Transform barrierRightAnchor;
    public float maxAcceleration = 200f;
    public float attackRotationBreakoffDistance = 150f; // distance at which the enemy stops rotating towards the player aggressively during the attack
    public float alignTime = 1f; // time for the enemy controller to get into the currentPivot position (during that we also start rotating the enemy towards the player)
    [SerializeField] private float alignTimer = 0f;
    public float leftRightAlignTime = 1f; // time for enemy a and enemy b to take left and right positions
    [SerializeField] private float leftRightAlignTimer = 0f;
    public float attackTime = 3f; // time during which the enemy performs the attack (consequentially - moves)
    [SerializeField] private float attackTimer = 0f;
    public bool isAttacking; // general state, tells if the enemy is within the attack sequence
    public bool randomizeABLeftRightPos; // if true - enemy a and b will assume left and right position randomly
    public int maxCollisionsBeforePrematureStop = 3; // how many objects it can kill before breaking the attack sequence
    [SerializeField] private int currentCollisionsBeforePrematureStop = 0;
    private Quaternion alignStartRot;
    private Quaternion anchorStartRot;
    private Vector3 anchorStartAPos;
    private Vector3 anchorStartBPos;
    [SerializeField] private bool aligningToPivot = false;
    [SerializeField] private bool aligningToAnchors = false;
    [SerializeField] private bool enemiesAligned = false;


    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private Vector3 contactNormal = Vector3.up;
    private float nextBurstTime = 0f;
    private uint destructibleCount = 0;
    private uint indestructibleCount = 0;

    public int currentSpiralStep = 0;

    void Start()
    {
        originalEnemyARef = enemyA;
        originalEnemyBRef = enemyB;

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

        if (randomizeInitialBurstTimer)
            burstCooldown = Random.Range(burstCooldown - maxRandomInitialBurstTimer, burstCooldown + maxRandomInitialBurstTimer);

        if (randomizeSpinDirection)
        {
            int val = Random.Range(0, 2); // 50/50 to be clockwise or counter clockwise
            if(val == 0)
                isClockwise = false;
            if(val == 1)
                isClockwise = true;
        }

        burstDuration = burstCooldown;

        if (enemyA != null && enemyB != null)
            isSolo = false;
        else if (enemyA)
            isSolo = true;
    }

    private void LateUpdate()
    {
        if (player.GetPlayerOnGround())
            currentPivot = CheckForceTopPivot();
        else
            currentPivot = UpdatePivot();

        AttachPivot();
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

        if (distToPivot >= minBurstDistance && distToPivot <= maxBurstDistance)
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
        transform.position += burstDir * burstSpeed * Time.deltaTime;
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
            float t = 1f - Mathf.Pow(0.01f, alignTimer / alignTime);

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
            if (alignTimer >= alignTime)
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

            if (leftRightAlignTimer >= leftRightAlignTime)
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
            if(indestructibleCount > 0 || currentCollisionsBeforePrematureStop >= maxCollisionsBeforePrematureStop)
            {
                //barrierIsColliding = false;
                currentCollisionsBeforePrematureStop = 0;
                isAttacking = false;
                enemiesAligned = false;
                barrierObj.SetActive(false);
                Debug.Log("Finishing the attack prematurely!");
            }

            attackTimer += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, attackTimer / attackTime);

            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            float distanceScaler = Mathf.Clamp01(distanceToPlayer / attackRotationBreakoffDistance); //smooth fade when getting closer to player

            // Move forward relative to the pivot orientation
            desiredVelocity = pivot.forward * maxSpeed;

            Vector3 playerDir = (player.transform.position - pivot.position).normalized;
            if (playerDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(playerDir, Vector3.up);

                // dynamically scale rotation speed by distance
                float dynamicRotationSpeed = pivotRotationSpeed * distanceScaler;

                pivot.rotation = Quaternion.RotateTowards(
                    pivot.rotation,
                    targetRot,
                    dynamicRotationSpeed * Time.deltaTime
                );
            }

            // End the attack after attackTime expires
            if (attackTimer >= attackTime)
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
                    pivotRotationSpeed * Time.fixedDeltaTime
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
            float distanceScaler = Mathf.Clamp01(distanceToPlayer / (spiralFadeDistance * 2f));
            if (distanceToPlayer < spiralFadeDistance)
                distanceScaler = 0f;

            float spiralMagnitude = maxSpiralOffset * distanceScaler;
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
                                 + up * Mathf.Sin(spiralAngle) * spiralMagnitude * verticalOffsetMultiplier;

            // --- Combine spiral + chaos ---
            Vector3 offsetCombined = spiralOffset + chaoticOffset;

            // --- Burst movement ---
            Vector3 targetDir = (toPlayerDir + avoidanceVector.normalized).normalized;
            float burstDistance = Mathf.Clamp(distanceToPlayer * 0.5f, minBurstDistance, maxBurstDistance);

            Vector3 burstTarget = transform.position + targetDir * burstDistance + offsetCombined;
            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > maxSpeed)
                desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }
        else
        {
            // --- Pivot selection ---
            if (Time.time >= nextPivotTime)
            {
                currentPivot = ChoosePivot();
                nextPivotTime = Time.time + pivotChangeInterval;
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
            float burstDistance = Mathf.Clamp(distanceToPivot * 0.5f, minBurstDistance, maxBurstDistance);
            Vector3 burstTarget = transform.position + (toPivotDir + avoidanceVector.normalized).normalized * burstDistance + chaoticOffset;

            desiredVelocity = (burstTarget - transform.position) / Time.fixedDeltaTime;

            if (desiredVelocity.magnitude > maxSpeed)
                desiredVelocity = desiredVelocity.normalized * maxSpeed;
        }
    }



    void AdjustVelocity()
    {
        if (Time.time >= nextBurstTime && !isAttacking)
        {
            velocity = desiredVelocity;
            nextBurstTime = Time.time + burstCooldown;
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

                float accelStep = maxAcceleration * Time.fixedDeltaTime;

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
        /*if (collision.gameObject.CompareTag("Destructibles"))
        {
            Debug.Log("Barrier physical collision");
            barrierIsColliding = true;
        }*/

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
            Vector3 candidate = playerPos + dir * pivotDistance;

            // Then apply global "forward push"
            candidate += playerForward * pivotForwardPush;

            // Offset upwards from ground
            candidate.y += pivotHeightOffset;

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
        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }

    Vector3 CheckForceTopPivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;

        Vector3 dir = availableDirections[3]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * pivotDistance;

        pivot += playerForward * pivotForwardPush;
        pivot.y += pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }

    Vector3 UpdatePivot()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = GetPlayerForward();

        if (currentDirectionIndex < 0 || currentDirectionIndex >= availableDirections.Count)
            return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;

        Vector3 dir = availableDirections[currentDirectionIndex]; // recalc direction relative to camera
        Vector3 pivot = playerPos + dir * pivotDistance;

        pivot += playerForward * pivotForwardPush;
        pivot.y += pivotHeightOffset;

        if (HasLineOfSight(pivot))
            return pivot;

        return playerPos + playerForward * (pivotDistance + pivotForwardPush) + Vector3.up * pivotHeightOffset;
    }


    void UpdateDirections()
    {
        availableDirections = new List<Vector3>
        {
            playerCamera.forward,
            //-playerCamera.forward,
            playerCamera.right,
            -playerCamera.right,
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
            return playerCamera.forward;

        return lastPlayerForward != Vector3.zero ? lastPlayerForward : Vector3.forward;
    }

    bool HasLineOfSight(Vector3 point)
    {
        if (point == Vector3.zero) return false;

        Vector3 dir = (player.transform.position - point).normalized;
        float distance = Vector3.Distance(point, player.transform.position);

        // LayerMask can help avoid hitting the worm's own colliders
        if (Physics.Raycast(point, dir, out RaycastHit hit, distance, LOSMask))
        {
            // Make sure we hit the player's collider (even if it's a child)
            return hit.collider.GetComponentInParent<SpaceShooterController>() != null;
        }

        // No obstruction, line of sight is clear
        return true;
    }
    public void HandlePartnerDeath(bool isA)
    {
        // Stop all current attack activity
        ForceStopAttack();

        // Mark solo
        isSolo = true;

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
        pivotPoints = null;

        Debug.Log("[BarrierRammerEnemyCentralized] Partner died — switching to SOLO mode.");
    }

    public void ResetBehaviorState(bool isSoloPtr)
    {
        isSolo = isSoloPtr;
        enemyB = originalEnemyBRef;
        enemyA = originalEnemyARef;
        velocity = Vector3.zero;
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
