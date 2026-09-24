using System;
using UnityEngine;

/// <summary>
/// Applies an opt-in frame cap for standalone profiling clients without changing normal gameplay settings.
/// Pass --egaku-fps=60 or --egaku-fps=120 when launching the Player to enable the override.
/// </summary>
public static class ClientFrameRateOverride
{
    private const string CommandLinePrefix = "--egaku-fps=";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyCommandLineFrameRate()
    {
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (!argument.StartsWith(CommandLinePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string value = argument.Substring(CommandLinePrefix.Length);
            if (!int.TryParse(value, out int frameRate) || (frameRate != 60 && frameRate != 120))
            {
                Debug.LogWarning("Ignoring --egaku-fps: only 60 or 120 are supported.");
                return;
            }

            // Disable VSync so it cannot override the explicit profiling cap on Windows Players.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = frameRate;
            Debug.Log($"Profiling frame-rate cap enabled: {frameRate} FPS.");
            return;
        }
    }
}
