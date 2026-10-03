using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Egaku.Editor
{
    /// <summary>One-time migration of existing authored references, with no hierarchy changes to gameplay.</summary>
    public static class SceneContextAuthoring
    {
        public static void Populate()
        {
            // Never save unrelated user edits while migrating references across levels.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene changes before populating scene contexts.");
            Scene active = SceneManager.GetActiveScene();
            // OnlineSelection now owns the room-code UI; this migration must not recreate a scene label.
            string[] paths = EditorBuildSettings.scenes.Where(s => s.enabled &&
                (s.path.Contains("/Level_") && !s.path.EndsWith("/Level_0.unity")))
                .Select(s => s.path).ToArray();
            foreach (string path in paths)
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
                if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var setup = roots.SelectMany(r => r.GetComponentsInChildren<LevelSetup>(true)).Single();
                    var context = setup.GetComponent<GameplaySceneContext>();
                    if (context == null) context = setup.gameObject.AddComponent<GameplaySceneContext>();
                    context.setup = setup;
                    context.gameplayCamera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
                    context.inkSlider = roots.SelectMany(r => r.GetComponentsInChildren<Slider>(true)).Single();
                    context.transition = roots.SelectMany(r => r.GetComponentsInChildren<LevelTransition>(true)).Single();
                    var fog = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Fog");
                    context.fog = fog != null ? fog.gameObject : null;
                    EditorUtility.SetDirty(context);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                }
                finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            Debug.Log("Authored gameplay contexts in " + paths.Length + " scenes; Level_0 excluded.");
        }
    }
}
