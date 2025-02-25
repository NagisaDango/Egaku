using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;
using WebSocketSharp;

using ExitGames.Client.Photon;
using UnityEditor;
using Unity.VisualScripting;
using ExitGames.Client.Photon.StructWrapping;

public class RoomListManager : MonoBehaviourPunCallbacks
{
    public GameObject roomItemPrefab;
    public Transform gridLayout;

    public TMP_InputField nameField;

    //struct Room {  public string[] playerNames; }

    public Dictionary<string,  string> rooms = new Dictionary<string, string>();


    public static RoomListManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            PhotonNetwork.AddCallbackTarget(this); // Listen for events
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this); // Remove listener
    }

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
        Debug.Log("Enter CreateJoinButton");
        if (nameField.text.IsNullOrEmpty())
        {
            return;
        }


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


    string test = "";
    [PunRPC]
    void UpdateRoomPlayerList()
    {
        // Get current room properties
        //ExitGames.Client.Photon.Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;

        // Get existing player list or create a new one
        //string playerList = roomProperties.ContainsKey("PlayerList") ? (string)roomProperties["PlayerList"] : "";

        string playerList;
        Debug.Log($"RoomName: {PhotonNetwork.CurrentRoom.Name}");
        // Add the new player name (avoid duplicates)
        //if (!rooms.ContainsKey(PhotonNetwork.CurrentRoom.Name))
        //{
        //    rooms.Add(PhotonNetwork.CurrentRoom.Name, (nameField.text, ""));
        //}
        //else
        //{
        //    var players = rooms[PhotonNetwork.CurrentRoom.Name];
        //    if (players.Item1 != "")
        //    {
        //        players = (nameField.text, players.Item2);
        //    }
        //    else
        //    {
        //        players = (players.Item1, nameField.text);
        //    }
        //    rooms[PhotonNetwork.CurrentRoom.Name] = players;
        //}

        if(!rooms.ContainsKey(PhotonNetwork.CurrentRoom.Name))
        {
            rooms.Add(PhotonNetwork.CurrentRoom.Name, nameField.text+ ",");
        }
        else
        {
            var players = rooms[PhotonNetwork.CurrentRoom.Name].Split(",");
            string rnames;
            if (players[0] == "")
            {
                rnames = (nameField.text + "," + players[1]);
            }
            else
            {
                rnames = players[0] + "," + nameField.text;
            }
            rooms[PhotonNetwork.CurrentRoom.Name] = rnames;
        }


        test = PhotonNetwork.CurrentRoom.Name;
        SendPlayerData(new Dictionary<string, string> { });
        //// Save back to room properties
        //roomProperties["PlayerList"] = playerList;
        //Debug.Log($"Update Players: {playerList}");
        //PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

        //string players = (string)PhotonNetwork.CurrentRoom.CustomProperties["PlayerList"];
        //Debug.Log($"Update Players: {players}");
        //print(PhotonNetwork.CurrentRoom.Name);
        //print(rooms);
        //rooms.Add(PhotonNetwork.CurrentRoom.Name, playerList);

        //string playerList = roomProperties.ContainsKey("PlayerList") ? (string)roomProperties["PlayerList"] : "";


    }

    // 发送变量到所有玩家
    public void SendPlayerData(Dictionary<string, string>  playerNames)
    {
        object content = playerNames; // 你可以传递多个值
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        SendOptions sendOptions = new SendOptions { Reliability = true };

        PhotonNetwork.RaiseEvent(1, content, options, sendOptions); // 事件代码 "1"
    }

    // 监听事件
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == 1) // 事件代码 "1"
        {
            object data = (object)photonEvent.CustomData;
            Dictionary<string, string> roomNames = (Dictionary<string, string>)data;

            //Debug.Log($"Room: {roomNames}");
            //rooms = roomNames;
        }
    }



    #region Photon Callbacks

    public override void OnCreatedRoom()
    {

    }
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        //UpdateRoomPlayerList(); 
        //PhotonView photonView = PhotonView.Get(this);
        //photonView.RPC("UpdateRoomPlayerList", RpcTarget.All);
        
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Room successfully created! Now joining the room...");

        UpdateRoomPlayerList();
        //photonView.RPC("UpdateRoomPlayerList", RpcTarget.All);
        print("Room X" +  PhotonNetwork.CurrentRoom.Name);
        //PhotonNetwork.JoinRoom(nameField.text);
        //PhotonNetwork.JoinRandomRoom();
        Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
        // #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
        if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Debug.Log("We load the RoleSelection");

            //UpdateRoomPlayerList();
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
                    rooms.Remove(roomList[i].Name);
                    roomList.Remove(roomList[i]);
                }

            }
        }


        foreach (RoomInfo room in roomList)
        {
            //GameObject newRoom = Instantiate(roomItemPrefab, gridLayout.position, Quaternion.identity);
            Transform newRoom = Instantiate(roomItemPrefab, gridLayout.transform).transform;
            //newRoom.transform.SetParent(gridLayout);

            string playerList;
            Debug.Log($"Rooms: {rooms.Keys},{rooms.ContainsKey(room.Name)},{test}, {room.Name}");
            if (rooms.ContainsKey(room.Name))
            {
                //playerList = (string)room.CustomProperties["PlayerList"];
                var players = rooms[room.Name];
                //newRoom.transform.Find("PlayerName").GetComponent<TMP_Text>().text = players.Item1 = " x " + players.Item2;
                newRoom.transform.Find("PlayerName").GetComponent<TMP_Text>().text = players;
                Debug.Log($"Room: {room.Name}, Players: {players}");
            }
            else
            {
                Debug.Log($"Room: {room.Name}, PlayerList not found in CustomProperties.");
            }


            newRoom.transform.Find("RoomName").GetComponent<TMP_Text>().text = room.Name;// + "(" + room.PlayerCount +")";
            newRoom.GetComponentInChildren<Button>().onClick.AddListener(() => JoinButton(room.Name));

        }
    }


    #endregion
}
