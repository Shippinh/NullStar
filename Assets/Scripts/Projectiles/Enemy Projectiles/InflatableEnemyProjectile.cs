using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InflatableEnemyProjectile : SimpleEnemyProjectile
{
    public enum SequencedProjectileState
    {
        Idle,               // Pre warm up
        SequenceStart,      // On warm up
        SequenceProcess,    // After warm up
        SequenceComplete
    }

    [Header("References")]
    private PhysicMaterial defaultPhysicsRef;
    public PhysicMaterial bouncyPhysicsRef;
    public EntityHealthController healthControllerRef;
    public RadialForceField forceFieldRef;

    [Header("Custom Internals and Options")]
    public SequencedProjectileState state;

    public float timerUntillInflation = 0f;
    public float timeUntillInflation = 5f;

    public float timerToInflate = 0f;
    public float timeToInflate = 0.5f;
    public bool randomizeTimeToInflate = false;
    [Range(0, 1)] public float desiredTimeToInflateRandomizationMultiplier = 1f;

    public float desiredScaleMultiplier = 2f;
    public bool randomizeScale = false;
    [Range(0, 1)] public float desiredScaleMinimumRandomizationRange = 1f;


    public LerpFactorMethods.LerpFactor scaleLerpFactor = LerpFactorMethods.LerpFactor.EaseOutQuad;

    [SerializeField] private Vector3 initialScale;

    public override void Awake()
    {
        base.Awake();

        if (col != null)
        {
            defaultPhysicsRef = col.material;
            if (bouncyPhysicsRef) col.material = bouncyPhysicsRef;
        }

        healthControllerRef = GetComponent<EntityHealthController>();
        forceFieldRef = GetComponent<RadialForceField>();

        if(randomizeTimeToInflate)
        {
            timeToInflate -= Random.Range(timeToInflate, timeToInflate * desiredTimeToInflateRandomizationMultiplier);
        }

        initialScale = transform.localScale;

        if(randomizeScale)
        {
            desiredScaleMultiplier *= Random.Range(desiredScaleMinimumRandomizationRange, 1.2f);
        }
    }

    public void Update()
    {
        HandleState(Time.deltaTime);
    }

    public virtual void HandleState(float t)
    {
        // If it's not inflated
        if (state != SequencedProjectileState.SequenceComplete)
        {
            // Check what to do
            switch (state)
            {
                // If it's been warmed up - proceed with the sequence
                case SequencedProjectileState.SequenceStart:
                    {
                        timerUntillInflation += t;

                        // Proceed until it completes
                        if (timerUntillInflation >= timeUntillInflation)
                        {
                            state = SequencedProjectileState.SequenceProcess;
                            healthControllerRef.canBeDamaged = true;
                        }
                        break;
                    }

                case SequencedProjectileState.SequenceProcess:
                    {
                        timerToInflate += t;

                        float normalizedT = Mathf.Clamp01(timerToInflate / timeToInflate);
                        float lerpFactor = LerpFactorMethods.GetLerpFactor(scaleLerpFactor, normalizedT);
                        transform.localScale = Vector3.Lerp(initialScale, initialScale * desiredScaleMultiplier, lerpFactor);

                        if (timerToInflate >= timeToInflate)
                        {
                            state = SequencedProjectileState.SequenceComplete;
                            forceFieldRef.ApplyForceTick(true);
                        }
                        break;
                    }
            }
        }
        // If it's inflated
        else
        {
            
        }
    }

    // POOL → ACTIVE
    public override void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        base.HandleDepool(poolableTag, position, rotation);

        timerUntillInflation = 0f;
        timerToInflate = 0f;
        transform.localScale = initialScale;

        healthControllerRef.Revive(true);

        state = SequencedProjectileState.SequenceStart;
    }

    public override void HandleRepool()
    {
        base.HandleRepool();

        state = SequencedProjectileState.Idle;
    }

    public void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject.GetComponent<Collider>());
    }
}
