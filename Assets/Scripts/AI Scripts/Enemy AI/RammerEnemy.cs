using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class RammerEnemy : MonoBehaviour
{
    [Header("Target & Movement")]
    public SpaceShooterController player;
    public float maxSpeed = 200f;
    public float maxAcceleration = 80f;
    public float maxAirAcceleration = 80f;
    public bool randomizeMaxAirAcceleration = true;
    public bool canMove = true;

    [Header("Avoidance")]
    public float avoidanceForce = 1000f;  
    public float detectionRadius = 40f;
    public LayerMask obstacleMask;

    [Header("Other")]
    public bool reinitializeOnEnable = true;
    public bool reinitializeCanMove = false; // Should canMove be set to true when reinitialized ? 

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;

    private Vector3 contactNormal = Vector3.up;

    void Start()
    {
        if (!player)
            player = FindObjectOfType<SpaceShooterController>();

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        velocity = Vector3.zero;

        if(randomizeMaxAirAcceleration)
        {
            maxAirAcceleration = Random.Range(maxAirAcceleration, maxAirAcceleration + 50f);
        }
    }

    // Soft AI reinitialization
    private void OnEnable()
    {
        if (reinitializeOnEnable)
        {
            velocity = Vector3.zero;
            desiredVelocity = Vector3.zero;

            if (reinitializeCanMove)
                canMove = true;
        }
    }

    void FixedUpdate()
    {
        CalculateDesiredVelocity();

        AdjustVelocity();
        AdjustAirVelocity();

        rb.velocity = velocity;
    }

    void CalculateDesiredVelocity()
    {
        Vector3 rawToPlayer = player.transform.position - transform.position;
        float distanceToPlayer = rawToPlayer.magnitude;

        // Add chaotic lateral + vertical offsets
        Vector3 sideOffset = Vector3.Cross(Vector3.up, rawToPlayer).normalized;
        Vector3 upOffset = Vector3.up;

        float sideStrength = Mathf.PerlinNoise(transform.position.x * 0.5f, Time.time * 0.5f) - 0.5f;
        float upStrength = Mathf.PerlinNoise(transform.position.z * 0.5f, Time.time * 0.7f + 42f) - 0.5f;

        // Scale chaotic vertical offset based on distance
        float minMultiplier = 1f;   // when close
        float maxMultiplier = 1500f; // when far
        float scalerDistance = 30f; // distance considered "close"
        float farDistance = 250f;    // distance considered "far"

        float distanceScaler = Mathf.Clamp01((distanceToPlayer - scalerDistance) / (farDistance - scalerDistance));
        float finalUpMultiplier = Mathf.Lerp(minMultiplier, maxMultiplier, distanceScaler);

        Vector3 chaoticOffset = sideOffset * sideStrength * 4f + upOffset * upStrength * finalUpMultiplier;

        // Always move at maxSpeed toward player
        Vector3 toPlayerDir = (rawToPlayer + chaoticOffset).normalized * maxSpeed;

        // Calculate avoidance
        Vector3 avoidanceVector = CalculateObstacleAvoidance();
        avoidanceVector = ProjectOnContactPlane(avoidanceVector);

        // Combine direction + avoidance
        Vector3 combined = toPlayerDir + avoidanceVector;

        // Optional: clamp final speed to maxSpeed + some margin if needed
        if (combined.magnitude > maxSpeed * 1.5f)
            combined = combined.normalized * maxSpeed * 1.5f;

        desiredVelocity = combined;
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
                // Scale avoidance inversely by distance (closer obstacles push stronger)
                // Using a smooth falloff (distance / detectionRadius)
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

    void AdjustVelocity()
    {
        Vector3 xAxis = ProjectOnContactPlane(Vector3.right).normalized;
        Vector3 zAxis = ProjectOnContactPlane(Vector3.forward).normalized;

        float currentX = Vector3.Dot(velocity, xAxis);
        float currentZ = Vector3.Dot(velocity, zAxis);

        float deltaX = desiredVelocity.x - currentX;
        float deltaZ = desiredVelocity.z - currentZ;

        velocity += xAxis * Mathf.Sign(deltaX) * Mathf.Min(Mathf.Abs(deltaX), maxAcceleration * Time.fixedDeltaTime);
        velocity += zAxis * Mathf.Sign(deltaZ) * Mathf.Min(Mathf.Abs(deltaZ), maxAcceleration * Time.fixedDeltaTime);
    }

    void AdjustAirVelocity()
    {
        Vector3 yAxis = Vector3.up;

        float currentY = Vector3.Dot(velocity, yAxis);

        float deltaY = desiredVelocity.y - currentY;

        velocity += yAxis * Mathf.Sign(deltaY) * Mathf.Min(Mathf.Abs(deltaY), maxAirAcceleration * Time.fixedDeltaTime);
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

    // On actual physical collision
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log(collision.gameObject.name);

        EntityHealthController hpController = collision.gameObject.GetComponent<EntityHealthController>();
        if (hpController != null)
        {
            // kill the hit collider
            hpController.InstantlyDie();
            GetComponent<EnemyController>().entityHealthControllerRef.InstantlyDie(); // kill this enemy as well
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 simPos = transform.position;
            Vector3 simVel = desiredVelocity;
            float simStep = 0.2f;
            int simSteps = 40;

            for (int i = 0; i < simSteps; i++)
            {
                Vector3 nextPos = simPos + simVel * simStep;
                Gizmos.DrawLine(simPos, nextPos);
                simPos = nextPos;
            }
        }
    }
}
