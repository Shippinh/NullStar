using UnityEngine;

public class EnemyRailController : RailController
{
    [Header("References")]
    public PlayerRailController playerRailControllerRef;
    public Rigidbody body;

    // ── Lane position ─────────────────────────────────────────────────────────

    /// <summary>Current lane offsets — read by EnemyLane each frame to compute world position.</summary>
    public float LaneRight { get; private set; }
    public float LaneUp { get; private set; }

    private float _laneRightTarget, _laneRightFrom;
    private float _laneUpTarget, _laneUpFrom;
    private float _laneTransitionT, _laneTransitionDuration;
    private bool _laneTransitioning;

    // ── Formation target (set by EnemyLane each FixedUpdate) ─────────────────

    private Vector3 _formationTargetPosition;
    private Quaternion _formationTargetRotation;
    private bool _hasFormationTarget;

    /// <summary>
    /// The EnemyLane instance that currently owns this enemy.
    /// Only that lane may call TickPhysics — any other caller is a stale pool
    /// reference from a previous life and must be ignored.
    /// Set by SetFormationTarget, cleared by ClearFormationTarget.
    /// </summary>
    public Object OwningLane { get; private set; }

    // Interpolation buffers for the formation path.
    // Spline-derived buffers (previousSplinePosition / currentSplinePosition) are
    // never updated when LaneDriven=true because EvaluateSpline() is intentionally
    // skipped.  We therefore maintain a parallel pair of snapshots that track the
    // actual world position the enemy was told to occupy each physics step.
    private Vector3 _prevFormationPos;
    private Vector3 _currFormationPos;
    private Quaternion _prevFormationRot;
    private Quaternion _currFormationRot;
    private bool _formationBuffersInitialized;

    // ── Diagnostics ───────────────────────────────────────────────────────────

    [Header("Diagnostics")]
    [SerializeField] bool _debugLog = false;

    private Vector3 _prevDesiredPos;
    private int _ticksThisFixedFrame;
    private int _lastFixedFrameCount = -1;

    // ── Travel params (optional decoration on top of lane) ────────────────────

    private FormationMovementType _travelPattern;
    private float _travelRadius;
    private float _travelFrequency;
    private float _travelPhase;
    private float _travelTime;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void Awake()
    {
        base.Awake();
        if (initializeOnAwake)
            InitializeEnemy();
    }

    public void InitializeEnemy()
    {
        if (!playerRailControllerRef)
            playerRailControllerRef = FindObjectOfType<PlayerRailController>();
        if (!body)
            body = GetComponent<Rigidbody>();

        EvaluateSpline();
        InitializeInterpolationBuffers();
    }

    private void Update()
    {
        if (LaneDriven && _formationBuffersInitialized)
        {
            // Formation-driven enemies must interpolate between consecutive
            // *formation* positions, not spline positions.  The spline buffers
            // (previousSplinePosition / currentSplinePosition) are frozen because
            // EvaluateSpline() is intentionally skipped — lerping them produces
            // a permanently frozen interpolated position.
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            InterpolatedSplinePosition = Vector3.Lerp(_prevFormationPos, _currFormationPos, alpha);
            InterpolatedSplineRotation = Quaternion.Slerp(_prevFormationRot, _currFormationRot, alpha);
            // Forward/Right/Up are derived from the interpolated rotation so any
            // consumer of InterpolatedSplineForward etc. also gets smooth values.
            InterpolatedSplineForward = InterpolatedSplineRotation * Vector3.forward;
            InterpolatedSplineUp = InterpolatedSplineRotation * Vector3.up;
            InterpolatedSplineRight = InterpolatedSplineRotation * Vector3.right;
        }
        else
        {
            UpdateInterpolatedSpline();
        }

        if (_debugLog)
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            if (LaneDriven)
            {
                Debug.Log($"[ERC:{name}] Update alpha={alpha:F4}  " +
                          $"interpPos={InterpolatedSplinePosition}  " +
                          $"prevFormationPos={_prevFormationPos}  " +
                          $"currFormationPos={_currFormationPos}  " +
                          $"buffersInit={_formationBuffersInitialized}");
            }
            else
            {
                Debug.Log($"[ERC:{name}] Update alpha={alpha:F4}  " +
                          $"interpPos={InterpolatedSplinePosition}  " +
                          $"prevPos={previousSplinePosition}  " +
                          $"currPos={currentSplinePosition}  " +
                          $"prevEqCurr={previousSplinePosition == currentSplinePosition}");
            }
        }
    }

    private void FixedUpdate()
    {
        // When EnemyLane owns this enemy it calls TickPhysics() itself, after
        // SetFormationTarget, so execution order is guaranteed and there is no
        // risk of the enemy ticking twice or with stale data.
        if (LaneDriven) return;

        TickPhysics(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Full physics step for this enemy.  Called by EnemyLane.FixedUpdate when
    /// lane-driven (so the lane is the single clock), or by our own FixedUpdate
    /// when self-propelled.
    /// </summary>
    public void TickPhysics(float dt)
    {
        if (_debugLog)
        {
            // Double-tick guard: if this fires more than once for the same Physics frame
            // something upstream is calling TickPhysics twice on the same enemy.
            if (Time.frameCount == _lastFixedFrameCount)
            {
                _ticksThisFixedFrame++;
                Debug.LogWarning($"[ERC:{name}] DOUBLE-TICK detected! " +
                                 $"TickPhysics called {_ticksThisFixedFrame}x in fixedFrame={Time.frameCount}  " +
                                 $"LaneDriven={LaneDriven}");
            }
            else
            {
                _ticksThisFixedFrame = 1;
                _lastFixedFrameCount = Time.frameCount;
            }
        }

        // ── Snapshot formation interpolation buffers ──────────────────────────
        // Must happen before we overwrite _formationTargetPosition so that
        // _prevFormationPos captures where we *were* this step, not where we're going.
        if (_hasFormationTarget)
        {
            if (_formationBuffersInitialized)
            {
                _prevFormationPos = _currFormationPos;
                _prevFormationRot = _currFormationRot;
            }
            // _currFormationPos will be written after desiredPos is computed below.
        }

        SnapshotSplineForInterpolation();

        if (_hasFormationTarget)
        {
            // Formation-controlled: do NOT tick or evaluate the spline.
            // Running EvaluateSpline() here would overwrite SplinePosition/Rotation
            // with splineT=0 (or wherever this enemy's cursor sits), which is the
            // same for all enemies sharing the same SplineContainer — that's what
            // causes the accordion / collapse into a single point.
            // splineT is kept in sync by EnemyLane.SyncSlotT so it's correct the
            // moment formation control is released.
        }
        else
        {
            // Self-propelled: advance and evaluate normally.
            TickSpline(dt);
            EvaluateSpline();
        }

        CommitSplineToInterpolation();

        TickLaneTransition(dt);

        _travelTime += dt;

        Vector3 desiredPos = _hasFormationTarget
            ? ApplyTravel(_formationTargetPosition, _formationTargetRotation)
            : SplinePosition + SplineRight * LaneRight + SplineUp * LaneUp;

        Quaternion desiredRot = _hasFormationTarget
            ? _formationTargetRotation
            : SplineRotation;

        // ── Commit formation interpolation buffers ────────────────────────────
        if (_hasFormationTarget)
        {
            _currFormationPos = desiredPos;
            _currFormationRot = desiredRot;
            if (!_formationBuffersInitialized)
            {
                // First tick: prev == curr so the first render frame doesn't lerp
                // from a garbage/zero position (which caused the 1236× step ratio).
                _prevFormationPos = desiredPos;
                _prevFormationRot = desiredRot;
                _formationBuffersInitialized = true;
            }
        }

        body.MovePosition(desiredPos);
        body.MoveRotation(desiredRot);

        if (_debugLog)
        {
            float stepSize = Vector3.Distance(desiredPos, _prevDesiredPos);
            float expectedStep = MaxSpeed * dt;
            // angleDelta: how many degrees the forward direction rotated this step.
            // Large values on turns indicate the spline is curving sharply here —
            // useful to correlate against when jitter is worst.
            float angleDelta = Quaternion.Angle(
                _prevDesiredPos == Vector3.zero ? desiredRot : body.rotation, desiredRot);

            Debug.Log($"[ERC:{name}] FixedUpdate  " +
                      $"stepSize={stepSize:F4}  expectedStep≈{expectedStep:F4}  " +
                      $"stepRatio={stepSize / Mathf.Max(0.0001f, expectedStep):F3}  " +
                      $"angleDelta={angleDelta:F3}°  " +
                      $"LaneDriven={LaneDriven}  hasTarget={_hasFormationTarget}  " +
                      $"splineT={splineT:F5}  dt={dt:F5}");

            _prevDesiredPos = desiredPos;
        }
    }

    // ── Lane API ──────────────────────────────────────────────────────────────

    public void SetLane(float targetRight, float targetUp, float duration)
    {
        if (duration <= 0f)
        {
            LaneRight = targetRight;
            LaneUp = targetUp;
            _laneTransitioning = false;
            return;
        }

        _laneRightFrom = LaneRight;
        _laneRightTarget = targetRight;
        _laneUpFrom = LaneUp;
        _laneUpTarget = targetUp;

        _laneTransitionT = 0f;
        _laneTransitionDuration = duration;
        _laneTransitioning = true;
    }

    private void TickLaneTransition(float dt)
    {
        if (!_laneTransitioning) return;

        _laneTransitionT += dt;
        float alpha = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(_laneTransitionT / _laneTransitionDuration));

        LaneRight = Mathf.Lerp(_laneRightFrom, _laneRightTarget, alpha);
        LaneUp = Mathf.Lerp(_laneUpFrom, _laneUpTarget, alpha);

        if (alpha >= 1f) _laneTransitioning = false;
    }

    // ── Formation API (called by EnemyLane) ───────────────────────────────────

    /// <summary>
    /// When true, EnemyLane calls TickPhysics() directly each FixedUpdate and
    /// this enemy's own FixedUpdate becomes a no-op.  This eliminates all
    /// ordering and double-tick desync between the lane and the enemy.
    /// </summary>
    public bool LaneDriven { get; private set; }

    public void SetFormationTarget(Vector3 position, Quaternion rotation, Object owner = null)
    {
        _formationTargetPosition = position;
        _formationTargetRotation = rotation;
        _hasFormationTarget = true;
        LaneDriven = true;
        if (owner != null) OwningLane = owner;
    }

    /// <summary>
    /// Seeds both formation interpolation buffers to the given position/rotation
    /// so the first render frame doesn't lerp from zero. Call immediately after
    /// SetFormationTarget at spawn time.
    /// </summary>
    public void WarmFormationBuffers(Vector3 position, Quaternion rotation)
    {
        _currFormationPos = position;
        _prevFormationPos = position;
        _currFormationRot = rotation;
        _prevFormationRot = rotation;
        _formationBuffersInitialized = true;

        InterpolatedSplinePosition = position;
        InterpolatedSplineRotation = rotation;
        InterpolatedSplineForward = rotation * Vector3.forward;
        InterpolatedSplineUp = rotation * Vector3.up;
        InterpolatedSplineRight = rotation * Vector3.right;
    }

    public void ClearFormationTarget()
    {
        _hasFormationTarget = false;
        _travelPattern = FormationMovementType.None;
        LaneDriven = false;
        _formationBuffersInitialized = false;
        OwningLane = null;
    }

    /// <summary>
    /// Called by EnemyLane every FixedUpdate to keep this enemy's splineT in sync
    /// with its assigned slot position.  This ensures that if formation control is
    /// ever released (e.g. the lane is destroyed) the enemy self-propels from the
    /// correct position rather than snapping back to t=0.
    /// </summary>
    public void SyncSplineT(float t)
    {
        splineT = t;
    }

    // ── Travel decoration ─────────────────────────────────────────────────────

    public void SetTravelBehavior(FormationMovementType pattern, float radius,
                                  float frequency, float phase)
    {
        _travelPattern = pattern;
        _travelRadius = radius;
        _travelFrequency = frequency;
        _travelPhase = phase;
        _travelTime = phase;
    }

    private Vector3 ApplyTravel(Vector3 target, Quaternion targetRot)
    {
        if (_travelPattern == FormationMovementType.None) return target;

        Vector3 right = targetRot * Vector3.right;
        Vector3 up = targetRot * Vector3.up;
        Vector3 fwd = targetRot * Vector3.forward;

        float t = _travelTime * _travelFrequency * Mathf.PI * 2f;
        float r = _travelRadius;

        return _travelPattern switch
        {
            FormationMovementType.Wiggle => target + right * Mathf.Sin(t) * r,
            FormationMovementType.Bob => target + up * Mathf.Sin(t) * r,
            FormationMovementType.Saw => target + right * ((_travelTime * _travelFrequency % 1f) * 2f - 1f) * r,
            FormationMovementType.Orbit => target + right * Mathf.Cos(t) * r + up * Mathf.Sin(t) * r,
            FormationMovementType.Lissajous => target + right * Mathf.Sin(t) * r + up * Mathf.Sin(t * 2f) * r,
            FormationMovementType.Surge => target + fwd * Mathf.Sin(t) * r,
            FormationMovementType.Spiral => target + right * Mathf.Cos(t) * r + up * Mathf.Sin(t) * r
                                                      + fwd * Mathf.Sin(t * 0.5f) * r * 0.5f,
            FormationMovementType.Snake => target + right * Mathf.Sin(t) * r
                                                      + fwd * Mathf.Sin(t * 0.5f) * r * 0.5f,
            FormationMovementType.Drift => target
                + right * (Mathf.PerlinNoise(_travelPhase, _travelTime * _travelFrequency) - 0.5f) * 2f * r
                + up * (Mathf.PerlinNoise(_travelPhase + 100f, _travelTime * _travelFrequency) - 0.5f) * 2f * r,
            _ => target
        };
    }
}