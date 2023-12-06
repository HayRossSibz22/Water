using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FlatKit {
public class FlatKitPixelation : ScriptableRendererFeature {
    [Tooltip("To create new settings use 'Create > FlatKit > Pixelation Settings'.")]
    public PixelationSettings settings;

    [SerializeField]
    [HideInInspector]
    // ReSharper disable once InconsistentNaming
    private Material _effectMaterial;

    private DustyroomRenderPass _fullScreenPass;
    private bool _requiresColor;
    private bool _injectedBeforeTransparents;
    private ScriptableRenderPassInput _requirements = ScriptableRenderPassInput.Color;

    private static readonly string ShaderName = "Hidden/FlatKit/PixelationWrap";
    private static readonly int PixelSizeProperty = Shader.PropertyToID("_PixelSize");

    public override void Create() {
        // Settings.
        {
            if (settings == null) return;
            settings.onSettingsChanged = null;
            settings.onReset = null;
            settings.onSettingsChanged += SetMaterialProperties;
            settings.onReset += CreateMaterial;
        }

        // Material.
        if (_effectMaterial == null) {
            CreateMaterial();
        }

        SetMaterialProperties();

        {
            _fullScreenPass = new DustyroomRenderPass {
                renderPassEvent = settings.renderEvent,
            };

            _requirements = ScriptableRenderPassInput.Color;
            ScriptableRenderPassInput modifiedRequirements = _requirements;

            _requiresColor = (_requirements & ScriptableRenderPassInput.Color) != 0;
            _injectedBeforeTransparents = settings.renderEvent <= RenderPassEvent.BeforeRenderingTransparents;

            if (_requiresColor && !_injectedBeforeTransparents) {
                modifiedRequirements ^= ScriptableRenderPassInput.Color;
            }

            _fullScreenPass.ConfigureInput(modifiedRequirements);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        if (settings == null || !settings.applyInSceneView && renderingData.cameraData.isSceneViewCamera) return;
        if (renderingData.cameraData.isPreviewCamera) return;
        if (_effectMaterial == null) return;

        _fullScreenPass.Setup(_effectMaterial, _requiresColor, _injectedBeforeTransparents, "Flat Kit Pixelation",
            renderingData);
        renderer.EnqueuePass(_fullScreenPass);
    }

    protected override void Dispose(bool disposing) {
        _fullScreenPass.Dispose();
    }

    private void CreateMaterial() {
        var effectShader = Shader.Find(ShaderName);

        // This may happen on project load.
        if (effectShader == null) return;

        _effectMaterial = CoreUtils.CreateEngineMaterial(effectShader);
#if UNITY_EDITOR
        AlwaysIncludedShaders.Add(ShaderName);
#endif
    }

    private void SetMaterialProperties() {
        if (_effectMaterial == null) return;

        var pixelSize = Mathf.Max(1f / settings.resolution, 0.0001f);
        _effectMaterial.SetFloat(PixelSizeProperty, pixelSize);
    }
}
}