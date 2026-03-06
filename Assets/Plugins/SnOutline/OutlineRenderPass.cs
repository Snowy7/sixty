// OutlineRenderPass.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class OutlineRenderPass : ScriptableRenderPass
{
    private readonly Material _material;
    private OutlineVolumeSettings _volumeSettings;

    private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
    private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
    private static readonly int ColorThresholdId = Shader.PropertyToID("_ColorThreshold");
    private static readonly int GlobalIntensityId = Shader.PropertyToID("_GlobalIntensity");
    private static readonly int UseDepthId = Shader.PropertyToID("_UseDepth");
    private static readonly int UseNormalsId = Shader.PropertyToID("_UseNormals");
    private static readonly int UseColorId = Shader.PropertyToID("_UseColor");
    private static readonly int MaskTextureId = Shader.PropertyToID("_MaskTexture");

    // Cached data rebuilt each frame
    private readonly Dictionary<int, List<OutlineTarget>> _groupedTargets = new();
    private readonly List<OutlineTarget> _customTargets = new();

    public OutlineRenderPass(Material material)
    {
        _material = material;
        profilingSampler = new ProfilingSampler("Outline Pass");
        requiresIntermediateTexture = true;
    }

    public void Setup(OutlineVolumeSettings volumeSettings)
    {
        _volumeSettings = volumeSettings;
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
    }

    private void CategorizeTargets(OutlineGroupSettings[] groups)
    {
        _groupedTargets.Clear();
        _customTargets.Clear();

        foreach (var target in OutlineTarget.ActiveTargets)
        {
            if (target == null || target.Renderers == null || target.Renderers.Length == 0)
                continue;

            if (target.Mode == OutlineTarget.TargetMode.CustomSettings)
            {
                _customTargets.Add(target);
            }
            else
            {
                int idx = target.FindMatchingGroupIndex(groups);
                if (idx >= 0)
                {
                    if (!_groupedTargets.TryGetValue(idx, out var list))
                    {
                        list = new List<OutlineTarget>();
                        _groupedTargets[idx] = list;
                    }
                    list.Add(target);
                }
            }
        }
    }

    private class MaskPassData
    {
        public RendererListHandle rendererList;
        public bool hasRendererList;
    }

    private class ApplyPassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle mask;
        public OutlineGroupSettings settings;
        public float globalIntensity;
    }

    private class FinalCopyPassData
    {
        public TextureHandle source;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (_material == null || _volumeSettings == null) return;

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();
        var renderingData = frameData.Get<UniversalRenderingData>();
        var lightData = frameData.Get<UniversalLightData>();

        if (cameraData.cameraType == CameraType.Preview) return;
        if (resourceData.isActiveTargetBackBuffer) return;

        var groups = _volumeSettings.groups.value;
        CategorizeTargets(groups);

        // Check if anything to render
        bool hasLayerGroups = groups.Any(g => g != null && g.enabled &&
            g.mode is OutlineMode.LayerMask or OutlineMode.Both && g.layerMask != 0);
        bool hasPerObjectGroups = _groupedTargets.Count > 0;
        bool hasCustomTargets = _customTargets.Count > 0;

        if (!hasLayerGroups && !hasPerObjectGroups && !hasCustomTargets) return;

        var backbufferColor = resourceData.activeColorTexture;
        var colorDesc = cameraData.cameraTargetDescriptor;
        colorDesc.depthBufferBits = 0;
        colorDesc.msaaSamples = 1;

        var currentColor = backbufferColor;
        bool didRender = false;  // <-- Track if we rendered anything

        var shaderTags = new List<ShaderTagId>
        {
            new("UniversalForward"),
            new("UniversalForwardOnly"),
            new("UniversalGBuffer"),
            new("SRPDefaultUnlit"),
        };

        // Process standard groups
        for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            var group = groups[groupIndex];
            if (group == null || !group.enabled) continue;

            bool useLayerMask = group.mode is OutlineMode.LayerMask or OutlineMode.Both && group.layerMask != 0;
            bool usePerObject = group.mode is OutlineMode.PerObject or OutlineMode.Both;
            _groupedTargets.TryGetValue(groupIndex, out var targets);
            bool hasTargets = targets != null && targets.Count > 0;

            if (!useLayerMask && (!usePerObject || !hasTargets)) continue;

            currentColor = RenderGroup(
                renderGraph, resourceData, renderingData, cameraData, lightData,
                colorDesc, shaderTags, currentColor, group, targets,
                useLayerMask, usePerObject && hasTargets
            );
            didRender = true;
        }

        // Process custom settings targets (each gets its own pass)
        foreach (var target in _customTargets)
        {
            var settings = target.CustomSettings;
            if (settings == null || !settings.enabled) continue;

            currentColor = RenderGroup(
                renderGraph, resourceData, renderingData, cameraData, lightData,
                colorDesc, shaderTags, currentColor, settings,
                new List<OutlineTarget> { target },
                false, true
            );
            didRender = true;
        }

        // Final copy
        if (didRender)
        {
            using var builder = renderGraph.AddRasterRenderPass<FinalCopyPassData>(
                "Outline Final Copy", out var passData, profilingSampler);

            passData.source = currentColor;
            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(backbufferColor, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((FinalCopyPassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
            });
        }
    }

    private TextureHandle RenderGroup(
        RenderGraph renderGraph,
        UniversalResourceData resourceData,
        UniversalRenderingData renderingData,
        UniversalCameraData cameraData,
        UniversalLightData lightData,
        RenderTextureDescriptor colorDesc,
        List<ShaderTagId> shaderTags,
        TextureHandle currentColor,
        OutlineGroupSettings baseSettings,
        List<OutlineTarget> targets,
        bool useLayerMask,
        bool usePerObject)
    {
        var maskDesc = colorDesc;
        maskDesc.colorFormat = RenderTextureFormat.R8;
        maskDesc.depthBufferBits = 0;

        var mask = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, maskDesc, $"_OutlineMask_{baseSettings.name}", false);

        // Compute effective settings (apply first target's overrides if any)
        var effectiveSettings = baseSettings;
        if (targets != null && targets.Count > 0)
        {
            var first = targets[0];
            if (first.OverrideColor || first.OverrideThickness)
            {
                effectiveSettings = baseSettings.Clone();
                if (first.OverrideColor) effectiveSettings.color = first.Color;
                if (first.OverrideThickness) effectiveSettings.thickness = first.Thickness;
            }
        }

        // Mask pass
        using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                   $"Outline Mask {baseSettings.name}", out var passData, profilingSampler))
        {
            builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);

            passData.hasRendererList = false;

            if (useLayerMask)
            {
                var drawing = RenderingUtils.CreateDrawingSettings(
                    shaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = 1;

                var filtering = new FilteringSettings(RenderQueueRange.all, baseSettings.layerMask);
                var rlp = new RendererListParams(renderingData.cullResults, drawing, filtering);

                passData.rendererList = renderGraph.CreateRendererList(rlp);
                passData.hasRendererList = true;
                builder.UseRendererList(passData.rendererList);
            }

            var capturedMaterial = _material;
            var capturedRenderers = usePerObject && targets != null
                ? targets.SelectMany(t => t.Renderers)
                         .Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy)
                         .ToList()
                : null;

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.ClearRenderTarget(false, true, Color.clear);

                if (data.hasRendererList)
                    ctx.cmd.DrawRendererList(data.rendererList);

                if (capturedRenderers != null)
                {
                    foreach (var renderer in capturedRenderers)
                    {
                        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                            ctx.cmd.DrawRenderer(renderer, capturedMaterial, i, 1);
                    }
                }
            });
        }

        // Apply pass
        var dst = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, colorDesc, $"_OutlineColor_{baseSettings.name}", false);

        using (var builder = renderGraph.AddRasterRenderPass<ApplyPassData>(
                   $"Outline Apply {baseSettings.name}", out var passData, profilingSampler))
        {
            passData.material = _material;
            passData.source = currentColor;
            passData.mask = mask;
            passData.settings = effectiveSettings;
            passData.globalIntensity = _volumeSettings.globalIntensity.value;

            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.UseTexture(passData.mask, AccessFlags.Read);
            builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
            if (resourceData.cameraNormalsTexture.IsValid())
                builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);

            builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((ApplyPassData data, RasterGraphContext ctx) =>
            {
                var s = data.settings;
                data.material.SetFloat(GlobalIntensityId, data.globalIntensity);
                data.material.SetFloat(ThicknessId, s.thickness);
                data.material.SetColor(OutlineColorId, s.color);
                data.material.SetFloat(DepthThresholdId, s.depthThreshold);
                data.material.SetFloat(NormalThresholdId, s.normalThreshold);
                data.material.SetFloat(ColorThresholdId, s.colorThreshold);
                data.material.SetFloat(UseDepthId, s.useDepth ? 1f : 0f);
                data.material.SetFloat(UseNormalsId, s.useNormals ? 1f : 0f);
                data.material.SetFloat(UseColorId, s.useColor ? 1f : 0f);

                ctx.cmd.SetGlobalTexture(MaskTextureId, data.mask);
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        return dst;
    }
}