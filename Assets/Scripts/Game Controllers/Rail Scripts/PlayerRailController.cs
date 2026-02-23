using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerRailController : RailController
{
    [Header("References")]
    public SpaceShooterController playerRef;
    public SplineAnimate playerSplineAnimateRef;

    [Header("Parameters")]
    public float maxSidewaysOffset = 200f;
    public float maxUpwardOffset = 200f;

    [Header("Internal Values")]
    public Vector2 splineOffset;
    public Vector3 velocity;

    [Header("Internal Speed Values")]
    public float defaultSplineSpeed;
    public FloatRef currentSplineSpeed;

    [Header("Speed Fade")]
    public RailSpeedController boostModeSpeedFade;

    // Start is called before the first frame update
    void Awake()
    {
        if (!playerRef)
            playerRef = GetComponent<SpaceShooterController>();

        defaultSplineSpeed = playerSplineAnimateRef.MaxSpeed;

        currentSplineSpeed.value = defaultSplineSpeed;

        boostModeSpeedFade = new RailSpeedController(currentSplineSpeed, defaultSplineSpeed);
    }

    // Update is called once per framea
    void FixedUpdate()
    {
        if (!playerRef.boostMode) return;

        UpdateRailSpeed();
        UpdateRail();

        ModifyOffset();
        CorrectOrientation();
    }

    public void UpdateRailSpeed()
    {
        playerSplineAnimateRef.MaxSpeed = currentSplineSpeed.value;

        boostModeSpeedFade.Update();
    }
    //
    public override void UpdateRail()
    {
        splineT = playerSplineAnimateRef.NormalizedTime;

        splineContainer.Spline.Evaluate(
            splineT,
            out float3 splinePos,
            out float3 splineTangent,
            out float3 splineUp
        );

        Vector3 forward = ((Vector3)splineTangent).normalized;
        Vector3 up = ((Vector3)splineUp).normalized;

        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.001f)
            right = transform.right;
        right.Normalize();

        SplinePosition = splinePos;
        SplineForward = forward;
        SplineUp = up;
        SplineRight = right;
        SplineRotation = Quaternion.LookRotation(forward, up);
    }

    public void ModifyOffset()
    {
        velocity = playerRef.body.velocity;

        // Remove spline-forward component (SplineAnimate owns this)
        Vector3 lateralVelocity =
            Vector3.ProjectOnPlane(velocity, SplineForward);

        // Convert lateral velocity into spline-local axes
        float rightDelta =
            Vector3.Dot(lateralVelocity, SplineRight) * Time.fixedDeltaTime;

        float upDelta =
            Vector3.Dot(lateralVelocity, SplineUp) * Time.fixedDeltaTime;

        // Accumulate offset (physics-based)
        splineOffset.x += rightDelta;
        splineOffset.y += upDelta;

        splineOffset.x = Mathf.Clamp(
                    splineOffset.x,
                    -maxSidewaysOffset,
                     maxSidewaysOffset
                );

        splineOffset.y = Mathf.Clamp(
            splineOffset.y,
            -maxUpwardOffset,
             maxUpwardOffset
        );

        transform.localPosition = new Vector3(
            splineOffset.x,
            splineOffset.y,
            0f
        );
    }

    public void CorrectOrientation()
    {
        Quaternion planeRotation = Quaternion.LookRotation(
                    SplineForward,
                    SplineUp
                );

        playerRef.playerRoot.rotation = planeRotation;
    }

    // dumb stupid hack because i don't want to remake RailSpeedController
    [Serializable]
    public class FloatRef
    {
        public float value;
    }

    public class RailSpeedController
    {
        FloatRef currentSpeedRef;

        float speedTarget;
        float speedSpeed;   // rate of change per second
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
            if (!speedRunning)
                return;

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

        // Optional: show plane normal
        Gizmos.color = Color.red;
        Gizmos.DrawRay(center, SplineForward * 2f);
    }
}
