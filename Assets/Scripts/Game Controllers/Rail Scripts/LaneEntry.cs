using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Drives a single enemy along a SplineContainer at a fixed speed.
/// Used for both entry paths (hands off to lane on completion) and
/// passby paths (enemy is repooled on completion).
/// </summary>
public class LaneEntryPlayer
{
    private readonly SplineContainer _spline;
    private readonly float _duration;
    private float _elapsed;

    public bool IsDone { get; private set; }
    public float Progress => Mathf.Clamp01(_elapsed / _duration);

    // Whether this player was constructed for a passby (repooled on done) vs entry (handoff).
    public readonly bool IsPassby;

    public LaneEntryPlayer(SplineContainer spline, float speed, bool isPassby = false)
    {
        _spline = spline;
        IsPassby = isPassby;
        float arc = spline != null ? spline.Spline.GetLength() : 1f;
        _duration = Mathf.Max(0.01f, arc / Mathf.Max(0.01f, speed));
        _elapsed = 0f;
    }

    public Vector3 Tick(float dt, Vector3 slotWorldPos, Quaternion slotRot,
                        out Quaternion rotation)
    {
        _elapsed += dt;
        float t = Mathf.Clamp01(_elapsed / _duration);
        if (t >= 1f) IsDone = true;

        if (_spline == null)
        {
            rotation = slotRot;
            return slotWorldPos;
        }

        _spline.Spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);
        Vector3 worldPos = _spline.transform.TransformPoint((Vector3)pos);
        Vector3 fwd = _spline.transform.TransformDirection(((Vector3)tangent).normalized);
        Vector3 upW = _spline.transform.TransformDirection(((Vector3)up).normalized);
        rotation = Quaternion.LookRotation(fwd, upW);
        return worldPos;
    }

    /// <summary>
    /// Returns the arc-length fraction (0..1) along the spline for a given world
    /// T offset.  Used by passby spline positioning in PlayerRelative mode.
    /// </summary>
    public float GetSplineProgress() => Mathf.Clamp01(_elapsed / _duration);
}