using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class JumpingMineEnemy : BasicMineEnemy
{

    [Header("Jump Settings")]
    public float jumpHeight = 50f;        // Max height of the arc
    public float jumpDuration = 1.5f;     // Time it takes to reach the player

    [Header("Line of Sight")]
    public LayerMask losMask;              // Layers to check for obstacles blocking jump

    private Rigidbody rb;

    override protected void Awake()
    {
        Initialize();

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.velocity = Vector3.zero;

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = triggerRange;

        rb.drag = explosionRadius / triggerRange * fuseDelay + 0.75f;
    }

    override protected void OnEnable()
    {
        if (reinitializeOnEnable)
        {
            rb.velocity = Vector3.zero;

            Rearm();
        }
    }

    override protected void FixedUpdate()
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
                    TriggerUnconditionally();
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, triggerRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
