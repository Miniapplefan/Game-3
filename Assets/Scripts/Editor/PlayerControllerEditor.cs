using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PlayerInputBinding))]
public sealed class PlayerInputBindingDrawer : PropertyDrawer
{
    public override bool CanCacheInspectorGUI(SerializedProperty property)
    {
        // The drawer changes height when its source changes. Caching can otherwise
        // leave the KeyCode selector hidden after switching away from a scroll input.
        return false;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty sourceProperty = property.FindPropertyRelative("source");
        bool showKey = sourceProperty != null
            && sourceProperty.enumValueIndex == (int)PlayerInputBindingSource.KeyCode;

        return showKey
            ? (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing
            : EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty sourceProperty = property.FindPropertyRelative("source");
        SerializedProperty keyProperty = property.FindPropertyRelative("key");
        if (sourceProperty == null || keyProperty == null)
        {
            EditorGUI.LabelField(position, label.text, "Binding data is unavailable.");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        Rect sourceRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(sourceRect, sourceProperty, label);

        if (sourceProperty.enumValueIndex == (int)PlayerInputBindingSource.KeyCode)
        {
            Rect keyRect = new Rect(
                position.x,
                sourceRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);

            EditorGUI.indentLevel++;
            EditorGUI.PropertyField(
                keyRect,
                keyProperty,
                new GUIContent(
                    "Key or Mouse Button",
                    "Choose any keyboard KeyCode or Mouse0 through Mouse6."));
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }
}

[CustomEditor(typeof(PlayerController))]
[CanEditMultipleObjects]
public sealed class PlayerControllerEditor : Editor
{
    private const string BreakoutLeftProperty = "breakoutLeftBinding";
    private const string BreakoutRightProperty = "breakoutRightBinding";
    private const string RecenterArmProperty = "recenterArmBinding";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (targets.Length != 1)
        {
            return;
        }

        serializedObject.UpdateIfRequiredOrScript();

        SerializedProperty breakoutLeft = serializedObject.FindProperty(BreakoutLeftProperty);
        SerializedProperty breakoutRight = serializedObject.FindProperty(BreakoutRightProperty);
        SerializedProperty recenterArm = serializedObject.FindProperty(RecenterArmProperty);
        if (breakoutLeft == null || breakoutRight == null || recenterArm == null)
        {
            return;
        }

        List<string> conflicts = new List<string>();
        AddConflict(conflicts, "Breakout Left", breakoutLeft, "Breakout Right", breakoutRight);
        AddConflict(conflicts, "Breakout Left", breakoutLeft, "Recenter Arm", recenterArm);
        AddConflict(conflicts, "Breakout Right", breakoutRight, "Recenter Arm", recenterArm);

        if (conflicts.Count > 0)
        {
            EditorGUILayout.HelpBox(
                "Conflicting arm input bindings are suppressed at runtime:\n" + string.Join("\n", conflicts),
                MessageType.Warning);
        }
    }

    private static void AddConflict(
        List<string> conflicts,
        string firstLabel,
        SerializedProperty first,
        string secondLabel,
        SerializedProperty second)
    {
        if (!TryGetConfiguredBinding(first, out PlayerInputBindingSource firstSource, out KeyCode firstKey)
            || !TryGetConfiguredBinding(second, out PlayerInputBindingSource secondSource, out KeyCode secondKey)
            || firstSource != secondSource
            || (firstSource == PlayerInputBindingSource.KeyCode && firstKey != secondKey))
        {
            return;
        }

        conflicts.Add($"{firstLabel} and {secondLabel} both use {GetBindingName(firstSource, firstKey)}.");
    }

    private static bool TryGetConfiguredBinding(
        SerializedProperty binding,
        out PlayerInputBindingSource source,
        out KeyCode key)
    {
        SerializedProperty sourceProperty = binding.FindPropertyRelative("source");
        SerializedProperty keyProperty = binding.FindPropertyRelative("key");
        source = sourceProperty != null
            ? (PlayerInputBindingSource)sourceProperty.enumValueIndex
            : PlayerInputBindingSource.None;
        key = keyProperty != null ? (KeyCode)keyProperty.intValue : KeyCode.None;

        return source != PlayerInputBindingSource.None
            && (source != PlayerInputBindingSource.KeyCode || key != KeyCode.None);
    }

    private static string GetBindingName(PlayerInputBindingSource source, KeyCode key)
    {
        switch (source)
        {
            case PlayerInputBindingSource.KeyCode:
                return ObjectNames.NicifyVariableName(key.ToString());
            case PlayerInputBindingSource.ScrollUp:
                return "Scroll Up";
            case PlayerInputBindingSource.ScrollDown:
                return "Scroll Down";
            default:
                return "None";
        }
    }
}
