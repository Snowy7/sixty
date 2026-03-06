// VolumetricFogRenderFeature.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumetricFogRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        
        [Header("Shaders")]
        public Shader volumetricFogShader;
        public Shader compositeShader;
        public Shader depthDownsampleShader;
        
        [Header("Debug")]
        public bool showDebug = false;
    }

    public Settings settings = new();
    
    private VolumetricFogRenderPass _renderPass;
    private Material _fogMaterial;
    private Material _compositeMaterial;
    private Material _depthDownsampleMaterial;

    public override void Create()
    {
        LoadShaders();
        
        if (_fogMaterial == null || _compositeMaterial == null || _depthDownsampleMaterial == null)
        {
            Debug.LogWarning("VolumetricFog: Failed to create materials. Check shader assignments.");
            return;
        }

        _renderPass = new VolumetricFogRenderPass(
            _fogMaterial, 
            _compositeMaterial, 
            _depthDownsampleMaterial,
            settings.showDebug
        )
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    private void LoadShaders()
    {
        if (settings.volumetricFogShader == null)
            settings.volumetricFogShader = Shader.Find("Hidden/VolumetricFog/Raymarch");
        if (settings.compositeShader == null)
            settings.compositeShader = Shader.Find("Hidden/VolumetricFog/Composite");
        if (settings.depthDownsampleShader == null)
            settings.depthDownsampleShader = Shader.Find("Hidden/VolumetricFog/DepthDownsample");

        if (settings.volumetricFogShader != null && _fogMaterial == null)
            _fogMaterial = CoreUtils.CreateEngineMaterial(settings.volumetricFogShader);
        if (settings.compositeShader != null && _compositeMaterial == null)
            _compositeMaterial = CoreUtils.CreateEngineMaterial(settings.compositeShader);
        if (settings.depthDownsampleShader != null && _depthDownsampleMaterial == null)
            _depthDownsampleMaterial = CoreUtils.CreateEngineMaterial(settings.depthDownsampleShader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_renderPass == null || _fogMaterial == null)
            return;

        // Only render for game and scene view cameras
        var cameraType = renderingData.cameraData.cameraType;
        if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
            return;

        // Check if volume is active
        var stack = VolumeManager.instance.stack;
        var fogVolume = stack.GetComponent<VolumetricFogVolumeComponent>();
        
        if (fogVolume == null || !fogVolume.IsActive())
            return;

        _renderPass.Setup(fogVolume);
        renderer.EnqueuePass(_renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        _renderPass?.Dispose();
        
        if (_fogMaterial != null)
            CoreUtils.Destroy(_fogMaterial);
        if (_compositeMaterial != null)
            CoreUtils.Destroy(_compositeMaterial);
        if (_depthDownsampleMaterial != null)
            CoreUtils.Destroy(_depthDownsampleMaterial);
    }
}