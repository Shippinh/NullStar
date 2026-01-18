using System;
using System.Collections.Generic;
using UnityEngine;

public class RailEventController : MonoBehaviour
{
    [Header("References")]
    public RailController railControllerRef;

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
}
