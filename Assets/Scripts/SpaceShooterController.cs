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

    public int overboostInput;
    public int dodgeInput;
    public int parryInput;
    public int shootInput;
    public int rageInput;
    public int adrenalineInput;

    [SerializeField, Range(0f, 100f)]
    float maxSpeed = 10f;
    [SerializeField, Range(0f, 100f)]
    float maxAcceleration = 10f, maxAirAcceleration = 1f;
    [SerializeField, Range(0f, 10f)]
    float jumpForce = 2f;
    [SerializeField, Range(0f, 200f)]
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
        OnValidate();
    }

    public void Update()
    {
        forwardInput = Input.GetKey(inputConfig.MoveUp) ? 1 : 0;
        backwardInput = Input.GetKey(inputConfig.MoveDown) ? -1 : 0;
        leftInput = Input.GetKey(inputConfig.MoveLeft) ? -1 : 0;
        rightInput = Input.GetKey(inputConfig.MoveRight) ? 1 : 0;
        jumpInput = Input.GetKey(inputConfig.Ascend);

        overboostInput = Input.GetKey(inputConfig.Overboost) ? 1 : 0;
        dodgeInput = Input.GetKey(inputConfig.Dodge) ? 1 : 0;
        parryInput = Input.GetKey(inputConfig.Parry) ? 1 : 0;
        shootInput = Input.GetKey(inputConfig.Shoot) ? 1 : 0;
        rageInput = Input.GetKey(inputConfig.RageMode) ? 1 : 0;
        adrenalineInput = Input.GetKey(inputConfig.AdrenalineMode) ? 1 : 0;

        Vector3 moveDirection = new Vector3(rightInput + leftInput, 0, forwardInput + backwardInput);
        Vector3 worldDirection = Camera.main.transform.right * moveDirection.x + Camera.main.transform.forward * moveDirection.z;
        desiredVelocity = worldDirection.normalized * maxSpeed;

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
                ApplyJetpack();
            }
        }

        body.velocity = velocity;
        ClearState();
    }

    void ApplyGravity()
    {
        float gravityMultiplier = 2f; // Increase for stronger gravity
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

    void ApplyJetpack()
    {
        velocity += Vector3.up * jetpackForce * Time.deltaTime;
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
