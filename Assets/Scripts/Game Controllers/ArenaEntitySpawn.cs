#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class ArenaEntitySpawn : MonoBehaviour
{
    [Header("Spawner Options")]
    public string poolableEnemyTag = "NaN";
    [Range(1, 10)] public int waveToAppear = 1;
    public bool additionalEnemy = false;

#if UNITY_EDITOR
    [Header("Editor Options")]
    [SerializeField] private bool showWaveGizmo = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float baseArrowLength = 1f;
    [SerializeField] private float baseArrowHeadSize = 0.25f;

    private void OnDrawGizmosSelected()
    {
        if (!showWaveGizmo) return;

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null) return;

        // Scale factor based on camera distance
        float scale = Vector3.Distance(sceneView.camera.transform.position, transform.position);

        // Adjust arrow size according to distance
        float arrowLength = baseArrowLength * scale * 0.2f;
        float arrowHeadSize = baseArrowHeadSize * scale * 0.15f;

        // Base gizmo
        Gizmos.color = gizmoColor;

        // Label
        Handles.color = gizmoColor;
        GUIStyle style = new GUIStyle
        {
            normal = { textColor = gizmoColor },
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        Handles.Label(transform.position + Vector3.up * 1.5f, $"Enemy {poolableEnemyTag}. Wave {waveToAppear}", style);

        // Arrow
        Vector3 start = transform.position;
        Vector3 end = start + transform.forward * arrowLength;
        Gizmos.DrawLine(start, end);

        // Arrowhead
        Vector3 right = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0, 150, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(0, -150, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * arrowHeadSize);
        Gizmos.DrawLine(end, end + left * arrowHeadSize);
    }
#endif
}
