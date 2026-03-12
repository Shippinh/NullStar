using UnityEngine;

public class Projectile : MonoBehaviour, IPoolable
{
    public string IPoolableTag { get; set; }

    public float speed = 400f;
    public float maxLifetime = 2f;
    public LayerMask hitLayers;

    private Vector3 direction;
    private float distanceToTarget;
    private float travelled = 0f;
    private bool impactHappened = false;

    public void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;
        impactHappened = false;
        travelled = 0f;
        distanceToTarget = 0f;

        transform.position = position;
        transform.rotation = rotation;

        CancelInvoke();
        Invoke(nameof(Impact), maxLifetime);

        gameObject.SetActive(true);
    }

    public void HandleRepool()
    {
        CancelInvoke();
        gameObject.SetActive(false);
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition)
    {
        direction = (targetPosition - startPosition).normalized;
        distanceToTarget = Vector3.Distance(startPosition, targetPosition);
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, step, hitLayers))
        {
            transform.position = hit.point;
            Impact();
            return;
        }

        transform.position += direction * step;
        travelled += step;

        if (travelled >= distanceToTarget)
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
    }
}