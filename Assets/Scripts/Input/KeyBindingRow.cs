using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authored visual row used by the Keys page. It contains no Input System policy;
/// the settings menu supplies captions and callbacks for the represented bindings.
/// </summary>
public sealed class KeyBindingRow : MonoBehaviour
{
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button keyboardPrimary;
    [SerializeField] private Button keyboardAlternate;
    [SerializeField] private Button gamepadPrimary;
    [SerializeField] private Button gamepadAlternate;
    [SerializeField] private GameObject keyboardSeparator;
    [SerializeField] private GameObject gamepadSeparator;

    public Button KeyboardPrimary => keyboardPrimary;
    public Button KeyboardAlternate => keyboardAlternate;
    public Button GamepadPrimary => gamepadPrimary;
    public Button GamepadAlternate => gamepadAlternate;

    public Button[] ButtonsByColumn => new[]
        { keyboardPrimary, keyboardAlternate, gamepadPrimary, gamepadAlternate };

    public void SetLabel(string value)
    {
        if (actionLabel != null) actionLabel.text = value;
    }

    public void ConfigureButton(Button button, string caption, Action callback, bool visible, bool interactable)
    {
        if (button == null) return;
        button.gameObject.SetActive(visible);
        button.interactable = interactable;
        button.onClick.RemoveAllListeners();
        if (callback != null) button.onClick.AddListener(() => callback());
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = caption;
    }

    public void RefreshDeviceGroups()
    {
        // Separators belong to the authored device columns. A single visible binding
        // expands through its layout group; two bindings remain independent buttons.
        if (keyboardSeparator != null)
            keyboardSeparator.SetActive(IsVisible(keyboardPrimary) && IsVisible(keyboardAlternate));
        if (gamepadSeparator != null)
            gamepadSeparator.SetActive(IsVisible(gamepadPrimary) && IsVisible(gamepadAlternate));
    }

    private static bool IsVisible(Button button) =>
        button != null && button.gameObject.activeSelf;
}
