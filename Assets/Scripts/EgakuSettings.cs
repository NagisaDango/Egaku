using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Preferences for this installation only. Gameplay and Photon room state never read these values.</summary>
public static class EgakuSettings
{
    public enum OnlineDeviceMode { Auto, KeyboardMouse, GamepadPreferred }
    public enum LocalDeviceLayout { RunnerKeyboard, DrawerKeyboard, TwoGamepads }

    private const string Prefix = "Egaku.Settings.v1.";
    public static event Action Changed;

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static float EffectsVolume { get; private set; } = 1f;
    public static float GamepadPointerSpeed { get; private set; } = 900f;
    public static float DrawerBrushCursorScale { get; private set; } = 1f;
    public static float RunnerIndicatorScale { get; private set; } = 1f;
    public static bool KeyboardRunnerInLocal { get; private set; } = true;
    public static LocalDeviceLayout PreferredLocalLayout { get; private set; } = LocalDeviceLayout.RunnerKeyboard;
    public static OnlineDeviceMode OnlineDevice { get; private set; } = OnlineDeviceMode.Auto;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadAtStartup()
    {
        // Domain reload can be disabled in the Editor. Re-read preferences for every new play session.
        MasterVolume = ReadFloat("Master", 1f, 0f, 1f);
        MusicVolume = ReadFloat("Music", 1f, 0f, 1f);
        EffectsVolume = ReadFloat("Effects", 1f, 0f, 1f);
        GamepadPointerSpeed = ReadFloat("PointerSpeed", 900f, 300f, 1800f);
        DrawerBrushCursorScale = ReadFloat("BrushCursorScale", 1f, 0.5f, 2f);
        RunnerIndicatorScale = ReadFloat("RunnerIndicatorScale", 1f, 0.5f, 2f);
        KeyboardRunnerInLocal = PlayerPrefs.GetInt(Prefix + "KeyboardRunner", 1) != 0;
        // Existing installs had one keyboard role. Keep that preference on first
        // migration, but never persist the identity of a physical controller.
        PreferredLocalLayout = (LocalDeviceLayout)Mathf.Clamp(
            PlayerPrefs.GetInt(Prefix + "LocalLayout", KeyboardRunnerInLocal ? 0 : 1), 0, 2);
        OnlineDevice = (OnlineDeviceMode)Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "OnlineDevice", 0), 0, 2);
        ApplySavedDisplay();
        Changed?.Invoke();
    }

    private static float ReadFloat(string key, float fallback, float minimum, float maximum)
    {
        float value = PlayerPrefs.GetFloat(Prefix + key, fallback);
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
    }

    public static void SetVolumes(float master, float music, float effects)
    {
        PreviewVolumes(master, music, effects);
        PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume);
        PlayerPrefs.Save();
    }

    public static void SetGamepadPointerSpeed(float speed)
    {
        PreviewControls(speed, DrawerBrushCursorScale, RunnerIndicatorScale);
        PlayerPrefs.SetFloat(Prefix + "PointerSpeed", GamepadPointerSpeed);
        PlayerPrefs.Save();
    }

    public static void SetDrawerCursorSizes(float brush, float runnerIndicator)
    {
        PreviewControls(GamepadPointerSpeed, brush, runnerIndicator);
        PlayerPrefs.SetFloat(Prefix + "BrushCursorScale", DrawerBrushCursorScale);
        PlayerPrefs.SetFloat(Prefix + "RunnerIndicatorScale", RunnerIndicatorScale);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Applies audio values for an open settings preview without making them survive a restart.
    /// The settings menu restores its opening snapshot when the user cancels.
    /// </summary>
    public static void PreviewVolumes(float master, float music, float effects)
    {
        MasterVolume = Mathf.Clamp01(master);
        MusicVolume = Mathf.Clamp01(music);
        EffectsVolume = Mathf.Clamp01(effects);
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies pointer values for an open settings preview without writing PlayerPrefs.
    /// Keeping preview and persistence separate lets Apply Settings commit every page together.
    /// </summary>
    public static void PreviewControls(float pointerSpeed, float brush, float runnerIndicator)
    {
        GamepadPointerSpeed = Mathf.Clamp(pointerSpeed, 300f, 1800f);
        DrawerBrushCursorScale = Mathf.Clamp(brush, 0.5f, 2f);
        RunnerIndicatorScale = Mathf.Clamp(runnerIndicator, 0.5f, 2f);
        Changed?.Invoke();
    }

    /// <summary>
    /// Commits the complete settings draft with one PlayerPrefs save. Display values are
    /// stored here too, but Screen.SetResolution remains owned by the menu's safety preview.
    /// </summary>
    public static void ApplyAll(float master, float music, float effects, float pointerSpeed,
        float brush, float runnerIndicator, OnlineDeviceMode onlineDevice,
        int displayWidth, int displayHeight, FullScreenMode displayMode)
    {
        MasterVolume = Mathf.Clamp01(master);
        MusicVolume = Mathf.Clamp01(music);
        EffectsVolume = Mathf.Clamp01(effects);
        GamepadPointerSpeed = Mathf.Clamp(pointerSpeed, 300f, 1800f);
        DrawerBrushCursorScale = Mathf.Clamp(brush, 0.5f, 2f);
        RunnerIndicatorScale = Mathf.Clamp(runnerIndicator, 0.5f, 2f);
        OnlineDevice = onlineDevice;

        PlayerPrefs.SetFloat(Prefix + "Master", MasterVolume);
        PlayerPrefs.SetFloat(Prefix + "Music", MusicVolume);
        PlayerPrefs.SetFloat(Prefix + "Effects", EffectsVolume);
        PlayerPrefs.SetFloat(Prefix + "PointerSpeed", GamepadPointerSpeed);
        PlayerPrefs.SetFloat(Prefix + "BrushCursorScale", DrawerBrushCursorScale);
        PlayerPrefs.SetFloat(Prefix + "RunnerIndicatorScale", RunnerIndicatorScale);
        PlayerPrefs.SetInt(Prefix + "OnlineDevice", (int)OnlineDevice);
        PlayerPrefs.SetInt(Prefix + "DisplayWidth", displayWidth);
        PlayerPrefs.SetInt(Prefix + "DisplayHeight", displayHeight);
        PlayerPrefs.SetInt(Prefix + "DisplayMode", (int)displayMode);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetKeyboardRunnerInLocal(bool keyboardRunner)
    {
        KeyboardRunnerInLocal = keyboardRunner;
        PlayerPrefs.SetInt(Prefix + "KeyboardRunner", keyboardRunner ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SaveLocalLayout(LocalDeviceLayout layout)
    {
        PreferredLocalLayout = layout;
        PlayerPrefs.SetInt(Prefix + "LocalLayout", (int)layout);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SetOnlineDevice(OnlineDeviceMode mode)
    {
        OnlineDevice = mode;
        PlayerPrefs.SetInt(Prefix + "OnlineDevice", (int)mode);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void SaveDisplay(int width, int height, FullScreenMode mode)
    {
        PlayerPrefs.SetInt(Prefix + "DisplayWidth", width);
        PlayerPrefs.SetInt(Prefix + "DisplayHeight", height);
        PlayerPrefs.SetInt(Prefix + "DisplayMode", (int)mode);
        PlayerPrefs.Save();
    }

    private static void ApplySavedDisplay()
    {
        if (!PlayerPrefs.HasKey(Prefix + "DisplayWidth")) return;
        int width = PlayerPrefs.GetInt(Prefix + "DisplayWidth");
        int height = PlayerPrefs.GetInt(Prefix + "DisplayHeight");
        int modeValue = PlayerPrefs.GetInt(Prefix + "DisplayMode", (int)FullScreenMode.Windowed);
        if (modeValue != (int)FullScreenMode.Windowed && modeValue != (int)FullScreenMode.FullScreenWindow &&
            modeValue != (int)FullScreenMode.ExclusiveFullScreen) modeValue = (int)FullScreenMode.Windowed;

        // A previously selected monitor mode may disappear. Let the Player use its safe default then.
        bool available = false;
        foreach (Resolution resolution in Screen.resolutions)
            if (resolution.width == width && resolution.height == height) { available = true; break; }
        if (available && width >= 640 && height >= 480)
            Screen.SetResolution(width, height, (FullScreenMode)modeValue);
    }
}
