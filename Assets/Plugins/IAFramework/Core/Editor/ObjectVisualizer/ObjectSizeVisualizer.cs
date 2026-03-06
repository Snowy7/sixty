using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "Size Visualizer", true)]
public class SizeVisualizerOverlay : ToolbarOverlay
{
    public static bool IsEnabled { get; set; } = true;
    public static bool ShowPivots { get; set; } = true;
    public static bool ShowIndividualBounds { get; set; } = true;

    SizeVisualizerOverlay() : base(
        SizeVisualizerToggle.Id,
        PivotToggle.Id,
        IndividualBoundsToggle.Id
    ) { }
}

[EditorToolbarElement(Id, typeof(SceneView))]
public class SizeVisualizerToggle : EditorToolbarToggle
{
    public const string Id = "SizeVisualizer/Toggle";

    public SizeVisualizerToggle()
    {
        text = "Size";
        tooltip = "Toggle Size Visualizer";
        value = SizeVisualizerOverlay.IsEnabled;
        this.RegisterValueChangedCallback(evt =>
        {
            SizeVisualizerOverlay.IsEnabled = evt.newValue;
            SceneView.RepaintAll();
        });
    }
}

[EditorToolbarElement(Id, typeof(SceneView))]
public class PivotToggle : EditorToolbarToggle
{
    public const string Id = "SizeVisualizer/Pivots";

    public PivotToggle()
    {
        text = "Pivots";
        tooltip = "Show Pivot Points";
        value = SizeVisualizerOverlay.ShowPivots;
        this.RegisterValueChangedCallback(evt =>
        {
            SizeVisualizerOverlay.ShowPivots = evt.newValue;
            SceneView.RepaintAll();
        });
    }
}

[EditorToolbarElement(Id, typeof(SceneView))]
public class IndividualBoundsToggle : EditorToolbarToggle
{
    public const string Id = "SizeVisualizer/Individual";

    public IndividualBoundsToggle()
    {
        text = "Individual";
        tooltip = "Show Individual Bounds (Multi-Select)";
        value = SizeVisualizerOverlay.ShowIndividualBounds;
        this.RegisterValueChangedCallback(evt =>
        {
            SizeVisualizerOverlay.ShowIndividualBounds = evt.newValue;
            SceneView.RepaintAll();
        });
    }
}

[InitializeOnLoad]
public static class ObjectSizeVisualizer
{
    // Colors
    private static readonly Color CombinedBoundsColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    private static readonly Color IndividualBoundsColor = new Color(0.8f, 0.8f, 0.2f, 0.5f);
    private static readonly Color RadiusColor = new Color(1f, 0.6f, 0.2f, 0.6f);
    private static readonly Color PivotColor = new Color(1f, 0.2f, 1f, 1f);
    private static readonly Color PivotOffsetColor = new Color(1f, 1f, 0.2f, 0.8f);
    private static readonly Color CombinedPivotColor = new Color(0.2f, 1f, 0.6f, 1f);

    static ObjectSizeVisualizer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        if (!SizeVisualizerOverlay.IsEnabled) return;

        var selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0) return;

        var objectDataList = new List<ObjectData>();
        
        foreach (var obj in selectedObjects)
        {
            var data = GetObjectData(obj);
            if (data.HasValue)
                objectDataList.Add(data.Value);
        }

        if (objectDataList.Count == 0) return;

        // Calculate combined bounds and pivot
        var combinedBounds = GetCombinedBounds(objectDataList);
        var combinedPivot = GetCombinedPivot(selectedObjects);

        // Draw individual bounds and pivots
        if (selectedObjects.Length > 1 && SizeVisualizerOverlay.ShowIndividualBounds)
        {
            foreach (var data in objectDataList)
            {
                DrawIndividualBounds(data.Bounds);
                if (SizeVisualizerOverlay.ShowPivots)
                    DrawPivot(data.Pivot, data.Bounds.center, HandleUtility.GetHandleSize(data.Pivot) * 0.12f, false);
            }
        }
        else if (selectedObjects.Length == 1 && SizeVisualizerOverlay.ShowPivots)
        {
            var data = objectDataList[0];
            DrawPivot(data.Pivot, data.Bounds.center, HandleUtility.GetHandleSize(data.Pivot) * 0.15f, false);
        }

        // Draw combined visualization
        DrawCombinedVisualization(combinedBounds, combinedPivot, sceneView, selectedObjects.Length);

        // Draw combined pivot for multi-select
        if (selectedObjects.Length > 1 && SizeVisualizerOverlay.ShowPivots)
        {
            DrawPivot(combinedPivot, combinedBounds.center, HandleUtility.GetHandleSize(combinedPivot) * 0.2f, true);
        }

        // Draw info panel
        DrawInfoPanel(combinedBounds, combinedPivot, objectDataList, sceneView, selectedObjects.Length);
    }

    private struct ObjectData
    {
        public Bounds Bounds;
        public Vector3 Pivot;
        public string Name;
    }

    private static ObjectData? GetObjectData(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>()
            .Where(r => r.enabled && r.gameObject.activeInHierarchy)
            .ToArray();

        if (renderers.Length == 0) return null;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return new ObjectData
        {
            Bounds = bounds,
            Pivot = obj.transform.position,
            Name = obj.name
        };
    }

    private static Bounds GetCombinedBounds(List<ObjectData> dataList)
    {
        var bounds = dataList[0].Bounds;
        for (int i = 1; i < dataList.Count; i++)
            bounds.Encapsulate(dataList[i].Bounds);
        return bounds;
    }

    private static Vector3 GetCombinedPivot(GameObject[] objects)
    {
        var sum = Vector3.zero;
        foreach (var obj in objects)
            sum += obj.transform.position;
        return sum / objects.Length;
    }

    private static void DrawIndividualBounds(Bounds bounds)
    {
        Handles.color = IndividualBoundsColor;
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private static void DrawPivot(Vector3 pivot, Vector3 boundsCenter, float size, bool isCombined)
    {
        var color = isCombined ? CombinedPivotColor : PivotColor;
        
        // Draw pivot sphere
        Handles.color = color;
        Handles.SphereHandleCap(0, pivot, Quaternion.identity, size, EventType.Repaint);

        // Draw axes at pivot
        float axisLength = size * 2.5f;
        float thickness = isCombined ? 3f : 2f;

        Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        Handles.DrawLine(pivot, pivot + Vector3.right * axisLength, thickness);
        
        Handles.color = new Color(0.2f, 1f, 0.2f, 1f);
        Handles.DrawLine(pivot, pivot + Vector3.up * axisLength, thickness);
        
        Handles.color = new Color(0.2f, 0.4f, 1f, 1f);
        Handles.DrawLine(pivot, pivot + Vector3.forward * axisLength, thickness);

        // Draw offset line from pivot to bounds center
        if (Vector3.Distance(pivot, boundsCenter) > 0.01f)
        {
            Handles.color = PivotOffsetColor;
            Handles.DrawDottedLine(pivot, boundsCenter, 4f);

            // Draw small diamond at bounds center
            float diamondSize = size * 0.4f;
            Handles.color = new Color(1f, 1f, 0.3f, 0.9f);
            DrawDiamond(boundsCenter, diamondSize);
        }

        // Pivot label
        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            normal = { textColor = color }
        };
        
        string label = isCombined ? "◉ COMBINED PIVOT" : "◉ PIVOT";
        Handles.Label(pivot + Vector3.up * size * 2f, label, labelStyle);
    }

    private static void DrawDiamond(Vector3 center, float size)
    {
        var up = center + Vector3.up * size;
        var down = center - Vector3.up * size;
        var left = center - Vector3.right * size;
        var right = center + Vector3.right * size;
        var front = center + Vector3.forward * size;
        var back = center - Vector3.forward * size;

        Handles.DrawLine(up, right, 2f);
        Handles.DrawLine(right, down, 2f);
        Handles.DrawLine(down, left, 2f);
        Handles.DrawLine(left, up, 2f);

        Handles.DrawLine(up, front, 2f);
        Handles.DrawLine(front, down, 2f);
        Handles.DrawLine(down, back, 2f);
        Handles.DrawLine(back, up, 2f);

        Handles.DrawLine(left, front, 2f);
        Handles.DrawLine(front, right, 2f);
        Handles.DrawLine(right, back, 2f);
        Handles.DrawLine(back, left, 2f);
    }

    private static void DrawCombinedVisualization(Bounds bounds, Vector3 pivot, SceneView sceneView, int objectCount)
    {
        var center = bounds.center;
        var size = bounds.size;
        var extents = bounds.extents;
        var radius = extents.magnitude;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        // Draw main bounding box
        Handles.color = CombinedBoundsColor;
        Handles.DrawWireCube(center, size);

        // Draw corner accents
        DrawCornerAccents(bounds);

        // Draw dimension lines
        DrawDimensionLines(bounds);

        // Draw radius circle
        Handles.color = RadiusColor;
        Handles.DrawWireDisc(center, sceneView.camera.transform.forward, radius);

        // Draw dimension labels in world space
        DrawWorldLabels(bounds, radius);
    }

    private static void DrawCornerAccents(Bounds bounds)
    {
        var corners = GetCorners(bounds);
        float accentLength = Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z) * 0.12f;

        Handles.color = new Color(0.4f, 1f, 0.8f, 1f);
        foreach (var corner in corners)
        {
            var dirToCenter = (bounds.center - corner).normalized;
            Vector3[] dirs = {
                Vector3.right * Mathf.Sign(dirToCenter.x),
                Vector3.up * Mathf.Sign(dirToCenter.y),
                Vector3.forward * Mathf.Sign(dirToCenter.z)
            };

            foreach (var dir in dirs)
                Handles.DrawLine(corner, corner + dir * accentLength, 3f);
        }
    }

    private static Vector3[] GetCorners(Bounds b)
    {
        var min = b.min;
        var max = b.max;
        return new Vector3[]
        {
            new(min.x, min.y, min.z), new(max.x, min.y, min.z),
            new(min.x, max.y, min.z), new(max.x, max.y, min.z),
            new(min.x, min.y, max.z), new(max.x, min.y, max.z),
            new(min.x, max.y, max.z), new(max.x, max.y, max.z),
        };
    }

    private static void DrawDimensionLines(Bounds bounds)
    {
        var center = bounds.center;
        var extents = bounds.extents;
        float offset = Mathf.Max(0.1f, extents.magnitude * 0.05f);

        // X dimension
        Handles.color = new Color(1f, 0.3f, 0.3f, 1f);
        var xStart = center + new Vector3(-extents.x, -extents.y - offset, -extents.z - offset);
        var xEnd = center + new Vector3(extents.x, -extents.y - offset, -extents.z - offset);
        DrawDimensionArrow(xStart, xEnd);

        // Y dimension
        Handles.color = new Color(0.3f, 1f, 0.3f, 1f);
        var yStart = center + new Vector3(-extents.x - offset, -extents.y, -extents.z - offset);
        var yEnd = center + new Vector3(-extents.x - offset, extents.y, -extents.z - offset);
        DrawDimensionArrow(yStart, yEnd);

        // Z dimension
        Handles.color = new Color(0.3f, 0.5f, 1f, 1f);
        var zStart = center + new Vector3(-extents.x - offset, -extents.y - offset, -extents.z);
        var zEnd = center + new Vector3(-extents.x - offset, -extents.y - offset, extents.z);
        DrawDimensionArrow(zStart, zEnd);
    }

    private static void DrawDimensionArrow(Vector3 start, Vector3 end)
    {
        Handles.DrawLine(start, end, 2f);
        float arrowSize = HandleUtility.GetHandleSize((start + end) * 0.5f) * 0.08f;
        var dir = (end - start).normalized;
        Handles.ConeHandleCap(0, end - dir * arrowSize, Quaternion.LookRotation(dir), arrowSize, EventType.Repaint);
        Handles.ConeHandleCap(0, start + dir * arrowSize, Quaternion.LookRotation(-dir), arrowSize, EventType.Repaint);
    }

    private static void DrawWorldLabels(Bounds bounds, float radius)
    {
        var center = bounds.center;
        var extents = bounds.extents;
        float offset = Mathf.Max(0.2f, extents.magnitude * 0.08f);

        var bgStyle = new GUIStyle(EditorStyles.helpBox)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(4, 4, 2, 2)
        };

        // X label
        bgStyle.normal.textColor = new Color(1f, 0.4f, 0.4f, 1f);
        Handles.Label(center + new Vector3(0, -extents.y - offset * 2, -extents.z - offset * 2),
            $"X: {bounds.size.x:F2}m", bgStyle);

        // Y label
        bgStyle.normal.textColor = new Color(0.4f, 1f, 0.4f, 1f);
        Handles.Label(center + new Vector3(-extents.x - offset * 2, 0, -extents.z - offset * 2),
            $"Y: {bounds.size.y:F2}m", bgStyle);

        // Z label
        bgStyle.normal.textColor = new Color(0.4f, 0.6f, 1f, 1f);
        Handles.Label(center + new Vector3(-extents.x - offset * 2, -extents.y - offset * 2, 0),
            $"Z: {bounds.size.z:F2}m", bgStyle);

        // Radius label
        bgStyle.normal.textColor = RadiusColor;
        Handles.Label(center + Vector3.up * (extents.y + offset * 3),
            $"⌀ {radius:F2}m", bgStyle);
    }

    private static void DrawInfoPanel(Bounds bounds, Vector3 pivot, List<ObjectData> dataList, 
        SceneView sceneView, int objectCount)
    {
        Handles.BeginGUI();

        float panelWidth = 220f;
        float panelHeight = objectCount > 1 ? 200f : 170f;
        var rect = new Rect(10, sceneView.position.height - panelHeight - 30, panelWidth, panelHeight);

        // Background
        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        GUILayout.BeginArea(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, rect.height - 16));

        // Header
        var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, richText = true };
        string header = objectCount > 1 
            ? $"📐 <color=#66DDFF>Combined ({objectCount} objects)</color>" 
            : "📐 Object Size";
        GUILayout.Label(header, headerStyle);

        EditorGUILayout.Space(4);

        // Dimensions section
        var labelStyle = new GUIStyle(EditorStyles.label) { richText = true, fontSize = 11 };
        var size = bounds.size;
        var radius = bounds.extents.magnitude;

        GUILayout.Label($"<b>━━ Dimensions ━━</b>", labelStyle);
        GUILayout.Label($"<color=#FF6666>X:</color> {size.x:F3} m", labelStyle);
        GUILayout.Label($"<color=#66FF66>Y:</color> {size.y:F3} m", labelStyle);
        GUILayout.Label($"<color=#6699FF>Z:</color> {size.z:F3} m", labelStyle);
        GUILayout.Label($"<color=#FFAA44>Radius:</color> {radius:F3} m", labelStyle);

        EditorGUILayout.Space(4);

        // Pivot section
        GUILayout.Label($"<b>━━ Pivot Point ━━</b>", labelStyle);
        var pivotOffset = pivot - bounds.center;
        string pivotLabel = objectCount > 1 ? "Combined" : "Local";
        GUILayout.Label($"<color=#FF66FF>{pivotLabel}:</color> ({pivot.x:F2}, {pivot.y:F2}, {pivot.z:F2})", labelStyle);
        GUILayout.Label($"<color=#FFFF66>Offset:</color> ({pivotOffset.x:F2}, {pivotOffset.y:F2}, {pivotOffset.z:F2})", labelStyle);

        // Multi-select breakdown
        if (objectCount > 1)
        {
            EditorGUILayout.Space(4);
            GUILayout.Label($"<b>━━ Selection ━━</b>", labelStyle);
            
            int shown = Mathf.Min(dataList.Count, 3);
            for (int i = 0; i < shown; i++)
            {
                var d = dataList[i];
                GUILayout.Label($"• {TruncateName(d.Name, 18)}", labelStyle);
            }
            if (dataList.Count > 3)
                GUILayout.Label($"  <i>+{dataList.Count - 3} more...</i>", labelStyle);
        }

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static string TruncateName(string name, int maxLength)
    {
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 3) + "...";
    }
}