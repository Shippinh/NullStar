using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public abstract class RailController : MonoBehaviour
{
    [Header("Spline Settings")]
    public SplineContainer splineContainer;
    public bool loopSpline = true;

    [Header("Parameters")]
    public float maxSidewaysOffset = 200f;
    public float maxUpwardOffset = 200f;

    [Header("Speed")]
    public float MaxSpeed = 10f;

    [Header("Internal Values")]
    public Vector3 splineOffset;
    [Range(0f, 1f)] public float splineT = 0f;

    [field: Header("Spline Cache")]
    public Vector3 SplinePosition { get; protected set; }
    public Vector3 SplineForward { get; protected set; }
    public Vector3 SplineUp { get; protected set; }
    public Vector3 SplineRight { get; protected set; }
    public Quaternion SplineRotation { get; protected set; }

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

    // Replaces RailMover.Tick() — advances splineT, no transform writes
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

    public Vector3 GetNextSplinePosition()
    {
        return splineContainer.Spline.EvaluatePosition(splineT);
    }
}