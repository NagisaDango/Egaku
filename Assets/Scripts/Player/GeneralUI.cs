using Allan;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GeneralUI : MonoBehaviourPunCallbacks
{
    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public void ResetGame()
    {
        photonView.RPC("RPC_Reset", RpcTarget.AllBuffered);
        //GameManager.Instance.LoadLevel(GameManager.Instance.currentLevel);
    }

    [PunRPC]
    private void RPC_Reset()
    {
        GameManager.Instance.LoadLevel(GameManager.Instance.currentLevel);
    }
}
