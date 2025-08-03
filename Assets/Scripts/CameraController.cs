using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

public class CameraController : MonoBehaviour
{
    public CustomInputs inputConfig;
    public SpaceShooterController playerRef;

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

    float defaultFieldOfView;
    float pitchTilt = 0f;
    public float cameraTiltSpeed = 2f; // Speed of tilt
    public float maxHorizontalTiltAngle = 1.2f;   // Maximum tilt angle
    public float maxVerticalTiltAngle = 1.1f;
    public float dodgeTiltModifier = 2f;
    public float overboostTiltModifier = 1f;
    [Range(-30f, 30f)] public float extraOverboostFieldOfView = 5f;
    [Range(0f, 10f)] public float fieldOfViewChangeRate = 3f;
    [Range(0f, 300f)] public float fieldOfViewResetRate = 3f;


    private float inputX, inputY;
    private float yaw = 0f, pitch = 0f, roll = 0f;
    public GameObject target;

    // Hands for rotation
    public Transform leftHand;
    public Transform rightHand;

    public Transform left;
    public Transform right;

    // Store initial offsets for hands
    private Vector3 leftHandInitialOffset;
    private Vector3 rightHandInitialOffset;

    public float shakeMagnitude = 0.1f; // How strong the shake is
    public float shakeDuration = 0.5f; // How long the shake lasts
    private float shakeTime = 0f; // Timer to track the duration of the shake

    public float overheatShakeFadeRate = 0.005f; // Rate at which shake intensity increases
    public float currentOverheatShakeMagnitude = 0f; // Current magnitude of shake

    // New variables for dynamic shake magnitude based on overheat
    public float minShakeMagnitude = 0.1f;  // Minimum shake magnitude
    public float maxShakeMagnitude = 1f;    // Maximum shake magnitude


    void Start()
    {
        playerRef.OnOverboostActivation += HandleOverboostActivation;

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

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

        defaultFieldOfView = Camera.main.fieldOfView;
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
        TiltCameraBasedOnInput();
        AdjustOverboostFoV();

        if (playerRef.overboostMode && playerRef.overboostOverheatMode)
            AdjustShakeOverheat();
        else
            AdjustShake();
    }

    void HandleOverboostActivation()
    {
        TriggerShake();
        currentOverheatShakeMagnitude = 0f;
    }

    void AdjustShake()
    {
        Vector3 currentCameraPosition = transform.position;

        // Apply camera shake if it is active
        if (shakeTime > 0)
        {
            // Calculate a fade factor based on remaining shake time
            float fadeFactor = shakeTime / shakeDuration;

            // Apply the fade factor to the shake magnitude (so it decreases over time)
            float currentNormalShakeMagnitude = shakeMagnitude * fadeFactor;

            // Generate random shake offsets based on the faded magnitude
            float shakeX = Random.Range(-currentNormalShakeMagnitude, currentNormalShakeMagnitude);
            float shakeY = Random.Range(-currentNormalShakeMagnitude, currentNormalShakeMagnitude);

            // Apply the shake offsets to the camera position
            transform.position = currentCameraPosition + new Vector3(shakeX, shakeY, 0f);

            // Decrease the shake time
            shakeTime -= Time.deltaTime;
        }
        else
        {
            // Reset to the original camera position if no shake is active
            transform.position = currentCameraPosition;
        }
    }

    void AdjustShakeOverheat()
    {
        Vector3 currentCameraPosition = transform.position;

        // Map overboost overheat duration to shake magnitude range
        float targetMagnitude = Mathf.Lerp(minShakeMagnitude, maxShakeMagnitude, playerRef.overboostOverheatDurationCurrent);

        // Increase the shake magnitude until it reaches the target magnitude
        currentOverheatShakeMagnitude = Mathf.MoveTowards(currentOverheatShakeMagnitude, targetMagnitude, overheatShakeFadeRate * Time.deltaTime);

        // Generate random shake offsets based on the current shake magnitude
        float shakeX = Random.Range(-currentOverheatShakeMagnitude, currentOverheatShakeMagnitude);
        float shakeY = Random.Range(-currentOverheatShakeMagnitude, currentOverheatShakeMagnitude);

        // Apply the shake offsets to the camera position
        transform.position = currentCameraPosition + new Vector3(shakeX, shakeY, 0f);
    }

    private void ResetOverheatShake()
    {

    }

    private void TriggerShake()
    {
        shakeTime = shakeDuration; // Reset shake time
    }

    private (float x, float y) GetMouseInput()
    {
        return (Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void TiltCameraBasedOnInput()
    {
        float tiltModifier = 1f;

        bool movingLeft = Input.GetKey(inputConfig.MoveLeft);
        bool movingRight = Input.GetKey(inputConfig.MoveRight);
        bool movingUp = Input.GetKey(inputConfig.MoveUp);
        bool movingDown = Input.GetKey(inputConfig.MoveDown);

        if (playerRef.overboostInitiated || playerRef.boostInitiated)
            tiltModifier += overboostTiltModifier;

        if (playerRef.isDodging)
            tiltModifier += dodgeTiltModifier;

        // --- Horizontal Tilt (Roll) ---
        if (movingLeft)
        {
            if (playerRef.isDodging)
                roll = Mathf.Lerp(roll, maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * (cameraTiltSpeed * 2));
            else
                roll = Mathf.Lerp(roll, maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * cameraTiltSpeed);
        }
        else if (movingRight)
        {
            if (playerRef.isDodging)
                roll = Mathf.Lerp(roll, -maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * (cameraTiltSpeed * 2));
            else
                roll = Mathf.Lerp(roll, -maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * cameraTiltSpeed);
        }
        else
        {
            roll = Mathf.Lerp(roll, 0f, Time.deltaTime * cameraTiltSpeed);
        }

        // --- Vertical Tilt (pitchTilt) ONLY DURING BOOST ---
        if (playerRef.boostInitiated)
        {
            if (movingDown)
            {
                float target = maxVerticalTiltAngle * tiltModifier;
                pitchTilt = Mathf.Lerp(pitchTilt, target, Time.deltaTime * cameraTiltSpeed);
            }
            else if (movingUp)
            {
                float target = -maxVerticalTiltAngle * tiltModifier;
                pitchTilt = Mathf.Lerp(pitchTilt, target, Time.deltaTime * cameraTiltSpeed);
            }
            else
            {
                pitchTilt = Mathf.Lerp(pitchTilt, 0f, Time.deltaTime * cameraTiltSpeed);
            }
        }
        else
        {
            // Not boosting — reset pitchTilt smoothly
            pitchTilt = Mathf.Lerp(pitchTilt, 0f, Time.deltaTime * cameraTiltSpeed);
        }

        // Apply rotation using Quaternion
        float combinedPitch = pitch + pitchTilt; // Additive tilt
        Quaternion rotation = Quaternion.Euler(combinedPitch * cameraRotationSpeed, yaw * cameraRotationSpeed, roll);
        transform.rotation = rotation;

        // Rotate player's body (yaw only)
        target.transform.rotation = Quaternion.Euler(0, yaw * cameraRotationSpeed, 0);
    }


    private void AdjustOverboostFoV()
    {

        if (playerRef.overboostMode && playerRef.overboostInitiated)
        {
            Camera.main.fieldOfView = Mathf.MoveTowards(Camera.main.fieldOfView, defaultFieldOfView + extraOverboostFieldOfView, fieldOfViewChangeRate * Time.deltaTime);
        }
        else if (!playerRef.overboostMode && !playerRef.overboostInitiated)
        {
            Camera.main.fieldOfView = Mathf.Lerp(Camera.main.fieldOfView, defaultFieldOfView, fieldOfViewResetRate * Time.deltaTime);
        }
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
