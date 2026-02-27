using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RailMover : MonoBehaviour
{
    public SplineContainer Container;
    public float MaxSpeed = 10f;

    public float NormalizedTime { get; private set; }

    float m_SplineLength;
    SplinePath<Spline> m_SplinePath;

    void Awake()
    {
        Rebuild();
    }

    void Rebuild()
    {
        m_SplinePath = new SplinePath<Spline>(Container.Splines);
        m_SplineLength = m_SplinePath.GetLength();
    }

    public void Tick(float dt)
    {
        if (m_SplineLength <= 0f) return;

        NormalizedTime = (NormalizedTime + (MaxSpeed * dt) / m_SplineLength) % 1f;

        Container.Spline.Evaluate(
            NormalizedTime,
            out float3 pos,
            out _,
            out _
        );

        transform.position = (Vector3)pos;
        // no rotation — EvaluateSpline + CorrectOrientation owns that
    }
}