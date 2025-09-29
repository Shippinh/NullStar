using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 400f;
    public float maxLifetime = 2f;

    protected Vector3 targetPosition;
    public bool hasTarget = false;

    private Vector3 lastPosition;

    void OnEnable()
    {
        Invoke(nameof(Deactivate), maxLifetime);
        lastPosition = transform.position;
    }

    public void Initialize(Vector3 startPosition, Vector3 hitPosition)
    {
        transform.position = startPosition;
        targetPosition = hitPosition;
        hasTarget = true;
        lastPosition = startPosition;
    }

    void Update()
    {
        if (!hasTarget) return;

        float step = speed * Time.deltaTime;
        Vector3 nextPos = Vector3.MoveTowards(transform.position, targetPosition, step);

        // --- Raycast to detect hit along movement path ---
        Vector3 direction = nextPos - transform.position;
        float distance = direction.magnitude;

        if (Physics.Raycast(transform.position, direction.normalized, out RaycastHit hit, distance))
        {
            HandleHit(hit); // virtual method, overridden by subclasses
            return;
        }

        // Move projectile
        transform.position = nextPos;

        // Check if reached target
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Impact();
        }

        lastPosition = transform.position;
    }

    protected virtual void HandleHit(RaycastHit hit)
    {
        // Base projectile does nothing on hit
        Impact();
    }

    public void Impact()
    {
        hasTarget = false;
        Deactivate();
    }

    public void Deactivate()
    {
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        CancelInvoke();
    }
}
