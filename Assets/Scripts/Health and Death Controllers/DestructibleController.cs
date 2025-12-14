using System.Collections;
using UnityEngine;


public class DestructibleController : MonoBehaviour
{
    public EntityHealthController entityHealthControllerRef; // health data reference (stuff like hp, dmg and death handling)
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

    // In case an object can't be pooled we can always just override the handler for dying and reviving

    /// <summary>
    /// Deactivates the game object when the entity health controller naturally dies
    /// </summary>
    public virtual void HandleDeath()
    {
        this.gameObject.SetActive(false);
    }

    /// <summary>
    /// Reactivates the game object
    /// </summary>
    public virtual void HandleRevival()
    {
        this.gameObject.SetActive(true);
    }

    // END

    public EntityHealthController GetHealthController()
    {
        if (entityHealthControllerRef == null)
            entityHealthControllerRef = GetComponent<EntityHealthController>();
        return entityHealthControllerRef;
    }

    /// <summary>
    /// Should be called only once
    /// </summary>
    protected virtual void Initialize()
    {
        if (entityHealthControllerRef == null)
            entityHealthControllerRef = GetComponent<EntityHealthController>();

        entityHealthControllerRef.Died += HandleDeath;      // when the entity health controller naturally dies deactivate the object
        entityHealthControllerRef.Revived += HandleRevival; // when the entity health controller naturally revives reactivate the object
    }
}
