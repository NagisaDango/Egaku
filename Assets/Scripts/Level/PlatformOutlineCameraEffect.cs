using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Draws an inner edge for each kind of visible Platform-layer geometry.
/// This is a local camera effect; it does not change colliders, render materials, or Photon state.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class PlatformOutlineCameraEffect : MonoBehaviour
{
    private const string SettingsResourceName = "PlatformOutlineSettings";
    private const int GroundGroup = 0;
    private const int GlassGroup = 1;
    private const int HorizontalErodePass = 0;
    private const int VisibleMaskPass = 1;
    private const int UnionPass = 2;
    private const int CompositePass = 3;

    private readonly List<GameObject> sceneRoots = new List<GameObject>();
    private readonly List<Renderer> foundRenderers = new List<Renderer>();
    private readonly List<Renderer> platformRenderers = new List<Renderer>();
    private readonly List<int> rendererGroups = new List<int>();
    private readonly List<int> visibleGroups = new List<int>();
    private readonly List<bool> originalForceOff = new List<bool>();

    private Camera sourceCamera;
    private Camera maskCamera;
    private GameObject maskCameraObject;
    private Material outlineMaterial;
    private PlatformOutlineSettings settings;
    private int platformLayer = -1;

    private void Awake()
    {
        sourceCamera = GetComponent<Camera>();
        settings = Resources.Load<PlatformOutlineSettings>(SettingsResourceName);
        platformLayer = LayerMask.NameToLayer("Platform");

        // The settings asset owns a direct shader reference so the effect also works in builds.
        Shader shader = settings != null ? settings.OutlineShader : null;
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("Platform outlines require a PlatformOutlineSettings asset in Resources with a supported shader.", this);
            enabled = false;
            return;
        }

        outlineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        maskCameraObject = new GameObject("Platform Outline Mask Camera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        maskCamera = maskCameraObject.AddComponent<Camera>();
        maskCamera.enabled = false;

        if (platformLayer < 0)
        {
            Debug.LogWarning("Platform outlines are unavailable because the Platform layer does not exist.", this);
            enabled = false;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (settings.widthPixels <= 0f || (sourceCamera.cullingMask & (1 << platformLayer)) == 0)
        {
            Graphics.Blit(source, destination);
            return;
        }

        CollectPlatformRenderers();
        if (visibleGroups.Count == 0)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // Each group needs its own silhouette. Glass and brown terrain share the Platform
        // physics layer, so a layer-only camera mask would incorrectly erase their boundary.
        RenderTexture silhouette = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture horizontalEroded = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture visibleSilhouette = null;
        RenderTexture occluderMask = null;
        RenderTexture occluderScratch = null;
        RenderTexture intermediateA = null;
        RenderTexture intermediateB = null;
        try
        {
            if (visibleGroups.Count > 1)
            {
                visibleSilhouette = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                occluderMask = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                intermediateA = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            }
            if (visibleGroups.Count > 2)
            {
                occluderScratch = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                intermediateB = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
            }

            outlineMaterial.SetFloat("_OutlineWidth", Mathf.Clamp(settings.widthPixels, 0f, 32f));
            outlineMaterial.SetColor("_OutlineColor", settings.color);
            RenderTexture currentScene = source;

            for (int groupIndex = 0; groupIndex < visibleGroups.Count; groupIndex++)
            {
                ConfigureMaskCamera(silhouette);
                RenderGroupMask(visibleGroups[groupIndex]);

                RenderTexture currentMask = silhouette;
                if (groupIndex > 0)
                {
                    // A rear group must be cut by all groups drawn in front before erosion.
                    // Otherwise a hidden glass endpoint would be outlined through brown terrain.
                    outlineMaterial.SetTexture("_OccluderMask", occluderMask);
                    Graphics.Blit(silhouette, visibleSilhouette, outlineMaterial, VisibleMaskPass);
                    currentMask = visibleSilhouette;
                }

                // A separable minimum filter shrinks the combined mask. Subtracting that
                // shrink from the original paints only pixels inside this group's edge.
                Graphics.Blit(currentMask, horizontalEroded, outlineMaterial, HorizontalErodePass);
                outlineMaterial.SetTexture("_OriginalMask", currentMask);
                outlineMaterial.SetTexture("_SceneTex", currentScene);

                bool lastGroup = groupIndex == visibleGroups.Count - 1;
                RenderTexture nextScene = lastGroup ? destination :
                    (groupIndex % 2 == 0 ? intermediateA : intermediateB);
                Graphics.Blit(horizontalEroded, nextScene, outlineMaterial, CompositePass);
                currentScene = nextScene;

                if (!lastGroup)
                {
                    if (groupIndex == 0)
                        Graphics.Blit(silhouette, occluderMask);
                    else
                    {
                        // Accumulate raw group coverage for the next group. The union is
                        // equivalent to all nearer platform surfaces hiding rear surfaces.
                        outlineMaterial.SetTexture("_OccluderMask", occluderMask);
                        Graphics.Blit(silhouette, occluderScratch, outlineMaterial, UnionPass);
                        RenderTexture swap = occluderMask;
                        occluderMask = occluderScratch;
                        occluderScratch = swap;
                    }
                }
            }
        }
        finally
        {
            if (intermediateB != null)
                RenderTexture.ReleaseTemporary(intermediateB);
            if (intermediateA != null)
                RenderTexture.ReleaseTemporary(intermediateA);
            if (occluderScratch != null)
                RenderTexture.ReleaseTemporary(occluderScratch);
            if (occluderMask != null)
                RenderTexture.ReleaseTemporary(occluderMask);
            if (visibleSilhouette != null)
                RenderTexture.ReleaseTemporary(visibleSilhouette);
            RenderTexture.ReleaseTemporary(horizontalEroded);
            RenderTexture.ReleaseTemporary(silhouette);
        }
    }

    private void CollectPlatformRenderers()
    {
        platformRenderers.Clear();
        rendererGroups.Clear();
        visibleGroups.Clear();

        // Re-scan loaded scenes so a locally destroyed glass panel disappears from the next
        // mask, and platforms created by the level loader can participate without scene edits.
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            sceneRoots.Clear();
            scene.GetRootGameObjects(sceneRoots);
            foreach (GameObject root in sceneRoots)
            {
                foundRenderers.Clear();
                root.GetComponentsInChildren(true, foundRenderers);
                foreach (Renderer renderer in foundRenderers)
                {
                    if (renderer.gameObject.layer != platformLayer || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy || renderer.forceRenderingOff)
                        continue;

                    // An explicit marker can define further terrain kinds. Existing glass is
                    // recognized by its gameplay component; all other map terrain is ground.
                    PlatformOutlineGroup marker = renderer.GetComponentInParent<PlatformOutlineGroup>();
                    int group = marker != null ? marker.GroupId :
                        (renderer.GetComponentInParent<BreakablePlatform>() != null ? GlassGroup : GroundGroup);
                    platformRenderers.Add(renderer);
                    rendererGroups.Add(group);
                    if (!visibleGroups.Contains(group))
                        visibleGroups.Add(group);
                }
            }
        }

        // Group masks are composited front to back. Match the common transparent-render
        // sorting keys so a glass panel behind a sprite does not paint through that sprite.
        visibleGroups.Sort(CompareGroupsFrontToBack);
    }

    private int CompareGroupsFrontToBack(int leftGroup, int rightGroup)
    {
        Renderer left = platformRenderers[rendererGroups.IndexOf(leftGroup)];
        Renderer right = platformRenderers[rendererGroups.IndexOf(rightGroup)];

        int leftQueue = left.sharedMaterial != null ? left.sharedMaterial.renderQueue : 3000;
        int rightQueue = right.sharedMaterial != null ? right.sharedMaterial.renderQueue : 3000;
        int compare = rightQueue.CompareTo(leftQueue);
        if (compare != 0)
            return compare;

        compare = SortingLayer.GetLayerValueFromID(right.sortingLayerID)
            .CompareTo(SortingLayer.GetLayerValueFromID(left.sortingLayerID));
        if (compare != 0)
            return compare;

        compare = right.sortingOrder.CompareTo(left.sortingOrder);
        if (compare != 0)
            return compare;

        float leftDepth = Vector3.Dot(left.bounds.center - sourceCamera.transform.position, sourceCamera.transform.forward);
        float rightDepth = Vector3.Dot(right.bounds.center - sourceCamera.transform.position, sourceCamera.transform.forward);
        compare = leftDepth.CompareTo(rightDepth);
        return compare != 0 ? compare : leftGroup.CompareTo(rightGroup);
    }

    private void RenderGroupMask(int group)
    {
        originalForceOff.Clear();
        for (int i = 0; i < platformRenderers.Count; i++)
        {
            Renderer renderer = platformRenderers[i];
            originalForceOff.Add(renderer.forceRenderingOff);
            if (rendererGroups[i] != group)
                renderer.forceRenderingOff = true;
        }

        try
        {
            // ForceRenderingOff only changes this synchronous auxiliary render. Restoring
            // every value in finally leaves the main camera and gameplay renderers untouched.
            maskCamera.Render();
        }
        finally
        {
            for (int i = 0; i < platformRenderers.Count; i++)
            {
                if (platformRenderers[i] != null)
                    platformRenderers[i].forceRenderingOff = originalForceOff[i];
            }
        }
    }

    private void ConfigureMaskCamera(RenderTexture target)
    {
        maskCamera.CopyFrom(sourceCamera);
        maskCamera.enabled = false;
        maskCamera.cullingMask = 1 << platformLayer;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        maskCamera.targetTexture = target;
        maskCamera.rect = new Rect(0f, 0f, 1f, 1f);
        maskCamera.depth = sourceCamera.depth - 1f;
        maskCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
        maskCamera.transform.localScale = Vector3.one;
        maskCamera.allowHDR = false;
        maskCamera.allowMSAA = false;
        maskCamera.useOcclusionCulling = false;
        maskCamera.ResetAspect();
    }

    private void OnDestroy()
    {
        if (maskCameraObject != null)
            Destroy(maskCameraObject);
        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }
}
