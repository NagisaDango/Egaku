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

    private GameObject thisPlayerDisplay;
    private int displayID;
    private PlayerRole selectedRole = PlayerRole.None; // Default

    public enum PlayerRole { None = -1, Drawer, Runner }

    void Start()
    {
        // Set up button listeners
        drawerButton.onClick.AddListener(() => SelectRole(PlayerRole.Drawer));
        runnerButton.onClick.AddListener(() => { SelectRole(PlayerRole.Runner); });

        startGameButton.onClick.AddListener(() => GameManager.Instance.LoadArena());
        startGameButton.interactable = false; // Disable until valid selections
        thisPlayerDisplay =
            PhotonNetwork.Instantiate(playerDisplay.name, new Vector3(0,0,0), this.transform.rotation);

        thisPlayerDisplay.transform.GetChild(0).GetComponent<Button>().onClick.AddListener(() => SelectRole(PlayerRole.None));
        //thisPlayerDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = PhotonNetwork.NickName;
        displayID = thisPlayerDisplay.GetComponent<PhotonView>().ViewID;
        photonView.RPC("RPC_SwitchDisplayPos", RpcTarget.AllBuffered, selectedRole, displayID, PhotonNetwork.LocalPlayer.ActorNumber);
        photonView.RPC("RPC_Set_Name", RpcTarget.AllBuffered, PhotonNetwork.NickName, displayID);
    }

    void SelectRole(PlayerRole role)
    {
        selectedRole = role;
        Hashtable playerProperties = new Hashtable { { "Role", (int)role } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);
        photonView.RPC("RPC_SwitchDisplayPos", RpcTarget.AllBuffered, selectedRole, displayID, PhotonNetwork.LocalPlayer.ActorNumber);
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
        if (targetView == null) return; // Ensure target exists

        GameObject targetDisplay = targetView.gameObject;
        GameObject noRoleButton = targetDisplay.transform.GetChild(0).gameObject;

        // Assign correct parent based on selected role
        if (role == PlayerRole.None)
            targetDisplay.transform.SetParent(this.transform);
        else if (role == PlayerRole.Drawer)
            targetDisplay.transform.SetParent(drawerButton.gameObject.transform);
        else if (role == PlayerRole.Runner)
            targetDisplay.transform.SetParent(runnerButton.gameObject.transform);

        // Store player's role in Photon Custom Properties (Async Update)
        Hashtable playerProperties = new Hashtable { { "Role_" + playerActorNumber, (int)role } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(playerProperties); // The UI will update when the property is synced

        // UI Management for ALL Players
        if (role == PlayerRole.Drawer)
        {
            drawerButton.interactable = false; // Disable Drawer button for all
            runnerButton.interactable = PhotonNetwork.LocalPlayer.ActorNumber != playerActorNumber;

            if (PhotonNetwork.LocalPlayer.ActorNumber == playerActorNumber)
                noRoleButton.SetActive(true);
            else
                noRoleButton.SetActive(false);
        }
        else if (role == PlayerRole.Runner)
        {
            runnerButton.interactable = false; // Disable Runner button for all
            drawerButton.interactable = PhotonNetwork.LocalPlayer.ActorNumber != playerActorNumber;

            if (PhotonNetwork.LocalPlayer.ActorNumber == playerActorNumber)
                noRoleButton.SetActive(true);
            else
                noRoleButton.SetActive(false);
        }
        else if (role == PlayerRole.None)
        {
            noRoleButton.SetActive(false); // Hide No Role button
        }

        targetDisplay.transform.localScale = Vector3.one;
        targetDisplay.transform.localPosition = Vector3.zero;
    }

    // Check if the selected role is already taken by another player
    bool IsRoleTaken(PlayerRole role)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("Role_" + player.ActorNumber))
            {
                Debug.Log((PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + player.ActorNumber]);
                if ((PlayerRole)(int)PhotonNetwork.CurrentRoom.CustomProperties["Role_" + player.ActorNumber] == role)
                {
                    return true; // Role is already taken by another player
                }
            }
        }
        return false;
    }

    [PunRPC]
    void RPC_CheckStartGameCondition(int[] roleArray)
    {
        HashSet<PlayerRole> roleSet = new HashSet<PlayerRole>(roleArray.Select(r => (PlayerRole)r));

        Debug.Log("Checking Start Game Condition...");
        Debug.Log($"Current Roles in Room: {string.Join(", ", roleSet)}");

        // Ensure both required roles are present
        if (roleSet.Contains(PlayerRole.Runner) && roleSet.Contains(PlayerRole.Drawer))
        {
            Debug.Log("Both roles selected, enabling start button.");
            startGameButton.interactable = true;
        }
        else
        {
            Debug.Log("Not all roles are selected, disabling start button.");
            startGameButton.interactable = false;
        }
    }


    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        //CheckStartGameCondition();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Debug.Log("Room properties updated.");

        List<int> roleList = new List<int>(); // Stores all current role selections

        // Loop through all players and collect their assigned roles
        foreach (var player in PhotonNetwork.PlayerList)
        {
            string key = "Role_" + player.ActorNumber;
            if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
            {
                int roleValue = (int)PhotonNetwork.CurrentRoom.CustomProperties[key];
                roleList.Add(roleValue);
            }
        }

        Debug.Log($"Updated Role List: {string.Join(", ", roleList)}");

        // Send the full role list to all clients
        photonView.RPC("RPC_CheckStartGameCondition", RpcTarget.All, roleList.ToArray());

        // Refresh button interactability
        drawerButton.interactable = !IsRoleTaken(PlayerRole.Drawer);
        runnerButton.interactable = !IsRoleTaken(PlayerRole.Runner);
    }


}
