using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : DestructibleController, IPoolable
{
    public string IPoolableTag {  get; set; } // always gets set, because no enemy can exists without getting depooled first, unless scripted to be like that specifically

    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    private float waveToAppear;
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

        /*
        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
        */
    }
}
