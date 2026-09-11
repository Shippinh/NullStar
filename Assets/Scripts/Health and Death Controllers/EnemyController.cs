using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : DestructibleController, IOwnedPoolable
{
    public string IPoolableTag { get; set; } // always gets set, because no enemy can exists without getting depooled first, unless scripted to be like that specifically

    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    private float waveToAppear;

    [SerializeField] protected EnemyAIComponent enemyAIRef;
    protected TurretBehavior[] enemyTurretsRefs;

    private IPooledSpawnOwner currentOwner;

    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    public EnemyAIComponent GetAIComponent()
    {
        return enemyAIRef;
    }

    public virtual void HandleRailAttach(float initialRailSpeed)
    {
        if (enemyAIRef != null)
        {
            //Debug.Log(name.ToString() + " handling rail attach");
            enemyAIRef.enabled = false;
        }
    }

    public virtual void HandleRailDettach()
    {
        if (enemyAIRef != null)
        {
            enemyAIRef.enabled = true;
            enemyAIRef.GetRidigbody().isKinematic = false;
        }
    }

    public virtual void HandleRailSpeedChange(float newSpeed)
    {

    }

    // ── Ownership ─────────────────────────────────────────────────────────

    public void ClaimOwnership(IPooledSpawnOwner newOwner)
    {
        if (currentOwner != null && currentOwner != newOwner)
        {
            Debug.LogWarning($"[EnemyController:{name}] {currentOwner} still owned this " +
                              $"when {newOwner} claimed it — forcing release of stale owner.");
            currentOwner.OnEntityReleased(this);
        }
        currentOwner = newOwner;
    }

    public void ReleaseOwnership()
    {
        currentOwner?.OnEntityReleased(this);
        currentOwner = null;
    }

    // ── Pooling ───────────────────────────────────────────────────────────

    // When grabbing from the pool
    public virtual void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;

        transform.position = position;
        transform.rotation = rotation;

        // Revive (prepare entity health controller) - "true" will call HandleRevival() after Revive() is done
        entityHealthControllerRef.Revive(true);

        // NOTE: ownership is claimed by ObjectPool.GetPooledObject right after this call returns,
        // not here — HandleDepool doesn't know who's asking.
    }

    // When returning to the pool
    public virtual void HandleRepool()
    {
        this.gameObject.SetActive(false);
        // ReleaseOwnership() is already called by ObjectPool.ReturnToPool before HandleRepool runs.
    }

    // On death
    public override void HandleDeath()
    {
        // Handle base death
        base.HandleDeath();

        // Then return to pool if it's a standalone enemy and was taken from the pool before
        if (!string.IsNullOrEmpty(IPoolableTag) && countsAsSeparateEnemy)
            ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag); // ownership release happens here

        if (enemyAIRef != null)
        {
            HandleRailDettach();
        }
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
            enemyAIRef = GetComponentInChildren<EnemyAIComponent>();

        if (enemyTurretsRefs == null)
            enemyTurretsRefs = GetComponentsInChildren<TurretBehavior>();

        /*
        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
        */
    }

    public TurretBehavior[] GetTurrets()
    {
        return enemyTurretsRefs;
    }
}