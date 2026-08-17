using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Drives a single enemy along a SplineContainer.
/// Progress advances each tick based on the *current* speed passed in,
/// so it stays in sync with lane speed changes (e.g. RailSpeedController fades)
/// instead of locking in the speed at construction time.
/// Used for both entry paths (hands off to lane on completion) and
/// passby paths (enemy is repooled on completion).
/// </summary>
public class LaneEntryPlayer
{
    private readonly SplineContainer _spline;
    private readonly float _arcLength;
    private float _t; // normalized progress 0..1, accumulated over time

    public bool IsDone { get; private set; }
    public float Progress => _t;

    // Whether this player was constructed for a passby (repooled on done) vs entry (handoff).
    public readonly bool IsPassby;

    public LaneEntryPlayer(SplineContainer spline, bool isPassby = false)
    {
        _spline = spline;
        IsPassby = isPassby;
        _arcLength = spline != null ? spline.Spline.GetLength() : 1f;
        _t = 0f;
    }

    /// <param name="dt">Delta time for this tick.</param>
    /// <param name="speed">Current lane speed (read live so mid-flight speed changes apply immediately).</param>
    public Vector3 Tick(float dt, float speed, Vector3 slotWorldPos, Quaternion slotRot,
                        out Quaternion rotation)
    {
        float safeArc = Mathf.Max(0.01f, _arcLength);
        _t = Mathf.Clamp01(_t + (speed * dt) / safeArc);
        if (_t >= 1f) IsDone = true;

        if (_spline == null)
        {
            rotation = slotRot;
            return slotWorldPos;
        }

        _spline.Spline.Evaluate(_t, out float3 pos, out float3 tangent, out float3 up);
        Vector3 worldPos = _spline.transform.TransformPoint((Vector3)pos);
        Vector3 fwd = _spline.transform.TransformDirection(((Vector3)tangent).normalized);
        Vector3 upW = _spline.transform.TransformDirection(((Vector3)up).normalized);
        rotation = Quaternion.LookRotation(fwd, upW);
        return worldPos;
    }
}