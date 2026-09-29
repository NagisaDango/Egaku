using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lets two local players choose distinct physical devices, roles, and readiness.
/// Device selection belongs to the player; InputDeviceRouter receives the final
/// role-to-device pairing only after both players confirm.
/// </summary>
public sealed class LocalDeviceClaimView : MonoBehaviour
{
    private sealed class PlayerSlot
    {
        public int index;
        public InputDevice device;
        public InputDeviceRouter.Role? role;
        public bool ready;
        public int stickDirection;
        public RectTransform card;
        public TMP_Text label;
        public TMP_Dropdown dropdown;
    }

    private readonly struct DeviceChoice
    {
        public readonly InputDevice device;
        public readonly string label;
        public DeviceChoice(InputDevice device, string label)
        {
            this.device = device;
            this.label = label;
        }
    }

    [Header("Columns")]
    [SerializeField] private RectTransform runnerColumn;
    [SerializeField] private RectTransform neutralColumn;
    [SerializeField] private RectTransform drawerColumn;
    [SerializeField] private Image runnerPanel;
    [SerializeField] private Image drawerPanel;
    [SerializeField] private TMP_Text runnerReadyText;
    [SerializeField] private TMP_Text drawerReadyText;

    [Header("Players")]
    [SerializeField] private RectTransform playerOneCard;
    [SerializeField] private TMP_Text playerOneLabel;
    [SerializeField] private TMP_Dropdown playerOneDeviceDropdown;
    [SerializeField] private RectTransform playerTwoCard;
    [SerializeField] private TMP_Text playerTwoLabel;
    [SerializeField] private TMP_Dropdown playerTwoDeviceDropdown;
    [SerializeField] private Button backButton;

    [Header("Movement")]
    [SerializeField, Min(1f)] private float cardMoveSpeed = 14f;
    [SerializeField] private float cardTopOffset = 55f;
    [SerializeField, Min(1f)] private float cardRowSpacing = 72f;

    private readonly List<DeviceChoice> deviceChoices = new();
    private PlayerSlot playerOne;
    private PlayerSlot playerTwo;
    private bool refreshingDropdowns;
    private bool completing;

    public GameObject FirstCard => playerOneDeviceDropdown != null
        ? playerOneDeviceDropdown.gameObject
        : backButton != null ? backButton.gameObject : gameObject;

    private void Awake() => EnsurePlayerSlots();

    private void EnsurePlayerSlots()
    {
        if (playerOne != null && playerTwo != null) return;
        playerOne = new PlayerSlot
        {
            index = 0,
            card = playerOneCard,
            label = playerOneLabel,
            dropdown = playerOneDeviceDropdown
        };
        playerTwo = new PlayerSlot
        {
            index = 1,
            card = playerTwoCard,
            label = playerTwoLabel,
            dropdown = playerTwoDeviceDropdown
        };
    }

    private void OnEnable()
    {
        // OnEnable can run after a Play Mode script reload without a matching
        // Awake call, so rebuild the non-serialized player state when necessary.
        EnsurePlayerSlots();
        InputSystem.onDeviceChange += OnDeviceChange;
        InputDeviceRouter.Initialize();
        completing = false;
        ResetRoleAssignments();

        // Player 1 starts on the complete keyboard/mouse pair. Player 2 receives
        // the first detected gamepad, or remains None until one is selected.
        playerOne.device = Keyboard.current != null && Mouse.current != null ? Keyboard.current : null;
        playerTwo.device = FirstPhysicalGamepad();
        playerOne.role = null;
        playerTwo.role = null;
        playerOne.ready = false;
        playerTwo.ready = false;
        RefreshDeviceChoices();
        RefreshVisuals();
    }

    private void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;

    public void BackToRoles()
    {
        if (Allan.GameManager.Instance != null) Allan.GameManager.Instance.Back2RoleSelection();
    }

    // These are persistent Prefab UnityEvent targets for the authored dropdowns.
    public void OnPlayerOneDeviceChanged(int choice) => SelectDevice(playerOne, playerTwo, choice);
    public void OnPlayerTwoDeviceChanged(int choice) => SelectDevice(playerTwo, playerOne, choice);

    private void Update()
    {
        if (!gameObject.activeInHierarchy || completing || !Photon.Pun.PhotonNetwork.OfflineMode) return;

        bool dropdownOpen = (playerOne.dropdown != null && playerOne.dropdown.IsExpanded) ||
                            (playerTwo.dropdown != null && playerTwo.dropdown.IsExpanded);
        if (!dropdownOpen)
        {
            HandlePlayerInput(playerOne, playerTwo);
            HandlePlayerInput(playerTwo, playerOne);
            TryCompleteSelection();
        }

        MoveCard(playerOne);
        MoveCard(playerTwo);
    }

    private void HandlePlayerInput(PlayerSlot player, PlayerSlot other)
    {
        if (player.device == null || !player.device.added) return;

        int direction = ReadDirection(player);
        if (direction != 0)
        {
            if (!player.ready) MovePlayer(player, other, direction);
            return;
        }

        if (ReadCancel(player))
        {
            if (player.ready)
            {
                player.ready = false;
                RefreshReadyVisuals();
            }
            else if (player.dropdown != null && EventSystem.current != null)
            {
                // An unready gamepad player can use Cancel to return focus to
                // their own device dropdown without affecting the other player.
                EventSystem.current.SetSelectedGameObject(player.dropdown.gameObject);
            }
            return;
        }

        if (!player.ready && player.role.HasValue && ReadConfirm(player) && !DropdownHasFocus(player.dropdown))
        {
            player.ready = true;
            RefreshReadyVisuals();
        }
    }

    private int ReadDirection(PlayerSlot player)
    {
        if (player.device is Keyboard keyboard)
        {
            if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) return -1;
            if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) return 1;
            return 0;
        }

        if (player.device is not Gamepad pad) return 0;
        if (pad.dpad.left.wasPressedThisFrame) return -1;
        if (pad.dpad.right.wasPressedThisFrame) return 1;

        float x = pad.leftStick.x.ReadValue();
        int current = x <= -0.65f ? -1 : x >= 0.65f ? 1 : 0;
        // One stick tilt moves one column. Returning near center arms the next step.
        if (Mathf.Abs(x) < 0.35f) current = 0;
        int previous = player.stickDirection;
        player.stickDirection = current;
        return current != 0 && previous == 0 ? current : 0;
    }

    private static bool ReadConfirm(PlayerSlot player)
    {
        if (player.device is Keyboard keyboard)
            return keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
        return player.device is Gamepad pad && pad.buttonSouth.wasPressedThisFrame;
    }

    private static bool ReadCancel(PlayerSlot player)
    {
        if (player.device is Keyboard keyboard) return keyboard.escapeKey.wasPressedThisFrame;
        return player.device is Gamepad pad && pad.buttonEast.wasPressedThisFrame;
    }

    private static bool DropdownHasFocus(TMP_Dropdown dropdown)
    {
        if (dropdown == null || EventSystem.current == null) return false;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        return selected != null && (selected == dropdown.gameObject || selected.transform.IsChildOf(dropdown.transform));
    }

    private void MovePlayer(PlayerSlot player, PlayerSlot other, int direction)
    {
        int current = player.role == InputDeviceRouter.Role.Runner ? -1
            : player.role == InputDeviceRouter.Role.Drawer ? 1 : 0;
        int target = Mathf.Clamp(current + direction, -1, 1);
        if (target == current) return;

        InputDeviceRouter.Role? targetRole = target < 0 ? InputDeviceRouter.Role.Runner
            : target > 0 ? InputDeviceRouter.Role.Drawer : null;
        if (targetRole.HasValue && other.role == targetRole) return;

        player.role = targetRole;
        // Clear standard UI focus so the same Enter/A press confirms this player
        // instead of also submitting the last selected dropdown or back button.
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void SelectDevice(PlayerSlot player, PlayerSlot other, int choice)
    {
        if (refreshingDropdowns || choice < 0 || choice >= deviceChoices.Count) return;
        if (player.ready)
        {
            // Ready is a lock. The player must cancel with Esc/B before changing devices.
            RefreshDropdownValues();
            return;
        }
        InputDevice requested = deviceChoices[choice].device;
        InputDevice previous = player.device;
        if (requested == previous) return;

        player.ready = false;
        player.stickDirection = 0;
        if (requested != null && requested == other.device)
        {
            // Device collisions resolve as an atomic player-device swap. A None
            // source therefore moves the other player to None.
            other.device = previous;
            other.ready = false;
            other.stickDirection = 0;
        }
        player.device = requested;
        if (player.device == null) player.role = null;
        if (other.device == null) other.role = null;
        RefreshDropdownValues();
        RefreshVisuals();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is not Gamepad && device is not Keyboard && device is not Mouse) return;
        if (change is InputDeviceChange.Removed or InputDeviceChange.Disconnected)
        {
            HandleDisconnectedPlayer(playerOne, playerTwo, device);
            HandleDisconnectedPlayer(playerTwo, playerOne, device);
        }
        if (change is InputDeviceChange.Added or InputDeviceChange.Removed or InputDeviceChange.Disconnected
            or InputDeviceChange.Reconnected or InputDeviceChange.ConfigurationChanged
            or InputDeviceChange.Enabled or InputDeviceChange.Disabled)
        {
            RefreshDeviceChoices();
            RefreshVisuals();
        }
    }

    private static void HandleDisconnectedPlayer(PlayerSlot player, PlayerSlot other, InputDevice lostDevice)
    {
        bool lostKeyboardPair = player.device is Keyboard && (lostDevice is Keyboard || lostDevice is Mouse);
        if (player.device != lostDevice && !lostKeyboardPair) return;

        player.ready = false;
        player.stickDirection = 0;
        InputDevice keyboardFallback = Keyboard.current != null && Mouse.current != null && other.device is not Keyboard
            ? Keyboard.current : null;
        player.device = keyboardFallback;
        // A player with no fallback cannot keep a role occupied without a device.
        if (player.device == null) player.role = null;
    }

    private void RefreshDeviceChoices()
    {
        deviceChoices.Clear();
        deviceChoices.Add(new DeviceChoice(null, "None"));
        if (Keyboard.current != null && Mouse.current != null)
            deviceChoices.Add(new DeviceChoice(Keyboard.current, "Keyboard + Mouse"));
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];
            // Input tests and editor tools can leave synthetic Gamepads in the
            // global device list. Only native, currently connected hardware is
            // a meaningful choice for players.
            if (!pad.added || !pad.enabled || !pad.native) continue;
            deviceChoices.Add(new DeviceChoice(pad, pad.displayName));
        }

        refreshingDropdowns = true;
        List<TMP_Dropdown.OptionData> options = new(deviceChoices.Count);
        foreach (DeviceChoice choice in deviceChoices) options.Add(new TMP_Dropdown.OptionData(choice.label));
        SetDropdownOptions(playerOne.dropdown, options);
        SetDropdownOptions(playerTwo.dropdown, options);
        RefreshDropdownValues();
        refreshingDropdowns = false;
    }

    private static void SetDropdownOptions(TMP_Dropdown dropdown, List<TMP_Dropdown.OptionData> options)
    {
        if (dropdown == null) return;
        dropdown.options = new List<TMP_Dropdown.OptionData>(options);
        dropdown.RefreshShownValue();
    }

    private void RefreshDropdownValues()
    {
        SetDropdownValue(playerOne.dropdown, ChoiceIndex(playerOne.device));
        SetDropdownValue(playerTwo.dropdown, ChoiceIndex(playerTwo.device));
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown == null) return;
        dropdown.SetValueWithoutNotify(value);
        dropdown.RefreshShownValue();
    }

    private int ChoiceIndex(InputDevice device)
    {
        for (int i = 0; i < deviceChoices.Count; i++)
            if (deviceChoices[i].device == device) return i;
        return 0;
    }

    private static Gamepad FirstPhysicalGamepad()
    {
        foreach (Gamepad pad in Gamepad.all)
            if (pad.added && pad.enabled && pad.native) return pad;
        return null;
    }

    private void RefreshVisuals()
    {
        playerOne.label.text = "PLAYER 1\n" + DeviceLabel(playerOne.device);
        playerTwo.label.text = "PLAYER 2\n" + DeviceLabel(playerTwo.device);
        RefreshReadyVisuals();
        MoveCardImmediate(playerOne);
        MoveCardImmediate(playerTwo);
    }

    private static string DeviceLabel(InputDevice device)
    {
        if (device is Keyboard) return "Keyboard + Mouse";
        return device is Gamepad pad ? pad.displayName : "None";
    }

    private void RefreshReadyVisuals()
    {
        bool runnerReady = (playerOne.ready && playerOne.role == InputDeviceRouter.Role.Runner) ||
                           (playerTwo.ready && playerTwo.role == InputDeviceRouter.Role.Runner);
        bool drawerReady = (playerOne.ready && playerOne.role == InputDeviceRouter.Role.Drawer) ||
                           (playerTwo.ready && playerTwo.role == InputDeviceRouter.Role.Drawer);
        runnerPanel.enabled = runnerReady;
        drawerPanel.enabled = drawerReady;
        runnerReadyText.gameObject.SetActive(runnerReady);
        drawerReadyText.gameObject.SetActive(drawerReady);
        playerOne.dropdown.interactable = !playerOne.ready;
        playerTwo.dropdown.interactable = !playerTwo.ready;
    }

    private void MoveCard(PlayerSlot player)
    {
        if (player.card == null) return;
        Vector3 target = CardTarget(player);
        player.card.position = Vector3.Lerp(player.card.position, target,
            1f - Mathf.Exp(-cardMoveSpeed * Time.unscaledDeltaTime));
    }

    private void MoveCardImmediate(PlayerSlot player)
    {
        if (player.card != null) player.card.position = CardTarget(player);
    }

    private Vector3 CardTarget(PlayerSlot player)
    {
        RectTransform column = player.role == InputDeviceRouter.Role.Runner ? runnerColumn
            : player.role == InputDeviceRouter.Role.Drawer ? drawerColumn : neutralColumn;
        return column.TransformPoint(new Vector3(0f, cardTopOffset - player.index * cardRowSpacing, 0f));
    }

    private void TryCompleteSelection()
    {
        if (!playerOne.ready || !playerTwo.ready || !playerOne.role.HasValue || !playerTwo.role.HasValue ||
            playerOne.role == playerTwo.role || playerOne.device == null || playerTwo.device == null) return;

        completing = true;
        ResetRoleAssignments();
        bool first = InputDeviceRouter.TryClaim(playerOne.role.Value, playerOne.device);
        bool second = InputDeviceRouter.TryClaim(playerTwo.role.Value, playerTwo.device);
        if (!first || !second || !InputDeviceRouter.BothClaimed)
        {
            completing = false;
            playerOne.ready = false;
            playerTwo.ready = false;
            RefreshReadyVisuals();
            Debug.LogError("Local device selection could not create two distinct role assignments.");
            return;
        }

        FocusFirstLevelButton();
        gameObject.SetActive(false);
    }

    private static void ResetRoleAssignments()
    {
        InputDeviceRouter.Clear(InputDeviceRouter.Role.Runner);
        InputDeviceRouter.Clear(InputDeviceRouter.Role.Drawer);
    }

    private void FocusFirstLevelButton()
    {
        if (EventSystem.current == null || transform.parent == null) return;
        foreach (Button button in transform.parent.GetComponentsInChildren<Button>(true))
        {
            if (button.transform.IsChildOf(transform) || !button.gameObject.activeInHierarchy || !button.interactable) continue;
            if (!button.name.StartsWith("LevelDisplay", System.StringComparison.Ordinal)) continue;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            return;
        }
    }
}
