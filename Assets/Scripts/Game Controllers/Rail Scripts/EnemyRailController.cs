using UnityEngine;

public class EnemyRailController : RailController
{
    [Header("References")]
    public PlayerRailController playerRailControllerRef;
    public Rigidbody body;

    [Header("Plane Offset")]
    public float rightOffset = 0f;
    public float upOffset = 0f;

    private Vector3 _formationTargetPosition;
    private Quaternion _formationTargetRotation;
    private bool _hasFormationTarget;

    public override void Awake()
    {
        base.Awake();

        if (initializeOnAwake)
        {
            InitializeEnemy();
        }
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

    void Update()
    {
        UpdateInterpolatedSpline();
    }

    void FixedUpdate()
    {
        SnapshotSplineForInterpolation();
        TickSpline(Time.fixedDeltaTime);
        EvaluateSpline();
        CommitSplineToInterpolation();

        Vector3 desiredPos = _hasFormationTarget
            ? _formationTargetPosition
            : SplinePosition + SplineRight * rightOffset + SplineUp * upOffset;

        Quaternion desiredRot = _hasFormationTarget
            ? _formationTargetRotation
            : SplineRotation;

        body.MovePosition(desiredPos);
        body.MoveRotation(desiredRot);
    }

    public void SetFormationTarget(Vector3 position, Quaternion rotation)
    {
        _formationTargetPosition = position;
        _formationTargetRotation = rotation;
        _hasFormationTarget = true;
    }

    public void ClearFormationTarget() => _hasFormationTarget = false;
}