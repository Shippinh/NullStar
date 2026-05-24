using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

// Fire once at a specific T position on the rail.

[System.Serializable]
public abstract class RailEvent
{
    [Range(0f, 1f)] public float t;
    public bool enabled = true;
    public bool repeatable = false;
    public int layerIndex = 0;

    [System.NonSerialized] public bool hasFired;

    public abstract void Execute(PlayerRailController ctx);

    // Used by editor tools — override in each subclass
    public abstract string EditorLabel { get; }
    public abstract Color EditorColor { get; }
}

[System.Serializable]
public class ChangeSpeedEvent : RailEvent
{
    public float targetSpeed = 10f;
    public float targetSidewaysPlaneSpeed = 10f;
    public float targetUpwardPlaneSpeed = 10f;
    public float targetPlaneAcceleration = 35f;
    public float targetDodgeSpeed = 20f;

    public LerpFactorMethods.LerpFactor speedEasing = LerpFactorMethods.LerpFactor.None;
    public LerpFactorMethods.LerpFactor sidewaysPlaneSpeedEasing = LerpFactorMethods.LerpFactor.None;
    public LerpFactorMethods.LerpFactor upwardPlaneSpeedEasing = LerpFactorMethods.LerpFactor.None;
    public LerpFactorMethods.LerpFactor planeAccelerationEasing = LerpFactorMethods.LerpFactor.None;
    public LerpFactorMethods.LerpFactor dodgeSpeedEasing = LerpFactorMethods.LerpFactor.None;

    [Min(0f)] public float transitionDuration = 1f;

    public override void Execute(PlayerRailController ctx)
    {
        if (targetSpeed >= 0f)
            ctx.boostModeSpeedFade.SetSpeedOverTime(targetSpeed, transitionDuration, speedEasing);

        if (targetSidewaysPlaneSpeed >= 0f)
            ctx.boostModeSidewaySplineMaxSpeedFade.SetSpeedOverTime(targetSidewaysPlaneSpeed, transitionDuration, sidewaysPlaneSpeedEasing);

        if (targetUpwardPlaneSpeed >= 0f)
            ctx.boostModeUpwardSplineMaxSpeedFade.SetSpeedOverTime(targetUpwardPlaneSpeed, transitionDuration, upwardPlaneSpeedEasing);

        if (targetPlaneAcceleration >= 0f)
            ctx.boostModeAccelerationFade.SetSpeedOverTime(targetPlaneAcceleration, transitionDuration, planeAccelerationEasing);

        if (targetDodgeSpeed >= 0f)
            ctx.boostModeDodgeSpeedFade.SetSpeedOverTime(targetDodgeSpeed, transitionDuration, dodgeSpeedEasing);
    }

    public override string EditorLabel => $"Speed → {targetSpeed} | S:{targetSidewaysPlaneSpeed} U:{targetUpwardPlaneSpeed} A:{targetPlaneAcceleration} D:{targetDodgeSpeed} over {transitionDuration}s";

    public override Color EditorColor => new Color(0.3f, 0.8f, 1f);
}

[System.Serializable]
public class ScriptedFormationShotEvent : RailEvent
{
    public EnemyLane targetLane;
    public FormationPlane formation;        // drag the Array root here
    public string projectileTag;
    public float travelDuration = 2f;
    public int turretIndex = -1;
    public float steerStrength = 80f;
    public float snapDistance = 1.5f;

    public override void Execute(PlayerRailController ctx)
    {
        if (targetLane == null || formation.root == null) return;

        Vector3[] positions = formation.GetPositions();
        if (positions.Length == 0) return;

        // Shuffle
        for (int i = positions.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        // Collect live turrets
        var liveTurrets = new System.Collections.Generic.List<TurretBehavior>();
        foreach (var slot in targetLane.slots)
        {
            if (!slot.isAlive || slot.enemyController == null) continue;
            var turrets = slot.enemyController.GetComponentsInChildren<TurretBehavior>();
            if (turretIndex < 0)
                liveTurrets.AddRange(turrets);
            else if (turretIndex < turrets.Length)
                liveTurrets.Add(turrets[turretIndex]);
        }

        if (liveTurrets.Count == 0) return;

        int targetsPerTurret = Mathf.CeilToInt((float)positions.Length / liveTurrets.Count);
        for (int t = 0; t < liveTurrets.Count; t++)
        {
            int start = t * targetsPerTurret;
            int count = Mathf.Min(targetsPerTurret, positions.Length - start);
            if (count <= 0) break;

            Vector3[] slice = new Vector3[count];
            System.Array.Copy(positions, start, slice, 0, count);
            liveTurrets[t].FireFormationShot(slice, projectileTag, travelDuration, steerStrength, snapDistance);
        }
    }

    public override string EditorLabel =>
        $"Formation shot → {(formation.root != null ? formation.root.name : "—")} via {(targetLane != null ? targetLane.name : "—")}";
    public override Color EditorColor => new Color(1f, 0.5f, 0.2f);
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

[System.Serializable]
public class DetachPlayerFromRail : RailEvent
{
    public SpaceShooterController playerRef;
    public float transitionDuration = 0f;

    public override void Execute(PlayerRailController ctx) => playerRef?.InitiateBoostModeDetach(transitionDuration);

    public override string EditorLabel => $"Detach player over {transitionDuration}s";
    public override Color EditorColor => new Color(0.5f, 0.5f, 0.8f);
}

[System.Serializable]
public class ChangeOffsetPlaneEvent : RailEvent
{
    public float targetSideways = 20f;
    public float targetUpward = 20f;
    [Min(0f)] public float transitionDuration = 2f;

    public override void Execute(PlayerRailController ctx)
    {
        float sides = targetSideways >= 0f ? targetSideways : ctx.maxSidewaysOffset;
        float up = targetUpward >= 0f ? targetUpward : ctx.maxUpwardOffset;

        ctx.boostModeOffsetsFade.SetOffsetOverTime(sides, up, transitionDuration);
    }

    public override string EditorLabel => $"Offset plane → ({targetSideways:0}, {targetUpward:0}) over {transitionDuration}s";

    public override Color EditorColor => new Color(1f, 0.75f, 0.3f);
}

[System.Serializable]
public class LaneChangeEvent : RailEvent
{
    [Tooltip("The lane to target. If null, targets the lane on the same GameObject.")]
    public EnemyLane targetLane;

    [Tooltip("-1 = move all slots. 0+ = move only that slot index.")]
    public int slotIndex = -1;

    [Tooltip("New lateral position in the spline plane.")]
    public float targetRight = 0f;

    [Tooltip("New vertical position in the spline plane.")]
    public float targetUp = 0f;

    [Min(0f)]
    [Tooltip("Transition time in seconds. 0 = instant snap.")]
    public float transitionDuration = 1f;

    public override void Execute(PlayerRailController ctx)
    {
        if (targetLane == null) return;

        if (slotIndex < 0)
            targetLane.ChangeLane(targetRight, targetUp, transitionDuration);
        else
            targetLane.ChangeSlotLane(slotIndex, targetRight, targetUp, transitionDuration);
    }

    public override string EditorLabel =>
        slotIndex < 0
            ? $"Lane → ({targetRight:0}, {targetUp:0}) all over {transitionDuration}s"
            : $"Lane[{slotIndex}] → ({targetRight:0}, {targetUp:0}) over {transitionDuration}s";

    public override Color EditorColor => new Color(1f, 0.4f, 0.9f);
}

[System.Serializable]
public class LaneSpeedEvent : RailEvent
{
    public EnemyLane targetLane;
    public float targetSpeed = 10f;

    public override void Execute(PlayerRailController ctx)
        => targetLane?.SetSpeed(targetSpeed);

    public override string EditorLabel => $"Lane speed → {targetSpeed}";
    public override Color EditorColor => new Color(0.4f, 1f, 0.7f);
}

[System.Serializable]
public class ActivateLaneEvent : RailEvent
{
    [Tooltip("The lane to activate or trigger a passby on.")]
    public EnemyLane targetLane;

    // ── Passby overrides (ignored for non-passby lanes) ───────────────────────

    [Header("Passby Overrides")]
    [Tooltip("Set to true to override the lane's ShootT, ShootActivation, ShootType. False = use lane defaults.")]
    public bool enableOverride = false;

    [Tooltip("Set to override the lane's passbyShootT for this trigger.")]
    [Range(0, 1f)]
    public float overrideShootT = -1f;

    [Tooltip("Override shooting activation mode. Only applied when the lane has a passby spline.")]
    public PassbyShootingActivation shootActivation = PassbyShootingActivation.OnMiddle;

    [Tooltip("Override what firing actually does. Only applied when the lane has a passby spline.")]
    public PassbyShootingType shootType = PassbyShootingType.SingleShot;

    // ── RailEvent ─────────────────────────────────────────────────────────────

    public override void Execute(PlayerRailController ctx)
    {
        if (targetLane == null) return;

        bool isPassbyLane = targetLane.passbySpline != null;

        if (isPassbyLane)
        {
            // Apply overrides before triggering
            if (enableOverride)
            {
                targetLane.passbyShootT = overrideShootT;
                targetLane.passbyShootActivation = shootActivation;
                targetLane.passbyShootType = shootType;
            }

            targetLane.Activate();
        }
        else
        {
            targetLane.Activate();
        }
    }

    public override string EditorLabel
    {
        get
        {
            string laneName = targetLane != null ? targetLane.name : "—";
            bool isPassby = targetLane != null && targetLane.passbySpline != null;

            return isPassby
                ? $"Passby  {laneName}  [{targetLane.passbyShootActivation}/{targetLane.passbyShootType}]"
                : $"Activate  {laneName}";
        }
    }

    public override Color EditorColor =>
        // Passby lanes get mint green (matching the old TriggerPassbyEvent),
        // entry/lane lanes get lime (matching the old ActivateLaneEvent).
        (targetLane != null && targetLane.passbySpline != null)
            ? new Color(0.4f, 1f, 0.6f)   // mint  — passby
            : new Color(0.6f, 1f, 0.3f);  // lime  — entry / activate
}

[System.Serializable]
public class TriggerCameraShake : RailEvent
{
    [Header("Shake Parameters")]
    public float magnitude = 0.25f;
    public float duration = 0.25f;

    public override void Execute(PlayerRailController ctx) => ctx?.playerRef.cameraControllerRef.TriggerShake(duration, magnitude);
    public override string EditorLabel => $"Shake Player Camera | M:{magnitude} over {duration}s";
    public override Color EditorColor => new Color(0.2f, 0.2f, 0.7f);
}

// Span a region of the timeline; fire Enter/Exit/Update callbacks.
[System.Serializable]
public abstract class RailRangeEvent
{
    [Range(0f, 1f)] public float tStart;
    [Range(0f, 1f)] public float tEnd = 1f;
    public bool enabled = true;
    public int layerIndex = 0;

    [System.NonSerialized] public bool isActive;

    public abstract void OnEnter(PlayerRailController ctx);
    public abstract void OnExit(PlayerRailController ctx);
    public virtual void OnUpdate(PlayerRailController ctx, float t) { }

    public abstract string EditorLabel { get; }
    public abstract Color EditorColor { get; }
}