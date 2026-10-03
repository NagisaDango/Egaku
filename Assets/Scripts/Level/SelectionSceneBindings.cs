using Allan;
using Photon.Pun;
using UnityEngine.EventSystems;
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

    private GameManager manager;
    private LocalDeviceClaimView localDeviceClaimView;
    private OnlineRoleSelectionView onlineRoleSelectionView;

    public void Bind(GameManager owner)
    {
        // Rebinding never accumulates listeners or retains a previous persistent manager.
        Unbind();
        manager = owner;
        createJoinButton.onClick.AddListener(manager.CreateJoinButton);
        backButton.onClick.AddListener(manager.Back2RoleSelection);
        ConfigurePrivateRoomCodeUi();
        ConfigureGameplayDeviceUi();
    }

    private void Unbind()
    {
        if (manager == null) return;
        createJoinButton.onClick.RemoveListener(manager.CreateJoinButton);
        backButton.onClick.RemoveListener(manager.Back2RoleSelection);
        manager = null;
    }

    private void OnDestroy() => Unbind();

    public void ShowCurrentRoomCode()
    {
        // Room-code graphics and clipboard interaction are authored together in OnlineSelection.
        if (onlineRoleSelectionView != null) onlineRoleSelectionView.RefreshRoomCode();
    }
        public void ConfigurePrivateRoomCodeUi()
        {
            if (nameField != null)
            {
                nameField.characterLimit = PhotonSessionPolicy.MaximumRoomCodeLength;
                nameField.contentType = TMP_InputField.ContentType.Alphanumeric;
                if (nameField.placeholder is TMP_Text placeholderLabel)
                    placeholderLabel.text = "Room code (blank = create)";
            }

            if (createJoinButton != null)
            {
                TMP_Text buttonLabel = createJoinButton.GetComponentInChildren<TMP_Text>();
                if (buttonLabel != null) buttonLabel.text = "Create / Join Code";
            }

            if (gridLayout != null && gridLayout.parent != null && gridLayout.parent.parent != null)
            {
                // Private rooms are never listed, so hide the legacy public-room scroll view.
                gridLayout.parent.parent.gameObject.SetActive(false);
            }
        }

        public void LoadLevelSelection()
        {
            print("Enter LoadLevelSelection");
            roomSelection.SetActive(false);
            roleSelection.SetActive(false);
            levelSelection.SetActive(true);
            if (localDeviceClaimView != null)
                localDeviceClaimView.gameObject.SetActive(PhotonNetwork.OfflineMode && manager.devSpawn);
            if (EventSystem.current != null)
            {
                // Online play has automatic switching, so focus the Back button
                // rather than an inactive device selector for gamepad navigation.
                GameObject selection = PhotonNetwork.OfflineMode && manager.devSpawn && localDeviceClaimView != null
                    ? localDeviceClaimView.FirstCard
                    : backButton.gameObject;
                EventSystem.current.SetSelectedGameObject(selection);
            }
        }

        public void ConfigureGameplayDeviceUi()
        {
            // The authored Prefab owns the long-lived visual hierarchy and its
            // persistent card UnityEvents. Reuse an authored scene instance when present.
            if (localDeviceClaimView == null && levelSelection != null)
            {
                localDeviceClaimView = levelSelection.GetComponentInChildren<LocalDeviceClaimView>(true);
                if (localDeviceClaimView == null)
                {
                    LocalDeviceClaimView prefab = Resources.Load<LocalDeviceClaimView>("UI/LocalDeviceClaimView");
                    if (prefab == null) Debug.LogError("LocalDeviceClaimView Prefab is missing.");
                    else localDeviceClaimView = Instantiate(prefab, levelSelection.transform, false);
                }

                if (localDeviceClaimView != null) localDeviceClaimView.gameObject.SetActive(false);
            }

            if (onlineRoleSelectionView == null && roleSelection != null)
            {
                onlineRoleSelectionView = roleSelection.GetComponentInChildren<OnlineRoleSelectionView>(true);
                if (onlineRoleSelectionView == null)
                {
                    OnlineRoleSelectionView prefab = Resources.Load<OnlineRoleSelectionView>("UI/OnlineSelection");
                    if (prefab == null) Debug.LogError("OnlineSelection Prefab is missing or has no OnlineRoleSelectionView.");
                    else onlineRoleSelectionView = Instantiate(prefab, roleSelection.transform, false);
                }

                if (onlineRoleSelectionView != null)
                    onlineRoleSelectionView.gameObject.SetActive(!PhotonNetwork.OfflineMode);
            }

            // Legacy RoleSelect manager disabling. The reorganized scene no longer contains
            // these controls; keep this block commented for reference instead of deleting it.
            // if (!PhotonNetwork.OfflineMode && roleSelection != null &&
            //     roleSelection.TryGetComponent(out RolesManager legacyRoles))
            // {
            //     legacyRoles.enabled = false;
            //     legacyRoles.runnerButton.gameObject.SetActive(false);
            //     legacyRoles.drawerButton.gameObject.SetActive(false);
            //     legacyRoles.startGameButton.gameObject.SetActive(false);
            // }
        }

        public void ApplyRoleSelectionPhase()
        {
            if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom || roleSelection == null || levelSelection == null)
                return;

            bool selectRoles = PhotonSessionPolicy.IsRoleSelectionPhase();
            roomSelection.SetActive(false);
            roleSelection.SetActive(selectRoles);
            levelSelection.SetActive(!selectRoles);
            if (onlineRoleSelectionView != null) onlineRoleSelectionView.gameObject.SetActive(selectRoles);
        }
}
