using UnityEngine;

public class FlyingBossController : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Pivot Settings")]
    public Vector3 pivotOffset = new Vector3(0, 2f, 6f); // Default: in front and slightly above player

    [Header("Movement Settings")]
    public float lerpSpeed = 3f;
    public float smoothDampTime = 0.3f;
    public float springFrequency = 2f;
    public float springDamping = 0.7f;

    private Vector3 currentVelocity;     // For SmoothDamp
    private Vector3 springVelocity;      // For Spring
    private Vector3 targetPosition;      // Updated each frame

    public enum InterpolationMode { Lerp, SmoothDamp }
    public InterpolationMode interpolationMode = InterpolationMode.Lerp;

    void Update()
    {
        if (!player) return;

        // Step 1: Compute pivot world position based on offset
        targetPosition = player.TransformPoint(pivotOffset); // keeps pivot relative to player's movement and rotation

        // Step 2: Move toward the pivot based on selected interpolation
        switch (interpolationMode)
        {
            case InterpolationMode.Lerp:
                transform.position = Vector3.Lerp(transform.position, targetPosition, lerpSpeed * Time.deltaTime);
                break;

            case InterpolationMode.SmoothDamp:
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothDampTime);
                break;
        }
    }

    // Runtime method to set new pivot offset
    public void SetPivotOffset(Vector3 newOffset)
    {
        pivotOffset = newOffset;
    }

    // Runtime method to change interpolation style
    public void SetInterpolation(InterpolationMode mode)
    {
        interpolationMode = mode;
    }
}
