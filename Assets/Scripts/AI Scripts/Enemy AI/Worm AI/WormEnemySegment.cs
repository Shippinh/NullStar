using UnityEngine;

public class WormEnemySegment : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform targetSegment;
    public float followDistance = 1f;
    public float moveSpeed = 10f;
    public float rotationSpeed = 5f;

    [Header("Local Rotation")]
    public float localZRotationSpeed = 90f;
    public bool invertLocalRotation = false;

    private float _currentZRotation;
    private Vector3 _smoothedTargetPos;
    private Vector3 _smoothedTargetVelocity;
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

        // Real position drives movement — no lag, no gaps
        Vector3 realToTarget = targetSegment.position - transform.position;
        float distance = realToTarget.magnitude;

        if (distance > followDistance + 0.01f)
        {
            Vector3 movePosition = targetSegment.position - realToTarget.normalized * followDistance;
            transform.position = Vector3.MoveTowards(transform.position, movePosition, moveSpeed * Time.deltaTime);
        }

        // Smoothed position drives rotation only — eliminates jitter without affecting distance
        _smoothedTargetPos = Vector3.SmoothDamp(
            _smoothedTargetPos, targetSegment.position,
            ref _smoothedTargetVelocity, 0.12f);

        Vector3 smoothToTarget = _smoothedTargetPos - transform.position;

        float rotDir = invertLocalRotation ? -1f : 1f;
        _currentZRotation += localZRotationSpeed * rotDir * Time.deltaTime;

        if (smoothToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(smoothToTarget);
            Quaternion localRotation = Quaternion.Euler(0f, 0f, _currentZRotation);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation * localRotation, rotationSpeed * Time.deltaTime);
        }
    }
}