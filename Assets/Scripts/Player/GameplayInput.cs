using Allan;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Routes the local physical devices to roles. In an offline two-player room each
/// role keeps its assigned device; online the local role follows the last active device.
/// This class never sends device state through Photon.
/// </summary>
public static class GameplayInput
{
    private static readonly Gamepad[] pointerDevices = new Gamepad[2];
    private static readonly Vector2[] gamepadPointers = new Vector2[2];
    private static readonly int[] pointerFrames = { -1, -1 };
    private static Gamepad onlineGamepad;
    // Online device choice is local presentation/input state, never room state.
    // Sample it once per frame so Runner, Drawer, camera, and cursor agree.
    private static int deviceFrame = -1;
    private static bool onlineGamepadActive;
    private static GameManager deviceManager;

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
        // Do not switch in the middle of a stroke or on its release frame. This
        // lets Drawer finish the stroke with the device and coordinates that began it.
        if (onlineGamepadActive &&
            (pad.rightTrigger.isPressed || pad.rightTrigger.wasReleasedThisFrame ||
             pad.leftTrigger.isPressed || pad.leftTrigger.wasReleasedThisFrame ||
             pad.buttonSouth.isPressed || pad.buttonSouth.wasReleasedThisFrame)) return;
        if (!onlineGamepadActive &&
            ((mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)) ||
             (Keyboard.current != null &&
              (Keyboard.current.spaceKey.isPressed || Keyboard.current.spaceKey.wasReleasedThisFrame ||
               Keyboard.current.leftShiftKey.isPressed || Keyboard.current.leftShiftKey.wasReleasedThisFrame)))) return;

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
            return LocalAction(runner, "PointerPosition")?.ReadValue<Vector2>() ?? (Vector2)Input.mousePosition;

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
                Vector2 motion = LocalAction(runner, "PointerMove")?.ReadValue<Vector2>() ?? pad.rightStick.ReadValue();
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
        bool strokeHeld = onlineGamepad != null &&
            (onlineGamepad.rightTrigger.isPressed || onlineGamepad.leftTrigger.isPressed ||
             onlineGamepad.buttonSouth.isPressed);
        if (active != null && !strokeHeld) onlineGamepad = active;
        return onlineGamepad;
    }

    private static InputAction LocalAction(bool runner, string action)
    {
        if (!PhotonNetwork.OfflineMode || !GameManager.IsLocalMultiplayer) return null;
        return InputDeviceRouter.ActionsFor(runner ? InputDeviceRouter.Role.Runner : InputDeviceRouter.Role.Drawer)
            .FindAction((runner ? "Runner/" : "Drawer/") + action);
    }

    public static bool GrabPressed => LocalAction(true, "Grab")?.WasPressedThisFrame() ?? (RunnerUsesGamepad
        ? PadFor(true) != null && PadFor(true).leftTrigger.wasPressedThisFrame
        : Input.GetKeyDown(KeyCode.LeftShift));

    public static bool GrabReleased => LocalAction(true, "Grab")?.WasReleasedThisFrame() ?? (RunnerUsesGamepad
        ? PadFor(true) == null || PadFor(true).leftTrigger.wasReleasedThisFrame
        : Input.GetKeyUp(KeyCode.LeftShift));

    public static bool WirePressed => LocalAction(true, "Wire")?.WasPressedThisFrame() ?? (RunnerUsesGamepad
        ? PadFor(true) != null && PadFor(true).buttonWest.wasPressedThisFrame
        : Input.GetKeyDown(KeyCode.E));

    public static bool DrawPressed => (LocalAction(false, "Draw")?.WasPressedThisFrame() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && PadFor(false).rightTrigger.wasPressedThisFrame && !GamepadDrawerPointer.CapturesDraw
        : Input.GetMouseButtonDown(0))) && !GamepadDrawerPointer.CapturesDraw;

    public static bool DrawHeld => (LocalAction(false, "Draw")?.IsPressed() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && PadFor(false).rightTrigger.isPressed && !GamepadDrawerPointer.CapturesDraw
        : Input.GetMouseButton(0))) && !GamepadDrawerPointer.CapturesDraw;

    public static bool DrawReleased => (LocalAction(false, "Draw")?.WasReleasedThisFrame() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && PadFor(false).rightTrigger.wasReleasedThisFrame && !GamepadDrawerPointer.CapturesDraw
        : Input.GetMouseButtonUp(0))) && !GamepadDrawerPointer.CapturesDraw;

    public static bool EraserPressed => (LocalAction(false, "Erase")?.WasPressedThisFrame() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && !PadFor(false).rightTrigger.isPressed && PadFor(false).buttonWest.wasPressedThisFrame
        : Input.GetMouseButtonDown(1))) && (!DrawerUsesGamepad || !DrawHeld);

    public static int BrushStep
    {
        get
        {
            if (PhotonNetwork.OfflineMode && GameManager.IsLocalMultiplayer)
            {
                if (DrawerUsesGamepad)
                    return (LocalAction(false, "BrushNext")?.WasPressedThisFrame() == true ? 1 : 0) -
                           (LocalAction(false, "BrushPrevious")?.WasPressedThisFrame() == true ? 1 : 0);
                float localScroll = LocalAction(false, "BrushScroll")?.ReadValue<float>() ?? 0f;
                return localScroll > 0f ? 1 : localScroll < 0f ? -1 : 0;
            }
            if (DrawerUsesGamepad)
                return PadFor(false) == null ? 0 :
                    (PadFor(false).rightShoulder.wasPressedThisFrame ? -1 : 0) +
                    (PadFor(false).leftShoulder.wasPressedThisFrame ? 1 : 0);
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            return scroll > 0f ? 1 : scroll < 0f ? -1 : 0;
        }
    }

    public static bool CameraTogglePressed => LocalAction(false, "CameraToggle")?.WasPressedThisFrame() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && PadFor(false).buttonNorth.wasPressedThisFrame
        : Input.GetKeyDown(KeyCode.Q));

    public static bool PenPanelPressed => LocalAction(false, "PenPanel")?.WasPressedThisFrame() ?? (DrawerUsesGamepad
        ? PadFor(false) != null && PadFor(false).selectButton.wasPressedThisFrame
        : Input.GetKeyDown(KeyCode.Tab));

    public static Vector2 CameraMove => LocalAction(false, "CameraMove")?.ReadValue<Vector2>() ?? (DrawerUsesGamepad
        ? PadFor(false) != null ? PadFor(false).leftStick.ReadValue() : Vector2.zero
        : new Vector2(
            (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
            (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f)));
}
