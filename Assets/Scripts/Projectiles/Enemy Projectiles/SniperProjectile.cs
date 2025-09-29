using UnityEngine;

public class SniperProjectile : Projectile
{
    [Header("Damage Settings")]
    public int damage = 1;
    public LayerMask hitLayers = 0; // Default: all layers

    protected override void HandleHit(RaycastHit hit)
    {
        // Only hit objects on the allowed layers
        if (((1 << hit.collider.gameObject.layer) & hitLayers) != 0)
        {
            // Apply damage if the object has EntityHealthController
            EntityHealthController health = hit.collider.GetComponent<EntityHealthController>();
            if (health != null)
            {
                health.TakeDamage(damage, true);
            }

            Impact();
        }
    }
}
