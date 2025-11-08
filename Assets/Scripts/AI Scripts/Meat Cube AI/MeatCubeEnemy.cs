using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MeatCubeEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 50f;
    public float maxAcceleration = 200f;
    public float blockDotProductLimit = 0.7f;
    public LayerMask obstacleLayers;
    public SpaceShooterController player;


    private Rigidbody rb;
    private Vector3 currentDirection;
    private Vector3 desiredVelocity;
    private Vector3 velocity;

    private float stuckTimer = 0f;
    private const float stuckThreshold = 0.5f; // seconds before forcing axis change

    private List<Collider> xPosBlockers = new();
    private List<Collider> xNegBlockers = new();
    private List<Collider> yPosBlockers = new();
    private List<Collider> yNegBlockers = new();
    private List<Collider> zPosBlockers = new();
    private List<Collider> zNegBlockers = new();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;

        PickNextAxis();
    }

    void FixedUpdate()
    {
        if (player == null) return;

        CalculateDesiredVelocity();
        AdjustVelocity();

        // --- Detect being stuck against obstacle ---
        if (IsAxisBlocked(currentDirection))
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= stuckThreshold)
            {
                PickNextAxis();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        rb.velocity = velocity;
    }

    void CalculateDesiredVelocity()
    {
        Vector3 toPlayer = player.transform.position - transform.position;

        float distance = toPlayer.magnitude;
        float alignmentThreshold = Mathf.Min(1f, distance * 0.3f);

        if (currentDirection.x != 0 && Mathf.Abs(toPlayer.x) < alignmentThreshold)
            PickNextAxis();
        else if (currentDirection.y != 0 && Mathf.Abs(toPlayer.y) < alignmentThreshold)
            PickNextAxis();
        else if (currentDirection.z != 0 && Mathf.Abs(toPlayer.z) < alignmentThreshold)
            PickNextAxis();

        desiredVelocity = currentDirection * moveSpeed;
    }

    void PickNextAxis()
    {
        Vector3 toPlayer = player.transform.position - transform.position;
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        Vector3 bestAxis = currentDirection;
        float bestScore = float.MinValue;

        foreach (var axis in axes)
        {
            if (axis == -currentDirection) continue; // no reversing immediately
            if (IsAxisBlocked(axis)) continue;       // skip blocked directions

            // Score based on alignment with the player and distance along axis
            float projectedDistance = Vector3.Project(toPlayer, axis).magnitude;
            float score = Vector3.Dot(toPlayer.normalized, axis) * projectedDistance;

            if (score > bestScore)
            {
                bestScore = score;
                bestAxis = axis;
            }
        }

        // Fallback if all axes blocked or score is low
        if (bestScore <= 0f)
        {
            foreach (var axis in axes)
            {
                if (!IsAxisBlocked(axis))
                {
                    bestAxis = axis;
                    break;
                }
            }
        }

        currentDirection = bestAxis;
        velocity = Vector3.zero;
    }

    void AdjustVelocity()
    {
        Vector3 delta = desiredVelocity - velocity;
        Vector3 accel = Vector3.ClampMagnitude(delta, maxAcceleration * Time.fixedDeltaTime);
        velocity += accel;
    }

    bool IsAxisBlocked(Vector3 axis)
    {
        if (axis == Vector3.right) return xPosBlockers.Count > 0;
        if (axis == Vector3.left) return xNegBlockers.Count > 0;
        if (axis == Vector3.up) return yPosBlockers.Count > 0;
        if (axis == Vector3.down) return yNegBlockers.Count > 0;
        if (axis == Vector3.forward) return zPosBlockers.Count > 0;
        if (axis == Vector3.back) return zNegBlockers.Count > 0;

        Debug.Log("Axis locked");
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if ((obstacleLayers & (1 << other.gameObject.layer)) == 0) return;

        Vector3 dir = (other.transform.position - transform.position).normalized;

        if (Vector3.Dot(dir, Vector3.right) > blockDotProductLimit) xPosBlockers.Add(other);
        if (Vector3.Dot(dir, Vector3.left) > blockDotProductLimit) xNegBlockers.Add(other);
        if (Vector3.Dot(dir, Vector3.up) > blockDotProductLimit) yPosBlockers.Add(other);
        if (Vector3.Dot(dir, Vector3.down) > blockDotProductLimit) yNegBlockers.Add(other);
        if (Vector3.Dot(dir, Vector3.forward) > blockDotProductLimit) zPosBlockers.Add(other);
        if (Vector3.Dot(dir, Vector3.back) > blockDotProductLimit) zNegBlockers.Add(other);

        // Reevaluate immediately if the new blocker affects current direction
        if (IsAxisBlocked(currentDirection))
            PickNextAxis();
    }

    void OnTriggerExit(Collider other)
    {
        xPosBlockers.Remove(other);
        xNegBlockers.Remove(other);
        yPosBlockers.Remove(other);
        yNegBlockers.Remove(other);
        zPosBlockers.Remove(other);
        zNegBlockers.Remove(other);
    }
}
