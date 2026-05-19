using UnityEngine;

public class ScriptedInflatableProjectile : InflatableEnemyProjectile
{
    [Header("Scripted Homing")]
    public float travelDuration = 2f;       // how long to reach the target
    public float snapDistance = 0.5f;

    private Vector3 targetPosition;
    private bool homing = false;
    private float travelTimer = 0f;


    public void SetTarget(Vector3 worldPos)
    {
        targetPosition = worldPos;
        homing = true;
        travelTimer = 0f;
    }

    void FixedUpdate()
    {
        if (!homing) return;

        travelTimer += Time.fixedDeltaTime;

        Vector3 toTarget = targetPosition - rb.position;
        float distance = toTarget.magnitude;

        if (distance <= snapDistance)
        {
            rb.velocity = Vector3.zero;
            rb.position = targetPosition;
            homing = false;
            state = SequencedProjectileState.SequenceStart;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            return;
        }

        // Same as HandleBoostAttach — required velocity to arrive by deadline
        float remainingTime = Mathf.Max(travelDuration - travelTimer, Time.fixedDeltaTime);
        rb.velocity = toTarget / remainingTime;
    }

    public override void HandleState(float t)
    {
        if (homing) return;
        base.HandleState(t);
    }

    public override void HandleDepool(string poolableTag, Vector3 position, Quaternion rotation)
    {
        base.HandleDepool(poolableTag, position, rotation);
        homing = false;
        travelTimer = 0f;
        state = SequencedProjectileState.Idle;
    }

    public new void OnCollisionEnter(Collision collision) { }
}