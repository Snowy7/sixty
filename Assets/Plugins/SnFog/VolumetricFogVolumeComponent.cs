// VolumetricFogVolumeComponent.cs
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenu("Custom/Volumetric Fog")]
public sealed class VolumetricFogVolumeComponent : VolumeComponent, IPostProcessComponent
{
    // Master toggle
    [Tooltip("Enable or disable volumetric fog")]
    public BoolParameter enabled = new(false);
    
    [Header("Global Fog")]
    [Tooltip("Base fog density")]
    public ClampedFloatParameter density = new(0.02f, 0f, 1.0f);
    
    [Tooltip("Fog starts below this height")]
    public FloatParameter baseHeight = new(0f);
    
    [Tooltip("Maximum height fog can reach")]
    public FloatParameter maxHeight = new(50f);
    
    [Tooltip("How quickly fog density falls off with height")]
    public ClampedFloatParameter heightFalloff = new(0.2f, 0.001f, 2f);
    
    [Tooltip("Base fog color/tint")]
    public ColorParameter albedo = new(new Color(0.9f, 0.9f, 1f, 1f), true, false, true);
    
    [Tooltip("Ambient/environmental light contribution")]
    public ColorParameter ambientLight = new(new Color(0.05f, 0.05f, 0.08f, 1f), true, false, true);
    
    [Header("Scattering")]
    [Tooltip("Scattering anisotropy (-1 = back, 0 = isotropic, 1 = forward)")]
    public ClampedFloatParameter anisotropy = new(0.7f, -0.99f, 0.99f);
    
    [Tooltip("Light scattering intensity multiplier")]
    public ClampedFloatParameter scatteringIntensity = new(1f, 0f, 5f);
    
    [Tooltip("Light absorption/extinction coefficient")]
    public ClampedFloatParameter extinction = new(0.02f, 0.001f, 0.5f);
    
    [Header("Distance Visibility Fade (Aerial Perspective)")]
    public BoolParameter distanceFade = new(true);

    [Tooltip("Distance (meters) where objects start fading into fog.")]
    public ClampedFloatParameter distanceFadeStart = new(60f, 0f, 5000f);

    [Tooltip("Distance (meters) where objects are fully faded into fog.")]
    public ClampedFloatParameter distanceFadeEnd = new(250f, 0f, 5000f);

    [Tooltip("Fade curve. 1 = linear-ish, >1 = smoother/stronger towards the end.")]
    public ClampedFloatParameter distanceFadeExponent = new(1.5f, 0.1f, 8f);

    [Tooltip("How much to apply the distance fade. 0 = off, 1 = fully.")]
    public ClampedFloatParameter distanceFadeStrength = new(1f, 0f, 1f);

    [Tooltip("Color objects fade into at far distances (usually close to your fog tint).")]
    public ColorParameter distanceFadeColor = new(
        new Color(0.8f, 0.85f, 0.95f, 1f),
        true,
        false,
        true
    );

    [Tooltip("If false, distance fade won't be applied on skybox pixels.")]
    public BoolParameter distanceFadeAffectsSkybox = new(false);
    
    [Header("Quality")]
    [Tooltip("Number of raymarching steps (lower = faster)")]
    public ClampedIntParameter steps = new(48, 8, 128);
    
    [Tooltip("Maximum raymarching distance")]
    public ClampedFloatParameter maxDistance = new(200f, 10f, 1000f);
    
    [Tooltip("Render at half resolution for better performance")]
    public BoolParameter halfResolution = new(true);
    
    [Tooltip("Use temporal reprojection to reduce noise")]
    public BoolParameter temporalReprojection = new(true);
    
    [Tooltip("Temporal blend factor (higher = more stable but more ghosting)")]
    public ClampedFloatParameter temporalBlendFactor = new(0.9f, 0f, 0.98f);
    
    [Header("Noise")]
    [Tooltip("Enable animated noise for more natural fog")]
    public BoolParameter enableNoise = new(true);
    
    [Tooltip("Noise texture scale")]
    public ClampedFloatParameter noiseScale = new(0.03f, 0.001f, 0.2f);
    
    [Tooltip("Noise influence on density")]
    public ClampedFloatParameter noiseIntensity = new(0.6f, 0f, 1f);
    
    [Tooltip("Noise animation speed")]
    public Vector3Parameter noiseWind = new(new Vector3(1f, 0.2f, 0.5f));
    
    [Tooltip("Noise detail layers")]
    public ClampedIntParameter noiseOctaves = new(3, 1, 5);
    
    [Header("Lighting")]
    [Tooltip("Include main directional light")]
    public BoolParameter mainLightScattering = new(true);
    
    [Tooltip("Include additional lights (point, spot)")]
    public BoolParameter additionalLightsScattering = new(true);
    
    [Tooltip("Maximum additional lights to process")]
    public ClampedIntParameter maxAdditionalLights = new(8, 0, 16);
    
    [Tooltip("Point light intensity in fog")]
    public ClampedFloatParameter pointLightIntensity = new(1f, 0f, 5f);
    
    [Tooltip("Spot light intensity in fog")]
    public ClampedFloatParameter spotLightIntensity = new(1f, 0f, 5f);
    
    [Header("Volumetric Shadows (Light Shafts)")]
    [Tooltip("Enable volumetric shadows from main light")]
    public BoolParameter volumetricShadows = new(true);
    
    [Tooltip("Shadow intensity")]
    public ClampedFloatParameter shadowIntensity = new(1f, 0f, 1f);
    
    [Tooltip("Shadow samples (higher = better quality shadows)")]
    public ClampedIntParameter shadowSteps = new(8, 1, 16);

    public bool IsActive() => enabled.value && density.value > 0f && active;
    public bool IsTileCompatible() => false;
}