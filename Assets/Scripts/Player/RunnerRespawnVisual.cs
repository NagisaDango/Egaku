using UnityEngine;

/// <summary>
/// Temporarily reveals the existing Runner sprites in short left-to-right rows.
/// Only renderer materials change; sprite tint, sorting, transforms, and collision stay intact.
/// </summary>
public sealed class RunnerRespawnVisual
{
    private static readonly int RevealRectId = Shader.PropertyToID("_RevealRect");
    private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");

    private readonly GameObject runner;
    private SpriteRenderer[] renderers;
    private Material[] originalMaterials;
    private Material revealMaterial;
    private MaterialPropertyBlock properties;
    private Vector4 revealRect;

    public RunnerRespawnVisual(GameObject runner)
    {
        this.runner = runner;
    }

    public bool Begin()
    {
        End();
        Shader shader = Resources.Load<Shader>("RunnerRespawnReveal");
        if (shader == null || !shader.isSupported)
        {
            // Missing or unsupported visual assets must never leave the Runner input locked.
            Debug.LogWarning("Runner respawn reveal shader is unavailable; respawning without the visual effect.");
            return false;
        }

        // The current Runner prefab uses Sprites/Default for its body and face. Select only
        // active sprites so dormant variants and fog helpers keep their authored materials.
        renderers = runner.GetComponentsInChildren<SpriteRenderer>();
        if (renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        revealRect = new Vector4(bounds.min.x, bounds.min.y,
            Mathf.Max(bounds.size.x, 0.01f), Mathf.Max(bounds.size.y, 0.01f));

        revealMaterial = new Material(shader) { name = "Runner Respawn Reveal (Local)" };
        originalMaterials = new Material[renderers.Length];
        properties = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            originalMaterials[i] = renderers[i].sharedMaterial;
            renderers[i].sharedMaterial = revealMaterial;
        }
        SetProgress(0f);
        return true;
    }

    public void SetProgress(float progress)
    {
        if (renderers == null)
            return;

        // One world-space rectangle spans body and face so separate sprites form a single
        // drawing pass, including the eye and mouth sprites selected by the player's appearance.
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            renderers[i].GetPropertyBlock(properties);
            properties.SetVector(RevealRectId, revealRect);
            properties.SetFloat(RevealProgressId, Mathf.Clamp01(progress));
            renderers[i].SetPropertyBlock(properties);
        }
    }

    public void End()
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                renderers[i].sharedMaterial = originalMaterials[i];
                // The current Runner renderers do not use other property blocks; remove
                // the temporary mask values when their original sprite material returns.
                renderers[i].SetPropertyBlock(null);
            }
        }

        if (revealMaterial != null)
            Object.Destroy(revealMaterial);
        renderers = null;
        originalMaterials = null;
        revealMaterial = null;
        properties = null;
    }
}
