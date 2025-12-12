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

    void Update()
    {
        if (targetSegment == null) return;

        // --- Position Following ---
        Vector3 toTarget = targetSegment.position - transform.position;
        float distance = toTarget.magnitude;

        if (distance > followDistance)
        {
            Vector3 movePosition = targetSegment.position - toTarget.normalized * followDistance;
            transform.position = Vector3.MoveTowards(transform.position, movePosition, moveSpeed * Time.deltaTime);
        }

        // --- Update local rotation angle ---
        float rotationDirection = invertLocalRotation ? -1f : 1f;
        // this potentially causes overflow
        currentZRotation += localZRotationSpeed * rotationDirection * Time.deltaTime;

        // --- Rotation Following ---
        if (toTarget.sqrMagnitude > 0.001f)
        {
            // Base look rotation
            Quaternion lookRotation = Quaternion.LookRotation(toTarget);

            // Continuous local Z rotation
            Quaternion localRotation = Quaternion.Euler(0f, 0f, currentZRotation);

            // Combine rotations
            Quaternion targetRotation = lookRotation * localRotation;

            // Smoothly slerp to the combined rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
