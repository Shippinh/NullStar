using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;


public class RailController : MonoBehaviour
{
    [Header("References")]
    public SpaceShooterController playerRef;

    [Header("Spline Settings")]
    public SplineContainer splineContainer; // assign in inspector
    public bool loopSpline = true;

    [Range(0f, 1f)] public float splineT = 0f; // CURRENT SPLINE PROGRESS

    public float maxSidewaysOffset = 200f;
    public float maxUpwardOffset = 200f;

    public float defaultSplineSpeed = 250f;
    public FloatRef currentSplineSpeed;

    [Header("Speed Fade")]
    public RailSpeedController boostModeSpeedFade;

    [field: Header("Spline Cache")]
    public Vector3 SplinePosition { get; private set; }
    public Vector3 SplineForward { get; private set; }
    public Vector3 SplineUp { get; private set; }
    public Vector3 SplineRight { get; private set; }
    public Quaternion SplineRotation { get; private set; }

    // Start is called before the first frame update
    void Awake()
    {
        if (!playerRef)
            playerRef = GetComponent<SpaceShooterController>();

        currentSplineSpeed.value = defaultSplineSpeed;

        boostModeSpeedFade = new RailSpeedController(currentSplineSpeed, defaultSplineSpeed);
    }

    // Update is called once per frame
    void Update()
    {
        boostModeSpeedFade.Update();

        UpdateRail(currentSplineSpeed.value);
    }

    public void UpdateRail(float speed)
    {
        // Sample
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

        // Advance
        float splineLength = splineContainer.Spline.CalculateLength(transform.localToWorldMatrix);
        splineT += speed * Time.deltaTime / splineLength;

        if (loopSpline) splineT %= 1f;
        else splineT = Mathf.Clamp01(splineT);
    }

    public Vector3 GetNextSplinePosition()
    {
        return splineContainer.Spline.EvaluatePosition(splineT);
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

            float delta = speedSpeed * Time.deltaTime;
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

