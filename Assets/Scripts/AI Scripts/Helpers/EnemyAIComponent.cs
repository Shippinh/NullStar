using System.Collections.Generic;
using UnityEngine;

// Allows us to reference enemy AI components generally
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class EnemyAIComponent : MonoBehaviour
{
    [Header("Avoidance")]
    public float avoidanceForce = 5f;
    public float detectionRadius = 5f;
    public LayerMask obstacleMask;

    [Header("Component Internals")]
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected SphereCollider avoidanceTrigger;
    [SerializeField] protected List<Collider> nearbyObstacles = new List<Collider>();
    [SerializeField] protected Vector3 velocity;
    [SerializeField] protected Vector3 desiredVelocity;
    [SerializeField] protected Vector3 contactNormal = Vector3.up;
    [SerializeField] protected float tiltAngle = 0f;

    public virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        avoidanceTrigger = GetComponent<SphereCollider>();
        avoidanceTrigger.isTrigger = true;
        avoidanceTrigger.radius = detectionRadius;

        velocity = Vector3.zero;
    }

    public void SetRBKinematic(bool kinematic)
    {
        rb.isKinematic = kinematic;
    }

    public bool GetRBKinematic()
    {
        return rb.isKinematic;
    }
}
