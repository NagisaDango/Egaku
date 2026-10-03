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
        // Home is a local departure; shared level selection uses the separate handler below.
        GameManager.Instance.BackToHomePage();
        //PhotonNetwork.LeaveRoom(); 
        //PhotonNetwork.LeaveLobby();
        //PhotonNetwork.LoadLevel("AllanLauncher");
    }

    public void BackToRoomSelectionPage()
    {
        GameManager.Instance.BackToRoomSelectionPage();
        //LoadLevelSelection();
    }

    public void ExitGame()
    {
        GameManager.Instance.LeaveRoom();
        Application.Quit();
    }
}
