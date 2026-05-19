#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Inspector for EnemyLane.
/// 
/// The inspector is split into two modes:
///   • Entry Mode  — lane uses an entry spline to fly enemies in, then they ride the main spline.
///   • Passby Mode — lane uses a passby spline; enemies traverse it once and are repooled.
///
/// Mode is inferred from which spline reference is set, but can also be toggled explicitly
/// via the mode selector at the top of the inspector.
/// </summary>
[CustomEditor(typeof(EnemyLane))]
public class EnemyLaneEditor : Editor
{
    // Persisted per-object so the toggle survives domain reloads.
    private enum LaneEditorMode { Entry, Passby }

    private LaneEditorMode _mode = LaneEditorMode.Entry;
    private bool _modeInitialized = false;

    // ── Foldouts ──────────────────────────────────────────────────────────────

    private bool _showCoreSettings = true;
    private bool _showMovement = true;
    private bool _showOrientation = true;
    private bool _showShootingFold = false;
    private bool _showActivation = true;
    private bool _showSlots = true;
    private bool _showSplineTools = true;

    // ── Styles (lazy-init) ────────────────────────────────────────────────────

    private GUIStyle _modeButtonLeft;
    private GUIStyle _modeButtonRight;
    private GUIStyle _headerLabel;
    private bool _stylesBuilt;

    private void BuildStyles()
    {
        if (_stylesBuilt) return;

        _modeButtonLeft = new GUIStyle(EditorStyles.miniButtonLeft)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            fixedHeight = 26f,
        };
        _modeButtonRight = new GUIStyle(EditorStyles.miniButtonRight)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            fixedHeight = 26f,
        };
        _headerLabel = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
        };

        _stylesBuilt = true;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    public override void OnInspectorGUI()
    {
        BuildStyles();

        var lane = (EnemyLane)target;
        serializedObject.Update();

        // Infer initial mode from which spline is set
        if (!_modeInitialized)
        {
            _mode = (lane.passbySpline != null) ? LaneEditorMode.Passby : LaneEditorMode.Entry;
            _modeInitialized = true;
        }

        DrawModeSelector(lane);
        EditorGUILayout.Space(4);

        DrawCoreSettings(lane);
        EditorGUILayout.Space(2);

        if (_mode == LaneEditorMode.Entry)
            DrawEntryMode(lane);
        else
            DrawPassbyMode(lane);

        EditorGUILayout.Space(4);
        DrawSlots(lane);

        serializedObject.ApplyModifiedProperties();
    }

    // ── Mode selector ─────────────────────────────────────────────────────────

    private void DrawModeSelector(EnemyLane lane)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("LANE MODE", _headerLabel);
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = _mode == LaneEditorMode.Entry
            ? new Color(0.35f, 0.7f, 1f)
            : new Color(0.75f, 0.75f, 0.75f);

        if (GUILayout.Toggle(_mode == LaneEditorMode.Entry, "⬇  Entry / Lane", _modeButtonLeft))
            _mode = LaneEditorMode.Entry;

        GUI.backgroundColor = _mode == LaneEditorMode.Passby
            ? new Color(0.4f, 1f, 0.6f)
            : new Color(0.75f, 0.75f, 0.75f);

        if (GUILayout.Toggle(_mode == LaneEditorMode.Passby, "↔  Passby", _modeButtonRight))
            _mode = LaneEditorMode.Passby;

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);
        if (_mode == LaneEditorMode.Entry)
            EditorGUILayout.LabelField(
                "Enemies fly in via an entry spline then ride the main lane spline continuously.",
                EditorStyles.wordWrappedMiniLabel);
        else
            EditorGUILayout.LabelField(
                "Enemies traverse the passby spline once, shoot, then are repooled. " +
                "Triggered by a RailEvent or code.",
                EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.EndVertical();
    }

    // ── Core settings (shared) ────────────────────────────────────────────────

    private void DrawCoreSettings(EnemyLane lane)
    {
        _showCoreSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showCoreSettings, "Core Settings");
        if (_showCoreSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(serializedObject.FindProperty("splineContainer"),
                new GUIContent("Main Spline"));
            EditorGUILayout.Space(2);

            _showMovement = EditorGUILayout.Foldout(_showMovement, "Movement", true);
            if (_showMovement)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enemySpacing"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startT"));
                EditorGUI.indentLevel--;
            }

            _showOrientation = EditorGUILayout.Foldout(_showOrientation, "Orientation", true);
            if (_showOrientation)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("orientation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
                EditorGUI.indentLevel--;
            }

            _showShootingFold = EditorGUILayout.Foldout(_showShootingFold, "Shooting", true);
            if (_showShootingFold)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("shootingEnabled"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── Entry mode ────────────────────────────────────────────────────────────

    private void DrawEntryMode(EnemyLane lane)
    {
        _showActivation = EditorGUILayout.BeginFoldoutHeaderGroup(_showActivation, "Activation");
        if (_showActivation)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("activationType"));
            // No extra UI for None — external code calls Activate() or TriggerPassby() directly
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        _showSplineTools = EditorGUILayout.BeginFoldoutHeaderGroup(_showSplineTools, "Entry Path");
        if (_showSplineTools)
        {
            EditorGUI.indentLevel++;
            DrawEntrySplineTools(lane);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawEntrySplineTools(EnemyLane lane)
    {
        if (lane.entrySpline == null)
        {
            EditorGUILayout.HelpBox(
                "No entry spline assigned. Click below to create one — Unity's spline editor " +
                "will open. Entry speed matches lane speed automatically.",
                MessageType.Info);

            if (GUILayout.Button("Create Entry Spline", GUILayout.Height(28)))
                CreateEntrySpline(lane);
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("entrySpline"));

            EditorGUILayout.HelpBox(
                "Last knot = where enemies hand off to the main lane. " +
                "Entry speed matches lane speed automatically.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select & Edit Spline", GUILayout.Height(24)))
            {
                Selection.activeGameObject = lane.entrySpline.gameObject;
                Tools.current = Tool.None;
            }

            if (GUILayout.Button("Align Last Knot to Lane", GUILayout.Height(24)))
                AlignLastKnotToLane(lane);

            EditorGUILayout.EndHorizontal();

            GUI.color = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Remove Entry Spline Reference"))
            {
                if (EditorUtility.DisplayDialog("Remove Entry Spline",
                    "Clear the entry spline reference? The child object will remain in the hierarchy.",
                    "Remove", "Cancel"))
                {
                    Undo.RecordObject(lane, "Remove Entry Spline");
                    lane.entrySpline = null;
                    EditorUtility.SetDirty(lane);
                }
            }
            GUI.color = Color.white;
        }
    }

    // ── Passby mode ───────────────────────────────────────────────────────────

    private void DrawPassbyMode(EnemyLane lane)
    {
        _showSplineTools = EditorGUILayout.BeginFoldoutHeaderGroup(_showSplineTools, "Passby Path");
        if (_showSplineTools)
        {
            EditorGUI.indentLevel++;
            DrawPassbySplineTools(lane);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Passby shooting settings
        _showShootingFold = EditorGUILayout.BeginFoldoutHeaderGroup(_showShootingFold, "Passby Shooting");
        if (_showShootingFold)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passbyShootT"),
                new GUIContent("Shoot T", "Arc-length T (0–1) along the passby spline where shooting triggers."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passbyShootActivation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passbyShootType"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Trigger helper button
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("RUNTIME TRIGGER", _headerLabel);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(
            "In play mode, click the button below to fire a test passby immediately.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(2);

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("▶  Test Trigger", GUILayout.Height(24)))
            lane.Activate();
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    private void DrawPassbySplineTools(EnemyLane lane)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("passbySplineMode"));

        if (lane.passbySpline == null)
        {
            EditorGUILayout.HelpBox(
                "No passby spline assigned. Create a SplineContainer child and assign it here, " +
                "or click the button below to auto-create one.",
                MessageType.Info);

            if (GUILayout.Button("Create Passby Spline", GUILayout.Height(28)))
                CreatePassbySpline(lane);
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("passbySpline"));

            if (lane.passbySplineMode == PassbySplineMode.PlayerRelative)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("passbyPlayerTOffset"),
                    new GUIContent("Player T Offset",
                        "Spline origin is placed at (playerT + this offset) on the player rail."));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select & Edit Spline", GUILayout.Height(24)))
            {
                Selection.activeGameObject = lane.passbySpline.gameObject;
                Tools.current = Tool.None;
            }

            GUI.color = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Remove Reference", GUILayout.Height(24), GUILayout.Width(110f)))
            {
                if (EditorUtility.DisplayDialog("Remove Passby Spline",
                    "Clear the passby spline reference? The child object will remain in the hierarchy.",
                    "Remove", "Cancel"))
                {
                    Undo.RecordObject(lane, "Remove Passby Spline");
                    lane.passbySpline = null;
                    EditorUtility.SetDirty(lane);
                }
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }

    // ── Slots ─────────────────────────────────────────────────────────────────

    private void DrawSlots(EnemyLane lane)
    {
        _showSlots = EditorGUILayout.Foldout(_showSlots, $"Slots  ({lane.slots?.Count ?? 0})", true, EditorStyles.foldoutHeader);
        if (_showSlots)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("slots"), true);
            EditorGUI.indentLevel--;
        }
    }

    // ── Scene GUI ─────────────────────────────────────────────────────────────

    private void OnSceneGUI()
    {
        var lane = (EnemyLane)target;

        if (_mode == LaneEditorMode.Entry && lane.entrySpline != null && lane.splineContainer != null)
            DrawEntryHandoff(lane);
        else if (_mode == LaneEditorMode.Passby && lane.passbySpline != null)
            DrawPassbyPreview(lane);
    }

    private static void DrawEntryHandoff(EnemyLane lane)
    {
        Vector3 slotPos = GetFirstSlotWorldPos(lane);
        lane.entrySpline.Spline.Evaluate(1f, out Unity.Mathematics.float3 ep, out _, out _);
        Vector3 entryEnd = lane.entrySpline.transform.TransformPoint((Vector3)ep);

        Handles.color = new Color(1f, 0.9f, 0.1f, 0.85f);
        Handles.DrawAAPolyLine(3f, entryEnd, slotPos);

        float arrowSize = HandleUtility.GetHandleSize(slotPos) * 0.22f;
        Vector3 dir = (slotPos - entryEnd).normalized;
        if (dir.sqrMagnitude > 0.001f)
            Handles.ConeHandleCap(0, slotPos - dir * arrowSize * 0.5f,
                Quaternion.LookRotation(dir), arrowSize, EventType.Repaint);

        Handles.Label(Vector3.Lerp(entryEnd, slotPos, 0.5f) + Vector3.up * 0.8f,
            "  handoff", EditorStyles.miniLabel);
    }

    private static void DrawPassbyPreview(EnemyLane lane)
    {
        var spline = lane.passbySpline.Spline;
        const int steps = 24;

        Handles.color = new Color(0.4f, 1f, 0.6f, 0.85f);

        Vector3 prev = lane.passbySpline.transform.TransformPoint(
            (Vector3)spline.EvaluatePosition(0f));

        for (int i = 1; i <= steps; i++)
        {
            Vector3 cur = lane.passbySpline.transform.TransformPoint(
                (Vector3)spline.EvaluatePosition((float)i / steps));
            Handles.DrawAAPolyLine(2.5f, prev, cur);
            prev = cur;
        }

        // Shoot-T marker
        spline.Evaluate(lane.passbyShootT, out Unity.Mathematics.float3 sp,
            out Unity.Mathematics.float3 st, out Unity.Mathematics.float3 su);
        Vector3 shootPos = lane.passbySpline.transform.TransformPoint((Vector3)sp);
        float hs = HandleUtility.GetHandleSize(shootPos) * 0.18f;
        Handles.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        Handles.SphereHandleCap(0, shootPos, Quaternion.identity, hs, EventType.Repaint);
        Handles.Label(shootPos + Vector3.up * hs * 1.5f, $"  shoot T={lane.passbyShootT:0.00}",
            EditorStyles.miniLabel);
    }

    // ── Spline helpers ────────────────────────────────────────────────────────

    private static void CreateEntrySpline(EnemyLane lane)
    {
        Vector3 slotPos = GetFirstSlotWorldPos(lane);
        Vector3 entryPos = slotPos;

        if (lane.splineContainer != null)
        {
            lane.splineContainer.Spline.Evaluate(lane.startT,
                out _, out Unity.Mathematics.float3 tang, out Unity.Mathematics.float3 up);
            Vector3 fwd = lane.splineContainer.transform.TransformDirection(((Vector3)tang).normalized);
            Vector3 upW = lane.splineContainer.transform.TransformDirection(((Vector3)up).normalized);
            Vector3 rW = Vector3.Cross(upW, fwd).normalized;
            entryPos = slotPos + rW * 30f;
        }

        var child = new GameObject("Entry Spline");
        Undo.RegisterCreatedObjectUndo(child, "Create Entry Spline");
        child.transform.SetParent(lane.transform, worldPositionStays: true);
        child.transform.position = Vector3.zero;

        var sc = child.AddComponent<SplineContainer>();
        var spline = sc.Spline;
        spline.Clear();

        Vector3 toSlot = (slotPos - entryPos).normalized;
        float dist = Vector3.Distance(entryPos, slotPos);

        Vector3 mainFwd = toSlot;
        if (lane.splineContainer != null)
        {
            float slotT = GetFirstSlotT(lane);
            lane.splineContainer.Spline.Evaluate(slotT,
                out _, out Unity.Mathematics.float3 mt, out _);
            mainFwd = lane.splineContainer.transform
                .TransformDirection(((Vector3)mt).normalized);
        }

        var k0 = new BezierKnot(entryPos,
            -(Unity.Mathematics.float3)(toSlot * dist * 0.33f),
             (Unity.Mathematics.float3)(toSlot * dist * 0.33f));

        var k1 = new BezierKnot(slotPos,
            -(Unity.Mathematics.float3)(mainFwd * dist * 0.33f),
             (Unity.Mathematics.float3)(mainFwd * dist * 0.33f));

        spline.Add(k0, TangentMode.Broken);
        spline.Add(k1, TangentMode.Broken);

        Undo.RecordObject(lane, "Assign Entry Spline");
        lane.entrySpline = sc;
        EditorUtility.SetDirty(lane);

        Selection.activeGameObject = child;
        Tools.current = Tool.None;
    }

    private static void CreatePassbySpline(EnemyLane lane)
    {
        // Place passby spline in front of / beside the main spline start
        Vector3 pivot = lane.splineContainer != null
            ? lane.splineContainer.transform.position
            : lane.transform.position;

        var child = new GameObject("Passby Spline");
        Undo.RegisterCreatedObjectUndo(child, "Create Passby Spline");
        child.transform.SetParent(lane.transform, worldPositionStays: true);
        child.transform.position = pivot;

        var sc = child.AddComponent<SplineContainer>();
        var spline = sc.Spline;
        spline.Clear();

        // Default: a simple left-to-right arc 40 units wide, 15 units in front
        Vector3 origin = pivot;
        var k0 = new BezierKnot(origin + new Vector3(-20f, 0f, 15f),
            -(Unity.Mathematics.float3)Vector3.right * 8f,
             (Unity.Mathematics.float3)Vector3.right * 8f);
        var k1 = new BezierKnot(origin + new Vector3(20f, 0f, 15f),
            -(Unity.Mathematics.float3)Vector3.right * 8f,
             (Unity.Mathematics.float3)Vector3.right * 8f);

        spline.Add(k0, TangentMode.Broken);
        spline.Add(k1, TangentMode.Broken);

        Undo.RecordObject(lane, "Assign Passby Spline");
        lane.passbySpline = sc;
        EditorUtility.SetDirty(lane);

        Selection.activeGameObject = child;
        Tools.current = Tool.None;
    }

    private static void AlignLastKnotToLane(EnemyLane lane)
    {
        if (lane.entrySpline == null || lane.splineContainer == null) return;

        Vector3 slotPos = GetFirstSlotWorldPos(lane);

        float slotT = GetFirstSlotT(lane);
        lane.splineContainer.Spline.Evaluate(slotT,
            out _, out Unity.Mathematics.float3 mt, out _);
        Vector3 mainFwd = lane.splineContainer.transform
            .TransformDirection(((Vector3)mt).normalized);

        float entryLen = lane.entrySpline.Spline.GetLength();
        float tangentScale = Mathf.Max(1f, entryLen * 0.25f);

        Vector3 localPos = lane.entrySpline.transform.InverseTransformPoint(slotPos);
        Vector3 localFwd = lane.entrySpline.transform.InverseTransformDirection(mainFwd).normalized;

        var spline = lane.entrySpline.Spline;
        int lastIdx = spline.Count - 1;
        if (lastIdx < 0) return;

        Undo.RecordObject(lane.entrySpline, "Align Last Knot to Lane");

        var knot = spline[lastIdx];
        knot.Position = localPos;
        knot.TangentIn = -(Unity.Mathematics.float3)(localFwd * tangentScale);
        knot.TangentOut = (Unity.Mathematics.float3)(localFwd * tangentScale);
        spline.SetKnot(lastIdx, knot);

        EditorUtility.SetDirty(lane.entrySpline);
        Debug.Log("[EnemyLane] Last knot aligned to lane entry point.");
    }

    private static float GetFirstSlotT(EnemyLane lane)
    {
        if (lane.splineContainer == null) return lane.startT;
        float len = lane.splineContainer.Spline.GetLength();
        if (len <= 0f) return lane.startT;
        return Mathf.Repeat(lane.startT, 1f);
    }

    private static Vector3 GetFirstSlotWorldPos(EnemyLane lane)
    {
        if (lane.splineContainer == null) return lane.transform.position;

        float slotT = GetFirstSlotT(lane);
        lane.splineContainer.Spline.Evaluate(slotT,
            out Unity.Mathematics.float3 p,
            out Unity.Mathematics.float3 tang,
            out Unity.Mathematics.float3 up);

        Vector3 fwd = lane.splineContainer.transform.TransformDirection(((Vector3)tang).normalized);
        Vector3 upW = lane.splineContainer.transform.TransformDirection(((Vector3)up).normalized);
        Vector3 rW = Vector3.Cross(upW, fwd).normalized;
        Vector3 pos = lane.splineContainer.transform.TransformPoint((Vector3)p);

        if (lane.slots != null && lane.slots.Count > 0)
            pos += rW * lane.slots[0].rightOffset + upW * lane.slots[0].upOffset;

        return pos;
    }
}
#endif