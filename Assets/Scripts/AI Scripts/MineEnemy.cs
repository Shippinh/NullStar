using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class MineEnemy : MonoBehaviour
{
    [Header("Target & Movement")]
    public SpaceShooterController player;
    public float maxSpeed = 200f;
    public float maxAcceleration = 80f;
    public float maxAirAcceleration = 80f;
    public bool randomizeMaxAirAcceleration = true;
    public bool canMove = true;
    public float motionlessDrag = 0.5f;

    [Header("Avoidance")]
    public float avoidanceForce = 1000f;
    public float detectionRadius = 40f;
    public LayerMask obstacleMask;

    [Header("Explosion Settings")]
    public float triggerRange = 300f;     // Player must be inside this to activate
    public float fuseDelay = 2f;          // Time before explosion after triggered
    public float explosionRadius = 250f;  // Damage radius
    public LayerMask damageMask;          // Who gets hit
    public bool dieOnExploding = false;
    public bool explodeOnDying = false;
    public EntityHealthController healthControllerRef;

    private Rigidbody rb;
    private List<Collider> nearbyObstacles = new List<Collider>();
    private Vector3 velocity;
    private Vector3 desiredVelocity;
    private Vector3 contactNormal = Vector3.up;

    [SerializeField] private bool isTriggered = false;
    [SerializeField] private bool hasExploded = false;
    [SerializeField] private float triggerTimer = 0f; // counts fuse time after triggered

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = detectionRadius;

        healthControllerRef = GetComponent<EntityHealthController>();
        healthControllerRef.Died += DeathEvents;

        velocity = Vector3.zero;

        if (randomizeMaxAirAcceleration)
        {
            maxAirAcceleration = Random.Range(maxAirAcceleration, maxAirAcceleration + 50f);
        }
    }

    void FixedUpdate()
    {
        // passive rearm in case the mine doesn't die on explosion
        if(hasExploded)
            Rearm();

        FuseAndDetonate();

        if (!canMove)
        {
            rb.drag = motionlessDrag;
            return;
        }
        else
            rb.drag = 0;

        CalculateDesiredVelocity();
        AdjustVelocity();
        AdjustAirVelocity();
        rb.velocity = velocity;
    }

    void CalculateDesiredVelocity()
    {
        Vector3 rawToPlayer = player.transform.position - transform.position;

        // Add chaotic lateral + vertical offsets
        Vector3 sideOffset = Vector3.Cross(Vector3.up, rawToPlayer).normalized;
        Vector3 upOffset = Vector3.up;

        float sideStrength = Mathf.PerlinNoise(transform.position.x * 0.5f, Time.time * 0.5f) - 0.5f;
        float upStrength = Mathf.PerlinNoise(transform.position.z * 0.5f, Time.time * 0.7f + 42f) - 0.5f;

        Vector3 chaoticOffset = sideOffset * sideStrength * 4f + upOffset * upStrength * 100f;

        // Always move at maxSpeed toward player
        Vector3 toPlayerDir = (rawToPlayer + chaoticOffset).normalized * maxSpeed;

        // Calculate avoidance
        Vector3 avoidanceVector = CalculateObstacleAvoidance();
        avoidanceVector = ProjectOnContactPlane(avoidanceVector);

        // Combine direction + avoidance
        Vector3 combined = toPlayerDir + avoidanceVector;

        if (combined.magnitude > maxSpeed * 1.5f)
            combined = combined.normalized * maxSpeed * 1.5f;

        desiredVelocity = combined;
    }

    void DeathEvents()
    {
        // Blows up the mine when the current mine dies
        if (explodeOnDying)
        {
            Debug.Log("Exploded on Death");
            Explode();
        }
    }

    void Rearm()
    {
        hasExploded = false;
        isTriggered = false;
        triggerTimer = 0f;
    }

    void FuseAndDetonate()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // Check if player is within trigger range
        if (!isTriggered && distToPlayer <= triggerRange)
        {
            isTriggered = true;
            triggerTimer = 0f; // start fuse
        }

        // Count fuse timer
        if (isTriggered)
        {
            // If the player breaks the range when the mine is triggered - rearm the mine
            if(distToPlayer > triggerRange)
            {
                Debug.Log("The player escaped trigger range, mine - rearmed");
                Rearm();
                return;
            }

            triggerTimer += Time.fixedDeltaTime;
            if (triggerTimer >= fuseDelay)
            {
                Explode();
            }
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Debug.Log("Mine Enemy Exploded");

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask);

        foreach (Collider hit in hits)
        {
            var healthController = hit.GetComponent<EntityHealthController>(); // Replace with your health script
            if (healthController != null)
            {
                healthController.CurrentHP = 0; // Kill instantly
            }
        }

        if (dieOnExploding)
        {
            var healthController = GetComponent<EntityHealthController>();
            if (healthController != null)
            {
                healthController.CurrentHP = 0;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
