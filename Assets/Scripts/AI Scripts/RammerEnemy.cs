using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class RammerEnemy : MonoBehaviour
{
    [Header("Target & Movement")]
    public SpaceShooterController player;
    public float maxSpeed = 10f;
    public float maxAcceleration = 30f;
    public float maxAirAcceleration = 10f;
    public float jetpackAcceleration = 15f;

    [Header("Avoidance")]
    public float avoidanceForce = 5f;  // magnitude of avoidance vector added to player dir
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private bool OnGround = true; // Simplified ground check, set properly in your project

    private Vector3 contactNormal = Vector3.up; // Adjust if needed

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;
    }
    void FixedUpdate()
    {
        Vector3 avoidanceVector = Vector3.zero;

        foreach (var col in nearbyObstacles)
        {
            if (!col) continue;
            Vector3 point = col.ClosestPoint(transform.position);
            Vector3 away = (transform.position - point);
            float dist = away.magnitude;

            if (dist > 0f)
            {
                avoidanceVector += away.normalized * (1f / dist);
            }
        }

        if (avoidanceVector != Vector3.zero)
        {
            avoidanceVector = avoidanceVector.normalized * avoidanceForce;
        }

        // Stronger deviation - more swarm chaos
        Vector3 rawToPlayer = player.transform.position - transform.position;

        // Add aggressive lateral and vertical variation
        Vector3 sideOffset = Vector3.Cross(Vector3.up, rawToPlayer).normalized;
        Vector3 upOffset = Vector3.up;

        float sideStrength = Mathf.PerlinNoise(transform.position.x * 0.5f, Time.time * 0.5f) - 0.5f;
        float upStrength = Mathf.PerlinNoise(transform.position.z * 0.5f, Time.time * 0.7f + 42f) - 0.5f;

        Vector3 chaoticOffset = sideOffset * sideStrength * 4f + upOffset * upStrength * 100f; // make this more/less extreme

        Vector3 toPlayer = (rawToPlayer + chaoticOffset).normalized;


        Vector3 combinedDir = (toPlayer + avoidanceVector).normalized;


        desiredVelocity = combinedDir * maxSpeed;

        AdjustVelocity();
        AdjustAirVelocity();

        rb.velocity = velocity;
    }


    Vector3 ProjectOnContactPlane(Vector3 vector)
    {
        return vector - contactNormal * Vector3.Dot(vector, contactNormal);
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

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;

        float currentY = Vector3.Dot(velocity, yAxis);
        float targetY = desiredVelocity.y;

        float deltaY = targetY - currentY;

        float change = Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), jetpackAcceleration * Time.fixedDeltaTime);
        velocity += yAxis * change;
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & obstacleMask) != 0 && !other.isTrigger)
        {
            if (!nearbyObstacles.Contains(other))
                nearbyObstacles.Add(other);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (nearbyObstacles.Contains(other))
            nearbyObstacles.Remove(other);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
