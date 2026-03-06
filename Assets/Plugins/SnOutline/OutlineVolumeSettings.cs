// OutlineVolumeSettings.cs
using System;
using UnityEngine;
using UnityEngine.Rendering;

public enum OutlineMode
{
    LayerMask,
    PerObject,
    Both
}

[Serializable]
public class OutlineGroupSettings
{
    public string name = "Group";
    public bool enabled = true;

    [Header("Targeting")]
    public OutlineMode mode = OutlineMode.LayerMask;
    public LayerMask layerMask = -1;

    [Header("Appearance")]
    [Range(0.1f, 10f)] public float thickness = 1f;
    [ColorUsage(true, true)] public Color color = Color.black;

    [Header("Edge Detection")]
    [Range(0f, 1f)] public float depthThreshold = 0.1f;
    [Range(0f, 1f)] public float normalThreshold = 0.4f;
    [Range(0f, 1f)] public float colorThreshold = 0.1f;
    public bool useDepth = true;
    public bool useNormals = true;
    public bool useColor = false;

    public OutlineGroupSettings Clone()
    {
        return (OutlineGroupSettings)MemberwiseClone();
    }
}

[Serializable, VolumeComponentMenu("Custom/Outline")]
public class OutlineVolumeSettings : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new(false);
    public ClampedFloatParameter globalIntensity = new(1f, 0f, 2f);

    [Header("Outline Groups")]
    public OutlineGroupListParameter groups = new(new OutlineGroupSettings[]
    {
        new() { name = "Default", mode = OutlineMode.LayerMask, color = Color.black },
        new() { name = "Highlight", mode = OutlineMode.PerObject, color = Color.yellow, thickness = 2f }
    });

    public bool IsActive() => enabled.value && globalIntensity.value > 0f;
    public bool IsTileCompatible() => true;
}

[Serializable]
public class OutlineGroupListParameter : VolumeParameter<OutlineGroupSettings[]>
{
    public OutlineGroupListParameter(OutlineGroupSettings[] value, bool overrideState = false)
        : base(value, overrideState) { }
}