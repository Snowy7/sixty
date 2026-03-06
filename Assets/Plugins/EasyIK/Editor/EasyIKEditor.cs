using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EasyIK))]
[CanEditMultipleObjects]
public class EasyIKEditor : Editor
{
    private EasyIK _ik;
    private SerializedProperty _numberOfJoints;
    private SerializedProperty _ikTarget;
    private SerializedProperty _iterations;
    private SerializedProperty _tolerance;
    private SerializedProperty _matchTargetRotation;
    private SerializedProperty _enableTwist;
    private SerializedProperty _twistAxis;
    private SerializedProperty _twistFalloff;
    private SerializedProperty _twistCurve;
    private SerializedProperty _twistWeights;
    private SerializedProperty _poleTarget;
    private SerializedProperty _debugJoints;
    private SerializedProperty _localRotationAxis;
    private SerializedProperty _gizmoSize;
    private SerializedProperty _poleDirection;
    private SerializedProperty _poleRotationAxis;
    private SerializedProperty _showTwistAxes;

    private bool _showTwistWeights;

    private void OnEnable()
    {
        _ik = (EasyIK)target;
        _numberOfJoints = serializedObject.FindProperty("numberOfJoints");
        _ikTarget = serializedObject.FindProperty("ikTarget");
        _iterations = serializedObject.FindProperty("iterations");
        _tolerance = serializedObject.FindProperty("tolerance");
        _matchTargetRotation = serializedObject.FindProperty("matchTargetRotation");
        _enableTwist = serializedObject.FindProperty("enableTwist");
        _twistAxis = serializedObject.FindProperty("twistAxis");
        _twistFalloff = serializedObject.FindProperty("twistFalloff");
        _twistCurve = serializedObject.FindProperty("twistCurve");
        _twistWeights = serializedObject.FindProperty("twistWeights");
        _poleTarget = serializedObject.FindProperty("poleTarget");
        _debugJoints = serializedObject.FindProperty("debugJoints");
        _localRotationAxis = serializedObject.FindProperty("localRotationAxis");
        _gizmoSize = serializedObject.FindProperty("gizmoSize");
        _poleDirection = serializedObject.FindProperty("poleDirection");
        _poleRotationAxis = serializedObject.FindProperty("poleRotationAxis");
        _showTwistAxes = serializedObject.FindProperty("showTwistAxes");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCalibrationSection();
        EditorGUILayout.Space(8);
        DrawIKSection();
        EditorGUILayout.Space(8);
        DrawTwistSection();
        EditorGUILayout.Space(8);
        DrawPoleSection();
        EditorGUILayout.Space(8);
        DrawDebugSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCalibrationSection()
    {
        bool calibrated = _ik.IsCalibrated;
        bool valid = ValidateChain();

        Color boxColor = calibrated
            ? new Color(0.2f, 0.7f, 0.3f)
            : new Color(0.9f, 0.6f, 0.1f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(3));
            EditorGUI.DrawRect(rect, boxColor);

            if (calibrated)
            {
                EditorGUILayout.LabelField("✓ Calibrated", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Recalibrate", GUILayout.Height(24)))
                    {
                        Undo.RecordObject(_ik, "Recalibrate IK");
                        _ik.Calibrate();
                    }

                    if (GUILayout.Button("Reset Pose", GUILayout.Height(24)))
                    {
                        RecordJoints("Reset IK Pose");
                        _ik.ResetToRestPose();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("⚠ Not Calibrated", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "1. Position joints in rest pose\n" +
                    "2. Assign the IK Target\n" +
                    "3. Click Calibrate",
                    MessageType.Info);

                using (new EditorGUI.DisabledScope(!valid || _ik.ikTarget == null))
                {
                    if (GUILayout.Button("Calibrate", GUILayout.Height(30)))
                    {
                        Undo.RecordObject(_ik, "Calibrate IK");
                        _ik.Calibrate();
                    }
                }

                if (!valid)
                    EditorGUILayout.HelpBox(GetValidationError(), MessageType.Error);
                else if (_ik.ikTarget == null)
                    EditorGUILayout.HelpBox("Assign IK Target first", MessageType.Warning);
            }
        }
    }

    private void DrawIKSection()
    {
        EditorGUILayout.LabelField("IK Configuration", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(_ikTarget);

            using (new EditorGUI.DisabledScope(_ik.IsCalibrated))
            {
                EditorGUILayout.PropertyField(_numberOfJoints);
            }

            if (_ik.IsCalibrated)
            {
                EditorGUILayout.HelpBox("Joint count locked. Recalibrate to change.", MessageType.None);
            }

            EditorGUILayout.PropertyField(_iterations);
            EditorGUILayout.PropertyField(_tolerance);
            EditorGUILayout.PropertyField(_matchTargetRotation);
        }
    }

    private void DrawTwistSection()
    {
        EditorGUILayout.LabelField("Twist Distribution", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(_enableTwist);

            if (_enableTwist.boolValue)
            {
                EditorGUILayout.Space(4);
                
                using (new EditorGUI.DisabledScope(_ik.IsCalibrated))
                {
                    EditorGUILayout.PropertyField(_twistAxis);
                }
                
                if (_ik.IsCalibrated)
                {
                    EditorGUILayout.HelpBox("Twist axis locked. Recalibrate to change.", MessageType.None);
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(_twistFalloff, new GUIContent("Global Falloff"));
                EditorGUILayout.PropertyField(_twistCurve, new GUIContent("Distribution Curve"));

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _showTwistWeights = EditorGUILayout.Foldout(_showTwistWeights, "Per-Joint Weights", true);
                    
                    GUILayout.FlexibleSpace();
                    
                    if (GUILayout.Button("Auto Generate", GUILayout.Width(100)))
                    {
                        Undo.RecordObject(_ik, "Generate Twist Weights");
                        _ik.GenerateTwistWeights();
                    }
                }

                if (_showTwistWeights && _ik.twistWeights != null)
                {
                    EditorGUI.indentLevel++;
                    
                    var current = _ik.transform;
                    for (int i = 0; i < _ik.twistWeights.Length && current != null; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(current.name, GUILayout.Width(120));
                            
                            EditorGUI.BeginChangeCheck();
                            float newWeight = EditorGUILayout.Slider(_ik.twistWeights[i], 0f, 1f);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(_ik, "Change Twist Weight");
                                _ik.twistWeights[i] = newWeight;
                            }
                        }
                        
                        current = current.childCount > 0 ? current.GetChild(0) : null;
                    }
                    
                    EditorGUI.indentLevel--;
                }

                if (_ik.numberOfJoints <= 2)
                {
                    EditorGUILayout.HelpBox(
                        "Twist distribution works best with 3+ joints.",
                        MessageType.Info);
                }
            }
        }
    }

    private void DrawPoleSection()
    {
        EditorGUILayout.LabelField("Pole Constraint", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(_poleTarget);

            if (_poleTarget.objectReferenceValue != null && _numberOfJoints.intValue != 3)
            {
                EditorGUILayout.HelpBox(
                    "Pole constraint requires exactly 3 joints.",
                    MessageType.Warning);
            }
        }
    }

    private void DrawDebugSection()
    {
        EditorGUILayout.LabelField("Debug Visualization", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(_debugJoints);
            EditorGUILayout.PropertyField(_localRotationAxis);
            EditorGUILayout.PropertyField(_gizmoSize);
            
            if (_enableTwist.boolValue)
            {
                EditorGUILayout.PropertyField(_showTwistAxes, new GUIContent("Show Twist Axes"));
            }

            if (_poleTarget.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(_poleDirection);
                EditorGUILayout.PropertyField(_poleRotationAxis);
            }
        }
    }

    private void RecordJoints(string name)
    {
        Undo.RecordObject(_ik, name);
        if (_ik.Joints != null)
        {
            foreach (var j in _ik.Joints)
            {
                if (j != null) Undo.RecordObject(j, name);
            }
        }
    }

    private bool ValidateChain()
    {
        var current = _ik.transform;
        for (int i = 0; i < _ik.numberOfJoints - 1; i++)
        {
            if (current == null || current.childCount == 0) return false;
            current = current.GetChild(0);
        }
        return current != null;
    }

    private string GetValidationError()
    {
        var current = _ik.transform;
        for (int i = 0; i < _ik.numberOfJoints - 1; i++)
        {
            if (current == null) return $"Joint {i} is missing";
            if (current.childCount == 0) return $"Joint '{current.name}' has no children";
            current = current.GetChild(0);
        }
        return "Unknown error";
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawGizmos(EasyIK ik, GizmoType gizmoType)
    {
        if (!ik.debugJoints && !ik.localRotationAxis && !ik.showTwistAxes) return;

        var current = ik.transform;

        for (int i = 0; i < ik.numberOfJoints && current != null; i++)
        {
            if (ik.localRotationAxis)
                DrawAxisHandles(current, ik.gizmoSize);

            if (ik.showTwistAxes && ik.enableTwist)
                DrawTwistAxis(current, ik, i);

            if (ik.debugJoints && i < ik.numberOfJoints - 1 && current.childCount > 0)
            {
                var child = current.GetChild(0);
                DrawBoneCapsule(current.position, child.position, ik.gizmoSize);
            }

            current = current.childCount > 0 ? current.GetChild(0) : null;
        }

        if (ik.poleTarget != null && ik.numberOfJoints == 3)
            DrawPoleGizmos(ik);
    }

    private static void DrawTwistAxis(Transform joint, EasyIK ik, int index)
    {
        if (ik.twistWeights == null || index >= ik.twistWeights.Length) return;

        float weight = ik.twistWeights[index];
        if (weight < 0.001f) return;

        Vector3 twistAxis = ik.twistAxis switch
        {
            EasyIK.TwistAxis.X => Vector3.right,
            EasyIK.TwistAxis.Y => Vector3.up,
            EasyIK.TwistAxis.Z => Vector3.forward,
            EasyIK.TwistAxis.NegX => Vector3.left,
            EasyIK.TwistAxis.NegY => Vector3.down,
            EasyIK.TwistAxis.NegZ => Vector3.back,
            _ => Vector3.right
        };

        Vector3 worldAxis = joint.TransformDirection(twistAxis);
        
        // Color based on weight
        Handles.color = Color.Lerp(Color.gray, Color.magenta, weight);
        
        float length = ik.gizmoSize * 2f * weight;
        Handles.DrawLine(joint.position - worldAxis * length * 0.5f, 
                         joint.position + worldAxis * length * 0.5f);
        
        // Draw a disc to show rotation plane
        Handles.DrawWireDisc(joint.position, worldAxis, ik.gizmoSize * 0.5f * weight);
    }

    private static void DrawAxisHandles(Transform t, float size)
    {
        Handles.color = Handles.xAxisColor;
        Handles.ArrowHandleCap(0, t.position, t.rotation * Quaternion.LookRotation(Vector3.right), size, EventType.Repaint);

        Handles.color = Handles.yAxisColor;
        Handles.ArrowHandleCap(0, t.position, t.rotation * Quaternion.LookRotation(Vector3.up), size, EventType.Repaint);

        Handles.color = Handles.zAxisColor;
        Handles.ArrowHandleCap(0, t.position, t.rotation * Quaternion.LookRotation(Vector3.forward), size, EventType.Repaint);
    }

    private static void DrawBoneCapsule(Vector3 start, Vector3 end, float radius)
    {
        float length = Vector3.Distance(start, end);
        if (length < 0.001f) return;

        Vector3 center = (start + end) * 0.5f;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);

        Handles.color = Color.cyan;

        using (new Handles.DrawingScope(Matrix4x4.TRS(center, rot, Vector3.one)))
        {
            float offset = Mathf.Max(0, (length - radius * 2) * 0.5f);

            Handles.DrawWireArc(Vector3.up * offset, Vector3.left, Vector3.back, -180, radius);
            Handles.DrawWireArc(Vector3.down * offset, Vector3.left, Vector3.back, 180, radius);
            Handles.DrawWireArc(Vector3.up * offset, Vector3.back, Vector3.left, 180, radius);
            Handles.DrawWireArc(Vector3.down * offset, Vector3.back, Vector3.left, -180, radius);

            Handles.DrawLine(new Vector3(0, offset, -radius), new Vector3(0, -offset, -radius));
            Handles.DrawLine(new Vector3(0, offset, radius), new Vector3(0, -offset, radius));
            Handles.DrawLine(new Vector3(-radius, offset, 0), new Vector3(-radius, -offset, 0));
            Handles.DrawLine(new Vector3(radius, offset, 0), new Vector3(radius, -offset, 0));

            Handles.DrawWireDisc(Vector3.up * offset, Vector3.up, radius);
            Handles.DrawWireDisc(Vector3.down * offset, Vector3.up, radius);
        }
    }

    private static void DrawPoleGizmos(EasyIK ik)
    {
        var start = ik.transform;
        if (start.childCount == 0) return;
        var mid = start.GetChild(0);
        if (mid.childCount == 0) return;
        var end = mid.GetChild(0);

        if (ik.poleRotationAxis)
        {
            Handles.color = Color.white;
            Handles.DrawLine(start.position, end.position);
        }

        if (ik.poleDirection)
        {
            Handles.color = Color.grey;
            Handles.DrawLine(start.position, ik.poleTarget.position);
            Handles.DrawLine(end.position, ik.poleTarget.position);
        }
    }
}