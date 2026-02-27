using System;
using UnityEngine;
using UnityEngine.Splines;


public abstract class RailController : MonoBehaviour
{
    [Header("References")]
    public RailMover railMoverRef;

    [Header("Parameters")]
    public float maxSidewaysOffset = 200f;
    public float maxUpwardOffset = 200f;

    [Header("Spline Settings")]
    public SplineContainer splineContainer; // assign in inspector, this defines the spline we follow
    public bool loopSpline = true;

    [Header("Internal Values")]
    public Vector2 splineOffset;
    [Range(0f, 1f)] public float splineT = 0f; // CURRENT SPLINE PROGRESS

    [field: Header("Spline Cache")]
    public Vector3 SplinePosition { get; protected set; }
    public Vector3 SplineForward { get; protected set; }
    public Vector3 SplineUp { get; protected set; }
    public Vector3 SplineRight { get; protected set; }
    public Quaternion SplineRotation { get; protected set; }

    public virtual void Awake()
    {
        Initialize();
    }

    public abstract void EvaluateSpline();

    public virtual void Initialize()
    {
        if (!railMoverRef)
        {
            railMoverRef = GetComponent<RailMover>();
        }

        if (!splineContainer && railMoverRef)
        {
            splineContainer = railMoverRef.Container;
        }
    }

    public Vector3 GetNextSplinePosition()
    {
        return splineContainer.Spline.EvaluatePosition(splineT);
    }
}

