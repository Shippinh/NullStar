using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Splines;

//omni.OS
public class SpaceShooterController : MonoBehaviour
{
    // References
    [field: Header("References")]
    public CustomInputs inputConfig;
    public EntityHealthController healthController;
    public Rigidbody body;
    public Transform hitboxRef;
    public CameraControllerNew cameraControllerRef;
    public PlayerRailController railControllerRef;
    public Transform playerRoot;

    // Movement
    [field: Header("Basic Movement")]
    [SerializeField, Range(0f, 1000f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxVerticalSpeed = 10f;
    [SerializeField, Range(0f, -1000f)] float maxFallSpeed = -10f;
    [SerializeField, Range(0f, 1000f)] float maxAcceleration = 10f;
    [SerializeField, Range(0f, 1000f)] float maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)] float jumpForce = 2f;
    [SerializeField, Range(0f, 100f)] float maxSpeedDecayRate = 2f;
    [SerializeField, Range(0f, 2000f)] float jetpackAcceleration = 10f;
    [SerializeField] float defaultMaxSpeed;

    // Overboost System
    [field: Header("Overboost Movement")]
    public bool overboostMode;
    public bool overboostOverheatMode;
    public bool overboostInitiated = false;
    public float overboostActivationDelay = 2f;
    [SerializeField] float overboostChargeTimer;
    [SerializeField, Range(0f, 1000f)] float maxOverboostInitiationSpeed = 5f;
    public float maxExtraOverboostSpeed;
    public float defaultMaxExtraOverboostSpeed;
    [SerializeField, Range(0f, 1000f)] float currentExtraOverboostSpeed;
    [SerializeField, Range(0f, 500f)] float extraOverboostSpeedRate = 5f;
    [SerializeField, Range(0f, 1000f)] float maxOverboostSpeed = 20f;
    [SerializeField] float defaultMaxOverboostSpeed;
    [SerializeField] float overboostTurnMultiplier;
    [SerializeField] float maxOverboostVerticalSpeed;
    public float overboostDuration = 7.5f;
    public float overboostDurationCurrent;
    public float overboostDurationRestoreRate = 1f;
    public float overboostOverheatDuration = 10f;
    public float overboostOverheatDurationCurrent;
    public float overboostOverheatDurationRestoreRate = 1f;
    public bool overboostForward = true;

    // Boost System
    [field: Header("Boost Movement")]
    public bool boostMode = false;
    public bool boostInitiated = false;

    // Dodge System
    [field: Header("Dodge Movement")]
    [SerializeField, Range(0f, 1000)] float dodgeMaxSpeed = 10f;
    [SerializeField] float dodgeMaxSpeedCap;
    [SerializeField, Range(0f, 1000f)] float perDodgeMaxSpeedIncrease = 6.5f;
    [SerializeField, Range(0f, 1000f)] float perDodgeMaxOverboostSpeedIncrease = 8f;
    [SerializeField] float dodgeMaxOverboostSpeedCap;
    [SerializeField] public int maxDodgeCharges = 5;
    [SerializeField] public int dodgeCharges;
    [SerializeField] float dodgeRechargeTime = 1.5f;
    [SerializeField] private List<float> dodgeCooldowns;
    [SerializeField] private bool allDodgesFull = true;
    [SerializeField] private float timeSinceLastDodge = 0f;
    [SerializeField] float timeToImproveDodgeRechargeRate = 3f;
    [SerializeField] float dodgeRechargeRate = 3f;
    [SerializeField] float dodgeTimeLimit = 0.15f;
    float dodgeTime;
    public bool isDodging = false;

    // Rage System
    [field: Header("Rage")]
    public bool rageCharged = false;
    public bool rageActive = false;
    public float rageDuration = 5f;
    public float rageDurationCurrent;
    [SerializeField] float rageChargeTimer;
    public float rageRechargeTimer = 40f;

    // Adrenaline System
    [field: Header("Adrenaline")]
    public bool adrenalineCharged = false;
    public bool adrenalineActive = false;
    public float adrenalineDuration = 3f;
    public float adrenalineDurationCurrent;
    [SerializeField] float adrenalineChargeTimer;
    public float adrenalineRechargeTimer = 20f;

    // Player Input
    [field: Header("Inputs")]
    public int forwardInput;
    public int backwardInput;
    public int leftInput;
    public int rightInput;
    public bool jumpInput;
    public bool healInput;
    public int shootInput;
    public int horizontalDodgeInput;
    public int verticalDodgeInput;
    public bool rageInput;
    public bool adrenalineInput;
    public InputToggle overboostToggle;
    public Vector3 lastExclusiveDirectionalInput = Vector3.forward;

    // State Flags
    [field: Header("Other")]
    public bool isCooled = true;
    public bool playerHasControl = true;
    public float OverboostVelocityDeathLimit {  get; protected set; }

    // AI Dynamic Orbit Calculation
    [Header("AI Dynamic Orbit Calculation")]
    public float baseMinRange = 1000f;
    public float baseMaxRange = 1500f;

    public float safeBuffer = 5f;
    public LayerMask obstacleMask;

    public Color minRangeColor = Color.cyan;
    public Color maxRangeColor = Color.magenta;
    public Color safeRadiusColor = Color.yellow;

    [SerializeField] public float minRange;
    [SerializeField] public float maxRange;
    [SerializeField] public float currentSafeRadius;

    // Precompute sampling directions over a sphere
    private List<Vector3> sampleDirections;

    // Timers and Durations
    // (already included under each system above for clarity)

    // Stats / Tuning (General)
    [SerializeField, Range(0, 90)] float maxGroundAngle = 25f;

    // Events
    public event Action OnOverboostInitiation;
    public event Action OnOverboostInitiationCancel;
    public event Action OnOverboostActivation;
    public event Action OnOverboostStop;
    public event Action OnOverboostOverheat;
    public event Action OnOverheatCoolingInitiated;
    public event Action OnOverheatCoolingConcluded;
    public event Action OnDodgeUsed;
    public event Action OnDodgeActualRechargeStart;
    public event Action OnDodgeChargeGain;

    // Physics and Grounding
    Vector3 contactNormal;
    int groundContactCount;
    bool OnGround => groundContactCount > 0;
    float minGroundDotProduct;

    // Internal State Vectors
    public Vector3 velocity, desiredVelocity, desiredDodgeVelocity;
    // Stored offset in spline-local space
    //public Vector2 splineOffset;



    void OnValidate() 
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake() 
    {
        Application.targetFrameRate = 120; // stupid hack to prevent high fps issues with inputs
        QualitySettings.vSyncCount = 0;

        if(!cameraControllerRef)
            cameraControllerRef = FindObjectOfType<CameraControllerNew>();

        body = GetComponent<Rigidbody>();
        overboostToggle = new InputToggle(inputConfig.Overboost);
        healthController.TookHit += HandleTakenHits;

        defaultMaxSpeed = maxSpeed;
        defaultMaxOverboostSpeed = maxOverboostSpeed;
        defaultMaxExtraOverboostSpeed = maxExtraOverboostSpeed;
        dodgeMaxSpeedCap = defaultMaxSpeed + (maxDodgeCharges - 1) * perDodgeMaxSpeedIncrease;
        dodgeMaxOverboostSpeedCap = defaultMaxOverboostSpeed + defaultMaxExtraOverboostSpeed + (maxDodgeCharges - 1) * perDodgeMaxOverboostSpeedIncrease;
        OnValidate();
        dodgeCharges = maxDodgeCharges;
        dodgeCooldowns = new List<float>(maxDodgeCharges);
        for (int i = 0; i < maxDodgeCharges; i++)
            dodgeCooldowns.Add(0f);
        overboostChargeTimer = 0f;
        rageChargeTimer = 0f;
        adrenalineChargeTimer = 0f;
        rageDurationCurrent = rageDuration;
        adrenalineDurationCurrent = adrenalineDuration;
        overboostDurationCurrent = 0f;
        overboostOverheatDurationCurrent = 0f;

        sampleDirections = GenerateSphereDirections(42); // ~42 evenly distributed directions

        OverboostVelocityDeathLimit = maxOverboostSpeed * 0.65f; // hardcoded = bad, remake this

        railControllerRef = GetComponent<PlayerRailController>();

        // DEBUG
        //boostDirection = Vector3.zero;
    }

    void Update()
    {
        AttachHitbox();
        AdjustMaxOverboostSpeed();
        HandleInput();
        if (!boostMode && !boostInitiated)
        {
            HandleOverboostInitiation();
        }
        HandleRageCharge();
        HandleAdrenalineCharge();
        HandleDodgeRecharge();
        HandleOverboostDuration();
    }

    void FixedUpdate()
    {
        CalculateDesiredVelocity();
        UpdateState();

        if (boostMode)
        {
            AdjustBoostVelocity();
            AdjustBoostAirVelocity();
        }
        else
        {
            AdjustVelocity();
        }
        AdjustDodgeVelocity();


        if (!OnGround && body.useGravity) 
        {
            ApplyGravity();
        }

        if (jumpInput && overboostMode == false)
        {
            if (OnGround)
            {
                Jump();
            }
            else
            {
                AdjustAirVelocity();
            }
        }
        
        if (overboostInitiated == true)
        {
            AdjustAirVelocity();
        }

        if(healInput)
        {
            healthController.Heal(100, true);
        }

        body.velocity = velocity;
        ClearState();

        DecayMaxSpeedToDefault();
    }

    void AttachHitbox()
    {
        hitboxRef.position = transform.position;
    }

    void AdjustMaxOverboostSpeed()
    {
        // Calculate current dodge-contributed max speeds before clamping
        float dodgeBonusSpeed = (maxDodgeCharges - dodgeCharges) * perDodgeMaxSpeedIncrease;
        float dodgeBonusOverboostSpeed = (maxDodgeCharges - dodgeCharges) * perDodgeMaxOverboostSpeedIncrease;

        // Cap dodge bonuses
        dodgeBonusSpeed = Mathf.Min(dodgeBonusSpeed, dodgeMaxSpeedCap - defaultMaxSpeed);
        dodgeBonusOverboostSpeed = Mathf.Min(dodgeBonusOverboostSpeed, dodgeMaxOverboostSpeedCap - defaultMaxOverboostSpeed - defaultMaxExtraOverboostSpeed);

        // Update maxSpeed and base overboost speed with dodge bonuses
        maxSpeed = defaultMaxSpeed + dodgeBonusSpeed;

        if (overboostMode && overboostInitiated)
        {
            float rateMultiplier = overboostOverheatMode ? 2f : 1f;
            currentExtraOverboostSpeed = Mathf.MoveTowards(
                currentExtraOverboostSpeed,
                maxExtraOverboostSpeed,
                extraOverboostSpeedRate * rateMultiplier * Time.deltaTime
            );
        }
        else
        {
            currentExtraOverboostSpeed = 0f;
        }

        // Final max overboost speed
        maxOverboostSpeed = defaultMaxOverboostSpeed + currentExtraOverboostSpeed + dodgeBonusOverboostSpeed;
    }



    // Gets invoked whenever the referenced entity gets hit
    void HandleTakenHits()
    {
        // Taking hits resets adrenaline meter
        ResetAdrenaline();
    }

    void ResetAdrenaline()
    {
        if(!adrenalineActive)
        {
            adrenalineChargeTimer = 0f;
            if(adrenalineCharged)
            {
                adrenalineCharged = false;
            }
        }
    }

    void DecayMaxSpeedToDefault()
    {
        if (!AnyMovementInput() && !isDodging)
        {
            maxSpeed = Mathf.MoveTowards(maxSpeed, defaultMaxSpeed, maxSpeedDecayRate * Time.fixedDeltaTime);
            maxOverboostSpeed = Mathf.MoveTowards(maxOverboostSpeed, defaultMaxOverboostSpeed, maxSpeedDecayRate * Time.fixedDeltaTime);
        }
    }

    // Custom gravity
    void ApplyGravity()
    {
        float gravityMultiplier = 4f; // Increase for stronger gravity
        velocity += Physics.gravity * gravityMultiplier * Time.fixedDeltaTime;
        if (velocity.y < maxFallSpeed && !overboostMode && !boostMode)
            velocity.y = maxFallSpeed;
    }

    void ClearState() 
    {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
    }

    void UpdateState() 
    {
        velocity = body.velocity;
        if (OnGround) 
        {
            if (groundContactCount > 1) 
            {
                contactNormal.Normalize();
            }
        }
        else 
        {
            contactNormal = Vector3.up;
        }
    }

    void CalculateDesiredVelocity()
    {
        Vector3 moveDirection = Vector3.zero;
        Vector3 worldDirection = Vector3.zero;
        float horizontalSpeed;
        float verticalSpeed;
        float verticalComponent;

        if (overboostMode)
        {
            // Pre apply vertical speed
            verticalSpeed = maxOverboostSpeed;
            //verticalSpeed = maxOverboostVerticalSpeed;

            // If we already overboosting
            if (overboostInitiated)
            {
                // Remember the input
                float horizontalInput = (rightInput + leftInput) * overboostTurnMultiplier; // left or right on demand
                float verticalInput = 1; //constant jump
                float forwardBackwardInput = (forwardInput + backwardInput) * overboostTurnMultiplier; // forward or backward on demand

                // Determine lock direction and adjustement direction
                // lastExclusiveDirectionalInput is calculated on input level for extra precision
                if (lastExclusiveDirectionalInput.x != 0)
                    horizontalInput = lastExclusiveDirectionalInput.x;
                else if (lastExclusiveDirectionalInput.z != 0)
                    forwardBackwardInput = lastExclusiveDirectionalInput.z;

                moveDirection = new Vector3(horizontalInput, 0, forwardBackwardInput).normalized + new Vector3(0, verticalInput, 0);

                // Adjust world direction according to camera
                worldDirection =
                cameraControllerRef.mainCameraRef.transform.right * moveDirection.x +
                cameraControllerRef.mainCameraRef.transform.forward * moveDirection.z;

                worldDirection.Normalize(); // Let camera pitch determine final direction (includes vertical movement)

                // Set speed
                horizontalSpeed = maxOverboostSpeed;
                // Set y component
                verticalComponent = worldDirection.y;

                // Remember the desiredVelocity
                desiredVelocity = new Vector3(
                    worldDirection.x * horizontalSpeed,
                    verticalComponent * verticalSpeed,
                    worldDirection.z * horizontalSpeed);
            }
            // If we preparing to overboost
            else
            {
                horizontalSpeed = maxOverboostInitiationSpeed;
            }
        }
        // WORKS IN TANDEM WITH PLAYER RAIL CONTROLLER
        else if (boostMode)
        {
            verticalSpeed = maxOverboostSpeed;
            //verticalSpeed = maxOverboostVerticalSpeed;

            if (boostInitiated)
            {
                // Remember the input
                float horizontalInput = (rightInput + leftInput);
                float verticalInput = (forwardInput + backwardInput);
                float forwardBackwardInput = 0;

                moveDirection = new Vector3(horizontalInput, verticalInput, forwardBackwardInput);

                // Compute input offset in spline plane
                horizontalSpeed = maxOverboostSpeed;
                verticalComponent = moveDirection.y;

                // Determine if the player is looking backward
                // Dot product: forward along spline vs. player’s forward (or camera forward)
                bool invertHorizontal = cameraControllerRef.LookingForward;
                bool lookingSideways = cameraControllerRef.LookingSideways;

                float adjustedHorizontal = invertHorizontal ? -moveDirection.x : moveDirection.x;
                adjustedHorizontal = lookingSideways ? 0 : adjustedHorizontal;

                // Physics velocity only in offset plane
                desiredVelocity =
                    adjustedHorizontal * horizontalSpeed * railControllerRef.SplineRight +
                    verticalComponent * verticalSpeed * railControllerRef.SplineUp;

                // Kill any accidental forward drift
                desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, railControllerRef.SplineForward);
            }
            else
            {
                horizontalSpeed = maxOverboostInitiationSpeed;
            }
        }
        else if(!boostInitiated && !overboostInitiated)
        {
            // Remember the input
            float horizontalInput = rightInput + leftInput;
            float verticalInput = jumpInput ? 1 : 0;
            float forwardBackwardInput = forwardInput + backwardInput;

            moveDirection = new Vector3(horizontalInput, verticalInput, forwardBackwardInput);

            // Step 2C: Normal mode - movement restricted to horizontal plane, with separate Y input
            Vector3 camRight = cameraControllerRef.mainCameraRef.transform.right;
            Vector3 camForward = cameraControllerRef.mainCameraRef.transform.forward;
            camForward.y = 0; // Flatten forward to horizontal
            camForward.Normalize();

            worldDirection = camRight * moveDirection.x + camForward * moveDirection.z;
            worldDirection.Normalize(); // Horizontal movement only

            horizontalSpeed = maxSpeed;
            verticalSpeed = maxVerticalSpeed;
            verticalComponent = moveDirection.y;

            desiredVelocity = new Vector3(
                worldDirection.x * horizontalSpeed,
                verticalComponent * verticalSpeed,
                worldDirection.z * horizontalSpeed);
        }
    }

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;

        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), acceleration * Time.fixedDeltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), acceleration * Time.fixedDeltaTime);
    }
    
    void AdjustBoostAirVelocity()
    {
        float currentY = velocity.y;

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;

        float deltaY = desiredVelocity.y - currentY;

        velocity.y += Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), acceleration * Time.fixedDeltaTime);
    }

    void AdjustBoostVelocity()
    {
        // Spline plane axes
        Vector3 rightAxis = railControllerRef.SplineRight;
        Vector3 upAxis = railControllerRef.SplineUp;

        // Current velocity components in spline space
        float currentRight = Vector3.Dot(velocity, rightAxis);
        float currentUp = Vector3.Dot(velocity, upAxis);

        // Desired components in spline space
        float targetRight = Vector3.Dot(desiredVelocity, rightAxis);
        float targetUp = Vector3.Dot(desiredVelocity, upAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;

        float deltaRight = targetRight - currentRight;
        float deltaUp = targetUp - currentUp;

        // Accelerate toward desired velocity, spline-local only
        velocity += rightAxis * Mathf.Sign(deltaRight) *
            Mathf.Min(Mathf.Abs(deltaRight), acceleration * Time.fixedDeltaTime);

        velocity += upAxis * Mathf.Sign(deltaUp) *
            Mathf.Min(Mathf.Abs(deltaUp), acceleration * Time.fixedDeltaTime);

        // Safety: strip any accidental spline-forward drift
        velocity = Vector3.ProjectOnPlane(velocity, railControllerRef.SplineForward);
    }


    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;

        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;

        float deltaY = targetY - currentY;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), jetpackAcceleration * Time.fixedDeltaTime);
        velocity += yAxis * change;
    }

    void AdjustDodgeVelocity()
    {
        // Horizontal dodge for normal mode and overboost mode
        if (horizontalDodgeInput > 0 && !isDodging && verticalDodgeInput == 0 && dodgeCharges > 0 && boostMode == false)
        {
            //if no input in overboost mode - just stop the method
            if (rightInput + leftInput == 0 && lastExclusiveDirectionalInput.z != 0 && overboostMode)
                return;

            if (forwardInput + backwardInput == 0 && lastExclusiveDirectionalInput.x != 0 && overboostMode)
                return;


            isDodging = true;
            timeSinceLastDodge = 0f;
            dodgeTime = 0f;
            for (int i = 0; i < dodgeCooldowns.Count; i++)
            {
                if (dodgeCooldowns[i] <= 0f)
                {
                    dodgeCooldowns[i] = dodgeRechargeTime;
                    break;
                }
            }
            dodgeCharges = Mathf.Max(0, dodgeCharges - 1);
            OnDodgeUsed?.Invoke();


            if(overboostMode)
            {
                if (lastExclusiveDirectionalInput.x != 0)
                    desiredDodgeVelocity = new Vector3(lastExclusiveDirectionalInput.x, 0, (forwardInput + backwardInput) * overboostTurnMultiplier).normalized;
                else if (lastExclusiveDirectionalInput.z != 0)
                    desiredDodgeVelocity = new Vector3((rightInput + leftInput) * overboostTurnMultiplier, 0, lastExclusiveDirectionalInput.z).normalized;
            }
            else
            {
                desiredDodgeVelocity = new Vector3(rightInput + leftInput, 0, forwardInput + backwardInput).normalized;
            }

            Vector3 camRight = cameraControllerRef.mainCameraRef.transform.right;
            Vector3 camForward = cameraControllerRef.mainCameraRef.transform.forward;

            if (overboostMode)
            {
                desiredDodgeVelocity = camRight * desiredDodgeVelocity.x  + camForward * desiredDodgeVelocity.z;
            }
            else
            {
                camRight.y = 0;
                camForward.y = 0;
                if (desiredDodgeVelocity == Vector3.zero)
                    desiredDodgeVelocity = transform.forward;
                desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            }

            desiredDodgeVelocity.Normalize();

            maxSpeed = Mathf.Min(maxSpeed + perDodgeMaxSpeedIncrease, dodgeMaxSpeedCap);
            maxOverboostSpeed = Mathf.Min(maxOverboostSpeed + perDodgeMaxOverboostSpeedIncrease, dodgeMaxOverboostSpeedCap);

            if (desiredDodgeVelocity != Vector3.zero)
            {
                if (overboostMode)
                {
                    velocity = desiredDodgeVelocity * dodgeMaxSpeed * 1.5f;
                }
                else
                    velocity = desiredDodgeVelocity * dodgeMaxSpeed;
            }
        }

        // Vertical dodge
        /* This has a more apparent bug where after dodging upwards while flying upwards
         * it just doesn't do the full thrust resulting in a worse dodge compared
         * to when you would dodge without accelerating upwards
         * This happens because of the acceleration and speed limits for jetpack mode
        */ 
        if (verticalDodgeInput > 0 && !isDodging && horizontalDodgeInput == 0 && dodgeCharges > 0 && overboostMode == false && boostMode == false)
        {
            isDodging = true;
            timeSinceLastDodge = 0f;
            dodgeTime = 0f;
            for (int i = 0; i < dodgeCooldowns.Count; i++)
            {
                if (dodgeCooldowns[i] <= 0f)
                {
                    dodgeCooldowns[i] = dodgeRechargeTime;
                    break;
                }
            }
            dodgeCharges = Mathf.Max(0, dodgeCharges - 1);
            OnDodgeUsed?.Invoke();

            desiredDodgeVelocity = new Vector3(rightInput + leftInput, 1f, forwardInput + backwardInput).normalized;

            Vector3 camRight = cameraControllerRef.mainCameraRef.transform.right;
            Vector3 camForward = cameraControllerRef.mainCameraRef.transform.forward;
            camRight.y = 0f;
            camForward.y = 0f;

            if (desiredDodgeVelocity == Vector3.zero)
                desiredDodgeVelocity = transform.up;

            desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            desiredDodgeVelocity = new Vector3(desiredDodgeVelocity.x, 1f, desiredDodgeVelocity.z);
            desiredDodgeVelocity.Normalize();

            maxSpeed = Mathf.Min(maxSpeed + perDodgeMaxSpeedIncrease, dodgeMaxSpeedCap);

            if (desiredDodgeVelocity != Vector3.zero)
                velocity = desiredDodgeVelocity * dodgeMaxSpeed * 0.75f;

        }
        // Boost mode omnidirectional dodge
        if (!isDodging && dodgeCharges > 0 && boostMode && !overboostMode && horizontalDodgeInput > 0f)
        {
            float horizontal = rightInput + leftInput;
            float vertical = forwardInput + backwardInput;

            if (cameraControllerRef.LookingSideways)
            {
                horizontal = 0f;

                if (vertical == 0f)
                    return;
            }

            if (horizontal == 0f && vertical == 0f)
                return;

            // --- Sample spline to get orientation ---

            bool invertHorizontal = cameraControllerRef.LookingForward;
            bool lookingSideways = cameraControllerRef.LookingSideways;

            horizontal = invertHorizontal ? -horizontal : horizontal;

            // --- Compute dodge direction in spline plane only ---
            Vector3 inputDirection = new Vector3(horizontal, vertical, 0f).normalized;

            // Project input onto spline plane (ignore forward along spline)
            Vector3 dodgeDirection = railControllerRef.SplineRight * inputDirection.x + railControllerRef.SplineUp * inputDirection.y;
            //dodgeDirection.Normalize();

            // --- Forward along spline is ignored for dodge ---
            Vector3 finalVelocity = dodgeDirection * dodgeMaxSpeed; // NO spline-forward component

            // --- Apply dodge ---
            isDodging = true;
            dodgeTime = 0f;
            timeSinceLastDodge = 0f;

            for (int i = 0; i < dodgeCooldowns.Count; i++)
            {
                if (dodgeCooldowns[i] <= 0f)
                {
                    dodgeCooldowns[i] = dodgeRechargeTime;
                    break;
                }
            }
            dodgeCharges = Mathf.Max(0, dodgeCharges - 1);
            OnDodgeUsed?.Invoke();

            maxSpeed = Mathf.Min(maxSpeed + perDodgeMaxSpeedIncrease, dodgeMaxSpeedCap);
            maxOverboostSpeed = Mathf.Min(maxOverboostSpeed + perDodgeMaxOverboostSpeedIncrease, dodgeMaxOverboostSpeedCap);

            desiredDodgeVelocity = dodgeDirection;

            // Project onto the spline plane (right/up), removing forward component
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(finalVelocity, railControllerRef.SplineForward);

            // Extra safety: zero out any forward projection
            lateralVelocity -= railControllerRef.SplineForward * Vector3.Dot(lateralVelocity, railControllerRef.SplineForward);

            // **Set** velocity along spline plane axes instead of adding
            // Preserve any forward velocity along spline if needed
            float forwardSpeed = Vector3.Dot(velocity, railControllerRef.SplineForward);
            velocity = lateralVelocity + railControllerRef.SplineForward * forwardSpeed;

            //velocity = finalVelocity;
        }



        if (isDodging)
        {
            dodgeTime += Time.fixedDeltaTime;
            if (dodgeTime >= dodgeTimeLimit)
            {
                isDodging = false;
                dodgeTime = 0f;
            }
        }
    }


    void HandleDodgeRecharge()
    {
        timeSinceLastDodge = Mathf.Min(timeSinceLastDodge + Time.deltaTime, timeToImproveDodgeRechargeRate);

        float rechargeDelta = Time.deltaTime * ((timeSinceLastDodge >= timeToImproveDodgeRechargeRate) ? dodgeRechargeRate : 1f);

        int available = 0;

        for (int i = 0; i < dodgeCooldowns.Count; i++)
        {
            if (dodgeCooldowns[i] > 0f)
            {
                dodgeCooldowns[i] -= rechargeDelta;

                if (dodgeCooldowns[i] <= 0f)
                {
                    dodgeCooldowns[i] = 0f;
                    OnDodgeChargeGain?.Invoke();
                }
            }

            if (dodgeCooldowns[i] <= 0f)
                available++;
        }

        dodgeCharges = available;

        // Trigger event when all charges are full, only once
        if (available == maxDodgeCharges && !allDodgesFull)
        {
            allDodgesFull = true;
            OnDodgeActualRechargeStart?.Invoke();
        }
        else if (available < maxDodgeCharges)
        {
            allDodgesFull = false;
        }
    }



    // Handles a brief stop before overboost starts, as well as overboost cancel
    void HandleOverboostInitiation()
    {
        if (overboostMode == true && overboostInitiated == false)
        {
            if(body.useGravity == true)
                OnOverboostInitiation?.Invoke();
            body.useGravity = false;
            maxOverboostSpeed = maxOverboostInitiationSpeed;
            overboostChargeTimer += Time.deltaTime;

            float t = overboostChargeTimer / (overboostActivationDelay + 2f); // Normalized time (0 to 1)
            float factor = 1f - (t * t);

            body.velocity = new Vector3(body.velocity.x, body.velocity.y * factor, body.velocity.z);

            if(overboostChargeTimer >= overboostActivationDelay)
            {
                maxOverboostSpeed = defaultMaxOverboostSpeed;
                if (lastExclusiveDirectionalInput.x != 0)
                    body.velocity = (cameraControllerRef.mainCameraRef.transform.right * lastExclusiveDirectionalInput.x) * dodgeMaxSpeed;
                else if (lastExclusiveDirectionalInput.z != 0)
                    body.velocity = (cameraControllerRef.mainCameraRef.transform.forward * lastExclusiveDirectionalInput.z) * dodgeMaxSpeed;
                overboostInitiated = true;
                body.useGravity = true;
                overboostChargeTimer = 0f;
                OnOverboostActivation?.Invoke();
            }
        }

        if(overboostToggle.GetCurrentToggleState() == false)
        {
            if(overboostInitiated)
                OnOverboostStop?.Invoke();
            overboostInitiated = false;
            if(body.useGravity == false)
                OnOverboostInitiationCancel?.Invoke();
            body.useGravity = true;
            overboostChargeTimer = 0f;
            overboostOverheatMode = false;
        }
    }

    void HandleOverboostDuration()
    {
        // When in overboost
        if(overboostMode == true && overboostInitiated == true)
        {
            // When not overheating
            if(overboostOverheatMode == false)
            {
                overboostDurationCurrent += Time.deltaTime;

                // If we overboost when we can overheat
                if(overboostDurationCurrent >= overboostDuration && overboostOverheatDurationCurrent < overboostOverheatDuration)
                {
                    if(overboostOverheatMode == false)
                        OnOverboostOverheat?.Invoke();
                    overboostOverheatMode = true;
                }
                // If we overboost when we cannot overheat
                else if(overboostDurationCurrent >= overboostOverheatDuration && overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostMode = false;
                    overboostToggle.ForceToggle(false);
                    OnOverboostStop?.Invoke();
                    OnOverheatCoolingInitiated?.Invoke();
                    overboostInitiated = false;
                    overboostOverheatMode = false;
                }
            }
            // When overheating
            else
            {
                overboostOverheatDurationCurrent += Time.deltaTime;

                //healthController.TakeDamage(1, false);

                // If we overheat before death - initiate cooling and disable overboost
                if(overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostMode = false;
                    overboostToggle.ForceToggle(false);
                    OnOverboostStop?.Invoke();
                    OnOverheatCoolingInitiated?.Invoke();
                    overboostInitiated = false;
                    overboostOverheatMode = false;
                }
            }
        }
        // When not in overboost
        else if(overboostMode == false && overboostInitiated == false)
        {
            overboostDurationCurrent = Mathf.MoveTowards(overboostDurationCurrent, 0f, overboostDurationRestoreRate * Time.deltaTime);
            overboostOverheatDurationCurrent = Mathf.MoveTowards(overboostOverheatDurationCurrent, 0f, overboostOverheatDurationRestoreRate * Time.deltaTime);
        }

        if(overboostOverheatDurationCurrent > 0)
            isCooled = false;

        if(overboostOverheatDurationCurrent <= 0f &&!isCooled)
        {
            isCooled = true;
            OnOverheatCoolingConcluded?.Invoke();
        }
    }

    void HandleRageCharge()
    {
        if(rageCharged == false)
        {
            rageChargeTimer += Time.deltaTime;
            if(rageChargeTimer >= rageRechargeTimer)
            {
                rageCharged = true;
            }
        }

        if(rageCharged && rageInput)
        {
            rageActive = true;
        }

        if(rageActive)
        {
            rageDurationCurrent -= Time.deltaTime;
            if(rageDurationCurrent <= 0f)
            {
                rageActive = false;
                rageCharged = false;
                rageDurationCurrent = rageDuration;
                rageChargeTimer = 0f;
            }
        }
    }

    // This has to be updated in the future when it will be possible to track hits on player character
    // For now it's just like rage
    void HandleAdrenalineCharge()
    {
        if(adrenalineCharged == false)
        {
            adrenalineChargeTimer += Time.deltaTime;
            if(adrenalineChargeTimer >= adrenalineRechargeTimer)
            {
                adrenalineCharged = true;
            }
        }

        if(adrenalineCharged && adrenalineInput)
        {
            adrenalineActive = true;
        }

        if(adrenalineActive)
        {
            adrenalineDurationCurrent -= Time.deltaTime;
            if(adrenalineDurationCurrent <= 0f)
            {
                adrenalineActive = false;
                adrenalineCharged = false;
                adrenalineDurationCurrent = adrenalineDuration;
                adrenalineChargeTimer = 0f;
            }
        }
    }

    public (float orbitMin, float orbitMax) CalculateDynamicOrbit(
    float baseMin,
    float baseMax,
    float sweetSpot,
    float safeBuffer = 1f)
    {
        // Start with the maximum possible radius
        float maxRadius = baseMax;

        // Check each sample direction for obstacles
        foreach (var dir in sampleDirections)
        {
            if (Physics.SphereCast(transform.position, 1f, dir, out RaycastHit hit, baseMax, obstacleMask))
            {
                float safeDist = hit.distance - safeBuffer;
                maxRadius = Mathf.Min(maxRadius, safeDist); // reduce max if obstacle is closer
            }
        }

        // Ensure max range >= min range + sweetSpot
        maxRadius = Mathf.Max(maxRadius, sweetSpot);

        // Compute dynamic min range based on max and sweet spot
        float minRadius = maxRadius - sweetSpot;

        // Clamp to non-negative values
        minRadius = Mathf.Max(0f, minRadius);
        maxRadius = Mathf.Max(minRadius + sweetSpot, maxRadius);

        return (minRadius, maxRadius);
    }


    // Generate evenly spread directions on a sphere (using "Fibonacci sphere")
    private List<Vector3> GenerateSphereDirections(int samples)
    {
        List<Vector3> dirs = new List<Vector3>(samples);
        float offset = 2f / samples;
        float increment = Mathf.PI * (3f - Mathf.Sqrt(5f));

        for (int i = 0; i < samples; i++)
        {
            float y = ((i * offset) - 1) + (offset / 2);
            float r = Mathf.Sqrt(1 - y * y);
            float phi = i * increment;

            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;

            dirs.Add(new Vector3(x, y, z).normalized);
        }
        return dirs;
    }

    void Jump()
    {
        if (OnGround)
        {
            velocity += Vector3.up * jumpForce;
        }
    }

    public bool GetPlayerOnGround()
    {
        return OnGround;
    }

    void OnCollisionEnter(Collision collision) 
    {
        EvaluateCollision(collision);
    }

    void OnCollisionStay(Collision collision) 
    {
        EvaluateCollision(collision);
    }

    void EvaluateCollision(Collision collision) 
    {
        for (int i = 0; i < collision.contactCount; i++) 
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGroundDotProduct) 
            {
                groundContactCount += 1;
                contactNormal += normal;
            }
        }
    }

    Vector3 ProjectOnContactPlane(Vector3 vector) 
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
    }

    void HandleInput()
    {
        forwardInput = Input.GetKey(inputConfig.MoveUp) ? 1 : 0;
        backwardInput = Input.GetKey(inputConfig.MoveDown) ? -1 : 0;
        leftInput = Input.GetKey(inputConfig.MoveLeft) ? -1 : 0;
        rightInput = Input.GetKey(inputConfig.MoveRight) ? 1 : 0;
        jumpInput = Input.GetKey(inputConfig.Ascend);
        horizontalDodgeInput = Input.GetKeyDown(inputConfig.HorizontalDodge) ? 1 : 0;
        verticalDodgeInput = Input.GetKeyDown(inputConfig.VerticalDodge) ? 1 : 0;
        healInput = Input.GetKey(inputConfig.Heal);
        shootInput = Input.GetKey(inputConfig.Shoot) ? 1 : 0;
        rageInput = Input.GetKey(inputConfig.RageMode);
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode);

        if (boostMode == false)
        {
            //railControllerRef.playerSplineAnimateRef.enabled = false; // NEED TO RESET ORIENTATION TOO
            
            if (isCooled || overboostMode)
            {
                overboostToggle.UpdateToggle();
                overboostMode = overboostToggle.GetCurrentToggleState();
            }
        }
        //else
            //railControllerRef.playerSplineAnimateRef.enabled = true;


        if (overboostInitiated == false)
        {/*
            overboostForward = true;
            // Determine in which direction to overboost
            if (forwardInput == 1 && (backwardInput == 0 || backwardInput == -1))
                overboostForward = true;
            else if (forwardInput == 0 && backwardInput == -1)
                overboostForward = false;*/

            if (Input.GetKey(inputConfig.MoveLeft)) lastExclusiveDirectionalInput = new Vector3(-1, 0, 0);
            if (Input.GetKey(inputConfig.MoveRight)) lastExclusiveDirectionalInput = new Vector3(1, 0, 0);
            if (Input.GetKey(inputConfig.MoveUp)) lastExclusiveDirectionalInput = new Vector3(0, 0, 1);
            if (Input.GetKey(inputConfig.MoveDown)) lastExclusiveDirectionalInput = new Vector3(0, 0, -1);
        }
    }

    public bool AnyMovementInput()
    {
        return overboostMode && overboostInitiated ? true : forwardInput != 0 || backwardInput != 0 || leftInput != 0 || rightInput != 0;
    }

    public bool AnySidewaysMovementInput()
    {
        return leftInput != 0 || rightInput != 0;
    }

    public bool AnyForwardMovementInput()
    {
        return forwardInput != 0 || backwardInput != 0;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = safeRadiusColor;
        Gizmos.DrawWireSphere(transform.position, currentSafeRadius);

        Gizmos.color = minRangeColor;
        Gizmos.DrawWireSphere(transform.position, minRange);

        Gizmos.color = maxRangeColor;
        Gizmos.DrawWireSphere(transform.position, maxRange);
    }
}


public class InputToggle
{
    private string axisName;
    private KeyCode keyName;
    private bool currentToggleState;
    private bool isAxis;

    public InputToggle(string axisName)
    {
        this.axisName = axisName;
        this.currentToggleState = false;
        this.isAxis = true;
    }

    public InputToggle(KeyCode keyName)
    {
        this.keyName = keyName;
        this.currentToggleState = false;
        this.isAxis = false;
    }

    public void UpdateToggle()
    {
        if (isAxis)
        {
            if (Input.GetButtonDown(axisName)) // Use Unity's button system
            {
                currentToggleState = !currentToggleState;
            }
        }
        else
        {
            if (Input.GetKeyDown(keyName)) // Directly toggles on key press
            {
                currentToggleState = !currentToggleState;
            }
        }
    }

    public bool GetCurrentToggleState()
    {
        return currentToggleState;
    }

    public void ForceToggle(bool value)
    {
        currentToggleState = value;
    }
}
