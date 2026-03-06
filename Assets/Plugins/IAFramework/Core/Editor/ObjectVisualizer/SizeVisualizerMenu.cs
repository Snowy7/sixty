// Menu shortcuts

using UnityEditor;

public static class SizeVisualizerMenu
{
    [MenuItem("Tools/Size Visualizer/Toggle Enabled _F2")]
    private static void ToggleEnabled()
    {
        SizeVisualizerOverlay.IsEnabled = !SizeVisualizerOverlay.IsEnabled;
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Size Visualizer/Toggle Pivots _F3")]
    private static void TogglePivots()
    {
        SizeVisualizerOverlay.ShowPivots = !SizeVisualizerOverlay.ShowPivots;
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Size Visualizer/Toggle Individual Bounds _F4")]
    private static void ToggleIndividual()
    {
        SizeVisualizerOverlay.ShowIndividualBounds = !SizeVisualizerOverlay.ShowIndividualBounds;
        SceneView.RepaintAll();
    }
}