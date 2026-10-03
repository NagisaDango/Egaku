using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authored, scene-owned dependencies. Never search another loaded scene for gameplay UI.</summary>
public sealed class GameplaySceneContext : MonoBehaviour
{
    public LevelSetup setup;
    public Camera gameplayCamera;
    public Slider inkSlider;
    public GameObject fog;
    public LevelTransition transition;

    public static GameplaySceneContext FindInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var context = root.GetComponentInChildren<GameplaySceneContext>(true);
            if (context != null) return context;
        }
        return null;
    }

    public bool IsConfigured => setup != null && gameplayCamera != null && inkSlider != null && transition != null &&
        setup.gameObject.scene == gameObject.scene && gameplayCamera.gameObject.scene == gameObject.scene &&
        inkSlider.gameObject.scene == gameObject.scene && transition.gameObject.scene == gameObject.scene;
}
