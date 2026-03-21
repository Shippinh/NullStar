using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Splines;
using static PlayerRailController;
using static UnityEditor.IMGUI.Controls.CapsuleBoundsHandle;

public enum PlayerState
{
    Normal,
    OverboostInitiating,
    OverboostActive,
    BoostAttaching,
    BoostDetaching,
    BoostActive
}

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
    public SpaceShooterPlasmaGunController gunControllerRef;

    [field: Header("Player playerState")]
    [SerializeField] public PlayerState playerState = PlayerState.Normal;

    // Movement
    [field: Header("Basic Movement")]
    [SerializeField, Range(0f, 1000f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxVerticalSpeed = 10f;
    [SerializeField, Range(0f, -1000f)] float maxFallSpeed = -10f;
    [SerializeField, Range(0f, 100f)] float maxFallSpeedDecayRate = 10f;
    [SerializeField, Range(0f, 1000f)] float maxRiseSpeed = 10f;
    [SerializeField, Range(0f, 100f)] float maxRiseSpeedDecayRate = 10f;
    [SerializeField, Range(0f, 1000f)] float maxAcceleration = 10f;
    [SerializeField, Range(0f, 1000f)] float maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)] float jumpForce = 2f;
    [SerializeField, Range(0f, 100f)] float maxSpeedDecayRate = 2f;
    [SerializeField, Range(0f, 2000f)] float jetpackAcceleration = 10f;
    [SerializeField] float defaultMaxSpeed;
    [SerializeField] float gravityModifier = 1f;

    // Overboost System
    [field: Header("Overboost Movement")]
    public bool overboostOverheatMode;
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

    // Transition
    [SerializeField] private float attachDuration = 0f;
    [SerializeField] private float attachDurationCurrent = 0f;
    [SerializeField] private Vector3 attachStartPosition;
    [SerializeField] private Quaternion attachStartRotation;
    [SerializeField] private float attachStartSpeed = 0f;
    [SerializeField] private float attachDesiredOffsetX = 0f;
    [SerializeField] private float attachDesiredOffsetY = 0f;

    [SerializeField] private bool detachRotationActive;
    [SerializeField] private float detachRotationDuration;
    [SerializeField] private float detachRotationElapsed;
    [SerializeField] private Quaternion detachStartBodyRotation;

    // Offsets
    public float currentRightOffset = 0f;
    public float currentUpOffset = 0f;
    float currentRightVelocity = 0f;
    float currentUpVelocity = 0f;


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
    public int downwardDodgeInput;
    public bool rageInput;
    public bool adrenalineInput;
    public InputToggle overboostToggle;
    public Vector3 lastExclusiveDirectionalInput = Vector3.forward;

    // playerState Flags
    [field: Header("Other")]
    public bool isCooled = true;
    public bool playerHasControl = true;
    public float OverboostVelocityDeathLimit { get; protected set; }

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

    private List<Vector3> sampleDirections;

    [Header("Internals")]
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

    // Internal playerState Vectors
    public Vector3 velocity, desiredVelocity, desiredDodgeVelocity;


    void OnValidate()
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;

        if (!cameraControllerRef)
            cameraControllerRef = FindObjectOfType<CameraControllerNew>();

        if (!gunControllerRef)
            gunControllerRef = GetComponentInChildren<SpaceShooterPlasmaGunController>();

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

        sampleDirections = GenerateSphereDirections(42);

        OverboostVelocityDeathLimit = maxOverboostSpeed * 0.65f;
    }

    void Update()
    {
        AttachHitbox();
        AdjustMaxOverboostSpeed();
        HandleInput();
        if (playerState == PlayerState.Normal || playerState == PlayerState.OverboostInitiating || playerState == PlayerState.OverboostActive)
            HandleOverboostInitiation();
        HandleRageCharge();
        HandleAdrenalineCharge();
        HandleDodgeRecharge();
        HandleOverboostDuration();
        HandleDodgeTime();
    }

    void FixedUpdate()
    {
        UpdateState();

        if (playerState == PlayerState.BoostActive)
        {
            CalculateDesiredVelocity();
            AdjustBoostVelocity();
            AdjustDodgeVelocity();
        }
        else if(BoostTransitioning)
        {
            HandleBoostModeTransition();
        }
        else
        {
            CalculateDesiredVelocity();
            AdjustVelocity();
            AdjustDodgeVelocity();

            if (!OnGround && body.useGravity)
                ApplyGravity();

            if (jumpInput && playerState != PlayerState.OverboostActive)
            {
                if (OnGround)
                    Jump();
                else
                    AdjustAirVelocity();
            }

            if (playerState == PlayerState.OverboostActive)
                AdjustAirVelocity();

            if (healInput)
                healthController.Heal(100, true);

            body.velocity = velocity;
        }

        ClearState();
        DecayMaxSpeedToDefault();
    }

    void AttachHitbox()
    {
        hitboxRef.position = transform.position;
    }

    void AdjustMaxOverboostSpeed()
    {
        float dodgeBonusSpeed = (maxDodgeCharges - dodgeCharges) * perDodgeMaxSpeedIncrease;
        float dodgeBonusOverboostSpeed = (maxDodgeCharges - dodgeCharges) * perDodgeMaxOverboostSpeedIncrease;

        dodgeBonusSpeed = Mathf.Min(dodgeBonusSpeed, dodgeMaxSpeedCap - defaultMaxSpeed);
        dodgeBonusOverboostSpeed = Mathf.Min(dodgeBonusOverboostSpeed, dodgeMaxOverboostSpeedCap - defaultMaxOverboostSpeed - defaultMaxExtraOverboostSpeed);

        maxSpeed = defaultMaxSpeed + dodgeBonusSpeed;

        if (playerState == PlayerState.OverboostActive)
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

        maxOverboostSpeed = defaultMaxOverboostSpeed + currentExtraOverboostSpeed + dodgeBonusOverboostSpeed;
    }

    void HandleTakenHits()
    {
        ResetAdrenaline();
    }

    void ResetAdrenaline()
    {
        if (!adrenalineActive)
        {
            adrenalineChargeTimer = 0f;
            if (adrenalineCharged)
                adrenalineCharged = false;
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

    void ApplyGravity()
    {
        velocity += Physics.gravity * gravityModifier * Time.fixedDeltaTime;

        if (velocity.y < maxFallSpeed && playerState == PlayerState.Normal)
            velocity.y = Mathf.MoveTowards(velocity.y, maxFallSpeed, maxFallSpeedDecayRate * Time.fixedDeltaTime);

        if (velocity.y > maxRiseSpeed && playerState == PlayerState.Normal)
            velocity.y = Mathf.MoveTowards(velocity.y, maxRiseSpeed, maxRiseSpeedDecayRate * Time.fixedDeltaTime);
    }

    void ClearState()
    {
        groundContactCount = 0;
        contactNormal = Vector3.zero;
        horizontalDodgeInput = 0;
        verticalDodgeInput = 0;
        downwardDodgeInput = 0;
    }

    void UpdateState()
    {
        if (playerState == PlayerState.BoostActive)
        {
            velocity = railControllerRef.SplineRight * currentRightVelocity
                     + railControllerRef.SplineUp * currentUpVelocity;
        }
        else
        {
            velocity = body.velocity;
        }

        if (OnGround)
        {
            if (groundContactCount > 1)
                contactNormal.Normalize();
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

        if (playerState == PlayerState.OverboostActive)
        {
            float horizontalInput = (rightInput + leftInput) * overboostTurnMultiplier;
            float verticalInput = 1;
            float forwardBackwardInput = (forwardInput + backwardInput) * overboostTurnMultiplier;

            if (lastExclusiveDirectionalInput.x != 0)
                horizontalInput = lastExclusiveDirectionalInput.x;
            else if (lastExclusiveDirectionalInput.z != 0)
                forwardBackwardInput = lastExclusiveDirectionalInput.z;

            moveDirection = new Vector3(horizontalInput, 0, forwardBackwardInput).normalized + new Vector3(0, verticalInput, 0);

            worldDirection =
                cameraControllerRef.mainCameraRef.transform.right * moveDirection.x +
                cameraControllerRef.mainCameraRef.transform.forward * moveDirection.z;

            worldDirection.Normalize();

            horizontalSpeed = maxOverboostSpeed;
            verticalSpeed = maxOverboostVerticalSpeed;

            verticalComponent = worldDirection.y;

            // Explicit vertical nudge on top of camera-derived direction
            float explicitVertical = 0f;
            if (jumpInput) explicitVertical = 1f;

            desiredVelocity = new Vector3(
                worldDirection.x * horizontalSpeed,
                verticalComponent * verticalSpeed + explicitVertical * verticalSpeed,
                worldDirection.z * horizontalSpeed);
        }
        else if (playerState == PlayerState.OverboostInitiating)
        {
            horizontalSpeed = maxOverboostInitiationSpeed;
        }
        else if (playerState == PlayerState.BoostActive)
        {
            verticalSpeed = maxOverboostSpeed;

            float horizontalInput = (rightInput + leftInput);
            float verticalInput = (forwardInput + backwardInput);

            moveDirection = new Vector3(horizontalInput, verticalInput, 0);

            horizontalSpeed = maxOverboostSpeed;
            verticalComponent = moveDirection.y;

            bool lookingForward = cameraControllerRef.LookingForward;
            bool lookingSideways = cameraControllerRef.LookingSideways;

            float adjustedHorizontal = lookingForward ? -moveDirection.x : moveDirection.x;
            adjustedHorizontal = lookingSideways ? 0 : adjustedHorizontal;

            desiredVelocity =
                adjustedHorizontal * horizontalSpeed * railControllerRef.SplineRight +
                verticalComponent * verticalSpeed * railControllerRef.SplineUp;

            desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, railControllerRef.SplineForward);
        }
        else if (playerState == PlayerState.Normal)
        {
            float horizontalInput = rightInput + leftInput;
            float verticalInput = jumpInput ? 1 : 0;
            float forwardBackwardInput = forwardInput + backwardInput;

            moveDirection = new Vector3(horizontalInput, verticalInput, forwardBackwardInput);

            Vector3 camRight = cameraControllerRef.mainCameraRef.transform.right;
            Vector3 camForward = cameraControllerRef.mainCameraRef.transform.forward;
            camForward.y = 0;
            camForward.Normalize();

            worldDirection = camRight * moveDirection.x + camForward * moveDirection.z;
            worldDirection.Normalize();

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
        Vector3 rightAxis = railControllerRef.SplineRight;
        Vector3 upAxis = railControllerRef.SplineUp;

        float targetRight = Vector3.Dot(desiredVelocity, rightAxis);
        float targetUp = Vector3.Dot(desiredVelocity, upAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;

        float deltaRight = targetRight - currentRightVelocity;
        float deltaUp = targetUp - currentUpVelocity;

        currentRightVelocity += Mathf.Sign(deltaRight) * Mathf.Min(Mathf.Abs(deltaRight), acceleration * Time.fixedDeltaTime);
        currentUpVelocity += Mathf.Sign(deltaUp) * Mathf.Min(Mathf.Abs(deltaUp), acceleration * Time.fixedDeltaTime);

        currentRightOffset += currentRightVelocity * Time.fixedDeltaTime;
        currentUpOffset += currentUpVelocity * Time.fixedDeltaTime;

        currentRightOffset = Mathf.Clamp(currentRightOffset, -railControllerRef.maxSidewaysOffset, railControllerRef.maxSidewaysOffset);
        currentUpOffset = Mathf.Clamp(currentUpOffset, -railControllerRef.maxUpwardOffset, railControllerRef.maxUpwardOffset);

        if (Mathf.Abs(currentRightOffset) >= railControllerRef.maxSidewaysOffset && Mathf.Sign(currentRightVelocity) == Mathf.Sign(currentRightOffset))
            currentRightVelocity = 0f;

        if (Mathf.Abs(currentUpOffset) >= railControllerRef.maxUpwardOffset && Mathf.Sign(currentUpVelocity) == Mathf.Sign(currentUpOffset))
            currentUpVelocity = 0f;
    }

    void AdjustAirVelocity()
    {
        if (isDodging) return;

        Vector3 yAxis = Vector3.up;
        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;
        float deltaY = targetY - currentY;

        // Don't apply if we're already moving faster than target in the same direction
        // This lets dodge vertical momentum carry naturally without fighting it
        if (Mathf.Abs(currentY) > Mathf.Abs(targetY) && Mathf.Sign(currentY) == Mathf.Sign(targetY))
            return;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), jetpackAcceleration * Time.fixedDeltaTime);
        velocity += yAxis * change;
    }

    void AdjustDodgeVelocity()
    {
        // Horizontal dodge — normal and overboost
        if (horizontalDodgeInput > 0 && !isDodging && dodgeCharges > 0 && playerState != PlayerState.BoostActive && !BoostTransitioning)
        {
            if (rightInput + leftInput == 0 && lastExclusiveDirectionalInput.z != 0 && playerState == PlayerState.OverboostActive)
                return;
            if (forwardInput + backwardInput == 0 && lastExclusiveDirectionalInput.x != 0 && playerState == PlayerState.OverboostActive)
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

            if (playerState == PlayerState.OverboostActive)
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

            if (playerState == PlayerState.OverboostActive)
            {
                desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
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
                velocity = desiredDodgeVelocity * dodgeMaxSpeed * (playerState == PlayerState.OverboostActive ? 1.5f : 1f);

            horizontalDodgeInput = 0;
            verticalDodgeInput = 0;
        }

        // Vertical dodge — normal only
        if (verticalDodgeInput > 0 && !isDodging && horizontalDodgeInput == 0 && dodgeCharges > 0 && playerState == PlayerState.Normal)
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

            horizontalDodgeInput = 0;
            verticalDodgeInput = 0;
        }

        // Downward dodge — normal only
        if (downwardDodgeInput > 0 && !isDodging && horizontalDodgeInput == 0 && dodgeCharges > 0 && playerState == PlayerState.Normal)
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

            desiredDodgeVelocity = new Vector3(rightInput + leftInput, -1f, forwardInput + backwardInput).normalized;

            Vector3 camRight = cameraControllerRef.mainCameraRef.transform.right;
            Vector3 camForward = cameraControllerRef.mainCameraRef.transform.forward;
            camRight.y = 0f;
            camForward.y = 0f;

            if (desiredDodgeVelocity == Vector3.zero)
                desiredDodgeVelocity = -transform.up;

            desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            desiredDodgeVelocity = new Vector3(desiredDodgeVelocity.x, -1f, desiredDodgeVelocity.z);
            desiredDodgeVelocity.Normalize();

            maxSpeed = Mathf.Min(maxSpeed + perDodgeMaxSpeedIncrease, dodgeMaxSpeedCap);

            if (desiredDodgeVelocity != Vector3.zero)
                velocity = desiredDodgeVelocity * dodgeMaxSpeed * 0.75f;

            horizontalDodgeInput = 0;
            downwardDodgeInput = 0;
        }

        // Boost mode omnidirectional dodge
        if (!isDodging && dodgeCharges > 0 && playerState == PlayerState.BoostActive && horizontalDodgeInput > 0f)
        {
            float horizontal = rightInput + leftInput;
            float vertical = forwardInput + backwardInput;

            if (cameraControllerRef.LookingSideways)
            {
                horizontal = 0f;
                if (vertical == 0f) return;
            }

            if (horizontal == 0f && vertical == 0f) return;

            bool invertHorizontal = cameraControllerRef.LookingForward;
            horizontal = invertHorizontal ? -horizontal : horizontal;

            Vector3 inputDirection = new Vector3(horizontal, vertical, 0f).normalized;
            Vector3 dodgeDirection = railControllerRef.SplineRight * inputDirection.x
                                   + railControllerRef.SplineUp * inputDirection.y;

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

            currentRightVelocity = Vector3.Dot(dodgeDirection, railControllerRef.SplineRight) * dodgeMaxSpeed;
            currentUpVelocity = Vector3.Dot(dodgeDirection, railControllerRef.SplineUp) * dodgeMaxSpeed;

            horizontalDodgeInput = 0;
            verticalDodgeInput = 0;
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

    void HandleOverboostInitiation()
    {
        // Enter initiation when toggle turns on
        if (overboostToggle.GetCurrentToggleState() && playerState == PlayerState.Normal)
            playerState = PlayerState.OverboostInitiating;

        // Run initiation sequence
        if (playerState == PlayerState.OverboostInitiating)
        {
            if (body.useGravity)
                OnOverboostInitiation?.Invoke();
            body.useGravity = false;
            maxOverboostSpeed = maxOverboostInitiationSpeed;
            overboostChargeTimer += Time.deltaTime;

            float t = overboostChargeTimer / (overboostActivationDelay + 2f);
            float factor = 1f - (t * t);
            body.velocity = new Vector3(body.velocity.x, body.velocity.y * factor, body.velocity.z); // brief stop only happens on y, other axis conserve momentum

            if (overboostChargeTimer >= overboostActivationDelay)
            {
                maxOverboostSpeed = defaultMaxOverboostSpeed;
                if (lastExclusiveDirectionalInput.x != 0)
                    body.velocity = cameraControllerRef.mainCameraRef.transform.right * lastExclusiveDirectionalInput.x * dodgeMaxSpeed;
                else if (lastExclusiveDirectionalInput.z != 0)
                    body.velocity = cameraControllerRef.mainCameraRef.transform.forward * lastExclusiveDirectionalInput.z * dodgeMaxSpeed;

                playerState = PlayerState.OverboostActive;
                body.useGravity = true;
                overboostChargeTimer = 0f;
                OnOverboostActivation?.Invoke();
            }
        }

        // Cancel when toggle turns off
        if (!overboostToggle.GetCurrentToggleState() &&
            (playerState == PlayerState.OverboostInitiating || playerState == PlayerState.OverboostActive))
        {
            if (playerState == PlayerState.OverboostActive) OnOverboostStop?.Invoke();
            if (!body.useGravity) OnOverboostInitiationCancel?.Invoke();
            body.useGravity = true;
            overboostChargeTimer = 0f;
            overboostOverheatMode = false;
            playerState = PlayerState.Normal;
        }
    }

    void HandleOverboostDuration()
    {
        if (playerState == PlayerState.OverboostActive)
        {
            if (!overboostOverheatMode)
            {
                overboostDurationCurrent += Time.deltaTime;

                if (overboostDurationCurrent >= overboostDuration && overboostOverheatDurationCurrent < overboostOverheatDuration)
                {
                    if (!overboostOverheatMode)
                        OnOverboostOverheat?.Invoke();
                    overboostOverheatMode = true;
                }
                else if (overboostDurationCurrent >= overboostOverheatDuration && overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostToggle.ForceToggle(false);
                    OnOverboostStop?.Invoke();
                    OnOverheatCoolingInitiated?.Invoke();
                    overboostOverheatMode = false;
                    playerState = PlayerState.Normal;
                }
            }
            else
            {
                overboostOverheatDurationCurrent += Time.deltaTime;

                if (overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostToggle.ForceToggle(false);
                    OnOverboostStop?.Invoke();
                    OnOverheatCoolingInitiated?.Invoke();
                    overboostOverheatMode = false;
                    playerState = PlayerState.Normal;
                }
            }
        }
        else if (playerState == PlayerState.Normal)
        {
            overboostDurationCurrent = Mathf.MoveTowards(overboostDurationCurrent, 0f, overboostDurationRestoreRate * Time.deltaTime);
            overboostOverheatDurationCurrent = Mathf.MoveTowards(overboostOverheatDurationCurrent, 0f, overboostOverheatDurationRestoreRate * Time.deltaTime);
        }

        if (overboostOverheatDurationCurrent > 0)
            isCooled = false;

        if (overboostOverheatDurationCurrent <= 0f && !isCooled)
        {
            isCooled = true;
            OnOverheatCoolingConcluded?.Invoke();
        }
    }

    void HandleRageCharge()
    {
        if (!rageCharged)
        {
            rageChargeTimer += Time.deltaTime;
            if (rageChargeTimer >= rageRechargeTimer)
                rageCharged = true;
        }

        if (rageCharged && rageInput)
            rageActive = true;

        if (rageActive)
        {
            rageDurationCurrent -= Time.deltaTime;
            if (rageDurationCurrent <= 0f)
            {
                rageActive = false;
                rageCharged = false;
                rageDurationCurrent = rageDuration;
                rageChargeTimer = 0f;
            }
        }
    }

    void HandleAdrenalineCharge()
    {
        if (!adrenalineCharged)
        {
            adrenalineChargeTimer += Time.deltaTime;
            if (adrenalineChargeTimer >= adrenalineRechargeTimer)
                adrenalineCharged = true;
        }

        if (adrenalineCharged && adrenalineInput)
            adrenalineActive = true;

        if (adrenalineActive)
        {
            adrenalineDurationCurrent -= Time.deltaTime;
            if (adrenalineDurationCurrent <= 0f)
            {
                adrenalineActive = false;
                adrenalineCharged = false;
                adrenalineDurationCurrent = adrenalineDuration;
                adrenalineChargeTimer = 0f;
            }
        }
    }

    void HandleDodgeTime()
    {
        if (!isDodging) return;

        dodgeTime += Time.deltaTime;
        if (dodgeTime >= dodgeTimeLimit)
        {
            isDodging = false;
            dodgeTime = 0f;
        }
    }

    public (float orbitMin, float orbitMax) CalculateDynamicOrbit(
        float baseMin, float baseMax, float sweetSpot, float safeBuffer = 1f)
    {
        float maxRadius = baseMax;

        foreach (var dir in sampleDirections)
        {
            if (Physics.SphereCast(transform.position, 1f, dir, out RaycastHit hit, baseMax, obstacleMask))
            {
                float safeDist = hit.distance - safeBuffer;
                maxRadius = Mathf.Min(maxRadius, safeDist);
            }
        }

        maxRadius = Mathf.Max(maxRadius, sweetSpot);
        float minRadius = Mathf.Max(0f, maxRadius - sweetSpot);
        maxRadius = Mathf.Max(minRadius + sweetSpot, maxRadius);

        return (minRadius, maxRadius);
    }

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
            dirs.Add(new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r).normalized);
        }
        return dirs;
    }

    void Jump()
    {
        if (OnGround)
            velocity += Vector3.up * jumpForce;
    }

    // Called externally
    public void InitiateBoostModeAttach(SplineContainer newSplineTarget, float duration, float xOffset, float yOffset, float initialSplineT, float initialRailSpeed)
    {
        if (BoostTransitioning || playerState == PlayerState.BoostActive) return;

        if (playerState == PlayerState.OverboostActive || playerState == PlayerState.OverboostInitiating)
            CancelOverboostForBoostAttach();

        attachStartSpeed = body.velocity.magnitude;

        body.useGravity = false;
        cameraControllerRef.canRotate = false;
        gunControllerRef.canFire = false;

        // RailController Reinitialization
        railControllerRef.splineContainer?.gameObject.SetActive(false);
        railControllerRef.splineContainer = newSplineTarget;

        var table = newSplineTarget.GetComponent<SplineArcLengthTable>();
        if (table != null && !table.IsReady)
            table.Bake(newSplineTarget.GetComponent<SplineContainer>().Spline);

        railControllerRef.Initialize();
        railControllerRef.splineT = initialSplineT;
        railControllerRef.defaultSplineSpeed = initialRailSpeed;
        railControllerRef.currentSplineSpeed.value = initialRailSpeed;
        railControllerRef.MaxSpeed = initialRailSpeed;
        railControllerRef.boostModeSpeedFade = new RailSpeedController(railControllerRef.currentSplineSpeed, initialRailSpeed);
        railControllerRef.InitializeSplineValues();

        attachDuration = duration;
        attachDurationCurrent = 0f;
        //transitionBlend = 0f;
        //transitionBlendVelocity = 0f;
        attachStartPosition = body.position;
        attachStartRotation = body.rotation;
        attachDesiredOffsetX = xOffset;
        attachDesiredOffsetY = yOffset;

        railControllerRef.splineContainer.gameObject.SetActive(true);
        cameraControllerRef.BeginBoostModeAttachTransition(duration);

        playerState = PlayerState.BoostAttaching;
    }

    public void InitiateBoostModeDetach(float transitionDuration)
    {
        if (playerState != PlayerState.BoostActive) return;

        Vector3 exitVelocity = railControllerRef.SplineForward * railControllerRef.MaxSpeed
                             + railControllerRef.SplineRight * currentRightVelocity
                             + railControllerRef.SplineUp * currentUpVelocity;

        railControllerRef.enabled = false;

        body.isKinematic = false;
        body.useGravity = true;
        body.velocity = exitVelocity;

        currentRightOffset = 0f;
        currentUpOffset = 0f;
        currentRightVelocity = 0f;
        currentUpVelocity = 0f;

        detachStartBodyRotation = body.rotation;
        detachRotationDuration = transitionDuration;
        detachRotationElapsed = 0f;

        gunControllerRef.canFire = true;
        cameraControllerRef.BeginBoostModeDetachTransition(transitionDuration, exitVelocity);

        playerState = PlayerState.BoostDetaching;
    }

    // Called only internally in FixedUpdate
    // In HandleBoostModeTransition — physics steering instead of MovePosition
    private void HandleBoostModeTransition()
    {
        if (playerState == PlayerState.BoostAttaching)
            HandleBoostAttach();
        else if (playerState == PlayerState.BoostDetaching)
            HandleBoostDetach();
    }

    private void HandleBoostAttach()
    {
        attachDurationCurrent += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(attachDurationCurrent / attachDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        railControllerRef.EvaluateSpline();

        Vector3 targetPos = railControllerRef.SplinePosition
            + railControllerRef.SplineRight * attachDesiredOffsetX
            + railControllerRef.SplineUp * attachDesiredOffsetY;

        float remainingTime = Mathf.Max(attachDuration - attachDurationCurrent, Time.fixedDeltaTime);
        Vector3 requiredVelocity = (targetPos - body.position) / remainingTime;
        body.velocity = Vector3.Lerp(body.velocity, requiredVelocity, smoothT);

        body.MoveRotation(Quaternion.Slerp(attachStartRotation, railControllerRef.SplineRotation, smoothT));

        if (t >= 1f)
        {
            body.velocity = Vector3.zero;
            body.isKinematic = true;
            body.MovePosition(targetPos);

            currentRightOffset = attachDesiredOffsetX;
            currentUpOffset = attachDesiredOffsetY;
            currentRightVelocity = 0f;
            currentUpVelocity = 0f;

            gunControllerRef.canFire = true;
            railControllerRef.enabled = true;
            playerState = PlayerState.BoostActive;

            railControllerRef.UpdateRailSpeed(Time.fixedDeltaTime);
            railControllerRef.SnapshotSplineForInterpolation();
            railControllerRef.TickSpline(Time.fixedDeltaTime);
            railControllerRef.EvaluateSpline();
            railControllerRef.CommitSplineToInterpolation();
        }
    }

    private void HandleBoostDetach()
    {
        detachRotationElapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(detachRotationElapsed / detachRotationDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        Quaternion targetRot = Quaternion.Euler(0f, cameraControllerRef.yaw, 0f);
        body.MoveRotation(Quaternion.Slerp(detachStartBodyRotation, targetRot, smoothT));

        if (t >= 1f)
            playerState = PlayerState.Normal;
    }

    private void CancelOverboostForBoostAttach()
    {
        if (playerState == PlayerState.OverboostActive)
        {
            OnOverboostStop?.Invoke();
            overboostOverheatMode = false;
        }
        else if (playerState == PlayerState.OverboostInitiating)
        {
            OnOverboostInitiationCancel?.Invoke();
        }

        // Shared cleanup regardless of which overboost state we were in
        body.useGravity = false; // transition will manage gravity
        overboostChargeTimer = 0f;
        overboostToggle.ForceToggle(false);
        overboostOverheatMode = false;
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
        if (Input.GetKeyDown(inputConfig.HorizontalDodge)) horizontalDodgeInput = 1;
        if (Input.GetKeyDown(inputConfig.VerticalDodge)) verticalDodgeInput = 1;
        if (Input.GetKeyDown(inputConfig.DownwardDodge)) downwardDodgeInput = 1;
        healInput = Input.GetKey(inputConfig.Heal);
        shootInput = Input.GetKey(inputConfig.Shoot) ? 1 : 0;
        rageInput = Input.GetKey(inputConfig.RageMode);
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode);

        if (playerState == PlayerState.Normal || playerState == PlayerState.OverboostInitiating || playerState == PlayerState.OverboostActive)
        {
            if (isCooled || playerState == PlayerState.OverboostActive)
            {
                overboostToggle.UpdateToggle();
                // Note: overboostMode bool is synced via playerState setter,
                // but toggle playerState drives the initiation check in HandleOverboostInitiation
            }
        }

        if (playerState != PlayerState.OverboostActive)
        {
            if (Input.GetKey(inputConfig.MoveLeft)) lastExclusiveDirectionalInput = new Vector3(-1, 0, 0);
            if (Input.GetKey(inputConfig.MoveRight)) lastExclusiveDirectionalInput = new Vector3(1, 0, 0);
            if (Input.GetKey(inputConfig.MoveUp)) lastExclusiveDirectionalInput = new Vector3(0, 0, 1);
            if (Input.GetKey(inputConfig.MoveDown)) lastExclusiveDirectionalInput = new Vector3(0, 0, -1);
        }
    }

    public bool AnyMovementInput()
    {
        return playerState == PlayerState.OverboostActive ? true : forwardInput != 0 || backwardInput != 0 || leftInput != 0 || rightInput != 0;
    }

    public bool AnySidewaysMovementInput() => leftInput != 0 || rightInput != 0;
    public bool AnyForwardMovementInput() => forwardInput != 0 || backwardInput != 0;
    public bool BoostTransitioning => playerState == PlayerState.BoostAttaching
                                || playerState == PlayerState.BoostDetaching;

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
            if (Input.GetButtonDown(axisName))
                currentToggleState = !currentToggleState;
        }
        else
        {
            if (Input.GetKeyDown(keyName))
                currentToggleState = !currentToggleState;
        }
    }

    public bool GetCurrentToggleState() => currentToggleState;

    public void ForceToggle(bool value) => currentToggleState = value;
}