using System;
using UnityEngine;
using System.Collections;

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

    public float overboostDuration = 7.5f;
    public float overboostDurationCurrent;

    public float overboostOverheatDuration = 10f;
    public float overboostOverheatDurationCurrent;

    //public float overboostDrainRate = 1f;
    public float overboostDurationRestoreRate = 1f;
    public float overboostOverheatDurationRestoreRate = 1f;

    public int dodgeInput;
    public int parryInput;
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
    [SerializeField] private int maxDodgeCharges = 5;
    [SerializeField] private int dodgeCharges;
    [SerializeField] float dodgeRechargeTime = 1.5f;
    [SerializeField] private float dodgeRechargeTimer;
    [SerializeField, Range(0, 90)] float maxGroundAngle = 25f;
    [SerializeField, Range(0f, 100f)] float maxSpeedDecayRate = 2f;
    [SerializeField] float defaultMaxSpeed, defaultMaxOverboostSpeed;
    float dodgeTime;
    Rigidbody body;
    Vector3 velocity, desiredVelocity, desiredDodgeVelocity;
    Vector3 contactNormal;
    int groundContactCount;
    bool OnGround => groundContactCount > 0;
    public bool isDodging = false;
    float minGroundDotProduct;

    void OnValidate() 
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake() 
    {
        body = GetComponent<Rigidbody>();
        overboostToggle = new InputToggle(inputConfig.Overboost);
        defaultMaxSpeed = maxSpeed;
        defaultMaxOverboostSpeed = maxOverboostSpeed;
        dodgeMaxSpeedCap = defaultMaxSpeed + (maxDodgeCharges - 1) * perDodgeMaxSpeedIncrease;
        dodgeMaxOverboostSpeedCap = defaultMaxOverboostSpeed + (maxDodgeCharges - 1) * perDodgeMaxOverboostSpeedIncrease;
        OnValidate();
        dodgeCharges = maxDodgeCharges; // Initialize full charges
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

        body.velocity = velocity;
        ClearState();

        DecayMaxSpeedToDefault();
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
        Vector3 moveDirection = overboostMode && overboostInitiated ? 
                                new Vector3(rightInput + leftInput, 1, 1) :
                                new Vector3(rightInput + leftInput, jumpInput ? 1 : 0, forwardInput + backwardInput);
        Vector3 worldDirection = overboostMode && overboostInitiated ? 
                                Camera.main.transform.right * moveDirection.x + Camera.main.transform.forward * moveDirection.z :
                                Camera.main.transform.right * moveDirection.x + Vector3.up * moveDirection.y + Camera.main.transform.forward * moveDirection.z;
        worldDirection.Normalize();
        desiredVelocity = new Vector3(overboostMode ? worldDirection.x * maxOverboostSpeed : worldDirection.x * maxSpeed, overboostMode ? worldDirection.y * maxOverboostVerticalSpeed : worldDirection.y * maxVerticalSpeed, overboostMode ? worldDirection.z * maxOverboostSpeed : worldDirection.z * maxSpeed);
    }

    void AdjustVelocity() 
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;
        
        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
        float maxSpeedChange = acceleration * Time.deltaTime;

        float newX = Mathf.MoveTowards(currentX, desiredVelocity.x, maxSpeedChange);
        float newZ = Mathf.MoveTowards(currentZ, desiredVelocity.z, maxSpeedChange);

        velocity += xAxis * (newX - currentX) + zAxis * (newZ - currentZ);
    }

    void AdjustDodgeVelocity()
    {
        if (dodgeInput > 0 && !isDodging && dodgeCharges > 0)
        {
            isDodging = true;
            dodgeTime = 0f;
            dodgeCharges--;  // Consume one dodge charge
            dodgeRechargeTimer = 0f; // Reset recharge timer since a dodge was used

            desiredDodgeVelocity = overboostMode
                ? new Vector3(rightInput + leftInput, 0, 0).normalized
                : new Vector3(rightInput + leftInput, 0, forwardInput + backwardInput).normalized;

            Vector3 camRight = Camera.main.transform.right;
            Vector3 camForward = Camera.main.transform.forward;
            camRight.y = 0;
            camForward.y = 0;

            if(overboostMode)
                desiredDodgeVelocity = Camera.main.transform.right * desiredDodgeVelocity.x + Camera.main.transform.forward * desiredDodgeVelocity.z;
            else
            {
                if(desiredDodgeVelocity == Vector3.zero)
                    desiredDodgeVelocity = transform.forward;
                desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            }

            desiredDodgeVelocity.Normalize();

            if(maxSpeed < dodgeMaxSpeedCap)
                maxSpeed += perDodgeMaxSpeedIncrease;
            else
                maxSpeed = dodgeMaxSpeedCap;

            if(maxOverboostSpeed < dodgeMaxOverboostSpeedCap)
                maxOverboostSpeed += perDodgeMaxOverboostSpeedIncrease;
            else
                maxOverboostSpeed = dodgeMaxOverboostSpeedCap;

            if (desiredDodgeVelocity != Vector3.zero)
            {
                if(overboostMode)
                {
                    desiredDodgeVelocity = new Vector3(rightInput + leftInput, 0, 1).normalized;
                    desiredDodgeVelocity = Camera.main.transform.right * desiredDodgeVelocity.x + Camera.main.transform.forward * desiredDodgeVelocity.z;
                    velocity = desiredDodgeVelocity * dodgeMaxSpeed;
                }
                else
                    velocity = desiredDodgeVelocity * dodgeMaxSpeed;
            }
        }
        
        if (isDodging)
        {
            // Dodge time logic to end dodge after a short duration
            dodgeTime += Time.fixedDeltaTime;
            if (dodgeTime >= 0.2f) // 0.2s dodge duration
            {
                isDodging = false;
                dodgeTime = 0f;
            }
        }
    }

    void HandleOverboostInitiation()
    {
        if (overboostMode == true && overboostInitiated == false)
        {
            body.useGravity = false;
            maxOverboostSpeed = maxOverboostInitiationSpeed;
            overboostChargeTimer += Time.deltaTime;

            float t = overboostChargeTimer / (overboostActivationDelay + 2f); // Normalized time (0 to 1)
            float factor = 1f - (t * t);

            body.velocity = new Vector3(body.velocity.x, body.velocity.y * factor, body.velocity.z);

            if(overboostChargeTimer >= overboostActivationDelay)
            {
                maxOverboostSpeed = defaultMaxOverboostSpeed;
                body.velocity = Camera.main.transform.forward * dodgeMaxSpeed;
                overboostInitiated = true;
                body.useGravity = true;
                overboostChargeTimer = 0f;
            }
        }

        if(overboostToggle.GetCurrentToggleState() == false)
        {
            overboostInitiated = false;
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
                    overboostOverheatMode = true;
                }
                // If we overboost when we cannot overheat
                else if(overboostDurationCurrent >= overboostOverheatDuration && overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostMode = false;
                    overboostToggle.ForceToggle(false);
                    overboostInitiated = false;
                    overboostOverheatMode = false;
                }
            }
            // When overheating
            else
            {
                overboostOverheatDurationCurrent += Time.deltaTime;

                healthController.TakeDamage(1);

                // If we overheat before death - initiate cooling and disable overboost
                if(overboostOverheatDurationCurrent >= overboostOverheatDuration)
                {
                    overboostMode = false;
                    overboostToggle.ForceToggle(false);
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

    void HandleDodgeRecharge()
    {
        // Regenerate dodge charges over time
        if (dodgeCharges < maxDodgeCharges)
        {
            dodgeRechargeTimer += Time.deltaTime;
            if (dodgeRechargeTimer >= dodgeRechargeTime)
            {
                dodgeCharges++;
                dodgeRechargeTimer = 0f; // Reset timer after regenerating a charge
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

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up; // Ensure we are using global up

        float currentY = Vector3.Dot(velocity, yAxis);
        float maxSpeedChange = jetpackAcceleration * Time.deltaTime;

        float newY = Mathf.MoveTowards(currentY, desiredVelocity.y, maxSpeedChange);

        velocity += yAxis * (newY - currentY);
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
        dodgeInput = Input.GetKeyDown(inputConfig.Dodge) ? 1 : 0;
        parryInput = Input.GetKey(inputConfig.Parry) ? 1 : 0;
        shootInput = Input.GetKey(inputConfig.Shoot) ? 1 : 0;
        rageInput = Input.GetKey(inputConfig.RageMode);
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode);

        overboostToggle.UpdateToggle();
        overboostMode = overboostToggle.GetCurrentToggleState();
    }

    bool AnyMovementInput()
    {
        return overboostMode && overboostInitiated ? true : forwardInput != 0 || backwardInput != 0 || leftInput != 0 || rightInput != 0;
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


