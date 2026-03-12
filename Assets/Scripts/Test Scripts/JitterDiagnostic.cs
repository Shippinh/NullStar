using UnityEngine;

public class JitterDiagnostic : MonoBehaviour
{
    Vector3 lastPosition;
    float lastDelta;

    void LateUpdate()
    {
        float delta = Vector3.Distance(transform.position, lastPosition);
        if (Mathf.Abs(delta - lastDelta) > 1.5f)
            Debug.Log($"Position spike: {delta} vs expected {lastDelta}");
        lastDelta = delta;
        lastPosition = transform.position;
    }
}