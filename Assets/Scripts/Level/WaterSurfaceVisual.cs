using UnityEngine;

// This component affects only the SpriteRenderer. The existing water trigger and buoyancy own gameplay behavior.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(BuoyancyEffector2D))]
public sealed class WaterSurfaceVisual : MonoBehaviour
{
    private static readonly int SurfaceYId = Shader.PropertyToID("_SurfaceY");

    private SpriteRenderer waterRenderer;
    private BuoyancyEffector2D buoyancy;
    private MaterialPropertyBlock propertyBlock;
    private float lastSurfaceY = float.NaN;

    private void OnEnable()
    {
        waterRenderer = GetComponent<SpriteRenderer>();
        buoyancy = GetComponent<BuoyancyEffector2D>();
        propertyBlock = new MaterialPropertyBlock();
        UpdateVisualSurface();
    }

    private void Update()
    {
        // Edit Mode also updates when a scene instance changes scale or overrides its surface level.
        UpdateVisualSurface();
    }

    private void UpdateVisualSurface()
    {
        if (waterRenderer == null || buoyancy == null)
            return;

        // Unity's buoyancy surface is a horizontal world-space line offset by the effector's scaled Y level.
        float surfaceY = transform.position.y + buoyancy.surfaceLevel * transform.lossyScale.y;
        if (Mathf.Approximately(surfaceY, lastSurfaceY))
            return;

        waterRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(SurfaceYId, surfaceY);
        waterRenderer.SetPropertyBlock(propertyBlock);
        lastSurfaceY = surfaceY;
    }
}
