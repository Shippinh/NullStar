using System.Collections;
using UnityEngine;


public class EnemyController : MonoBehaviour
{
    public string enemyName = "Default Enemy Name";
    public EntityHealthController entityHealthControllerRef; // health data reference (stuff like hp, dmg and death handling)
    public EntityArenaController entityArenaControllerRef; // arena data reference (stuff like at what wave to appear, etc.)
    public bool countsAsSeparateEnemy = true;
    // Use this for initialization
    void Awake()
    {
        Initialize();
    }

    public void HandleEnemyDeath()
    {
        this.gameObject.SetActive(false);
    }

    public EntityHealthController GetEnemyHealthController()
    {
        if (entityHealthControllerRef == null)
            entityHealthControllerRef = GetComponent<EntityHealthController>();
        return entityHealthControllerRef;
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
    protected void Initialize()
    {
        entityHealthControllerRef = GetComponent<EntityHealthController>();

        entityHealthControllerRef.Died += HandleEnemyDeath;

        entityArenaControllerRef = GetComponent<EntityArenaController>();
    }
}
