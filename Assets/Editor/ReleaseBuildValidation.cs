using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Egaku.Editor
{
    /// <summary>Builds two separately packaged Development Players from the enabled scene list.</summary>
    public static class ReleaseBuildValidation
    {
        public static void BuildClientA()
        {
            BuildWindowsDevelopment("ClientA");
        }

        public static void BuildClientB()
        {
            BuildWindowsDevelopment("ClientB");
        }

        private static void BuildWindowsDevelopment(string clientName)
        {
            // Use the production Build Settings order so a release check cannot silently omit a level.
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for Egaku.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                throw new BuildFailedException(
                    "Start Unity with -buildTarget StandaloneWindows64 before building Egaku.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputPath = Path.Combine(projectRoot, "Builds", "EgakuReleaseValidation",
                clientName, "Egaku.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            // A failed BuildReport must fail batch mode rather than leaving a stale Player as evidence.
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
            {
                throw new BuildFailedException("Egaku " + clientName + " build failed: " +
                    report.summary.result + ", errors=" + report.summary.totalErrors);
            }

            Debug.Log("Egaku " + clientName + " Development Build succeeded at " + outputPath +
                " (warnings=" + report.summary.totalWarnings + ").");
        }
    }
}
