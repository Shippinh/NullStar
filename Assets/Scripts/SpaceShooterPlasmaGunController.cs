using UnityEngine;
using System.Collections.Generic;

public class SpaceShooterPlasmaGunController : MonoBehaviour
{
    [Header("References")]
    public Transform muzzleTransform;
    public LayerMask hitMask;
    
    [Header("Settings")]
    public float maxRange = 100f;
    
    [Header("Pooling")]
    public ObjectPool projectilePool;
    public ObjectPool hitEffectPool;
    public ObjectPool tracerPool;

    void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            FireShot();
        }
    }

    void FireShot()
    {
        Vector3 cameraPos = Camera.main.transform.position;
        Vector3 cameraDir = Camera.main.transform.forward;
        Vector3 targetPoint = cameraPos + (cameraDir * maxRange);

        RaycastHit hit;
        if (Physics.Raycast(cameraPos, cameraDir, out hit, maxRange, hitMask))
        {
            targetPoint = hit.point;

            /*EnemyHealth enemy = hit.collider.GetComponent<EnemyHealth>(); // Implement enemy class
            if (enemy)
            {
                enemy.TakeDamage(10);
            }*/

            if (hitEffectPool)
            {
                GameObject hitEffect = hitEffectPool.GetObject();
                hitEffect.transform.position = hit.point;
                hitEffect.transform.rotation = Quaternion.LookRotation(hit.normal);
                hitEffect.SetActive(true);
            }
        }

        if (muzzleTransform)
        {
            Vector3 muzzlePos = muzzleTransform.position;
            Vector3 visualDir = (targetPoint - muzzlePos).normalized;
            Vector3 projectileTarget = targetPoint;

            RaycastHit muzzleHit;
            if (Physics.Raycast(muzzlePos, visualDir, out muzzleHit, maxRange, hitMask))
            {
                projectileTarget = muzzleHit.point;
            }

            if (tracerPool)
            {
                GameObject tracer = tracerPool.GetObject();
                tracer.transform.position = muzzlePos;
                tracer.transform.LookAt(projectileTarget);
                tracer.SetActive(true);
            }

            if (projectilePool)
            {
                GameObject projectile = projectilePool.GetObject();
                projectile.transform.position = muzzlePos;
                projectile.transform.rotation = Quaternion.LookRotation(visualDir);
                projectile.GetComponent<Projectile>().SetTarget(projectileTarget);
                projectile.SetActive(true);
            }
        }
    }
}

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    private Queue<GameObject> pool = new Queue<GameObject>();

    public GameObject GetObject()
    {
        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        return Instantiate(prefab);
    }

    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}

public class Projectile : MonoBehaviour
{
    private Vector3 target;
    public float speed = 50f;

    public void SetTarget(Vector3 newTarget)
    {
        target = newTarget;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            gameObject.SetActive(false);
        }
    }
}
