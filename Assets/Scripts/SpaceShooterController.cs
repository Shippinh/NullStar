using System;
using UnityEngine;
using System.Collections.Generic;
//omni.OS
public class SpaceShooterController : MonoBehaviour
{
    public CustomInputs inputConfig;
    public EntityHealthController healthController;
    
    public int forwardInput;
    public int backwardInput;
    public int leftInput;
    public int rightInput;
    public bool jumpInput;

    public InputToggle overboostToggle;
    public bool overboostMode;
    public bool overboostOverheatMode;
    public bool overboostInitiated = false;
    public float overboostActivationDelay = 2f;
    [SerializeField] float overboostChargeTimer;
    [SerializeField, Range(0f, 1000f)] float maxOverboostInitiationSpeed = 5f;
    public float maxExtraOverboostSpeed;
    public float defaultMaxExtraOverboostSpeed;
    [SerializeField, Range(0f, 1000f)] float currentExtraOverboostSpeed;
    [SerializeField, Range(0f, 500f)] float extraOverboostSpeedRate = 5f; // Speed per second


    public bool overboostForward = true; // Overboost direction - either forward or backward. By default - forward

    public float overboostDuration = 7.5f;
    public float overboostDurationCurrent;

    public float overboostOverheatDuration = 10f;
    public float overboostOverheatDurationCurrent;

    //public float overboostDrainRate = 1f;
    public float overboostDurationRestoreRate = 1f;
    public float overboostOverheatDurationRestoreRate = 1f;
    public bool isCooled = true;

    public int horizontalDodgeInput;
    public int verticalDodgeInput;
    public bool healInput;
    public int shootInput;

    public bool rageInput;
    public bool rageCharged = false;
    public bool rageActive = false;
    public float rageDuration = 5f;
    public float rageDurationCurrent;
    [SerializeField] float rageChargeTimer;
    public float rageRechargeTimer = 40f;

    public bool adrenalineInput;
    public bool adrenalineCharged = false;
    public bool adrenalineActive = false;
    public float adrenalineDuration = 3f;
    public float adrenalineDurationCurrent;
    [SerializeField] float adrenalineChargeTimer;
    public float adrenalineRechargeTimer = 20f;
    

    [SerializeField, Range(0f, 1000f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxOverboostSpeed = 20f, maxOverboostVerticalSpeed;
    [SerializeField, Range(0f, 1000f)] float maxVerticalSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)] float jumpForce = 2f;
    [SerializeField, Range(0f, 2000f)] float jetpackAcceleration = 10f;

    [SerializeField, Range(0f, 1000)] float dodgeMaxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float perDodgeMaxSpeedIncrease = 6.5f, perDodgeMaxOverboostSpeedIncrease = 8f;
    [SerializeField] float dodgeMaxSpeedCap, dodgeMaxOverboostSpeedCap;
    [SerializeField] public int maxDodgeCharges = 5;
    [SerializeField] public int dodgeCharges;
    [SerializeField] float dodgeRechargeTime = 1.5f;
    [SerializeField] private float dodgeRechargeTimer;
    [SerializeField] private float dodgeRechargeDelay = 1f;
    [SerializeField] private float dodgeRechargeDelayTimer;
    [SerializeField] private bool dodgeRecharging;

    [SerializeField, Range(0, 90)] float maxGroundAngle = 25f;
    [SerializeField, Range(0f, 100f)] float maxSpeedDecayRate = 2f;
    [SerializeField] float defaultMaxSpeed, defaultMaxOverboostSpeed;
    [SerializeField, Range(0, 1f)] float overboostTurnMultiplier;

    float dodgeTime;

    Rigidbody body;
    Vector3 velocity, desiredVelocity, desiredDodgeVelocity;
    Vector3 contactNormal;
    int groundContactCount;
    bool OnGround => groundContactCount > 0;
    public bool isDodging = false;
    float minGroundDotProduct;

    public event Action OnOverboostInitiation;
    public event Action OnOverboostInitiationCancel;
    public event Action OnOverboostActivation;
    public event Action OnOverboostStop;
    public event Action OnOverboostOverheat;
    public event Action OnOverheatCoolingInitiated;
    public event Action OnOverheatCoolingConcluded;
    public event Action OnDodgeUsed; // Invoked on each dodge
    public event Action OnDodgeActualRechargeStart; // Invoked after the dodge cd delay
    public event Action OnDodgeChargeGain; // Invoked when a dodge charge is gained

    void OnValidate() 
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake() 
    {
        Application.targetFrameRate = 120; // stupid hack to prevent high fps issues with inputs

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
        dodgeRechargeTimer = 0f;
        overboostChargeTimer = 0f;
        rageChargeTimer = 0f;
        adrenalineChargeTimer = 0f;
        rageDurationCurrent = rageDuration;
        adrenalineDurationCurrent = adrenalineDuration;
        overboostDurationCurrent = 0f;
        overboostOverheatDurationCurrent = 0f;
    }

    void Update()
    {
        AdjustMaxOverboostSpeed();
        HandleInput();
        CalculateDesiredVelocity();
        HandleOverboostInitiation();
        HandleRageCharge();
        HandleAdrenalineCharge();
        HandleDodgeRecharge();
        HandleOverboostDuration();
    }

    void FixedUpdate()
    {
        UpdateState();

        AdjustVelocity();
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

    // Replace this portion in your class
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
            maxSpeed = Mathf.MoveTowards(maxSpeed, defaultMaxSpeed, maxSpeedDecayRate * Time.deltaTime);
            maxOverboostSpeed = Mathf.MoveTowards(maxOverboostSpeed, defaultMaxOverboostSpeed, maxSpeedDecayRate * Time.deltaTime);
        }
    }

    // Custom gravity
    void ApplyGravity()
    {
        float gravityMultiplier = 4f; // Increase for stronger gravity
        velocity += Physics.gravity * gravityMultiplier * Time.deltaTime;
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
        // Step 1: Create input direction
        Vector3 moveDirection = overboostMode && overboostInitiated ?
                                new Vector3((rightInput + leftInput) * overboostTurnMultiplier, 1, overboostForward ? 1 : -1) : // Overboost: use fixed vertical input, decide if overboost goes forward or backwards based on the input
                                new Vector3(rightInput + leftInput, jumpInput ? 1 : 0, forwardInput + backwardInput);

        Vector3 worldDirection;

        if (overboostMode && overboostInitiated)
        {
            // Step 2A: Overboost - full 3D movement direction based on camera
            worldDirection = 
                Camera.main.transform.right * moveDirection.x +
                Camera.main.transform.forward * moveDirection.z;

            worldDirection.Normalize(); // Let camera pitch determine final direction (includes vertical movement)
        }
        else
        {
            // Step 2B: Normal mode - movement restricted to horizontal plane, with separate Y input
            Vector3 camRight = Camera.main.transform.right;
            Vector3 camForward = Camera.main.transform.forward;
            camForward.y = 0; // Flatten forward to horizontal
            camForward.Normalize();

            worldDirection = camRight * moveDirection.x + camForward * moveDirection.z;
            worldDirection.Normalize(); // Horizontal movement only
        }

        // Step 3: Apply speed based on mode
        float horizontalSpeed = overboostMode ? maxOverboostSpeed : maxSpeed;
        if(overboostMode && !overboostInitiated)
        {
            horizontalSpeed = maxOverboostInitiationSpeed;
        }
        float verticalSpeed = overboostMode ? maxOverboostVerticalSpeed : maxVerticalSpeed;

        desiredVelocity = new Vector3(
            worldDirection.x * horizontalSpeed,
            (overboostMode && overboostInitiated ? worldDirection.y : moveDirection.y) * verticalSpeed,
            worldDirection.z * horizontalSpeed
        );
    }


    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        // Get the component of the velocity along the respective axes
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        // Determine if the character is on the ground or in the air for acceleration
        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        
        // Apply the full acceleration directly to the components
        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        // Cap the speed change based on max acceleration, but without smoothing
        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), acceleration * Time.deltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), acceleration * Time.deltaTime);
    }

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;

        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;

        float deltaY = targetY - currentY;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), jetpackAcceleration * Time.deltaTime);
        velocity += yAxis * change;
    }


    void AdjustDodgeVelocity()
    {
        // Horizontal dodge
        if (horizontalDodgeInput > 0 && !isDodging && verticalDodgeInput == 0 && dodgeCharges > 0)
        {
            isDodging = true;
            dodgeTime = 0f;
            dodgeCharges--;

            // Start the delay before recharge begins
            dodgeRechargeDelayTimer = 0f;
            dodgeRecharging = false;

            OnDodgeUsed?.Invoke();

            desiredDodgeVelocity = overboostMode
                ? new Vector3((rightInput + leftInput) * overboostTurnMultiplier, 0, 0).normalized
                : new Vector3(rightInput + leftInput, 0, forwardInput + backwardInput).normalized;

            Vector3 camRight = Camera.main.transform.right;
            Vector3 camForward = Camera.main.transform.forward;
            camRight.y = 0;
            camForward.y = 0;

            if (overboostMode)
                desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            else
            {
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
                    desiredDodgeVelocity = new Vector3(rightInput + leftInput, 0, overboostForward ? 1 : -1).normalized;
                    desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
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
        */ 
        if (verticalDodgeInput > 0 && !isDodging && horizontalDodgeInput == 0 && dodgeCharges > 0 && overboostMode == false)
        {
            isDodging = true;
            dodgeTime = 0f;
            dodgeCharges--;

            // Start the delay before recharge begins
            dodgeRechargeDelayTimer = 0f;
            dodgeRecharging = false;

            OnDodgeUsed?.Invoke();

            desiredDodgeVelocity = new Vector3(rightInput + leftInput, 1f, forwardInput + backwardInput).normalized;

            Vector3 camRight = Camera.main.transform.right;
            Vector3 camForward = Camera.main.transform.forward;
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

        if (isDodging)
        {
            dodgeTime += Time.fixedDeltaTime;
            if (dodgeTime >= 0.2f)
            {
                isDodging = false;
                dodgeTime = 0f;
            }
        }
    }


    void HandleDodgeRecharge()
    {
        if (dodgeCharges >= maxDodgeCharges)
            return;

        if (!dodgeRecharging)
        {
            dodgeRechargeDelayTimer += Time.deltaTime;
            if (dodgeRechargeDelayTimer >= dodgeRechargeDelay)
            {
                dodgeRecharging = true;
                dodgeRechargeTimer = 0f;
                OnDodgeActualRechargeStart?.Invoke();
            }
        }
        else
        {
            dodgeRechargeTimer += Time.deltaTime;
            if (dodgeRechargeTimer >= dodgeRechargeTime)
            {
                dodgeCharges++;
                dodgeRechargeTimer = 0f;
                OnDodgeChargeGain?.Invoke();

                // If still not full, stay in recharge state
                if (dodgeCharges < maxDodgeCharges)
                {
                    dodgeRecharging = true;
                    dodgeRechargeDelayTimer = 0f;
                }
                else
                {
                    dodgeRecharging = false;
                }
            }
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
                body.velocity = overboostForward ? Camera.main.transform.forward * dodgeMaxSpeed : -Camera.main.transform.forward * dodgeMaxSpeed;
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

                healthController.TakeDamage(1, false);

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

    void Jump()
    {
        if (OnGround)
        {
            velocity += Vector3.up * jumpForce;
        }
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

        if(isCooled || overboostMode)
        {
            // Determine in which direction to overboost
            if (forwardInput == 1 && (backwardInput == 0 || backwardInput == -1))
                overboostForward = true;
            else if (forwardInput == 0 && backwardInput == -1)
                overboostForward = false;

            overboostToggle.UpdateToggle();
            overboostMode = overboostToggle.GetCurrentToggleState();
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


