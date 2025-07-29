using System.Collections;
using UnityEngine;


public class EnemyController : MonoBehaviour
{
    public string enemyName = "Default Enemy Name";
    public EntityHealthController healthControllerRef;
    // Use this for initialization
    void Awake()
    {
        healthControllerRef = GetComponent<EntityHealthController>();
        healthControllerRef.Died += HandleEnemyDeath;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void HandleEnemyDeath()
    {
        this.gameObject.SetActive(false);
    }
}
