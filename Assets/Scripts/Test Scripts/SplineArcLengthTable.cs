using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// Bakes an arc-length LUT once at startup.
/// - Single shared instance per SplineContainer (all RailControllers reuse it)
/// - O(1) amortized lookup via per-caller cached index instead of binary search
/// </summary>
public class SplineArcLengthTable : MonoBehaviour
{
    [Tooltip("More samples = smoother correction. 512 is accurate to ~0.2% for most splines.")]
    public int resolution = 2600;

    private float[] _arcLengths;  // cumulative distance at each sample
    private float _totalLength;

    public float TotalLength => _totalLength;
    public bool IsReady => _arcLengths != null;

    public void Bake(Spline spline)
    {
        _arcLengths = new float[resolution + 1];
        _arcLengths[0] = 0f;

        float3 prev = spline.EvaluatePosition(0f);
        for (int i = 1; i <= resolution; i++)
        {
            float3 curr = spline.EvaluatePosition(i / (float)resolution);
            _arcLengths[i] = _arcLengths[i - 1] + math.distance(prev, curr);
            prev = curr;
        }

        _totalLength = _arcLengths[resolution];
    }

    /// <summary>
    /// Allocate a cursor for one RailController. Each caller gets its own cursor
    /// so the index walk stays O(1) amortized — no binary search per frame.
    /// </summary>
    public ArcLengthCursor CreateCursor() => new ArcLengthCursor(this);

    // ── Cursor ────────────────────────────────────────────────────────────────

    public class ArcLengthCursor
    {
        private readonly SplineArcLengthTable _table;
        private int _cachedIndex = 0;

        internal ArcLengthCursor(SplineArcLengthTable table) => _table = table;

        /// <summary>
        /// Convert arc-length fraction (0..1) → spline curve-parameter t.
        /// Pass splineT in increasing order each frame for O(1) performance.
        /// Handles wrap-around for looping splines.
        /// </summary>
        public float Evaluate(float fraction)
        {
            if (!_table.IsReady) return fraction;

            fraction = Mathf.Clamp01(fraction);
            float targetDist = fraction * _table._totalLength;
            int res = _table.resolution;
            float[] arc = _table._arcLengths;

            // If t wrapped around (looping spline), reset the cached index
            if (_cachedIndex > 0 && arc[_cachedIndex] > targetDist + _table._totalLength * 0.5f)
                _cachedIndex = 0;

            // Walk forward from last known position — usually 0-2 steps per frame
            while (_cachedIndex < res - 1 && arc[_cachedIndex + 1] < targetDist)
                _cachedIndex++;

            int lo = _cachedIndex;
            int hi = Mathf.Min(lo + 1, res);

            float segLen = arc[hi] - arc[lo];
            float localT = segLen > 0f ? (targetDist - arc[lo]) / segLen : 0f;

            return Mathf.Lerp(lo / (float)res, hi / (float)res, localT);
        }

        public void Reset() => _cachedIndex = 0;
    }
}