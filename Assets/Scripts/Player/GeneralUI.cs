using Allan;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GeneralUI : MonoBehaviourPunCallbacks
{
    public void LeaveRoom()
    {
        print("Enter LeaveRoom");
        GameManager.Instance.LeaveRoom();
        //PhotonNetwork.LeaveRoom();
    }

    public void ResetGame()
    {
        print("Enter ResetGame");
        GameManager.Instance.RequestLevelRefresh();
    }


    public void SkipLevel()
    {
        print("Enter SkipLevel");

        EventHandler.CallReachDestinationEvent();
        GameObject.Find("GameCanvas/Panel/SkipBtn").GetComponent<Button>().interactable = false;
    }
}
