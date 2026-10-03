using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stores the local player's gameplay binding overrides. The authored Input Action
/// asset remains the default source; every runtime action copy receives the same overrides.
/// </summary>
public static class EgakuInputBindings
{
    private const string OverridesKey = "Egaku.InputBindings.v1.Overrides";

    private static string overridesJson = string.Empty;
    private static bool loaded;
    private static InputActionAsset sourceAsset;

    public static event Action BindingsChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        overridesJson = string.Empty;
        loaded = false;
        sourceAsset = null;
        BindingsChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadAtStartup() => EnsureLoaded();

    public static InputActionAsset CreateActionCopy()
    {
        InputActionAsset source = GetSourceAsset();
        if (source == null) return null;

        InputActionAsset copy = UnityEngine.Object.Instantiate(source);
        ApplyTo(copy);
        return copy;
    }

    public static InputActionAsset CreateDraft() => CreateActionCopy();

    public static void ApplyTo(InputActionAsset actions)
    {
        if (actions == null) return;
        EnsureLoaded();

        // PlayerInput may enable only its current map. Preserve each map separately so
        // refreshing overrides cannot silently activate input that was intentionally off.
        bool[] enabledMaps = new bool[actions.actionMaps.Count];
        for (int i = 0; i < actions.actionMaps.Count; i++)
            enabledMaps[i] = actions.actionMaps[i].enabled;

        actions.Disable();
        actions.RemoveAllBindingOverrides();
        if (!string.IsNullOrWhiteSpace(overridesJson))
        {
            try
            {
                actions.LoadBindingOverridesFromJson(overridesJson, true);
            }
            catch (Exception exception)
            {
                // Corrupt or obsolete local preferences must never prevent gameplay input.
                Debug.LogWarning("Ignoring invalid input binding overrides: " + exception.Message);
            }
        }
        for (int i = 0; i < actions.actionMaps.Count; i++)
            if (enabledMaps[i]) actions.actionMaps[i].Enable();
    }

    public static void CommitDraft(InputActionAsset draft)
    {
        if (draft == null) return;
        overridesJson = draft.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(OverridesKey, overridesJson);
        PlayerPrefs.Save();
        BindingsChanged?.Invoke();
    }

    public static void ResetRole(InputActionAsset draft, string mapName)
    {
        InputActionMap map = draft != null ? draft.FindActionMap(mapName, false) : null;
        if (map == null) return;

        bool wasEnabled = map.enabled;
        map.Disable();
        foreach (InputAction action in map.actions)
            for (int i = 0; i < action.bindings.Count; i++)
                action.RemoveBindingOverride(i);
        if (wasEnabled) map.Enable();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        overridesJson = PlayerPrefs.GetString(OverridesKey, string.Empty);
        loaded = true;
    }

    private static InputActionAsset GetSourceAsset()
    {
        if (sourceAsset != null) return sourceAsset;
        GameObject runner = Resources.Load<GameObject>("Runner");
        PlayerInput input = runner != null ? runner.GetComponent<PlayerInput>() : null;
        sourceAsset = input != null ? input.actions : null;
        if (sourceAsset == null)
            Debug.LogError("Input binding settings require the Runner prefab's Input Action Asset.");
        return sourceAsset;
    }
}
