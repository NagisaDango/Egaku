using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Presents the online role lobby from Photon room state. Each client only drives
/// its own card; role ownership, readiness, and the next-screen phase live in room properties.
/// </summary>
public sealed class OnlineRoleSelectionView : MonoBehaviourPunCallbacks
{
    private sealed class PlayerCard
    {
        public Player player;
        public RectTransform rect;
        public DisplayInRoleselect appearance;
    }

    [Header("Columns")]
    [SerializeField] private RectTransform runnerColumn;
    [SerializeField] private RectTransform neutralColumn;
    [SerializeField] private RectTransform drawerColumn;
    [SerializeField] private TMP_Text runnerReadyText;
    [SerializeField] private TMP_Text drawerReadyText;
    [SerializeField] private Image runnerReadyImage;
    [SerializeField] private Image drawerReadyImage;

    [Header("Cards")]
    [SerializeField] private GameObject playerCardPrefab;
    [SerializeField, Min(1f)] private float cardMoveSpeed = 14f;
    [SerializeField] private float cardTopOffset = 55f;
    [SerializeField, Min(1f)] private float cardRowSpacing = 110f;

    [Header("Navigation")]
    [SerializeField] private Button runnerButton;
    [SerializeField] private Button drawerButton;
    [SerializeField] private Button leaveRoomButton;

    private readonly Dictionary<int, PlayerCard> cards = new();
    private RolesManager.PlayerRole pendingRole = RolesManager.PlayerRole.None;
    private RolesManager.PlayerRole queuedRole = RolesManager.PlayerRole.None;
    private int stickDirection;
    private bool completing;

    public override void OnEnable()
    {
        base.OnEnable();
        ResolveReadyImages();
        if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom) return;

        completing = false;
        pendingRole = RolesManager.PlayerRole.None;
        queuedRole = RolesManager.PlayerRole.None;
        RecoverLocalRoleProperty();
        RebuildCards();
        RefreshReadyVisuals();
        TryAdvanceToLevelSelection();
    }

    private void ResolveReadyImages()
    {
        // Scene instances created before these serialized references were added
        // can still recover them from the authored column objects after a domain reload.
        if (runnerReadyImage == null && runnerColumn != null)
            runnerReadyImage = runnerColumn.GetComponent<Image>();
        if (drawerReadyImage == null && drawerColumn != null)
            drawerReadyImage = drawerColumn.GetComponent<Image>();

        // These Images are also each column Button's hit area. They must remain
        // raycast targets even while their ready artwork is visually transparent.
        if (runnerReadyImage != null) runnerReadyImage.raycastTarget = true;
        if (drawerReadyImage != null) drawerReadyImage.raycastTarget = true;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        stickDirection = 0;
        completing = false;
    }

    private void Update()
    {
        if (PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom || completing ||
            !PhotonSessionPolicy.IsRoleSelectionPhase()) return;

        if (IsLocalReady())
        {
            // Confirm is a ready toggle. Cancel remains an equivalent shortcut so
            // either input convention can return the player to role selection.
            if (ConfirmPressed() || CancelPressed()) SetLocalReady(false);
        }
        else
        {
            int direction = ReadDirection();
            if (direction != 0) MoveLocalPlayer(direction);
            else if (ConfirmPressed() && LocalRole() != RolesManager.PlayerRole.None) SetLocalReady(true);
            else if (CancelPressed() && LocalRole() != RolesManager.PlayerRole.None)
                RequestRole(RolesManager.PlayerRole.None);
        }

        MoveCards();
    }

    // Persistent Prefab UnityEvents call these methods. Clicking an owned column a
    // second time is the mouse equivalent of pressing Enter/A to become ready.
    public void SelectRunner() => SelectFromPointer(RolesManager.PlayerRole.Runner);
    public void SelectDrawer() => SelectFromPointer(RolesManager.PlayerRole.Drawer);

    public void LeaveRoom()
    {
        if (Allan.GameManager.Instance != null) Allan.GameManager.Instance.LeaveRoom();
    }

    private void SelectFromPointer(RolesManager.PlayerRole role)
    {
        if (!isActiveAndEnabled || completing || !PhotonSessionPolicy.IsRoleSelectionPhase()) return;
        if (IsLocalReady())
        {
            // Clicking the local player's selected column mirrors pressing Confirm again.
            if (LocalRole() == role) SetLocalReady(false);
            return;
        }
        if (LocalRole() == role) SetLocalReady(true);
        else RequestRole(role);
    }

    private void MoveLocalPlayer(int direction)
    {
        RolesManager.PlayerRole current = LocalRole();
        int column = current == RolesManager.PlayerRole.Runner ? -1
            : current == RolesManager.PlayerRole.Drawer ? 1 : 0;
        int target = Mathf.Clamp(column + direction, -1, 1);
        if (target == column) return;

        RolesManager.PlayerRole role = target < 0 ? RolesManager.PlayerRole.Runner
            : target > 0 ? RolesManager.PlayerRole.Drawer : RolesManager.PlayerRole.None;
        RequestRole(role);
    }

    private void RequestRole(RolesManager.PlayerRole requested)
    {
        if (!PhotonNetwork.InRoom || pendingRole != RolesManager.PlayerRole.None || IsLocalReady()) return;

        RolesManager.PlayerRole current = LocalRole();
        if (current == requested) return;

        // A mouse click can jump across the neutral column. Release the current
        // slot first, then claim the requested slot after Photon confirms the release.
        if (current != RolesManager.PlayerRole.None)
        {
            queuedRole = requested;
            ReleaseRole(current);
            return;
        }

        if (requested == RolesManager.PlayerRole.None) return;
        string ownerKey = PhotonSessionPolicy.GetRoleOwnerKey(requested);
        if (PhotonSessionPolicy.GetActorProperty(ownerKey) != 0) return;

        pendingRole = requested;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { ownerKey, PhotonNetwork.LocalPlayer.ActorNumber },
                { "Role_" + PhotonNetwork.LocalPlayer.ActorNumber, (int)requested }
            },
            new Hashtable { { ownerKey, 0 } });
    }

    private void ReleaseRole(RolesManager.PlayerRole role)
    {
        if (role == RolesManager.PlayerRole.None || !PhotonNetwork.InRoom) return;
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        string ownerKey = PhotonSessionPolicy.GetRoleOwnerKey(role);
        string readyKey = PhotonSessionPolicy.GetRoleReadyKey(role);

        // Clearing readiness with the ownership release prevents a stale ready
        // actor from satisfying the next Master's start validation.
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { ownerKey, 0 },
                { readyKey, 0 },
                { "Role_" + actor, (int)RolesManager.PlayerRole.None }
            },
            new Hashtable { { ownerKey, actor } });
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { "Role", (int)RolesManager.PlayerRole.None } });
    }

    private void SetLocalReady(bool ready)
    {
        RolesManager.PlayerRole role = LocalRole();
        if (role == RolesManager.PlayerRole.None || !PhotonNetwork.InRoom) return;

        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        string readyKey = PhotonSessionPolicy.GetRoleReadyKey(role);
        int expected = ready ? 0 : actor;
        int value = ready ? actor : 0;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { readyKey, value } },
            new Hashtable { { readyKey, expected } });
    }

    private RolesManager.PlayerRole LocalRole()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return RolesManager.PlayerRole.None;
        return RoleForActor(PhotonNetwork.LocalPlayer.ActorNumber);
    }

    private static RolesManager.PlayerRole RoleForActor(int actor)
    {
        if (PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.RunnerOwnerKey) == actor)
            return RolesManager.PlayerRole.Runner;
        if (PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.DrawerOwnerKey) == actor)
            return RolesManager.PlayerRole.Drawer;
        return RolesManager.PlayerRole.None;
    }

    private bool IsLocalReady()
    {
        RolesManager.PlayerRole role = LocalRole();
        return role != RolesManager.PlayerRole.None &&
               PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.GetRoleReadyKey(role)) ==
               PhotonNetwork.LocalPlayer.ActorNumber;
    }

    private int ReadDirection()
    {
        Gamepad pad = GameplayInput.OnlineUiGamepad;
        if (pad != null)
        {
            if (pad.dpad.left.wasPressedThisFrame) return -1;
            if (pad.dpad.right.wasPressedThisFrame) return 1;
            float x = pad.leftStick.x.ReadValue();
            int current = x <= -0.65f ? -1 : x >= 0.65f ? 1 : 0;
            if (Mathf.Abs(x) < 0.35f) current = 0;
            int previous = stickDirection;
            stickDirection = current;
            return current != 0 && previous == 0 ? current : 0;
        }

        stickDirection = 0;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return 0;
        if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) return -1;
        if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) return 1;
        return 0;
    }

    private static bool ConfirmPressed()
    {
        Gamepad pad = GameplayInput.OnlineUiGamepad;
        if (pad != null) return pad.buttonSouth.wasPressedThisFrame;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                    keyboard.numpadEnterKey.wasPressedThisFrame ||
                                    keyboard.spaceKey.wasPressedThisFrame);
    }

    private static bool CancelPressed()
    {
        Gamepad pad = GameplayInput.OnlineUiGamepad;
        if (pad != null) return pad.buttonEast.wasPressedThisFrame;
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private void RecoverLocalRoleProperty()
    {
        RolesManager.PlayerRole role = LocalRole();
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Role", (int)role } });
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { "Role_" + PhotonNetwork.LocalPlayer.ActorNumber, (int)role }
        });
    }

    private void RebuildCards()
    {
        if (!PhotonNetwork.InRoom) return;

        HashSet<int> present = new();
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            present.Add(player.ActorNumber);
            if (!cards.TryGetValue(player.ActorNumber, out PlayerCard card))
            {
                if (playerCardPrefab == null) continue;
                GameObject instance = Instantiate(playerCardPrefab, neutralColumn, false);
                instance.name = "OnlinePlayerCard_" + player.ActorNumber;

                // This clone is local UI derived from Photon properties. Disabling
                // its legacy network components prevents it from registering a second network object.
                PhotonView view = instance.GetComponent<PhotonView>();
                if (view != null) view.enabled = false;
                PhotonTransformView transformView = instance.GetComponent<PhotonTransformView>();
                if (transformView != null) transformView.enabled = false;

                card = new PlayerCard
                {
                    player = player,
                    rect = instance.transform as RectTransform,
                    appearance = instance.GetComponent<DisplayInRoleselect>()
                };
                cards.Add(player.ActorNumber, card);

                // A new player has no previous screen position to animate from, so
                // place only that new card at the column represented by current room state.
                // Existing cards retain their previous position and animate after a role update.
                if (card.rect != null) card.rect.position = CardTarget(player);

                // Appearance is rebound explicitly when Photon properties change;
                // it does not need to remain a registered PUN callback target.
                if (card.appearance != null) card.appearance.enabled = false;

                Button cancelButton = instance.GetComponentInChildren<Button>(true);
                if (cancelButton != null)
                {
                    cancelButton.gameObject.SetActive(player.IsLocal);
                    cancelButton.onClick.RemoveAllListeners();
                    cancelButton.onClick.AddListener(CancelLocalSelection);
                }
            }

            card.player = player;
            if (card.appearance != null) card.appearance.BindPlayer(player);
        }

        List<int> removed = new();
        foreach (KeyValuePair<int, PlayerCard> pair in cards)
        {
            if (present.Contains(pair.Key)) continue;
            if (pair.Value.rect != null) Destroy(pair.Value.rect.gameObject);
            removed.Add(pair.Key);
        }
        foreach (int actor in removed) cards.Remove(actor);
    }

    private void CancelLocalSelection()
    {
        if (IsLocalReady()) SetLocalReady(false);
        else if (LocalRole() != RolesManager.PlayerRole.None)
            RequestRole(RolesManager.PlayerRole.None);
    }

    private void MoveCards()
    {
        foreach (PlayerCard card in cards.Values)
        {
            if (card.rect == null) continue;
            Vector3 target = CardTarget(card.player);
            card.rect.position = Vector3.Lerp(card.rect.position, target,
                1f - Mathf.Exp(-cardMoveSpeed * Time.unscaledDeltaTime));
        }
    }

    private Vector3 CardTarget(Player player)
    {
        RolesManager.PlayerRole role = RoleForActor(player.ActorNumber);
        RectTransform column = role == RolesManager.PlayerRole.Runner ? runnerColumn
            : role == RolesManager.PlayerRole.Drawer ? drawerColumn : neutralColumn;
        int neutralIndex = 0;
        if (role == RolesManager.PlayerRole.None)
        {
            foreach (Player candidate in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (candidate.ActorNumber >= player.ActorNumber) continue;
                if (RoleForActor(candidate.ActorNumber) == RolesManager.PlayerRole.None) neutralIndex++;
            }
        }
        return column.TransformPoint(new Vector3(0f, cardTopOffset - neutralIndex * cardRowSpacing, 0f));
    }

    private void RefreshReadyVisuals()
    {
        int runner = PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.RunnerOwnerKey);
        int drawer = PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.DrawerOwnerKey);
        bool runnerReady = runner != 0 &&
                           PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.RunnerReadyActorKey) == runner;
        bool drawerReady = drawer != 0 &&
                           PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.DrawerReadyActorKey) == drawer;
        if (runnerReadyText != null) runnerReadyText.gameObject.SetActive(runnerReady);
        if (drawerReadyText != null) drawerReadyText.gameObject.SetActive(drawerReady);
        SetReadyImageVisible(runnerReadyImage, runnerReady);
        SetReadyImageVisible(drawerReadyImage, drawerReady);
    }

    private static void SetReadyImageVisible(Image image, bool visible)
    {
        if (image == null) return;

        // Keep the Graphic enabled so the full column remains a mouse raycast target.
        // CanvasRenderer alpha changes only its appearance: unready is hidden, ready is visible.
        image.canvasRenderer.SetAlpha(visible ? 1f : 0f);
    }

    private void TryResolvePendingRole()
    {
        if (pendingRole != RolesManager.PlayerRole.None)
        {
            int owner = PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.GetRoleOwnerKey(pendingRole));
            if (owner == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                RolesManager.PlayerRole confirmed = pendingRole;
                pendingRole = RolesManager.PlayerRole.None;
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Role", (int)confirmed } });
            }
            else if (owner != 0)
            {
                pendingRole = RolesManager.PlayerRole.None;
            }
        }

        if (queuedRole != RolesManager.PlayerRole.None && LocalRole() == RolesManager.PlayerRole.None &&
            pendingRole == RolesManager.PlayerRole.None)
        {
            RolesManager.PlayerRole requested = queuedRole;
            queuedRole = RolesManager.PlayerRole.None;
            RequestRole(requested);
        }
    }

    private void TryAdvanceToLevelSelection()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonSessionPolicy.IsRoleSelectionPhase()) return;
        int runner = PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.RunnerOwnerKey);
        int drawer = PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.DrawerOwnerKey);
        if (runner == 0 || drawer == 0 || runner == drawer) return;
        if (PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.RunnerReadyActorKey) != runner ||
            PhotonSessionPolicy.GetActorProperty(PhotonSessionPolicy.DrawerReadyActorKey) != drawer) return;
        if (!IsActiveActor(runner) || !IsActiveActor(drawer)) return;

        completing = true;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseLevels } },
            new Hashtable
            {
                { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseRoles },
                { PhotonSessionPolicy.RunnerOwnerKey, runner },
                { PhotonSessionPolicy.DrawerOwnerKey, drawer },
                { PhotonSessionPolicy.RunnerReadyActorKey, runner },
                { PhotonSessionPolicy.DrawerReadyActorKey, drawer }
            });
    }

    private static bool IsActiveActor(int actor)
    {
        return PhotonNetwork.CurrentRoom.Players.TryGetValue(actor, out Player player) && !player.IsInactive;
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        TryResolvePendingRole();
        RebuildCards();
        RefreshReadyVisuals();
        TryAdvanceToLevelSelection();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        RebuildCards();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RebuildCards();
        TryAdvanceToLevelSelection();
    }

    public override void OnJoinedRoom()
    {
        RecoverLocalRoleProperty();
        RebuildCards();
        RefreshReadyVisuals();
        TryAdvanceToLevelSelection();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RebuildCards();
        RefreshReadyVisuals();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TryAdvanceToLevelSelection();
    }
}
