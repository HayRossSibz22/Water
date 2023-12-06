using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ReSharper disable RedundantDefaultMemberInitializer

namespace FlatKit {
[CreateAssetMenu(fileName = "PixelationSettings", menuName = "FlatKit/Pixelation Settings")]
public class PixelationSettings : ScriptableObject {
    [Tooltip("The number of pixels on the longer side of the screen.\nLower values result in a more pixelated image.")]
    [Min(0)]
    public int resolution = 320;

    [Space]
    [Tooltip("The render stage at which the effect is applied. To exclude transparent objects, like water or " +
             "UI elements, set this to \"Before Transparent\".")]
    public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    [Tooltip("Whether the effect should be applied in the Scene view as well as in the Game view. Please keep in " +
             "mind that Unity always renders the scene view with the default Renderer settings of the URP config.")]
    public bool applyInSceneView = true;

    internal Action onSettingsChanged;
    internal Action onReset;

    private void OnValidate() {
        onSettingsChanged?.Invoke();
    }

    private void Reset() {
        onReset?.Invoke();
    }

    private void OnDestroy() {
        onSettingsChanged = null;
        onReset = null;
    }
}
}