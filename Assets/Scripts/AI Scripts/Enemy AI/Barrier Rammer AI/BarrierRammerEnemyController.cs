using UnityEngine;

// Has a special death controller due to the complexity of AI.
public class BarrierRammerEnemyController : EnemyController
{
    [Header("References")]
    public EntityHealthController enemyAHealth; // preset in inspector
    public EntityHealthController enemyBHealth; // preset in inspector

    public int deadCount = 0;

    public BarrierRammerEnemyCentralized enemyAIRef; // preset in inspector

    void Awake()
    {
        Initialize();
        countsAsSeparateEnemy = true;
        enemyName = "Barrier Rammer";

        if(!enemyAIRef)
            enemyAIRef = GetComponentInChildren<BarrierRammerEnemyCentralized>();

        if (enemyAHealth != null)
            enemyAHealth.Died += OnEnemyADied;
        else
            deadCount++;

        if (enemyBHealth != null)
            enemyBHealth.Died += OnEnemyBDied;
        else
            deadCount++;

        // force death so the invisible enemy doesn't try to kill the player with no enemies present
        if (deadCount == 2)
        {
            entityHealthControllerRef.ForciblyDieOverGodMode();
            return;
        }
    }

    private void OnEnemyADied()
    {
        deadCount++;

        // force death so the invisible enemy doesn't try to kill the player with no enemies present
        if (deadCount == 2)
        {
            entityHealthControllerRef.ForciblyDieOverGodMode();
            return;
        }

        enemyAIRef.HandlePartnerDeath(isA: true);
    }

    private void OnEnemyBDied()
    {
        deadCount++;

        // force death so the invisible enemy doesn't try to kill the player with no enemies present
        if (deadCount == 2)
        {
            entityHealthControllerRef.ForciblyDieOverGodMode();
            return;
        }

        enemyAIRef.HandlePartnerDeath(isA: false);
    }
}
