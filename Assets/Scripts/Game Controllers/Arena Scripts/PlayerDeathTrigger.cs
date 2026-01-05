using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerDeathTrigger : MonoBehaviour
{
    public Collider playerColliderRef;
    [SerializeField] private EntityHealthController playerHealthControllerRef;
    public float timeToDie = 12f;
    public float deathTimer = 0f;

    private void Update()
    {
        if(playerHealthControllerRef)
        {
            deathTimer += Time.deltaTime;

            if (deathTimer >= timeToDie)
            {
                playerHealthControllerRef.ForciblyDie();
                playerHealthControllerRef = null;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            if (playerColliderRef == null)
            {
                playerColliderRef = other;
                playerHealthControllerRef = other.GetComponent<SpaceShooterController>().healthController;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            playerColliderRef = null;
            playerHealthControllerRef = null;
            deathTimer = 0f;
            
        }
    }
}
