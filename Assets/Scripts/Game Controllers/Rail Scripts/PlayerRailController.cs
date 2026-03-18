using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerRailController : RailController
{

    [Header("References")]
    public SpaceShooterController playerRef;
    public Rigidbody body;

    [Header("Speed")]
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

        // Evaluate once so interpolation buffers start with valid data
        InitializeSplineValues();
    }

    public void InitializeSplineValues()
    {
        EvaluateSpline();
        InitializeInterpolationBuffers();
    }


    public void Update()
    {
        if (playerRef.playerState != PlayerState.BoostActive) return;

        UpdateRailSpeed(Time.deltaTime);
        UpdateInterpolatedSpline(); // smooth visual values every render frame
    }


    public void FixedUpdate()
    {
        if (playerRef.playerState != PlayerState.BoostActive) return;

        // Snapshot before advancing so interpolation has previous frame
        SnapshotSplineForInterpolation();

        TickSpline(Time.fixedDeltaTime);
        EvaluateSpline();

        // Commit new raw values to interpolation buffers
        CommitSplineToInterpolation();

        // Build target position from raw spline values (physics accurate)
        Vector3 targetPosition = SplinePosition
            + SplineRight * playerRef.currentRightOffset
            + SplineUp * playerRef.currentUpOffset;

        body.MovePosition(targetPosition);
        body.MoveRotation(SplineRotation);
    }

    public void UpdateRailSpeed(float dt)
    {
        MaxSpeed = currentSplineSpeed.value;
        boostModeSpeedFade.Update(dt);
    }

    public void GetOptimalResolution()
    {
        // How far you travel per physics tick
        float distancePerTick = currentSplineSpeed.value * Time.fixedDeltaTime;

        // Samples needed so each tick never skips a segment
        float optimalResolution = splineLength / distancePerTick;

        Debug.Log("Optimal resolution = " + optimalResolution);
    }


    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 center = InterpolatedSplinePosition;
        Quaternion rot = InterpolatedSplineRotation;

        Vector3 right = rot * Vector3.right * maxSidewaysOffset;
        Vector3 up = rot * Vector3.up * maxUpwardOffset;

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
        Gizmos.DrawRay(center, rot * Vector3.forward * 2f);
    }

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

        public void Update(float dt)
        {
            if (!speedRunning) return;

            float delta = speedSpeed * dt;
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
}