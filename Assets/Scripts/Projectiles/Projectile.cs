using UnityEngine;

public class Projectile : MonoBehaviour, IPoolable
{
    public string IPoolableTag { get; set; }

    public float speed = 400f;
    public float maxLifetime = 2f;

    protected Vector3 direction;
    private Rigidbody rb;

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

    // What happens when a collider enters projectile trigger
    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    // How we handle hits with the collider
    protected virtual void HandleHit(Collider other)
    {
        Impact();
    }


    // What happens during impact
    protected virtual void Impact()
    {
        if (impactHappened) return;
        impactHappened = true;

        ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag);
    }

    // SAFETY NET ONLY
    void OnDisable()
    {
        CancelInvoke();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
