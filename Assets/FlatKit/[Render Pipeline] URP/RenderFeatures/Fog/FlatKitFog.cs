using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FlatKit {
public class FlatKitFog : ScriptableRendererFeature {
    [Tooltip("To create new settings use 'Create > FlatKit > Fog Settings'.")]
    public FogSettings settings;

    [SerializeField]
    [HideInInspector]
    // ReSharper disable once InconsistentNaming
    private Material _effectMaterial;

    private DustyroomRenderPass _fullScreenPass;
    private bool _requiresColor;
    private bool _injectedBeforeTransparents;
    private ScriptableRenderPassInput _requirements = ScriptableRenderPassInput.Color;

    private Texture2D _lutDepth;
    private Texture2D _lutHeight;

    private static readonly string FogShaderName = "Hidden/FlatKit/FogWrap";
    private static readonly int DistanceLut = Shader.PropertyToID("_DistanceLUT");
    private static readonly int Near = Shader.PropertyToID("_Near");
    private static readonly int Far = Shader.PropertyToID("_Far");
    private static readonly int DistanceFogIntensity = Shader.PropertyToID("_DistanceFogIntensity");
    private static readonly int HeightLut = Shader.PropertyToID("_HeightLUT");
    private static readonly int LowWorldY = Shader.PropertyToID("_LowWorldY");
    private static readonly int HighWorldY = Shader.PropertyToID("_HighWorldY");
    private static readonly int HeightFogIntensity = Shader.PropertyToID("_HeightFogIntensity");
    private static readonly int DistanceHeightBlend = Shader.PropertyToID("_DistanceHeightBlend");
    private const string CameraRelativePosition = "FOG_CAMERA_RELATIVE";
    private const string UseDistanceFog = "USE_DISTANCE_FOG";
    private const string UseHeightFog = "USE_HEIGHT_FOG";

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

            _requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color;
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

        _fullScreenPass.Setup(_effectMaterial, _requiresColor, _injectedBeforeTransparents, "Flat Kit Fog",
            renderingData);
        renderer.EnqueuePass(_fullScreenPass);
    }

    protected override void Dispose(bool disposing) {
        _fullScreenPass.Dispose();
    }

    private void CreateMaterial() {
        var effectShader = Shader.Find(FogShaderName);
        
        // This may happen on project load.
        if (effectShader == null) return;

        _effectMaterial = CoreUtils.CreateEngineMaterial(effectShader);
#if UNITY_EDITOR
        AlwaysIncludedShaders.Add(FogShaderName);
#endif
    }

    private void SetMaterialProperties() {
        if (_effectMaterial == null) return;

        UpdateDistanceLut();
        SetKeyword(_effectMaterial, UseDistanceFog, settings.useDistance);
        _effectMaterial.SetTexture(DistanceLut, _lutDepth);
        _effectMaterial.SetFloat(Near, settings.near);
        _effectMaterial.SetFloat(Far, settings.far);
        _effectMaterial.SetFloat(DistanceFogIntensity, settings.distanceFogIntensity);

        UpdateHeightLut();
        SetKeyword(_effectMaterial, UseHeightFog, settings.useHeight);
        _effectMaterial.SetTexture(HeightLut, _lutHeight);
        _effectMaterial.SetFloat(LowWorldY, settings.low);
        _effectMaterial.SetFloat(HighWorldY, settings.high);
        _effectMaterial.SetFloat(HeightFogIntensity, settings.heightFogIntensity);
        _effectMaterial.SetFloat(DistanceHeightBlend, settings.distanceHeightBlend);

        SetKeyword(_effectMaterial, CameraRelativePosition, settings.cameraRelativePosition);
    }

    private void UpdateDistanceLut() {
        if (settings.distanceGradient == null) return;

        if (_lutDepth != null) {
            DestroyImmediate(_lutDepth);
        }

        const int width = 256;
        const int height = 1;
        _lutDepth = new Texture2D(width, height, TextureFormat.RGBA32, /*mipChain=*/false) {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        for (float x = 0; x < width; x++) {
            Color color = settings.distanceGradient.Evaluate(x / (width - 1));
            for (float y = 0; y < height; y++) {
                _lutDepth.SetPixel(Mathf.CeilToInt(x), Mathf.CeilToInt(y), color);
            }
        }

        _lutDepth.Apply();
    }

    private void UpdateHeightLut() {
        if (settings.heightGradient == null) return;

        if (_lutHeight != null) {
            DestroyImmediate(_lutHeight);
        }

        const int width = 256;
        const int height = 1;
        _lutHeight = new Texture2D(width, height, TextureFormat.RGBA32, /*mipChain=*/false) {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };

        for (float x = 0; x < width; x++) {
            Color color = settings.heightGradient.Evaluate(x / (width - 1));
            for (float y = 0; y < height; y++) {
                _lutHeight.SetPixel(Mathf.CeilToInt(x), Mathf.CeilToInt(y), color);
            }
        }

        _lutHeight.Apply();
    }

    private static void SetKeyword(Material material, string keyword, bool enabled) {
        if (enabled) {
            material.EnableKeyword(keyword);
        } else {
            material.DisableKeyword(keyword);
        }
    }
}
}