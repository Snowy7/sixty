// OutlineRendererFeature.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader outlineShader;
    }

    public OutlineSettings settings = new();
    private OutlineRenderPass _renderPass;
    private Material _outlineMaterial;

    public override void Create()
    {
        if (settings.outlineShader == null)
            settings.outlineShader = Shader.Find("Hidden/Outline");

        if (settings.outlineShader != null)
        {
            _outlineMaterial = CoreUtils.CreateEngineMaterial(settings.outlineShader);
            _renderPass = new OutlineRenderPass(_outlineMaterial)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_outlineMaterial == null || _renderPass == null) return;

        var stack = VolumeManager.instance.stack;
        var outlineVolume = stack.GetComponent<OutlineVolumeSettings>();

        if (outlineVolume == null || !outlineVolume.IsActive()) return;

        _renderPass.Setup(outlineVolume);
        renderer.EnqueuePass(_renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CoreUtils.Destroy(_outlineMaterial);
        }
    }
}