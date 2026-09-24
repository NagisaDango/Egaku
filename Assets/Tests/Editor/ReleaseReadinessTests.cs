using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Egaku.Tests.Editor
{
    /// <summary>Checks the assets that a packaged two-player session must be able to load.</summary>
    public sealed class ReleaseReadinessTests
    {
        private static readonly string[] RequiredFlowScenes =
        {
            "Assets/Scenes/AllanLauncher.unity",
            "Assets/Scenes/RoleSelection.unity",
            "Assets/Scenes/FinishGame.unity"
        };

        private static readonly string[] NetworkPrefabNames =
        {
            "Runner", "Drawer", "DrawnMesh", "PlayerDisplay", "Spline", "Bullet"
        };

        private static IEnumerable<string> GetEnabledScenePaths()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);
        }

        [Test]
        public void EnabledSceneListContainsTheProductionFlow()
        {
            string[] paths = GetEnabledScenePaths().ToArray();

            Assert.That(paths, Is.Not.Empty, "The Player must contain enabled scenes.");
            Assert.That(paths.Distinct().Count(), Is.EqualTo(paths.Length),
                "An enabled scene must occur only once in Build Settings.");

            foreach (string path in paths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null,
                    "Enabled scene is missing: " + path);
            }

            foreach (string path in RequiredFlowScenes)
            {
                Assert.That(paths, Does.Contain(path), "Production flow scene is disabled: " + path);
            }

            Assert.That(paths.Any(path => path.StartsWith("Assets/Scenes/Level_", StringComparison.Ordinal)),
                Is.True, "At least one production level must be enabled.");
        }

        [TestCaseSource(nameof(GetEnabledScenePaths))]
        public void EnabledSceneHasNoMissingScriptsOrObjectReferences(string scenePath)
        {
            // Additive loading preserves any scene already open in the developer's Editor.
            Scene loaded = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !loaded.IsValid() || !loaded.isLoaded;
            Scene scene = openedForTest
                ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                : loaded;

            try
            {
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True, scenePath);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    ValidateHierarchy(root.transform, scenePath);
                }
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [TestCaseSource(nameof(NetworkPrefabNames))]
        public void NetworkPrefabExistsAndItsPhotonViewsHaveValidReferences(string prefabName)
        {
            string path = "Assets/Resources/" + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, "Network prefab is missing: " + path);
            Assert.That(Resources.Load<GameObject>(prefabName), Is.Not.Null,
                "PhotonNetwork.Instantiate cannot resolve " + prefabName + " through Resources.");
            Assert.That(prefab.name, Is.EqualTo(prefabName),
                "PhotonNetwork.Instantiate receives the prefab root name, so it must match its Resources path.");

            Type photonViewType = Type.GetType("Photon.Pun.PhotonView, PhotonUnityNetworking", true);
            Assert.That(prefab.GetComponent(photonViewType), Is.Not.Null,
                "Network prefab root needs a PhotonView: " + path);
            ValidateHierarchy(prefab.transform, path);
        }

        [Test]
        public void RoleAndSpawnPrefabsResolveToPackagedNetworkAssets()
        {
            GameObject managerPrefab = Resources.Load<GameObject>("AllanGameManager");
            GameObject drawerPrefab = Resources.Load<GameObject>("Drawer");
            GameObject drawnMeshPrefab = Resources.Load<GameObject>("DrawnMesh");

            Assert.That(managerPrefab, Is.Not.Null);
            Assert.That(drawerPrefab, Is.Not.Null);
            Assert.That(drawnMeshPrefab, Is.Not.Null);

            Component gameManager = RequiredComponent(managerPrefab, "GameManager");
            AssertPrefabReference(gameManager, "runnerPrefab", "Runner");
            AssertPrefabReference(gameManager, "drawerPrefab", "Drawer");

            Component drawer = RequiredComponent(drawerPrefab, "Drawer");
            AssertPrefabReference(drawer, "drawMeshPrefab", "DrawnMesh");

            Component drawnMesh = RequiredComponent(drawnMeshPrefab, "DrawMesh");
            AssertPrefabReference(drawnMesh, "splinePrefab", "Spline");
        }

        [Test]
        public void RoleSelectionHasBothButtonsAndTheNetworkDisplay()
        {
            const string path = "Assets/Scenes/RoleSelection.unity";
            Scene loaded = SceneManager.GetSceneByPath(path);
            bool openedForTest = !loaded.IsValid() || !loaded.isLoaded;
            Scene scene = openedForTest
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : loaded;

            try
            {
                Component[] managers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Where(component => component != null && component.GetType().Name == "RolesManager")
                    .ToArray();

                Assert.That(managers, Has.Length.EqualTo(1),
                    "Role Selection needs exactly one RolesManager.");

                foreach (string field in new[] { "drawerButton", "runnerButton", "playerDisplayParent" })
                {
                    Assert.That(new SerializedObject(managers[0]).FindProperty(field).objectReferenceValue,
                        Is.Not.Null, "RolesManager." + field + " is missing.");
                }

                AssertPrefabReference(managers[0], "playerDisplay", "PlayerDisplay");
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Component RequiredComponent(GameObject prefab, string typeName)
        {
            Component component = prefab.GetComponents<Component>()
                .FirstOrDefault(candidate => candidate != null && candidate.GetType().Name == typeName);
            Assert.That(component, Is.Not.Null, prefab.name + " is missing " + typeName + ".");
            return component;
        }

        private static void AssertPrefabReference(Component owner, string field, string expectedPrefabName)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.That(property, Is.Not.Null, owner.name + "." + field + " is not serialized.");
            Assert.That(property.objectReferenceValue, Is.Not.Null,
                owner.name + "." + field + " is not assigned.");

            string expectedPath = "Assets/Resources/" + expectedPrefabName + ".prefab";
            Assert.That(AssetDatabase.GetAssetPath(property.objectReferenceValue), Is.EqualTo(expectedPath),
                owner.name + "." + field + " must reference the packaged network prefab.");
        }

        private static void ValidateHierarchy(Transform root, string assetPath)
        {
            GameObject gameObject = root.gameObject;
            string context = assetPath + " / " + GetHierarchyPath(root);

            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject), Is.Zero,
                "Missing Script on " + context);

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                Assert.That(component, Is.Not.Null, "Missing component on " + context);
                ValidateSerializedReferences(component, context);
            }

            foreach (Transform child in root)
            {
                ValidateHierarchy(child, assetPath);
            }
        }

        private static void ValidateSerializedReferences(Component component, string context)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null &&
                    property.objectReferenceEntityIdValue != EntityId.None)
                {
                    Assert.Fail("Broken reference at " + context + " / " +
                        component.GetType().Name + "." + property.propertyPath);
                }
            }

            if (component.GetType().FullName != "Photon.Pun.PhotonView")
            {
                return;
            }

            // PUN serializes every observed component by index; a null entry breaks stream alignment.
            SerializedProperty observed = serialized.FindProperty("ObservedComponents");
            Assert.That(observed, Is.Not.Null, "PhotonView is missing ObservedComponents at " + context);

            for (int index = 0; index < observed.arraySize; index++)
            {
                Assert.That(observed.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null,
                    "PhotonView has a missing observed component at " + context + " [" + index + "].");
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            return transform.parent == null
                ? transform.name
                : GetHierarchyPath(transform.parent) + "/" + transform.name;
        }
    }
}
