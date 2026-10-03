using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Owns the Keys page draft and interactive capture. Gameplay actions receive the
/// draft only when the parent settings menu applies all settings.
/// </summary>
public sealed class KeyRebindController : MonoBehaviour
{
    private sealed class BindingSlot
    {
        public string map;
        public string action;
        public string group;
        public string part;
        public int occurrence;
        public string fixedCaption;
        public string suffix;

        public bool Visible => action != null || fixedCaption != null;
        public bool Rebindable => action != null;
    }

    private sealed class RowDefinition
    {
        public string label;
        public BindingSlot keyboardPrimary;
        public BindingSlot keyboardAlternate;
        public BindingSlot gamepadPrimary;
        public BindingSlot gamepadAlternate;
    }

    private sealed class RowRuntime
    {
        public RowDefinition definition;
        public KeyBindingRow view;
    }

    [SerializeField] private KeyBindingRow rowPrefab;
    [SerializeField] private RectTransform runnerContent;
    [SerializeField] private RectTransform drawerContent;

    private readonly List<RowRuntime> runnerRows = new();
    private readonly List<RowRuntime> drawerRows = new();
    private readonly List<Button> visibleButtons = new();

    private InputActionAsset draftActions;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private EventSystem menuEventSystem;
    private TMP_Text note;
    private Action<bool> setUiInputEnabled;
    private Button returnButton;
    private string beforeRebindJson;
    private string originalPath;
    private BindingSlot activeSlot;
    private bool keepOriginal;
    private bool pendingCapture;
    // Menu close destroys the draft immediately. Suppress the normal cancel callback so
    // it cannot refresh rows or selection while the settings hierarchy is shutting down.
    private bool suppressRebindCallback;
    private float captureStartedAt;
    private float blockMenuUntil;
    private bool showingDrawer;

    public bool BlocksMenuInput => pendingCapture || rebindOperation != null || Time.unscaledTime < blockMenuUntil;
    public IReadOnlyList<Button> VisibleButtons => visibleButtons;

    public void Initialize(EventSystem eventSystem, TMP_Text helpText, Action<bool> uiInputToggle)
    {
        menuEventSystem = eventSystem;
        note = helpText;
        setUiInputEnabled = uiInputToggle;
        BuildRowsIfNeeded();
    }

    public void BeginSession()
    {
        EndSession(false);
        draftActions = EgakuInputBindings.CreateDraft();
        RefreshRows();
    }

    public void EndSession(bool commit)
    {
        CancelOperation();
        if (draftActions == null) return;
        if (commit) EgakuInputBindings.CommitDraft(draftActions);
        Destroy(draftActions);
        draftActions = null;
    }

    public void ShowRole(bool drawer)
    {
        showingDrawer = drawer;
        if (runnerContent != null) runnerContent.transform.parent.gameObject.SetActive(!drawer);
        if (drawerContent != null) drawerContent.transform.parent.gameObject.SetActive(drawer);
        RefreshRows();
        RebuildVisibleButtons();
    }

    public void Tick()
    {
        if (rebindOperation == null || note == null) return;
        int seconds = Mathf.Max(0, Mathf.CeilToInt(10f - (Time.unscaledTime - captureStartedAt)));
        note.text = "Press a new input  |  Current input keeps this binding  |  " + seconds + " s";
    }

    public void ResetVisibleRole()
    {
        if (draftActions == null || BlocksMenuInput) return;
        EgakuInputBindings.ResetRole(draftActions, showingDrawer ? "Drawer" : "Runner");
        RefreshRows();
        if (note != null) note.text = (showingDrawer ? "Drawer" : "Runner") +
            " bindings restored in the draft. Apply Settings to save.";
    }

    private void BuildRowsIfNeeded()
    {
        if (rowPrefab == null || runnerContent == null || drawerContent == null) return;
        if (runnerRows.Count == 0) BuildRows(runnerContent, RunnerDefinitions(), runnerRows);
        if (drawerRows.Count == 0) BuildRows(drawerContent, DrawerDefinitions(), drawerRows);
    }

    private void BuildRows(RectTransform parent, IEnumerable<RowDefinition> definitions, List<RowRuntime> rows)
    {
        foreach (RowDefinition definition in definitions)
        {
            KeyBindingRow view = Instantiate(rowPrefab, parent, false);
            view.name = definition.label.Trim() + " Binding Row";
            rows.Add(new RowRuntime { definition = definition, view = view });
        }
    }

    private void RefreshRows()
    {
        if (draftActions == null) return;
        foreach (RowRuntime row in runnerRows) RefreshRow(row);
        foreach (RowRuntime row in drawerRows) RefreshRow(row);
    }

    private void RefreshRow(RowRuntime runtime)
    {
        runtime.view.SetLabel(runtime.definition.label);
        ConfigureSlot(runtime.view, runtime.view.KeyboardPrimary, runtime.definition.keyboardPrimary);
        ConfigureSlot(runtime.view, runtime.view.KeyboardAlternate, runtime.definition.keyboardAlternate);
        ConfigureSlot(runtime.view, runtime.view.GamepadPrimary, runtime.definition.gamepadPrimary);
        ConfigureSlot(runtime.view, runtime.view.GamepadAlternate, runtime.definition.gamepadAlternate);
        runtime.view.RefreshDeviceGroups();
    }

    private void ConfigureSlot(KeyBindingRow row, Button button, BindingSlot slot)
    {
        bool visible = slot != null && slot.Visible;
        string caption = visible ? GetCaption(slot) : string.Empty;
        row.ConfigureButton(button, caption,
            slot != null && slot.Rebindable ? () => QueueRebind(slot, button) : null,
            visible, slot != null && slot.Rebindable);
    }

    private string GetCaption(BindingSlot slot)
    {
        if (slot.fixedCaption != null) return slot.fixedCaption;
        if (!TryGetBinding(slot, out InputAction action, out int bindingIndex)) return "Missing";
        // Full names make alternate keys and stick directions understandable without
        // requiring platform-specific icon knowledge (for example, "Up Arrow").
        string value = action.GetBindingDisplayString(bindingIndex,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        value = ExpandArrowKeyCaption(action.bindings[bindingIndex].effectivePath, value);
        if (string.IsNullOrWhiteSpace(value)) value = "Unbound";
        return value + (slot.suffix ?? string.Empty);
    }

    private static string ExpandArrowKeyCaption(string effectivePath, string fallback)
    {
        // Input System shortens arrow keys to "Up", "Down", etc. The Keys table
        // names them explicitly so they cannot be mistaken for the action direction.
        return effectivePath switch
        {
            "<Keyboard>/upArrow" => "Up Arrow",
            "<Keyboard>/downArrow" => "Down Arrow",
            "<Keyboard>/leftArrow" => "Left Arrow",
            "<Keyboard>/rightArrow" => "Right Arrow",
            _ => fallback
        };
    }

    private void QueueRebind(BindingSlot slot, Button button)
    {
        if (draftActions == null || BlocksMenuInput || !TryGetBinding(slot, out _, out _)) return;
        activeSlot = slot;
        returnButton = button;
        pendingCapture = true;
        blockMenuUntil = Time.unscaledTime + 0.2f;
        setUiInputEnabled?.Invoke(false);
        StartCoroutine(StartRebindNextFrame());
    }

    private IEnumerator StartRebindNextFrame()
    {
        // The Submit that opened this row must finish before capture starts, otherwise
        // A/Enter would immediately become the new gameplay binding.
        yield return null;
        pendingCapture = false;
        if (draftActions == null || activeSlot == null ||
            !TryGetBinding(activeSlot, out InputAction action, out int bindingIndex))
        {
            FinishOperationUi();
            yield break;
        }

        beforeRebindJson = draftActions.SaveBindingOverridesAsJson();
        originalPath = action.bindings[bindingIndex].effectivePath;
        keepOriginal = false;
        suppressRebindCallback = false;
        captureStartedAt = Time.unscaledTime;
        if (note != null) note.text = "Press a new input  |  Current input keeps this binding  |  10 s";

        rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithTimeout(10f)
            .WithMagnitudeHavingToBeGreaterThan(0.2f)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Sensor>")
            .OnPotentialMatch(operation =>
            {
                InputControl control = operation.selectedControl;
                if (control == null || !DeviceMatches(activeSlot.group, control.device))
                {
                    if (control != null) operation.RemoveCandidate(control);
                    return;
                }
                if (!string.IsNullOrEmpty(originalPath) && InputControlPath.Matches(originalPath, control))
                {
                    keepOriginal = true;
                    operation.Cancel();
                    return;
                }
                operation.Complete();
            })
            .OnCancel(_ =>
            {
                if (!suppressRebindCallback) FinishRebind(true);
            })
            .OnComplete(operation =>
            {
                if (!suppressRebindCallback) FinishRebind(false);
            });
        rebindOperation.Start();
    }

    private void FinishRebind(bool canceled)
    {
        InputControl selected = rebindOperation != null ? rebindOperation.selectedControl : null;
        string conflict = !canceled && selected != null ? FindConflict(activeSlot, selected) : null;
        bool restore = canceled || keepOriginal || conflict != null;
        if (restore) RestoreBeforeRebind();

        string message = conflict != null
            ? "Already assigned to " + conflict + ". The previous binding was kept."
            : keepOriginal
                ? "Binding unchanged."
                : canceled ? "No input selected. Binding unchanged." : "Binding changed in the draft.";

        DisposeOperation();
        RefreshRows();
        if (note != null) note.text = message;
        FinishOperationUi();
    }

    private string FindConflict(BindingSlot slot, InputControl selected)
    {
        if (!TryGetBinding(slot, out InputAction targetAction, out int targetIndex)) return null;
        InputActionMap map = targetAction.actionMap;
        Guid targetId = targetAction.bindings[targetIndex].id;
        foreach (InputAction action in map.actions)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.id == targetId || binding.isComposite ||
                    !BindingHasGroup(binding, slot.group) || string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (InputControlPath.Matches(binding.effectivePath, selected))
                    return action.name + (binding.isPartOfComposite ? " / " + binding.name : string.Empty);
            }
        }
        return null;
    }

    private void RestoreBeforeRebind()
    {
        if (draftActions == null) return;
        draftActions.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(beforeRebindJson))
            draftActions.LoadBindingOverridesFromJson(beforeRebindJson, true);
    }

    private void FinishOperationUi()
    {
        pendingCapture = false;
        blockMenuUntil = Time.unscaledTime + 0.2f;
        setUiInputEnabled?.Invoke(true);
        if (menuEventSystem != null && returnButton != null && returnButton.gameObject.activeInHierarchy)
            menuEventSystem.SetSelectedGameObject(returnButton.gameObject);
        activeSlot = null;
        returnButton = null;
    }

    private void CancelOperation()
    {
        StopAllCoroutines();
        pendingCapture = false;
        if (rebindOperation != null)
        {
            suppressRebindCallback = true;
            rebindOperation.Cancel();
            DisposeOperation();
            suppressRebindCallback = false;
        }
        setUiInputEnabled?.Invoke(true);
        activeSlot = null;
        returnButton = null;
    }

    private void DisposeOperation()
    {
        if (rebindOperation == null) return;
        rebindOperation.Dispose();
        rebindOperation = null;
    }

    private void RebuildVisibleButtons()
    {
        visibleButtons.Clear();
        List<RowRuntime> rows = showingDrawer ? drawerRows : runnerRows;
        foreach (RowRuntime runtime in rows)
        {
            AddVisible(runtime.view.KeyboardPrimary);
            AddVisible(runtime.view.KeyboardAlternate);
            AddVisible(runtime.view.GamepadPrimary);
            AddVisible(runtime.view.GamepadAlternate);
        }
    }

    public void ConfigureGridNavigation(Button above, Button below)
    {
        List<RowRuntime> rows = showingDrawer ? drawerRows : runnerRows;
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Button[] columns = rows[rowIndex].view.ButtonsByColumn;
            for (int column = 0; column < columns.Length; column++)
            {
                Button button = columns[column];
                if (!IsNavigable(button)) continue;

                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = FindHorizontal(columns, column, -1);
                navigation.selectOnRight = FindHorizontal(columns, column, 1);
                navigation.selectOnUp = FindVertical(rows, rowIndex, -1, column) ?? above;
                navigation.selectOnDown = FindVertical(rows, rowIndex, 1, column) ?? below;
                button.navigation = navigation;
            }
        }
    }

    private static Button FindHorizontal(Button[] columns, int start, int direction)
    {
        for (int column = start + direction; column >= 0 && column < columns.Length; column += direction)
            if (IsNavigable(columns[column])) return columns[column];
        return null;
    }

    private static Button FindVertical(List<RowRuntime> rows, int startRow, int direction, int targetColumn)
    {
        for (int row = startRow + direction; row >= 0 && row < rows.Count; row += direction)
        {
            Button[] columns = rows[row].view.ButtonsByColumn;
            Button nearest = null;
            int nearestDistance = int.MaxValue;
            for (int column = 0; column < columns.Length; column++)
            {
                if (!IsNavigable(columns[column])) continue;
                int distance = Mathf.Abs(column - targetColumn);
                if (distance >= nearestDistance) continue;
                nearest = columns[column];
                nearestDistance = distance;
            }
            if (nearest != null) return nearest;
        }
        return null;
    }

    private static bool IsNavigable(Button button) =>
        button != null && button.gameObject.activeInHierarchy && button.interactable;

    private void AddVisible(Button button)
    {
        if (IsNavigable(button))
            visibleButtons.Add(button);
    }

    private bool TryGetBinding(BindingSlot slot, out InputAction action, out int bindingIndex)
    {
        action = draftActions?.FindAction(slot.map + "/" + slot.action, false);
        bindingIndex = -1;
        if (action == null) return false;
        int occurrence = 0;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            bool partMatches = slot.part != null
                ? binding.isPartOfComposite && string.Equals(binding.name, slot.part, StringComparison.OrdinalIgnoreCase)
                : !binding.isComposite && !binding.isPartOfComposite;
            if (!partMatches || !BindingHasGroup(binding, slot.group)) continue;
            if (occurrence++ != slot.occurrence) continue;
            bindingIndex = i;
            return true;
        }
        return false;
    }

    private static bool BindingHasGroup(InputBinding binding, string group)
    {
        if (string.IsNullOrEmpty(binding.groups)) return false;
        foreach (string candidate in binding.groups.Split(';'))
            if (string.Equals(candidate, group, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool DeviceMatches(string group, InputDevice device) => group == "Gamepad"
        ? device is Gamepad : device is Keyboard || device is Mouse;

    private static BindingSlot Bind(string map, string action, string group,
        string part = null, int occurrence = 0, string suffix = null) => new()
    {
        map = map,
        action = action,
        group = group,
        part = part,
        occurrence = occurrence,
        suffix = suffix
    };

    private static BindingSlot Fixed(string caption) => new() { fixedCaption = caption };

    private static IEnumerable<RowDefinition> RunnerDefinitions()
    {
        string[] parts = { "up", "down", "left", "right" };
        string[] labels = { "Move\n  Up", "  Down", "  Left", "  Right" };
        for (int i = 0; i < parts.Length; i++)
            yield return new RowDefinition
            {
                label = labels[i],
                keyboardPrimary = Bind("Runner", "Move", "Keyboard&Mouse", parts[i]),
                keyboardAlternate = Bind("Runner", "Move", "Keyboard&Mouse", parts[i], 1),
                gamepadPrimary = Bind("Runner", "Move", "Gamepad", parts[i]),
                gamepadAlternate = Bind("Runner", "Move", "Gamepad", parts[i], 1)
            };
        yield return Simple("Jump", "Runner", "Jump");
        yield return Simple("Grab / release", "Runner", "Grab");
        yield return Simple("Electric wire", "Runner", "Wire");
        yield return new RowDefinition
        {
            label = "Position pointer",
            keyboardPrimary = Fixed("Mouse"),
            gamepadPrimary = Bind("Runner", "PointerMove", "Gamepad")
        };
    }

    private static IEnumerable<RowDefinition> DrawerDefinitions()
    {
        yield return Simple("Pointer / draw", "Drawer", "Draw");
        yield return Simple("Toggle eraser", "Drawer", "Erase");
        yield return new RowDefinition
        {
            label = "Next brush",
            keyboardPrimary = Fixed("Wheel Up"),
            gamepadPrimary = Bind("Drawer", "BrushNext", "Gamepad")
        };
        yield return new RowDefinition
        {
            label = "Previous brush",
            keyboardPrimary = Fixed("Wheel Down"),
            gamepadPrimary = Bind("Drawer", "BrushPrevious", "Gamepad")
        };
        yield return Simple("Pen panel", "Drawer", "PenPanel");
        yield return Simple("Free camera toggle", "Drawer", "CameraToggle");

        string[] parts = { "up", "down", "left", "right" };
        string[] labels = { "Camera Move\n  Up", "  Down", "  Left", "  Right" };
        for (int i = 0; i < parts.Length; i++)
            yield return new RowDefinition
            {
                label = labels[i],
                keyboardPrimary = Bind("Drawer", "CameraMove", "Keyboard&Mouse", parts[i]),
                gamepadPrimary = Bind("Drawer", "CameraMove", "Gamepad", parts[i])
            };
        yield return new RowDefinition
        {
            label = "Position pointer",
            keyboardPrimary = Fixed("Mouse"),
            gamepadPrimary = Bind("Drawer", "PointerMove", "Gamepad")
        };
    }

    private static RowDefinition Simple(string label, string map, string action) => new()
    {
        label = label,
        keyboardPrimary = Bind(map, action, "Keyboard&Mouse"),
        gamepadPrimary = Bind(map, action, "Gamepad")
    };
}
