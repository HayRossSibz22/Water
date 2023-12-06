using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DustyroomRenderPass : ScriptableRenderPass {
    private Material _passMaterial;
    private bool _requiresColor;
    private bool _isBeforeTransparents;
    private PassData _passData;
    private ProfilingSampler _profilingSampler;
    private RTHandle _copiedColor;
    private static readonly int BlitTextureShaderID = Shader.PropertyToID("_EffectTexture");

    public void Setup(Material mat, bool requiresColor, bool isBeforeTransparents, string featureName,
        in RenderingData renderingData) {
        _passMaterial = mat;
        _requiresColor = requiresColor;
        _isBeforeTransparents = isBeforeTransparents;
        _profilingSampler ??= new ProfilingSampler(featureName);

        var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
        colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
        RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, colorCopyDescriptor, name: "_FullscreenPassColorCopy");

        _passData ??= new PassData();
    }

    public void Dispose() {
        _copiedColor?.Release();
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
        _passData.effectMaterial = _passMaterial;
        _passData.requiresColor = _requiresColor;
        _passData.isBeforeTransparents = _isBeforeTransparents;
        _passData.profilingSampler = _profilingSampler;
        _passData.copiedColor = _copiedColor;

        ExecutePass(_passData, ref renderingData, ref context);
    }

    private static void ExecutePass(PassData passData, ref RenderingData renderingData,
        ref ScriptableRenderContext context) {
        var passMaterial = passData.effectMaterial;
        var requiresColor = passData.requiresColor;
        var isBeforeTransparents = passData.isBeforeTransparents;
        var copiedColor = passData.copiedColor;
        var profilingSampler = passData.profilingSampler;

        if (passMaterial == null) {
            return;
        }

        if (renderingData.cameraData.isPreviewCamera) {
            return;
        }

        CommandBuffer cmd = renderingData.commandBuffer;
        var cameraData = renderingData.cameraData;

        using (new ProfilingScope(cmd, profilingSampler)) {
            if (requiresColor) {
                var source = isBeforeTransparents
                    ? cameraData.renderer.GetCameraColorBackBuffer(cmd)
                    : cameraData.renderer.cameraColorTargetHandle;

                Blitter.BlitCameraTexture(cmd, source, copiedColor);
                passMaterial.SetTexture(BlitTextureShaderID, copiedColor);
            }

            CoreUtils.SetRenderTarget(cmd, cameraData.renderer.GetCameraColorBackBuffer(cmd));
            CoreUtils.DrawFullScreen(cmd, passMaterial);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }

    private class PassData {
        internal Material effectMaterial;
        internal bool requiresColor;
        internal bool isBeforeTransparents;
        public ProfilingSampler profilingSampler;
        public RTHandle copiedColor;
    }
}