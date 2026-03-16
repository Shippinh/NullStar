using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using static Unity.VisualScripting.FlowStateWidget;

[RequireComponent(typeof(SplineArcLengthTable))]
public class RailEventController : MonoBehaviour
{
    [Header("References")]
    public PlayerRailController railControllerRef;

    [Header("Point Events")]
    [SerializeReference] public List<RailEvent> events = new();

    [Header("Range Events")]
    [SerializeReference] public List<RailRangeEvent> rangeEvents = new();

    private float _lastT;

    private void Awake()
    {
        SortEvents();
        ResetAll();

        if (!railControllerRef)
            railControllerRef = FindObjectOfType<PlayerRailController>();
    }

    private void Update()
    {
        float t = railControllerRef.splineT;

        if (t < _lastT) // Loop wrap detected
        {
            // Fire any events that sat between lastT and the end of the loop
            FirePointEventsInRange(_lastT, 1f);
            ResetAll();
            // Then fire from zero up to the current position
            FirePointEventsInRange(0f, t);
            UpdateRangeEvents(t, justLooped: true);
        }
        else
        {
            FirePointEventsInRange(_lastT, t);
            UpdateRangeEvents(t, justLooped: false);
        }

        _lastT = t;
    }

    // Events are sorted on Awake so we can binary-search later if lists grow large.
    private void SortEvents() =>
        events.Sort((a, b) => a.t.CompareTo(b.t));

    // Fire all enabled events whose T falls strictly inside (from, to].
    private void FirePointEventsInRange(float from, float to)
    {
        foreach (var e in events)
        {
            if (!e.enabled) continue;
            if (e.t <= from || e.t > to) continue;
            if (e.hasFired && !e.repeatable) continue;

            e.Execute(railControllerRef);
            e.hasFired = !e.repeatable; // fire-once events latch; repeatable ones reset
        }
    }

    private void UpdateRangeEvents(float t, bool justLooped)
    {
        foreach (var e in rangeEvents)
        {
            if (!e.enabled) continue;

            if (justLooped) // Treat as if we left the zone cleanly before re-entering
            {
                if (e.isActive) { e.OnExit(railControllerRef); e.isActive = false; }
            }

            bool inRange = t >= e.tStart && t <= e.tEnd;

            if (inRange && !e.isActive)
            {
                e.isActive = true;
                e.OnEnter(railControllerRef);
            }
            else if (!inRange && e.isActive)
            {
                e.isActive = false;
                e.OnExit(railControllerRef);
            }
            else if (inRange)
            {
                e.OnUpdate(railControllerRef, t);
            }
        }
    }

    private void ResetAll()
    {
        _lastT = 0f;
        foreach (var e in events) e.hasFired = false;
        foreach (var e in rangeEvents) e.isActive = false;
    }

    // Allow external systems (e.g. a cutscene manager) to reset a single event
    public void ResetEvent(RailEvent e) => e.hasFired = false;

#if UNITY_EDITOR

    const float labelSpacingPx = 16f;
    const float verticalOffsetPerEvent = 0.25f;
    const float sphereSize = 0.5f;
    const float labelUpOffset = 0.75f;

    const bool showLabels = true;

    void OnDrawGizmos()
    {
        if (!railControllerRef || events == null || railControllerRef.splineContainer == null)
            return;

        var spline = railControllerRef.splineContainer.Spline;
        var container = railControllerRef.splineContainer.transform;

        var grouped = GroupEvents(events);

        Handles.BeginGUI();

        foreach (var group in grouped)
        {
            float t = group.Key;
            var eventsAtT = group.Value;

            Vector3 baseWorldPos = container.TransformPoint(
                spline.EvaluatePosition(t));

            for (int i = 0; i < eventsAtT.Count; i++)
            {
                Vector3 eventPos = baseWorldPos + Vector3.up * verticalOffsetPerEvent * i;

                DrawMarker(eventPos, t);

                if(showLabels)
                    DrawLabel(eventPos + Vector3.up * labelUpOffset, eventsAtT[i].EditorLabel.ToString(), i);
            }
        }

        Handles.EndGUI();
    }

    static Dictionary<float, List<RailEvent>> GroupEvents(List<RailEvent> events)
    {
        const float epsilon = 0.0001f;

        Dictionary<float, List<RailEvent>> dict =
            new Dictionary<float, List<RailEvent>>();

        foreach (var e in events)
        {
            float key = Mathf.Round(e.t / epsilon) * epsilon;

            if (!dict.ContainsKey(key))
                dict[key] = new List<RailEvent>();

            dict[key].Add(e);
        }

        return dict;
    }

    static void DrawMarker(Vector3 pos, float t)
    {
        UnityEngine.Random.InitState(Mathf.FloorToInt(t * 1000f));

        Color c = new Color(
            UnityEngine.Random.value,
            UnityEngine.Random.value,
            UnityEngine.Random.value
        );

        Gizmos.color = c;
        Gizmos.DrawSphere(pos, sphereSize);
    }

    static void DrawLabel(Vector3 worldPos, string text, int index)
    {
        SceneView sceneView = SceneView.currentDrawingSceneView;
        if (!sceneView) return;

        Camera cam = sceneView.camera;

        Vector3 camToPoint = worldPos - cam.transform.position;

        if (Vector3.Dot(cam.transform.forward, camToPoint) <= 0f)
            return;

        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(worldPos);

        float yOffset = labelSpacingPx * index;

        GUIContent content = new GUIContent(text);
        Vector2 size = GUI.skin.label.CalcSize(content);

        Rect rect = new Rect(
            guiPoint.x - size.x * 0.5f,
            guiPoint.y - size.y * 0.5f - yOffset,
            size.x,
            size.y
        );

        GUI.Label(rect, content);
    }

#endif
}