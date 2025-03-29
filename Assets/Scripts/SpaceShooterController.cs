using System;
using UnityEngine;
using System.Collections;

public class SpaceShooterController : MonoBehaviour
{
    public CustomInputs inputConfig;
    
    public int forwardInput;
    public int backwardInput;
    public int leftInput;
    public int rightInput;
    public bool jumpInput;

    public InputToggle overboostToggle;
    public bool overboostMode;

    public int dodgeInput;
    public int parryInput;
    public int shootInput;
    public int rageInput;
    public int adrenalineInput;
    

    [SerializeField, Range(0f, 1000f)] float maxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxOverboostSpeed = 20f, maxOverboostVerticalSpeed;
    [SerializeField, Range(0f, 1000f)] float maxVerticalSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)] float jumpForce = 2f;
    [SerializeField, Range(0f, 2000f)] float jetpackAcceleration = 10f;
    [SerializeField, Range(0f, 2000f)] float dodgeAcceleration = 10f;
    [SerializeField, Range(0f, 1000f)] float dodgeMaxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)] float perDodgeMaxSpeedIncrease = 6.5f;
    [SerializeField] float dodgeMaxSpeedCap;
    [SerializeField] private int maxDodgeCharges = 5;
    [SerializeField] private int dodgeCharges;
    [SerializeField] private float dodgeRechargeTime = 1.5f;
    [SerializeField] private float dodgeRechargeTimer;
    [SerializeField, Range(0, 90)] float maxGroundAngle = 25f;
    [SerializeField, Range(0f, 100f)] float maxSpeedDecayRate = 2f;
    [SerializeField] float defaultMaxSpeed;
    float dodgeTime;
    Rigidbody body;
    Vector3 velocity, desiredVelocity, desiredDodgeVelocity;
    Vector3 contactNormal;
    int groundContactCount;
    bool OnGround => groundContactCount > 0;
    bool isDodging;
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
        dodgeMaxSpeedCap = defaultMaxSpeed + (maxDodgeCharges - 1) * perDodgeMaxSpeedIncrease;
        OnValidate();
        dodgeCharges = maxDodgeCharges; // Initialize full charges
        dodgeRechargeTimer = 0f;
    }

    void Update()
    {
        HandleInput();
        CalculateDesiredVelocity();

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

    void FixedUpdate()
    {
        UpdateState();

        AdjustVelocity();
        AdjustDodgeVelocity();

        if (!OnGround) 
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
        else if (overboostMode == true)
        {
            AdjustAirVelocity();
        }

        body.velocity = velocity;
        ClearState();

        if (!AnyMovementInput() && !isDodging)
        {
            maxSpeed = Mathf.MoveTowards(maxSpeed, defaultMaxSpeed, maxSpeedDecayRate * Time.deltaTime);
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
        Vector3 moveDirection = overboostMode ? 
                                new Vector3(rightInput + leftInput, 1, 1) :
                                new Vector3(rightInput + leftInput, jumpInput ? 1 : 0, forwardInput + backwardInput);
        Vector3 worldDirection = overboostMode ? 
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

    // Modify AdjustDodgeVelocity()
    void AdjustDodgeVelocity()
    {
        if (dodgeInput > 0 && !isDodging && dodgeCharges > 0)
        {
            isDodging = true;
            dodgeTime = 0f;
            dodgeCharges--;  // Consume one dodge charge
            dodgeRechargeTimer = 0f; // Reset recharge timer since a dodge was used

            // Dodge direction calculation (unchanged)
            desiredDodgeVelocity = new Vector3(rightInput + leftInput, 0, forwardInput + backwardInput).normalized;
            if (overboostMode)
            {
                desiredDodgeVelocity = new Vector3(rightInput + leftInput, 0, 1).normalized;
            }
            Vector3 camRight = Camera.main.transform.right;
            Vector3 camForward = Camera.main.transform.forward;
            camRight.y = 0;
            camForward.y = 0;
            desiredDodgeVelocity = camRight * desiredDodgeVelocity.x + camForward * desiredDodgeVelocity.z;
            desiredDodgeVelocity.Normalize();

            // Update dodge speed
            if (maxSpeed < dodgeMaxSpeedCap)
                maxSpeed += perDodgeMaxSpeedIncrease;
            else
                maxSpeed = dodgeMaxSpeedCap;

            velocity = desiredDodgeVelocity * maxSpeed;
        }

        // Dodge duration logic (unchanged)
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
        rageInput = Input.GetKey(inputConfig.RageMode) ? 1 : 0;
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode) ? 1 : 0;

        overboostToggle.UpdateToggle();
        overboostMode = overboostToggle.GetCurrentToggleState();
    }

    bool AnyMovementInput()
    {
        return overboostMode ? true : forwardInput != 0 || backwardInput != 0 || leftInput != 0 || rightInput != 0;
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
}


