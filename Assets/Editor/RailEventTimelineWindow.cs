using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RailEventTimelineWindow : EditorWindow
{
    private RailEventController _target;
    private SerializedObject _so;
    private SerializedProperty _pointsProp;
    private SerializedProperty _rangesProp;

    private int _selectedIndex = -1;
    private bool _selectedIsRange = false;
    private bool _isDragging = false;

    private Vector2 _inspectorScroll;
    private Vector2 _listScroll;

    private const float HEADER_H = 26f;
    private const float TIMELINE_H = 72f;
    private const float RANGE_TRACK_Y = 8f;
    private const float RANGE_TRACK_H = 14f;
    private const float RANGE_TRACK_GAP = 2f;
    private const float MARKER_Y = 52f;
    private const float MARKER_R = 7f;
    private const float INSPECTOR_W = 290f;
    private const float PAD = 12f;

    [MenuItem("Window/Rail Event Timeline")]
    public static void Open() => GetWindow<RailEventTimelineWindow>("Rail Timeline");

    private void OnEnable()
    {
        Selection.selectionChanged += Bind;
        Bind();
    }

    private void OnDisable() => Selection.selectionChanged -= Bind;

    private void Bind()
    {
        _target = null; _so = null;
        var go = Selection.activeGameObject;
        if (go) _target = go.GetComponent<RailEventController>();
        if (_target)
        {
            _so = new SerializedObject(_target);
            _pointsProp = _so.FindProperty("events");
            _rangesProp = _so.FindProperty("rangeEvents");
        }
        _selectedIndex = -1;
        Repaint();
    }

    private void OnGUI()
    {
        if (_target == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a RailEventController.", MessageType.Info);
            return;
        }

        _so.Update();

        float trackW = position.width - INSPECTOR_W - PAD * 3f;

        DrawHeader(new Rect(0, 0, position.width, HEADER_H));
        DrawTimeline(new Rect(PAD, HEADER_H + PAD, trackW, TIMELINE_H));
        DrawInspector(new Rect(trackW + PAD * 2f, HEADER_H + PAD, INSPECTOR_W, position.height - HEADER_H - PAD * 2f));
        DrawEventList(new Rect(PAD, HEADER_H + PAD + TIMELINE_H + PAD, trackW, position.height - HEADER_H - PAD * 3f - TIMELINE_H));

        _so.ApplyModifiedProperties();
    }


    private void DrawHeader(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.17f));
        GUI.Label(new Rect(r.x + 8f, r.y + 5f, 300f, 16f),
                  $"Rail Timeline  —  {_target.name}", EditorStyles.boldLabel);

        if (GUI.Button(new Rect(r.xMax - 152f, r.y + 3f, 72f, 20f), "+ Event"))
            ShowTypeMenu(_pointsProp, PointEventTypes);

        if (GUI.Button(new Rect(r.xMax - 76f, r.y + 3f, 70f, 20f), "+ Zone"))
            ShowTypeMenu(_rangesProp, RangeEventTypes);
    }

    private void DrawTimeline(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));
        DrawBorder(r, new Color(0.3f, 0.3f, 0.3f));
        DrawTicks(r);

        for (int i = 0; i < _rangesProp.arraySize; i++)
            if (_rangesProp.GetArrayElementAtIndex(i).managedReferenceValue is RailRangeEvent re)
                DrawRangeBand(r, i, re);

        // Two markers are "overlapping" when they're closer than this in pixels.
        const float overlapThresholdPx = MARKER_R * 2f + 6f;

        int count = _pointsProp.arraySize;
        int[] lanes = new int[count];

        for (int i = 0; i < count; i++)
        {
            if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailEvent ei) continue;

            // Find the lowest lane not already occupied by a nearby earlier marker
            var occupied = new System.Collections.Generic.HashSet<int>();
            for (int j = 0; j < i; j++)
            {
                if (_pointsProp.GetArrayElementAtIndex(j).managedReferenceValue is not RailEvent ej) continue;
                float dist = Mathf.Abs((ei.t - ej.t) * r.width);
                if (dist < overlapThresholdPx)
                    occupied.Add(lanes[j]);
            }
            int lane = 0;
            while (occupied.Contains(lane)) lane++;
            lanes[i] = lane;
        }

        for (int i = 0; i < count; i++)
            if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is RailEvent e)
                DrawPointMarker(r, i, e, lanes[i]);

        HandleTimelineInput(r, lanes);
    }

    private static void DrawBorder(Rect r, Color c)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
    }

    private static void DrawTicks(Rect r)
    {
        for (int i = 0; i <= 10; i++)
        {
            float x = r.x + (float)i / 10f * r.width;
            bool major = i % 5 == 0;
            EditorGUI.DrawRect(new Rect(x, r.y, 1f, major ? r.height : r.height * 0.3f),
                               major ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.25f, 0.25f, 0.25f));
            if (major)
                GUI.Label(new Rect(x + 2f, r.y + 2f, 30f, 12f), $"{(float)i / 10f:0.0}", EditorStyles.miniLabel);
        }
    }

    private void DrawRangeBand(Rect timeline, int index, RailRangeEvent e)
    {
        float x1 = timeline.x + Mathf.Clamp01(e.tStart) * timeline.width;
        float x2 = timeline.x + Mathf.Clamp01(e.tEnd) * timeline.width;
        float y = timeline.y + RANGE_TRACK_Y + index * (RANGE_TRACK_H + RANGE_TRACK_GAP);

        bool selected = _selectedIsRange && _selectedIndex == index;
        Color c = e.EditorColor;
        if (selected) c = Color.Lerp(c, Color.white, 0.45f);

        EditorGUI.DrawRect(new Rect(x1, y, Mathf.Max(x2 - x1, 4f), RANGE_TRACK_H), c);

        // Drag handles at each end
        DrawHandle(new Rect(x1 - 3f, y, 6f, RANGE_TRACK_H), c);
        DrawHandle(new Rect(x2 - 3f, y, 6f, RANGE_TRACK_H), c);

        GUI.Label(new Rect(x1 + 4f, y, x2 - x1 - 8f, RANGE_TRACK_H), e.EditorLabel, EditorStyles.miniLabel);
    }

    private static void DrawHandle(Rect r, Color c)
    {
        EditorGUI.DrawRect(r, Color.Lerp(c, Color.white, 0.6f));
    }

    private void DrawPointMarker(Rect timeline, int index, RailEvent e, int lane)
    {
        float x = timeline.x + Mathf.Clamp01(e.t) * timeline.width;
        float laneStep = MARKER_R * 2f + 4f;
        float baseY = timeline.y + MARKER_Y;
        float markerY = baseY - lane * laneStep;

        bool selected = !_selectedIsRange && _selectedIndex == index;
        Color c = selected ? Color.white : e.EditorColor;

        // Stem line connecting marker back down to the baseline
        if (lane > 0)
        {
            Handles.color = new Color(c.r, c.g, c.b, 0.45f);
            Handles.DrawLine(new Vector3(x, baseY), new Vector3(x, markerY + MARKER_R));
            Handles.color = Color.white;
        }

        // Diamond
        Handles.DrawSolidRectangleWithOutline(
            new Rect(x - MARKER_R * 0.7f, markerY - MARKER_R * 0.7f, MARKER_R * 1.4f, MARKER_R * 1.4f),
            c,
            selected ? Color.white : new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f));

        // Label
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
        GUI.Label(new Rect(x - 55f, markerY + MARKER_R + 2f, 110f, 14f), e.EditorLabel, style);
    }

    private void HandleTimelineInput(Rect timeline, int[] lanes)
    {
        Event ev = Event.current;

        if (ev.type == EventType.MouseDown && ev.button == 0 && timeline.Contains(ev.mousePosition))
        {
            //const float overlapThresholdPx = MARKER_R * 2f + 6f;
            float laneStep = MARKER_R * 2f + 4f;

            for (int i = 0; i < _pointsProp.arraySize; i++)
            {
                if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailEvent e) continue;

                float markerX = timeline.x + e.t * timeline.width;
                float markerY = timeline.y + MARKER_Y - lanes[i] * laneStep;

                if (Mathf.Abs(ev.mousePosition.x - markerX) < MARKER_R + 4f &&
                    Mathf.Abs(ev.mousePosition.y - markerY) < MARKER_R + 4f)
                {
                    Select(i, isRange: false);
                    _isDragging = true;
                    ev.Use();
                    return;
                }
            }

            // Range band hit-test (unchanged)
            for (int i = 0; i < _rangesProp.arraySize; i++)
            {
                if (_rangesProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailRangeEvent re) continue;

                float x1 = timeline.x + re.tStart * timeline.width;
                float x2 = timeline.x + re.tEnd * timeline.width;
                float bandY = timeline.y + RANGE_TRACK_Y + i * (RANGE_TRACK_H + RANGE_TRACK_GAP);

                if (ev.mousePosition.x >= x1 && ev.mousePosition.x <= x2 &&
                    ev.mousePosition.y >= bandY && ev.mousePosition.y <= bandY + RANGE_TRACK_H)
                {
                    Select(i, isRange: true);
                    _isDragging = true;
                    ev.Use();
                    return;
                }
            }

            _selectedIndex = -1;
            ev.Use();
            Repaint();
        }

        if (ev.type == EventType.MouseDrag && _isDragging && _selectedIndex >= 0 && !_selectedIsRange)
        {
            if (_selectedIndex < _pointsProp.arraySize)
            {
                var tProp = _pointsProp.GetArrayElementAtIndex(_selectedIndex).FindPropertyRelative("t");
                if (tProp != null)
                {
                    tProp.floatValue = Mathf.Clamp01((ev.mousePosition.x - timeline.x) / timeline.width);
                    ev.Use();
                    Repaint();
                }
            }
        }

        if (ev.type == EventType.MouseUp) _isDragging = false;

        if (ev.type == EventType.ContextClick && timeline.Contains(ev.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Point Event"), false, () => ShowTypeMenu(_pointsProp, PointEventTypes));
            menu.AddItem(new GUIContent("Add Range Event"), false, () => ShowTypeMenu(_rangesProp, RangeEventTypes));
            menu.ShowAsContext();
            ev.Use();
        }
    }

    private void DrawInspector(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));
        DrawBorder(r, new Color(0.28f, 0.28f, 0.28f));

        GUILayout.BeginArea(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12));

        if (_selectedIndex < 0)
        {
            GUILayout.FlexibleSpace();
            GUILayout.Label("No event selected", new GUIStyle(EditorStyles.centeredGreyMiniLabel));
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
            return;
        }

        var listProp = _selectedIsRange ? _rangesProp : _pointsProp;
        if (_selectedIndex >= listProp.arraySize) { GUILayout.EndArea(); return; }

        var prop = listProp.GetArrayElementAtIndex(_selectedIndex);
        if (prop.managedReferenceValue == null) { GUILayout.EndArea(); return; }

        string typeLabel = _selectedIsRange ? "Range Event" : "Point Event";
        GUILayout.Label($"{typeLabel}  ({prop.managedReferenceValue.GetType().Name})", EditorStyles.boldLabel);
        GUILayout.Space(4f);

        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
        EditorGUILayout.PropertyField(prop, GUIContent.none, includeChildren: true);
        GUILayout.EndScrollView();

        GUILayout.Space(6f);
        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button("Remove", GUILayout.Height(22f)))
        {
            listProp.DeleteArrayElementAtIndex(_selectedIndex);
            _so.ApplyModifiedProperties();
            _selectedIndex = -1;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndArea();
    }

    private void DrawEventList(Rect r)
    {
        GUILayout.BeginArea(r);
        _listScroll = GUILayout.BeginScrollView(_listScroll);

        GUILayout.Label("Point Events", EditorStyles.boldLabel);
        DrawListEntries(_pointsProp, isRange: false, e =>
        {
            var pe = (RailEvent)e;
            return ($"[{pe.t:0.00}]  {pe.EditorLabel}", pe.EditorColor);
        });

        GUILayout.Space(6f);
        GUILayout.Label("Range Events", EditorStyles.boldLabel);
        DrawListEntries(_rangesProp, isRange: true, e =>
        {
            var re = (RailRangeEvent)e;
            return ($"[{re.tStart:0.00} → {re.tEnd:0.00}]  {re.EditorLabel}", re.EditorColor);
        });

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawListEntries(SerializedProperty listProp, bool isRange,
                                 Func<object, (string label, Color color)> info)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var refVal = listProp.GetArrayElementAtIndex(i).managedReferenceValue;
            if (refVal == null) continue;

            var (label, color) = info(refVal);
            bool selected = isRange == _selectedIsRange && _selectedIndex == i;

            // Colored left stripe
            var rowRect = EditorGUILayout.GetControlRect(GUILayout.Height(20f));
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + 1f, 4f, rowRect.height - 2f), color);

            GUI.backgroundColor = selected ? new Color(0.3f, 0.55f, 1f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(new Rect(rowRect.x + 6f, rowRect.y, rowRect.width - 6f, rowRect.height), label,
                           new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft }))
                Select(i, isRange);

            GUI.backgroundColor = Color.white;
        }
    }

    private void ShowTypeMenu(SerializedProperty listProp, Type[] types)
    {
        var menu = new GenericMenu();
        foreach (var t in types)
        {
            var captured = t;
            menu.AddItem(new GUIContent(captured.Name), false, () =>
            {
                Undo.RecordObject(_target, $"Add {captured.Name}");
                listProp.arraySize++;
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).managedReferenceValue =
                    Activator.CreateInstance(captured);
                _so.ApplyModifiedProperties();
                Repaint();
            });
        }
        menu.ShowAsContext();
    }

    // append with events to add them to selection
    private static readonly Type[] PointEventTypes =
    {
        typeof(ChangeSpeedEvent),
        typeof(SetObjectActiveEvent),
    };

    private static readonly Type[] RangeEventTypes =
    {
        //typeof(SpeedZoneEvent),
    };

    private void Select(int index, bool isRange)
    {
        _selectedIndex = index;
        _selectedIsRange = isRange;
        Repaint();
    }
}