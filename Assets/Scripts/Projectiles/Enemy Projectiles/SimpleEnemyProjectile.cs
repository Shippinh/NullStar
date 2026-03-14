using UnityEngine;

public class SimpleEnemyProjectile : MonoBehaviour, IPoolable
{
    public string IPoolableTag { get; set; }

    [Header("Movement")]
    public float speed = 400f;
    public float maxLifetime = 2f;

    [Header("Damage")]
    public int damage = 1;
    public LayerMask hitLayers;

    private Rigidbody rb;
    private Vector3 direction;
    private bool impactHappened = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // POOL → ACTIVE
    public void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;
        impactHappened = false;

        transform.position = position;
        transform.rotation = rotation;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        CancelInvoke();
        Invoke(nameof(Impact), maxLifetime);

        gameObject.SetActive(true);
    }

    // ACTIVE → POOL
    public void HandleRepool()
    {
        CancelInvoke();
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition)
    {
        direction = (targetPosition - startPosition).normalized;
        rb.velocity = direction * speed;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        EntityHealthController health = other.GetComponent<EntityHealthController>();
        if (health != null)
            health.TakeDamage(damage, true);

        Impact();
    }

    private void Impact()
    {
        if (impactHappened) return;
        impactHappened = true;
        ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    protected  void HandleHit(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) != 0)
        {
            EntityHealthController health = other.GetComponent<EntityHealthController>();
            if (health != null)
                health.TakeDamage(damage, true);
        }
        Impact();
    }
}