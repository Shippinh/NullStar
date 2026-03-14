using UnityEngine;

// Fire once at a specific T position on the rail.

[System.Serializable]
public abstract class RailEvent
{
    [Range(0f, 1f)] public float t;
    public bool enabled = true;
    public bool repeatable = false;

    [System.NonSerialized] public bool hasFired;

    public abstract void Execute(PlayerRailController ctx);

    // Used by editor tools — override in each subclass
    public abstract string EditorLabel { get; }
    public abstract Color EditorColor { get; }
}

[System.Serializable]
public class ChangeSpeedEvent : RailEvent
{
    public int targetSpeed = 10;
    [Min(0f)] public float transitionDuration = 1f;

    public override void Execute(PlayerRailController ctx) =>
        ctx.boostModeSpeedFade.SetSpeedOverTime(targetSpeed, transitionDuration);

    public override string EditorLabel => $"Speed → {targetSpeed} over {transitionDuration}s";
    public override Color EditorColor => new Color(0.3f, 0.8f, 1f);
}

[System.Serializable]
public class SetObjectActiveEvent : RailEvent
{
    public GameObject target;
    public bool active = true;

    public override void Execute(PlayerRailController ctx) => target?.SetActive(active);

    public override string EditorLabel => $"{(active ? "Enable" : "Disable")} {(target != null ? target.name : "—")}";
    public override Color EditorColor => new Color(0.3f, 1f, 0.5f);
}

// Span a region of the timeline; fire Enter/Exit/Update callbacks.
[System.Serializable]
public abstract class RailRangeEvent
{
    [Range(0f, 1f)] public float tStart;
    [Range(0f, 1f)] public float tEnd = 1f;
    public bool enabled = true;

    [System.NonSerialized] public bool isActive;

    public abstract void OnEnter(PlayerRailController ctx);
    public abstract void OnExit(PlayerRailController ctx);
    public virtual void OnUpdate(PlayerRailController ctx, float t) { }

    public abstract string EditorLabel { get; }
    public abstract Color EditorColor { get; }
}