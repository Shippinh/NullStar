    using UnityEngine;

    public class SimpleEnemyProjectile : MonoBehaviour, IPoolable
    {
        public enum MovementMode { Default, PlayerSpace, SplineTracking }

        public string IPoolableTag { get; set; }

        [Header("Movement")]
        public float speed = 400f;
        public float maxLifetime = 2f;
        public MovementMode movementMode = MovementMode.Default;
        public bool overrideDefaultSpeedOnPlayerSpace = true;

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

        private Vector3 _originLocal;
        private Vector3 _targetLocal;

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

        public void InitializeInPlayerSpace(EnemyRailController turretRail, Vector3 turretLocalOffset,
        RailController playerRail, Vector3 spawnPos, float rightOffset, float upOffset)
        {
            _playerRail = playerRail;
            rb.isKinematic = true;

            Vector3 origin = turretRail.transform.position + turretRail.transform.rotation * turretLocalOffset;
            Vector3 target = playerRail.InterpolatedSplinePosition
                + playerRail.InterpolatedSplineRight * rightOffset
                + playerRail.InterpolatedSplineUp * upOffset;

            _originLocal = WorldToPlayerLocal(origin, playerRail);
            _targetLocal = WorldToPlayerLocal(target, playerRail);

            _rayProgress = Vector3.Distance(origin, spawnPos);
        }

        private Vector3 WorldToPlayerLocal(Vector3 worldPos, RailController rail)
        {
            Vector3 offset = worldPos - rail.InterpolatedSplinePosition;
            return new Vector3(
                Vector3.Dot(offset, rail.InterpolatedSplineRight),
                Vector3.Dot(offset, rail.InterpolatedSplineUp),
                Vector3.Dot(offset, rail.InterpolatedSplineForward));
        }

        private Vector3 PlayerLocalToWorld(Vector3 local, RailController rail)
        {
            return rail.InterpolatedSplinePosition
                + rail.InterpolatedSplineRight * local.x
                + rail.InterpolatedSplineUp * local.y
                + rail.InterpolatedSplineForward * local.z;
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            CancelInvoke();
            Invoke(nameof(Impact), maxLifetime);
            gameObject.SetActive(true);
        }

        public virtual void HandleRepool()
        {
            CancelInvoke();
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
        }

        public void Initialize(Vector3 startPosition, Vector3 targetPosition)
        {
            if (movementMode != MovementMode.Default) return;
            direction = (targetPosition - startPosition).normalized;
            rb.linearVelocity = direction * speed;
        }

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
            if (_playerRail == null) return;

            Vector3 origin = PlayerLocalToWorld(_originLocal, _playerRail);
            Vector3 target = PlayerLocalToWorld(_targetLocal, _playerRail);

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

        // These determine which logic we use for hit detection
        public virtual void OnTriggerEnter(Collider other) => HandleHit(other);

        public virtual void OnCollisionEnter(Collision other) { }

        protected void Impact()
        {
            if (impactHappened) return;
            impactHappened = true;
            ObjectPool.Instance.ReturnToPool(gameObject, IPoolableTag);
        }

        void OnDisable()
        {
            CancelInvoke();
            if (rb != null && rb.isKinematic != true) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        protected void HandleHit(Collider other)
        {
            if (((1 << other.gameObject.layer) & hitLayers) == 0) return;
            //other.GetComponent<EntityHealthController>()?.TakeDamage(damage, true);
            Debug.Log("Projectile hit on ["+ other.gameObject.name +"]");
        
            Impact();
        }
    }