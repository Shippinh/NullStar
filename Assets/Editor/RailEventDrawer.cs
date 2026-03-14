using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RailEvent), true)]
[CustomPropertyDrawer(typeof(RailRangeEvent), true)]
public class RailEventPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        if (prop.managedReferenceValue == null)
        {
            EditorGUI.LabelField(pos, label.text, "null — add via Timeline window");
            return;
        }

        // Show concrete type name in the foldout header
        string typeName = prop.managedReferenceValue.GetType().Name;
        var richLabel = new GUIContent($"{label.text}  ({typeName})");
        EditorGUI.PropertyField(pos, prop, richLabel, includeChildren: true);
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label) =>
        EditorGUI.GetPropertyHeight(prop, label, includeChildren: true);
}