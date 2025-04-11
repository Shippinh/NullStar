using UnityEngine;

public class RadialForceField : MonoBehaviour
{
    public float radius = 10f;
    public float forceStrength = 50f;
    public bool attract = false; // Set to true to attract instead of repel
    public Color gizmoColor = new Color(0.5f, 0.8f, 1f, 0.25f);

    void FixedUpdate()
    {
        Collider[] affected = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider col in affected)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && rb != this.GetComponent<Rigidbody>())
            {
                Vector3 direction = (rb.position - transform.position).normalized;
                if (attract)
                    direction = -direction;
                    
                rb.AddForce(direction * forceStrength, ForceMode.Force);
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
