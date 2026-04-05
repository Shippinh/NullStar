using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure data asset. Describes one formation's shape, slot layout, and travel behavior.
/// Contains no runtime state — FormationController drives everything from this each frame.
/// Lane sequencing is handled externally by FormationLaneController.
/// </summary>
[CreateAssetMenu(fileName = "Formation Preset", menuName = "NullStar/Formation Preset")]
public class FormationDefinition : ScriptableObject
{
    // ── Shape ─────────────────────────────────────────────────────────────────

    [Header("Shape")]
    public FormationShape shape = FormationShape.Line;
    public OscillatedFloat slotSpacing = new() { value = 5f };
    public int gridColumns = 3;

    // ── Slots ─────────────────────────────────────────────────────────────────

    [Header("Slots")]
    public List<FormationSlot> slots = new();

    // ── Per-Slot Oscillation ──────────────────────────────────────────────────

    [Header("Per-Slot Oscillation")]
    [Tooltip("Oscillates each slot vertically, phase-staggered across the formation (Line shape).")]
    public OscillatedFloat perSlotUpOffset = new();

    [Tooltip("Oscillates each slot laterally, phase-staggered across the formation (Column shape).")]
    public OscillatedFloat perSlotRightOffset = new();

    [Tooltip("How much movement phase is staggered between consecutive slots. " +
             "0 = all in sync. 1 = one full cycle spread across all slots.")]
    public float slotPhaseSpread = 0f;

    // ── Travel Behavior ───────────────────────────────────────────────────────

    [Header("Travel Behavior")]
    [Tooltip("How each enemy moves around its slot. The formation shape stays steady.")]
    public FormationMovementType travelPattern = FormationMovementType.None;
    public float travelRadius = 3f;
    public OscillatedFloat travelFrequency = new() { value = 0.1f };

    // ── Per-Slot Randomization ────────────────────────────────────────────────

    [Header("Per-Slot Randomization")]
    [Tooltip("Gives each enemy a unique frequency and phase so slots are never perfectly synced.")]
    public bool randomizeTravelPerSlot = false;
    public float randomFrequencyVariance = 0.05f;
    public float randomPhaseVariance = 1f;

    // ── Pattern Inversion ─────────────────────────────────────────────────────

    [Header("Pattern Inversion")]
    [Tooltip("Flips the travel pattern for every slot globally.")]
    public bool invertAll = false;

    [Tooltip("Every other slot mirrors the pattern — creates mirrored pairs.")]
    public bool alternateInversion = false;

    // ── Orientation ───────────────────────────────────────────────────────────

    [Header("Orientation")]
    public FormationOrientation orientation = FormationOrientation.TowardsMoveDirection;

    // ── Shooting ──────────────────────────────────────────────────────────────

    [Header("Shooting")]
    public bool shootingEnabled = true;
}