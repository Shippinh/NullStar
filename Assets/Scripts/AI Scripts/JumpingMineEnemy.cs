using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class JumpingMineEnemy : MonoBehaviour
{
    [Header("Target")]
    public SpaceShooterController player;

    [Header("Jump Settings")]
    public float jumpHeight = 50f;        // Max height of the arc
    public float jumpDuration = 1.5f;     // Time it takes to reach the player

    [Header("Explosion Settings")]
    public float triggerRange = 300f;
    public float fuseDelay = 2f;
    public float explosionRadius = 250f;
    public LayerMask damageMask;
    public bool dieOnExploding = false;
    public bool explodeOnDying = false;
    [SerializeField] private EntityHealthController healthControllerRef;

    [Header("Line of Sight")]
    public LayerMask losMask;               // Layers to check for obstacles blocking jump

    private Rigidbody rb;
    [SerializeField] private bool isTriggered = false;
    [SerializeField] private bool hasExploded = false;
    [SerializeField] private float triggerTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.velocity = Vector3.zero;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = triggerRange;

        rb.drag = explosionRadius / triggerRange * fuseDelay + 0.75f;

        healthControllerRef = GetComponent<EntityHealthController>();
        if (healthControllerRef != null)
            healthControllerRef.Died += DeathEvents;
    }

    void FixedUpdate()
    {
        if (hasExploded) return;

        if (!isTriggered)
        {
            if (player && Vector3.Distance(transform.position, player.transform.position) <= triggerRange)
            {
                // Check clear line of sight
                Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
                float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

                if (!Physics.Raycast(transform.position, dirToPlayer, distanceToPlayer, losMask))
                {
                    isTriggered = true;
                    triggerTimer = 0f;
                    JumpTowardPlayer();
                }
            }
        }
        else
        {
            triggerTimer += Time.fixedDeltaTime;
            if (triggerTimer >= fuseDelay)
                Explode();
        }
    }

    void JumpTowardPlayer()
    {
        if (!player) return;

        Vector3 displacement = player.transform.position - transform.position;

        rb.AddForce(displacement, ForceMode.Impulse);
    }

    void DeathEvents()
    {
        if (explodeOnDying)
            Explode();
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius, damageMask);
        foreach (Collider hit in hits)
        {
            var healthController = hit.GetComponent<EntityHealthController>();
            if (healthController != null)
                healthController.CurrentHP = 0;
        }

        if (dieOnExploding && healthControllerRef != null)
            healthControllerRef.CurrentHP = 0;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
