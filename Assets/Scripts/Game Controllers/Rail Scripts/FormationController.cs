using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class FormationController : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;

    [Header("Anchor")]
    [Range(0f, 1f)] public float anchorT = 0f;
    public float anchorSpeed = 5f;

    [Header("Slots")]
    public List<FormationSlot> slots = new();
    public float slotSpacing = 5f;

    private float _splineLength;
    private SplineArcLengthTable.ArcLengthCursor _cursor;

    // Raw physics values (FixedUpdate)
    public Vector3 AnchorPosition { get; private set; }
    public Vector3 AnchorForward { get; private set; }
    public Vector3 AnchorUp { get; private set; }
    public Vector3 AnchorRight { get; private set; }
    public Quaternion AnchorRotation { get; private set; }

    // Interpolated visual values (Update)
    public Vector3 InterpolatedAnchorPosition { get; private set; }
    public Vector3 InterpolatedAnchorForward { get; private set; }
    public Vector3 InterpolatedAnchorUp { get; private set; }
    public Vector3 InterpolatedAnchorRight { get; private set; }
    public Quaternion InterpolatedAnchorRotation { get; private set; }

    private Vector3 _previousAnchorPosition;
    private Vector3 _previousAnchorForward;
    private Vector3 _previousAnchorUp;
    private Vector3 _previousAnchorRight;
    private Quaternion _previousAnchorRotation;

    private Vector3 _currentAnchorPosition;
    private Vector3 _currentAnchorForward;
    private Vector3 _currentAnchorUp;
    private Vector3 _currentAnchorRight;
    private Quaternion _currentAnchorRotation;

    private void Awake()
    {
        if (!splineContainer) { Debug.LogError("[FormationController] No SplineContainer"); return; }

        _splineLength = splineContainer.Spline.GetLength();

        var table = splineContainer.GetComponent<SplineArcLengthTable>();
        if (table == null) { Debug.LogError("[FormationController] No SplineArcLengthTable"); return; }

        _cursor = table.CreateCursor();

        // Seed buffers so interpolation starts valid
        EvaluateAnchor();
        InitializeInterpolationBuffers();
    }

    private void Start()
    {
        EvaluateAnchor();
        DistributeSlots();
        FillEmptySlots();
    }

    private void Update()
    {
        float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

        InterpolatedAnchorPosition = Vector3.Lerp(_previousAnchorPosition, _currentAnchorPosition, alpha);
        InterpolatedAnchorForward = Vector3.Lerp(_previousAnchorForward, _currentAnchorForward, alpha).normalized;
        InterpolatedAnchorUp = Vector3.Lerp(_previousAnchorUp, _currentAnchorUp, alpha).normalized;
        InterpolatedAnchorRight = Vector3.Lerp(_previousAnchorRight, _currentAnchorRight, alpha).normalized;
        InterpolatedAnchorRotation = Quaternion.Slerp(_previousAnchorRotation, _currentAnchorRotation, alpha);
    }

    private void FixedUpdate()
    {
        if (!splineContainer) return;

        _previousAnchorPosition = _currentAnchorPosition;
        _previousAnchorForward = _currentAnchorForward;
        _previousAnchorUp = _currentAnchorUp;
        _previousAnchorRight = _currentAnchorRight;
        _previousAnchorRotation = _currentAnchorRotation;

        AdvanceAnchor(Time.fixedDeltaTime);
        EvaluateAnchor();

        _currentAnchorPosition = AnchorPosition;
        _currentAnchorForward = AnchorForward;
        _currentAnchorUp = AnchorUp;
        _currentAnchorRight = AnchorRight;
        _currentAnchorRotation = AnchorRotation;

        DistributeSlots();
        DriveEnemies();
    }

    private void InitializeInterpolationBuffers()
    {
        _currentAnchorPosition = AnchorPosition;
        _currentAnchorForward = AnchorForward;
        _currentAnchorUp = AnchorUp;
        _currentAnchorRight = AnchorRight;
        _currentAnchorRotation = AnchorRotation;

        _previousAnchorPosition = _currentAnchorPosition;
        _previousAnchorForward = _currentAnchorForward;
        _previousAnchorUp = _currentAnchorUp;
        _previousAnchorRight = _currentAnchorRight;
        _previousAnchorRotation = _currentAnchorRotation;

        InterpolatedAnchorPosition = _currentAnchorPosition;
        InterpolatedAnchorForward = _currentAnchorForward;
        InterpolatedAnchorUp = _currentAnchorUp;
        InterpolatedAnchorRight = _currentAnchorRight;
        InterpolatedAnchorRotation = _currentAnchorRotation;
    }

    // ── Pooling ───────────────────────────────────────────────────────────────

    public void FillEmptySlots()
    {
        foreach (var slot in slots)
            if (!slot.IsOccupied) DePoolIntoSlot(slot);
    }

    public void DePoolIntoSlot(FormationSlot slot)
    {
        if (string.IsNullOrEmpty(slot.poolTag)) return;

        Vector3 slotWorldPos = AnchorPosition
            + AnchorRight * slot.rightOffset
            + AnchorUp * slot.upOffset;

        GameObject obj = ObjectPool.Instance.GetPooledObject(slot.poolTag, slotWorldPos, AnchorRotation, shouldBeRequeued: false);

        if (obj == null) return;

        EnemyRailController rail = obj.GetComponentInChildren<EnemyRailController>();
        if (rail == null) return;

        EnemyAIComponent ai = obj.GetComponentInChildren<EnemyAIComponent>();
        if (ai != null) ai.enabled = false;

        EnemyController enemy = obj.GetComponentInChildren<EnemyController>();
        if (enemy != null) enemy.HandleRailAttach(anchorSpeed); // this is basic, in case we need some unique formations with custom movespeeds then we need a full proper implementation for that

        rail.splineContainer = splineContainer;
        rail.InitializeSpline();
        rail.InitializeEnemy();

        rail.body.isKinematic = true;

        rail.SetFormationTarget(slotWorldPos, AnchorRotation);

        slot.enemy = rail;
    }

    // ── Anchor ────────────────────────────────────────────────────────────────

    private void AdvanceAnchor(float dt)
    {
        if (_splineLength <= 0f) return;
        anchorT = (anchorT + anchorSpeed * dt / _splineLength) % 1f;
    }

    private void EvaluateAnchor()
    {
        float curveT = _cursor != null ? _cursor.Evaluate(anchorT) : anchorT;

        splineContainer.Spline.Evaluate(curveT,
            out float3 pos, out float3 tangent, out float3 upVec);

        Vector3 fwd = splineContainer.transform.TransformDirection(((Vector3)tangent).normalized);
        Vector3 up = splineContainer.transform.TransformDirection(((Vector3)upVec).normalized);
        Vector3 right = Vector3.Cross(up, fwd).normalized;

        AnchorPosition = splineContainer.transform.TransformPoint((Vector3)pos);
        AnchorForward = fwd;
        AnchorUp = up;
        AnchorRight = right;
        AnchorRotation = Quaternion.LookRotation(fwd, up);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void DistributeSlots()
    {
        int count = slots.Count;
        float totalWidth = (count - 1) * slotSpacing;

        for (int i = 0; i < count; i++)
        {
            slots[i].rightOffset = -totalWidth * 0.5f + i * slotSpacing;
            slots[i].upOffset = 0f;
        }
    }

    private void DriveEnemies()
    {
        foreach (var slot in slots)
        {
            if (!slot.IsOccupied) continue;

            Vector3 target = AnchorPosition
                + AnchorRight * slot.rightOffset
                + AnchorUp * slot.upOffset;

            slot.enemy.SetFormationTarget(target, AnchorRotation);
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Use interpolated values in editor so the gizmo tracks smoothly
        Vector3 pos = Application.isPlaying ? InterpolatedAnchorPosition : AnchorPosition;
        Quaternion rot = Application.isPlaying ? InterpolatedAnchorRotation : AnchorRotation;
        Vector3 right = Application.isPlaying ? InterpolatedAnchorRight : AnchorRight;
        Vector3 up = Application.isPlaying ? InterpolatedAnchorUp : AnchorUp;
        Vector3 fwd = Application.isPlaying ? InterpolatedAnchorForward : AnchorForward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 0.5f);
        Gizmos.DrawRay(pos, fwd * 3f);

        Gizmos.color = Color.yellow;
        foreach (var slot in slots)
        {
            Vector3 slotPos = pos
                + right * slot.rightOffset
                + up * slot.upOffset;
            Gizmos.DrawWireSphere(slotPos, 0.3f);
            Gizmos.DrawLine(pos, slotPos);
        }
    }
}