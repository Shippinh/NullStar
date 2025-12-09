using UnityEngine;

// Has a special death controller due to the complexity of AI.
public class BarrierRammerEnemyController : EnemyController
{
    [Header("References")]
    public EntityHealthController enemyAHealth; // preset in inspector
    public EntityHealthController enemyBHealth; // preset in inspector

    public int deadCount = 0;

    public BarrierRammerEnemyCentralized behaviorControllerRef; // preset in inspector

    void Awake()
    {
        Initialize();
        countsAsSeparateEnemy = true;
        enemyName = "Barrier Rammer";

        if(behaviorControllerRef == null)
            behaviorControllerRef = GetComponent<BarrierRammerEnemyCentralized>();

        if (behaviorControllerRef != null)

        enemyName = "Barrier Rammer Duo";

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

        behaviorControllerRef.HandlePartnerDeath(isA: true);
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

        behaviorControllerRef.HandlePartnerDeath(isA: false);
    }
}
