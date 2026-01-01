using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterPlasmaGunController : MonoBehaviour
{
    public CustomInputs inputConfig;
    public SpaceShooterController playerRef;
    public int damage = 1;
    
    [Header("Shooting Settings")]
    public Transform muzzlePoint;
    public Camera playerCamera;
    public LayerMask hitLayers;
    public float fireRate = 0.2f;
    public float hitscanRange = 2000f;
    private float nextFireTime;
    
    [Header("Power-ups")]
    private float fireRateMultiplier = 1f;
    
    [Header("Pooling")]
    public ObjectPool projectilePool;
    public ObjectPool tracerPool;
    public ObjectPool impactEffectPool;
    
    void Update()
    {
        UpdatePowerUps();
        if (Input.GetKey(inputConfig.Shoot) && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + (fireRate / fireRateMultiplier);
        }
    }

    void UpdatePowerUps()
    {
        fireRateMultiplier = 1f;
        if (playerRef.rageActive) fireRateMultiplier *= 2f;
        if (playerRef.adrenalineActive) fireRateMultiplier *= 4f;
    }

    void Fire()
    {
        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit cameraHit;
        Vector3 targetPoint = cameraRay.origin + cameraRay.direction * hitscanRange;
        
        if (Physics.Raycast(cameraRay, out cameraHit, hitscanRange, hitLayers))
        {
            targetPoint = cameraHit.point;
            //Debug.Log("Hit enemy " + cameraHit.collider.gameObject.name);
            HandleImpact(cameraHit);
        }

        // Muzzle Raycast (For visual alignment)
        Ray muzzleRay = new Ray(muzzlePoint.position, (targetPoint - muzzlePoint.position).normalized);
        RaycastHit muzzleHit;
        if (Physics.Raycast(muzzleRay, out muzzleHit, hitscanRange, hitLayers))
        {
            targetPoint = muzzleHit.point;
        }

        // Fire visual projectile
        string projectileTag = "";

        if(playerRef.rageActive && !playerRef.adrenalineActive)
        {
            projectileTag = "Rage Projectile";
        }
        else if(!playerRef.rageActive && playerRef.adrenalineActive)
        {
            projectileTag = "Adrenaline Projectile";
        }

        if(playerRef.rageActive && playerRef.adrenalineActive)
        {
            projectileTag = "Rage-Adrenaline Projectile";
        }
        else if(!playerRef.rageActive && !playerRef.adrenalineActive)
        {
            projectileTag = "Normal Projectile";
        }

        if (string.IsNullOrEmpty(projectileTag)) return;

        // This is for visuals ONLY
        GameObject projectile = projectilePool.GetPooledObject(projectileTag, muzzlePoint.position, Quaternion.LookRotation(targetPoint - muzzlePoint.position), true);
        if (projectile != null)
            projectile.GetComponent<Projectile>().Initialize(muzzlePoint.position, targetPoint);

        // Fire tracer effect
        /*GameObject tracer = tracerPool.GetPooledObject();
        if (tracer != null)
        {
            tracer.transform.position = muzzlePoint.position;
            tracer.transform.rotation = Quaternion.LookRotation(targetPoint - muzzlePoint.position);
            tracer.SetActive(true);
        }*/
    }
    
    void HandleImpact(RaycastHit hit)
    {
        /*GameObject impact = impactEffectPool.GetPooledObject();
        if (impact != null)
        {
            impact.transform.position = hit.point;
            impact.transform.rotation = Quaternion.LookRotation(hit.normal);
            impact.SetActive(true);
        }*/

        // this is what was used before - on hit we would've hit the topmost health controller (or the health controller on rigidbody, i don't really remember)
        // that combined with rigidbody nature of destructible things just made everything really complicated
        // However this could be used for the instakills as we could reference the enemy controller easily this way
        // Transform hitTransform = hit.transform;

        // instead we are going to register hit on the exact collider we hit
        // This allows us to mix and match approaches of modular and single piece enemies way more easily (we just need a collider on specific layer)
        // For this to work - apply an Entity Health Controller to hittable colliders (specified in hitLayers)
        // Then connect all these health controllers to an Enemy Controller to handle it properly. (Ideally enemy controller should be in the topmost object paired with an AI script)
        // If it's multi piece enemy - create a custom enemy controller to handle all the sub pieces
        // (or to handle special death behavior, see examples in WormEnemyController (dies only when all sub pieces are dead) and ShieldedDroneEnemyController (dies only if the core is destroyed, changes behavior if the player kills all weapons))
        Transform hitTransform = hit.collider.transform;
        if(hitTransform != null)
        {
            EntityHealthController hitHealthController = hitTransform.GetComponent<EntityHealthController>();
            if(hitHealthController != null)
            {
                hitHealthController.TakeDamage(damage, true);
            }
        }
    }
}
