using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 400f;
    public float maxLifetime = 2f;
    
    private Vector3 targetPosition;
    private bool hasTarget = false;
    
    void OnEnable()
    {
        Invoke(nameof(Deactivate), maxLifetime); // Auto-disable after time
    }

    public void Initialize(Vector3 startPosition, Vector3 hitPosition)
    {
        transform.position = startPosition;
        targetPosition = hitPosition;
        hasTarget = true;
    }

    void Update()
    {
        if (!hasTarget) return;

        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            Impact();
        }
    }

    void Impact()
    {
        hasTarget = false;
        Deactivate();
    }

    void Deactivate()
    {
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        CancelInvoke();
    }
}
