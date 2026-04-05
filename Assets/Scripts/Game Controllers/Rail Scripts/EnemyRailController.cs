using UnityEngine;

public class EnemyRailController : RailController
{
    [Header("References")]
    public PlayerRailController playerRailControllerRef;
    public Rigidbody body;

    [Header("Plane Offset (no formation)")]
    public float rightOffset = 0f;
    public float upOffset = 0f;

    // ── Formation target ──────────────────────────────────────────────────────

    private Vector3 _formationTargetPosition;
    private Quaternion _formationTargetRotation;
    private bool _hasFormationTarget;

    // ── Travel params (set once on depool by FormationController) ─────────────

    private FormationMovementType _travelPattern;
    private float _travelRadius;
    private float _travelFrequency;
    private float _travelPhase;   // Perlin noise seed / initial time offset
    private float _travelSign;    // +1 or -1, set by inversion flags
    private float _travelTime;    // accumulates in FixedUpdate

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
        UpdateInterpolatedSpline();
    }

    private void FixedUpdate()
    {
        SnapshotSplineForInterpolation();

        if (!_hasFormationTarget)
        {
            TickSpline(Time.fixedDeltaTime);
            EvaluateSpline();
        }

        CommitSplineToInterpolation();

        _travelTime += Time.fixedDeltaTime;

        Vector3 desiredPos = _hasFormationTarget
            ? ComputeTravelPosition(_formationTargetPosition, _formationTargetRotation)
            : SplinePosition + SplineRight * rightOffset + SplineUp * upOffset;

        Quaternion desiredRot = _hasFormationTarget
            ? _formationTargetRotation
            : SplineRotation;

        body.MovePosition(desiredPos);
        body.MoveRotation(desiredRot);
    }

    // ── Formation API ─────────────────────────────────────────────────────────

    public void SetFormationTarget(Vector3 position, Quaternion rotation)
    {
        _formationTargetPosition = position;
        _formationTargetRotation = rotation;
        _hasFormationTarget = true;
    }

    public void ClearFormationTarget()
    {
        _hasFormationTarget = false;
        _travelPattern = FormationMovementType.None;
    }

    /// <summary>
    /// Assigned once by FormationController at depool time.
    /// <paramref name="sign"/> is +1 or -1 depending on inversion flags.
    /// <paramref name="phase"/> doubles as both the initial time offset (desync) and
    /// the Perlin seed for Drift.
    /// </summary>
    public void SetTravelBehavior(FormationMovementType pattern, float radius,
                                  float frequency, float phase, float sign = 1f)
    {
        _travelPattern = pattern;
        _travelRadius = radius;
        _travelFrequency = frequency;
        _travelPhase = phase;
        _travelSign = sign;
        _travelTime = phase;   // start at phase so slots desync immediately
    }

    // ── Travel computation ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world position where this enemy should be this frame.
    /// Sign applies to the right/up plane; forward is intentionally unsigned
    /// so Surge/Spiral feel natural regardless of inversion.
    /// </summary>
    private Vector3 ComputeTravelPosition(Vector3 target, Quaternion targetRot)
    {
        if (_travelPattern == FormationMovementType.None) return target;

        Vector3 right = targetRot * Vector3.right * _travelSign;
        Vector3 up = targetRot * Vector3.up * _travelSign;
        Vector3 fwd = targetRot * Vector3.forward;           // never inverted

        float t = _travelTime * _travelFrequency * Mathf.PI * 2f;
        float r = _travelRadius;

        return _travelPattern switch
        {
            FormationMovementType.Wiggle =>
                target + right * Mathf.Sin(t) * r,

            FormationMovementType.Bob =>
                target + up * Mathf.Sin(t) * r,

            FormationMovementType.Saw =>
                target + right * ((_travelTime * _travelFrequency % 1f) * 2f - 1f) * r,

            FormationMovementType.Orbit =>
                target + right * Mathf.Cos(t) * r
                       + up * Mathf.Sin(t) * r,

            FormationMovementType.Lissajous =>
                target + right * Mathf.Sin(t) * r
                       + up * Mathf.Sin(t * 2f) * r,

            FormationMovementType.Surge =>
                target + fwd * Mathf.Sin(t) * r,

            FormationMovementType.Spiral =>
                target + right * Mathf.Cos(t) * r
                       + up * Mathf.Sin(t) * r
                       + fwd * Mathf.Sin(t * 0.5f) * r * 0.5f,

            FormationMovementType.Snake =>
                target + right * Mathf.Sin(t) * r
                       + fwd * Mathf.Sin(t * 0.5f) * r * 0.5f,

            FormationMovementType.Drift =>
                target
                    + right * (Mathf.PerlinNoise(_travelPhase, _travelTime * _travelFrequency) - 0.5f) * 2f * r
                    + up * (Mathf.PerlinNoise(_travelPhase + 100f, _travelTime * _travelFrequency) - 0.5f) * 2f * r,

            _ => target
        };
    }
}