using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class EnemyRailController : RailController
{
    [Header("References")]
    public PlayerRailController playerRailControllerRef;
    public Rigidbody body;

    [Header("Spline Positioning")]
    public bool usePlayerRelativeOffset = false;
    [Range(-1f, 1f)] public float splineTOffset = 0.1f;

    [Header("Plane Offset")]
    public float rightOffset = 0f;
    public float upOffset = 0f;

    [Header("Worm Follow")]
    public bool useWormFollow = false;
    public EnemyRailController leaderRef;
    public float followDistance = 5f;

    // Only populated on leaders
    private List<Vector3> pathHistory = new List<Vector3>();
    private List<float> pathDistances = new List<float>(); // cumulative distances
    private float totalPathLength = 0f;

    public override void Awake()
    {
        base.Awake();

        if (!playerRailControllerRef)
            playerRailControllerRef = FindObjectOfType<PlayerRailController>();

        if (!body)
            body = GetComponent<Rigidbody>();

        if (body)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (!useWormFollow && !usePlayerRelativeOffset)
            splineT = splineTOffset;
    }

    void FixedUpdate()
    {
        if (useWormFollow && leaderRef != null)
        {
            TickSpline(Time.fixedDeltaTime);
            EvaluateSpline();

            body.MovePosition(SplinePosition);
            body.MoveRotation(SplineRotation);
        }
        else
        {
            UpdateSplineT();
            EvaluateSpline();

            Vector3 targetPosition = SplinePosition
                + SplineRight * rightOffset
                + SplineUp * upOffset;

            body.MovePosition(targetPosition);
            body.MoveRotation(SplineRotation);
        }
    }

    void UpdateSplineT()
    {
        if (usePlayerRelativeOffset)
            splineT = (playerRailControllerRef.splineT + splineTOffset) % 1f;
        else
            TickSpline(Time.fixedDeltaTime);
    }

    void RecordPath()
    {
        Vector3 current = SplinePosition;

        if (pathHistory.Count == 0)
        {
            pathHistory.Add(current);
            pathDistances.Add(0f);
            return;
        }

        float delta = Vector3.Distance(pathHistory[pathHistory.Count - 1], current);
        if (delta < 0.01f) return;

        totalPathLength += delta;
        pathHistory.Add(current);
        pathDistances.Add(totalPathLength);

        // Trim entries beyond max needed distance
        float maxNeeded = followDistance * 10f; // enough for a long chain
        while (pathHistory.Count > 2 && (totalPathLength - pathDistances[0]) > maxNeeded)
        {
            totalPathLength -= Vector3.Distance(pathHistory[0], pathHistory[1]);
            pathHistory.RemoveAt(0);
            pathDistances.RemoveAt(0);
        }
    }

    public bool TryGetPositionAtDistance(float distance, out Vector3 result)
    {
        result = Vector3.zero;
        if (pathHistory.Count < 2) return false;

        // Start from the newest point, walk back by distance
        float target = totalPathLength - distance;
        if (target < pathDistances[0]) return false;

        for (int i = pathHistory.Count - 1; i > 0; i--)
        {
            if (pathDistances[i - 1] <= target && pathDistances[i] >= target)
            {
                float segLength = pathDistances[i] - pathDistances[i - 1];
                float t = segLength > 0f ? (target - pathDistances[i - 1]) / segLength : 0f;
                result = Vector3.Lerp(pathHistory[i - 1], pathHistory[i], t);
                return true;
            }
        }

        return false;
    }
}