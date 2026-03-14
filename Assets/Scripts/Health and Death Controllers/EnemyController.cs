using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : DestructibleController, IPoolable, IRailAttachable
{
    public string IPoolableTag {  get; set; } // always gets set, because no enemy can exists without getting depooled first, unless scripted to be like that specifically

    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    private float waveToAppear;

    protected EnemyAIComponent enemyAIRef;
    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    // When grabbing from the pool
    public virtual void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;

        transform.position = position;
        transform.rotation = rotation;

        // Revive (prepare entity health controller) - "true" will call HandleRevival() after Revive() is done
        entityHealthControllerRef.Revive(true);
    }


    // When returning to the pool
    public virtual void HandleRepool()
    {
        //HandleDeath();
    }

    // On death
    public override void HandleDeath()
    {
        // Handle base death
        base.HandleDeath();

        // Then return to pool if it's a standalone enemy and was taken from the pool before
        if(!string.IsNullOrEmpty(IPoolableTag) && countsAsSeparateEnemy)
            ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag);
    }

    // From the enemy controller side we need to give controls to the RailAIController properly
    public virtual void HandleRailAttach()
    {
        enemyAIRef.enabled = false;

        enemyAIRef.SetRBKinematic(true);
    }

    // From the enemy controller side we need to make sure we can give back controls to the normal AI properly
    public virtual void HandleRailDetach()
    {
        enemyAIRef.SetRBKinematic(false);

        enemyAIRef.enabled = true;

    }

    // On revival
    public override void HandleRevival()
    {
        // Handle base revival
        base.HandleRevival();
    }

    public float GetWaveToAppear()
    {
        return waveToAppear;
    }

    /// <summary>
    /// Should be called only once
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        if (!enemyAIRef)
            enemyAIRef = GetComponent<EnemyAIComponent>();

        /*
        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
        */
    }
}
