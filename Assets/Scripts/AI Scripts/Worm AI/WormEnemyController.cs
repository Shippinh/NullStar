using System.Collections.Generic;
using UnityEngine;

public class WormEnemyController : EnemyController
{
    [Header("References")]
    public List<EntityHealthController> weakPointHealths = new List<EntityHealthController>();

    public int deadSegments = 0;
    public bool isDead = false;

    void Awake()
    {
        Initialize();

        countsAsSeparateEnemy = true;

        // Grab all child weak points (including inactive)
        weakPointHealths.AddRange(GetComponentsInChildren<EntityHealthController>(true));

        // Remove the base health reference if included
        weakPointHealths.Remove(entityHealthControllerRef);

        // Subscribe to weak point deaths
        foreach (var wp in weakPointHealths)
        {
            wp.Died += () => OnWeakPointDied(wp);
        }

        enemyName = "Worm Enemy";
    }

    private void OnWeakPointDied(EntityHealthController deadHP)
    {
        deadSegments++;
        Debug.Log($"Weak point {deadHP.name} died ({deadSegments}/{weakPointHealths.Count})");

        // If all weak points are dead, trigger worm death
        if (!isDead && deadSegments >= weakPointHealths.Count)
        {
            entityHealthControllerRef.CurrentHP = 0;
        }
    }

    // Optional helper to access weak points
    public List<EntityHealthController> GetWeakPointHealths()
    {
        return weakPointHealths;
    }
}
