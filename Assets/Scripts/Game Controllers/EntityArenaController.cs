using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EntityArenaController : MonoBehaviour
{
    [SerializeField, Range(1, 10)] public int waveToAppear = 1;
    [SerializeField] private bool showWaveGizmo = true; // toggle gizmo visibility
    [SerializeField] private Color gizmoColor = Color.yellow; // optional color

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showWaveGizmo) return;

        // Draw a small sphere marker (optional)
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position + Vector3.up * 0.2f, 0.05f);

        // Draw label slightly above the object
        Handles.color = gizmoColor;
        GUIStyle style = new GUIStyle();
        style.normal.textColor = gizmoColor;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        Handles.Label(transform.position + Vector3.up * 1.5f, $"Wave {waveToAppear}", style);
    }
#endif
}
