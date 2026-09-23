using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Egaku.Tests.Editor
{
    public sealed class BuildSceneSerializationTests
    {
        private static readonly IReadOnlyDictionary<string, string> EditorOnlyColorPickerScriptGuids =
            new Dictionary<string, string>
            {
                { "ColorPickerBuilder", "c1ea71f7cad4a6642875d0e08613ed3c" },
                { "TransformBinder", "da6c4a7d7ff84c04488f35e92b61585c" },
                { "TransformLocker", "7f844d12d23d9874891fc25045de6ee5" }
            };

        [Test]
        public void EnabledBuildScenesDoNotSerializeEditorOnlyColorPickerComponents()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                string sceneText = File.ReadAllText(scene.path);

                foreach (KeyValuePair<string, string> script in EditorOnlyColorPickerScriptGuids)
                {
                    Assert.That(
                        sceneText,
                        Does.Not.Contain(script.Value),
                        $"Enabled build scene '{scene.path}' serializes editor-only Xenia component " +
                        $"'{script.Key}'. Remove that component from the runtime scene before building.");
                }
            }
        }
    }
}
