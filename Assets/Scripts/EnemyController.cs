using System.Collections;
using UnityEngine;


public class EnemyController : MonoBehaviour
{
    public string enemyName = "Default Enemy Name";
    public EntityHealthController entityHealthControllerRef; // health data reference (stuff like hp, dmg and death handling)
    public EntityArenaController entityArenaControllerRef; // arena data reference (stuff like at what wave to appear, etc.)
    public bool countsAsSeparateEnemy = true;
    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    // START

    // The reason this is here is pretty simple, i don't want internals to control the external logic for dying

    // for example: when this specific enemy dies we use EntityHealthController only to control states.
    // The actual visuals are called only after we handle the logic within.
    // In this case - the enemy just get returned to pool.
    // (Enemy pooling is not yet implemented, so for now we just disable the game object instead)

    // HandleEnemyDeath() allows us to do all sort of stuff here before an enemy truly dies and gets sent back to pool, for example:
    // - call particles,
    // - change the size of an enemy

    // See ShieldedDroneEnemyController or WormEnemyController for examples. We can have as many variations for as many enemy types we want to make.

    // Then we'll just use HandleEnemyRevival() to grab a pooled object, undo shit, if any was applied, and then just reactivate it.

    // STRICTLY CONTROL POOLING WITH THIS
    /// <summary>
    /// Deactivates the game object when the enemy dies
    /// </summary>
    public virtual void HandleEnemyDeath()
    {
        this.gameObject.SetActive(false);
    }

    // STRICTLY CONTROL POOLING WITH THIS
    /// <summary>
    /// Reactivates the game object when the enemy gets revived
    /// </summary>
    public virtual void HandleEnemyRevival()
    {
        this.gameObject.SetActive(true);
    }

    // END

    public EntityHealthController GetEnemyHealthController()
    {
        if (entityHealthControllerRef == null)
            entityHealthControllerRef = GetComponent<EntityHealthController>();
        return entityHealthControllerRef;
    }

    public EntityArenaController GetEnemyArenaController()
    {
        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
        return entityArenaControllerRef;
    }

    /// <summary>
    /// Should be called only once
    /// </summary>
    protected void Initialize()
    {
        if (entityHealthControllerRef == null)
            entityHealthControllerRef = GetComponent<EntityHealthController>();

        entityHealthControllerRef.Died += HandleEnemyDeath;
        entityHealthControllerRef.Revived += HandleEnemyRevival;

        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
    }
}
