using System.Collections.Generic;
using UnityEngine;

public enum EnemyAIState
{
    Normal,             // When not on rail
    BoostAttaching,     // When attach to rail initiated
    BoostActive,        // When on rail
    BoostDetaching      // When detach from rail initiated
}

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
    [SerializeField] public EnemyAIState enemyState = EnemyAIState.Normal;

    public virtual void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;

        avoidanceTrigger = GetComponent<SphereCollider>();
        avoidanceTrigger.isTrigger = true;
        avoidanceTrigger.radius = detectionRadius;

        velocity = Vector3.zero;
    }

    public Rigidbody GetRidigbody()
    {
        return rb;
    }

    public void InitializeOnRail()
    {

    }

    public void InitializeNormally()
    {

    }

    /*public virtual void InitiateBoostModeAttach()
    {
        if (BoostTransitioning || enemyState == EnemyAIState.BoostActive) return;
    }

    public virtual void InitiateBoostModeDetach(float duration)
    {
        if (enemyState != EnemyAIState.BoostActive) return;
    }

    public virtual void HandleBoostModeTransition()
    {
        if (enemyState == EnemyAIState.BoostAttaching)
            HandleBoostAttach();
        else if (enemyState == EnemyAIState.BoostDetaching)
            HandleBoostDetach();
    }

    public virtual void HandleBoostAttach()
    {

    }

    public virtual void HandleBoostDetach()
    {

    }

    public bool BoostTransitioning => enemyState == EnemyAIState.BoostAttaching
                                || enemyState == EnemyAIState.BoostDetaching;
    */
}
