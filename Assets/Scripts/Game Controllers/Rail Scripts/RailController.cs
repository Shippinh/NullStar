using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public abstract class RailController : MonoBehaviour
{
    //Spline Settings

    [Header("Spline Settings")]
    public SplineContainer splineContainer;
    public bool loopSpline = true;

    [Header("Plane Limits")]
    public float maxSidewaysOffset = 200f;
    public float maxUpwardOffset = 200f;

    [Header("Speed")]
    public float MaxSpeed = 10f;

    [Header("Internal Values")]
    public Vector3 splineOffset;
    [Range(0f, 1f)] public float splineT = 0f;

    //Raw Spline Cache (FixedUpdate accurate)

    [field: Header("Spline Cache — Raw (Physics)")]
    public Vector3 SplinePosition { get; protected set; }
    public Vector3 SplineForward { get; protected set; }
    public Vector3 SplineUp { get; protected set; }
    public Vector3 SplineRight { get; protected set; }
    public Quaternion SplineRotation { get; protected set; }

    //Interpolated Spline Cache (Update smooth)

    public Vector3 InterpolatedSplinePosition { get; protected set; }
    public Vector3 InterpolatedSplineForward { get; protected set; }
    public Vector3 InterpolatedSplineUp { get; protected set; }
    public Vector3 InterpolatedSplineRight { get; protected set; }
    public Quaternion InterpolatedSplineRotation { get; protected set; }

    //Previous fixed-step values for interpolation
    protected Vector3 previousSplinePosition;
    protected Vector3 previousSplineForward;
    protected Vector3 previousSplineUp;
    protected Vector3 previousSplineRight;
    protected Quaternion previousSplineRotation;

    //Current fixed-step values for interpolation
    protected Vector3 currentSplinePosition;
    protected Vector3 currentSplineForward;
    protected Vector3 currentSplineUp;
    protected Vector3 currentSplineRight;
    protected Quaternion currentSplineRotation;

    protected float splineLength;

    public virtual void Awake()
    {
        Initialize();
    }

    public virtual void Initialize()
    {
        if (!splineContainer)
            Debug.LogError("No SplineContainer assigned to RailController");

        splineLength = splineContainer.Spline.GetLength();
    }

    public void TickSpline(float dt)
    {
        if (splineLength <= 0f) return;
        splineT = (splineT + (MaxSpeed * dt) / splineLength) % 1f;
    }

    public virtual void EvaluateSpline()
    {
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

    protected void SnapshotSplineForInterpolation()
    {
        previousSplinePosition = currentSplinePosition;
        previousSplineForward = currentSplineForward;
        previousSplineUp = currentSplineUp;
        previousSplineRight = currentSplineRight;
        previousSplineRotation = currentSplineRotation;
    }

    protected void CommitSplineToInterpolation()
    {
        currentSplinePosition = SplinePosition;
        currentSplineForward = SplineForward;
        currentSplineUp = SplineUp;
        currentSplineRight = SplineRight;
        currentSplineRotation = SplineRotation;
    }

    protected void UpdateInterpolatedSpline()
    {
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

        InterpolatedSplinePosition = Vector3.Lerp(previousSplinePosition, currentSplinePosition, alpha);
        InterpolatedSplineForward = Vector3.Lerp(previousSplineForward, currentSplineForward, alpha).normalized;
        InterpolatedSplineUp = Vector3.Lerp(previousSplineUp, currentSplineUp, alpha).normalized;
        InterpolatedSplineRight = Vector3.Lerp(previousSplineRight, currentSplineRight, alpha).normalized;
        InterpolatedSplineRotation = Quaternion.Slerp(previousSplineRotation, currentSplineRotation, alpha);
    }

    protected void InitializeInterpolationBuffers()
    {
        currentSplinePosition = SplinePosition;
        currentSplineForward = SplineForward;
        currentSplineUp = SplineUp;
        currentSplineRight = SplineRight;
        currentSplineRotation = SplineRotation;

        previousSplinePosition = currentSplinePosition;
        previousSplineForward = currentSplineForward;
        previousSplineUp = currentSplineUp;
        previousSplineRight = currentSplineRight;
        previousSplineRotation = currentSplineRotation;

        InterpolatedSplinePosition = currentSplinePosition;
        InterpolatedSplineForward = currentSplineForward;
        InterpolatedSplineUp = currentSplineUp;
        InterpolatedSplineRight = currentSplineRight;
        InterpolatedSplineRotation = currentSplineRotation;
    }

    public Vector3 GetNextSplinePosition()
    {
        return splineContainer.Spline.EvaluatePosition(splineT);
    }
}