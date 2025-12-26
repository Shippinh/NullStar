using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WormEnemyController : EnemyController
{
    [Header("References")]
    public Transform segmentsParentRef;
    [SerializeField] private List<Transform> segmentsLogic = new List<Transform>();
    [SerializeField] private List<Transform> segmentsVisuals = new List<Transform>();
    public List<EntityHealthController> weakPointHealths = new List<EntityHealthController>();

    public WormEnemy enemyAIRef;

    [Header("Internal Variables")]

    public int deadWeakPoints = 0;
    public int segmentMaxHP = 3;

    private bool firstInitialization = true;
    private float distanceBetweenSegments = 0; // This is internal, used for depooling for unusual orientations

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

        if (!enemyAIRef)
            enemyAIRef = GetComponentInChildren<WormEnemy>();

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
        if (weakPointHealths == null || weakPointHealths.Count == 0)
            weakPointHealths.AddRange(GetComponentsInChildren<EntityHealthController>(true));

        // Remove the base health reference if included
        if (weakPointHealths != null || weakPointHealths.Count > 0)
            weakPointHealths.Remove(entityHealthControllerRef);

        if (firstInitialization)
        {
            firstInitialization = false;

            distanceBetweenSegments = Vector3.Distance(transform.position, segmentsLogic[0].position);

            // Subscribe to weak point deaths
            foreach (var wp in weakPointHealths)
            {
                wp.SetMaxHP(segmentMaxHP);
                // right now i handle children weak points like the default enemy controller, but i guess i'll have to remake it later so the object doesn't actually disappear, since it's a part of its mesh
                wp.Died += () => OnWeakPointDied(wp);
            }
        }
    }

    private void OnWeakPointDied(EntityHealthController deadHP)
    {
        deadWeakPoints++;
        //Debug.Log($"Weak point {deadHP.name} died ({deadWeakPoints}/{weakPointHealths.Count})");

        // If all weak points are dead, trigger worm death
        if (deadWeakPoints >= weakPointHealths.Count)
        {
            StartCoroutine(DeathSequence());
        }
    }

    // Overrides the basic method to properly revive all sub-entity health controllers
    public override void HandleRevival()
    {
        // In case we allow an enemy to be reenqueued immediately we do this so it doesn't break completely
        StopAllCoroutines();

        foreach (EntityHealthController entity in weakPointHealths)
        {
            entity.Revive(true);
        }

        foreach (var entity in segmentsVisuals)
        {
            entity.gameObject.SetActive(true);
        }

            // Deactivate logic
        foreach (var entity in segmentsLogic)
        {
            entity.gameObject.SetActive(true);
        }

        deadWeakPoints = 0;

        base.HandleRevival();
    }

    // Worm enemy is trickier, because we need to reset all segments instead of a single mesh
    public override void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;

        // The AI head position, we use this as base to iterate from
        transform.position = position;
        transform.rotation = rotation;

        ResetSegments();

        // Revive (prepare entity health controller) - "true" will call HandleRevival() after Revive() is done
        entityHealthControllerRef.Revive(true);
    }

    // Helper to access weak points
    public List<EntityHealthController> GetWeakPointHealths()
    {
        return weakPointHealths;
    }

    private IEnumerator DeathSequence()
    {
        // Shuffle segments for random deactivation order
        /* for (int i = 0; i < segmentsVisuals.Count; i++)
         {
             int rand = Random.Range(i, segmentsVisuals.Count);
             (segmentsVisuals[i], segmentsVisuals[rand]) = (segmentsVisuals[rand], segmentsVisuals[i]);
         }*/

        // Deactivate segments one by one
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


        // After all deactivations

        // Deactivate logic
        foreach (var entity in segmentsLogic)
        {
            if (entity != null && entity.gameObject.activeSelf)
            {
                entity.gameObject.SetActive(false);
            }
        }

        // Forcibly kill the main entity, since it's immune to instakills and immune to damage in general
        entityHealthControllerRef.ForciblyDie();
    }

    private void ResetSegments()
    {
        Transform previous = transform;

        Vector3 backward = -transform.forward;

        for (int i = 0; i < segmentsLogic.Count; i++)
        {
            Transform segment = segmentsLogic[i];

            // Position segment behind the previous transform
            segment.position = previous.position + backward * distanceBetweenSegments;

            // Match rotation exactly
            segment.rotation = transform.rotation;

            // Safety: ensure active
            segment.gameObject.SetActive(true);

            previous = segment;
        }

        // Reset visuals to match logic
        for (int i = 0; i < segmentsVisuals.Count; i++)
        {
            segmentsVisuals[i].localPosition = Vector3.zero;
            segmentsVisuals[i].localRotation = Quaternion.identity;
            segmentsVisuals[i].gameObject.SetActive(true);
        }
    }
}
