using UnityEngine;

public class SimpleEnemyProjectile : MonoBehaviour, IPoolable
{
    public enum MovementMode { Default, PlayerSpace, SplineTracking }

    public string IPoolableTag { get; set; }

    [Header("Movement")]
    public float speed = 400f;
    public float maxLifetime = 2f;
    public MovementMode movementMode = MovementMode.Default;

    [Header("Damage")]
    public int damage = 1;
    public LayerMask hitLayers;

    public Rigidbody rb;
    public Collider col;
    public Vector3 direction;
    public bool impactHappened = false;

    // PlayerSpace state
    public EnemyRailController _turretRail;
    public Vector3 _turretLocalOffset;
    public RailController _playerRail;
    public float _playerRightOffset;
    public float _playerUpOffset;
    public float _rayProgress;

    // SplineTracking state
    public RailController _laneRail;
    public float _laneSplineT;
    public float _laneRight;
    public float _laneUp;

    public virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void InitializeInPlayerSpace(EnemyRailController turretRail, Vector3 turretLocalOffset, RailController playerRail, Vector3 spawnPos, float rightOffset, float upOffset)
    {
        _turretRail = turretRail;
        _turretLocalOffset = turretLocalOffset;
        _playerRail = playerRail;
        _playerRightOffset = rightOffset;
        _playerUpOffset = upOffset;
        rb.isKinematic = true;

        Vector3 origin = turretRail.InterpolatedSplinePosition + turretRail.InterpolatedSplineRotation * turretLocalOffset;
        Vector3 target = playerRail.InterpolatedSplinePosition
            + playerRail.InterpolatedSplineRight * rightOffset
            + playerRail.InterpolatedSplineUp * upOffset;

        _rayProgress = Vector3.Distance(origin, spawnPos);
    }

    public void InitializeOnSpline(EnemyRailController sourceRail, Vector3 worldSpawnPos)
    {
        movementMode = MovementMode.SplineTracking;
        _laneRail = sourceRail;
        _laneSplineT = sourceRail.splineT;
        rb.isKinematic = true;

        var (pos, _, up, right) = sourceRail.EvaluateAt(_laneSplineT);
        Vector3 offset = worldSpawnPos - pos;
        _laneRight = Vector3.Dot(offset, right);
        _laneUp = Vector3.Dot(offset, up);
    }

    public virtual void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        IPoolableTag = poolableTag;
        impactHappened = false;
        _turretRail = null;
        _playerRail = null;
        _laneRail = null;
        transform.position = position;
        transform.rotation = rotation;
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        CancelInvoke();
        Invoke(nameof(Impact), maxLifetime);
        gameObject.SetActive(true);
    }

    public virtual void HandleRepool()
    {
        CancelInvoke();
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    public void Initialize(Vector3 startPosition, Vector3 targetPosition)
    {
        if (movementMode != MovementMode.Default) return;
        direction = (targetPosition - startPosition).normalized;
        rb.velocity = direction * speed;
    }

    private Vector3 GetRayOrigin() =>
        _turretRail.InterpolatedSplinePosition + _turretRail.InterpolatedSplineRotation * _turretLocalOffset;

    protected virtual void Update() { }

    protected virtual void LateUpdate()
    {
        if (movementMode == MovementMode.PlayerSpace)
            TickPlayerSpace();
    }

    protected virtual void FixedUpdate()
    {
        if (movementMode == MovementMode.SplineTracking)
            TickSplineTracking();
    }

    private void TickPlayerSpace()
    {
        if (_turretRail == null || _playerRail == null) return;

        Vector3 origin = GetRayOrigin();
        Vector3 target = _playerRail.InterpolatedSplinePosition
            + _playerRail.InterpolatedSplineRight * _playerRightOffset
            + _playerRail.InterpolatedSplineUp * _playerUpOffset;

        Vector3 toTarget = target - origin;
        float totalDist = toTarget.magnitude;
        Vector3 dir = totalDist > 0.001f ? toTarget / totalDist : transform.forward;

        _rayProgress += speed * Time.deltaTime;

        transform.position = origin + dir * _rayProgress;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        Debug.DrawLine(origin, target, Color.red);
        Debug.DrawLine(origin, transform.position, Color.yellow);
    }

    private void TickSplineTracking()
    {
        if (_laneRail == null) return;

        _laneSplineT = Mathf.Repeat(
            _laneSplineT + speed * Time.fixedDeltaTime / _laneRail.splineLength, 1f);

        var (pos, fwd, up, right) = _laneRail.EvaluateAt(_laneSplineT);

        rb.MovePosition(pos + right * _laneRight + up * _laneUp);
        if (fwd.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.LookRotation(fwd*-1, up));
    }

    protected virtual void OnTriggerEnter(Collider other) => HandleHit(other);

    protected void Impact()
    {
        if (impactHappened) return;
        impactHappened = true;
        ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag);
    }

    void OnDisable()
    {
        CancelInvoke();
        if (rb != null && rb.isKinematic != true) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    protected void HandleHit(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;
        other.GetComponent<EntityHealthController>()?.TakeDamage(damage, true);
        Impact();
    }
}