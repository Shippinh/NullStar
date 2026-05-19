using UnityEngine;

[System.Serializable]
public class FormationPlane
{
    [Tooltip("Root transform containing the grouped placeholder hierarchy")]
    public Transform root;

    // Flattens all leaf transforms (no children) under root into world positions
    public Vector3[] GetPositions()
    {
        if (root == null) return System.Array.Empty<Vector3>();
        var positions = new System.Collections.Generic.List<Vector3>();
        CollectLeaves(root, positions);
        return positions.ToArray();
    }

    private void CollectLeaves(Transform t, System.Collections.Generic.List<Vector3> results)
    {
        if (t.childCount == 0)
        {
            results.Add(t.position);
            return;
        }
        for (int i = 0; i < t.childCount; i++)
            CollectLeaves(t.GetChild(i), results);
    }
}