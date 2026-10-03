using System;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent local settings overlay. The device that opens it exclusively owns its UI actions;
/// settings and device assignments stay local and are never written to Photon room state.
/// </summary>
public sealed class EgakuSettingsMenu : MonoBehaviour
{
    private enum EditKind
    {
        None, Resolution, ScreenMode, MasterVolume, MusicVolume, EffectsVolume,
        PointerSpeed, BrushCursorSize, RunnerCursorSize
    }

    private sealed class SettingsDraft
    {
        public float master;
        public float music;
        public float effects;
        public float pointerSpeed;
        public float brushCursorSize;
        public float runnerCursorSize;
        public EgakuSettings.OnlineDeviceMode onlineDevice;
        public int resolution;
        public FullScreenMode screenMode;
    }

    private static EgakuSettingsMenu instance;
    public static bool IsOpen => instance != null && instance.open;

    public static bool PointerIsOverOpenButton(Vector2 screenPosition)
    {
        return instance != null && !instance.open && instance.openButton != null &&
               RectTransformUtility.RectangleContainsScreenPoint(
                   instance.openButton.GetComponent<RectTransform>(), screenPosition);
    }

    private readonly List<Button> entries = new List<Button>();
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private readonly List<InputActionReference> uiReferences = new List<InputActionReference>();
    private readonly SettingsDraft draft = new SettingsDraft();
    private readonly SettingsDraft openingSnapshot = new SettingsDraft();

    private EventSystem suspendedEventSystem;
    private bool eventSystemWasEnabled;
    private EventSystem menuEventSystem;
    private InputSystemUIInputModule menuInputModule;
    private InputActionAsset temporaryUiActions;
    private InputDevice openingDevice;
    private InputDeviceRouter.Role? openingRole;
    private bool recoveryOnly;
    private bool awaitingDevice;
    private InputDevice pendingSwapDevice;
    private InputDeviceRouter.Role? recoveryTarget;
    private float deviceRequestAfter;

    // The Prefab owns every visual element. Runtime code only binds behavior, values, and navigation.
    [SerializeField] private GraphicRaycaster raycaster;
    [SerializeField] private GameObject openButton;
    [SerializeField] private GameObject shade;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text note;
    [SerializeField] private Button[] tabButtons;
    [SerializeField] private GameObject[] pageRoots;
    [SerializeField] private Button[] displayRows;
    [SerializeField] private Button[] audioRows;
    [SerializeField] private Button[] controlRows;
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown modeDropdown;
    [SerializeField] private Slider[] audioSliders;
    [SerializeField] private Slider[] controlSliders;
    [SerializeField] private Button keysRoleButton;
    [SerializeField] private GameObject runnerKeys;
    [SerializeField] private GameObject drawerKeys;
    [SerializeField] private KeyRebindController keyRebindController;
    [SerializeField] private Button backButton;
    [SerializeField] private Button applyButton;
    [SerializeField] private Button restoreDefaultButton;
    [SerializeField] private Color activeTabColor = new Color(0.23f, 0.56f, 0.54f, 1f);
    [SerializeField] private Color inactiveTabColor = new Color(0.16f, 0.22f, 0.29f, 1f);

    private int page;
    private int roleForKeys;
    private bool keyPreviewDrawer;
    private bool open;
    private bool openedByGamepad;
    private bool gameplayScene;
    private bool locallyPaused;
    private float previousTimeScale;
    private float ignoreInputUntil;
    private bool refreshingWidgets;
    private bool draftCommitted;

    private EditKind editing;
    private Button editingRow;
    private Dropdown editingDropdown;
    private Slider editingSlider;
    private int editOriginalInt;
    private float editOriginalFloat;
    private bool finishDropdownAfterFrame;
    private bool editConfirmArmed;

    private bool displayPreview;
    private int oldWidth;
    private int oldHeight;
    private FullScreenMode oldMode;
    private float previewDeadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        EgakuSettingsMenu prefab = Resources.Load<EgakuSettingsMenu>("UI/EgakuSettingsMenu");
        if (prefab == null)
        {
            Debug.LogError("Egaku settings Prefab is missing from Resources/UI/EgakuSettingsMenu.");
            return;
        }
        EgakuSettingsMenu menu = Instantiate(prefab);
        DontDestroyOnLoad(menu.gameObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.activeSceneChanged += OnSceneChanged;
        InputDeviceRouter.DeviceLost += OnLocalDeviceLost;
        InputDeviceRouter.AssignmentsChanged += OnAssignmentsChanged;
        menuEventSystem = GetComponentInChildren<EventSystem>(true);
        menuInputModule = menuEventSystem != null ? menuEventSystem.GetComponent<InputSystemUIInputModule>() : null;
        if (keyRebindController != null)
            keyRebindController.Initialize(menuEventSystem, note,
                enabled => { if (menuInputModule != null) menuInputModule.enabled = enabled; });
        if (menuEventSystem != null) menuEventSystem.enabled = false;
        if (restoreDefaultButton != null) restoreDefaultButton.interactable = false;
        ConfigureStaticNavigation();
        shade.SetActive(false);
        BuildResolutionList();
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
        InputDeviceRouter.DeviceLost -= OnLocalDeviceLost;
        InputDeviceRouter.AssignmentsChanged -= OnAssignmentsChanged;
        if (keyRebindController != null) keyRebindController.EndSession(false);
        if (instance == this)
        {
            Close(true);
            instance = null;
        }
    }

    private void OnSceneChanged(Scene previous, Scene next) => Close(true);

    private void Update()
    {
        if (!open)
        {
            if (Allan.GameManager.InteractionsPausedForRecovery) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Open(Keyboard.current);
            else
                foreach (Gamepad pad in Gamepad.all)
                    if (pad.startButton.wasPressedThisFrame)
                    {
                        Open(pad);
                        break;
                    }
            return;
        }

        if (displayPreview)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(previewDeadline - Time.unscaledTime));
            note.text = "Keep these display settings? " + seconds + " s  |  Confirm: keep  |  Cancel: restore";
            if (seconds == 0)
            {
                RestoreDisplayPreview();
                return;
            }
        }
        if (Time.unscaledTime < ignoreInputUntil) return;

        if (keyRebindController != null && keyRebindController.BlocksMenuInput)
        {
            keyRebindController.Tick();
            return;
        }

        if (recoveryOnly)
        {
            HandleRecoveryClaim();
            return;
        }
        if (awaitingDevice)
        {
            HandleDeviceRequest();
            if (CancelPressed())
            {
                awaitingDevice = false;
                pendingSwapDevice = null;
                BuildPage(true);
            }
            return;
        }
        if (displayPreview)
        {
            if (ConfirmPressed()) KeepDisplayAndApply();
            else if (CancelPressed()) RestoreDisplayPreview();
            return;
        }

        if (PreviousPagePressed())
        {
            if (editing != EditKind.None) CancelEdit();
            ChangePage(-1);
            return;
        }
        if (NextPagePressed())
        {
            if (editing != EditKind.None) CancelEdit();
            ChangePage(1);
            return;
        }

        if (editing != EditKind.None)
        {
            if (CancelPressed())
            {
                CancelEdit();
                return;
            }

            // The Submit that opens a row can reach this component in the same frame as
            // EventSystem dispatch, depending on script order. Require a physical release
            // before accepting the next Submit so one press cannot both enter and exit edit.
            if (!editConfirmArmed)
            {
                if (!ConfirmIsHeld()) editConfirmArmed = true;
                return;
            }
            if (IsSliderEdit(editing) && ConfirmPressed())
            {
                FinishEdit();
                return;
            }
            // Let Dropdown process Submit first, then restore focus after its Toggle updates.
            if (IsDropdownEdit(editing) && ConfirmPressed())
            {
                if (KeyboardSpacePressed()) SubmitSelectedObject();
                finishDropdownAfterFrame = true;
            }
            return;
        }

        // Input System's generic Submit usage covers Enter and gamepad South; add Space explicitly.
        if (KeyboardSpacePressed())
        {
            SubmitSelectedObject();
            return;
        }
        if (CancelPressed()) Close();
    }

    private void LateUpdate()
    {
        if (!open) return;
        if (IsDropdownEdit(editing) && finishDropdownAfterFrame)
        {
            finishDropdownAfterFrame = false;
            FinishEdit();
            return;
        }
        if (IsDropdownEdit(editing) || page == 3)
            EnsureSelectedDropdownItemVisible();
    }

    private void Open(InputDevice device)
    {
        if (open || device == null || menuEventSystem == null || menuInputModule == null) return;
        open = true;
        openingDevice = device;
        openedByGamepad = device is Gamepad;
        gameplayScene = SceneManager.GetActiveScene().name.StartsWith("Level_", StringComparison.Ordinal);
        openingRole = PhotonNetwork.OfflineMode && Allan.GameManager.IsLocalMultiplayer
            ? InputDeviceRouter.OwnerOf(device) : null;
        recoveryOnly = gameplayScene && PhotonNetwork.OfflineMode && Allan.GameManager.IsLocalMultiplayer &&
            !openingRole.HasValue;
        awaitingDevice = false;
        pendingSwapDevice = null;
        roleForKeys = DetermineVisibleRole(openingRole);
        page = recoveryOnly ? 2 : 0;
        editing = EditKind.None;
        displayPreview = false;
        draftCommitted = false;
        BuildResolutionList();
        CaptureDraft();
        if (keyRebindController != null) keyRebindController.BeginSession();

        if (gameplayScene)
            foreach (Drawer drawer in FindObjectsByType<Drawer>())
                if (drawer.photonView.IsMine) drawer.FinishStrokeForSettings();
        if (gameplayScene && PhotonNetwork.OfflineMode)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            locallyPaused = true;
        }

        suspendedEventSystem = EventSystem.current;
        if (suspendedEventSystem != null)
        {
            // Disabling the whole scene EventSystem clears its retained pointer press as well.
            eventSystemWasEnabled = suspendedEventSystem.enabled;
            suspendedEventSystem.enabled = false;
        }
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        temporaryUiActions = openingRole.HasValue ? null : InputDeviceRouter.CreateTemporaryActions(device);
        BindMenuActions(openingRole.HasValue
            ? InputDeviceRouter.ActionsFor(openingRole.Value) : temporaryUiActions);
        shade.SetActive(true);
        openButton.SetActive(false);
        BuildPage(false);
        menuEventSystem.enabled = true;
        SelectFirstEntry();
        ignoreInputUntil = Time.unscaledTime + 0.15f;
    }

    private void CaptureDraft()
    {
        int currentResolution = FindResolution(Screen.width, Screen.height);
        draft.master = openingSnapshot.master = EgakuSettings.MasterVolume;
        draft.music = openingSnapshot.music = EgakuSettings.MusicVolume;
        draft.effects = openingSnapshot.effects = EgakuSettings.EffectsVolume;
        draft.pointerSpeed = openingSnapshot.pointerSpeed = EgakuSettings.GamepadPointerSpeed;
        draft.brushCursorSize = openingSnapshot.brushCursorSize = EgakuSettings.DrawerBrushCursorScale;
        draft.runnerCursorSize = openingSnapshot.runnerCursorSize = EgakuSettings.RunnerIndicatorScale;
        draft.onlineDevice = openingSnapshot.onlineDevice = EgakuSettings.OnlineDevice;
        draft.resolution = openingSnapshot.resolution = currentResolution;
        draft.screenMode = openingSnapshot.screenMode = Screen.fullScreenMode;
    }

    private int DetermineVisibleRole(InputDeviceRouter.Role? localRole)
    {
        if (!gameplayScene) return 0;
        if (PhotonNetwork.OfflineMode)
            return localRole == InputDeviceRouter.Role.Runner ? 1
                : localRole == InputDeviceRouter.Role.Drawer ? 2 : 0;
        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Role", out object value) && value is int role)
        {
            if (role == (int)RolesManager.PlayerRole.Drawer) return 2;
            if (role == (int)RolesManager.PlayerRole.Runner) return 1;
        }
        foreach (Drawer drawer in FindObjectsByType<Drawer>())
            if (drawer.photonView.IsMine) return 2;
        return 1;
    }

    private void Close(bool force = false)
    {
        if (!open) return;
        if (!force && gameplayScene && PhotonNetwork.OfflineMode && Allan.GameManager.IsLocalMultiplayer &&
            !InputDeviceRouter.BothClaimed)
        {
            recoveryOnly = true;
            page = 2;
            BuildPage(true);
            return;
        }

        if (displayPreview)
        {
            Screen.SetResolution(oldWidth, oldHeight, oldMode);
            displayPreview = false;
        }
        if (!draftCommitted)
        {
            // Live previews never survive a Back/Escape close without Apply.
            EgakuSettings.PreviewVolumes(openingSnapshot.master, openingSnapshot.music, openingSnapshot.effects);
            EgakuSettings.PreviewControls(openingSnapshot.pointerSpeed,
                openingSnapshot.brushCursorSize, openingSnapshot.runnerCursorSize);
        }
        if (keyRebindController != null) keyRebindController.EndSession(false);

        open = false;
        editing = EditKind.None;
        if (menuEventSystem != null) menuEventSystem.enabled = false;
        if (menuInputModule != null) menuInputModule.actionsAsset = null;
        foreach (InputActionReference reference in uiReferences) Destroy(reference);
        uiReferences.Clear();
        if (temporaryUiActions != null) Destroy(temporaryUiActions);
        temporaryUiActions = null;
        openingDevice = null;
        openingRole = null;
        recoveryOnly = false;
        recoveryTarget = null;
        awaitingDevice = false;
        pendingSwapDevice = null;
        if (suspendedEventSystem != null) suspendedEventSystem.enabled = eventSystemWasEnabled;
        suspendedEventSystem = null;
        if (locallyPaused)
        {
            Time.timeScale = previousTimeScale;
            locallyPaused = false;
        }
        if (shade != null) shade.SetActive(false);
        if (openButton != null) openButton.SetActive(true);
        foreach (Drawer drawer in FindObjectsByType<Drawer>())
            if (drawer.photonView.IsMine) drawer.RefreshCursorForSettings();
    }

    private void BindMenuActions(InputActionAsset actions)
    {
        // Each reference targets the role's exact device-filtered action copy.
        menuInputModule.actionsAsset = actions;
        InputActionReference Ref(string name)
        {
            InputActionReference reference = InputActionReference.Create(actions.FindAction("UI/" + name));
            uiReferences.Add(reference);
            return reference;
        }
        menuInputModule.point = Ref("Point");
        menuInputModule.leftClick = Ref("Click");
        menuInputModule.rightClick = Ref("RightClick");
        menuInputModule.middleClick = Ref("MiddleClick");
        menuInputModule.scrollWheel = Ref("ScrollWheel");
        menuInputModule.move = Ref("Navigate");
        menuInputModule.submit = Ref("Submit");
        menuInputModule.cancel = Ref("Cancel");
    }

    private bool ConfirmPressed()
    {
        if (openingDevice is Gamepad pad) return pad.added && pad.buttonSouth.wasPressedThisFrame;
        return Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
    }

    private bool CancelPressed()
    {
        if (openingDevice is Gamepad pad)
            return pad.added && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private bool ConfirmIsHeld()
    {
        if (openingDevice is Gamepad pad) return pad.added && pad.buttonSouth.isPressed;
        return Keyboard.current != null && (Keyboard.current.enterKey.isPressed ||
            Keyboard.current.numpadEnterKey.isPressed || Keyboard.current.spaceKey.isPressed);
    }

    private bool PreviousPagePressed()
    {
        if (openingDevice is Gamepad pad) return pad.added && pad.leftShoulder.wasPressedThisFrame;
        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
    }

    private bool NextPagePressed()
    {
        if (openingDevice is Gamepad pad) return pad.added && pad.rightShoulder.wasPressedThisFrame;
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private bool KeyboardSpacePressed()
    {
        return openingDevice is not Gamepad && Keyboard.current != null &&
               Keyboard.current.spaceKey.wasPressedThisFrame;
    }

    private void SubmitSelectedObject()
    {
        if (menuEventSystem == null || menuEventSystem.currentSelectedGameObject == null) return;
        ExecuteEvents.Execute(menuEventSystem.currentSelectedGameObject,
            new BaseEventData(menuEventSystem), ExecuteEvents.submitHandler);
    }

    private void ChangePage(int direction)
    {
        page = (page + direction + pageRoots.Length) % pageRoots.Length;
        BuildPage(true);
    }

    private void ShowPage(int target)
    {
        if (recoveryOnly || target < 0 || target >= pageRoots.Length) return;
        if (editing != EditKind.None) CancelEdit();
        page = target;
        BuildPage(true);
    }

    private void BuildPage(bool selectFirst)
    {
        entries.Clear();
        string[] pages = { "Display", "Audio", "Controls", "Keys" };
        title.text = recoveryOnly ? "Device recovery" : "Settings  /  " + pages[page] +
            (roleForKeys == 1 ? "  |  Runner" : roleForKeys == 2 ? "  |  Drawer" : "");
        for (int i = 0; i < pages.Length; i++)
        {
            pageRoots[i].SetActive(i == page);
            tabButtons[i].gameObject.SetActive(!recoveryOnly || i == 2);
        }
        RefreshTabVisuals();

        if (recoveryOnly)
        {
            foreach (Button row in controlRows) row.gameObject.SetActive(false);
            applyButton.gameObject.SetActive(false);
            restoreDefaultButton.gameObject.SetActive(false);
            note.text = "Missing controller: press A on a free pad, or Enter on an unassigned keyboard.";
        }
        else
        {
            applyButton.gameObject.SetActive(true);
            restoreDefaultButton.gameObject.SetActive(true);
            restoreDefaultButton.interactable = page == 3;
            if (page == 3)
                SetButtonCaption(restoreDefaultButton, "Restore current role bindings to default");
            switch (page)
            {
                case 0: BuildDisplayPage(); break;
                case 1: BuildAudioPage(); break;
                case 2: BuildControlsPage(); break;
                case 3: BuildKeysPage(); break;
            }
            if (restoreDefaultButton.interactable) AddEntry(restoreDefaultButton);
            AddEntry(applyButton);
            SetButtonCaption(applyButton, displayPreview ? "Keep Display Settings" : "Apply Settings");
            UpdateHelpText();
        }
        backButton.interactable = !recoveryOnly || InputDeviceRouter.BothClaimed;
        ConfigureEntryNavigation();
        if (selectFirst) SelectFirstEntry();
    }

    private void BuildDisplayPage()
    {
        SetButtonCaption(displayRows[0], "Resolution");
        SetButtonCaption(displayRows[1], "Screen mode");
        AddEntry(displayRows[0]);
        AddEntry(displayRows[1]);
        refreshingWidgets = true;
        resolutionDropdown.ClearOptions();
        List<string> choices = new List<string>();
        foreach (Vector2Int size in resolutions) choices.Add(size.x + " x " + size.y);
        resolutionDropdown.AddOptions(choices);
        resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(draft.resolution, 0, resolutions.Count - 1));
        modeDropdown.SetValueWithoutNotify(ModeIndex(draft.screenMode));
        refreshingWidgets = false;
    }

    private void BuildAudioPage()
    {
        RefreshAudioCaptions();
        foreach (Button row in audioRows) AddEntry(row);
        refreshingWidgets = true;
        audioSliders[0].SetValueWithoutNotify(draft.master);
        audioSliders[1].SetValueWithoutNotify(draft.music);
        audioSliders[2].SetValueWithoutNotify(draft.effects);
        refreshingWidgets = false;
    }

    private void BuildControlsPage()
    {
        foreach (Button row in controlRows) row.gameObject.SetActive(true);
        if (PhotonNetwork.OfflineMode)
            SetButtonCaption(controlRows[0], "Change my input device  |  " +
                (openingRole.HasValue ? InputDeviceRouter.Kind(openingRole.Value).ToString() : "unassigned"));
        else
            SetButtonCaption(controlRows[0], "Online input     " + draft.onlineDevice);
        RefreshControlCaptions();

        bool showDrawerControls = roleForKeys != 1;
        controlRows[2].gameObject.SetActive(showDrawerControls);
        controlRows[3].gameObject.SetActive(showDrawerControls);
        AddEntry(controlRows[0]);
        AddEntry(controlRows[1]);
        if (showDrawerControls)
        {
            AddEntry(controlRows[2]);
            AddEntry(controlRows[3]);
        }

        refreshingWidgets = true;
        controlSliders[0].SetValueWithoutNotify(draft.pointerSpeed);
        controlSliders[1].SetValueWithoutNotify(draft.brushCursorSize);
        controlSliders[2].SetValueWithoutNotify(draft.runnerCursorSize);
        refreshingWidgets = false;
    }

    private void BuildKeysPage()
    {
        if (keysRoleButton != null) keysRoleButton.gameObject.SetActive(roleForKeys == 0);
        if (roleForKeys == 0)
        {
            SetButtonCaption(keysRoleButton,
                "Showing: " + (keyPreviewDrawer ? "Drawer" : "Runner") + "  |  select to switch");
            AddEntry(keysRoleButton);
        }
        bool drawer = roleForKeys == 2 || (roleForKeys == 0 && keyPreviewDrawer);
        runnerKeys.SetActive(!drawer);
        drawerKeys.SetActive(drawer);
        if (keyRebindController != null)
        {
            keyRebindController.ShowRole(drawer);
            foreach (Button bindingButton in keyRebindController.VisibleButtons) AddEntry(bindingButton);
        }
    }

    private void AddEntry(Button button)
    {
        if (button != null && button.gameObject.activeInHierarchy && button.interactable) entries.Add(button);
    }

    private void ConfigureStaticNavigation()
    {
        Navigation none = new Navigation { mode = Navigation.Mode.None };
        foreach (Button tab in tabButtons) if (tab != null) tab.navigation = none;
        if (backButton != null) backButton.navigation = none;
        if (restoreDefaultButton != null) restoreDefaultButton.navigation = none;
        if (resolutionDropdown != null) resolutionDropdown.navigation = none;
        if (modeDropdown != null) modeDropdown.navigation = none;
        foreach (Slider slider in audioSliders) if (slider != null) slider.navigation = none;
        foreach (Slider slider in controlSliders) if (slider != null) slider.navigation = none;
    }

    private void ConfigureEntryNavigation()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Navigation navigation = entries[i].navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = entries[(i - 1 + entries.Count) % entries.Count];
            navigation.selectOnDown = entries[(i + 1) % entries.Count];
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            entries[i].navigation = navigation;
        }

        if (page == 3 && !recoveryOnly && keyRebindController != null)
        {
            // Keys is a two-dimensional table. Keep the generic navigation for the
            // role/apply/restore rows, then give each binding explicit row and column links.
            Button above = roleForKeys == 0 && keysRoleButton != null && keysRoleButton.gameObject.activeInHierarchy
                ? keysRoleButton : applyButton;
            Button below = restoreDefaultButton != null && restoreDefaultButton.interactable
                ? restoreDefaultButton : applyButton;
            keyRebindController.ConfigureGridNavigation(above, below);
        }
    }

    private void SelectFirstEntry()
    {
        if (menuEventSystem != null)
            menuEventSystem.SetSelectedGameObject(entries.Count > 0 ? entries[0].gameObject : null);
    }

    private void UpdateHelpText()
    {
        if (displayPreview || recoveryOnly || awaitingDevice) return;
        if (page == 3)
        {
            note.text = openedByGamepad
                ? "D-pad/stick: navigate  |  A: rebind  |  LB/RB: page  |  capture waits 10 seconds"
                : "Arrow keys: navigate  |  Enter/Space: rebind  |  Q/E: page  |  capture waits 10 seconds";
            return;
        }
        note.text = openedByGamepad
            ? "D-pad or stick: choose  |  A: edit/confirm  |  B: cancel  |  LB/RB: page"
            : "Arrows: choose/change  |  Enter/Space: edit/confirm  |  Esc: cancel  |  Q/E: page";
    }

    private void RefreshTabVisuals()
    {
        // EventSystem focus moves into the page content, so the active tab needs a separate
        // persistent visual state that remains visible while a setting row is selected.
        for (int i = 0; i < tabButtons.Length; i++)
            if (tabButtons[i] != null && tabButtons[i].image != null)
                tabButtons[i].image.color = i == page ? activeTabColor : inactiveTabColor;
    }

    private static void SetButtonCaption(Button button, string caption)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "   " + caption;
    }

    private void RefreshAudioCaptions()
    {
        SetButtonCaption(audioRows[0], "Master volume     " + Percent(draft.master));
        SetButtonCaption(audioRows[1], "BGM volume        " + Percent(draft.music));
        SetButtonCaption(audioRows[2], "SFX volume        " + Percent(draft.effects));
    }

    private void RefreshControlCaptions()
    {
        SetButtonCaption(controlRows[1], "Gamepad pointer speed     " + Mathf.RoundToInt(draft.pointerSpeed) + " px/s");
        SetButtonCaption(controlRows[2], "Drawer brush cursor     " + Percent(draft.brushCursorSize));
        SetButtonCaption(controlRows[3], "Runner position cursor     " + Percent(draft.runnerCursorSize));
    }

    private void BeginDropdownEdit(EditKind kind, Button row, Dropdown dropdown)
    {
        if (!open || recoveryOnly || editing != EditKind.None || dropdown == null) return;
        editing = kind;
        editingRow = row;
        editingDropdown = dropdown;
        editOriginalInt = dropdown.value;
        editConfirmArmed = false;
        menuEventSystem.SetSelectedGameObject(dropdown.gameObject);
        dropdown.Show();
    }

    private void BeginSliderEdit(EditKind kind, Button row, Slider slider)
    {
        if (!open || recoveryOnly || editing != EditKind.None || slider == null) return;
        editing = kind;
        editingRow = row;
        editingSlider = slider;
        editOriginalFloat = slider.value;
        editConfirmArmed = false;
        menuEventSystem.SetSelectedGameObject(slider.gameObject);
    }

    private static bool IsDropdownEdit(EditKind kind) =>
        kind == EditKind.Resolution || kind == EditKind.ScreenMode;

    private static bool IsSliderEdit(EditKind kind) =>
        kind >= EditKind.MasterVolume && kind <= EditKind.RunnerCursorSize;

    private void FinishEdit()
    {
        if (editingDropdown != null) editingDropdown.Hide();
        Button returnTo = editingRow;
        editing = EditKind.None;
        editingRow = null;
        editingDropdown = null;
        editingSlider = null;
        finishDropdownAfterFrame = false;
        editConfirmArmed = false;
        if (returnTo != null && returnTo.gameObject.activeInHierarchy)
            menuEventSystem.SetSelectedGameObject(returnTo.gameObject);
    }

    private void EnsureSelectedDropdownItemVisible()
    {
        GameObject selected = menuEventSystem != null ? menuEventSystem.currentSelectedGameObject : null;
        if (selected == null) return;
        ScrollRect scroll = selected.GetComponentInParent<ScrollRect>();
        RectTransform item = selected.transform as RectTransform;
        if (scroll == null || scroll.content == null || item == null || !item.IsChildOf(scroll.content)) return;

        RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
        if (viewport == null) return;
        Canvas.ForceUpdateCanvases();
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, item);
        Rect view = viewport.rect;
        Vector2 position = scroll.content.anchoredPosition;
        if (bounds.min.y < view.yMin)
            position.y += view.yMin - bounds.min.y;
        else if (bounds.max.y > view.yMax)
            position.y -= bounds.max.y - view.yMax;
        scroll.content.anchoredPosition = position;
        scroll.velocity = Vector2.zero;
    }

    private void CancelEdit()
    {
        switch (editing)
        {
            case EditKind.Resolution:
                draft.resolution = editOriginalInt;
                resolutionDropdown.SetValueWithoutNotify(editOriginalInt);
                break;
            case EditKind.ScreenMode:
                draft.screenMode = ModeFromIndex(editOriginalInt);
                modeDropdown.SetValueWithoutNotify(editOriginalInt);
                break;
            case EditKind.MasterVolume:
                draft.master = editOriginalFloat;
                audioSliders[0].SetValueWithoutNotify(editOriginalFloat);
                PreviewAudio();
                break;
            case EditKind.MusicVolume:
                draft.music = editOriginalFloat;
                audioSliders[1].SetValueWithoutNotify(editOriginalFloat);
                PreviewAudio();
                break;
            case EditKind.EffectsVolume:
                draft.effects = editOriginalFloat;
                audioSliders[2].SetValueWithoutNotify(editOriginalFloat);
                PreviewAudio();
                break;
            case EditKind.PointerSpeed:
                draft.pointerSpeed = editOriginalFloat;
                controlSliders[0].SetValueWithoutNotify(editOriginalFloat);
                PreviewControls();
                break;
            case EditKind.BrushCursorSize:
                draft.brushCursorSize = editOriginalFloat;
                controlSliders[1].SetValueWithoutNotify(editOriginalFloat);
                PreviewControls();
                break;
            case EditKind.RunnerCursorSize:
                draft.runnerCursorSize = editOriginalFloat;
                controlSliders[2].SetValueWithoutNotify(editOriginalFloat);
                PreviewControls();
                break;
        }
        RefreshAudioCaptions();
        if (page == 2) RefreshControlCaptions();
        FinishEdit();
    }

    private void PreviewAudio() =>
        EgakuSettings.PreviewVolumes(draft.master, draft.music, draft.effects);

    private void PreviewControls() =>
        EgakuSettings.PreviewControls(draft.pointerSpeed, draft.brushCursorSize, draft.runnerCursorSize);

    public void OnResolutionDropdown(int index)
    {
        if (refreshingWidgets || index < 0 || index >= resolutions.Count) return;
        draft.resolution = index;
        if (editing == EditKind.Resolution) finishDropdownAfterFrame = true;
    }

    public void OnModeDropdown(int index)
    {
        if (refreshingWidgets) return;
        draft.screenMode = ModeFromIndex(index);
        if (editing == EditKind.ScreenMode) finishDropdownAfterFrame = true;
    }

    public void OnMasterVolume(float value)
    {
        if (refreshingWidgets) return;
        draft.master = value;
        PreviewAudio();
        RefreshAudioCaptions();
    }

    public void OnMusicVolume(float value)
    {
        if (refreshingWidgets) return;
        draft.music = value;
        PreviewAudio();
        RefreshAudioCaptions();
    }

    public void OnEffectsVolume(float value)
    {
        if (refreshingWidgets) return;
        draft.effects = value;
        PreviewAudio();
        RefreshAudioCaptions();
    }

    public void OnPointerSpeed(float value)
    {
        if (refreshingWidgets) return;
        draft.pointerSpeed = value;
        PreviewControls();
        RefreshControlCaptions();
    }

    public void OnBrushCursorSize(float value)
    {
        if (refreshingWidgets) return;
        draft.brushCursorSize = value;
        PreviewControls();
        RefreshControlCaptions();
    }

    public void OnRunnerCursorSize(float value)
    {
        if (refreshingWidgets) return;
        draft.runnerCursorSize = value;
        PreviewControls();
        RefreshControlCaptions();
    }

    private void ToggleLocalDevices()
    {
        if (!openingRole.HasValue) return;
        awaitingDevice = true;
        pendingSwapDevice = null;
        deviceRequestAfter = Time.unscaledTime + 0.2f;
        note.text = "Press Enter or A on the device you want to use.";
    }

    private void HandleDeviceRequest()
    {
        if (!openingRole.HasValue || Time.unscaledTime < deviceRequestAfter) return;
        if (pendingSwapDevice != null)
        {
            bool accepted = pendingSwapDevice is Gamepad waitingPad
                ? waitingPad.buttonSouth.wasPressedThisFrame
                : Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
            bool rejected = pendingSwapDevice is Gamepad rejectingPad
                ? rejectingPad.buttonEast.wasPressedThisFrame
                : Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (accepted && InputDeviceRouter.TrySwap(openingRole.Value, pendingSwapDevice, pendingSwapDevice)) Close();
            else if (rejected)
            {
                pendingSwapDevice = null;
                note.text = "Swap declined. Choose another device.";
            }
            return;
        }
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
            RequestDevice(Keyboard.current);
        foreach (Gamepad pad in Gamepad.all)
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                RequestDevice(pad);
                break;
            }
    }

    private void RequestDevice(InputDevice device)
    {
        InputDeviceRouter.Role? owner = InputDeviceRouter.OwnerOf(device);
        if (owner == openingRole) return;
        if (!owner.HasValue)
        {
            if (InputDeviceRouter.TryClaim(openingRole.Value, device)) Close();
            return;
        }
        // The other role must confirm on that same physical device before an atomic swap.
        pendingSwapDevice = device;
        deviceRequestAfter = Time.unscaledTime + 0.2f;
        note.text = "Other player: A / Enter to accept swap, B / Esc to reject.";
    }

    private void HandleRecoveryClaim()
    {
        InputDeviceRouter.Role? missing = recoveryTarget;
        foreach (InputDeviceRouter.Role role in new[] { InputDeviceRouter.Role.Runner, InputDeviceRouter.Role.Drawer })
            if (!missing.HasValue && InputDeviceRouter.Kind(role) == InputDeviceRouter.DeviceKind.Gamepad &&
                InputDeviceRouter.GamepadFor(role) == null)
            {
                missing = role;
                break;
            }
        if (!missing.HasValue) return;
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame &&
            InputDeviceRouter.OwnerOf(Keyboard.current) == null && InputDeviceRouter.TryClaim(missing.Value, Keyboard.current))
            Close();
        foreach (Gamepad pad in Gamepad.all)
            if (pad.buttonSouth.wasPressedThisFrame && InputDeviceRouter.OwnerOf(pad) == null &&
                InputDeviceRouter.TryClaim(missing.Value, pad))
            {
                Close();
                break;
            }
    }

    private void OnLocalDeviceLost(InputDeviceRouter.Role role)
    {
        if (!PhotonNetwork.OfflineMode || !Allan.GameManager.IsLocalMultiplayer) return;
        if (InputDeviceRouter.IsReady(role))
        {
            if (open && openingRole == role) Close(true);
            return;
        }
        if (open && openingDevice != null && openingDevice.added)
        {
            recoveryTarget = role;
            recoveryOnly = true;
            page = 2;
            BuildPage(true);
            return;
        }
        InputDevice fallback = Keyboard.current != null ? (InputDevice)Keyboard.current
            : GameplayInput.PadFor(role != InputDeviceRouter.Role.Runner);
        if (open) Close(true);
        Open(fallback);
        if (open)
        {
            recoveryTarget = role;
            recoveryOnly = true;
            page = 2;
            BuildPage(true);
        }
    }

    private void OnAssignmentsChanged()
    {
        if (open && recoveryOnly && InputDeviceRouter.BothClaimed) Close();
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (size.x >= 640 && size.y >= 480 && !resolutions.Contains(size)) resolutions.Add(size);
        }
        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x * a.y == b.x * b.y
            ? a.x.CompareTo(b.x) : (a.x * a.y).CompareTo(b.x * b.y));
    }

    private int FindResolution(int width, int height)
    {
        int index = resolutions.IndexOf(new Vector2Int(width, height));
        return Mathf.Max(0, index);
    }

    private static int ModeIndex(FullScreenMode mode) =>
        mode == FullScreenMode.Windowed ? 0 : mode == FullScreenMode.FullScreenWindow ? 1 : 2;

    private static FullScreenMode ModeFromIndex(int index)
    {
        FullScreenMode[] modes =
        {
            FullScreenMode.Windowed,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.ExclusiveFullScreen
        };
        return modes[Mathf.Clamp(index, 0, modes.Length - 1)];
    }

    private void StartDisplayPreview()
    {
        Vector2Int size = resolutions[Mathf.Clamp(draft.resolution, 0, resolutions.Count - 1)];
        bool changed = size.x != Screen.width || size.y != Screen.height || draft.screenMode != Screen.fullScreenMode;
        if (!changed)
        {
            CommitDraftAndClose();
            return;
        }
        oldWidth = Screen.width;
        oldHeight = Screen.height;
        oldMode = Screen.fullScreenMode;
        Screen.SetResolution(size.x, size.y, draft.screenMode);
        displayPreview = true;
        previewDeadline = Time.unscaledTime + 15f;
        SetButtonCaption(applyButton, "Keep Display Settings");
        menuEventSystem.SetSelectedGameObject(applyButton.gameObject);
    }

    private void KeepDisplayAndApply()
    {
        if (!displayPreview) return;
        displayPreview = false;
        CommitDraftAndClose();
    }

    private void RestoreDisplayPreview()
    {
        if (!displayPreview) return;
        displayPreview = false;
        Screen.SetResolution(oldWidth, oldHeight, oldMode);
        BuildResolutionList();
        draft.resolution = FindResolution(oldWidth, oldHeight);
        draft.screenMode = oldMode;
        BuildPage(true);
    }

    private void CommitDraftAndClose()
    {
        Vector2Int size = resolutions[Mathf.Clamp(draft.resolution, 0, resolutions.Count - 1)];
        EgakuSettings.ApplyAll(draft.master, draft.music, draft.effects, draft.pointerSpeed,
            draft.brushCursorSize, draft.runnerCursorSize, draft.onlineDevice,
            size.x, size.y, draft.screenMode);
        if (keyRebindController != null) keyRebindController.EndSession(true);
        draftCommitted = true;
        Close();
    }

    private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

    // Persistent UnityEvents authored in the settings Prefab.
    public void OpenFromButton() => Open(GamepadDrawerPointer.CapturesDraw
        ? (InputDevice)GameplayInput.PadFor(false) : Mouse.current);
    public void ShowDisplay() => ShowPage(0);
    public void ShowAudio() => ShowPage(1);
    public void ShowControls() => ShowPage(2);
    public void ShowKeys() => ShowPage(3);
    public void EditResolution() => BeginDropdownEdit(EditKind.Resolution, displayRows[0], resolutionDropdown);
    public void EditScreenMode() => BeginDropdownEdit(EditKind.ScreenMode, displayRows[1], modeDropdown);
    public void EditMasterVolume() => BeginSliderEdit(EditKind.MasterVolume, audioRows[0], audioSliders[0]);
    public void EditMusicVolume() => BeginSliderEdit(EditKind.MusicVolume, audioRows[1], audioSliders[1]);
    public void EditEffectsVolume() => BeginSliderEdit(EditKind.EffectsVolume, audioRows[2], audioSliders[2]);
    public void EditPointerSpeed() => BeginSliderEdit(EditKind.PointerSpeed, controlRows[1], controlSliders[0]);
    public void EditBrushCursorSize() => BeginSliderEdit(EditKind.BrushCursorSize, controlRows[2], controlSliders[1]);
    public void EditRunnerCursorSize() => BeginSliderEdit(EditKind.RunnerCursorSize, controlRows[3], controlSliders[2]);
    public void ApplySettings()
    {
        // During the display safety prompt the same authored button becomes the mouse
        // confirmation control, so it must commit instead of starting another preview.
        if (displayPreview)
        {
            KeepDisplayAndApply();
            return;
        }
        if (editing != EditKind.None) FinishEdit();
        StartDisplayPreview();
    }
    public void OnDeviceRow()
    {
        if (PhotonNetwork.OfflineMode) ToggleLocalDevices();
        else
        {
            draft.onlineDevice = (EgakuSettings.OnlineDeviceMode)(((int)draft.onlineDevice + 1) % 3);
            SetButtonCaption(controlRows[0], "Online input     " + draft.onlineDevice);
        }
    }
    public void ShowOtherKeys()
    {
        keyPreviewDrawer = !keyPreviewDrawer;
        BuildPage(true);
    }
    public void RestoreCurrentPageToDefault()
    {
        if (page != 3 || keyRebindController == null) return;
        keyRebindController.ResetVisibleRole();
    }
    public void CloseFromButton() => Close();
}
