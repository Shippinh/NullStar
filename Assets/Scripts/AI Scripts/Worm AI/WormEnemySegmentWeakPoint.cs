using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WormEnemySegmentWeakPoint : MonoBehaviour
{
    [Header("Target & Aiming")]
    public SpaceShooterController player;
    public LineRenderer aimLine;
    public float aimSmoothing = 2f;
    public LayerMask hitMask;

    private Vector3 aimedDir;

    void Start()
    {
        if (!aimLine) aimLine = GetComponent<LineRenderer>();
        aimLine.positionCount = 2;
    }

    void Update()
    {
        if (!player || !aimLine) return;

        Vector3 playerPos = player.transform.position;
        Vector3 playerVel = player.body ? player.body.velocity : Vector3.zero;

        float projectileSpeed = 300f;
        float distance = Vector3.Distance(transform.position, playerPos);
        float timeToHit = distance / projectileSpeed;
        Vector3 predictedPos = playerPos + playerVel * timeToHit;

        aimedDir = (predictedPos - transform.position).normalized;

        // Rotate smoothly toward aim
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(aimedDir),
            Time.deltaTime * aimSmoothing
        );

        // Update line renderer
        aimLine.enabled = true;
        aimLine.SetPosition(0, transform.position);
        if (Physics.Raycast(transform.position, aimedDir, out RaycastHit hit, 1200f, hitMask))
        {
            aimLine.SetPosition(1, hit.point);
        }
        else
        {
            aimLine.SetPosition(1, transform.position + aimedDir * 1200f);
        }
    }
}
