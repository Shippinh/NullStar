using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RailEvent), true)]
public class RailEventDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position = EditorGUI.IndentedRect(position);

        // Draw Timeline T
        var timelineProp = property.FindPropertyRelative("eventTimelinePosition");
        DrawProperty(ref position, timelineProp, "Timeline T");

        // Draw Type
        var typeProp = property.FindPropertyRelative("type");
        DrawProperty(ref position, typeProp);

        var activeProp = property.FindPropertyRelative("canShoot");
        DrawProperty(ref position, activeProp);

        var canReactivateProp = property.FindPropertyRelative("canShootAgain");
        DrawProperty(ref position, canReactivateProp);

        // Draw Type-specific payload
        DrawPayload(ref position, property, (RailEventType)typeProp.enumValueIndex);

        EditorGUI.EndProperty();
    }

    void DrawProperty(ref Rect position, SerializedProperty prop, string label = null)
    {
        float h = EditorGUI.GetPropertyHeight(prop, true);
        position.height = h;

        if (!string.IsNullOrEmpty(label))
            EditorGUI.PropertyField(position, prop, new GUIContent(label), true);
        else
            EditorGUI.PropertyField(position, prop, true);

        position.y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    void DrawPayload(ref Rect position, SerializedProperty property, RailEventType type)
    {
        switch (type)
        {
            case RailEventType.ChangePlayerSpeed:
                DrawProperty(ref position, property.FindPropertyRelative("intPtr"), "Target Speed");
                DrawProperty(ref position, property.FindPropertyRelative("floatPtr"), "Change Time");
                break;

            case RailEventType.EnableObject:
                DrawProperty(ref position, property.FindPropertyRelative("objectParam"), "Target Object");
                break;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 0f;

        var timelineProp = property.FindPropertyRelative("eventTimelinePosition");
        height += EditorGUI.GetPropertyHeight(timelineProp, true) + EditorGUIUtility.standardVerticalSpacing;

        var typeProp = property.FindPropertyRelative("type");
        height += EditorGUI.GetPropertyHeight(typeProp, true) + EditorGUIUtility.standardVerticalSpacing;

        var activeProp = property.FindPropertyRelative("canShoot");
        height += EditorGUI.GetPropertyHeight(activeProp, true) + EditorGUIUtility.standardVerticalSpacing;

        var canReactivateProp = property.FindPropertyRelative("canShootAgain");
        height += EditorGUI.GetPropertyHeight(canReactivateProp, true) + EditorGUIUtility.standardVerticalSpacing;

        switch ((RailEventType)typeProp.enumValueIndex)
        {
            case RailEventType.ChangePlayerSpeed:
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("intPtr"), true) + EditorGUIUtility.standardVerticalSpacing;
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("floatPtr"), true) + EditorGUIUtility.standardVerticalSpacing;
                break;

            case RailEventType.EnableObject:
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("objectParam"), true) + EditorGUIUtility.standardVerticalSpacing;
                break;
        }

        return height;
    }
}
