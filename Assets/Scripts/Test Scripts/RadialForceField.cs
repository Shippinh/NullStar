using UnityEngine;

public class RadialForceField : MonoBehaviour
{
    public float radius = 10f;
    public float forceStrength = 50f;
    public bool attract = false; // Set to true to attract instead of repel
    public ForceMode forceMode = ForceMode.Force;
    public LayerMask affectedLayers;
    public Color gizmoColor = new Color(0.5f, 0.8f, 1f, 0.25f);
    public bool runUpdate = true;
    [Range(0,1)] public float playerForceMultiplier = 0.2f;


    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (!runUpdate) return;

        ApplyForceTick();
    }

    public void ApplyForceTick()
    {
        Collider[] affected = Physics.OverlapSphere(transform.position, radius, affectedLayers);
        foreach (Collider col in affected)
        {
            Rigidbody hitRB = col.attachedRigidbody;
            if (hitRB != null && hitRB != rb)
            {
                Vector3 direction = (hitRB.position - transform.position).normalized;
                if (attract)
                    direction = -direction;

                hitRB.AddForce(direction * forceStrength, forceMode);
            }
        }
    }

    public void ApplyForceTick(ForceMode forceModePtr)
    {
        Collider[] affected = Physics.OverlapSphere(transform.position, radius, affectedLayers);
        foreach (Collider col in affected)
        {
            Rigidbody hitRB = col.attachedRigidbody;
            if (hitRB != null && hitRB != rb)
            {
                Vector3 direction = (hitRB.position - transform.position).normalized;
                if (attract)
                    direction = -direction;

                hitRB.AddForce(direction * forceStrength, forceModePtr);
            }
        }
    }

    public void ApplyForceTick(bool makePlayerLessAffected)
    {
        Collider[] affected = Physics.OverlapSphere(transform.position, radius, affectedLayers);
        foreach (Collider col in affected)
        {
            Rigidbody hitRB = col.attachedRigidbody;
            if (hitRB != null && hitRB != rb)
            {
                Vector3 direction = (hitRB.position - transform.position).normalized;
                if (attract)
                    direction = -direction;

                if(col.gameObject.layer.ToString() == "Player" && makePlayerLessAffected)
                    hitRB.AddForce((direction * forceStrength) * playerForceMultiplier, forceMode);
                else
                    hitRB.AddForce(direction * forceStrength, forceMode);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);

        // Optional: filled transparent sphere
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.5f);
        Gizmos.DrawSphere(transform.position, radius * 0.95f);
    }
}
