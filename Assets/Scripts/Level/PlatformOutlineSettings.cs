using UnityEngine;

/// <summary>
/// Project-wide visual settings for the inner screen-space outline of map platforms.
/// </summary>
[CreateAssetMenu(fileName = "PlatformOutlineSettings", menuName = "Egaku/Platform Outline Settings")]
public sealed class PlatformOutlineSettings : ScriptableObject
{
    // A serialized shader reference keeps the hidden effect available in player builds, where
    // Shader.Find alone can fail because unused shaders are stripped from the build.
    [SerializeField] private Shader outlineShader;
    public Shader OutlineShader => outlineShader;

    // The shader samples at most 32 neighboring pixels in each direction; expose that same
    // range in the Inspector so the saved setting matches the rendered thickness.
    [Range(0f, 32f)]
    [Tooltip("Inner outline thickness in screen pixels (0-32). Set to zero to hide outlines.")]
    public float widthPixels = 2f;

    [Tooltip("Color painted inside the combined visible silhouette of each platform group.")]
    public Color color = Color.black;
}
