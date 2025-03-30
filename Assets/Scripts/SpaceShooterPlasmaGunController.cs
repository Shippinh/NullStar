using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceShooterPlasmaGunControllers : MonoBehaviour
{
    public CustomInputs inputConfig;
    public SpaceShooterController playerRef;
    
    [Header("Shooting Settings")]
    public Transform muzzlePoint;
    public Camera playerCamera;
    public LayerMask hitLayers;
    public float fireRate = 0.2f;
    private float nextFireTime;
    
    [Header("Power-ups")]
    public bool rageActive;
    public bool adrenalineActive;
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
        if (rageActive) fireRateMultiplier *= 2f;
        if (adrenalineActive) fireRateMultiplier *= 4f;
    }

    void Fire()
    {
        Ray cameraRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit cameraHit;
        Vector3 targetPoint = cameraRay.origin + cameraRay.direction * 100f;
        
        if (Physics.Raycast(cameraRay, out cameraHit, 100f, hitLayers))
        {
            targetPoint = cameraHit.point;
            HandleImpact(cameraHit);
        }

        // Muzzle Raycast (For visual alignment)
        Ray muzzleRay = new Ray(muzzlePoint.position, (targetPoint - muzzlePoint.position).normalized);
        RaycastHit muzzleHit;
        if (Physics.Raycast(muzzleRay, out muzzleHit, 100f, hitLayers))
        {
            targetPoint = muzzleHit.point;
        }

        // Fire visual projectile
        GameObject projectile = projectilePool.GetPooledObject();
        if (projectile != null)
        {
            projectile.transform.position = muzzlePoint.position;
            projectile.transform.rotation = Quaternion.LookRotation(targetPoint - muzzlePoint.position);
            projectile.SetActive(true);
        }

        // Fire tracer effect
        GameObject tracer = tracerPool.GetPooledObject();
        if (tracer != null)
        {
            tracer.transform.position = muzzlePoint.position;
            tracer.transform.rotation = Quaternion.LookRotation(targetPoint - muzzlePoint.position);
            tracer.SetActive(true);
        }
    }
    
    void HandleImpact(RaycastHit hit)
    {
        GameObject impact = impactEffectPool.GetPooledObject();
        if (impact != null)
        {
            impact.transform.position = hit.point;
            impact.transform.rotation = Quaternion.LookRotation(hit.normal);
            impact.SetActive(true);
        }
    }
}

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int poolSize = 10;
    private List<GameObject> pool;
    
    void Awake()
    {
        pool = new List<GameObject>();
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }
    
    public GameObject GetPooledObject()
    {
        foreach (GameObject obj in pool)
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }
        return null;
    }
}
