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

        // This is for visuals ONLY
        GameObject projectile = projectilePool.GetPooledObject(projectileTag);
        if (projectile != null)
        {
            projectile.transform.position = muzzlePoint.position;
            projectile.transform.rotation = Quaternion.LookRotation(targetPoint - muzzlePoint.position);

            projectile.GetComponent<Projectile>().Initialize(muzzlePoint.position, targetPoint);

            projectile.SetActive(true);
        }

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
        Transform hitTransform = hit.transform;
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
