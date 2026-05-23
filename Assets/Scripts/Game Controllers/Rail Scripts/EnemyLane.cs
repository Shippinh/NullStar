using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[DefaultExecutionOrder(-100)]
public class EnemyLane : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Spline")]
    public SplineContainer splineContainer;

    [Header("Movement")]
    public float speed = 10f;
    public float enemySpacing = 8f;
    [Range(0f, 1f)] public float startT = 0f;

    [Header("Orientation")]
    public FormationOrientation orientation = FormationOrientation.TowardsMoveDirection;
    public PlayerRailController player;

    [Header("Shooting")]
    public bool shootingEnabled = true;

    [Header("Activation")]
    public LaneActivationType activationType = LaneActivationType.OnInitialization;

    [Header("Entry Path")]
    public SplineContainer entrySpline;

    [Header("Passby")]
    public SplineContainer passbySpline;
    public PassbySplineMode passbySplineMode = PassbySplineMode.WorldSpace;
    [Range(-0.5f, 0.5f)] public float passbyPlayerTOffset = 0f;

    [Header("Passby Shooting")]
    [Range(0f, 1f)] public float passbyShootT = 0.5f;
    public PassbyShootingActivation passbyShootActivation = PassbyShootingActivation.OnMiddle;
    public PassbyShootingType passbyShootType = PassbyShootingType.SingleShot;

    [Header("Slots")]
    public List<LaneSlot> slots = new();

    [Header("Repool")]
    public float repoolDelay = 0f;          // 0 = disabled
    private float _repoolCountdown = -1f;

    // ──────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class LaneSlot
    {
        public string poolTag = "";
        public float rightOffset = 0f;
        public float upOffset = 0f;

        public FormationMovementType travelPattern = FormationMovementType.None;
        public float travelRadius = 2f;
        public float travelFrequency = 0.5f;

        public SlotPathMode pathMode = SlotPathMode.Entry;

        // ── Runtime state ──────────────────────────────────────────────────
        [System.NonSerialized] public EnemyRailController enemy;
        [System.NonSerialized] public EnemyController enemyController;
        [System.NonSerialized] public LaneEntryPlayer entryPlayer;
        [System.NonSerialized] public SplineArcLengthTable.ArcLengthCursor cursor;
        [System.NonSerialized] public bool isAlive;
        [System.NonSerialized] public bool handedOff;
        [System.NonSerialized] public string activePoolTag;
        [System.NonSerialized] public float localAnchorT;
        [System.NonSerialized] public float slotT;
        [System.NonSerialized] public bool passbyShootFired;

        public bool IsOnPath => entryPlayer != null && !entryPlayer.IsDone;
        public bool IsPassby => pathMode == SlotPathMode.Passby;
    }

    // ── Runtime fields ────────────────────────────────────────────────────────

    [Range(0f, 1f)] public float anchorT = 0f;

    private float _splineLength;
    private SplineArcLengthTable _arcTable;
    private int _nextSpawnIndex;
    private float _nextSpawnCountdown;
    private bool _activated;
    private bool _passbyShootFired;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (!splineContainer) { Debug.LogError("[EnemyLane] No SplineContainer.", this); return; }
        _splineLength = splineContainer.Spline.GetLength();
        _arcTable = splineContainer.GetComponent<SplineArcLengthTable>();
        if (_arcTable == null) { Debug.LogError("[EnemyLane] Missing SplineArcLengthTable.", this); return; }
        anchorT = startT;
        if (!player) player = FindObjectOfType<PlayerRailController>();
    }

    private void Start()
    {
        if (activationType == LaneActivationType.OnInitialization)
            Activate();
    }

    private void FixedUpdate()
    {
        if (!_activated) return;

        if (_repoolCountdown > 0f)
        {
            _repoolCountdown -= Time.fixedDeltaTime;
            if (_repoolCountdown <= 0f)
            {
                _repoolCountdown = -1f;
                RepoolAll();
                return;
            }
        }

        if (_splineLength <= 0f || slots.Count == 0) return;

        float dt = Time.fixedDeltaTime;
        float tDelta = speed * dt / _splineLength;
        float tSpacing = enemySpacing / _splineLength;

        anchorT = Mathf.Repeat(anchorT + tDelta, 1f);

        // Staggered spawning — same logic for entry and passby
        if (_nextSpawnIndex < slots.Count)
        {
            _nextSpawnCountdown -= dt;
            if (_nextSpawnCountdown <= 0f)
            {
                SpawnSlot(_nextSpawnIndex++);
                bool hasPath = entrySpline != null || passbySpline != null;
                _nextSpawnCountdown = hasPath ? enemySpacing / Mathf.Max(0.01f, speed) : 0f;
            }
        }

        if (passbySpline != null && passbySplineMode == PassbySplineMode.PlayerRelative && player != null)
            SyncPassbySplineToPlayer();

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (!slot.isAlive || slot.enemy == null) continue;

            if (slot.IsOnPath)
            {
                TickPathSlot(slot, i, dt, tSpacing);
                continue;
            }

            if (!slot.handedOff) continue;

            TickLaneSlot(slot, i, dt, tDelta, tSpacing);
        }

        CheckPassbyShoot();
    }

    // ── Activation ────────────────────────────────────────────────────────────

    public void Activate()
    {
        if (_activated) return;
        _activated = true;
        _nextSpawnIndex = 0;
        _nextSpawnCountdown = 0f;
        _passbyShootFired = false;
        foreach (var s in slots) s.passbyShootFired = false;

        if (repoolDelay > 0f)
            _repoolCountdown = repoolDelay;
    }

    // ── Slot ticking ──────────────────────────────────────────────────────────

    /// Unified tick for both entry and passby — same movement, different end behaviour.
    private void TickPathSlot(LaneSlot slot, int i, float dt, float tSpacing)
    {
        Vector3 pos = slot.entryPlayer.Tick(dt, Vector3.zero, Quaternion.identity, out Quaternion rot);

        // Per-slot individual shoot check (passby only)
        if (slot.IsPassby && passbyShootActivation == PassbyShootingActivation.OnIndividual)
        {
            if (!slot.passbyShootFired && slot.entryPlayer.Progress >= passbyShootT)
            {
                slot.passbyShootFired = true;
                FireShoot(slot);
            }
        }

        if (slot.entryPlayer.IsDone)
        {
            if (slot.IsPassby)
            {
                ReturnSlotToPool(slot);
            }
            else
            {
                // Hand off to lane
                slot.entryPlayer = null;
                slot.handedOff = true;
                slot.cursor?.Reset();
                slot.localAnchorT = slots[0].localAnchorT;
                slot.slotT = Mathf.Repeat(slot.localAnchorT - i * (enemySpacing / _splineLength), 1f);
                slot.enemy.SyncSplineT(slot.slotT);

                EvaluateSplineAt(slot.slotT, slot.cursor, out Vector3 ap, out Vector3 r, out Vector3 u, out Vector3 f);
                Vector3 handoffPos = ap + r * slot.enemy.LaneRight + u * slot.enemy.LaneUp;
                slot.enemy.SetFormationTarget(handoffPos, ComputeRotation(handoffPos, f, u), this);
                slot.enemy.TickPhysics(dt);
            }
            return;
        }

        slot.enemy.SetFormationTarget(pos, rot, this);
        slot.enemy.TickPhysics(dt);
    }

    private void TickLaneSlot(LaneSlot slot, int i, float dt, float tDelta, float tSpacing)
    {
        if (slot.enemy.OwningLane != this)
        {
            Debug.LogWarning($"[EnemyLane:{name}] Slot {i} owned by different lane — dropping.");
            slot.enemy = null;
            slot.enemyController = null;
            slot.isAlive = false;
            return;
        }

        slot.localAnchorT = Mathf.Repeat(slot.localAnchorT + tDelta, 1f);
        slot.slotT = Mathf.Repeat(slot.localAnchorT - i * (enemySpacing / _splineLength), 1f);
        slot.enemy.SyncSplineT(slot.slotT);

        EvaluateSplineAt(slot.slotT, slot.cursor, out Vector3 pos, out Vector3 right, out Vector3 up, out Vector3 fwd);
        Vector3 slotPos = pos + right * slot.enemy.LaneRight + up * slot.enemy.LaneUp;
        slot.enemy.SetFormationTarget(slotPos, ComputeRotation(slotPos, fwd, up), this);
        slot.enemy.TickPhysics(dt);
    }

    // ── Passby shooting ───────────────────────────────────────────────────────

    private void CheckPassbyShoot()
    {
        if (passbyShootActivation == PassbyShootingActivation.None) return;
        if (passbyShootActivation == PassbyShootingActivation.OnIndividual) return; // handled per-slot

        if (_passbyShootFired) return;

        float progress = 0f;
        int count = 0;
        LaneSlot first = null;

        foreach (var s in slots)
        {
            if (!s.isAlive || !s.IsPassby || s.entryPlayer == null) continue;
            if (first == null) first = s;
            progress += s.entryPlayer.Progress;
            count++;
        }

        if (count == 0) return;

        bool shouldFire = passbyShootActivation switch
        {
            PassbyShootingActivation.OnFirst => first != null && first.entryPlayer.Progress >= passbyShootT,
            PassbyShootingActivation.OnMiddle => (progress / count) >= passbyShootT,
            _ => false
        };

        if (!shouldFire) return;

        _passbyShootFired = true;
        foreach (var s in slots)
        {
            if (s.isAlive && s.IsPassby) FireShoot(s);
        }
    }

    private void FireShoot(LaneSlot slot)
    {
        if (slot.enemyController == null) return;
        foreach (var turret in slot.enemyController.GetComponentsInChildren<TurretBehavior>())
        {
            switch (passbyShootType)
            {
                case PassbyShootingType.SingleShot: turret.FireSingleShot(); break;
                case PassbyShootingType.EnableTurret: turret.SyncState(true); break;
            }
        }
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

    private void ReturnSlotToPool(LaneSlot slot)
    {
        if (slot.enemy != null)
        {
            slot.enemy.ClearFormationTarget();
            slot.enemy.gameObject.SetActive(false);
            GameObject obj = slot.enemyController != null
                ? slot.enemyController.gameObject
                : slot.enemy.gameObject;
            ObjectPool.Instance.ReturnToPool(obj, slot.activePoolTag);
        }

        slot.enemy = null;
        slot.enemyController = null;
        slot.isAlive = false;
        slot.entryPlayer = null;
        slot.handedOff = false;
        slot.cursor = null;
        slot.passbyShootFired = false;
        slot.activePoolTag = null;
    }

    private void RepoolAll()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].isAlive)
                ReturnSlotToPool(slots[i]);
        }
        _activated = false;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    private void SpawnSlot(int index)
    {
        var slot = slots[index];
        if (string.IsNullOrEmpty(slot.poolTag)) return;

        float tSpacing = enemySpacing / _splineLength;
        slot.handedOff = false;
        slot.passbyShootFired = false;
        slot.cursor = _arcTable?.CreateCursor();

        // Pick which path spline to use
        SplineContainer pathSpline = slot.IsPassby ? passbySpline : entrySpline;
        bool hasPath = pathSpline != null;

        Vector3 spawnPos;
        Quaternion spawnRot;

        if (hasPath)
        {
            pathSpline.Spline.Evaluate(0f, out float3 ep, out float3 et, out float3 eu);
            spawnPos = pathSpline.transform.TransformPoint((Vector3)ep);
            spawnRot = Quaternion.LookRotation(
                pathSpline.transform.TransformDirection(((Vector3)et).normalized),
                pathSpline.transform.TransformDirection(((Vector3)eu).normalized));

            slot.localAnchorT = startT;
            slot.slotT = Mathf.Repeat(startT - index * tSpacing, 1f);
        }
        else
        {
            // Lane-only: place directly on main spline
            slot.localAnchorT = anchorT;
            slot.slotT = Mathf.Repeat(anchorT - index * tSpacing, 1f);
            slot.handedOff = true;

            EvaluateSplineAt(slot.slotT, slot.cursor, out Vector3 ap, out Vector3 r, out Vector3 u, out Vector3 f);
            spawnPos = ap + r * slot.rightOffset + u * slot.upOffset;
            spawnRot = Quaternion.LookRotation(f, u);
        }

        GameObject obj = ObjectPool.Instance.GetPooledObject(slot.poolTag, spawnPos, spawnRot, shouldBeRequeued: false);
        if (obj == null) return;

        var rail = obj.GetComponentInChildren<EnemyRailController>();
        if (rail == null) return;

        var ai = obj.GetComponentInChildren<EnemyAIComponent>();
        if (ai != null) ai.enabled = false;

        var controller = obj.GetComponent<EnemyController>();
        controller?.HandleRailAttach(speed);

        rail.splineContainer = splineContainer;
        rail.InitializeSpline();
        rail.SyncSplineT(slot.slotT);
        rail.InitializeEnemy();
        rail.body.isKinematic = true;
        rail.body.interpolation = RigidbodyInterpolation.Interpolate;
        rail.SetLane(slot.rightOffset, slot.upOffset, duration: 0f);

        if (slot.travelPattern != FormationMovementType.None)
            rail.SetTravelBehavior(slot.travelPattern, slot.travelRadius,
                slot.travelFrequency, phase: UnityEngine.Random.Range(0f, Mathf.PI * 2f));

        slot.entryPlayer = hasPath
            ? new LaneEntryPlayer(pathSpline, speed, isPassby: slot.IsPassby)
            : null;

        rail.SetFormationTarget(spawnPos, spawnRot, this);

        slot.enemy = rail;
        slot.enemyController = controller;
        slot.isAlive = true;
        slot.activePoolTag = slot.poolTag;

        SyncShooting(slot);
    }

    // ── Spline helpers ────────────────────────────────────────────────────────

    private void EvaluateSplineAt(float t, SplineArcLengthTable.ArcLengthCursor cursor,
        out Vector3 pos, out Vector3 right, out Vector3 up, out Vector3 fwd)
    {
        float curveT = cursor != null ? cursor.Evaluate(t) : t;
        splineContainer.Spline.Evaluate(curveT, out float3 splinePos, out float3 tangent, out float3 splineUp);
        fwd = splineContainer.transform.TransformDirection(((Vector3)tangent).normalized);
        up = splineContainer.transform.TransformDirection(((Vector3)splineUp).normalized);
        right = Vector3.Cross(up, fwd).normalized;
        pos = splineContainer.transform.TransformPoint((Vector3)splinePos);
    }

    private Quaternion ComputeRotation(Vector3 targetPos, Vector3 fwd, Vector3 up)
    {
        if (orientation == FormationOrientation.TowardsPlayer && player != null)
        {
            Vector3 dir = (player.transform.position - targetPos).normalized;
            if (dir.sqrMagnitude > 0.001f) return Quaternion.LookRotation(dir, up);
        }
        return Quaternion.LookRotation(fwd, up);
    }

    private void SyncPassbySplineToPlayer()
    {
        float playerT = Mathf.Repeat(player.splineT + passbyPlayerTOffset, 1f);
        if (player.splineContainer == null) return;
        player.splineContainer.Spline.Evaluate(playerT, out float3 p, out float3 tang, out float3 up);
        passbySpline.transform.position = player.splineContainer.transform.TransformPoint((Vector3)p);
        Vector3 fwd = player.splineContainer.transform.TransformDirection(((Vector3)tang).normalized);
        Vector3 upW = player.splineContainer.transform.TransformDirection(((Vector3)up).normalized);
        if (fwd.sqrMagnitude > 0.001f)
            passbySpline.transform.rotation = Quaternion.LookRotation(fwd, upW);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void HandleSlotDeath(EnemyRailController enemy)
    {
        foreach (var slot in slots)
        {
            if (slot.enemy != enemy) continue;
            slot.enemy = null;
            slot.enemyController = null;
            slot.isAlive = false;
            slot.entryPlayer = null;
            slot.handedOff = false;
            slot.cursor = null;
            break;
        }
    }

    public void ChangeLane(float targetRight, float targetUp, float duration)
    {
        foreach (var slot in slots)
        {
            slot.rightOffset = targetRight;
            slot.upOffset = targetUp;
            slot.enemy?.SetLane(targetRight, targetUp, duration);
        }
    }

    public void ChangeSlotLane(int slotIndex, float targetRight, float targetUp, float duration)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count) return;
        var slot = slots[slotIndex];
        slot.rightOffset = targetRight;
        slot.upOffset = targetUp;
        slot.enemy?.SetLane(targetRight, targetUp, duration);
    }

    public void SetSpeed(float newSpeed) => speed = newSpeed;

    public void SetShootingEnabled(bool enabled)
    {
        shootingEnabled = enabled;
        foreach (var slot in slots) SyncShooting(slot);
    }

    public void TeleportTo(float t)
    {
        anchorT = Mathf.Repeat(t, 1f);
        float tSpacing = enemySpacing / _splineLength;
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].localAnchorT = anchorT;
            slots[i].slotT = Mathf.Repeat(anchorT - i * tSpacing, 1f);
            slots[i].cursor?.Reset();
            slots[i].enemy?.SyncSplineT(slots[i].slotT);
        }
    }

    private void SyncShooting(LaneSlot slot)
    {
        if (slot.enemyController == null) return;
        foreach (var turret in slot.enemyController.GetComponentsInChildren<TurretBehavior>())
            turret.SyncState(shootingEnabled);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!splineContainer || slots.Count == 0) return;
        float len = splineContainer.Spline.GetLength();
        if (len <= 0f) return;

        float tSpacing = enemySpacing / len;
        var gizmoCursor = Application.isPlaying && _arcTable != null ? _arcTable.CreateCursor() : null;
        var positions = new Vector3[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            float arcT = Mathf.Repeat(startT - i * tSpacing, 1f);
            float curveT = gizmoCursor != null ? gizmoCursor.Evaluate(arcT) : arcT;
            splineContainer.Spline.Evaluate(curveT, out float3 p, out float3 tang, out float3 upVec);
            Vector3 fwd = splineContainer.transform.TransformDirection(((Vector3)tang).normalized);
            Vector3 upW = splineContainer.transform.TransformDirection(((Vector3)upVec).normalized);
            Vector3 rW = Vector3.Cross(upW, fwd).normalized;
            positions[i] = splineContainer.transform.TransformPoint((Vector3)p)
                         + rW * slots[i].rightOffset + upW * slots[i].upOffset;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            Gizmos.color = Color.HSVToRGB((float)i / Mathf.Max(1, slots.Count), 0.7f, 1f);
            Gizmos.DrawWireSphere(positions[i], 0.4f);
            if (i > 0) Gizmos.DrawLine(positions[i - 1], positions[i]);
        }

        if (entrySpline != null)
            DrawSplineGizmo(entrySpline, new Color(1f, 0.92f, 0.1f, 0.9f), positions[0]);
        if (passbySpline != null)
            DrawSplineGizmo(passbySpline, new Color(0.4f, 1f, 0.6f, 0.9f), Vector3.zero, drawEndLink: false);
    }

    private static void DrawSplineGizmo(SplineContainer spline, Color color, Vector3 linkTarget, bool drawEndLink = true)
    {
        const int steps = 24;
        var points = new Vector3[steps + 1];
        for (int s = 0; s <= steps; s++)
        {
            spline.Spline.Evaluate(s / (float)steps, out float3 ep, out _, out _);
            points[s] = spline.transform.TransformPoint((Vector3)ep);
        }
        Gizmos.color = color;
        for (int s = 0; s < steps; s++) Gizmos.DrawLine(points[s], points[s + 1]);
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(points[0], 0.5f);
        if (drawEndLink)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            Gizmos.DrawLine(points[steps], linkTarget);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(points[steps], 0.35f);
        }
    }
}