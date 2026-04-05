using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a sequence of FormationControllers arranged as a lane along the spline.
///
/// Each entry gets its own T position: entry[i].anchorT = anchorT + i * (slotSpacing / splineLength).
/// Mathf.Repeat handles wraparound on each entry independently, so the lane loops correctly.
///
/// Setup:
///   1. Create child GameObjects, each with a FormationController + FormationDefinition.
///   2. Set externalControl = true on each FormationController.
///   3. Assign them to the <see cref="formations"/> list.
///   4. Set <see cref="slotSpacing"/> to the world-space gap you want between anchors.
///
/// This runs at execution order -10 so anchorT is written before FormationController.FixedUpdate reads it.
/// </summary>
[DefaultExecutionOrder(-10)]
public class FormationLaneController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Formations")]
    [Tooltip("FormationControllers to drive. Each must have externalControl = true.")]
    public List<FormationController> formations = new();

    [Header("Spacing")]
    [Tooltip("World-space distance between consecutive formation anchors along the spline.")]
    public float slotSpacing = 15f;

    [Header("Movement")]
    public float anchorSpeed = 5f;

    [Tooltip("Normalized start T for the first formation (0–1).")]
    [Range(0f, 1f)]
    public float startT = 0f;

    // ── Internals ─────────────────────────────────────────────────────────────

    private float _anchorT;
    private float _splineLength;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Derive spline length from the first valid formation's container
        foreach (var f in formations)
        {
            if (f != null && f.splineContainer != null)
            {
                _splineLength = f.splineContainer.Spline.GetLength();
                break;
            }
        }

        if (_splineLength <= 0f)
            Debug.LogWarning("[FormationLaneController] Could not determine spline length. " +
                             "Make sure at least one formation has a SplineContainer assigned.");

        _anchorT = startT;

        ValidateFormations();
    }

    private void FixedUpdate()
    {
        if (_splineLength <= 0f || formations.Count == 0) return;

        // Advance the shared anchor T
        _anchorT = Mathf.Repeat(
            _anchorT + anchorSpeed * Time.fixedDeltaTime / _splineLength, 1f);

        float tSpacing = slotSpacing / _splineLength;

        for (int i = 0; i < formations.Count; i++)
        {
            if (formations[i] == null) continue;

            // Each formation gets its own independently wrapped T.
            // Mathf.Repeat here is what fixes the loop bug — each slot wraps
            // on its own rather than inheriting a stale parent value.
            formations[i].anchorT = Mathf.Repeat(_anchorT + i * tSpacing, 1f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Immediately repositions all formation anchors to a new T.</summary>
    public void TeleportTo(float t)
    {
        _anchorT = Mathf.Repeat(t, 1f);
    }

    /// <summary>Changes speed at runtime (e.g. for wave scripting).</summary>
    public void SetSpeed(float speed) => anchorSpeed = speed;

    /// <summary>Enables or disables shooting on all managed formations.</summary>
    public void SetShootingEnabled(bool enabled)
    {
        foreach (var f in formations)
            if (f != null) f.SetShootingEnabled(enabled);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private void ValidateFormations()
    {
        for (int i = 0; i < formations.Count; i++)
        {
            var f = formations[i];
            if (f == null)
            {
                Debug.LogWarning($"[FormationLaneController] formations[{i}] is null.");
                continue;
            }

            if (!f.externalControl)
            {
                Debug.LogWarning(
                    $"[FormationLaneController] formations[{i}] ({f.name}) has externalControl = false. " +
                    "It will self-advance and fight the lane controller. Set externalControl = true.");
            }
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (formations.Count == 0 || _splineLength <= 0f) return;

        float tSpacing = slotSpacing / _splineLength;

        for (int i = 0; i < formations.Count; i++)
        {
            var f = formations[i];
            if (f == null) continue;

            Color c = Color.HSVToRGB((float)i / formations.Count, 0.7f, 1f);
            Gizmos.color = c;

            // Draw a labeled sphere at each formation's anchor
            Vector3 pos = Application.isPlaying
                ? f.InterpolatedAnchorPosition
                : f.AnchorPosition;

            Gizmos.DrawWireSphere(pos, 0.6f);

            // Connect adjacent formations with a line
            if (i > 0 && formations[i - 1] != null)
            {
                Vector3 prev = Application.isPlaying
                    ? formations[i - 1].InterpolatedAnchorPosition
                    : formations[i - 1].AnchorPosition;
                Gizmos.DrawLine(prev, pos);
            }
        }
    }
}