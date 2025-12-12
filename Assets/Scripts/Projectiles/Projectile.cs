using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 400f;
    public float maxLifetime = 2f;

    protected Vector3 direction;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        Invoke(nameof(Deactivate), maxLifetime);
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition)
    {
        transform.position = startPosition;
        direction = (targetPosition - startPosition).normalized;
        rb.velocity = direction * speed; // use Rigidbody to move
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    protected virtual void HandleHit(Collider other)
    {
        Debug.Log(gameObject.name + " just hit " + other.gameObject.name);
        Impact();
    }

    public void Impact()
    {
        rb.velocity = Vector3.zero;
        Deactivate();
    }

    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (rb != null) rb.velocity = Vector3.zero;
    }
}
