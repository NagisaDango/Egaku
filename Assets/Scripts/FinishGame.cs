using Allan;
using Photon.Pun;
using UnityEngine;

public class FinishGame : MonoBehaviourPun
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //photonView.RPC("RPC_SetUpAppearance", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.CustomProperties["Eyes"], PhotonNetwork.LocalPlayer.CustomProperties["Mouth"], PhotonNetwork.LocalPlayer.CustomProperties["Color"]);
    }

    public void BackToHomePage()
    {
        //GameManager.Instance.LeaveRoom();
        GameManager.Instance.LeaveRoom();
        //PhotonNetwork.LeaveRoom(); 
        //PhotonNetwork.LeaveLobby();
        //PhotonNetwork.LoadLevel("AllanLauncher");
    }

    public void BackToRoomSelectionPage()
    {
        PhotonNetwork.LoadLevel("RoleSelection");
        //LoadLevelSelection();
    }

    public void ExitGame()
    {
        GameManager.Instance.LeaveRoom();
        Application.Quit();
    }
}
