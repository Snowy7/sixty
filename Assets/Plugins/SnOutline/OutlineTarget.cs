// OutlineTarget.cs
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class OutlineTarget : MonoBehaviour
{
    private static readonly HashSet<OutlineTarget> _activeTargets = new();
    public static IReadOnlyCollection<OutlineTarget> ActiveTargets => _activeTargets;

    public enum TargetMode
    {
        Auto,           // Find group by layer match
        GroupName,      // Match by group name
        CustomSettings  // Use own settings, ignore groups
    }

    [SerializeField] private TargetMode _mode = TargetMode.Auto;
    [SerializeField] private string _groupName = "Highlight";

    [Header("Custom Settings (when Mode = CustomSettings)")]
    [SerializeField] private OutlineGroupSettings _customSettings = new()
    {
        name = "Custom",
        enabled = true,
        thickness = 2f,
        color = Color.yellow
    };

    [Header("Overrides (when Mode = Auto or GroupName)")]
    [SerializeField] private bool _overrideColor;
    [SerializeField, ColorUsage(true, true)] private Color _color = Color.yellow;
    [SerializeField] private bool _overrideThickness;
    [SerializeField, Range(0.1f, 10f)] private float _thickness = 2f;

    private Renderer[] _renderers;
    private bool _isActive;

    // Public API
    public bool IsActive => _isActive;
    public TargetMode Mode => _mode;
    public string GroupName => _groupName;
    public OutlineGroupSettings CustomSettings => _customSettings;
    public bool OverrideColor => _overrideColor;
    public Color Color => _color;
    public bool OverrideThickness => _overrideThickness;
    public float Thickness => _thickness;

    public Renderer[] Renderers
    {
        get
        {
            if (_renderers == null || _renderers.Length == 0)
                CacheRenderers();
            return _renderers;
        }
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable()
    {
        CacheRenderers();
    }

    private void OnDisable()
    {
        Disable();
    }

    private void OnDestroy()
    {
        Disable();
    }

    private void OnTransformChildrenChanged()
    {
        CacheRenderers();
    }

    /// <summary>Enable outline for this object.</summary>
    public void Enable()
    {
        if (_isActive) return;
        _isActive = true;
        _activeTargets.Add(this);
    }

    /// <summary>Disable outline for this object.</summary>
    public void Disable()
    {
        if (!_isActive) return;
        _isActive = false;
        _activeTargets.Remove(this);
    }

    public void Toggle() => SetActive(!_isActive);
    public void SetActive(bool active) { if (active) Enable(); else Disable(); }

    // Settings API for runtime changes
    public void SetColor(Color color)
    {
        _overrideColor = true;
        _color = color;
    }

    public void SetThickness(float thickness)
    {
        _overrideThickness = true;
        _thickness = thickness;
    }

    public void SetGroupName(string name)
    {
        _mode = TargetMode.GroupName;
        _groupName = name;
    }

    /// <summary>
    /// Finds the best matching group index for this target.
    /// Returns -1 if using CustomSettings mode.
    /// </summary>
    public int FindMatchingGroupIndex(OutlineGroupSettings[] groups)
    {
        if (groups == null || groups.Length == 0) return -1;
        if (_mode == TargetMode.CustomSettings) return -1;

        if (_mode == TargetMode.GroupName)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].name == _groupName)
                    return i;
            }
        }

        // Auto mode: find by layer
        int layer = gameObject.layer;
        int layerBit = 1 << layer;

        // First pass: exact layer match with PerObject or Both mode
        for (int i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null || !g.enabled) continue;
            if (g.mode is OutlineMode.PerObject or OutlineMode.Both)
            {
                if ((g.layerMask & layerBit) != 0)
                    return i;
            }
        }

        // Second pass: any PerObject group
        for (int i = 0; i < groups.Length; i++)
        {
            var g = groups[i];
            if (g == null || !g.enabled) continue;
            if (g.mode == OutlineMode.PerObject)
                return i;
        }

        return -1;
    }

    #if UNITY_EDITOR
    [Header("Editor Preview")]
    [SerializeField] private bool _previewInEditor;

    private bool _wasPreviewActive;

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (_previewInEditor && !_wasPreviewActive)
            {
                Enable();
                _wasPreviewActive = true;
            }
            else if (!_previewInEditor && _wasPreviewActive)
            {
                Disable();
                _wasPreviewActive = false;
            }
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && !_previewInEditor)
        {
            Disable();
            _wasPreviewActive = false;
        }
    }
    #endif
}