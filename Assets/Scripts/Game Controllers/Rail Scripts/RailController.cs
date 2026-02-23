using System;
using UnityEngine;
using UnityEngine.Splines;


public abstract class RailController : MonoBehaviour
{
    [Header("Spline Settings")]
    public SplineContainer splineContainer; // assign in inspector, this defines the spline we follow
    public bool loopSpline = true;

    [Range(0f, 1f)] public float splineT = 0f; // CURRENT SPLINE PROGRESS

    [field: Header("Spline Cache")]
    public Vector3 SplinePosition { get; protected set; }
    public Vector3 SplineForward { get; protected set; }
    public Vector3 SplineUp { get; protected set; }
    public Vector3 SplineRight { get; protected set; }
    public Quaternion SplineRotation { get; protected set; }

    // Update is called once per frame
    void Update()
    {
        UpdateRail();
    }

    public abstract void UpdateRail();

    public Vector3 GetNextSplinePosition()
    {
        return splineContainer.Spline.EvaluatePosition(splineT);
    }
}

