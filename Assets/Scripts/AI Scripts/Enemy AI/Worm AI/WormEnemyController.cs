using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Rendering.HableCurve;

public class WormEnemyController : EnemyController
{
    [Header("References")]
    public Transform segmentsParentRef;
    [SerializeField] private List<Transform> segmentsLogic = new List<Transform>();
    [SerializeField] private List<Transform> segmentsVisuals = new List<Transform>();
    public List<EntityHealthController> weakPointHealths = new List<EntityHealthController>();

    public int deadWeakPoints = 0;
    public bool isDead = false;

    // Game Juice
    [Header("Random Death Timers")]
    [Tooltip("Minimum time between sub-entity deaths.")]
    public float minTimerDuration = 0.2f;


    [Tooltip("If this variable is false then the script will use Min Timer Duration for all deaths.")]
    public bool randomizeTimeBetweenDeaths = false;

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

        if ((segmentsLogic == null || segmentsLogic.Count == 0) || (segmentsVisuals == null || segmentsVisuals.Count == 0))
        {
            foreach (Transform child1 in segmentsParentRef)
            {
                segmentsLogic.Add(child1);
                foreach (Transform child2 in child1)
                {
                    segmentsVisuals.Add(child2);
                }
            }
        }

        // Grab all child weak points (including inactive)
        if(weakPointHealths == null || weakPointHealths.Count == 0)
            weakPointHealths.AddRange(GetComponentsInChildren<EntityHealthController>(true));

        // Remove the base health reference if included
        if(weakPointHealths != null || weakPointHealths.Count > 0)
            weakPointHealths.Remove(entityHealthControllerRef);

        // Subscribe to weak point deaths
        foreach (var wp in weakPointHealths)
        {
            // right now i handle children weak points like the default enemy controller, but i guess i'll have to remake it later so the object doesn't actually disappear, since it's a part of its mesh
            wp.Died += () => OnWeakPointDied(wp);
        }

        enemyName = "Worm Enemy";
    }

    private void OnWeakPointDied(EntityHealthController deadHP)
    {
        deadWeakPoints++;
        //Debug.Log($"Weak point {deadHP.name} died ({deadWeakPoints}/{weakPointHealths.Count})");

        // If all weak points are dead, trigger worm death
        if (!isDead && deadWeakPoints >= weakPointHealths.Count)
        {
            StartCoroutine(DeathSequence());
        }
    }

    // Overrides the basic method to properly revive all sub-entity health controllers
    public override void HandleRevival()
    {
        base.HandleRevival();
    }

    // Helper to access weak points
    public List<EntityHealthController> GetWeakPointHealths()
    {
        return weakPointHealths;
    }

    private IEnumerator DeathSequence()
    {
        // Shuffle for random death order
       /* for (int i = 0; i < segmentsVisuals.Count; i++)
        {
            int rand = Random.Range(i, segmentsVisuals.Count);
            (segmentsVisuals[i], segmentsVisuals[rand]) = (segmentsVisuals[rand], segmentsVisuals[i]);
        }*/

        // Kill them one by one
        foreach (var entity in segmentsVisuals)
        {
            if (entity != null && entity.gameObject.activeSelf)
            {
                entity.gameObject.SetActive(false);
            }

            float duration = minTimerDuration;

            // Randomized timing
            if (randomizeTimeBetweenDeaths)
            duration = (Random.value < longDeathChance)
                ? Random.Range(lowerTimerDuration, maxTimerDuration)
                : Random.Range(minTimerDuration, lowerTimerDuration);

            yield return new WaitForSeconds(duration);
        }


        // After all deaths

        // Deactivate logic
        foreach (var entity in segmentsLogic)
        {
            if (entity != null && entity.gameObject.activeSelf)
            {
                entity.gameObject.SetActive(false);
            }
        }

        // Deactivate the main entity
        this.gameObject.SetActive(false);
    }
}
