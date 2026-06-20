/// <summary>
/// Controls when a lane automatically begins spawning/entering enemies.
/// </summary>
public enum LaneActivationType
{
    /// <summary>Enemies enter immediately when the lane initialises (original behaviour).</summary>
    OnInitialization,

    /// <summary>Lane waits for an explicit Activate() call or the OnActivationRequested UnityEvent.</summary>
    None,
}

/// <summary>
/// Per-slot: what kind of path the slot uses.
/// </summary>
public enum SlotPathMode
{
    /// <summary>Slot travels directly on the main lane spline (original behaviour).</summary>
    Lane,

    /// <summary>Slot follows an entry spline before handing off to the lane.</summary>
    Entry,

    /// <summary>Slot follows a passby spline and is repooled when it finishes.</summary>
    Passby,
}

/// <summary>
/// Determines which moment in the passby formation triggers the shooting event.
/// </summary>
public enum PassbyShootingActivation
{
    /// <summary>Fires when the first enemy in the formation reaches the shoot T.</summary>
    OnFirst,

    /// <summary>Each enemy fires individually when it reaches the shoot T.</summary>
    OnIndividual,

    /// <summary>
    /// Fires when the virtual midpoint of the formation passes the shoot T.
    /// For even counts the midpoint lies between the two centre slots.
    /// </summary>
    OnMiddle,

    None,
}

/// <summary>
/// What the shooting event actually does.
/// </summary>
public enum PassbyShootingType
{
    /// <summary>Fires one shot burst from the enemy's turrets.</summary>
    SingleShot,

    /// <summary>Enables the enemy's turrets for continuous fire until the passby ends.</summary>
    EnableTurret,
}

/// <summary>
/// How the passby spline is positioned in the world.
/// </summary>
public enum PassbySplineMode
{
    /// <summary>Spline is a fixed world-space asset (like the entry spline).</summary>
    WorldSpace,

    /// <summary>
    /// The spline's origin tracks the player's position along the player's rail at a
    /// configurable T offset, so the whole passby path slides with the player.
    /// </summary>
    PlayerRelative,
}

public enum GroupActivationFilter
{
    /// <summary>Activates every EnemyLane found on the children.</summary>
    All,
    /// <summary>Only activates lanes that use an entry spline (no passby spline set).</summary>
    EntryOnly,
    /// <summary>Only activates lanes that have a passby spline set.</summary>
    PassbyOnly,
}