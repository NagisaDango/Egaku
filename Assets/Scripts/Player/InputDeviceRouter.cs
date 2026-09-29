using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// Owns the two physical device assignments on this machine. A Photon role is never
/// inferred from a device and assignments are never written to room properties.
/// </summary>
public static class InputDeviceRouter
{
    public enum Role { Runner, Drawer }
    public enum DeviceKind { None, KeyboardMouse, Gamepad }

    private sealed class Slot
    {
        public InputUser user;
        public InputActionAsset actions;
        public DeviceKind kind;
        public Gamepad gamepad;
    }

    private static readonly Slot[] slots = { new Slot(), new Slot() };
    private static bool initialized;
    public static event Action AssignmentsChanged;
    public static event Action<Role> DeviceLost;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewSession()
    {
        // Editor sessions can skip domain reload; old InputUsers must not retain
        // controllers or callbacks from a previous Play Mode session.
        if (initialized)
        {
            InputUser.onChange -= OnUserChange;
            foreach (Slot slot in slots)
            {
                if (slot.actions != null) slot.actions.Disable();
                // Input System can reset its global user table before this hook
                // when Enter Play Mode skips a managed-domain reload.
                if (slot.user.valid) slot.user.UnpairDevicesAndRemoveUser();
                if (slot.actions != null) UnityEngine.Object.Destroy(slot.actions);
                slot.actions = null;
                slot.gamepad = null;
                slot.kind = DeviceKind.None;
            }
        }
        initialized = false;
        AssignmentsChanged = null;
        DeviceLost = null;
    }

    public static void Initialize()
    {
        if (initialized) return;
        // The existing Runner prefab keeps the source asset reference; each role
        // receives a private copy so InputUser can restrict it to its own devices.
        GameObject runner = Resources.Load<GameObject>("Runner");
        PlayerInput input = runner != null ? runner.GetComponent<PlayerInput>() : null;
        if (input == null || input.actions == null)
        {
            Debug.LogError("InputDeviceRouter requires the Runner prefab's input action asset.");
            return;
        }
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].actions = UnityEngine.Object.Instantiate(input.actions);
            slots[i].user = InputUser.CreateUserWithoutPairedDevices();
            slots[i].user.AssociateActionsWithUser(slots[i].actions);
            slots[i].actions.Enable();
        }
        InputUser.onChange += OnUserChange;
        initialized = true;
    }

    public static DeviceKind Kind(Role role) { Initialize(); return slots[(int)role].kind; }
    public static Gamepad GamepadFor(Role role) { Initialize(); Gamepad pad = slots[(int)role].gamepad; return pad != null && pad.added ? pad : null; }
    public static InputActionAsset ActionsFor(Role role) { Initialize(); return slots[(int)role].actions; }
    public static InputActionAsset CreateTemporaryActions(InputDevice device)
    {
        Initialize();
        InputActionAsset copy = UnityEngine.Object.Instantiate(slots[0].actions);
        InputDevice[] devices = device is Keyboard || device is Mouse
            ? new InputDevice[] { Keyboard.current, Mouse.current }
            : new[] { device };
        // Online and recovery menus have no local role user. Restrict this copy
        // explicitly so another connected controller cannot navigate their UI.
        copy.devices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(devices);
        copy.bindingMask = InputBinding.MaskByGroup(device is Gamepad ? "Gamepad" : "Keyboard&Mouse");
        return copy;
    }
    public static bool BothClaimed => Ready(slots[0]) && Ready(slots[1]);
    public static bool IsReady(Role role) { Initialize(); return Ready(slots[(int)role]); }

    private static bool Ready(Slot slot) => slot.kind == DeviceKind.KeyboardMouse
        ? Keyboard.current != null && Mouse.current != null
        : slot.kind == DeviceKind.Gamepad && slot.gamepad != null && slot.gamepad.added;

    public static Role? OwnerOf(InputDevice device)
    {
        if (device == null) return null;
        Initialize();
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot.kind == DeviceKind.Gamepad && slot.gamepad == device) return (Role)i;
            if (slot.kind == DeviceKind.KeyboardMouse && (device is Keyboard || device is Mouse)) return (Role)i;
        }
        return null;
    }

    public static bool TryClaim(Role role, InputDevice device)
    {
        Initialize();
        if (!initialized || device == null || !(device is Gamepad || device is Keyboard || device is Mouse)) return false;
        Role? owner = OwnerOf(device);
        if (owner.HasValue && owner.Value != role) return false;
        Assign(role, device is Gamepad pad ? pad : null);
        AssignmentsChanged?.Invoke();
        return true;
    }

    public static bool TrySwap(Role requester, InputDevice requestedDevice, InputDevice confirmingDevice)
    {
        Role? owner = OwnerOf(requestedDevice);
        if (!owner.HasValue || owner.Value == requester || OwnerOf(confirmingDevice) != owner) return false;
        Slot first = slots[(int)requester];
        Slot second = slots[(int)owner.Value];
        DeviceKind firstKind = first.kind;
        Gamepad firstPad = first.gamepad;
        DeviceKind secondKind = second.kind;
        Gamepad secondPad = second.gamepad;
        // Release both users before pairing; no frame can observe duplicate ownership.
        Unpair(first);
        Unpair(second);
        Pair(first, secondKind, secondPad);
        Pair(second, firstKind, firstPad);
        SaveLayoutIfReady();
        AssignmentsChanged?.Invoke();
        return true;
    }

    public static void Clear(Role role)
    {
        Initialize();
        Slot slot = slots[(int)role];
        Unpair(slot);
        slot.kind = DeviceKind.None;
        slot.gamepad = null;
        slot.actions.bindingMask = null;
        AssignmentsChanged?.Invoke();
    }

    private static void Assign(Role role, Gamepad pad)
    {
        Slot slot = slots[(int)role];
        Unpair(slot);
        Pair(slot, pad == null ? DeviceKind.KeyboardMouse : DeviceKind.Gamepad, pad);
        SaveLayoutIfReady();
    }

    private static void SaveLayoutIfReady()
    {
        if (!BothClaimed) return;
        EgakuSettings.SaveLocalLayout(slots[0].kind == DeviceKind.KeyboardMouse
            ? EgakuSettings.LocalDeviceLayout.RunnerKeyboard
            : slots[1].kind == DeviceKind.KeyboardMouse
                ? EgakuSettings.LocalDeviceLayout.DrawerKeyboard
                : EgakuSettings.LocalDeviceLayout.TwoGamepads);
    }

    private static void Unpair(Slot slot)
    {
        // Disabling actions discards held values before another physical device is paired.
        slot.actions.Disable();
        slot.user.UnpairDevices();
    }

    private static void Pair(Slot slot, DeviceKind kind, Gamepad pad)
    {
        slot.kind = kind;
        slot.gamepad = pad;
        slot.actions.bindingMask = kind == DeviceKind.Gamepad ? InputBinding.MaskByGroup("Gamepad")
            : kind == DeviceKind.KeyboardMouse ? InputBinding.MaskByGroup("Keyboard&Mouse") : null;
        if (kind == DeviceKind.Gamepad && pad != null)
            InputUser.PerformPairingWithDevice(pad, slot.user);
        else if (kind == DeviceKind.KeyboardMouse)
        {
            if (Keyboard.current != null) InputUser.PerformPairingWithDevice(Keyboard.current, slot.user);
            if (Mouse.current != null) InputUser.PerformPairingWithDevice(Mouse.current, slot.user);
        }
        slot.actions.Enable();
    }

    private static void OnUserChange(InputUser user, InputUserChange change, InputDevice device)
    {
        if (change != InputUserChange.DeviceLost && change != InputUserChange.DeviceRegained) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].user != user) continue;
            if (change == InputUserChange.DeviceLost)
            {
                Slot lost = slots[i];
                // A disconnected local gamepad falls back to keyboard/mouse only
                // when the other role does not already own that shared device pair.
                // Otherwise this role becomes unassigned and the recovery UI pauses play.
                bool keyboardFree = lost.kind == DeviceKind.Gamepad &&
                    Keyboard.current != null && Mouse.current != null &&
                    slots[1 - i].kind != DeviceKind.KeyboardMouse;
                Unpair(lost);
                Pair(lost, keyboardFree ? DeviceKind.KeyboardMouse : DeviceKind.None, null);
                SaveLayoutIfReady();
                DeviceLost?.Invoke((Role)i);
            }
            else if (slots[i].kind == DeviceKind.Gamepad && device is Gamepad restoredPad)
                slots[i].gamepad = restoredPad;
            AssignmentsChanged?.Invoke();
            return;
        }
    }
}
