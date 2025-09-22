using System.Collections;
using UnityEngine;


public class EnemyController : MonoBehaviour
{
    public string enemyName = "Default Enemy Name";
    [SerializeField] private EntityHealthController entityHealthControllerRef; // health data reference (stuff like hp, dmg and death handling)
    [SerializeField] private EntityArenaController entityArenaControllerRef; // arena data reference (stuff like at what wave to appear, etc.)
    //public GeneralArenaController generalArenaControllerRef; // reference to the general arena, set in the inspector (allows setting up multiple arenas with one time switches that enable or disable next arena)
    // Use this for initialization
    void Awake()
    {
        entityHealthControllerRef = GetComponent<EntityHealthController>();
        entityHealthControllerRef.Died += HandleEnemyDeath;

        entityArenaControllerRef = GetComponent<EntityArenaController>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void HandleEnemyDeath()
    {
        //generalArenaControllerRef.RemoveEnemyFromList(this); // INSTANCING IS PERFECT, LETS FUCKING GOOOOO, NO MINDFUCKERY THIS TIME
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
}
