using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldedDroneEnemyController : EnemyController
{
    [Header("References")]
    public Transform shieldsParentRef;
    public Transform gunsParentRef;

    public List<EntityHealthController> shieldHealthControllers = new();
    public List<EntityHealthController> gunHealthControllers = new();

    public ShieldedEnemy enemyAIRef;

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

        // Collect all sub-entity health controllers
        shieldHealthControllers.AddRange(GetComponentsInChildren<EntityHealthController>(true));
        gunHealthControllers.AddRange(GetComponentsInChildren<EntityHealthController>(true));

        // Remove self if included
        shieldHealthControllers.Remove(entityHealthControllerRef);
        gunHealthControllers.Remove(entityHealthControllerRef);

        enemyName = "Shielded Drone";
    }

    public override void HandleDeath()
    {
        // Stop AI behavior
        if (enemyAIRef != null)
        {
            enemyAIRef.canAct = false;
            if (enemyAIRef.projectileEmittersControllerRef != null)
                enemyAIRef.projectileEmittersControllerRef.RequestSoftStop();
        }

        // Combine all sub-controllers
        List<EntityHealthController> allSubEntities = new();
        allSubEntities.AddRange(shieldHealthControllers);
        allSubEntities.AddRange(gunHealthControllers);

        // Filter out already-dead or inactive entities
        allSubEntities.RemoveAll(e => e == null || !e.gameObject.activeSelf || e.IsAlive() == false);

        if (allSubEntities.Count == 0)
        {
            // Everything's already dead, just disable the core
            entityHealthControllerRef.gameObject.SetActive(false);
            return;
        }

        StartCoroutine(DeathSequence(allSubEntities));
    }

    // Overrides the basic method to properly revive all sub-entity health controllers
    public override void HandleRevival()
    {
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

        // Deactivate the main entity after all deaths
        entityHealthControllerRef.gameObject.SetActive(false);
    }
}
