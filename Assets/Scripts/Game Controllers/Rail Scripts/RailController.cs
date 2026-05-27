using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

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
    public bool initializeOnAwake = false;

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

    // Cached table reference for stateless EvaluateAt lookups
    private SplineArcLengthTable _arcLengthTable;

    public virtual void Awake()
    {
        if (initializeOnAwake)
            InitializeSpline();
    }

    public void InitializeSpline()
    {
        if (!splineContainer)
        {
            Debug.LogWarning("No SplineContainer assigned to RailController");
            return;
        }

        splineLength = splineContainer.Spline.GetLength();

        var table = splineContainer.GetComponent<SplineArcLengthTable>();
        if (table == null)
        {
            Debug.LogError($"No SplineArcLengthTable on {splineContainer.name} — add one in the editor.");
            return;
        }

        _arcLengthTable = table;
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

    public void SnapshotSplineForInterpolation()
    {
        previousSplinePosition = currentSplinePosition;
        previousSplineForward = currentSplineForward;
        previousSplineUp = currentSplineUp;
        previousSplineRight = currentSplineRight;
        previousSplineRotation = currentSplineRotation;
    }

    public void CommitSplineToInterpolation()
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

    /// <summary>
    /// Evaluate the spline at an arc-length fraction (0..1), matching the same
    /// remapping used by EvaluateSpline() and TickSpline(). Use this wherever
    /// you previously called EvaluateAt() with a raw splineT value.
    ///
    /// Uses a stateless binary search so it is safe to call with non-monotonic
    /// or wrapping fractions (e.g. from projectile PlayerSpace tracking).
    /// </summary>
    public (Vector3 pos, Vector3 fwd, Vector3 up, Vector3 right) EvaluateAt(float arcFraction)
    {
        // Remap arc-length fraction → true curve parameter via binary search.
        // Binary search is used instead of the cursor because EvaluateAt is
        // called with arbitrary (potentially non-monotonic) fractions from
        // projectile code, while the cursor requires monotonically increasing input.
        float curveT = _arcLengthTable != null
            ? ArcFractionToCurveTBinarySearch(_arcLengthTable, arcFraction)
            : arcFraction;

        splineContainer.Spline.Evaluate(curveT,
            out Unity.Mathematics.float3 sp,
            out Unity.Mathematics.float3 st,
            out Unity.Mathematics.float3 su);

        var tr = splineContainer.transform;
        var fwd = tr.TransformDirection(((Vector3)st).normalized);
        var up = tr.TransformDirection(((Vector3)su).normalized);
        // Cross(up, fwd) matches the convention in EvaluateSpline()
        var right = Vector3.Cross(up, fwd).normalized;

        return (tr.TransformPoint((Vector3)sp), fwd, up, right);
    }

    /// <summary>
    /// Stateless binary search: arc-length fraction → spline curve parameter t.
    /// O(log N) but safe for any input order. Suitable for infrequent callers
    /// such as per-projectile updates.
    /// </summary>
    private static float ArcFractionToCurveTBinarySearch(SplineArcLengthTable table, float fraction)
    {
        if (!table.IsReady) return fraction;

        fraction = Mathf.Clamp01(fraction);
        float targetDist = fraction * table.TotalLength;

        int lo = 0;
        int hi = table.Resolution;           // last valid index == resolution

        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (table.GetArcLength(mid) < targetDist)
                lo = mid;
            else
                hi = mid;
        }

        float segLen = table.GetArcLength(hi) - table.GetArcLength(lo);
        float localT = segLen > 0f ? (targetDist - table.GetArcLength(lo)) / segLen : 0f;

        return Mathf.Lerp(lo / (float)table.Resolution, hi / (float)table.Resolution, localT);
    }
}