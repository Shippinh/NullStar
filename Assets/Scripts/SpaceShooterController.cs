using System;
using UnityEngine;

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
    

    [SerializeField, Range(0f, 1000f)]
    float maxSpeed = 10f;
    [SerializeField, Range(0f, 1000f)]
    float maxVerticalSpeed = 10f;
    [SerializeField, Range(0f, 1000f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 100f)]
    float jumpForce = 2f;
    [SerializeField, Range(0f, 2000f)]
    float jetpackForce = 10f;
    [SerializeField, Range(0, 90)]
    float maxGroundAngle = 25f;
    Rigidbody body;
    Vector3 velocity, desiredVelocity;
    Vector3 contactNormal;
    int groundContactCount;
    bool OnGround => groundContactCount > 0;
    int jumpPhase;
    float minGroundDotProduct;

    void OnValidate() 
    {
        minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
    }

    void Awake() 
    {
        body = GetComponent<Rigidbody>();
        overboostToggle = new InputToggle(inputConfig.Overboost);
        OnValidate();
    }

    public void Update()
    {
        forwardInput = Input.GetKey(inputConfig.MoveUp) ? 1 : 0;
        backwardInput = Input.GetKey(inputConfig.MoveDown) ? -1 : 0;
        leftInput = Input.GetKey(inputConfig.MoveLeft) ? -1 : 0;
        rightInput = Input.GetKey(inputConfig.MoveRight) ? 1 : 0;
        jumpInput = Input.GetKey(inputConfig.Ascend);

        dodgeInput = Input.GetKey(inputConfig.Dodge) ? 1 : 0;
        parryInput = Input.GetKey(inputConfig.Parry) ? 1 : 0;
        shootInput = Input.GetKey(inputConfig.Shoot) ? 1 : 0;
        rageInput = Input.GetKey(inputConfig.RageMode) ? 1 : 0;
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode) ? 1 : 0;

        overboostToggle.UpdateToggle();
        overboostMode = overboostToggle.GetCurrentToggleState();

        Vector3 moveDirection = new Vector3(rightInput + leftInput, jumpInput ? 1 : 0, forwardInput + backwardInput);
        Vector3 worldDirection = overboostMode ? 
                                Camera.main.transform.right * moveDirection.x + Camera.main.transform.forward * moveDirection.z :
                                Camera.main.transform.right * moveDirection.x + Vector3.up * moveDirection.y + Camera.main.transform.forward * moveDirection.z;
        worldDirection.Normalize();
        desiredVelocity = new Vector3(worldDirection.x * maxSpeed, worldDirection.y * maxVerticalSpeed, worldDirection.z * maxSpeed);
    }

    void FixedUpdate()
    {
        UpdateState();
        AdjustVelocity();
        
        if (!OnGround) 
        {
            ApplyGravity();
        }

        if (jumpInput)
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

        body.velocity = velocity;
        ClearState();
    }

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
            jumpPhase = 0;
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

    void Jump()
    {
        if (OnGround)
        {
            jumpPhase = 1;
            velocity += Vector3.up * jumpForce;
        }
    }

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up; // Ensure we are using global up

        float currentY = Vector3.Dot(velocity, yAxis);
        float maxSpeedChange = jetpackForce * Time.deltaTime;

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


