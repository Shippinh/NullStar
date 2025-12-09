using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class BasicMineEnemy : MonoBehaviour
{
    [Header("States")]
    [SerializeField] protected bool isTriggered = false;
    [SerializeField] protected bool hasExploded = false;
    [SerializeField] protected float triggerTimer = 0f; // counts fuse time after triggered

    [Header("Target")]
    public SpaceShooterController player;

    [Header("Explosion Settings")]
    public float triggerRange = 300f;     // Player must be inside this to activate
    public float fuseDelay = 2f;          // Time before explosion after triggered
    public float explosionRadius = 250f;  // Damage radius

    public bool dieOnExploding = false;
    public bool explodeOnDying = false;
    public bool oneTimeFuse = false;      // if true - once fuse is active it cannot be stopped
    public bool canBeFused = true;        // if true - allows natural fusing

    public LayerMask damageMask;          // Who gets hit

    public EntityHealthController healthControllerRef;

    virtual protected void Start()
    {
        Initialize();
    }

    protected void Initialize()
    {
        healthControllerRef = GetComponent<EntityHealthController>();
        if (healthControllerRef != null)
            healthControllerRef.Died += DeathEvents;
    }

    virtual protected void FixedUpdate()
    {
        // passive rearm in case the mine doesn't die on explosion
        if (hasExploded)
            Rearm();

        FuseAndDetonate();
    }

    protected void DeathEvents()
    {
        // Blows up the mine when the current mine dies
        if (explodeOnDying)
        {
            Debug.Log("Exploded on Death");
            Explode();
        }
    }

    protected void Rearm()
    {
        hasExploded = false;
        isTriggered = false;
        triggerTimer = 0f;
    }

    protected void FuseAndDetonate()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // Check if player is within trigger range
        if (canBeFused)
            Trigger(distToPlayer);

        // Count fuse timer
        if (isTriggered)
        {
            // If the player breaks the range when the mine is triggered and if it's not a single time fuse (aka can be rearmed) - rearm the mine
            if (distToPlayer > triggerRange && !oneTimeFuse)
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

    protected void Trigger(float distToPlayerPtr)
    {
        if (!isTriggered && distToPlayerPtr <= triggerRange)
        {
            isTriggered = true;
            triggerTimer = 0f; // start fuse
        }
    }

    protected void TriggerUnconditionally()
    {
        isTriggered = true;
        triggerTimer = 0f; // start fuse
    }

    protected void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Debug.Log("Mine Enemy Exploded");

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask);

        foreach (Collider hit in hits)
        {
            Debug.Log(hit);
            var healthController = hit.GetComponent<EntityHealthController>();
            if (healthController != null)
            {
                healthController.InstantlyDie();
            }
        }

        if (dieOnExploding)
        {
            var healthController = GetComponent<EntityHealthController>();
            if (healthController != null)
            {
                healthController.ForciblyDie();
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
