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
        // Preserve the serialized button method name; gameplay Back now keeps the room and roles.
        GameManager.Instance.BackToRoomSelectionPage();
    }

    // Explicit alias for future authored return controls; existing buttons retain their method name.
    public void ReturnToLevelSelection() => GameManager.Instance.BackToRoomSelectionPage();

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
