using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-owned references; the persistent manager rebinds without hierarchy-name searches.</summary>
public sealed class SelectionSceneBindings : MonoBehaviour
{
    public GameObject roomSelection, roleSelection, levelSelection;
    public Transform gridLayout;
    public TMP_InputField nameField;
    public Button createJoinButton, backButton;
}
