using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : DestructibleController, IPoolable
{
    public string IPoolableTag {  get; set; } // always gets set, because no enemy can exists without getting depooled first, unless scripted to be like that specifically

    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    private float waveToAppear;

    [SerializeField] protected EnemyAIComponent enemyAIRef;
    protected TurretBehavior[] enemyTurretsRefs;
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

        this.gameObject.SetActive(false);
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
