using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// ReSharper disable RedundantDefaultMemberInitializer

namespace FlatKit {
[CreateAssetMenu(fileName = "FogSettings", menuName = "FlatKit/Fog Settings")]
public class FogSettings : ScriptableObject {
    [Header("Distance Fog")]
    [Tooltip("Whether to use distance fog. This is the fog that fades out the scene into the background.")]
    public bool useDistance = true;
    [Tooltip("The color changes from near (left) to far (right).")]
    public Gradient distanceGradient;
    [Tooltip("The distance from the camera in world units at which the fog starts.")]
    public float near = 0;
    [Tooltip("The distance from the camera in world units at which the fog ends.")]
    public float far = 100;
    [Range(0, 1)]
    [Tooltip("How much the fog should be applied. 0 means no fog, 1 means full fog.")]
    public float distanceFogIntensity = 1.0f;

    [Header("Height Fog")]
    [Tooltip("Whether to use height fog. This is the fog that goes up from the ground.")]
    public bool useHeight = false;
    [Tooltip("The color changes from low (left) to high (right).")]
    public Gradient heightGradient;
    [Tooltip("The height in world units at which the fog starts.")]
    public float low = 0;
    [Tooltip("The height in world units at which the fog ends.")]
    public float high = 10;
    [Range(0, 1)]
    [Tooltip("How much the fog should be applied. 0 means no fog, 1 means full fog.")]
    public float heightFogIntensity = 1.0f;

    [Tooltip("Reverts fog behavior to pre-3.9.0. This is useful if you want to use the new fog settings, but want to " +
             "keep the old look of your scene.")]
    public bool cameraRelativePosition = false;

    [Header("Blending")]
    [Range(0, 1)]
    [Tooltip("The ratio between distance and height fog. 0 means only distance fog, 1 means only height fog.")]
    public float distanceHeightBlend = 0.5f;

    [Header("Advanced settings")]
    [Tooltip("The render stage at which the effect is applied. To exclude transparent objects, like water or " +
             "UI elements, set this to \"Before Transparent\".")]
    public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;

    [Tooltip("Whether the effect should be applied in the Scene view as well as in the Game view. Please keep in " +
             "mind that Unity always renders the scene view with the default Renderer settings of the URP config.")]
    public bool applyInSceneView = true;

    internal Action onSettingsChanged;
    internal Action onReset;

    private void OnValidate() {
        low = Mathf.Min(low, high);
        high = Mathf.Max(low, high);

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