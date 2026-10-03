using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Egaku.Editor
{
    /// <summary>Checks inherited scene references without activating gameplay or saving scenes.</summary>
    public static class SceneSetupValidation
    {
        [MenuItem("Egaku/Validation/Validate Scene Setup")]
        public static void ValidateMenu()
        {
            string[] errors = Validate();
            foreach (string error in errors) Debug.LogError(error);
            if (errors.Length == 0) Debug.Log("Egaku scene setup validation passed.");
        }

        public static string[] Validate()
        {
            var errors = new List<string>();
            LevelCatalog catalog = LevelCatalog.Load();
            if (catalog == null) return new[] { "LevelCatalog asset is missing." };
            string[] paths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var names = paths.Select(System.IO.Path.GetFileNameWithoutExtension).ToArray();
            if (catalog.levels.Select(l => l.id).Distinct().Count() != catalog.levels.Count)
                errors.Add("LevelCatalog has duplicate level IDs.");
            if (catalog.levels.Select(l => l.sceneName).Distinct().Count() != catalog.levels.Count)
                errors.Add("LevelCatalog has duplicate scene names.");
            foreach (var level in catalog.levels)
            {
                if (!names.Contains(level.sceneName)) errors.Add("Disabled/missing scene: " + level.sceneName);
                if (level.id != 0 && level.thumbnail == null) errors.Add("Missing thumbnail: " + level.sceneName);
            }
            foreach (string required in new[] { LevelCatalog.LauncherScene, LevelCatalog.SelectionScene, LevelCatalog.FinishScene })
                if (!names.Contains(required)) errors.Add("Missing flow scene: " + required);

            foreach (string path in paths)
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var scripts = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    if (scripts.Any(s => s == null)) errors.Add(scene.name + ": missing scripts.");
                    if (catalog.FindScene(scene.name) != null)
                    {
                        var setups = scripts.OfType<LevelSetup>().ToArray();
                        if (setups.Length != 1) errors.Add(scene.name + ": expected one LevelSetup.");
                        else if (new SerializedObject(setups[0]).FindProperty("_camera").objectReferenceValue == null)
                            errors.Add(scene.name + ": camera is unassigned.");
                        if (scripts.OfType<LevelTransition>().Count() != 1)
                            errors.Add(scene.name + ": expected one LevelTransition.");
                        // Level_0 is deliberately outside this migration; production selection starts at one.
                        if (scene.name != "Level_0")
                        {
                            var contexts = scripts.OfType<GameplaySceneContext>().ToArray();
                            if (contexts.Length != 1 || !contexts[0].IsConfigured)
                                errors.Add(scene.name + ": missing, duplicate or invalid GameplaySceneContext.");
                        }
                    }
                    if (scene.name == LevelCatalog.SelectionScene)
                    {
                        var bindings = scripts.OfType<SelectionSceneBindings>().ToArray();
                        if (bindings.Length != 1) errors.Add("RoleSelection: expected one SelectionSceneBindings.");
                        else
                        {
                            var b = bindings[0];
                            if (b.roomSelection == null || b.roleSelection == null || b.levelSelection == null ||
                                b.gridLayout == null || b.nameField == null || b.createJoinButton == null || b.backButton == null)
                                errors.Add("RoleSelection: incomplete UI bindings.");
                        }
                    }
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            // These controls now belong to the resource Prefab, not RoleSelection's scene bindings.
            var online = AssetDatabase.LoadAssetAtPath<OnlineRoleSelectionView>("Assets/Resources/UI/OnlineSelection.prefab");
            if (online == null) errors.Add("OnlineSelection Prefab is missing.");
            else
            {
                var serialized = new SerializedObject(online);
                foreach (string field in new[] { "roomCodeLabel", "copyRoomCodeButton", "copyRoomCodeText", "roleColumnsRoot" })
                    if (serialized.FindProperty(field).objectReferenceValue == null)
                        errors.Add("OnlineSelection: missing " + field + ".");
            }
            return errors.ToArray();
        }
    }
}
