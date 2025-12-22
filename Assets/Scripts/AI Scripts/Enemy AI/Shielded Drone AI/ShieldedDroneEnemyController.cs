using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldedDroneEnemyController : EnemyController
{
    [Header("References")]
    public Transform shieldsParentRef;
    public Transform gunsParentRef;

    public EntityHealthController coreHealthController;
    public List<EntityHealthController> shieldHealthControllers = new();
    public List<EntityHealthController> gunHealthControllers = new();

    public ShieldedDroneEnemy enemyAIRef;

    // Game Juice
    [Header("Random Death Timers")]
    [Tooltip("Minimum time between sub-entity deaths.")]
    public float minTimerDuration = 0.2f;

    [Tooltip("Lower bound for common quick deaths.")]
    public float lowerTimerDuration = 0.4f;

    [Tooltip("Maximum time for rare slow deaths.")]
    public float maxTimerDuration = 1.5f;

    [Tooltip("Chance (0–1) that a slow death interval will occur.")]
    [Range(0f, 1f)]
    public float longDeathChance = 0.15f;

    void Awake()
    {
        Initialize();
        countsAsSeparateEnemy = true;

        if(!enemyAIRef)
            enemyAIRef = GetComponentInChildren<ShieldedDroneEnemy>();

        // Collect all sub-entity health controllers
        if(shieldHealthControllers == null || shieldHealthControllers.Count == 0)
            shieldHealthControllers.AddRange(shieldsParentRef.GetComponentsInChildren<EntityHealthController>(true));

        if (gunHealthControllers == null || gunHealthControllers.Count == 0)
            gunHealthControllers.AddRange(gunsParentRef.GetComponentsInChildren<EntityHealthController>(true));

        // if core dies - kill everything else
        coreHealthController.Died += HandleCoreDeath;
    }

    private void HandleCoreDeath()
    {
        // Stop AI behavior
        enemyAIRef.canAct = false;

        // Combine all sub-controllers
        List<EntityHealthController> allSubEntities = new();
        allSubEntities.AddRange(shieldHealthControllers);
        allSubEntities.AddRange(gunHealthControllers);

        // Filter out already-dead or inactive entities
        allSubEntities.RemoveAll(e => e == null || !e.gameObject.activeSelf || e.IsAlive() == false);

        if (allSubEntities.Count == 0)
        {
            // Everything's already dead, just disable the main object
            entityHealthControllerRef.gameObject.SetActive(false);
            return;
        }

        // otherwise - start death sequence
        StartCoroutine(DeathSequence(allSubEntities));
    }

    public override void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        base.HandleDepool(poolableTag, position, rotation);
        coreHealthController.Revive(true);
    }

    // Overrides the basic method to properly revive all sub-entity health controllers
    public override void HandleRevival()
    {
        // In case we allow an enemy to be reenqueued immediately we do this so it doesn't break completely
        StopAllCoroutines();

        List<EntityHealthController> allSubEntities = new();
        allSubEntities.AddRange(shieldHealthControllers);
        allSubEntities.AddRange(gunHealthControllers);

        foreach (EntityHealthController entity in allSubEntities)
        {
            entity.Revive(true);
        }

        base.HandleRevival();
    }

    // THIS SHOULD BE REMADE TO USE A PROPER TIMER, WAY TOO UNRELIABLE, god i hate coroutines
    private IEnumerator DeathSequence(List<EntityHealthController> subEntities)
    {
        // Shuffle for random death order
        for (int i = 0; i < subEntities.Count; i++)
        {
            int rand = Random.Range(i, subEntities.Count);
            (subEntities[i], subEntities[rand]) = (subEntities[rand], subEntities[i]);
        }

        // Kill them one by one
        foreach (var entity in subEntities)
        {
            if (entity != null && entity.gameObject.activeSelf)
            {
                entity.ForciblyDie();
            }

            // Randomized timing
            float duration = (Random.value < longDeathChance)
                ? Random.Range(lowerTimerDuration, maxTimerDuration)
                : Random.Range(minTimerDuration, lowerTimerDuration);

            yield return new WaitForSeconds(duration);
        }

        // Forcibly kill the main entity, since it's immune to instakills and immune to damage in general
        entityHealthControllerRef.ForciblyDie();
    }
}
