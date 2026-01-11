using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarricadeScript : MonoBehaviour
{
    // On actual physical collision
    private void OnCollisionEnter(Collision collision)
    {
        //Debug.Log(collision.gameObject.name);

        if (collision.gameObject.tag == "Player")
        {
            SpaceShooterController player = collision.gameObject.GetComponent<SpaceShooterController>();

            if (!player.boostMode)
            {
                if (player.overboostInitiated && player.body.velocity.magnitude >= player.OverboostVelocityDeathLimit)
                {
                    player.healthController.InstantlyDie();
                    return;
                }
                else
                {
                    return;
                }
            }
            else
            {
                player.healthController.InstantlyDie();
                return;
            }
        }
    }
}
