using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using System.Collections.Generic;

public class SplinePreprocessTool : EditorWindow
{
    SplineContainer sourceSpline;
    int sampleCount = 200;
    int smoothIterations = 5;
    float smoothStrength = 0.5f;

    [MenuItem("Tools/Spline Preprocess Tool")]
    static void Open()
    {
        GetWindow<SplinePreprocessTool>("Spline Preprocess");
    }

    void OnGUI()
    {
        sourceSpline = (SplineContainer)EditorGUILayout.ObjectField(
            "Source Spline",
            sourceSpline,
            typeof(SplineContainer),
            true);

        sampleCount = EditorGUILayout.IntField("Sample Count", sampleCount);
        smoothIterations = EditorGUILayout.IntField("Smooth Iterations", smoothIterations);
        smoothStrength = EditorGUILayout.Slider("Smooth Strength", smoothStrength, 0f, 1f);

        if (GUILayout.Button("Generate Smoothed Spline"))
        {
            Generate();
        }
    }

    void Generate()
    {
        if (!sourceSpline) return;

        List<Vector3> points = new List<Vector3>();

        // Sample spline
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);
            points.Add(sourceSpline.Spline.EvaluatePosition(t));
        }

        // Laplacian smoothing
        for (int iter = 0; iter < smoothIterations; iter++)
        {
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 prev = points[i - 1];
                Vector3 next = points[i + 1];
                Vector3 avg = (prev + next) * 0.5f;

                points[i] = Vector3.Lerp(points[i], avg, smoothStrength);
            }
        }

        // Create new spline object
        GameObject go = new GameObject(sourceSpline.name + "_Smoothed");
        SplineContainer container = go.AddComponent<SplineContainer>();

        Spline spline = new Spline();

        foreach (var p in points)
        {
            spline.Add(new BezierKnot(p));
        }

        container.Spline = spline;

        Selection.activeGameObject = go;

        Debug.Log("Smoothed spline generated.");
    }
}