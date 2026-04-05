using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FormationDefinition))]
public class FormationDefinitionEditor : Editor
{
    // ── Foldout state ─────────────────────────────────────────────────────────

    private bool _showShape = true;
    private bool _showSlots = true;
    private bool _showPerSlot = false;
    private bool _showTravel = true;
    private bool _showOrientation = true;
    private bool _showShooting = true;
    private bool _showPreview = true;

    // ── Preview state ─────────────────────────────────────────────────────────

    private float _previewTime = 0f;
    private float _previewSize = 300f;
    private float _previewRange = 100f;
    private int _previewSlotCount = 5;
    private double _lastEditorTime = 0;

    private float[] _previewSeedsX;
    private float[] _previewSeedsY;
    private float[] _previewPhases;
    private float[] _previewFreqs;
    private int _lastSlotCount = -1;

    // ── Serialized properties ─────────────────────────────────────────────────

    private SerializedProperty _shape;
    private SerializedProperty _slotSpacing;
    private SerializedProperty _gridColumns;
    private SerializedProperty _slotPhaseSpread;
    private SerializedProperty _perSlotUpOffset;
    private SerializedProperty _perSlotRightOffset;
    private SerializedProperty _slots;
    private SerializedProperty _travelPattern;
    private SerializedProperty _travelRadius;
    private SerializedProperty _travelFrequency;
    private SerializedProperty _randomizeTravelPerSlot;
    private SerializedProperty _randomFrequencyVariance;
    private SerializedProperty _randomPhaseVariance;
    private SerializedProperty _invertAll;
    private SerializedProperty _alternateInversion;
    private SerializedProperty _orientation;
    private SerializedProperty _shootingEnabled;

    private void OnEnable()
    {
        _shape = serializedObject.FindProperty("shape");
        _slotSpacing = serializedObject.FindProperty("slotSpacing");
        _gridColumns = serializedObject.FindProperty("gridColumns");
        _slotPhaseSpread = serializedObject.FindProperty("slotPhaseSpread");
        _perSlotUpOffset = serializedObject.FindProperty("perSlotUpOffset");
        _perSlotRightOffset = serializedObject.FindProperty("perSlotRightOffset");
        _slots = serializedObject.FindProperty("slots");
        _travelPattern = serializedObject.FindProperty("travelPattern");
        _travelRadius = serializedObject.FindProperty("travelRadius");
        _travelFrequency = serializedObject.FindProperty("travelFrequency");
        _randomizeTravelPerSlot = serializedObject.FindProperty("randomizeTravelPerSlot");
        _randomFrequencyVariance = serializedObject.FindProperty("randomFrequencyVariance");
        _randomPhaseVariance = serializedObject.FindProperty("randomPhaseVariance");
        _invertAll = serializedObject.FindProperty("invertAll");
        _alternateInversion = serializedObject.FindProperty("alternateInversion");
        _orientation = serializedObject.FindProperty("orientation");
        _shootingEnabled = serializedObject.FindProperty("shootingEnabled");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawShape();
        EditorGUILayout.Space(4);
        DrawSlots();
        EditorGUILayout.Space(4);
        DrawPerSlot();
        EditorGUILayout.Space(4);
        DrawTravel();
        EditorGUILayout.Space(4);
        DrawOrientation();
        EditorGUILayout.Space(4);
        DrawShooting();
        EditorGUILayout.Space(4);
        DrawPreview();

        if (_showPreview)
        {
            double now = EditorApplication.timeSinceStartup;
            _previewTime += (float)(now - _lastEditorTime);
            _lastEditorTime = now;
            Repaint();
        }
        else
        {
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ── Shape ─────────────────────────────────────────────────────────────────

    private void DrawShape()
    {
        _showShape = DrawHeader("Shape", _showShape);
        if (!_showShape) return;

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(_shape);
            EditorGUILayout.PropertyField(_slotSpacing);
            EditorGUILayout.PropertyField(_slotPhaseSpread,
                new GUIContent("Slot Phase Spread",
                    "How much movement phase is staggered between slots. " +
                    "0 = all in sync, 1 = full cycle spread across slots."));

            if ((FormationShape)_shape.enumValueIndex == FormationShape.Grid)
                EditorGUILayout.PropertyField(_gridColumns);
        }
    }

    // ── Slots ─────────────────────────────────────────────────────────────────

    private void DrawSlots()
    {
        _showSlots = DrawHeader("Slots", _showSlots);
        if (!_showSlots) return;

        using (new EditorGUI.IndentLevelScope())
        {
            int count = _slots.arraySize;

            for (int i = 0; i < count; i++)
            {
                var slot = _slots.GetArrayElementAtIndex(i);
                var poolTag = slot.FindPropertyRelative("poolTag");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Slot {i}", GUILayout.Width(50));
                EditorGUILayout.PropertyField(poolTag, GUIContent.none);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    _slots.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Slot"))
                _slots.InsertArrayElementAtIndex(count);
            if (count > 0 && GUILayout.Button("Clear All", GUILayout.Width(75)))
                _slots.ClearArray();
            EditorGUILayout.EndHorizontal();

            if (count > 0)
                _previewSlotCount = count;
        }
    }

    // ── Per-Slot Oscillation ──────────────────────────────────────────────────

    private void DrawPerSlot()
    {
        _showPerSlot = DrawHeader("Per-Slot Oscillation", _showPerSlot,
            "Phase-shifted per slot — creates wave effects across the formation");
        if (!_showPerSlot) return;

        using (new EditorGUI.IndentLevelScope())
        {
            var shapeVal = (FormationShape)_shape.enumValueIndex;
            if (shapeVal != FormationShape.Column)
                EditorGUILayout.PropertyField(_perSlotUpOffset, new GUIContent("Up Offset Wave"));
            if (shapeVal == FormationShape.Column)
                EditorGUILayout.PropertyField(_perSlotRightOffset, new GUIContent("Right Offset Wave"));
        }
    }

    // ── Travel Behavior ───────────────────────────────────────────────────────

    private void DrawTravel()
    {
        _showTravel = DrawHeader("Travel Behavior", _showTravel,
            "How each enemy moves around its slot — the formation shape stays steady");
        if (!_showTravel) return;

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(_travelPattern, new GUIContent("Pattern"));

            var pattern = (FormationMovementType)_travelPattern.enumValueIndex;

            if (pattern == FormationMovementType.None)
            {
                EditorGUILayout.HelpBox("Enemies move directly to their slot.", MessageType.None);
                return;
            }

            EditorGUILayout.PropertyField(_travelRadius, new GUIContent("Radius"));
            EditorGUILayout.PropertyField(_travelFrequency, new GUIContent("Frequency"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Per-Enemy Randomization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_randomizeTravelPerSlot, new GUIContent("Randomize Per Slot"));

            if (_randomizeTravelPerSlot.boolValue)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(_randomFrequencyVariance, new GUIContent("Frequency Variance"));
                    EditorGUILayout.PropertyField(_randomPhaseVariance, new GUIContent("Phase Variance"));
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Inversion", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_invertAll, new GUIContent("Invert All",
                "Flips travel pattern for every slot."));
            EditorGUILayout.PropertyField(_alternateInversion, new GUIContent("Alternate Inversion",
                "Every other slot mirrors the pattern."));

            if (_invertAll.boolValue && _alternateInversion.boolValue)
                EditorGUILayout.HelpBox("Invert All overrides Alternate Inversion.", MessageType.Warning);

            DrawPatternHint(pattern);
        }
    }

    private void DrawPatternHint(FormationMovementType pattern)
    {
        string hint = pattern switch
        {
            FormationMovementType.Wiggle => "Enemies oscillate left/right around their slot.",
            FormationMovementType.Bob => "Enemies oscillate up/down around their slot.",
            FormationMovementType.Saw => "Enemies sweep left to right then snap back.",
            FormationMovementType.Orbit => "Enemies circle their slot in the right/up plane.",
            FormationMovementType.Lissajous => "Enemies trace a figure-8 curve. FreqX:FreqY ratio shapes the curve.",
            FormationMovementType.Surge => "Enemies pulse forward/back along the spline forward axis.",
            FormationMovementType.Spiral => "Enemies orbit while surging — corkscrew path.",
            FormationMovementType.Snake => "Enemies wiggle laterally while surging — S-curve path.",
            FormationMovementType.Drift => "Enemies drift on unique noise paths — fully desynced naturally.",
            _ => ""
        };

        if (!string.IsNullOrEmpty(hint))
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }
    }

    // ── Orientation ───────────────────────────────────────────────────────────

    private void DrawOrientation()
    {
        _showOrientation = DrawHeader("Orientation", _showOrientation);
        if (!_showOrientation) return;

        using (new EditorGUI.IndentLevelScope())
            EditorGUILayout.PropertyField(_orientation);
    }

    // ── Shooting ──────────────────────────────────────────────────────────────

    private void DrawShooting()
    {
        _showShooting = DrawHeader("Shooting", _showShooting);
        if (!_showShooting) return;

        using (new EditorGUI.IndentLevelScope())
            EditorGUILayout.PropertyField(_shootingEnabled);
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    private void DrawPreview()
    {
        _showPreview = DrawHeader("Preview", _showPreview,
            "Formation shape on the left — single-slot travel pattern on the right");
        if (!_showPreview) return;

        var def = (FormationDefinition)target;

        EditorGUILayout.Space(2);
        using (new EditorGUI.IndentLevelScope())
        {
            _previewRange = EditorGUILayout.Slider("View Range", _previewRange, 1f, 100f);
            _previewSize = EditorGUILayout.Slider("Preview Size", _previewSize, 100f, 600f);

            bool hasSlots = def.slots != null && def.slots.Count > 0;
            if (!hasSlots)
                _previewSlotCount = EditorGUILayout.IntSlider("Slot Count", _previewSlotCount, 1, 20);
            else
                EditorGUILayout.LabelField("Slot Count", _previewSlotCount.ToString(), EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Time", GUILayout.Width(90))) _previewTime = 0f;
            if (GUILayout.Button("Reseed", GUILayout.Width(70))) _lastSlotCount = -1;
            EditorGUILayout.LabelField($"t = {_previewTime:F1}s", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        RebuildPreviewSeeds(def);

        float panelSize = _previewSize * 0.5f - 2f;

        EditorGUILayout.BeginHorizontal();
        Rect leftRect = GUILayoutUtility.GetRect(panelSize, panelSize,
            GUILayout.Width(panelSize), GUILayout.Height(panelSize));
        DrawFormationPanel(leftRect, def);

        GUILayout.Space(4);

        Rect rightRect = GUILayoutUtility.GetRect(panelSize, panelSize,
            GUILayout.Width(panelSize), GUILayout.Height(panelSize));
        DrawSlotPatternPanel(rightRect, def);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawCenteredLabel("Formation", panelSize);
        GUILayout.Space(4);
        DrawCenteredLabel("Slot Pattern", panelSize);
        EditorGUILayout.EndHorizontal();
    }

    private void RebuildPreviewSeeds(FormationDefinition def)
    {
        if (_previewSlotCount == _lastSlotCount) return;

        _lastSlotCount = _previewSlotCount;
        _previewSeedsX = new float[_previewSlotCount];
        _previewSeedsY = new float[_previewSlotCount];
        _previewPhases = new float[_previewSlotCount];
        _previewFreqs = new float[_previewSlotCount];

        float baseFreq = def.travelFrequency.Evaluate(0f);

        for (int i = 0; i < _previewSlotCount; i++)
        {
            _previewSeedsX[i] = Random.Range(0f, 1000f);
            _previewSeedsY[i] = Random.Range(0f, 1000f);

            if (def.randomizeTravelPerSlot)
            {
                _previewPhases[i] = Random.Range(0f, def.randomPhaseVariance);
                _previewFreqs[i] = baseFreq + Random.Range(
                    -def.randomFrequencyVariance, def.randomFrequencyVariance);
            }
            else
            {
                _previewPhases[i] = 0f;
                _previewFreqs[i] = baseFreq;
            }
        }
    }

    // ── Formation panel ───────────────────────────────────────────────────────

    private void DrawFormationPanel(Rect rect, FormationDefinition def)
    {
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));
        DrawPreviewGrid(rect);

        float spacing = def.slotSpacing.Evaluate(_previewTime);
        Vector2[] offsets = ComputePreviewOffsets(def, def.shape,
            _previewSlotCount, Mathf.Max(1f, _previewSlotCount), spacing);

        for (int i = 0; i < _previewSlotCount; i++)
        {
            Color slotColor = Color.HSVToRGB((float)i / Mathf.Max(_previewSlotCount, 1), 0.7f, 0.9f);
            DrawSlotInFormation(rect, def, i, offsets[i], slotColor);
        }

        DrawPanelBorder(rect);
    }

    private void DrawSlotInFormation(Rect rect, FormationDefinition def,
                                     int slotIndex, Vector2 slotBase, Color color)
    {
        if (slotIndex >= _previewSlotCount) return;

        DrawTravelTrail(rect, def, slotIndex, slotBase, color);

        Vector2 travelOffset = SampleTravelOffset(def, slotIndex, _previewTime);
        Vector2 currentPos = slotBase + travelOffset;
        Vector3 currentSP = WorldToPreview(rect, currentPos);

        Handles.color = new Color(color.r, color.g, color.b, 0.3f);
        Handles.DrawSolidDisc(WorldToPreview(rect, slotBase), Vector3.forward, 3f);

        Handles.color = new Color(color.r, color.g, color.b, 0.4f);
        Handles.DrawLine(WorldToPreview(rect, slotBase), currentSP);

        Handles.color = color;
        Handles.DrawSolidDisc(currentSP, Vector3.forward, 4f);
    }

    private void DrawTravelTrail(Rect rect, FormationDefinition def,
                                  int slotIndex, Vector2 slotBase, Color color)
    {
        const int steps = 80;
        const float window = 4f;

        for (int pass = 0; pass < 2; pass++)
        {
            float passStart = pass == 0 ? _previewTime - window : _previewTime - window * 0.3f;
            float passEnd = pass == 0 ? _previewTime - window * 0.3f : _previewTime;
            float baseAlpha = pass == 0 ? 0.15f : 0.6f;

            Vector3? prev = null;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(passStart, passEnd, i / (float)steps);
                Vector3 sp = WorldToPreview(rect, slotBase + SampleTravelOffset(def, slotIndex, t));

                Handles.color = new Color(color.r, color.g, color.b, (float)i / steps * baseAlpha);
                if (prev.HasValue) Handles.DrawLine(prev.Value, sp);
                prev = sp;
            }
        }
    }

    // ── Slot pattern panel ────────────────────────────────────────────────────

    private void DrawSlotPatternPanel(Rect rect, FormationDefinition def)
    {
        EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.12f, 1f));
        DrawPreviewGrid(rect);

        const int steps = 128;
        const float window = 6f;

        for (int pass = 0; pass < 2; pass++)
        {
            float passStart = pass == 0 ? _previewTime - window : _previewTime - window * 0.25f;
            float passEnd = pass == 0 ? _previewTime - window * 0.25f : _previewTime;
            Color baseColor = pass == 0
                ? new Color(0.8f, 0.4f, 0.1f, 0.25f)
                : new Color(1.0f, 0.6f, 0.2f, 0.9f);

            Vector3? prev = null;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(passStart, passEnd, i / (float)steps);
                Vector2 p = SampleTravelOffset(def, 0, t);
                Vector3 sp = WorldToPreview(rect, p);
                float alpha = (float)i / steps;

                Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
                if (prev.HasValue) Handles.DrawLine(prev.Value, sp);
                prev = sp;
            }
        }

        // Current dot
        Vector2 cur = SampleTravelOffset(def, 0, _previewTime);
        Vector3 curSP = WorldToPreview(rect, cur);
        Handles.color = new Color(1f, 0.6f, 0.2f, 1f);
        Handles.DrawSolidDisc(curSP, Vector3.forward, 5f);

        // Slot anchor
        Handles.color = new Color(1f, 1f, 1f, 0.3f);
        Handles.DrawSolidDisc(WorldToPreview(rect, Vector2.zero), Vector3.forward, 4f);

        // Direction arrow
        Vector2 next = SampleTravelOffset(def, 0, _previewTime + 0.05f);
        Vector2 dir = (next - cur).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            Vector3 arrowEnd = WorldToPreview(rect, cur + dir * _previewRange * 0.12f);
            Handles.color = new Color(0.4f, 1f, 0.4f, 0.8f);
            Handles.DrawLine(curSP, arrowEnd);
            Handles.DrawSolidDisc(arrowEnd, Vector3.forward, 3f);
        }

        // Pattern label
        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
        GUI.Label(new Rect(rect.xMin + 4, rect.yMin + 4, rect.width - 8, 16),
            def.travelPattern.ToString(), labelStyle);

        // Radius reference circle
        Handles.color = new Color(1f, 1f, 1f, 0.08f);
        DrawCircleInPreview(rect, Vector2.zero, def.travelRadius);

        DrawPanelBorder(rect);
    }

    private void DrawCircleInPreview(Rect rect, Vector2 center, float radius)
    {
        const int segments = 48;
        Vector3? prev = null;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector3 sp = WorldToPreview(rect, p);
            if (prev.HasValue) Handles.DrawLine(prev.Value, sp);
            prev = sp;
        }
    }

    // ── Layout preview sampling ───────────────────────────────────────────────

    private Vector2[] ComputePreviewOffsets(FormationDefinition def,
        FormationShape shape, int count, float n, float spacing)
    {
        var offsets = new Vector2[count];

        switch (shape)
        {
            case FormationShape.Line:
                {
                    float totalWidth = (n - 1f) * spacing;
                    for (int i = 0; i < count; i++)
                    {
                        float phase = count > 1
                            ? def.slotPhaseSpread * ((float)i / (count - 1))
                            : 0f;
                        offsets[i] = new Vector2(
                            -totalWidth * 0.5f + i * spacing,
                            def.perSlotUpOffset.oscillator.Evaluate(
                                _previewTime + phase, def.perSlotUpOffset.value));
                    }
                    break;
                }
            case FormationShape.Column:
                {
                    float totalHeight = (n - 1f) * spacing;
                    for (int i = 0; i < count; i++)
                    {
                        float phase = count > 1
                            ? def.slotPhaseSpread * ((float)i / (count - 1))
                            : 0f;
                        offsets[i] = new Vector2(
                            def.perSlotRightOffset.oscillator.Evaluate(
                                _previewTime + phase, def.perSlotRightOffset.value),
                            -totalHeight * 0.5f + i * spacing);
                    }
                    break;
                }
            case FormationShape.Circle:
                {
                    float radius = Mathf.Max(spacing, spacing * n / (Mathf.PI * 2f));
                    for (int i = 0; i < count; i++)
                    {
                        float angle = (i / n) * Mathf.PI * 2f;
                        offsets[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                    }
                    break;
                }
            case FormationShape.V:
                {
                    for (int i = 0; i < count; i++)
                    {
                        int side = (i % 2 == 0) ? 1 : -1;
                        int depth = i / 2 + 1;
                        offsets[i] = new Vector2(side * depth * spacing, -depth * spacing * 0.5f);
                    }
                    break;
                }
            case FormationShape.Diamond:
                {
                    Vector2[] dirs = { Vector2.right, Vector2.up, Vector2.left, Vector2.down };
                    for (int i = 0; i < count; i++)
                        offsets[i] = dirs[i % 4] * ((i / 4 + 1) * spacing);
                    break;
                }
            case FormationShape.Stagger:
                {
                    int perRow = Mathf.CeilToInt(n * 0.5f);
                    for (int i = 0; i < count; i++)
                    {
                        int row = i % 2;
                        int col = i / 2;
                        float rowShift = row == 1 ? spacing * 0.5f : 0f;
                        float totalWidth = (perRow - 1) * spacing;
                        offsets[i] = new Vector2(
                            -totalWidth * 0.5f + col * spacing + rowShift,
                            row * spacing * 0.8f - spacing * 0.4f);
                    }
                    break;
                }
            default: // Grid
                {
                    int cols = Mathf.Max(1, def.gridColumns);
                    int rowCount = Mathf.CeilToInt(n / cols);
                    for (int i = 0; i < count; i++)
                    {
                        int col = i % cols;
                        int row = i / cols;
                        offsets[i] = new Vector2(
                            (col - (cols - 1) * 0.5f) * spacing,
                            (row - (rowCount - 1) * 0.5f) * spacing);
                    }
                    break;
                }
        }

        return offsets;
    }

    // ── Travel offset preview sampling ────────────────────────────────────────

    private Vector2 SampleTravelOffset(FormationDefinition def, int slotIndex, float t)
    {
        if (_previewSeedsX == null || slotIndex >= _previewSeedsX.Length)
            return Vector2.zero;

        float sign = 1f;
        if (def.invertAll)
            sign = -1f;
        else if (def.alternateInversion && slotIndex % 2 == 1)
            sign = -1f;

        float phase = _previewPhases[slotIndex];
        float freq = _previewFreqs[slotIndex];
        float time = (t + phase) * freq * Mathf.PI * 2f;
        float r = def.travelRadius * sign;

        if (def.travelPattern == FormationMovementType.Drift)
        {
            float tScaled = (t + phase) * freq;
            float nx = (Mathf.PerlinNoise(_previewSeedsX[slotIndex], tScaled) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(_previewSeedsY[slotIndex], tScaled) - 0.5f) * 2f;
            return new Vector2(nx * r, ny * r);
        }

        return def.travelPattern switch
        {
            FormationMovementType.Wiggle => new Vector2(Mathf.Sin(time) * r, 0f),
            FormationMovementType.Bob => new Vector2(0f, Mathf.Sin(time) * r),
            FormationMovementType.Saw => new Vector2(((t + phase) * freq % 1f * 2f - 1f) * r, 0f),
            FormationMovementType.Orbit => new Vector2(Mathf.Cos(time) * r, Mathf.Sin(time) * r),
            FormationMovementType.Lissajous => new Vector2(Mathf.Sin(time) * r, Mathf.Sin(time * 2f) * r),
            FormationMovementType.Surge => new Vector2(0f, Mathf.Sin(time) * r),
            FormationMovementType.Spiral => new Vector2(Mathf.Cos(time) * r,
                                                  Mathf.Sin(time) * r + Mathf.Sin(time * 0.5f) * r * 0.5f),
            FormationMovementType.Snake => new Vector2(Mathf.Sin(time) * r,
                                                  Mathf.Sin(time * 0.5f) * r * 0.5f),
            _ => Vector2.zero
        };
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private void DrawPreviewGrid(Rect rect)
    {
        Vector2 center = RectCenter(rect);

        Handles.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Handles.DrawLine(new Vector3(rect.xMin, center.y), new Vector3(rect.xMax, center.y));
        Handles.DrawLine(new Vector3(center.x, rect.yMin), new Vector3(center.x, rect.yMax));

        Handles.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        for (int i = 1; i < 8; i++)
        {
            float x = rect.xMin + i * (rect.width / 8f);
            float y = rect.yMin + i * (rect.height / 8f);
            Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
            Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
        }

        var labelStyle = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.35f, 0.35f, 0.35f) } };
        GUI.Label(new Rect(rect.xMin + 2, rect.yMin + 2, 60, 16), $"+{_previewRange:F0}", labelStyle);
        GUI.Label(new Rect(rect.xMin + 2, rect.yMax - 16, 60, 16), $"-{_previewRange:F0}", labelStyle);
    }

    private void DrawPanelBorder(Rect rect) =>
        Handles.DrawSolidRectangleWithOutline(rect, Color.clear, new Color(0.4f, 0.4f, 0.4f));

    private void DrawCenteredLabel(string text, float width)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };
        GUILayout.Label(text, style, GUILayout.Width(width));
    }

    private bool DrawHeader(string label, bool foldout, string tooltip = "")
    {
        var style = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold };
        Rect rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        return EditorGUI.Foldout(rect, foldout,
            string.IsNullOrEmpty(tooltip)
                ? new GUIContent(label)
                : new GUIContent(label, tooltip),
            true, style);
    }

    private Vector3 WorldToPreview(Rect rect, Vector2 worldPos)
    {
        Vector2 center = RectCenter(rect);
        return new Vector3(
            center.x + (worldPos.x / _previewRange) * (rect.width * 0.5f),
            center.y - (worldPos.y / _previewRange) * (rect.height * 0.5f),
            0f);
    }

    private Vector2 RectCenter(Rect rect) =>
        new Vector2(rect.xMin + rect.width * 0.5f, rect.yMin + rect.height * 0.5f);
}