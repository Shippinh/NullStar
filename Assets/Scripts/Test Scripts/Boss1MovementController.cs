using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Boss1MovementController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform proxyTarget;
    public Transform bossModel;
    private Rigidbody bossRb;

    [Header("Proxy Target Behavior")]
    public float proxyFollowSpeed = 10f;
    public float proxyMinDistanceToPlayer = 3f;

    [Header("Boss Model Behavior")]
    public float bossFollowSpeed = 5f;
    public float maxDistanceFromProxy = 8f;
    public float stopDistanceThreshold = 0.1f;
    public float yStopDistanceThresholdModel = 0.1f;
    public float driftStrength = 1f;
    public float driftRadius = 2f;

    [Header("Proxy Y-Position Balance")]
    public float yBalanceOffset = 2f;
    public float proxyYFollowSpeed = 5f;
    public float yStopDistanceThreshold = 0.1f;

    void Start()
    {
        if (bossModel.TryGetComponent(out Rigidbody rb))
        {
            bossRb = rb;
            bossRb.useGravity = false;
            bossRb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        else
        {
            Debug.LogError("Boss model requires a Rigidbody.");
        }
    }

    void FixedUpdate()
    {
        if (player == null || proxyTarget == null || bossModel == null || bossRb == null) return;

        UpdateProxyTarget();
        UpdateBossModel();
    }

    void UpdateProxyTarget()
    {
        Vector3 toPlayer = player.position - proxyTarget.position;
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        float distanceToPlayer = flatToPlayer.magnitude;

        if (distanceToPlayer != proxyMinDistanceToPlayer)
        {
            Vector3 moveDir = flatToPlayer.normalized;
            float directionMultiplier = distanceToPlayer > proxyMinDistanceToPlayer ? 1f : -1f;

            float moveStep = proxyFollowSpeed * Time.fixedDeltaTime;
            float moveAmount = Mathf.Min(moveStep, Mathf.Abs(distanceToPlayer - proxyMinDistanceToPlayer));
            proxyTarget.position += new Vector3(moveDir.x, 0f, moveDir.z) * moveAmount * directionMultiplier;
        }

        float targetY = player.position.y + yBalanceOffset;
        float yDiff = Mathf.Abs(proxyTarget.position.y - targetY);
        if (yDiff > yStopDistanceThreshold)
        {
            float newY = Mathf.Lerp(proxyTarget.position.y, targetY, proxyYFollowSpeed * Time.fixedDeltaTime);
            proxyTarget.position = new Vector3(proxyTarget.position.x, newY, proxyTarget.position.z);
        }
        else
        {
            proxyTarget.position = new Vector3(proxyTarget.position.x, targetY, proxyTarget.position.z);
        }
    }

    void UpdateBossModel()
    {
        Vector3 toProxy = proxyTarget.position - bossModel.position;
        float distance = toProxy.magnitude;

        Vector3 desiredVelocity = Vector3.zero;

        // Smooth follow when outside the stop threshold
        if (distance > stopDistanceThreshold)
        {
            desiredVelocity = toProxy.normalized * bossFollowSpeed;
        }

        // Apply drift if within drift radius
        if (distance < driftRadius)
        {
            Vector3 drift = new Vector3(
                Mathf.PerlinNoise(Time.time * 0.5f, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, Time.time * 0.5f) - 0.5f,
                Mathf.PerlinNoise(Time.time * 0.5f, Time.time * 0.5f) - 0.5f
            );

            drift *= driftStrength;
            desiredVelocity += drift;
        }

        // Smooth velocity change
        bossRb.velocity = Vector3.Lerp(bossRb.velocity, desiredVelocity, Time.fixedDeltaTime * 2f);
    }
}
