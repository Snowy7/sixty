// VolumetricFogRenderPass.cs

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class VolumetricFogRenderPass : ScriptableRenderPass, IDisposable
{
    private const string k_PassName = "Volumetric Fog";
    
    private readonly Material _fogMaterial;
    private readonly Material _compositeMaterial;
    private readonly Material _depthDownsampleMaterial;
    private readonly bool _showDebug;
    
    private VolumetricFogVolumeComponent _volume;
    
    // Temporal history per camera
    private readonly Dictionary<int, TemporalData> _temporalDataMap = new();
    
    private static int s_FrameCount;

    private class TemporalData
    {
        public RTHandle HistoryBuffer;
        public Matrix4x4 PrevViewProjection;
        public int LastFrameUsed;
    }

    #region Shader Property IDs

    private static class ShaderIDs
    {
        // Fog parameters
        public static readonly int _Density = Shader.PropertyToID("_Density");
        public static readonly int _BaseHeight = Shader.PropertyToID("_BaseHeight");
        public static readonly int _MaxHeight = Shader.PropertyToID("_MaxHeight");
        public static readonly int _HeightFalloff = Shader.PropertyToID("_HeightFalloff");
        public static readonly int _Albedo = Shader.PropertyToID("_Albedo");
        public static readonly int _AmbientLight = Shader.PropertyToID("_AmbientLight");
        public static readonly int _Anisotropy = Shader.PropertyToID("_Anisotropy");
        public static readonly int _ScatteringIntensity = Shader.PropertyToID("_ScatteringIntensity");
        public static readonly int _Extinction = Shader.PropertyToID("_Extinction");

        // Quality
        public static readonly int _Steps = Shader.PropertyToID("_Steps");
        public static readonly int _MaxDistance = Shader.PropertyToID("_MaxDistance");
        public static readonly int _TemporalBlend = Shader.PropertyToID("_TemporalBlend");
        public static readonly int _FrameIndex = Shader.PropertyToID("_FrameIndex");

        // Noise
        public static readonly int _NoiseEnabled = Shader.PropertyToID("_NoiseEnabled");
        public static readonly int _NoiseScale = Shader.PropertyToID("_NoiseScale");
        public static readonly int _NoiseIntensity = Shader.PropertyToID("_NoiseIntensity");
        public static readonly int _NoiseWind = Shader.PropertyToID("_NoiseWind");
        public static readonly int _NoiseOctaves = Shader.PropertyToID("_NoiseOctaves");

        // Lighting
        public static readonly int _MainLightEnabled = Shader.PropertyToID("_MainLightEnabled");
        public static readonly int _AdditionalLightsEnabled = Shader.PropertyToID("_AdditionalLightsEnabled");
        public static readonly int _MaxAdditionalLights = Shader.PropertyToID("_MaxAdditionalLights");
        public static readonly int _PointLightIntensity = Shader.PropertyToID("_PointLightIntensity");
        public static readonly int _SpotLightIntensity = Shader.PropertyToID("_SpotLightIntensity");

        // Shadows
        public static readonly int _VolumetricShadows = Shader.PropertyToID("_VolumetricShadows");
        public static readonly int _ShadowIntensity = Shader.PropertyToID("_ShadowIntensity");
        public static readonly int _ShadowSteps = Shader.PropertyToID("_ShadowSteps");

        // Textures
        public static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
        public static readonly int _FogTexture = Shader.PropertyToID("_FogTexture");
        public static readonly int _HistoryTexture = Shader.PropertyToID("_HistoryTexture");
        public static readonly int _FullResDepth = Shader.PropertyToID("_FullResDepth");
        public static readonly int _HalfResDepth = Shader.PropertyToID("_HalfResDepth");

        // Matrices & vectors
        public static readonly int _PrevViewProjMatrix = Shader.PropertyToID("_PrevViewProjMatrix");
        public static readonly int _InvViewProjMatrix = Shader.PropertyToID("_InvViewProjMatrix");
        public static readonly int _Resolution = Shader.PropertyToID("_Resolution");
        public static readonly int _HalfResolution = Shader.PropertyToID("_HalfResolution");
        public static readonly int _CameraForward = Shader.PropertyToID("_CameraForward");

        // Flags
        public static readonly int _UseTemporalReprojection = Shader.PropertyToID("_UseTemporalReprojection");
        public static readonly int _IsHalfRes = Shader.PropertyToID("_IsHalfRes");

        public static readonly int _DistanceFadeEnabled =
            Shader.PropertyToID("_DistanceFadeEnabled");

        public static readonly int _DistanceFadeStart =
            Shader.PropertyToID("_DistanceFadeStart");

        public static readonly int _DistanceFadeEnd =
            Shader.PropertyToID("_DistanceFadeEnd");

        public static readonly int _DistanceFadeExponent =
            Shader.PropertyToID("_DistanceFadeExponent");

        public static readonly int _DistanceFadeStrength =
            Shader.PropertyToID("_DistanceFadeStrength");

        public static readonly int _DistanceFadeColor =
            Shader.PropertyToID("_DistanceFadeColor");

        public static readonly int _DistanceFadeAffectsSkybox =
            Shader.PropertyToID("_DistanceFadeAffectsSkybox");
    }

    #endregion

    public VolumetricFogRenderPass(
        Material fogMaterial, 
        Material compositeMaterial,
        Material depthDownsampleMaterial,
        bool showDebug)
    {
        _fogMaterial = fogMaterial;
        _compositeMaterial = compositeMaterial;
        _depthDownsampleMaterial = depthDownsampleMaterial;
        _showDebug = showDebug;
        
        profilingSampler = new ProfilingSampler(k_PassName);
        requiresIntermediateTexture = true;
    }

    public void Setup(VolumetricFogVolumeComponent volume)
    {
        _volume = volume;
        ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    #region Pass Data Classes
    
    private class DepthDownsamplePassData
    {
        public TextureHandle FullResDepth;
        public TextureHandle HalfResDepth;
        public Material Material;
        public Vector4 Resolution;
    }

    private class FogRaymarchPassData
    {
        public TextureHandle SourceColor;
        public TextureHandle DepthTexture;
        public TextureHandle HalfResDepth;
        public TextureHandle HistoryTexture;
        public TextureHandle OutputFog;
        public Material Material;
        public bool UseHistory;
        public bool IsHalfRes;
        public Matrix4x4 PrevViewProj;
        public Matrix4x4 InvViewProj;
        public Vector4 Resolution;
        public Vector3 CameraForward;
    }

    private class CompositePassData
    {
        public TextureHandle SourceColor;
        public TextureHandle FogTexture;
        public TextureHandle FullResDepth;
        public TextureHandle HalfResDepth;
        public TextureHandle Output;
        public Material Material;
        public bool IsHalfRes;
        public Vector4 FullResolution;
        public Vector4 HalfResolution;
    }

    private class CopyToHistoryPassData
    {
        public TextureHandle Source;
        public TextureHandle Destination;
    }

    #endregion

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        
        if (resourceData.isActiveTargetBackBuffer)
            return;

        if (_volume == null || !_volume.IsActive())
            return;

        var camera = cameraData.camera;
        var descriptor = cameraData.cameraTargetDescriptor;
        
        var fullWidth = descriptor.width;
        var fullHeight = descriptor.height;
        var useHalfRes = _volume.halfResolution.value;
        var halfWidth = Mathf.Max(1, fullWidth / 2);
        var halfHeight = Mathf.Max(1, fullHeight / 2);
        
        var workingWidth = useHalfRes ? halfWidth : fullWidth;
        var workingHeight = useHalfRes ? halfHeight : fullHeight;
        
        // Update material properties
        UpdateMaterialProperties(cameraData);
        
        // Get/create temporal data
        var cameraId = camera.GetInstanceID();
        var temporalData = GetOrCreateTemporalData(cameraId, workingWidth, workingHeight);
        var useTemporalReprojection = _volume.temporalReprojection.value && temporalData.LastFrameUsed == s_FrameCount - 1;
        
        // Create textures
        var fogOutputDesc = new TextureDesc(workingWidth, workingHeight)
        {
            colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
            clearBuffer = true,
            clearColor = Color.clear,
            name = "VolumetricFogOutput"
        };
        var fogOutput = renderGraph.CreateTexture(fogOutputDesc);

        // Half-res depth (for edge-aware upsampling)
        TextureHandle halfResDepth = TextureHandle.nullHandle;
        if (useHalfRes)
        {
            var halfResDepthDesc = new TextureDesc(halfWidth, halfHeight)
            {
                colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat,
                clearBuffer = false,
                name = "HalfResDepth"
            };
            halfResDepth = renderGraph.CreateTexture(halfResDepthDesc);
            
            // Downsample depth
            RecordDepthDownsample(renderGraph, resourceData.cameraDepthTexture, halfResDepth, 
                new Vector4(halfWidth, halfHeight, 1f / halfWidth, 1f / halfHeight));
        }

        // Import history buffer
        TextureHandle historyHandle = TextureHandle.nullHandle;
        if (useTemporalReprojection && temporalData.HistoryBuffer != null)
        {
            historyHandle = renderGraph.ImportTexture(temporalData.HistoryBuffer);
        }

        // Calculate matrices
        var view = camera.worldToCameraMatrix;
        var proj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        var viewProj = proj * view;
        var invViewProj = viewProj.inverse;

        // Main fog raymarching pass
        RecordFogRaymarch(
            renderGraph,
            resourceData.activeColorTexture,
            resourceData.cameraDepthTexture,
            useHalfRes ? halfResDepth : resourceData.cameraDepthTexture,
            historyHandle,
            fogOutput,
            useTemporalReprojection,
            useHalfRes,
            temporalData.PrevViewProjection,
            invViewProj,
            new Vector4(workingWidth, workingHeight, 1f / workingWidth, 1f / workingHeight),
            camera.transform.forward
        );

        // Copy to history for next frame
        if (_volume.temporalReprojection.value && temporalData.HistoryBuffer != null)
        {
            var historyDest = renderGraph.ImportTexture(temporalData.HistoryBuffer);
            RecordCopyToHistory(renderGraph, fogOutput, historyDest);
        }

        // Composite pass
        var compositeOutputDesc = new TextureDesc(fullWidth, fullHeight)
        {
            colorFormat = descriptor.graphicsFormat,
            clearBuffer = false,
            name = "VolumetricFogComposite"
        };
        var compositeOutput = renderGraph.CreateTexture(compositeOutputDesc);

        RecordComposite(
            renderGraph,
            resourceData.activeColorTexture,
            fogOutput,
            resourceData.cameraDepthTexture,
            useHalfRes ? halfResDepth : resourceData.cameraDepthTexture,
            compositeOutput,
            useHalfRes,
            new Vector4(fullWidth, fullHeight, 1f / fullWidth, 1f / fullHeight),
            new Vector4(halfWidth, halfHeight, 1f / halfWidth, 1f / halfHeight)
        );

        // Copy result back to camera target
        RecordCopyBack(renderGraph, compositeOutput, resourceData.activeColorTexture);

        // Update temporal data for next frame
        temporalData.PrevViewProjection = viewProj;
        temporalData.LastFrameUsed = s_FrameCount;
        s_FrameCount++;
    }

    private void RecordDepthDownsample(RenderGraph renderGraph, TextureHandle source, TextureHandle dest, Vector4 resolution)
    {
        using var builder = renderGraph.AddRasterRenderPass<DepthDownsamplePassData>("Downsample Depth", out var passData);
        
        passData.FullResDepth = source;
        passData.HalfResDepth = dest;
        passData.Material = _depthDownsampleMaterial;
        passData.Resolution = resolution;
        
        builder.UseTexture(passData.FullResDepth, AccessFlags.Read);
        builder.SetRenderAttachment(passData.HalfResDepth, 0, AccessFlags.Write);
        
        builder.SetRenderFunc(static (DepthDownsamplePassData data, RasterGraphContext ctx) =>
        {
            data.Material.SetVector(ShaderIDs._Resolution, data.Resolution);
            Blitter.BlitTexture(ctx.cmd, data.FullResDepth, new Vector4(1, 1, 0, 0), data.Material, 0);
        });
    }

    private void RecordFogRaymarch(
        RenderGraph renderGraph,
        TextureHandle sourceColor,
        TextureHandle depthTexture,
        TextureHandle halfResDepth,
        TextureHandle historyTexture,
        TextureHandle outputFog,
        bool useHistory,
        bool isHalfRes,
        Matrix4x4 prevViewProj,
        Matrix4x4 invViewProj,
        Vector4 resolution,
        Vector3 cameraForward)
    {
        using var builder = renderGraph.AddRasterRenderPass<FogRaymarchPassData>("Fog Raymarch", out var passData);
        
        passData.SourceColor = sourceColor;
        passData.DepthTexture = depthTexture;
        passData.HalfResDepth = halfResDepth;
        passData.HistoryTexture = historyTexture;
        passData.OutputFog = outputFog;
        passData.Material = _fogMaterial;
        passData.UseHistory = useHistory && historyTexture.IsValid();
        passData.IsHalfRes = isHalfRes;
        passData.PrevViewProj = prevViewProj;
        passData.InvViewProj = invViewProj;
        passData.Resolution = resolution;
        passData.CameraForward = cameraForward;
        
        builder.UseTexture(passData.SourceColor, AccessFlags.Read);
        builder.UseTexture(passData.DepthTexture, AccessFlags.Read);
        if (isHalfRes && halfResDepth.IsValid())
            builder.UseTexture(passData.HalfResDepth, AccessFlags.Read);
        if (passData.UseHistory)
            builder.UseTexture(passData.HistoryTexture, AccessFlags.Read);
        builder.SetRenderAttachment(passData.OutputFog, 0, AccessFlags.Write);
        
        builder.SetRenderFunc(static (FogRaymarchPassData data, RasterGraphContext ctx) =>
        {
            var mat = data.Material;
            
            mat.SetTexture(ShaderIDs._SourceTexture, data.SourceColor);
            mat.SetTexture(ShaderIDs._FullResDepth, data.DepthTexture);
            mat.SetTexture(ShaderIDs._HalfResDepth, data.HalfResDepth);
            mat.SetFloat(ShaderIDs._UseTemporalReprojection, data.UseHistory ? 1f : 0f);
            mat.SetFloat(ShaderIDs._IsHalfRes, data.IsHalfRes ? 1f : 0f);
            mat.SetMatrix(ShaderIDs._PrevViewProjMatrix, data.PrevViewProj);
            mat.SetMatrix(ShaderIDs._InvViewProjMatrix, data.InvViewProj);
            mat.SetVector(ShaderIDs._Resolution, data.Resolution);
            mat.SetVector(ShaderIDs._CameraForward, data.CameraForward);
            
            if (data.UseHistory)
                mat.SetTexture(ShaderIDs._HistoryTexture, data.HistoryTexture);
            
            Blitter.BlitTexture(ctx.cmd, data.SourceColor, new Vector4(1, 1, 0, 0), data.Material, 0);
        });
    }

    private void RecordComposite(
        RenderGraph renderGraph,
        TextureHandle sourceColor,
        TextureHandle fogTexture,
        TextureHandle fullResDepth,
        TextureHandle halfResDepth,
        TextureHandle output,
        bool isHalfRes,
        Vector4 fullResolution,
        Vector4 halfResolution)
    {
        using var builder = renderGraph.AddRasterRenderPass<CompositePassData>("Fog Composite", out var passData);
    
        passData.SourceColor = sourceColor;
        passData.FogTexture = fogTexture;
        passData.FullResDepth = fullResDepth;
        passData.HalfResDepth = halfResDepth;
        passData.Output = output;
        passData.Material = _compositeMaterial;
        passData.IsHalfRes = isHalfRes;
        passData.FullResolution = fullResolution;
        passData.HalfResolution = halfResolution;
    
        builder.UseTexture(passData.SourceColor, AccessFlags.Read);
        builder.UseTexture(passData.FogTexture, AccessFlags.Read);
        builder.UseTexture(passData.FullResDepth, AccessFlags.Read);
        if (isHalfRes && halfResDepth.IsValid())
            builder.UseTexture(passData.HalfResDepth, AccessFlags.Read);
        builder.SetRenderAttachment(passData.Output, 0, AccessFlags.Write);
    
        builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext ctx) =>
        {
            var mat = data.Material;
        
            mat.SetTexture(ShaderIDs._SourceTexture, data.SourceColor);
            mat.SetTexture(ShaderIDs._FogTexture, data.FogTexture);
            mat.SetTexture(ShaderIDs._FullResDepth, data.FullResDepth);
            mat.SetTexture(ShaderIDs._HalfResDepth, data.HalfResDepth);
            mat.SetFloat(ShaderIDs._IsHalfRes, data.IsHalfRes ? 1f : 0f);
            mat.SetVector(ShaderIDs._Resolution, data.FullResolution);
            mat.SetVector(ShaderIDs._HalfResolution, data.HalfResolution);
        
            Blitter.BlitTexture(ctx.cmd, data.SourceColor, new Vector4(1, 1, 0, 0), mat, 0);
        });
    }

    private void RecordCopyToHistory(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
    {
        using var builder = renderGraph.AddRasterRenderPass<CopyToHistoryPassData>("Copy Fog History", out var passData);
        
        passData.Source = source;
        passData.Destination = destination;
        
        builder.UseTexture(passData.Source, AccessFlags.Read);
        builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
        
        builder.SetRenderFunc(static (CopyToHistoryPassData data, RasterGraphContext ctx) =>
        {
            Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1, 1, 0, 0), 0, false);
        });
    }

    private void RecordCopyBack(RenderGraph renderGraph, TextureHandle source, TextureHandle destination)
    {
        using var builder = renderGraph.AddRasterRenderPass<CopyToHistoryPassData>("Copy Fog Result", out var passData);
        
        passData.Source = source;
        passData.Destination = destination;
        
        builder.UseTexture(passData.Source, AccessFlags.Read);
        builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
        
        builder.SetRenderFunc(static (CopyToHistoryPassData data, RasterGraphContext ctx) =>
        {
            Blitter.BlitTexture(ctx.cmd, data.Source, new Vector4(1, 1, 0, 0), 0, false);
        });
    }

    private void UpdateMaterialProperties(UniversalCameraData cameraData)
    {
        var mat = _fogMaterial;
        
        // Fog parameters
        mat.SetFloat(ShaderIDs._Density, _volume.density.value);
        mat.SetFloat(ShaderIDs._BaseHeight, _volume.baseHeight.value);
        mat.SetFloat(ShaderIDs._MaxHeight, _volume.maxHeight.value);
        mat.SetFloat(ShaderIDs._HeightFalloff, _volume.heightFalloff.value);
        mat.SetColor(ShaderIDs._Albedo, _volume.albedo.value);
        mat.SetColor(ShaderIDs._AmbientLight, _volume.ambientLight.value);
        mat.SetFloat(ShaderIDs._Anisotropy, _volume.anisotropy.value);
        mat.SetFloat(ShaderIDs._ScatteringIntensity, _volume.scatteringIntensity.value);
        mat.SetFloat(ShaderIDs._Extinction, _volume.extinction.value);
        
        // Quality
        mat.SetInt(ShaderIDs._Steps, _volume.steps.value);
        mat.SetFloat(ShaderIDs._MaxDistance, _volume.maxDistance.value);
        mat.SetFloat(ShaderIDs._TemporalBlend, _volume.temporalBlendFactor.value);
        mat.SetInt(ShaderIDs._FrameIndex, s_FrameCount);
        
        // Noise
        mat.SetFloat(ShaderIDs._NoiseEnabled, _volume.enableNoise.value ? 1f : 0f);
        mat.SetFloat(ShaderIDs._NoiseScale, _volume.noiseScale.value);
        mat.SetFloat(ShaderIDs._NoiseIntensity, _volume.noiseIntensity.value);
        mat.SetVector(ShaderIDs._NoiseWind, _volume.noiseWind.value);
        mat.SetInt(ShaderIDs._NoiseOctaves, _volume.noiseOctaves.value);
        
        // Lighting
        mat.SetFloat(ShaderIDs._MainLightEnabled, _volume.mainLightScattering.value ? 1f : 0f);
        mat.SetFloat(ShaderIDs._AdditionalLightsEnabled, _volume.additionalLightsScattering.value ? 1f : 0f);
        mat.SetInt(ShaderIDs._MaxAdditionalLights, _volume.maxAdditionalLights.value);
        mat.SetFloat(ShaderIDs._PointLightIntensity, _volume.pointLightIntensity.value);
        mat.SetFloat(ShaderIDs._SpotLightIntensity, _volume.spotLightIntensity.value);
        
        // Shadows
        mat.SetFloat(ShaderIDs._VolumetricShadows, _volume.volumetricShadows.value ? 1f : 0f);
        mat.SetFloat(ShaderIDs._ShadowIntensity, _volume.shadowIntensity.value);
        mat.SetInt(ShaderIDs._ShadowSteps, _volume.shadowSteps.value);
        
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeEnabled,
            _volume.distanceFade.value ? 1f : 0f
        );
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeStart,
            _volume.distanceFadeStart.value
        );
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeEnd,
            _volume.distanceFadeEnd.value
        );
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeExponent,
            _volume.distanceFadeExponent.value
        );
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeStrength,
            _volume.distanceFadeStrength.value
        );
        _compositeMaterial.SetColor(
            ShaderIDs._DistanceFadeColor,
            _volume.distanceFadeColor.value
        );
        _compositeMaterial.SetFloat(
            ShaderIDs._DistanceFadeAffectsSkybox,
            _volume.distanceFadeAffectsSkybox.value ? 1f : 0f
        );
    }

    private TemporalData GetOrCreateTemporalData(int cameraId, int width, int height)
    {
        if (!_temporalDataMap.TryGetValue(cameraId, out var data))
        {
            data = new TemporalData
            {
                PrevViewProjection = Matrix4x4.identity,
                LastFrameUsed = -1
            };
            _temporalDataMap[cameraId] = data;
        }

        // Recreate history buffer if size changed
        if (data.HistoryBuffer == null || 
            data.HistoryBuffer.rt.width != width || 
            data.HistoryBuffer.rt.height != height)
        {
            data.HistoryBuffer?.Release();
            
            var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0)
            {
                enableRandomWrite = false,
                msaaSamples = 1
            };
            data.HistoryBuffer = RTHandles.Alloc(desc, name: $"VolumetricFogHistory_{cameraId}");
        }

        return data;
    }

    public void Dispose()
    {
        foreach (var kvp in _temporalDataMap)
        {
            kvp.Value.HistoryBuffer?.Release();
        }
        _temporalDataMap.Clear();
    }
}