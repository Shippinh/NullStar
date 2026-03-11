using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerRailController : RailController
{
    [Header("References")]
    public SpaceShooterController playerRef;
    public Rigidbody body;

    [Header("Internal Values")]
    public Vector3 velocity;

    [Header("Internal Speed Values")]
    public float defaultSplineSpeed;
    public FloatRef currentSplineSpeed;

    [Header("Speed Fade")]
    public RailSpeedController boostModeSpeedFade;

    public override void Awake()
    {
        base.Awake();

        if (!playerRef)
            playerRef = GetComponent<SpaceShooterController>();

        if (!body)
            body = playerRef.body;

        defaultSplineSpeed = MaxSpeed;
        currentSplineSpeed.value = defaultSplineSpeed;
        boostModeSpeedFade = new RailSpeedController(currentSplineSpeed, defaultSplineSpeed);

        // Initialize spline state so first frame has no pop
        EvaluateSpline();
    }

    public void Update()
    {
        if (!playerRef.boostMode) return;
        UpdateRailSpeed();
    }

    public void FixedUpdate()
    {
        if (!playerRef.boostMode) return;

        TickSpline(Time.fixedDeltaTime);
        EvaluateSpline();

        // Spline position + lateral offset owned by SpaceShooterController
        Vector3 targetPosition = SplinePosition
            + SplineRight * playerRef.currentRightOffset
            + SplineUp * playerRef.currentUpOffset;

        body.MovePosition(targetPosition);
        body.MoveRotation(SplineRotation);
    }

    public void UpdateRailSpeed()
    {
        MaxSpeed = currentSplineSpeed.value;
        boostModeSpeedFade.Update();
    }

    // Unchanged from before
    [System.Serializable]
    public class FloatRef
    {
        public float value;
    }

    public class RailSpeedController
    {
        FloatRef currentSpeedRef;
        float speedTarget;
        float speedSpeed;
        bool speedRunning;
        float defaultSpeed;

        public RailSpeedController(FloatRef currentSpeedPtr, float defaultSpeedPtr)
        {
            currentSpeedRef = currentSpeedPtr;
            defaultSpeed = defaultSpeedPtr;
        }

        public void SetSpeedOverTime(float target, float duration)
        {
            if (duration <= 0f)
            {
                currentSpeedRef.value = target;
                speedRunning = false;
                return;
            }

            speedTarget = target;
            speedSpeed = (target - currentSpeedRef.value) / duration;
            speedRunning = true;
        }

        public void ResetToDefault(float duration = 0f)
        {
            SetSpeedOverTime(defaultSpeed, duration);
        }

        public void Update()
        {
            if (!speedRunning) return;

            float delta = speedSpeed * Time.fixedDeltaTime;
            float remaining = speedTarget - currentSpeedRef.value;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                currentSpeedRef.value = speedTarget;
                speedRunning = false;
            }
            else
            {
                currentSpeedRef.value += delta;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 center = SplinePosition;
        Vector3 right = SplineRight * maxSidewaysOffset;
        Vector3 up = SplineUp * maxUpwardOffset;

        Vector3 p1 = center + right + up;
        Vector3 p2 = center + right - up;
        Vector3 p3 = center - right - up;
        Vector3 p4 = center - right + up;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(center, SplineForward * 2f);
    }
}