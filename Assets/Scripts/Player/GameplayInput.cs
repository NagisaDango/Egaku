using Allan;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Routes the local physical devices to roles. In an offline two-player room each
/// role keeps its assigned device; online the local role follows the last active device.
/// This class never sends device state through Photon.
/// </summary>
public static class GameplayInput
{
    private static readonly string[] continuousOnlineActions =
        { "Runner/Jump", "Runner/Grab", "Drawer/Draw", "Drawer/Erase" };
    private static readonly Gamepad[] pointerDevices = new Gamepad[2];
    private static readonly Vector2[] gamepadPointers = new Vector2[2];
    private static readonly int[] pointerFrames = { -1, -1 };
    private static Gamepad onlineGamepad;
    // Online device choice is local presentation/input state, never room state.
    // Sample it once per frame so Runner, Drawer, camera, and cursor agree.
    private static int deviceFrame = -1;
    private static bool onlineGamepadActive;
    private static GameManager deviceManager;
    private static InputActionAsset onlineActions;
    private static bool onlineActionsUseGamepad;
    private static bool onlineActionsConfigured;
    // The control scheme alone cannot identify a second physical Gamepad.
    private static Gamepad configuredOnlineGamepad;
    private static bool bindingEventsSubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        if (bindingEventsSubscribed)
            EgakuInputBindings.BindingsChanged -= RefreshOnlineBindingOverrides;
        if (onlineActions != null) Object.Destroy(onlineActions);
        onlineActions = null;
        onlineActionsConfigured = false;
        configuredOnlineGamepad = null;
        bindingEventsSubscribed = false;
        onlineGamepad = null;
        onlineGamepadActive = false;
        deviceFrame = -1;
        deviceManager = null;
    }

    private static bool OnlineUsesGamepad
    {
        get
        {
            // Device preference is local only. A missing pad always falls back to keyboard/mouse.
            if (EgakuSettings.OnlineDevice == EgakuSettings.OnlineDeviceMode.KeyboardMouse) return false;
            if (EgakuSettings.OnlineDevice == EgakuSettings.OnlineDeviceMode.GamepadPreferred)
            {
                if (AvailableOnlineGamepad() == null) { onlineGamepadActive = false; return false; }
                // Plugging in a pad during a mouse stroke must not switch coordinates
                // before that stroke is released or force-finished by the settings menu.
                if (!onlineGamepadActive && Mouse.current != null &&
                    (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame))
                    return false;
                onlineGamepadActive = true;
                return true;
            }
            UpdateOnlineDevice();
            return onlineGamepadActive;
        }
    }

    private static void UpdateOnlineDevice()
    {
        if (deviceFrame == Time.frameCount) return;
        deviceFrame = Time.frameCount;

        GameManager manager = GameManager.Instance;
        if (deviceManager != manager)
        {
            // A new lobby/session must not inherit a stale choice from its predecessor.
            deviceManager = manager;
            onlineGamepadActive = false;
        }

        Gamepad pad = AvailableOnlineGamepad();
        if (pad == null)
        {
            // A removed controller immediately yields to keyboard/mouse online.
            onlineGamepadActive = false;
            return;
        }
        if (!Application.isFocused || PhotonNetwork.OfflineMode) return;

        Mouse mouse = Mouse.current;
        // Keep the current device through a held or just-released gameplay action.
        // The action copy includes binding overrides, so rebinding Draw/Grab/Jump does
        // not reintroduce a mid-stroke or mid-grab device switch.
        if (OnlineActionInProgress(onlineGamepadActive)) return;

        bool keyboardActivity = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool mouseActivity = mouse != null &&
            (mouse.delta.ReadValue().sqrMagnitude > 9f ||
             mouse.scroll.ReadValue().sqrMagnitude > 0.01f ||
             mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
        // A quarter-stick threshold accepts deliberate small movements while
        // ignoring ordinary analogue drift from an idle controller.
        bool gamepadActivity = pad.leftStick.ReadValue().sqrMagnitude > 0.0625f ||
            pad.rightStick.ReadValue().sqrMagnitude > 0.0625f ||
            pad.dpad.ReadValue().sqrMagnitude > 0.0625f ||
            pad.leftTrigger.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame ||
            pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
            pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
            pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
            pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame;

        // Simultaneous activity is ambiguous, so keep the current owner of input.
        if (gamepadActivity) onlineGamepad = pad;
        if (gamepadActivity && !keyboardActivity && !mouseActivity) onlineGamepadActive = true;
        else if ((keyboardActivity || mouseActivity) && !gamepadActivity) onlineGamepadActive = false;
    }

    public static bool RunnerUsesGamepad
    {
        get
        {
            GameManager manager = GameManager.Instance;
            return manager != null && (PhotonNetwork.OfflineMode
                ? GameManager.IsLocalMultiplayer && InputDeviceRouter.Kind(InputDeviceRouter.Role.Runner) == InputDeviceRouter.DeviceKind.Gamepad
                : OnlineUsesGamepad);
        }
    }

    public static bool DrawerUsesGamepad
    {
        get
        {
            GameManager manager = GameManager.Instance;
            return manager != null && (PhotonNetwork.OfflineMode
                ? GameManager.IsLocalMultiplayer && InputDeviceRouter.Kind(InputDeviceRouter.Role.Drawer) == InputDeviceRouter.DeviceKind.Gamepad
                : OnlineUsesGamepad);
        }
    }

    /// <summary>
    /// Returns the exact online gamepad selected by the same Auto/Keyboard/Gamepad Preferred
    /// policy as gameplay. Lobby UI uses this instead of Gamepad.current so device switching
    /// behaves consistently before and after the level starts.
    /// </summary>
    public static Gamepad OnlineUiGamepad => !PhotonNetwork.OfflineMode && OnlineUsesGamepad
        ? AvailableOnlineGamepad()
        : null;

    public static Vector2 PointerScreenPosition(bool runner)
    {
        if (!(runner ? RunnerUsesGamepad : DrawerUsesGamepad))
            return RoleAction(runner, "PointerPosition")?.ReadValue<Vector2>() ?? (Vector2)Input.mousePosition;

        // Multiple gameplay components may read the pointer in one frame. Advance
        // it once so camera bounds, drawing, and the visible cursor agree exactly.
        int index = runner ? 0 : 1;
        Gamepad pad = PadFor(runner);
        if (pointerDevices[index] != pad)
        {
            pointerDevices[index] = pad;
            gamepadPointers[index] = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            pointerFrames[index] = -1;
        }
        if (pointerFrames[index] != Time.frameCount)
        {
            pointerFrames[index] = Time.frameCount;
            if (pad != null && Application.isFocused)
            {
                Vector2 motion = RoleAction(runner, "PointerMove")?.ReadValue<Vector2>() ?? Vector2.zero;
                gamepadPointers[index] += motion * (EgakuSettings.GamepadPointerSpeed * Time.unscaledDeltaTime);
            }
            gamepadPointers[index].x = Mathf.Clamp(gamepadPointers[index].x, 0f, Mathf.Max(0f, Screen.width - 1f));
            gamepadPointers[index].y = Mathf.Clamp(gamepadPointers[index].y, 0f, Mathf.Max(0f, Screen.height - 1f));
        }
        return gamepadPointers[index];
    }

    public static Gamepad PadFor(bool runner) => PhotonNetwork.OfflineMode && GameManager.IsLocalMultiplayer
        ? InputDeviceRouter.GamepadFor(runner ? InputDeviceRouter.Role.Runner : InputDeviceRouter.Role.Drawer)
        : AvailableOnlineGamepad();

    private static Gamepad AvailableOnlineGamepad()
    {
        if (onlineGamepad == null || !onlineGamepad.added)
            onlineGamepad = Gamepad.all.Count > 0 ? Gamepad.all[0] : null;
        Gamepad active = null;
        foreach (Gamepad candidate in Gamepad.all)
        {
            // Follow a deliberate second controller without relying on the
            // process-wide Gamepad.current, which would break local role pairing.
            bool used = candidate.leftStick.ReadValue().sqrMagnitude > 0.0625f ||
                candidate.rightStick.ReadValue().sqrMagnitude > 0.0625f ||
                candidate.dpad.ReadValue().sqrMagnitude > 0.0625f ||
                candidate.leftTrigger.wasPressedThisFrame || candidate.rightTrigger.wasPressedThisFrame ||
                candidate.leftShoulder.wasPressedThisFrame || candidate.rightShoulder.wasPressedThisFrame ||
                candidate.buttonSouth.wasPressedThisFrame || candidate.buttonEast.wasPressedThisFrame ||
                candidate.buttonWest.wasPressedThisFrame || candidate.buttonNorth.wasPressedThisFrame ||
                candidate.selectButton.wasPressedThisFrame || candidate.startButton.wasPressedThisFrame;
            if (!used) continue;
            if (active != null) return onlineGamepad; // Ambiguous simultaneous input.
            active = candidate;
        }
        // Once the online action copy is available, use its overridden bindings to
        // decide whether it is safe to follow activity from a different controller.
        bool strokeHeld = OnlineActionInProgress(true, false);
        if (onlineActions == null)
            strokeHeld = onlineGamepad != null &&
                (onlineGamepad.rightTrigger.isPressed || onlineGamepad.leftTrigger.isPressed ||
                 onlineGamepad.buttonSouth.isPressed);
        if (active != null && !strokeHeld) onlineGamepad = active;
        return onlineGamepad;
    }

    private static bool OnlineActionInProgress(bool useGamepad, bool includeReleaseFrame = true)
    {
        if (onlineActions == null || !onlineActionsConfigured || onlineActionsUseGamepad != useGamepad)
            return false;

        // These actions can own continuous world state. Waiting through their release
        // frame keeps the same device responsible for completing that state transition.
        foreach (string path in continuousOnlineActions)
        {
            InputAction action = onlineActions.FindAction(path, false);
            if (action != null && (action.IsPressed() || (includeReleaseFrame && action.WasReleasedThisFrame())))
                return true;
        }
        return false;
    }

    private static InputAction RoleAction(bool runner, string action)
    {
        if (PhotonNetwork.OfflineMode && GameManager.IsLocalMultiplayer)
            return InputDeviceRouter.ActionsFor(runner ? InputDeviceRouter.Role.Runner : InputDeviceRouter.Role.Drawer)
                .FindAction((runner ? "Runner/" : "Drawer/") + action);

        EnsureOnlineActions(runner ? RunnerUsesGamepad : DrawerUsesGamepad);
        return onlineActions?.FindAction((runner ? "Runner/" : "Drawer/") + action);
    }

    private static void EnsureOnlineActions(bool useGamepad)
    {
        if (onlineActions == null)
        {
            onlineActions = EgakuInputBindings.CreateActionCopy();
            if (onlineActions == null) return;
            if (!bindingEventsSubscribed)
            {
                EgakuInputBindings.BindingsChanged += RefreshOnlineBindingOverrides;
                bindingEventsSubscribed = true;
            }
        }
        Gamepad selectedPad = useGamepad ? AvailableOnlineGamepad() : null;
        if (onlineActionsConfigured && onlineActionsUseGamepad == useGamepad && configuredOnlineGamepad == selectedPad) return;

        onlineActions.Disable();
        onlineActionsUseGamepad = useGamepad;
        configuredOnlineGamepad = selectedPad;
        onlineActions.bindingMask = InputBinding.MaskByGroup(useGamepad ? "Gamepad" : "Keyboard&Mouse");
        if (useGamepad)
        {
            Gamepad pad = selectedPad;
            onlineActions.devices = pad != null
                ? new ReadOnlyArray<InputDevice>(new InputDevice[] { pad })
                : new ReadOnlyArray<InputDevice>(System.Array.Empty<InputDevice>());
        }
        else
        {
            var devices = new System.Collections.Generic.List<InputDevice>(2);
            if (Keyboard.current != null) devices.Add(Keyboard.current);
            if (Mouse.current != null) devices.Add(Mouse.current);
            onlineActions.devices = new ReadOnlyArray<InputDevice>(devices.ToArray());
        }
        onlineActions.Enable();
        onlineActionsConfigured = true;
    }

    private static void RefreshOnlineBindingOverrides()
    {
        if (onlineActions == null) return;
        bool useGamepad = onlineActionsUseGamepad;
        onlineActionsConfigured = false;
        EgakuInputBindings.ApplyTo(onlineActions);
        EnsureOnlineActions(useGamepad);
    }

    public static bool JumpPressed => RoleAction(true, "Jump")?.WasPressedThisFrame() == true;
    public static bool JumpReleased => RoleAction(true, "Jump")?.WasReleasedThisFrame() == true;

    public static bool GrabPressed => RoleAction(true, "Grab")?.WasPressedThisFrame() == true;

    public static bool GrabReleased => RoleAction(true, "Grab")?.WasReleasedThisFrame() == true;
    // Level state must recover even if the release edge occurred while input was suspended.
    public static bool GrabHeld => RoleAction(true, "Grab")?.IsPressed() == true;

    public static bool WirePressed => RoleAction(true, "Wire")?.WasPressedThisFrame() == true;

    public static bool PointerClickPressed => RoleAction(false, "Draw")?.WasPressedThisFrame() == true;
    public static bool PointerClickReleased => RoleAction(false, "Draw")?.WasReleasedThisFrame() == true;

    public static bool DrawPressed => PointerClickPressed && !GamepadDrawerPointer.CapturesDraw;

    public static bool DrawHeld => (RoleAction(false, "Draw")?.IsPressed() == true) &&
                                   !GamepadDrawerPointer.CapturesDraw;

    public static bool DrawReleased => PointerClickReleased && !GamepadDrawerPointer.CapturesDraw;

    public static bool EraserPressed => RoleAction(false, "Erase")?.WasPressedThisFrame() == true &&
                                        (!DrawerUsesGamepad || !DrawHeld);

    public static int BrushStep
    {
        get
        {
            if (PhotonNetwork.OfflineMode && GameManager.IsLocalMultiplayer)
            {
                if (DrawerUsesGamepad)
                    return (RoleAction(false, "BrushNext")?.WasPressedThisFrame() == true ? 1 : 0) -
                           (RoleAction(false, "BrushPrevious")?.WasPressedThisFrame() == true ? 1 : 0);
                float localScroll = RoleAction(false, "BrushScroll")?.ReadValue<float>() ?? 0f;
                return localScroll > 0f ? 1 : localScroll < 0f ? -1 : 0;
            }
            if (DrawerUsesGamepad)
                return (RoleAction(false, "BrushNext")?.WasPressedThisFrame() == true ? 1 : 0) -
                       (RoleAction(false, "BrushPrevious")?.WasPressedThisFrame() == true ? 1 : 0);
            float scroll = RoleAction(false, "BrushScroll")?.ReadValue<float>() ?? 0f;
            return scroll > 0f ? 1 : scroll < 0f ? -1 : 0;
        }
    }

    public static bool CameraTogglePressed => RoleAction(false, "CameraToggle")?.WasPressedThisFrame() == true;

    public static bool PenPanelPressed => RoleAction(false, "PenPanel")?.WasPressedThisFrame() == true;

    public static Vector2 CameraMove => RoleAction(false, "CameraMove")?.ReadValue<Vector2>() ?? Vector2.zero;
}
