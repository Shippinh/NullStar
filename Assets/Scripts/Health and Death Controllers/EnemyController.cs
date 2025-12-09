using System.Collections;
using UnityEngine;

public class EnemyController : DestructibleController
{
    public EntityArenaController entityArenaControllerRef; // arena data reference (stuff like at what wave to appear, etc.)
    public string enemyName = "Default Enemy Name";
    public bool countsAsSeparateEnemy = true;
    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    public EntityArenaController GetEnemyArenaController()
    {
        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
        return entityArenaControllerRef;
    }

    /// <summary>
    /// Should be called only once
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        if (entityArenaControllerRef == null)
            entityArenaControllerRef = GetComponent<EntityArenaController>();
    }
}
