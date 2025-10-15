using System.Collections.Generic;
using UnityEngine;

public class ShieldedDroneEnemyHealthController : EnemyController
{
    [Header("References")]
    public List<EntityHealthController> shieldHealths = new List<EntityHealthController>();

    public int brokenShields = 0;
    public bool isDead = false;

    void Awake()
    {
        Initialize();

        countsAsSeparateEnemy = true;

        // Grab all child weak points (including inactive)
        shieldHealths.AddRange(GetComponentsInChildren<EntityHealthController>(true));

        // Remove the base health reference if included
        shieldHealths.Remove(entityHealthControllerRef);

        // Subscribe to weak point deaths
        foreach (var wp in shieldHealths)
        {
            wp.Died += () => OnWeakPointDied(wp);
        }

        enemyName = "Shielded Drone";
    }

    private void OnWeakPointDied(EntityHealthController deadHP)
    {
        brokenShields++;
        Debug.Log($"Weak point {deadHP.name} died ({brokenShields}/{shieldHealths.Count})");

        // If all shields are down
        if (!isDead && brokenShields >= shieldHealths.Count)
        {
            entityHealthControllerRef.canBeDamaged = true;
        }
    }

    // Optional helper to access weak points
    public List<EntityHealthController> GetWeakPointHealths()
    {
        return shieldHealths;
    }
}

