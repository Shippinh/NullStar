using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public enum RailState
{
    NotOnRail,              // When not following the rail, completely detached
    FollowingRail,          // When following the rail, completely attached
    Attaching,              // When in the process of attaching to the rail, called in between NotOnRail and FollowingRail
    Detaching               // When in the process of detaching from the rail, called in between FollowingRail and NotOnRail
}

// This class stores the state of an object on a spline
public abstract class RailController : MonoBehaviour
{
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
    [Range(0f, 1f)] public float splineT = 0f;  // arc-length fraction 0..1

    [field: Header("Spline Cache — Raw (Physics)")]
    public Vector3 SplinePosition { get; protected set; }
    public Vector3 SplineForward { get; protected set; }
    public Vector3 SplineUp { get; protected set; }
    public Vector3 SplineRight { get; protected set; }
    public Quaternion SplineRotation { get; protected set; }

    public Vector3 InterpolatedSplinePosition { get; protected set; }
    public Vector3 InterpolatedSplineForward { get; protected set; }
    public Vector3 InterpolatedSplineUp { get; protected set; }
    public Vector3 InterpolatedSplineRight { get; protected set; }
    public Quaternion InterpolatedSplineRotation { get; protected set; }

    protected Vector3 previousSplinePosition;
    protected Vector3 previousSplineForward;
    protected Vector3 previousSplineUp;
    protected Vector3 previousSplineRight;
    protected Quaternion previousSplineRotation;

    protected Vector3 currentSplinePosition;
    protected Vector3 currentSplineForward;
    protected Vector3 currentSplineUp;
    protected Vector3 currentSplineRight;
    protected Quaternion currentSplineRotation;

    public float splineLength;

    // Each RailController owns one cursor into the shared arc-length table
    private SplineArcLengthTable.ArcLengthCursor _cursor;

    public virtual void Awake()
    {
        Initialize();
    }

    public virtual void Initialize()
    {
        if (!splineContainer)
        {
            Debug.LogError("No SplineContainer assigned to RailController");
            return;
        }

        splineLength = splineContainer.Spline.GetLength();

        var table = splineContainer.GetComponent<SplineArcLengthTable>();
        if (table == null)
        {
            Debug.LogError($"No SplineArcLengthTable on {splineContainer.name} — add one in the editor.");
            return;
        }

        _cursor = table.CreateCursor();
    }

    public void TickSpline(float dt)
    {
        if (splineLength <= 0f) return;
        splineT = (splineT + (MaxSpeed * dt) / splineLength) % 1f;
    }

    public virtual void EvaluateSpline()
    {
        // Remap arc-length fraction → true curve parameter via cursor (O(1) amortized)
        float curveT = _cursor != null ? _cursor.Evaluate(splineT) : splineT;

        splineContainer.Spline.Evaluate(
            curveT,
            out float3 splinePos,
            out float3 splineTangent,
            out float3 splineUpVec
        );

        Vector3 forward = ((Vector3)splineTangent).normalized;
        Vector3 up = ((Vector3)splineUpVec).normalized;
        Vector3 right = Vector3.Cross(up, forward);

        if (right.sqrMagnitude < 0.001f)
            right = transform.right;
        right.Normalize();

        SplinePosition = splineContainer.transform.TransformPoint(splinePos);
        SplineForward = splineContainer.transform.TransformDirection(forward);
        SplineUp = splineContainer.transform.TransformDirection(up);
        SplineRight = splineContainer.transform.TransformDirection(right);
        SplineRotation = Quaternion.LookRotation(SplineForward, SplineUp);
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
        float curveT = _cursor != null ? _cursor.Evaluate(splineT) : splineT;
        return splineContainer.transform.TransformPoint(
            (Vector3)splineContainer.Spline.EvaluatePosition(curveT)
        );
    }
}