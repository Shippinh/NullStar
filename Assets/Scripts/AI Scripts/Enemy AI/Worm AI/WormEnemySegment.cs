using UnityEngine;

// Describes movement for segments
public class WormEnemySegment : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform targetSegment;
    public float followDistance = 1f;
    public float moveSpeed = 10f;
    public float rotationSpeed = 5f;

    [Header("Local Rotation")]
    public float localZRotationSpeed = 90f; // degrees per second
    public bool invertLocalRotation = false;

    private float currentZRotation = 0f;

    private Vector3 _smoothedTargetPos;
    private bool _initialized;

    void Update()
    {
        if (targetSegment == null) return;

        if (!_initialized)
        {
            _smoothedTargetPos = targetSegment.position;
            _initialized = true;
            return;
        }

        // Smooth the target position before chasing it — hides discrete physics steps
        _smoothedTargetPos = Vector3.Lerp(_smoothedTargetPos, targetSegment.position, moveSpeed * Time.deltaTime);

        Vector3 toTarget = _smoothedTargetPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance > followDistance)
        {
            Vector3 movePosition = _smoothedTargetPos - toTarget.normalized * followDistance;
            transform.position = Vector3.MoveTowards(transform.position, movePosition, moveSpeed * Time.deltaTime);
        }

        float rotationDirection = invertLocalRotation ? -1f : 1f;
        currentZRotation += localZRotationSpeed * rotationDirection * Time.deltaTime;

        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(toTarget);
            Quaternion localRotation = Quaternion.Euler(0f, 0f, currentZRotation);
            Quaternion targetRotation = lookRotation * localRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
