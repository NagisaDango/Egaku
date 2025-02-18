using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using Allan; // Required for Hashtable

public class RolesManager : MonoBehaviourPunCallbacks
{
    public TMP_Text selectedRoleText;
    public Button drawerButton;
    public Button runnerButton;
    public Button confirmButton;
    public TMP_Text statusText; 
    public Button startGameButton;
    public GameObject playerDisplay;

    private GameObject thisPlayerDisplay;
    private PlayerRole selectedRole = PlayerRole.None; // Default

    public enum PlayerRole { None = -1, Drawer, Runner }

    void Start()
    {
        // Set up button listeners
        drawerButton.onClick.AddListener(() => SelectRole(PlayerRole.Drawer));
        runnerButton.onClick.AddListener(() => SelectRole(PlayerRole.Runner));
        confirmButton.onClick.AddListener(ConfirmSelection);

        startGameButton.onClick.AddListener(() => GameManager.Instance.LoadArena());
        startGameButton.interactable = false; // Disable until valid selections

        thisPlayerDisplay =
            PhotonNetwork.Instantiate(playerDisplay.name, new Vector3(0,0,0), this.transform.rotation);
    }



    void SelectRole(PlayerRole role)
    {
        if (IsRoleTaken(role))
        {
            statusText.text = "This role is already taken!";
            return;
        }

        selectedRole = role;
        selectedRoleText.text = "Selected Role: " + role.ToString();
        statusText.text = "Press Confirm to lock your role.";
        photonView.RPC("RPC_SwitchDisplayPos", RpcTarget.AllBuffered, selectedRole);
    }

    [PunRPC]
    void RPC_SwitchDisplayPos(PlayerRole role)
    {
        if (role == PlayerRole.None)
            thisPlayerDisplay.transform.SetParent(this.transform);
        else if (role == PlayerRole.Drawer)
            thisPlayerDisplay.transform.SetParent(drawerButton.gameObject.transform);
        else if (role == PlayerRole.Runner)
            thisPlayerDisplay.transform.SetParent(runnerButton.gameObject.transform);

        thisPlayerDisplay.transform.localPosition = Vector3.zero;
    }
    
    void ConfirmSelection()
    {
        if (selectedRole == PlayerRole.None)
        {
            statusText.text = "You must select a role!";
            return;
        }

        if (IsRoleTaken(selectedRole))
        {
            statusText.text = "This role is already taken!";
            return;
        }

        Hashtable playerProperties = new Hashtable();
        playerProperties["Role"] = (int)selectedRole;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

        confirmButton.interactable = false; // Prevent multiple selections
        
        CheckStartGameCondition();
    }

    // Check if the selected role is already taken by another player
    bool IsRoleTaken(PlayerRole role)
    {
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey("Role") && (PlayerRole)(int)player.CustomProperties["Role"] == role)
            {
                return true; // Role is already taken
            }
        }
        return false;
    }

    void CheckStartGameCondition()
    {
        if (PhotonNetwork.PlayerList.Length < 2)
        {
            statusText.text = "Waiting for another player...";
            return;
        }

        PlayerRole role1 = PlayerRole.None;
        PlayerRole role2 = PlayerRole.None;

        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.CustomProperties.ContainsKey("Role"))
            {
                if (role1 == PlayerRole.None)
                    role1 = (PlayerRole)(int)player.CustomProperties["Role"];
                else
                    role2 = (PlayerRole)(int)player.CustomProperties["Role"];
            }
        }

        if (role1 != PlayerRole.None && role2 != PlayerRole.None && role1 != role2)
        {
            startGameButton.interactable = true; // Enable start button when conditions are met
            statusText.text = "All set! Press Start to begin.";
        }
        else
        {
            statusText.text = "Both players must select different roles!";
            startGameButton.interactable = false;
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        CheckStartGameCondition();
    }
}
