using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using Allan;
using System.Collections.Generic;
using System.Linq; // Required for Hashtable

public class RolesManager : MonoBehaviourPunCallbacks
{
    public Button drawerButton;
    public Button runnerButton;
    public Button startGameButton;
    public GameObject playerDisplay;
    public Transform playerDisplayParent;

    [SerializeField] private Transform drawerTrans;
    [SerializeField] private Transform runnerTrans;
    private GameObject thisPlayerDisplay;
    private int displayID;
    private PlayerRole selectedRole = PlayerRole.None; // Default

    // confirmedRole follows the server-owned room slot; pendingRole exists only while a CAS request is in flight.
    private PlayerRole confirmedRole = PlayerRole.None;
    private PlayerRole pendingRole = PlayerRole.None;

    public enum PlayerRole { None = -1, Drawer, Runner }

    void Start()
    {
        // Set up button listeners
        drawerButton.onClick.AddListener(() => SelectRole(PlayerRole.Drawer));
        runnerButton.onClick.AddListener(() => { SelectRole(PlayerRole.Runner); });

        confirmedRole = GetLocalOwnedRole();
        selectedRole = confirmedRole;

        //startGameButton.onClick.AddListener(() => GameManager.Instance.LoadArena());
        startGameButton.interactable = false; // Disable until valid selections
        thisPlayerDisplay =
            PhotonNetwork.Instantiate(playerDisplay.name, new Vector3(0,0,0), this.transform.rotation);

        //drawerTrans = drawerButton.transform.parent;
        //runnerTrans = runnerButton.transform.parent;

        //photonView.RPC("RPC_ChangePlayerDisplayParent", RpcTarget.AllBuffered, playerDisplayParent.GetComponent<PhotonView>().ViewID);
        //thisPlayerDisplay.transform.parent = playerDisplayParent;
        //thisPlayerDisplay.transform.SetParent(playerDisplayParent);
        thisPlayerDisplay.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => SelectRole(PlayerRole.None));
        //thisPlayerDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = PhotonNetwork.NickName;
        displayID = thisPlayerDisplay.GetComponent<PhotonView>().ViewID;
        photonView.RPC("RPC_SwitchDisplayPos", RpcTarget.AllBuffered, selectedRole, displayID, PhotonNetwork.LocalPlayer.ActorNumber);
        photonView.RPC("RPC_Set_Name", RpcTarget.AllBuffered, PhotonNetwork.NickName, displayID);

        if (!PhotonNetwork.IsMasterClient)
        {
            startGameButton.gameObject.SetActive(false);
        }

        RefreshRoleUi();
    }



    void SelectRole(PlayerRole role)
    {
        // None also represents "no pending request", so it must not be rejected by the pending-role guard.
        if (!PhotonNetwork.InRoom || role == confirmedRole ||
            (pendingRole != PlayerRole.None && role == pendingRole)) return;

        if (role == PlayerRole.None)
        {
            // The X button releases only the local actor's confirmed slot.
            ReleaseRole(confirmedRole);
            ConfirmRole(PlayerRole.None);
            return;
        }

        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        string slotKey = PhotonSessionPolicy.GetRoleOwnerKey(role);
        int currentOwner = GetRoleOwner(role);

        if (currentOwner == actorNumber)
        {
            ConfirmRole(role);
            return;
        }

        if (currentOwner != 0)
        {
            Debug.Log($"Role {role} is already owned by actor {currentOwner}.");
            RefreshRoleUi();
            return;
        }

        // Photon CAS guarantees that only one actor can replace the empty (zero) slot.
        pendingRole = role;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { slotKey, actorNumber } },
            new Hashtable { { slotKey, 0 } });
    }

    private int GetRoleOwner(PlayerRole role)
    {
        // Missing slots are treated as empty for offline mode and defensive compatibility.
        if (!PhotonNetwork.InRoom) return 0;

        string key = PhotonSessionPolicy.GetRoleOwnerKey(role);
        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object owner) ? (int)owner : 0;
    }

    private PlayerRole GetLocalOwnedRole()
    {
        // Rejoining actors recover the role that is still reserved by their actor number.
        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        if (GetRoleOwner(PlayerRole.Drawer) == actorNumber) return PlayerRole.Drawer;
        if (GetRoleOwner(PlayerRole.Runner) == actorNumber) return PlayerRole.Runner;
        return PlayerRole.None;
    }

    private void ConfirmRole(PlayerRole role)
    {
        // Local/player properties and the display are updated only after the authoritative room slot confirms ownership.
        PlayerRole previousRole = confirmedRole;
        confirmedRole = role;
        selectedRole = role;
        pendingRole = PlayerRole.None;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Role", (int)role } });
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { "Role_" + PhotonNetwork.LocalPlayer.ActorNumber, (int)role }
        });
        photonView.RPC("RPC_SwitchDisplayPos", RpcTarget.AllBuffered, role, displayID, PhotonNetwork.LocalPlayer.ActorNumber);

        if (previousRole != PlayerRole.None && previousRole != role)
            ReleaseRole(previousRole);

        RefreshRoleUi();
    }

    private void ReleaseRole(PlayerRole role)
    {
        // The expected actor number prevents one client from clearing another client's role.
        if (role == PlayerRole.None || !PhotonNetwork.InRoom) return;

        int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { PhotonSessionPolicy.GetRoleOwnerKey(role), 0 } },
            new Hashtable { { PhotonSessionPolicy.GetRoleOwnerKey(role), actorNumber } });
    }

    [PunRPC]
    void RPC_Set_Name(string name, int viewID)
    {
        PhotonView targetView = PhotonView.Find(viewID);
        if (targetView == null) return; // Ensure target exists

        GameObject targetDisplay = targetView.gameObject;
        targetDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = name;
    }

    [PunRPC]
    void RPC_SwitchDisplayPos(PlayerRole role, int viewID, int playerActorNumber)
    {
        PhotonView targetView = PhotonView.Find(viewID);
        Debug.Log("RPC" + viewID + targetView == null);
        if (targetView == null) return; // Ensure target exists

        GameObject targetDisplay = targetView.gameObject;
        GameObject noRoleButton = targetDisplay.transform.GetChild(0).gameObject;
        print("RPC " + targetDisplay.name + viewID);
        // Assign correct parent based on selected role
        if (role == PlayerRole.None)
        {
            targetDisplay.transform.SetParent(playerDisplayParent);
        }
        else if (role == PlayerRole.Drawer)
        {
            targetDisplay.transform.SetParent(drawerTrans);
        }
        else if (role == PlayerRole.Runner)
        {
            targetDisplay.transform.SetParent(runnerTrans);
        }

        else
            print("RPC enter else");

        // This RPC only positions the replicated player display. Role buttons are refreshed from
        // authoritative room slots below, so a remote player's RPC cannot overwrite the local UI.
        bool isLocalPlayersDisplay = PhotonNetwork.LocalPlayer.ActorNumber == playerActorNumber;
        noRoleButton.SetActive(isLocalPlayersDisplay && role != PlayerRole.None);

        targetDisplay.transform.localScale = Vector3.one;
        targetDisplay.transform.localPosition = Vector3.zero;

        // Reapply the local view after every display RPC because buffered RPC delivery order can vary.
        RefreshRoleUi();
    }

    // Check if the selected role is already taken by another player
    bool IsRoleTaken(PlayerRole role)
    {
        return GetRoleOwner(role) != 0;
    }

    [PunRPC]
    void RPC_CheckStartGameCondition(int[] roleArray)
    {
        RefreshRoleUi();
    }

    private void RefreshRoleUi()
    {
        if (!PhotonNetwork.InRoom) return;

        int localActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int drawerOwner = GetRoleOwner(PlayerRole.Drawer);
        int runnerOwner = GetRoleOwner(PlayerRole.Runner);

        // Once this client owns a role, both role-selection buttons stay hidden until the X button is used.
        bool localPlayerHasRole = confirmedRole != PlayerRole.None;
        drawerButton.gameObject.SetActive(!localPlayerHasRole && drawerOwner == 0);
        runnerButton.gameObject.SetActive(!localPlayerHasRole && runnerOwner == 0);
        startGameButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);

        // Distinct owners are required so one actor can never satisfy both role requirements during a role switch.
        startGameButton.interactable = PhotonNetwork.IsMasterClient &&
                                       drawerOwner != 0 &&
                                       runnerOwner != 0 &&
                                       drawerOwner != runnerOwner;

        if (confirmedRole != PlayerRole.None &&
            GetRoleOwner(confirmedRole) != localActorNumber)
        {
            confirmedRole = PlayerRole.None;
            selectedRole = PlayerRole.None;
        }
    }


    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        //CheckStartGameCondition();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log("Room properties updated.");

        if (pendingRole != PlayerRole.None &&
            propertiesThatChanged.ContainsKey(PhotonSessionPolicy.GetRoleOwnerKey(pendingRole)))
        {
            // A CAS loser observes the winner's actor number and simply clears its pending request.
            if (GetRoleOwner(pendingRole) == PhotonNetwork.LocalPlayer.ActorNumber)
                ConfirmRole(pendingRole);
            else
                pendingRole = PlayerRole.None;
        }

        RefreshRoleUi();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshRoleUi();
    }


}
