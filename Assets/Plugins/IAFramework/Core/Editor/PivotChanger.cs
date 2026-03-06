using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PivotEditor : EditorWindow
{
    private GameObject targetObject;
    private Vector3 newPivot;
    private bool useWorldSpace = true;

    [MenuItem("Tools/Pivot Editor")]
    public static void ShowWindow()
    {
        GetWindow<PivotEditor>("Pivot Editor");
    }

    void OnGUI()
    {
        GUILayout.Label("Pivot Adjustment Tool", EditorStyles.boldLabel);

        targetObject = (GameObject)EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true);

        useWorldSpace = EditorGUILayout.Toggle("Use World Space", useWorldSpace);

        if (targetObject != null)
        {
            if (useWorldSpace)
            {
                newPivot = EditorGUILayout.Vector3Field("New Pivot (World)", newPivot);
                if (GUILayout.Button("Snap Pivot to Selected Position"))
                {
                    newPivot = SceneView.lastActiveSceneView.camera.transform.position;
                }
            }
            else
            {
                newPivot = EditorGUILayout.Vector3Field("New Pivot (Local)", newPivot);
            }

            if (GUILayout.Button("Apply New Pivot"))
            {
                ApplyNewPivot();
            }
        }
    }

    void ApplyNewPivot()
    {
        if (targetObject == null)
            return;

        Undo.RegisterCompleteObjectUndo(targetObject.transform, "Pivot Change");

        Vector3 worldPivotPos = useWorldSpace ? newPivot : targetObject.transform.TransformPoint(newPivot);

        GameObject pivotGO = new GameObject(targetObject.name + "_Pivot");
        Undo.RegisterCreatedObjectUndo(pivotGO, "Create Pivot");

        pivotGO.transform.position = worldPivotPos;
        pivotGO.transform.rotation = targetObject.transform.rotation;
        pivotGO.transform.localScale = targetObject.transform.localScale;

        // Reparent target under new pivot
        Transform originalParent = targetObject.transform.parent;
        Undo.SetTransformParent(targetObject.transform, pivotGO.transform, "Reparent Target");

        // Match pivot GO to original parent
        Undo.SetTransformParent(pivotGO.transform, originalParent, "Reparent Pivot");

        // Move target so children stay in world position
        Vector3 delta = pivotGO.transform.position - targetObject.transform.position;
        targetObject.transform.position -= delta;

        Selection.activeGameObject = pivotGO;
    }
}
