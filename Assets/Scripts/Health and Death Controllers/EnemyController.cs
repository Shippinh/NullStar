using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class EnemyController : DestructibleController, IPoolable
{
    public string IPoolableTag {  get; set; }

    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    private float waveToAppear;
    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    public virtual void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        HandleRevival();
    }

    public virtual void HandleRepool()
    {
        HandleDeath();
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
