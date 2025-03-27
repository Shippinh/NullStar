using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 1, -10);
    [Range(0f, 5f)]
    public float cameraRotationSpeed = 3f;
    public float minRotationY = -84f;
    public float maxRotationY = 84f;
    public float attachRotationLimit = 80f;
    public bool canRotate = true;

    public float handRotationSpeed = 5f;  // Speed for hands' rotation (up/down)
    public float handResetSpeedVertical = 8f;  // Speed for resetting hands (up/down) to neutral rotation
    public float handResetSpeedHorizontal = 8f; // More aggressive resetting for horizontal rotation

    private float inputX, inputY;
    private float yaw = 0f, pitch = 0f;
    public GameObject target;

    // Hands for rotation
    public Transform leftHand;
    public Transform rightHand;

    public Transform left;
    public Transform right;

    // Store initial offsets for hands
    private Vector3 leftHandInitialOffset;
    private Vector3 rightHandInitialOffset;


    void Start()
    {
        (minRotationY, maxRotationY) = (minRotationY / cameraRotationSpeed, maxRotationY / cameraRotationSpeed);

        // Calculate initial offsets relative to the camera
        if (leftHand != null)
        {
            leftHandInitialOffset = leftHand.position - Camera.main.transform.position;
        }
        if (rightHand != null)
        {
            rightHandInitialOffset = rightHand.position - Camera.main.transform.position;
        }
    }

    void Update()
    {
        if (canRotate)
        {
            (inputX, inputY) = GetMouseInput();
        }
    }

    void LateUpdate()
    {
        AttachCamera();
        if (canRotate)
        {
            RotateCameraMouse(inputX, inputY);
            RotatePlayerHandsSmoothly();
            AttachHandsToCamera();
        }
    }

    private (float x, float y) GetMouseInput()
    {
        return (Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void RotateCameraMouse(float x, float y)
    {
        yaw += x;
        pitch -= y;

        pitch = Mathf.Clamp(pitch, minRotationY, maxRotationY);

        // Apply camera rotation
        transform.eulerAngles = new Vector3(pitch, yaw, 0f) * cameraRotationSpeed;

        // Apply rotation to the player's body (yaw only)
        target.transform.Rotate(Vector3.up * x * cameraRotationSpeed);
    }

    private void RotatePlayerHandsSmoothly()
    {
        if (leftHand == null || rightHand == null) return;

        // Create a rotation target based on vertical (inputY) and horizontal (inputX) mouse input
        // Adjust the direction in front of the camera and add vertical and horizontal offsets for hands
        Vector3 targetDirection = Camera.main.transform.forward + target.transform.up * inputY * 0.1f + target.transform.right * inputX * 0.1f;

        // Smooth rotation for hands (both up/down and left/right)
        Quaternion handRotation = Quaternion.LookRotation(targetDirection);

        // Blend the current rotation with the target rotation for hands, independent of frame rate
        float rotationLerpFactor = Mathf.Min(1, Time.deltaTime * handRotationSpeed);
        left.rotation = Quaternion.Slerp(left.rotation, handRotation, rotationLerpFactor);
        right.rotation = Quaternion.Slerp(right.rotation, handRotation, rotationLerpFactor);
    }

    /// <summary>
    /// Attaches hands to the camera based on its position and rotation without making them children of the camera.
    /// </summary>
    private void AttachHandsToCamera()
    {
        // Calculate the camera's position and rotation
        Vector3 cameraPosition = Camera.main.transform.position;
        Quaternion cameraRotation = Camera.main.transform.rotation;

        // Calculate the target positions based on the camera's rotation (orbital movement)
        Vector3 newLeftHandPosition = cameraPosition + cameraRotation * leftHandInitialOffset;
        Vector3 newRightHandPosition = cameraPosition + cameraRotation * rightHandInitialOffset;

        // If the camera is within the rotation limit, smoothly move hands to their calculated position
        // Orbiting behavior when inside the range
        float positionLerpFactor = Mathf.Min(1, Time.deltaTime * handRotationSpeed);
        leftHand.position = Vector3.Lerp(leftHand.position, newLeftHandPosition, positionLerpFactor);
        rightHand.position = Vector3.Lerp(rightHand.position, newRightHandPosition, positionLerpFactor);
    }

    /// <summary>
    /// Attaches Camera's transform at target's transform and adds some offset to it.
    /// </summary>
    private void AttachCamera()
    {
        transform.position = target.transform.position + offset;
    }

    private void OnDrawGizmos()
    {
        if (Camera.main != null)
        {
            // Get the world position of the viewport center
            Vector3 viewportCenter = new Vector3(0.5f, 0.5f, Camera.main.nearClipPlane);
            Vector3 worldCenter = Camera.main.ViewportToWorldPoint(viewportCenter);

            // Draw a line from the camera's position to the viewport center
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Camera.main.transform.position, worldCenter);

            // Optionally, draw an arrow indicating direction
            Vector3 direction = (worldCenter - Camera.main.transform.position).normalized;
            Gizmos.color = Color.red;

            Gizmos.color = Color.red;
        }
    }
}
