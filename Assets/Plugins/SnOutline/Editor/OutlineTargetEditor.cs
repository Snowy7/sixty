// Editor/OutlineTargetEditor.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OutlineTarget))]
public class OutlineTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var target = (OutlineTarget)this.target;

        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("_mode"));

        var mode = (OutlineTarget.TargetMode)serializedObject.FindProperty("_mode").enumValueIndex;

        if (mode == OutlineTarget.TargetMode.GroupName)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_groupName"));
        }

        EditorGUILayout.Space();

        if (mode == OutlineTarget.TargetMode.CustomSettings)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_customSettings"), true);
        }
        else
        {
            EditorGUILayout.LabelField("Overrides", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var overrideColor = serializedObject.FindProperty("_overrideColor");
            EditorGUILayout.PropertyField(overrideColor);
            if (overrideColor.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_color"));
                EditorGUI.indentLevel--;
            }

            var overrideThickness = serializedObject.FindProperty("_overrideThickness");
            EditorGUILayout.PropertyField(overrideThickness);
            if (overrideThickness.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_thickness"));
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_previewInEditor"));

        serializedObject.ApplyModifiedProperties();

        // Runtime state display
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Is Active", target.IsActive);
            EditorGUILayout.IntField("Renderer Count", target.Renderers?.Length ?? 0);
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(target.IsActive ? "Disable" : "Enable"))
                target.Toggle();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif