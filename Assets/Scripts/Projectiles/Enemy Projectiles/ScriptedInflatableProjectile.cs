using UnityEngine;

public class ScriptedInflatableProjectile : InflatableEnemyProjectile
{
    [Header("Scripted Homing")]
    public float travelDuration = 2f;
    public float snapDistance = 0.5f;

    private Vector3 homingTarget;
    private bool isHoming = false;
    private float homingTimer = 0f;


    public void SetTarget(Vector3 worldPos)
    {
        homingTarget = worldPos;
        isHoming = true;
        homingTimer = 0f;
    }

    public override void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        base.HandleDepool(poolableTag, position, rotation);
        isHoming = false;
        homingTimer = 0f;
        state = SequencedProjectileState.Idle;
    }

    public override void HandleRepool()
    {
        base.HandleRepool();
        isHoming = false;
        homingTimer = 0f;
    }

    protected override void Update()
    {
        // Inflation only runs after homing completes
        if (!isHoming)
            base.Update();
    }

    protected override void FixedUpdate()
    {
        if (!isHoming) return;

        Vector3 toTarget = homingTarget - rb.position;
        float distance = toTarget.magnitude;

        if (distance <= snapDistance)
        {
            rb.velocity = Vector3.zero;
            rb.position = homingTarget;
            isHoming = false;
            state = SequencedProjectileState.SequenceStart;
            return;
        }

        homingTimer += Time.fixedDeltaTime;
        float remainingTime = Mathf.Max(travelDuration - homingTimer, Time.fixedDeltaTime);
        rb.velocity = toTarget / remainingTime;
    }

    // Swallow collisions during flight
    public override void OnTriggerEnter(Collider other) => HandleHit(other);
    public override void OnCollisionEnter(Collision collision)
    {
        //HandleHit(collision.gameObject.GetComponent<Collider>());
    }
}