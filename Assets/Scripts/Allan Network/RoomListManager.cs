using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;

public class RoomListManager : MonoBehaviourPunCallbacks
{
    public GameObject roomItemPrefab;
    public Transform gridLayout;

    public TMP_InputField nameField;


    void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            // Connect to the Photon Master Server
            PhotonNetwork.ConnectUsingSettings();
            Debug.Log("Connecting to Photon Master Server...");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
     

    public void CreateJoinButton()
    {


        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)
        {
            PhotonNetwork.CreateRoom(nameField.text, new RoomOptions { MaxPlayers = 2 });
            //PhotonNetwork.JoinRoom(name);

        }
    }

    public void JoinButton(string name )
    {
        PhotonNetwork.JoinRoom(name);

    }


    #region Photon Callbacks


    public override void OnJoinedRoom()
    {
        Debug.Log("Room successfully created! Now joining the room...");

        //PhotonNetwork.JoinRoom(nameField.text);
        //PhotonNetwork.JoinRandomRoom();
        Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
        // #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Debug.Log("We load the RoleSelection");

            // #Critical
            // Load the Room Level.
            PhotonNetwork.LoadLevel("RoleSelection");
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        for (int i = 0; i < gridLayout.childCount; i++) { 
            if(gridLayout.GetChild(i).GetComponentInChildren<TMP_Text>().text == roomList[i].Name)
            {
                Destroy(gridLayout.GetChild(i).gameObject);
                gridLayout.GetChild(i).GetComponentInChildren<Button>().onClick.RemoveListener(() => JoinButton(roomList[i].Name));

                if (roomList[i].PlayerCount == 0)
                {
                    roomList.Remove(roomList[i]);
                }

            }
        }


        foreach (RoomInfo room in roomList)
        {
            GameObject newRoom = Instantiate(roomItemPrefab, gridLayout.position, Quaternion.identity);
            newRoom.GetComponentInChildren<TMP_Text>().text = room.Name;// + "(" + room.PlayerCount +")";
            newRoom.GetComponentInChildren<Button>().onClick.AddListener(() => JoinButton(room.Name));
            newRoom.transform.SetParent(gridLayout);

        }
    }


    #endregion
}
