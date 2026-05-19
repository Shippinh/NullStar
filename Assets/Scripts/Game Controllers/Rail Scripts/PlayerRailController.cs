using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerRailController : RailController
{

    [Header("References")]
    public SpaceShooterController playerRef;
    public Rigidbody body;

    [Header("Speed Vars")]
    public float defaultSplineSpeed;
    public FloatRef currentSplineSpeed;

    public float defaultSidewaysPlaneMaxSpeed;
    public FloatRef currentSidewaysPlaneMaxSpeed;

    public float defaultUpwardPlaneMaxSpeed;
    public FloatRef currentUpwardPlaneMaxSpeed;

    public float defaultPlaneAcceleration;
    public FloatRef currentPlaneAcceleration;

    public float defaultDodgeSpeed;
    public FloatRef currentDodgeSpeed;

    [Header("Plane Offset Vars")]
    public float defaultSidewaysOffset;
    public FloatRef currentSidewaysOffset;

    public float defaultUpwardOffset;
    public FloatRef currentUpwardOffset;

    [Header("Fade Controllers")]
    public RailSpeedController boostModeSpeedFade;
    public RailSpeedController boostModeSidewaySplineMaxSpeedFade;
    public RailSpeedController boostModeUpwardSplineMaxSpeedFade;
    public RailSpeedController boostModeAccelerationFade;
    public RailSpeedController boostModeDodgeSpeedFade;
    public RailOffsetController boostModeOffsetsFade;

    public override void Awake()
    {
        InitializeSpline();

        if (!playerRef)
            playerRef = GetComponent<SpaceShooterController>();

        if (!body)
            body = playerRef.body;

        InitializeFades();

        // Evaluate once so interpolation buffers start with valid data
        if (splineContainer)
        {
            InitializeSplineValues();
            GetOptimalResolution();
        }
    }

    public void InitializeFades()
    {
        // Forward speed
        defaultSplineSpeed = MaxSpeed;
        currentSplineSpeed.value = defaultSplineSpeed;
        boostModeSpeedFade = new RailSpeedController(currentSplineSpeed, defaultSplineSpeed);

        // Sideways plane max speed — sourced from SpaceShooterController
        defaultSidewaysPlaneMaxSpeed = playerRef.maxBoostHorizontalStrafeSpeed;
        currentSidewaysPlaneMaxSpeed.value = defaultSidewaysPlaneMaxSpeed;
        boostModeSidewaySplineMaxSpeedFade = new RailSpeedController(currentSidewaysPlaneMaxSpeed, defaultSidewaysPlaneMaxSpeed);

        // Upward plane max speed — sourced from SpaceShooterController
        defaultUpwardPlaneMaxSpeed = playerRef.maxBoostVerticalStrafeSpeed;
        currentUpwardPlaneMaxSpeed.value = defaultUpwardPlaneMaxSpeed;
        boostModeUpwardSplineMaxSpeedFade = new RailSpeedController(currentUpwardPlaneMaxSpeed, defaultUpwardPlaneMaxSpeed);

        // Plane acceleration — sourced from SpaceShooterController
        defaultPlaneAcceleration = playerRef.maxBoostAcceleration;
        currentPlaneAcceleration.value = defaultPlaneAcceleration;
        boostModeAccelerationFade = new RailSpeedController(currentPlaneAcceleration, defaultPlaneAcceleration);

        defaultDodgeSpeed = playerRef.boostDodgeMaxSpeed;
        currentDodgeSpeed.value = defaultDodgeSpeed;
        boostModeDodgeSpeedFade = new RailSpeedController (currentDodgeSpeed, defaultDodgeSpeed);

        // Offsets
        defaultSidewaysOffset = maxSidewaysOffset;
        currentSidewaysOffset.value = defaultSidewaysOffset;
        defaultUpwardOffset = maxUpwardOffset;
        currentUpwardOffset.value = defaultUpwardOffset;
        boostModeOffsetsFade = new RailOffsetController(
            currentSidewaysOffset, defaultSidewaysOffset,
            currentUpwardOffset, defaultUpwardOffset);
    }

    public void InitializeSplineValues()
    {
        EvaluateSpline();
        InitializeInterpolationBuffers();
    }

    public void Update()
    {
        if (playerRef.playerState != PlayerState.BoostActive) return;

        UpdateRailSpeed(Time.deltaTime);
        UpdateRailOffsets(Time.deltaTime);
        UpdateInterpolatedSpline();
    }

    public void FixedUpdate()
    {
        if (playerRef.playerState != PlayerState.BoostActive) return;

        SnapshotSplineForInterpolation();

        TickSpline(Time.fixedDeltaTime);
        EvaluateSpline();

        CommitSplineToInterpolation();

        ApplyOffsetBounds();

        Vector3 targetPosition = SplinePosition
            + SplineRight * playerRef.currentRightOffset
            + SplineUp * playerRef.currentUpOffset;

        body.MovePosition(targetPosition);
        body.MoveRotation(SplineRotation);
    }

    public void UpdateRailSpeed(float dt)
    {
        // Forward speed
        MaxSpeed = currentSplineSpeed.value;
        boostModeSpeedFade.Update(dt);

        // Strafe speeds and acceleration — tick fades then write back to SpaceShooterController
        boostModeSidewaySplineMaxSpeedFade.Update(dt);
        playerRef.maxBoostHorizontalStrafeSpeed = currentSidewaysPlaneMaxSpeed.value;

        boostModeUpwardSplineMaxSpeedFade.Update(dt);
        playerRef.maxBoostVerticalStrafeSpeed = currentUpwardPlaneMaxSpeed.value;

        boostModeAccelerationFade.Update(dt);
        playerRef.maxBoostAcceleration = currentPlaneAcceleration.value;

        boostModeDodgeSpeedFade.Update(dt);
        playerRef.boostDodgeMaxSpeed = currentDodgeSpeed.value;
    }

    public void UpdateRailOffsets(float dt)
    {
        boostModeOffsetsFade.Update(dt);

        // Push live FloatRef values into the rail limits so AdjustBoostVelocity
        // always clamps against the current (possibly mid-transition) bounds
        maxSidewaysOffset = currentSidewaysOffset.value;
        maxUpwardOffset = currentUpwardOffset.value;
    }

    // Rescales and clamps the player's current offsets to match any change in
    // the bounds that happened this frame. Called from FixedUpdate so physics
    // positioning uses up-to-date values.
    void ApplyOffsetBounds()
    {
        float newSideways = currentSidewaysOffset.value;
        float newUpward = currentUpwardOffset.value;

        // Proportional rescale so the player's relative position is preserved
        if (maxSidewaysOffset > 0f)
            playerRef.currentRightOffset *= newSideways / maxSidewaysOffset;
        if (maxUpwardOffset > 0f)
            playerRef.currentUpOffset *= newUpward / maxUpwardOffset;

        // Clamp to new bounds (handles hard snaps and floating-point drift)
        playerRef.currentRightOffset = Mathf.Clamp(playerRef.currentRightOffset, -newSideways, newSideways);
        playerRef.currentUpOffset = Mathf.Clamp(playerRef.currentUpOffset, -newUpward, newUpward);
    }

    public void GetOptimalResolution()
    {
        float distancePerTick = currentSplineSpeed.value * Time.fixedDeltaTime;
        float optimalResolution = splineLength / distancePerTick;
        Debug.Log("Optimal resolution = " + optimalResolution);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector3 center = InterpolatedSplinePosition;
        Quaternion rot = InterpolatedSplineRotation;

        Vector3 right = rot * Vector3.right * maxSidewaysOffset;
        Vector3 up = rot * Vector3.up * maxUpwardOffset;

        Vector3 p1 = center + right + up;
        Vector3 p2 = center + right - up;
        Vector3 p3 = center - right - up;
        Vector3 p4 = center - right + up;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        Gizmos.DrawRay(center, rot * Vector3.forward * 2f);
    }

    // ── Shared ref wrapper ────────────────────────────────────────────────────

    [System.Serializable]
    public class FloatRef
    {
        public float value;
    }

    // ── RailSpeedController ───────────────────────────────────────────────────

    public class RailSpeedController
    {
        FloatRef currentSpeedRef;
        float speedTarget;
        float speedSpeed;
        bool speedRunning;
        float defaultSpeed;

        public RailSpeedController(FloatRef currentSpeedPtr, float defaultSpeedPtr)
        {
            currentSpeedRef = currentSpeedPtr;
            defaultSpeed = defaultSpeedPtr;
        }

        public void SetSpeedOverTime(float target, float duration)
        {
            if (duration <= 0f)
            {
                currentSpeedRef.value = target;
                speedRunning = false;
                return;
            }

            speedTarget = target;
            speedSpeed = (target - currentSpeedRef.value) / duration;
            speedRunning = true;
        }

        public void ResetToDefault(float duration = 0f)
        {
            SetSpeedOverTime(defaultSpeed, duration);
        }

        public void Update(float dt)
        {
            if (!speedRunning) return;

            float delta = speedSpeed * dt;
            float remaining = speedTarget - currentSpeedRef.value;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                currentSpeedRef.value = speedTarget;
                speedRunning = false;
            }
            else
            {
                currentSpeedRef.value += delta;
            }
        }
    }

    // ── RailOffsetController ──────────────────────────────────────────────────

    public class RailOffsetController
    {
        FloatRef _sidewaysRef;
        float _sidewaysTarget;
        float _sidewaysSpeed;
        bool _sidewaysRunning;
        float _defaultSideways;

        FloatRef _upwardRef;
        float _upwardTarget;
        float _upwardSpeed;
        bool _upwardRunning;
        float _defaultUpward;

        public RailOffsetController(FloatRef sidewaysRef, float defaultSideways, FloatRef upwardRef, float defaultUpward)
        {
            _sidewaysRef = sidewaysRef;
            _defaultSideways = defaultSideways;
            _sidewaysTarget = defaultSideways;

            _upwardRef = upwardRef;
            _defaultUpward = defaultUpward;
            _upwardTarget = defaultUpward;
        }

        public void SetOffsetOverTime(float targetSideways, float targetUpward, float duration)
        {
            targetSideways = Mathf.Max(0f, targetSideways);
            targetUpward = Mathf.Max(0f, targetUpward);

            if (duration <= 0f)
            {
                _sidewaysRef.value = targetSideways;
                _upwardRef.value = targetUpward;
                _sidewaysRunning = false;
                _upwardRunning = false;
                return;
            }

            _sidewaysTarget = targetSideways;
            _sidewaysSpeed = (targetSideways - _sidewaysRef.value) / duration;
            _sidewaysRunning = !Mathf.Approximately(_sidewaysRef.value, targetSideways);

            _upwardTarget = targetUpward;
            _upwardSpeed = (targetUpward - _upwardRef.value) / duration;
            _upwardRunning = !Mathf.Approximately(_upwardRef.value, targetUpward);
        }

        public void ResetToDefault(float duration = 0f) => SetOffsetOverTime(_defaultSideways, _defaultUpward, duration);

        public void Update(float dt)
        {
            if (!_sidewaysRunning && !_upwardRunning) return;

            if (_sidewaysRunning)
            {
                float delta = _sidewaysSpeed * dt;
                float remaining = _sidewaysTarget - _sidewaysRef.value;

                if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
                {
                    _sidewaysRef.value = _sidewaysTarget;
                    _sidewaysRunning = false;
                }
                else
                {
                    _sidewaysRef.value += delta;
                }
            }

            if (_upwardRunning)
            {
                float delta = _upwardSpeed * dt;
                float remaining = _upwardTarget - _upwardRef.value;

                if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
                {
                    _upwardRef.value = _upwardTarget;
                    _upwardRunning = false;
                }
                else
                {
                    _upwardRef.value += delta;
                }
            }
        }
    }
}