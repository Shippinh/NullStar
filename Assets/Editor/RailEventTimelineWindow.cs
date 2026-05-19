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
    private SerializedProperty _layersProp;

    private int _selectedIndex = -1;
    private bool _selectedIsRange = false;
    private bool _isDragging = false;

    private Vector2 _inspectorScroll;
    private Vector2 _listScroll;
    private Vector2 _layerScroll;

    // Active layer = the layer events are added to / filtered by.
    // -1 means "All Layers" (show everything, adds to layer 0).
    private int _activeLayerIndex = -1;

    private const float HEADER_H = 26f;
    private const float LAYER_W = 160f;
    private const float TIMELINE_H = 72f;
    private const float RANGE_TRACK_Y = 8f;
    private const float RANGE_TRACK_H = 14f;
    private const float RANGE_TRACK_GAP = 2f;
    private const float MARKER_Y = 52f;
    private const float MARKER_R = 7f;
    private const float INSPECTOR_W = 290f;
    private const float PAD = 12f;

    // Layer panel row heights
    private const float LAYER_ROW_H = 22f;
    private const float LAYER_SWATCH_W = 12f;

    [MenuItem("Window/Rail Event Timeline")]
    public static void Open() => GetWindow<RailEventTimelineWindow>("Rail Timeline");

    // ──────────────────────────────────────────────────────────────
    //  Lifecycle
    // ──────────────────────────────────────────────────────────────

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
            _layersProp = _so.FindProperty("layers");
        }
        _selectedIndex = -1;
        Repaint();
    }

    // ──────────────────────────────────────────────────────────────
    //  OnGUI root
    // ──────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (_target == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a RailEventController.", MessageType.Info);
            return;
        }

        _so.Update();

        // ┌─────────────┬─────────────────────────┬─────────────┐
        // │ Layer panel │     Timeline + List      │  Inspector  │
        // └─────────────┴─────────────────────────┴─────────────┘

        float totalW = position.width;
        float totalH = position.height;
        float midW = totalW - LAYER_W - INSPECTOR_W - PAD * 4f;

        float layerX = 0f;
        float timelineX = LAYER_W + PAD;
        float inspectorX = LAYER_W + PAD + midW + PAD;

        DrawHeader(new Rect(0, 0, totalW, HEADER_H));
        DrawLayerPanel(new Rect(layerX, HEADER_H, LAYER_W, totalH - HEADER_H));
        DrawTimeline(new Rect(timelineX, HEADER_H + PAD, midW, TIMELINE_H));
        DrawInspector(new Rect(inspectorX, HEADER_H + PAD, INSPECTOR_W, totalH - HEADER_H - PAD * 2f));
        DrawEventList(new Rect(timelineX, HEADER_H + PAD + TIMELINE_H + PAD, midW,
                               totalH - HEADER_H - PAD * 3f - TIMELINE_H));

        _so.ApplyModifiedProperties();
    }

    // ──────────────────────────────────────────────────────────────
    //  Header
    // ──────────────────────────────────────────────────────────────

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

    // ──────────────────────────────────────────────────────────────
    //  Layer Panel
    // ──────────────────────────────────────────────────────────────

    private void DrawLayerPanel(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
        DrawBorder(r, new Color(0.28f, 0.28f, 0.28f));

        // ── Title bar ──
        var titleRect = new Rect(r.x, r.y, r.width, 22f);
        EditorGUI.DrawRect(titleRect, new Color(0.19f, 0.19f, 0.19f));
        GUI.Label(new Rect(r.x + 6f, r.y + 4f, r.width - 32f, 14f), "LAYERS", EditorStyles.boldLabel);

        // Add layer button
        if (GUI.Button(new Rect(r.xMax - 24f, r.y + 2f, 20f, 18f), "+"))
            AddLayer();

        float listY = r.y + 22f;
        float listH = r.height - 22f - 2f;

        // ── "All Layers" row ──
        DrawLayerRow(new Rect(r.x, listY, r.width, LAYER_ROW_H), -1, "All Layers", Color.grey, ref listY);

        // ── Per-layer rows ──
        _layerScroll = GUI.BeginScrollView(
            new Rect(r.x, listY, r.width, listH - (listY - (r.y + 22f))),
            _layerScroll,
            new Rect(0, 0, r.width - 14f, _layersProp.arraySize * (LAYER_ROW_H + 1f)));

        for (int i = 0; i < _layersProp.arraySize; i++)
        {
            var layerProp = _layersProp.GetArrayElementAtIndex(i);
            var nameProp = layerProp.FindPropertyRelative("name");
            var colorProp = layerProp.FindPropertyRelative("color");
            var visibleProp = layerProp.FindPropertyRelative("visible");
            var lockedProp = layerProp.FindPropertyRelative("locked");

            float rowY = i * (LAYER_ROW_H + 1f);
            Rect rowRect = new Rect(0, rowY, r.width - 14f, LAYER_ROW_H);

            bool selected = _activeLayerIndex == i;
            EditorGUI.DrawRect(rowRect, selected ? new Color(0.25f, 0.4f, 0.65f) : new Color(0.18f, 0.18f, 0.18f));

            // Color swatch
            colorProp.colorValue = EditorGUI.ColorField(
                new Rect(4f, rowY + 3f, LAYER_SWATCH_W, LAYER_ROW_H - 6f),
                GUIContent.none, colorProp.colorValue, false, false, false);

            // Name field (inline edit when selected)
            if (selected)
            {
                nameProp.stringValue = EditorGUI.TextField(
                    new Rect(LAYER_SWATCH_W + 8f, rowY + 3f, r.width - 80f, LAYER_ROW_H - 6f),
                    nameProp.stringValue, EditorStyles.miniTextField);
            }
            else
            {
                if (GUI.Button(new Rect(LAYER_SWATCH_W + 8f, rowY + 2f, r.width - 80f, LAYER_ROW_H - 4f),
                               nameProp.stringValue,
                               new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft }))
                    SelectLayer(i);
            }

            // Visibility toggle (eye icon substitute)
            bool vis = visibleProp.boolValue;
            if (GUI.Button(new Rect(r.width - 52f, rowY + 3f, 16f, LAYER_ROW_H - 6f),
                           vis ? "●" : "○", EditorStyles.miniLabel))
                visibleProp.boolValue = !vis;

            // Lock toggle
            bool lk = lockedProp.boolValue;
            if (GUI.Button(new Rect(r.width - 34f, rowY + 3f, 16f, LAYER_ROW_H - 6f),
                           lk ? "🔒" : "🔓", EditorStyles.miniLabel))
                lockedProp.boolValue = !lk;


            // Delete button
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUI.Button(new Rect(r.width - 18f, rowY + 3f, 14f, LAYER_ROW_H - 6f), "✕", EditorStyles.miniLabel))
            {
                DeleteLayer(i);
                GUI.backgroundColor = Color.white;
                break; // list changed, bail
            }
            GUI.backgroundColor = Color.white;
        }

        GUI.EndScrollView();
    }

    private void DrawLayerRow(Rect r, int layerIdx, string label, Color swatchColor, ref float nextY)
    {
        bool selected = _activeLayerIndex == layerIdx;
        EditorGUI.DrawRect(r, selected ? new Color(0.25f, 0.4f, 0.65f) : new Color(0.18f, 0.18f, 0.18f));
        EditorGUI.DrawRect(new Rect(r.x + 4f, r.y + 4f, LAYER_SWATCH_W, r.height - 8f), swatchColor);

        if (GUI.Button(new Rect(r.x + LAYER_SWATCH_W + 8f, r.y + 2f, r.width - LAYER_SWATCH_W - 12f, r.height - 4f),
                       label, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft }))
            SelectLayer(layerIdx);

        nextY += r.height + 1f;
    }

    private void SelectLayer(int index)
    {
        _activeLayerIndex = index;
        _selectedIndex = -1;
        Repaint();
    }

    private void AddLayer()
    {
        Undo.RecordObject(_target, "Add Layer");
        _layersProp.arraySize++;
        var newLayer = _layersProp.GetArrayElementAtIndex(_layersProp.arraySize - 1);
        newLayer.FindPropertyRelative("name").stringValue = $"Layer {_layersProp.arraySize}";
        newLayer.FindPropertyRelative("color").colorValue = LayerDefaultColor(_layersProp.arraySize - 1);
        newLayer.FindPropertyRelative("visible").boolValue = true;
        newLayer.FindPropertyRelative("locked").boolValue = false;
        _so.ApplyModifiedProperties();
        Repaint();
    }

    private void DeleteLayer(int index)
    {
        if (_layersProp.arraySize <= 1)
        {
            EditorUtility.DisplayDialog("Cannot Delete", "At least one layer must exist.", "OK");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Delete Layer",
            $"Delete layer \"{_layersProp.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue}\"?\n" +
            "Events on this layer will be moved to layer 0.", "Delete", "Cancel");

        if (!ok) return;

        Undo.RecordObject(_target, "Delete Layer");

        // Reassign events on deleted layer to layer 0
        for (int i = 0; i < _pointsProp.arraySize; i++)
        {
            var li = _pointsProp.GetArrayElementAtIndex(i).FindPropertyRelative("layerIndex");
            if (li != null && li.intValue == index) li.intValue = 0;
            else if (li != null && li.intValue > index) li.intValue--;
        }
        for (int i = 0; i < _rangesProp.arraySize; i++)
        {
            var li = _rangesProp.GetArrayElementAtIndex(i).FindPropertyRelative("layerIndex");
            if (li != null && li.intValue == index) li.intValue = 0;
            else if (li != null && li.intValue > index) li.intValue--;
        }

        _layersProp.DeleteArrayElementAtIndex(index);
        _so.ApplyModifiedProperties();

        if (_activeLayerIndex >= _layersProp.arraySize)
            _activeLayerIndex = _layersProp.arraySize - 1;

        Repaint();
    }

    private static Color LayerDefaultColor(int index)
    {
        Color[] palette =
        {
            new Color(0.6f, 0.6f, 0.6f),
            new Color(0.3f, 0.8f, 1.0f),
            new Color(0.3f, 1.0f, 0.5f),
            new Color(1.0f, 0.75f, 0.3f),
            new Color(1.0f, 0.4f, 0.9f),
            new Color(0.5f, 0.5f, 0.9f),
        };
        return palette[index % palette.Length];
    }

    // ──────────────────────────────────────────────────────────────
    //  Timeline
    // ──────────────────────────────────────────────────────────────

    private void DrawTimeline(Rect r)
    {
        EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));
        DrawBorder(r, new Color(0.3f, 0.3f, 0.3f));
        DrawTicks(r);

        for (int i = 0; i < _rangesProp.arraySize; i++)
        {
            if (_rangesProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailRangeEvent re) continue;
            if (!IsVisibleOnActiveLayer(re.layerIndex)) continue;
            DrawRangeBand(r, i, re);
        }

        const float overlapThresholdPx = MARKER_R * 2f + 6f;
        int count = _pointsProp.arraySize;
        int[] lanes = new int[count];

        for (int i = 0; i < count; i++)
        {
            if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailEvent ei) continue;
            if (!IsVisibleOnActiveLayer(ei.layerIndex)) { lanes[i] = -1; continue; }

            var occupied = new HashSet<int>();
            for (int j = 0; j < i; j++)
            {
                if (lanes[j] < 0) continue;
                if (_pointsProp.GetArrayElementAtIndex(j).managedReferenceValue is not RailEvent ej) continue;
                if (!IsVisibleOnActiveLayer(ej.layerIndex)) continue;
                float dist = Mathf.Abs((ei.t - ej.t) * r.width);
                if (dist < overlapThresholdPx)
                    occupied.Add(lanes[j]);
            }
            int lane = 0;
            while (occupied.Contains(lane)) lane++;
            lanes[i] = lane;
        }

        for (int i = 0; i < count; i++)
        {
            if (lanes[i] < 0) continue;
            if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is RailEvent e)
                DrawPointMarker(r, i, e, lanes[i]);
        }

        HandleTimelineInput(r, lanes);
    }

    // Is event on a layer that should be shown given the current _activeLayerIndex?
    private bool IsVisibleOnActiveLayer(int eventLayerIndex)
    {
        // "All Layers" mode: show based on each layer's visible flag
        if (_activeLayerIndex == -1)
        {
            var layer = _target.GetLayer(eventLayerIndex);
            return layer.visible;
        }
        // Single layer filter: show only events on the active layer
        return eventLayerIndex == _activeLayerIndex;
    }

    // Is the currently active layer locked?
    private bool IsActiveLocked()
    {
        if (_activeLayerIndex < 0 || _activeLayerIndex >= _target.layers.Count) return false;
        return _target.layers[_activeLayerIndex].locked;
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

        // Dim if on a different layer in single-layer mode
        if (_activeLayerIndex >= 0 && e.layerIndex != _activeLayerIndex)
            c = Color.Lerp(c, new Color(0.1f, 0.1f, 0.1f), 0.6f);

        EditorGUI.DrawRect(new Rect(x1, y, Mathf.Max(x2 - x1, 4f), RANGE_TRACK_H), c);
        DrawHandle(new Rect(x1 - 3f, y, 6f, RANGE_TRACK_H), c);
        DrawHandle(new Rect(x2 - 3f, y, 6f, RANGE_TRACK_H), c);

        // Layer indicator stripe on the left of the band
        if (_activeLayerIndex == -1)
        {
            Color lc = _target.GetLayer(e.layerIndex).color;
            EditorGUI.DrawRect(new Rect(x1, y, 3f, RANGE_TRACK_H), lc);
        }

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

        // Use layer color as base in "all layers" mode, event color otherwise
        Color layerColor = _target.GetLayer(e.layerIndex).color;
        bool dimmed = (_activeLayerIndex >= 0 && e.layerIndex != _activeLayerIndex);
        Color c = selected ? Color.white : (dimmed ? Color.Lerp(e.EditorColor, new Color(0.2f, 0.2f, 0.2f), 0.7f) : e.EditorColor);

        if (lane > 0)
        {
            Handles.color = new Color(c.r, c.g, c.b, 0.45f);
            Handles.DrawLine(new Vector3(x, baseY), new Vector3(x, markerY + MARKER_R));
            Handles.color = Color.white;
        }

        Handles.DrawSolidRectangleWithOutline(
            new Rect(x - MARKER_R * 0.7f, markerY - MARKER_R * 0.7f, MARKER_R * 1.4f, MARKER_R * 1.4f),
            c,
            selected ? Color.white : new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f));

        // Small layer-color dot in bottom-right of diamond when in "All Layers" mode
        if (_activeLayerIndex == -1)
        {
            EditorGUI.DrawRect(new Rect(x + 1f, markerY + 1f, 4f, 4f), layerColor);
        }

        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };
        GUI.Label(new Rect(x - 55f, markerY + MARKER_R + 2f, 110f, 14f), e.EditorLabel, style);
    }

    private void HandleTimelineInput(Rect timeline, int[] lanes)
    {
        Event ev = Event.current;

        if (ev.type == EventType.MouseDown && ev.button == 0 && timeline.Contains(ev.mousePosition))
        {
            float laneStep = MARKER_R * 2f + 4f;

            for (int i = 0; i < _pointsProp.arraySize; i++)
            {
                if (_pointsProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailEvent e) continue;
                if (lanes[i] < 0) continue; // hidden

                float markerX = timeline.x + e.t * timeline.width;
                float markerY = timeline.y + MARKER_Y - lanes[i] * laneStep;

                if (Mathf.Abs(ev.mousePosition.x - markerX) < MARKER_R + 4f &&
                    Mathf.Abs(ev.mousePosition.y - markerY) < MARKER_R + 4f)
                {
                    Select(i, isRange: false);
                    _isDragging = !IsEventLocked(e.layerIndex);
                    ev.Use();
                    return;
                }
            }

            for (int i = 0; i < _rangesProp.arraySize; i++)
            {
                if (_rangesProp.GetArrayElementAtIndex(i).managedReferenceValue is not RailRangeEvent re) continue;
                if (!IsVisibleOnActiveLayer(re.layerIndex)) continue;

                float x1 = timeline.x + re.tStart * timeline.width;
                float x2 = timeline.x + re.tEnd * timeline.width;
                float bandY = timeline.y + RANGE_TRACK_Y + i * (RANGE_TRACK_H + RANGE_TRACK_GAP);

                if (ev.mousePosition.x >= x1 && ev.mousePosition.x <= x2 &&
                    ev.mousePosition.y >= bandY && ev.mousePosition.y <= bandY + RANGE_TRACK_H)
                {
                    Select(i, isRange: true);
                    _isDragging = !IsEventLocked(re.layerIndex);
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

    private bool IsEventLocked(int eventLayerIndex)
    {
        var layer = _target.GetLayer(eventLayerIndex);
        return layer.locked;
    }

    // ──────────────────────────────────────────────────────────────
    //  Inspector
    // ──────────────────────────────────────────────────────────────

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

        // Layer assignment dropdown
        GUILayout.BeginHorizontal();
        GUILayout.Label("Layer", GUILayout.Width(44f));
        var layerIdxProp = prop.FindPropertyRelative("layerIndex");
        if (layerIdxProp != null)
        {
            string[] layerNames = new string[_layersProp.arraySize];
            for (int i = 0; i < _layersProp.arraySize; i++)
                layerNames[i] = _layersProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;

            layerIdxProp.intValue = EditorGUILayout.Popup(
                Mathf.Clamp(layerIdxProp.intValue, 0, _layersProp.arraySize - 1),
                layerNames);
        }
        GUILayout.EndHorizontal();
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

    // ──────────────────────────────────────────────────────────────
    //  Event List
    // ──────────────────────────────────────────────────────────────

    private void DrawEventList(Rect r)
    {
        GUILayout.BeginArea(r);
        _listScroll = GUILayout.BeginScrollView(_listScroll);

        string layerFilter = _activeLayerIndex == -1
            ? "All Layers"
            : (_activeLayerIndex < _target.layers.Count ? _target.layers[_activeLayerIndex].name : "?");

        GUILayout.Label($"Point Events  [{layerFilter}]", EditorStyles.boldLabel);
        DrawListEntries(_pointsProp, isRange: false, e =>
        {
            var pe = (RailEvent)e;
            return ($"[{pe.t:0.00}]  {pe.EditorLabel}", pe.EditorColor);
        });

        GUILayout.Space(6f);
        GUILayout.Label($"Range Events  [{layerFilter}]", EditorStyles.boldLabel);
        DrawListEntries(_rangesProp, isRange: true, e =>
        {
            var re = (RailRangeEvent)e;
            return ($"[{re.tStart:0.00} → {re.tEnd:0.00}]  {re.EditorLabel}", re.EditorColor);
        });

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawListEntries(SerializedProperty listProp, bool isRange, Func<object, (string label, Color color)> info)
    {
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var prop = listProp.GetArrayElementAtIndex(i);
            var refVal = prop.managedReferenceValue;
            if (refVal == null) continue;

            var layerIdxProp = prop.FindPropertyRelative("layerIndex");
            int eventLayer = layerIdxProp?.intValue ?? 0;

            if (_activeLayerIndex >= 0 && eventLayer != _activeLayerIndex) continue;

            var eventLayerData = _target.GetLayer(eventLayer);
            if (!eventLayerData.visible) continue;

            var (label, color) = info(refVal);
            bool selected = isRange == _selectedIsRange && _selectedIndex == i;

            var rowRect = EditorGUILayout.GetControlRect(GUILayout.Height(20f));

            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + 1f, 4f, rowRect.height - 2f), eventLayerData.color);
            EditorGUI.DrawRect(new Rect(rowRect.x + 5f, rowRect.y + 1f, 4f, rowRect.height - 2f), color);

            bool locked = eventLayerData.locked;
            string rowLabel = locked ? $"🔒 {label}" : label;

            // Reserve space for duplicate + delete buttons
            float buttonW = 20f;
            float gap = 2f;
            float buttonsTotal = buttonW * 2 + gap + 4f;

            GUI.backgroundColor = selected ? new Color(0.3f, 0.55f, 1f) : new Color(0.22f, 0.22f, 0.22f);
            if (GUI.Button(new Rect(rowRect.x + 11f, rowRect.y, rowRect.width - 11f - buttonsTotal, rowRect.height),
                           rowLabel, new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft }))
                Select(i, isRange);
            GUI.backgroundColor = Color.white;

            // Duplicate button
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUI.Button(new Rect(rowRect.xMax - buttonsTotal + 2f, rowRect.y, buttonW, rowRect.height),
                           "⧉", EditorStyles.miniButton))
            {
                Undo.RecordObject(_target, "Duplicate Event");
                listProp.InsertArrayElementAtIndex(i);
                // InsertArrayElementAtIndex copies the element but managed references
                // need to be deep-copied manually via JSON
                var original = listProp.GetArrayElementAtIndex(i).managedReferenceValue;
                var json = JsonUtility.ToJson(original);
                var clone = JsonUtility.FromJson(json, original.GetType());
                listProp.GetArrayElementAtIndex(i + 1).managedReferenceValue = clone;
                _so.ApplyModifiedProperties();
                Select(i + 1, isRange);
            }
            GUI.backgroundColor = Color.white;

            // Delete button
            GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            if (GUI.Button(new Rect(rowRect.xMax - buttonW - 2f, rowRect.y, buttonW, rowRect.height),
                           "✕", EditorStyles.miniButton))
            {
                Undo.RecordObject(_target, "Remove Event");
                listProp.DeleteArrayElementAtIndex(i);
                _so.ApplyModifiedProperties();
                if (_selectedIndex == i) _selectedIndex = -1;
                else if (_selectedIndex > i) _selectedIndex--;
                break;
            }
            GUI.backgroundColor = Color.white;
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────

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
                var newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newElem.managedReferenceValue = Activator.CreateInstance(captured);

                // Assign to active layer
                int targetLayer = Mathf.Max(0, _activeLayerIndex);
                var liProp = newElem.FindPropertyRelative("layerIndex");
                if (liProp != null) liProp.intValue = targetLayer;

                _so.ApplyModifiedProperties();
                Repaint();
            });
        }
        menu.ShowAsContext();
    }

    private static readonly Type[] PointEventTypes =
    {
        typeof(ChangeSpeedEvent),
        typeof(SetObjectActiveEvent),
        typeof(DetachPlayerFromRail),
        typeof(ChangeOffsetPlaneEvent),
        typeof(LaneChangeEvent),
        typeof(LaneSpeedEvent),
        typeof(ActivateLaneEvent),
        typeof(ScriptedFormationShotEvent),
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