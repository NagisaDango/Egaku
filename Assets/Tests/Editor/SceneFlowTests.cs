using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Egaku.Tests.Editor
{
    public sealed class SceneFlowTests
    {
        private Type CatalogType => Type.GetType("LevelCatalog, Assembly-CSharp", true);
        private object Catalog => Resources.Load("LevelCatalog", CatalogType);
        private object Call(string method, params object[] args) => CatalogType.GetMethod(method).Invoke(Catalog, args);

        [Test]
        public void FrontierUnlocksTheImmediateNextLevel()
        {
            // Regression: the old manager assigned unlocked-count as the next ID and rejected its own load.
            Assert.That(Call("UnlockedAfter", 2, 3), Is.EqualTo(4));
            object next = Call("Next", 2);
            Assert.That(next.GetType().GetField("id").GetValue(next), Is.EqualTo(3));
            Assert.That(Call("IsUnlocked", 3, 4), Is.True);
            Assert.That(Call("IsUnlocked", 4, 4), Is.False);
        }

        [Test]
        public void UnlockingLastLevelDoesNotFinishBeforeItIsPlayed()
        {
            Assert.That(Call("UnlockedAfter", 19, 20), Is.EqualTo(21));
            Assert.That(Call("Next", 19), Is.Not.Null);
            Assert.That(Call("Next", 20), Is.Null);
            Assert.That(Call("UnlockedAfter", 20, 21), Is.EqualTo(21));
        }

        [Test]
        public void TestUnlocksDoNotShrinkAndUnknownLevelsAreRejected()
        {
            Assert.That(Call("UnlockedAfter", 1, 21), Is.EqualTo(21));
            Assert.That(Call("IsUnlocked", -1, 21), Is.False);
            Assert.That(Call("IsUnlocked", 21, 21), Is.False);
        }

        [Test]
        public void ProductionScenesAndCatalogHaveValidBindings()
        {
            // The Editor test assembly cannot reference Assembly-CSharp directly.
            Type validator = Type.GetType("Egaku.Editor.SceneSetupValidation, Assembly-CSharp-Editor", true);
            var errors = (string[])validator.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void GameplayContextRejectsUiFromAnotherLevel()
        {
            // Two loaded scenes must never share an ink UI, even if both have identically named objects.
            var first = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level_1.unity");
            var second = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level_2.unity");
            try
            {
                Type contextType = Type.GetType("GameplaySceneContext, Assembly-CSharp", true);
                var find = contextType.GetMethod("FindInScene");
                object a = find.Invoke(null, new object[] { first });
                object b = find.Invoke(null, new object[] { second });
                Assert.That(contextType.GetProperty("IsConfigured").GetValue(a), Is.True);
                var slider = contextType.GetField("inkSlider");
                slider.SetValue(a, slider.GetValue(b));
                Assert.That(contextType.GetProperty("IsConfigured").GetValue(a), Is.False);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(first);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(second);
            }
        }

        [Test]
        public void MissingMaskDoesNotThrowDuringClosing()
        {
            var root = new GameObject("Missing transition visual test");
            root.SetActive(false);
            try
            {
                Type transition = Type.GetType("LevelTransition, Assembly-CSharp", true);
                var component = root.AddComponent(transition);
                var visual = (IEnumerator)transition.GetMethod("ShowTransitionEndScene", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(component, null);
                Assert.That(visual.MoveNext(), Is.False);
                Assert.DoesNotThrow(() => transition.GetMethod("FinishClosingVisual").Invoke(component, null));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(0.5f, 0.5f, 1.777778f)]
        [TestCase(0.05f, 0.9f, 2.333333f)]
        [TestCase(-0.2f, 1.1f, 1.777778f)]
        public void TransitionOpeningRevealsEveryCorner(float x, float y, float aspect)
        {
            Type transition = Type.GetType("LevelTransition, Assembly-CSharp", true);
            var center = new Vector2(x, y);
            float radius = (float)transition.GetMethod("GetOpenRadius", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { center, aspect });
            // Use the shader's distance independently: a fixed 1.5 radius fails near an edge.
            foreach (var corner in new[] { Vector2.zero, Vector2.one, Vector2.up, Vector2.right })
            {
                var offset = corner - center;
                offset.x *= aspect;
                Assert.That(radius, Is.GreaterThan(offset.magnitude));
            }
        }

        [TestCase(0f, 2f)]
        [TestCase(2f, 0f)]
        public void TransitionAlwaysWritesItsExactEndpoint(float from, float to)
        {
            Type transition = Type.GetType("LevelTransition, Assembly-CSharp", true);
            var root = new GameObject("Transition endpoint test");
            root.SetActive(false);
            var material = new Material(Shader.Find("UI/CircleMask"));
            try
            {
                // Keep Awake/Photon callbacks dormant; exercise the real animation with its own material.
                var component = root.AddComponent(transition);
                transition.GetField("matTransition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(component, material);
                material.SetFloat("_Radius", 0.123f);
                var animation = (IEnumerator)transition.GetMethod("AnimateRadius", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(component, new object[] { from, to, 0f });
                Assert.That(animation.MoveNext(), Is.False);
                Assert.That(material.GetFloat("_Radius"), Is.EqualTo(to));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
