using UnityEngine;

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum FormationShape
{
    Line,       // horizontal row
    Column,     // vertical row
    Grid,       // rows × columns
    Circle,     // ring in the right/up plane
    V,          // V-shape spreading back
    Diamond,    // 4-point diamond
    Stagger,    // two offset rows (brick pattern)
}

public enum FormationMovementType
{
    None,
    Wiggle,     // sinusoidal side-to-side on right axis
    Bob,        // sinusoidal up/down on up axis
    Orbit,      // circle in the right/up plane
    Lissajous,  // figure-8 / complex curves via two independent oscillators
    Saw,        // sawtooth sweep on right axis — smooth travel, sharp reset
    Surge,      // sinusoidal push/pull along spline forward axis
    Spiral,     // orbit + surge — corkscrew path
    Snake,      // wiggle + surge — S-curve path
    Drift,      // slow random walk using Perlin noise
}

public enum FormationOrientation
{
    TowardsMoveDirection,
    TowardsPlayer,
}

// ── Slot ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Owns two things: which pool tag to spawn from, and which enemy currently
/// occupies this slot. Layout data lives in FormationController._layout[].
/// </summary>
[System.Serializable]
public class FormationSlot
{
    public string poolTag = "";

    [System.NonSerialized] public EnemyRailController enemy;
    [System.NonSerialized] public EnemyController enemyController;

    public bool IsOccupied => enemy != null;
}