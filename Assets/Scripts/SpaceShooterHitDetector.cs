using UnityEngine;

public class SpaceShooterHitDetector : MonoBehaviour
{

    public enum HitDetection
    {
        All,
        TriggerOnly,
        ColliderOnly
    }

    public HitDetection hitDetection;
    public LayerMask hitLayers;
    public ContactDamageTable damageTable;
    public EntityHealthController playerHealthController;

    void OnCollisionEnter(Collision collision)
    {
        if (hitDetection == HitDetection.ColliderOnly || hitDetection == HitDetection.All)
        {
            Debug.Log("Type = Collider");
            HandleHit(collision.collider);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hitDetection == HitDetection.TriggerOnly || hitDetection == HitDetection.All)
        {
            Debug.Log("Type = Trigger");
            HandleHit(other);
        }
    }

    protected virtual void HandleHit(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        ContactDamageTable.Entry entry = damageTable.GetEntry(other.gameObject.tag);

        if (entry != null)
        {
            Debug.Log("Tag [" + other.gameObject.tag + "] from the table detected on [" + other.gameObject.name +"]. Applying damage logic.");
            if (entry.instantKill) playerHealthController.InstantlyDie();
            else playerHealthController.TakeDamage(entry.damage, true);
        }
        else
        {
            Debug.Log("No [" + other.gameObject.tag + "] tag assiciated entry detected on [" + other.gameObject.name + "]. Doing nothing.");
        }
    }
}
