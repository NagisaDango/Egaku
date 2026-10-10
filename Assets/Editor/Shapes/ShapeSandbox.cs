using System.Collections.Generic;
using Egaku.Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Egaku.EditorShapes
{
    public static class ShapeSandbox
    {
        public const string ScenePath = "Assets/Scenes/ShapeTools/ShapeSandbox.unity";
        [MenuItem("Egaku/Shapes/Open Shape Sandbox (Additive)")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded) { SceneManager.SetActiveScene(loaded); return; }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { SceneManager.SetActiveScene(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)); return; }
            // Unity cannot create another normal scene beside an untitled unsaved
            // scene. Do not save or close the user's scene to bypass that restriction.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                {
                    Debug.LogWarning("請先替未命名場景選擇儲存路徑，再建立 Sandbox；目前場景未被變更。");
                    return;
                }
            // Additive authoring keeps any current unsaved level open. Only this
            // newly-created sandbox is saved; the production scene is never saved.
            ShapeAuthoring.EnsureFolder("Assets/Scenes/ShapeTools");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var cameraObject = new GameObject("Sandbox Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.position = new Vector3(0, -2, -10);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 12;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.94f, 0.95f, 0.96f);
            var lightObject = new GameObject("Directional Light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.AddComponent<Light>().type = LightType.Directional;
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 5; column++)
                {
                    var use = row == 0 ? StaticShapeUse.Platform : StaticShapeUse.DrawProhibited;
                    var preset = (ShapePreset)column;
                    var go = ShapeAuthoring.Create(use, preset, new Vector3((column - 2) * 6, 4 - row * 6, 0), scene);
                    go.name = use + " - " + preset;
                }
            for (int row = 0; row < 2; row++)
            {
                var use = row == 0 ? StaticShapeUse.Platform : StaticShapeUse.DrawProhibited;
                var concave = ShapeAuthoring.Create(use, ShapePreset.Rectangle, new Vector3(-6 + row * 12, -8, 0), scene).GetComponent<ShapePath>();
                concave.name = use + " - Concave";
                concave.points = new List<ShapePoint>
                {
                    new ShapePoint(new Vector2(-2,-1)), new ShapePoint(new Vector2(2,-1)),
                    new ShapePoint(new Vector2(2,1)), new ShapePoint(Vector2.zero), new ShapePoint(new Vector2(-2,1))
                };
                ShapeAuthoring.Rebuild(concave);
                var mixed = ShapeAuthoring.Create(use, ShapePreset.Rectangle, new Vector3(-6 + row * 12, -12, 0), scene).GetComponent<ShapePath>();
                mixed.name = use + " - Mixed Curve";
                mixed.points[2].incoming = new Vector2(-1, -0.5f);
                mixed.points[2].outgoing = new Vector2(-0.5f, 0.5f);
                mixed.points[3].incoming = new Vector2(0.5f, 0.5f);
                ShapeAuthoring.Rebuild(mixed);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = null;
        }
    }
}
