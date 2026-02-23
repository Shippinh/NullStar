using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public class RailEventController : MonoBehaviour
{
    [Header("References")]
    public PlayerRailController railControllerRef;

    [Header("Events")]
    [SerializeField] public List<RailEvent> eventList = new List<RailEvent>();
    [NonSerialized] public List<RailEvent> eventExecutionQueue = new List<RailEvent>();

    private int nextEventIndex;
    private float lastT = 0f;

    private void Awake()
    {
        nextEventIndex = 0;
        eventList.Sort((a, b) => a.eventTimelinePosition.CompareTo(b.eventTimelinePosition));
    }


    private void Update()
    {
        float currentT = railControllerRef.splineT;

        // detect loop wrap
        if (currentT < lastT)
        {
            nextEventIndex = 0; // reset event index on loop
        }

        EvaluateQueue();
        ExecuteQueue();

        lastT = currentT;
    }

    private void EvaluateQueue()
    {
        if (eventList == null || eventList.Count == 0)
            return;

        while (nextEventIndex < eventList.Count &&
               eventList[nextEventIndex].eventTimelinePosition <= lastT)
        {
            eventExecutionQueue.Add(eventList[nextEventIndex]);
            nextEventIndex++;
        }
    }

    private void ExecuteQueue()
    {
        for (int i = 0; i < eventExecutionQueue.Count; i++)
        {
            ExecuteEvent(eventExecutionQueue[i]);
        }

        eventExecutionQueue.Clear();
    }

    private void ExecuteEvent(RailEvent railEvent)
    {
        if (!railEvent.canShoot)
            return;

        switch (railEvent.type)
        {
            case RailEventType.ChangePlayerSpeed:
                railControllerRef.boostModeSpeedFade.SetSpeedOverTime(railEvent.intPtr, railEvent.floatPtr);
                break;

            case RailEventType.EnableObject:
                if (railEvent.objectParam is GameObject go)
                    go.SetActive(true);
                break;
        }

        // Optional: reset canShoot
        railEvent.canShoot = railEvent.canShootAgain;
    }

#if UNITY_EDITOR
    const float labelSpacingPx = 16f;
    const float verticalOffsetPerEvent = 0.25f; // offset for multiple events

    void OnDrawGizmos()
    {
        if (!railControllerRef || eventList == null || railControllerRef.splineContainer == null)
            return;

        var spline = railControllerRef.splineContainer.Spline;
        var l2w = railControllerRef.transform.localToWorldMatrix;

        var grouped = GroupEvents(eventList);

        foreach (var group in grouped)
        {
            float t = group.Key;
            var eventsAtT = group.Value;

            Vector3 baseWorldPos = railControllerRef.splineContainer.transform.TransformPoint(spline.EvaluatePosition(t));

            for (int i = 0; i < eventsAtT.Count; i++)
            {
                Vector3 eventPos = baseWorldPos;

                // Offset slightly along local up vector so cubes don't overlap
                eventPos += Vector3.up * verticalOffsetPerEvent * i;

                DrawMarker(eventPos, t);
                DrawLabels(eventPos + Vector3.up * 5f, eventsAtT[i].type.ToString(), i);
            }
        }
    }

    static Dictionary<float, List<RailEvent>> GroupEvents(List<RailEvent> events)
    {
        const float epsilon = 0.0001f;
        var dict = new Dictionary<float, List<RailEvent>>();

        foreach (var e in events)
        {
            bool placed = false;

            foreach (var key in dict.Keys)
            {
                if (Mathf.Abs(key - e.eventTimelinePosition) < epsilon)
                {
                    dict[key].Add(e);
                    placed = true;
                    break;
                }
            }

            if (!placed)
                dict[e.eventTimelinePosition] = new List<RailEvent> { e };
        }

        return dict;
    }

    // Draw a cube with a pseudo-random color based on t
    static void DrawMarker(Vector3 pos, float t)
    {
        UnityEngine.Random.InitState(Mathf.FloorToInt(t * 1000f));
        Color c = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        Gizmos.color = c;
        Gizmos.DrawSphere(pos, 2.5f);
    }

    // Draw a label for a single event
    static void DrawLabels(Vector3 worldPos, string text, int index)
    {
        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(worldPos);

        Handles.BeginGUI();

        float yOffset = labelSpacingPx * index; // stack labels for multiple events

        GUIContent content = new GUIContent(text);
        Vector2 size = GUI.skin.label.CalcSize(content);

        Rect rect = new Rect(
            guiPoint.x - size.x * 0.5f,
            guiPoint.y - size.y * 0.5f - yOffset, // offset up
            size.x,
            size.y
        );

        GUI.Label(rect, content);

        Handles.EndGUI();
    }
#endif
}
