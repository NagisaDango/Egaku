using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the local visual effect to level cameras without changing scene or Photon setup data.
/// </summary>
public static class PlatformOutlineBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScene()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddEffectToLevelCameras(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AddEffectToLevelCameras(scene);
    }

    private static void AddEffectToLevelCameras(Scene scene)
    {
        if (!scene.name.StartsWith("Level_"))
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                // Only the gameplay camera should add outlines; minimap or UI cameras retain their own view.
                if (camera.CompareTag("MainCamera") && camera.GetComponent<PlatformOutlineCameraEffect>() == null)
                    camera.gameObject.AddComponent<PlatformOutlineCameraEffect>();
            }
        }
    }
}
