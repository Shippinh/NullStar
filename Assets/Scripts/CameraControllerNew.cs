using UnityEngine;
using static LerpFactorMethods;

public static class LerpFactorMethods
{
    public enum LerpFactor
    {
        None = 0,
        Linear = 1,
        SmoothDamp = 2,
        EaseInQuad = 3,
        EaseOutQuad = 4,
        EaseInOutCubic = 5,
        Elastic = 6
    }

    // Linear interpolation
    public static float Linear(float t)
    {
        return Mathf.Clamp01(t);
    }

    // Exponential / Smooth Damp (speed-based)
    public static float SmoothDamp(float speed, float deltaTime)
    {
        return 1f - Mathf.Exp(-speed * deltaTime);
    }

    // Quadratic Ease-in (slow start, fast end)
    public static float EaseInQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    // Quadratic Ease-out (fast start, slow end)
    public static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    // Cubic Ease-in-out (smooth start and end)
    public static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // Optional elastic / overshoot
    public static float Elastic(float t, float overshoot = 0.1f)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Sin(t * Mathf.PI * 0.5f) + t * overshoot;
    }

    // Universal getter helper for cleaner usage
    public static float GetLerpFactor(LerpFactor type, float t, float speed = 0f)
    {
        switch (type)
        {
            case LerpFactor.None:
            case LerpFactor.Linear: return Linear(t);
            case LerpFactor.SmoothDamp: return SmoothDamp(speed, t);
            case LerpFactor.EaseInQuad: return EaseInQuad(t);
            case LerpFactor.EaseOutQuad: return EaseOutQuad(t);
            case LerpFactor.EaseInOutCubic: return EaseInOutCubic(t);
            case LerpFactor.Elastic: return Elastic(t);
            default: return t;
        }
    }
}

public class CameraControllerNew : MonoBehaviour
{

    [Header("References")]
    public SpaceShooterController playerRef;            // Take inputs and target transform from here
    
    public Camera mainCameraRef;

    public Transform handPivotRef;

    public Transform leftHandRef;
    public Transform rightHandRef;

    [Header("Camera Constraints")]
    public bool canRotate = true;

    public float minRotationY = -84f;
    public float maxRotationY = 84f;

    [Header("Camera Settings")]
    [Range(1f, 5f)] public float cameraRotationSpeed = 3f;

    [Header("Hands Follow Settings")]
    [Range(1f, 32f)] public float handRotationSpeed = 15f;
    [Range(1f, 32f)] public float handPositionSpeed = 8f;
    [Range(1f, 10f)] public float handSwayPowerMultiplier = 3f;
    [Range(0f, 1f)] public float handLagPowerMultiplier = 0.01f;
    public LerpFactor handRotationLerpFactor = LerpFactor.None;
    public LerpFactor handPositionLerpFactor = LerpFactor.None;

    [Header("Camera Shake Settings")]
    public float shakeMagnitude = 0.05f;                            // How strong the shake is
    public float shakeDuration = 0.25f;                             // How long the shake lasts
    private float shakeTime = 0f;                                   // Timer to track the duration of the shake

    public float overheatShakeFadeRate = 0.002f;                    // Rate at which shake intensity increases
    public float currentOverheatShakeMagnitude = 0f;                // Current magnitude of shake

    // Dynamic shake magnitude based on overheat
    public float minShakeMagnitude = 0.0005f;                       // Minimum shake magnitude
    public float maxShakeMagnitude = 0.25f;                         // Maximum shake magnitude

    [Header("Camera Tilt Settings")]
    public float cameraTiltSpeed = 10f;                             // Speed of tilt
    public float maxHorizontalTiltAngle = 1.2f;                     // Maximum tilt angle
    public float maxVerticalTiltAngle = 0.325f;
    public float dodgeTiltModifier = 1f;
    public float overboostTiltModifier = 0.5f;

    [SerializeField] private float pitchTilt = 0f;

    [Header("Camera FoV Settings")]
    [Range(-30f, 60f)] public float extraOverboostFieldOfView = 30f;
    [Range(0f, 5f)] public float fieldOfViewChangeRate = 0.95f;
    [Range(0f, 25f)] public float fieldOfViewResetRate = 5f;

    [field: Header("Externals")]
    public bool LookingSideways { get; protected set; }
    [Range(0f, 1f)] public float sidewaysLookCriteria = 0.3f;
    public bool LookingForward { get; protected set; }
    [Range(0f, 1f)] public float forwardLookCriteria = 1f;

    [Header("Internals")]
    public float inputX;
    public float inputY;

    public float yaw = 0f, pitch = 0f, roll = 0f;

    public Quaternion desiredRotation;
    public Vector3 defaultOffset;
    public float defaultFieldOfView;

    public Vector3 defaultHandPivotPosition;

    public Quaternion defaultLeftHandRot;
    public Quaternion defaultRightHandRot;

    public Vector3 defaultLeftHandOffset;
    public Vector3 defaultRightHandOffset;

    // Start is called before the first frame update
    void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (!playerRef)
            playerRef = FindObjectOfType<SpaceShooterController>();

        if(!mainCameraRef)
            mainCameraRef = GetComponentInChildren<Camera>();

        playerRef.OnOverboostActivation += HandleOverboostActivation; // Sub to player event so we trigger a massive shake when the player activates the overboost (and maybe boost in the near future as well)

        if (handPivotRef)
            defaultHandPivotPosition = handPivotRef.localPosition;
        else
            Debug.LogWarning("Hand pivot reference is null, please set it in the inspector (this script will now crash the game :) )");

        if (leftHandRef)
        {
            defaultLeftHandRot = leftHandRef.rotation;
            defaultLeftHandOffset = leftHandRef.localPosition;
        }

        if (rightHandRef)
        {
            defaultRightHandRot = rightHandRef.rotation;
            defaultRightHandOffset = rightHandRef.localPosition;
        }

        defaultOffset = transform.position - playerRef.transform.position;
        defaultFieldOfView = mainCameraRef.fieldOfView;
    }

    void Update()
    {
        HandleMouseInput();
        UpdateCamDot();
    }

    private void FixedUpdate()
    {
        if (canRotate)
        {
            CalculateDesiredRotation();
            AdjustOverboostFoV();
        }
    }

    void LateUpdate()
    {
        

        ApplyRotation(); // Has to be called here to not cause any jitter

        // Apply any shake after all rotations so it doesn't interfere with rotations
        if (playerRef.overboostMode && playerRef.overboostOverheatMode)
            AdjustShakeOverheat();
        else
            AdjustShake();

        // Apply hands rotation after everything since they just follow
        RotateHandsSmoothly();
        UpdateHandsPositionLag();
    }

    private void HandleMouseInput()
    {
        inputX = Input.GetAxisRaw("Mouse X");
        inputY = Input.GetAxisRaw("Mouse Y");
    }

    private void CalculateDesiredRotation()
    {
        CalculateTiltAndPitchFromInput(); // Has to be referenced before CalculateDesiredRotation() otherwise we'll use pitchTilt and roll from the previous frame instead of the current one

        yaw += inputX;
        pitch -= inputY;

        pitch = Mathf.Clamp(pitch, minRotationY, maxRotationY);

        // Apply camera rotation
        desiredRotation = Quaternion.Euler(pitch + pitchTilt, yaw, roll);
    }

    // This is the single final desiredRotation application
    private void ApplyRotation()
    {
        mainCameraRef.transform.localRotation = Quaternion.Slerp(mainCameraRef.transform.localRotation, desiredRotation, cameraRotationSpeed);
    }

    private void CalculateTiltAndPitchFromInput()
    {
        float tiltModifier = 1f;        // Default 1
        float cameraTiltModifier = 1f;  // Default 1

        if (playerRef.overboostInitiated || playerRef.boostInitiated)
            tiltModifier += overboostTiltModifier;

        if (playerRef.isDodging)
        {
            cameraTiltModifier += 1f;
            tiltModifier += dodgeTiltModifier;
        }

        float camDot = Vector3.Dot(mainCameraRef.transform.forward, playerRef.transform.forward);
        bool boostTilt = camDot > 0.25f || camDot < -0.25f;

        // Handling if the player is in boost
        if (playerRef.boostInitiated)
        {
            if (playerRef.backwardInput != 0)
            {
                float target = maxVerticalTiltAngle * tiltModifier;
                pitchTilt = Mathf.Lerp(pitchTilt, target, Time.deltaTime * cameraTiltSpeed);
            }
            else if (playerRef.forwardInput != 0)
            {
                float target = -maxVerticalTiltAngle * tiltModifier;
                pitchTilt = Mathf.Lerp(pitchTilt, target, Time.deltaTime * cameraTiltSpeed);
            }
            else
                pitchTilt = Mathf.Lerp(pitchTilt, 0f, Time.deltaTime * cameraTiltSpeed);

            if (boostTilt)
            {
                if (playerRef.leftInput != 0)
                    roll = Mathf.Lerp(roll, maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * cameraTiltSpeed * cameraTiltModifier);
                else if (playerRef.rightInput != 0)
                    roll = Mathf.Lerp(roll, -maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * (cameraTiltSpeed * cameraTiltModifier));
                else
                    roll = Mathf.Lerp(roll, 0f, Time.deltaTime * cameraTiltSpeed);
            }
            else
                roll = Mathf.Lerp(roll, 0f, Time.deltaTime * cameraTiltSpeed);
        }
        // Handling if the player is not in boost
        else
        {
            if (playerRef.leftInput != 0)
                roll = Mathf.Lerp(roll, maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * cameraTiltSpeed * cameraTiltModifier);
            else if (playerRef.rightInput != 0)
                roll = Mathf.Lerp(roll, -maxHorizontalTiltAngle * tiltModifier, Time.deltaTime * (cameraTiltSpeed * cameraTiltModifier));
            else
                roll = Mathf.Lerp(roll, 0f, Time.deltaTime * cameraTiltSpeed);
            
            // Not boosting — reset pitchTilt smoothly
            if(pitchTilt != 0f)
                pitchTilt = Mathf.Lerp(pitchTilt, 0f, Time.deltaTime * cameraTiltSpeed);
        }
    }

    private void AdjustOverboostFoV()
    {

        if (playerRef.overboostMode && playerRef.overboostInitiated)
        {
            mainCameraRef.fieldOfView = Mathf.MoveTowards(mainCameraRef.fieldOfView, defaultFieldOfView + extraOverboostFieldOfView, fieldOfViewChangeRate * Time.deltaTime);
        }
        else if (!playerRef.overboostMode && !playerRef.overboostInitiated)
        {
            mainCameraRef.fieldOfView = Mathf.MoveTowards(mainCameraRef.fieldOfView, defaultFieldOfView, fieldOfViewResetRate * Time.deltaTime);
        }
    }

    void HandleOverboostActivation()
    {
        TriggerShake();
        currentOverheatShakeMagnitude = 0f;
    }

    // Call to trigger a normal shake (camera or hands)
    public void TriggerShake()
    {
        shakeTime = shakeDuration;
    }

    // Standard shake — can target any transform
    private void AdjustShake()
    {
        if (shakeTime > 0f)
        {
            float fadeFactor = shakeTime / shakeDuration;
            float currentMagnitude = shakeMagnitude * fadeFactor;

            // Random offsets
            float shakeX = Random.Range(-currentMagnitude, currentMagnitude);
            float shakeY = Random.Range(-currentMagnitude, currentMagnitude);

            // Apply shake to hands pivot only
            handPivotRef.localPosition = defaultHandPivotPosition + new Vector3(shakeX, shakeY, 0f);

            shakeTime -= Time.deltaTime;
        }
        else
        {
            // Reset to base position when shake ends
            handPivotRef.localPosition = defaultHandPivotPosition;
        }
    }

    // Overheat-style dynamic shake
    private void AdjustShakeOverheat()
    {
        // Calculate magnitude from overheat
        float targetMagnitude = Mathf.Lerp(minShakeMagnitude, maxShakeMagnitude, playerRef.overboostOverheatDurationCurrent);
        currentOverheatShakeMagnitude = Mathf.MoveTowards(currentOverheatShakeMagnitude, targetMagnitude, overheatShakeFadeRate * Time.deltaTime);

        // Random 2D offsets
        float shakeX = Random.Range(-currentOverheatShakeMagnitude, currentOverheatShakeMagnitude);
        float shakeY = Random.Range(-currentOverheatShakeMagnitude, currentOverheatShakeMagnitude);

        handPivotRef.localPosition = defaultHandPivotPosition + new Vector3(shakeX, shakeY, 0f);
    }

    private void RotateHandsSmoothly()
    {
        if (leftHandRef == null || rightHandRef == null) return;

        // Get camera rotation
        Quaternion cameraRot = mainCameraRef.transform.rotation;

        // Optional sway offset based on input
        float swayX = inputX * handSwayPowerMultiplier; // horizontal sway
        float swayY = inputY * handSwayPowerMultiplier; // vertical sway

        // Construct a rotation offset for sway
        Quaternion swayOffset = Quaternion.Euler(-swayY, swayX, 0f);

        // Target rotation = default rotation + camera rotation + sway
        Quaternion targetLeftRot = cameraRot * swayOffset * defaultLeftHandRot;
        Quaternion targetRightRot = cameraRot * swayOffset * defaultRightHandRot;

        float t = handRotationSpeed * Time.deltaTime; // normalized "time step"
        float lerpFactor = GetLerpFactor(handRotationLerpFactor, t, handRotationSpeed);

        leftHandRef.rotation = Quaternion.Slerp(leftHandRef.rotation, targetLeftRot, lerpFactor);
        rightHandRef.rotation = Quaternion.Slerp(rightHandRef.rotation, targetRightRot, lerpFactor);
    }

    private void UpdateHandsPositionLag()
    {
        if (leftHandRef == null || rightHandRef == null || handPivotRef == null) return;

        // Base position = pivot + camera rotation + default hand offsets
        Vector3 baseLeftTarget = handPivotRef.position + mainCameraRef.transform.rotation * defaultLeftHandOffset;
        Vector3 baseRightTarget = handPivotRef.position + mainCameraRef.transform.rotation * defaultRightHandOffset;

        // Calculate sway based on input
        Vector3 inputSway = new Vector3(inputX, -inputY, 0f) * handLagPowerMultiplier; // Y inverted so up = backward sway

        // Convert sway from camera space to world space
        Vector3 swayWorld = mainCameraRef.transform.rotation * inputSway;

        // Add shake offset
        Vector3 shakeOffset = handPivotRef.localPosition - defaultHandPivotPosition;

        // Final target positions
        Vector3 leftTarget = baseLeftTarget + swayWorld + shakeOffset;
        Vector3 rightTarget = baseRightTarget + swayWorld + shakeOffset;

        float t = handRotationSpeed * Time.deltaTime; // normalized "time step"
        float lerpFactor = GetLerpFactor(handPositionLerpFactor, t, handPositionSpeed);

        leftHandRef.position = Vector3.Lerp(leftHandRef.position, leftTarget, lerpFactor);
        rightHandRef.position = Vector3.Lerp(rightHandRef.position, rightTarget, lerpFactor);
    }

    private void UpdateCamDot()
    {
        float camDot = Vector3.Dot(mainCameraRef.transform.forward, playerRef.transform.forward);
        LookingForward = camDot < 0f;
        LookingSideways = camDot < 0.3f && camDot > -0.3f;
    }
}
