using UnityEngine;

public class SniperProjectile : Projectile
{
    [Header("Damage Settings")]
    public int damage = 1;
    public LayerMask hitLayers;

    protected override void HandleHit(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            EntityHealthController health = other.GetComponent<EntityHealthController>();
            if (health != null)
            {
                health.TakeDamage(damage, true);
            }
        }

        Impact();
    }
}
