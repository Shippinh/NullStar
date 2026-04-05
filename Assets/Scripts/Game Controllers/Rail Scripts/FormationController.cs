using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Drives a single formation along a spline. Manages one FormationDefinition:
/// slot layout, enemy pooling, and travel behavior.
///
/// Set <see cref="externalControl"/> = true when this controller is owned by a
/// FormationLaneController — it will stop self-advancing anchorT and let the
/// lane controller drive it instead.
/// </summary>
public class FormationController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Spline")]
    public SplineContainer splineContainer;

    [Header("Definition")]
    public FormationDefinition definition;

    [Header("Anchor")]
    [Range(0f, 1f)] public float anchorT = 0f;
    public float anchorSpeed = 5f;

    [Header("External Control")]
    [Tooltip("When true, anchorT is driven externally (e.g. by FormationLaneController). " +
             "Self-advancement in FixedUpdate is disabled.")]
    public bool externalControl = false;

    [Header("Player Reference")]
    public PlayerRailController player;

    [Header("Manual Offsets")]
    public float manualRightOffset = 0f;
    public float manualUpOffset = 0f;
    public float manualTOffset = 0f;

    // ── Public anchor state ───────────────────────────────────────────────────

    public Vector3 AnchorPosition { get; private set; }
    public Vector3 AnchorForward { get; private set; }
    public Vector3 AnchorUp { get; private set; }
    public Vector3 AnchorRight { get; private set; }
    public Quaternion AnchorRotation { get; private set; }

    public Vector3 InterpolatedAnchorPosition { get; private set; }
    public Vector3 InterpolatedAnchorForward { get; private set; }
    public Vector3 InterpolatedAnchorUp { get; private set; }
    public Vector3 InterpolatedAnchorRight { get; private set; }
    public Quaternion InterpolatedAnchorRotation { get; private set; }

    // ── Transient layout ──────────────────────────────────────────────────────

    /// <summary>
    /// Per-slot world-space layout, rebuilt every FixedUpdate.
    /// No layout data lives on FormationSlot.
    /// </summary>
    private struct SlotLayout
    {
        public Vector3 anchorPos;
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;
        public float rightOffset;
        public float upOffset;

        public readonly Vector3 WorldPosition => anchorPos + right * rightOffset + up * upOffset;
        public readonly Quaternion Rotation => Quaternion.LookRotation(forward, up);
    }

    private SlotLayout[] _layout = System.Array.Empty<SlotLayout>();

    // ── Interpolation buffers ─────────────────────────────────────────────────

    private Vector3 _prevAnchorPos, _curAnchorPos;
    private Vector3 _prevAnchorFwd, _curAnchorFwd;
    private Vector3 _prevAnchorUp, _curAnchorUp;
    private Vector3 _prevAnchorRight, _curAnchorRight;
    private Quaternion _prevAnchorRot, _curAnchorRot;

    // ── Internals ─────────────────────────────────────────────────────────────

    private List<FormationSlot> Slots =>
        definition != null ? definition.slots : _emptySlots;
    private static readonly List<FormationSlot> _emptySlots = new();

    private float _splineLength;
    private SplineArcLengthTable.ArcLengthCursor _cursor;
    private float _time;
    private float _smoothSlotCount;

    private const float SlotCountSmoothSpeed = 3f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (!splineContainer) { Debug.LogError("[FormationController] No SplineContainer"); return; }

        var table = splineContainer.GetComponent<SplineArcLengthTable>();
        if (table == null) { Debug.LogError("[FormationController] No SplineArcLengthTable"); return; }

        _splineLength = splineContainer.Spline.GetLength();
        _cursor = table.CreateCursor();

        if (!player) player = FindObjectOfType<PlayerRailController>();

        // Work on a runtime instance so we never dirty the source asset
        if (definition != null)
            definition = Instantiate(definition);

        RefreshAnchor();
        InitInterpolationBuffers();
    }

    private void Start()
    {
        _smoothSlotCount = Slots.Count;
        RebuildLayout(_time);
        FillEmptySlots();
    }

    private void Update()
    {
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
        InterpolatedAnchorPosition = Vector3.Lerp(_prevAnchorPos, _curAnchorPos, alpha);
        InterpolatedAnchorForward = Vector3.Lerp(_prevAnchorFwd, _curAnchorFwd, alpha).normalized;
        InterpolatedAnchorUp = Vector3.Lerp(_prevAnchorUp, _curAnchorUp, alpha).normalized;
        InterpolatedAnchorRight = Vector3.Lerp(_prevAnchorRight, _curAnchorRight, alpha).normalized;
        InterpolatedAnchorRotation = Quaternion.Slerp(_prevAnchorRot, _curAnchorRot, alpha);
    }

    private void FixedUpdate()
    {
        if (!splineContainer) return;

        _time += Time.fixedDeltaTime;

        _prevAnchorPos = _curAnchorPos;
        _prevAnchorFwd = _curAnchorFwd;
        _prevAnchorUp = _curAnchorUp;
        _prevAnchorRight = _curAnchorRight;
        _prevAnchorRot = _curAnchorRot;

        // Only self-advance when not driven by a lane controller
        if (!externalControl)
            anchorT = Mathf.Repeat(anchorT + anchorSpeed * Time.fixedDeltaTime / _splineLength, 1f);

        RefreshAnchor();

        _curAnchorPos = AnchorPosition;
        _curAnchorFwd = AnchorForward;
        _curAnchorUp = AnchorUp;
        _curAnchorRight = AnchorRight;
        _curAnchorRot = AnchorRotation;

        SmoothSlotCount();
        RebuildLayout(_time);
        DriveEnemies();
    }

    // ── Anchor evaluation — single source of truth ────────────────────────────

    /// <summary>
    /// Evaluates the spline at <c>anchorT + manualTOffset + tOffset</c>.
    /// Every slot position uses this — no per-slot caching, no stale state.
    /// </summary>
    private void EvaluateAnchorAtT(float tOffset,
        out Vector3 pos, out Vector3 right, out Vector3 up, out Vector3 fwd)
    {
        float t = Mathf.Repeat(anchorT + manualTOffset + tOffset, 1f);
        float curveT = _cursor != null ? _cursor.Evaluate(t) : t;

        splineContainer.Spline.Evaluate(curveT, out float3 p, out float3 tangent, out float3 upVec);

        fwd = splineContainer.transform.TransformDirection(((Vector3)tangent).normalized);
        up = splineContainer.transform.TransformDirection(((Vector3)upVec).normalized);
        right = Vector3.Cross(up, fwd).normalized;
        pos = splineContainer.transform.TransformPoint((Vector3)p)
                + right * manualRightOffset
                + up * manualUpOffset;
    }

    private void RefreshAnchor()
    {
        EvaluateAnchorAtT(0f, out var pos, out var right, out var up, out var fwd);
        AnchorPosition = pos;
        AnchorRight = right;
        AnchorUp = up;
        AnchorForward = fwd;
        AnchorRotation = Quaternion.LookRotation(fwd, up);
    }

    // ── Manual offset API ─────────────────────────────────────────────────────

    public void SetRightOffset(float v) => manualRightOffset = v;
    public void SetUpOffset(float v) => manualUpOffset = v;
    public void SetTOffset(float v) => manualTOffset = v;
    public void AdjustRightOffset(float d) => manualRightOffset += d;
    public void AdjustUpOffset(float d) => manualUpOffset += d;
    public void AdjustTOffset(float d) => manualTOffset += d;
    public void ResetManualOffsets()
    {
        manualRightOffset = 0f;
        manualUpOffset = 0f;
        manualTOffset = 0f;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void SmoothSlotCount()
    {
        int occupied = 0;
        foreach (var s in Slots) if (s.IsOccupied) occupied++;
        _smoothSlotCount = Mathf.Lerp(
            _smoothSlotCount, occupied, Time.fixedDeltaTime * SlotCountSmoothSpeed);
    }

    private void RebuildLayout(float t)
    {
        if (definition == null) return;

        int total = Slots.Count;
        if (_layout.Length != total)
            _layout = new SlotLayout[total];

        float spacing = definition.slotSpacing.Evaluate(t);
        float n = Mathf.Max(1f, _smoothSlotCount);
        Vector2[] local = ComputeLayoutOffsets(definition.shape, total, n, spacing, t);

        for (int i = 0; i < total; i++)
        {
            _layout[i] = new SlotLayout
            {
                anchorPos = AnchorPosition,
                right = AnchorRight,
                up = AnchorUp,
                forward = AnchorForward,
                rightOffset = local[i].x,
                upOffset = local[i].y,
            };
        }
    }

    private Vector2[] ComputeLayoutOffsets(
        FormationShape shape, int count, float n, float spacing, float t)
    {
        var offsets = new Vector2[count];

        switch (shape)
        {
            case FormationShape.Line:
                {
                    float totalWidth = (n - 1f) * spacing;
                    for (int i = 0; i < count; i++)
                    {
                        float phase = count > 1
                            ? definition.slotPhaseSpread * ((float)i / (count - 1))
                            : 0f;
                        offsets[i] = new Vector2(
                            -totalWidth * 0.5f + i * spacing,
                            definition.perSlotUpOffset.oscillator.Evaluate(
                                t + phase, definition.perSlotUpOffset.value));
                    }
                    break;
                }
            case FormationShape.Column:
                {
                    float totalHeight = (n - 1f) * spacing;
                    for (int i = 0; i < count; i++)
                    {
                        float phase = count > 1
                            ? definition.slotPhaseSpread * ((float)i / (count - 1))
                            : 0f;
                        offsets[i] = new Vector2(
                            definition.perSlotRightOffset.oscillator.Evaluate(
                                t + phase, definition.perSlotRightOffset.value),
                            -totalHeight * 0.5f + i * spacing);
                    }
                    break;
                }
            case FormationShape.Circle:
                {
                    float radius = Mathf.Max(spacing, spacing * n / (Mathf.PI * 2f));
                    for (int i = 0; i < count; i++)
                    {
                        float angle = (i / n) * Mathf.PI * 2f;
                        offsets[i] = new Vector2(
                            Mathf.Cos(angle) * radius,
                            Mathf.Sin(angle) * radius);
                    }
                    break;
                }
            case FormationShape.V:
                {
                    for (int i = 0; i < count; i++)
                    {
                        int side = (i % 2 == 0) ? 1 : -1;
                        int depth = i / 2 + 1;
                        offsets[i] = new Vector2(
                            side * depth * spacing,
                            -depth * spacing * 0.5f);
                    }
                    break;
                }
            case FormationShape.Diamond:
                {
                    Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
                    for (int i = 0; i < count; i++)
                        offsets[i] = dirs[i % 4] * ((i / 4 + 1) * spacing);
                    break;
                }
            case FormationShape.Stagger:
                {
                    int perRow = Mathf.CeilToInt(n * 0.5f);
                    for (int i = 0; i < count; i++)
                    {
                        int row = i % 2;
                        int col = i / 2;
                        float rowShift = row == 1 ? spacing * 0.5f : 0f;
                        float totalWidth = (perRow - 1) * spacing;
                        offsets[i] = new Vector2(
                            -totalWidth * 0.5f + col * spacing + rowShift,
                            row * spacing * 0.8f - spacing * 0.4f);
                    }
                    break;
                }
            default: // Grid
                {
                    int cols = Mathf.Max(1, definition.gridColumns);
                    int rowCount = Mathf.CeilToInt(n / cols);
                    for (int i = 0; i < count; i++)
                    {
                        int col = i % cols;
                        int row = i / cols;
                        offsets[i] = new Vector2(
                            (col - (cols - 1) * 0.5f) * spacing,
                            (row - (rowCount - 1) * 0.5f) * spacing);
                    }
                    break;
                }
        }

        return offsets;
    }

    // ── Drive enemies ─────────────────────────────────────────────────────────

    private void DriveEnemies()
    {
        if (definition == null) return;

        for (int i = 0; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (!slot.IsOccupied) continue;

            ref readonly SlotLayout sl = ref _layout[i];
            Vector3 target = sl.WorldPosition;
            Quaternion rot = ComputeEnemyRotation(target, in sl);

            slot.enemy.SetFormationTarget(target, rot);

            if (slot.enemyController != null)
                SyncTurrets(slot.enemyController, definition.shootingEnabled);
        }
    }

    private Quaternion ComputeEnemyRotation(Vector3 targetPos, in SlotLayout sl)
    {
        if (definition.orientation == FormationOrientation.TowardsPlayer && player != null)
        {
            Vector3 dir = (player.transform.position - targetPos).normalized;
            if (dir.sqrMagnitude > 0.001f)
                return Quaternion.LookRotation(dir, sl.up);
        }
        return sl.Rotation;
    }

    // ── Turret API ────────────────────────────────────────────────────────────

    public void SetShootingEnabled(bool enabled)
    {
        if (definition != null) definition.shootingEnabled = enabled;
        foreach (var slot in Slots)
            if (slot.enemyController != null)
                SyncTurrets(slot.enemyController, enabled);
    }

    private void SyncTurrets(EnemyController controller, bool enabled)
    {
        foreach (var turret in controller.GetComponentsInChildren<TurretBehavior>())
            turret.SyncState(enabled);
    }

    // ── Pooling ───────────────────────────────────────────────────────────────

    public void FillEmptySlots()
    {
        for (int i = 0; i < Slots.Count; i++)
            if (!Slots[i].IsOccupied) DepoolIntoSlot(i);
    }

    private void DepoolIntoSlot(int slotIndex)
    {
        var slot = Slots[slotIndex];
        if (string.IsNullOrEmpty(slot.poolTag)) return;

        bool hasLayout = _layout.Length > slotIndex;
        Vector3 spawnPos = hasLayout ? _layout[slotIndex].WorldPosition : AnchorPosition;
        Quaternion spawnRot = hasLayout ? _layout[slotIndex].Rotation : AnchorRotation;

        GameObject obj = ObjectPool.Instance.GetPooledObject(
            slot.poolTag, spawnPos, spawnRot, shouldBeRequeued: false);
        if (obj == null) return;

        var rail = obj.GetComponentInChildren<EnemyRailController>();
        if (rail == null) return;

        var ai = obj.GetComponentInChildren<EnemyAIComponent>();
        if (ai != null) ai.enabled = false;

        var enemyController = obj.GetComponent<EnemyController>();
        if (enemyController != null) enemyController.HandleRailAttach(anchorSpeed);

        rail.splineContainer = splineContainer;
        rail.InitializeSpline();
        rail.InitializeEnemy();
        rail.body.isKinematic = true;

        if (definition != null)
        {
            float freq = definition.travelFrequency.Evaluate(0f);
            float phase = 0f;

            if (definition.randomizeTravelPerSlot)
            {
                freq += UnityEngine.Random.Range(-definition.randomFrequencyVariance, definition.randomFrequencyVariance);
                phase = UnityEngine.Random.Range(0f, definition.randomPhaseVariance);
            }

            rail.SetTravelBehavior(
                definition.travelPattern,
                definition.travelRadius,
                freq, phase,
                GetSlotSign(slotIndex));
        }

        rail.SetFormationTarget(spawnPos, spawnRot);
        slot.enemy = rail;
        slot.enemyController = enemyController;
    }

    private float GetSlotSign(int slotIndex)
    {
        if (definition.invertAll) return -1f;
        if (definition.alternateInversion && slotIndex % 2 == 1) return -1f;
        return 1f;
    }

    // ── Interpolation buffers ─────────────────────────────────────────────────

    private void InitInterpolationBuffers()
    {
        _curAnchorPos = AnchorPosition;
        _curAnchorFwd = AnchorForward;
        _curAnchorUp = AnchorUp;
        _curAnchorRight = AnchorRight;
        _curAnchorRot = AnchorRotation;

        _prevAnchorPos = _curAnchorPos;
        _prevAnchorFwd = _curAnchorFwd;
        _prevAnchorUp = _curAnchorUp;
        _prevAnchorRight = _curAnchorRight;
        _prevAnchorRot = _curAnchorRot;

        InterpolatedAnchorPosition = _curAnchorPos;
        InterpolatedAnchorForward = _curAnchorFwd;
        InterpolatedAnchorUp = _curAnchorUp;
        InterpolatedAnchorRight = _curAnchorRight;
        InterpolatedAnchorRotation = _curAnchorRot;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        bool playing = Application.isPlaying;
        Vector3 pos = playing ? InterpolatedAnchorPosition : AnchorPosition;
        Vector3 fwd = playing ? InterpolatedAnchorForward : AnchorForward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 0.5f);
        Gizmos.DrawRay(pos, fwd * 3f);

        if (playing && _layout != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var sl in _layout)
            {
                Vector3 slotPos = sl.WorldPosition;
                Gizmos.DrawWireSphere(slotPos, 0.3f);
                Gizmos.DrawLine(sl.anchorPos, slotPos);
            }
        }
    }
}