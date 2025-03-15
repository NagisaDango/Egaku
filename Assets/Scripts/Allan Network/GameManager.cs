using System;
using System.Collections;

using UnityEngine;
using UnityEngine.UI;

using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;
using Photon.Pun.Demo.PunBasics;
using static RolesManager;
using TMPro;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using UnityEditor;
using WebSocketSharp;
using Hashtable = ExitGames.Client.Photon.Hashtable;



namespace Allan
{
    public class GameManager : MonoBehaviourPunCallbacks
    {


        
        public static GameManager Instance; 
        [Tooltip("The prefab to use for representing the player")]
        public GameObject runnerPrefab;
        public GameObject drawerPrefab;

        public GameObject roomSelection;
        public GameObject roleSelection;


        public GameObject roomItemPrefab;
        public Transform gridLayout;
        public TMP_InputField nameField;

        private HashSet<string> roomInfoSet = new();
        private bool devSpawn = false;

        private void Awake()
        {
            roomSelection.SetActive(true);
            roleSelection.SetActive(false);

        }
        public override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Start()
        {
            if (runnerPrefab == null || drawerPrefab == null)
            {
                Debug.LogError("<Color=Red><a>Missing</a></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'", this);
            }
            else
            {
                if (Runner.LocalPlayerInstance == null)
                {
                    Debug.LogFormat("We are Instantiating LocalPlayer from {0}", SceneManagerHelper.ActiveSceneName);
                    // we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
                    //PhotonNetwork.Instantiate(this.playerPrefab.name, new Vector3(0f, 5f, 0f), Quaternion.identity, 0);
                }
                else
                {
                    Debug.LogFormat("Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);
                }
            }
            DontDestroyOnLoad(this.gameObject);
            Instance = this;
        }

        public void DevSpawnPlayers()
        {
            devSpawn = true;
            LoadArena();
        }

        void SpawnPlayer()
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
            {
                PlayerRole playerRole = (PlayerRole)(int)PhotonNetwork.LocalPlayer.CustomProperties["Role"];

                Vector3 spawnPosition = (playerRole == PlayerRole.Runner) ? new Vector3(0, 5, 0) : new Vector3(0, 0, 0);

                GameObject playerPrefab = (playerRole == PlayerRole.Runner) ? runnerPrefab : drawerPrefab;

                var r = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
                r.name = playerPrefab.name;
            }
            else
            {
                Debug.LogError("Role not assigned to player!");
            }
        }

        public void LoadArena()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogError("PhotonNetwork : Trying to Load a level but we are not the master Client");
                return;
            }
            Debug.LogFormat("PhotonNetwork : Loading Level to scene Allan : {0}", PhotonNetwork.CurrentRoom.PlayerCount);
            
            //PhotonNetwork.IsMessageQueueRunning = false; // ✅ 暂停消息队列，防止 Photon 处理不完整的 ViewID
            PhotonNetwork.LoadLevel("Allan 1");
            //SpawnPlayer();
        }

        /// <summary>
        /// Called when the local player left the room. We need to load the launcher scene.
        /// </summary>
        public override void OnLeftRoom()
        {
            //SceneManager.LoadScene(0);
            roomSelection.SetActive(true);
            roleSelection.SetActive(false);

            //Hashtable roomProperties = currentRoom.CustomProperties;

            //if (roomProperties["p1"] == PhotonNetwork.NickName)
            //    roomProperties["p1"] = "";
            //if (roomProperties["p2"] == PhotonNetwork.NickName)
            //    roomProperties["p2"] = "";

            //currentRoom.SetCustomProperties(roomProperties);

            //PhotonNetwork.LeaveLobby();
            //PhotonNetwork.JoinLobby();
            //JoinLobbyAfterDelay();
            //Invoke("JoinLobbyAfterDelay", 0.5f);

        }
        private Room currentRoom;

        void JoinLobbyAfterDelay()
        {
            Debug.Log("Waiting for room cleanup...");
            PhotonNetwork.JoinLobby();
        }

        public void LeaveRoom()
        {
            currentRoom = PhotonNetwork.CurrentRoom;

            Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            photonView.RPC("RPC_RemoveRoomInfoSet", RpcTarget.All, PhotonNetwork.CurrentRoom.Name);
            Debug.Log($"Cleaning {roomProperties["p1"]}， {roomProperties["p2"]}， {PhotonNetwork.NickName}");

            if ((string)roomProperties["p1"] == PhotonNetwork.NickName)
            {
                Debug.Log("Cleaning p1");
                roomProperties["p1"] = "";
            }
            if ((string)roomProperties["p2"] == PhotonNetwork.NickName)
            {
                Debug.Log("Cleaning p2");
                roomProperties["p2"] = "";
            }

            if((string) roomProperties["p1"] == "" && (string)roomProperties["p2"] == "")
            {
                roomProperties["show"] = false;
            }
            else
            {
                roomProperties["show"] = true;
            }


            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
            //RefreshRoomList();
            PhotonNetwork.LeaveRoom();
            //PhotonNetwork.LeaveLobby();
            //PhotonNetwork.JoinLobby();
            //PhotonNetwork.JoinLobby();
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("Enter Callback OnConnectedToMaster");
            JoinLobbyAfterDelay();
            //RefreshRoomList();

            //PhotonNetwork.LeaveLobby();
            //PhotonNetwork.JoinLobby();
        }

        public override void OnPlayerEnteredRoom(Player other)
        {
            Debug.Log("Enter Callback OnPlayerEnteredRoom");

            Debug.LogFormat("OnPlayerEnteredRoom() {0}", other.NickName); // not seen if you're the player connecting

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

                //LoadArena();
            }
        }

        public override void OnPlayerLeftRoom(Player other)
        {
            Debug.LogFormat("OnPlayerLeftRoom() {0}", other.NickName); // seen when other disconnects

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogFormat("OnPlayerLeftRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

                //LoadArena();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Allan" || scene.name == "Allan 1")
            {
                Debug.Log("Scene Allan loaded. Spawning player...");
                if (devSpawn)
                {
                    var d = PhotonNetwork.Instantiate(drawerPrefab.name, new Vector3(0, 0, 0), Quaternion.identity);
                    d.name = drawerPrefab.name;
                    var r = PhotonNetwork.Instantiate(runnerPrefab.name, new Vector3(0, 5, 0), Quaternion.identity);
                    r.name = runnerPrefab.name;

                }
                else SpawnPlayer();
            }
        }

        public void CreateJoinButton()
        {
            Debug.Log("Enter CreateJoinButton" + PhotonNetwork.InLobby);
            if (nameField.text.IsNullOrEmpty())
            {
                return;
            }


            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)
            {
                Hashtable customProperties = new Hashtable
                {
                    { "p1", "" },
                    { "p2", "" },
                    { "show", false}
                };

                PhotonNetwork.CreateRoom(nameField.text, new RoomOptions {   MaxPlayers = 2, CleanupCacheOnLeave = true, EmptyRoomTtl = 0,  CustomRoomProperties = customProperties, CustomRoomPropertiesForLobby = new string[] { "p1", "p2", "show" } });
                //roomSelection.SetActive(false);
                //roleSelection.SetActive(true);

            }
            else
            {
                Debug.Log("connect ready: " + PhotonNetwork.IsConnectedAndReady);
                Debug.Log("in lobby: " + PhotonNetwork.InLobby);
            }
        }

        public void JoinButton(string name)
        {
            PhotonNetwork.JoinRoom(name);
            //roomSelection.SetActive(false);
            //roleSelection.SetActive(true);

        }

        public override void OnJoinedLobby()
        {
            Debug.Log("ding zhengasds");
            List<Transform> childs = new List<Transform>();
            foreach(Transform child in gridLayout)
            {
                childs.Add(child);
            }

            foreach(Transform child in childs)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        string test = "";

        void UpdateRoomPlayerList()
        {
            // Get current room properties
            Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;

            string playerList;
            Debug.Log($"RoomName: {PhotonNetwork.CurrentRoom.Name},{PhotonNetwork.NickName}");
            //Add the new player name(avoid duplicates)
            if ((string)roomProperties["p1"] == "" && (string)roomProperties["p2"] == "")
            {
                roomProperties["p1"] = PhotonNetwork.NickName;
            }
            else
            {
                if ((string)roomProperties["p1"] == "")
                {
                    roomProperties["p1"] = PhotonNetwork.NickName;
                }
                else
                {
                    roomProperties["p2"] = PhotonNetwork.NickName;
                }
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
        }

        void UpdateRoomPlayerList(Dictionary<int, Player> players)
        {
            Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            int jj = 1;
            foreach (int i in players.Keys)
            {
                roomProperties["p" + jj] = players[i].NickName;
                jj++;
            }

            if ((string)roomProperties["p1"] == "" && (string)roomProperties["p2"] == "")
            {
                roomProperties["show"] = false;
            }
            else
            {
                roomProperties["show"] = true;
            }



            //roomProperties["p1"] = players.ContainsKey(1) ? players[1].NickName : "";
            //roomProperties["p2"] = players.ContainsKey(2) ? players[2].NickName : "";


            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
        }

        // 发送变量到所有玩家
        public void SendPlayerData(Dictionary<string, string> playerNames)
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

            }
        }


        public override void OnJoinedRoom()
        {

            Debug.Log("Enter Callback OnJoinedRoom");
            Debug.Log($"Room successfully created! Now joining the room...");
            roomSelection.SetActive(false);
            roleSelection.SetActive(true);

            UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);


            print("Room X" + PhotonNetwork.CurrentRoom.Name);
            photonView.RPC("RPC_AddRoomInfoSet", RpcTarget.All, PhotonNetwork.CurrentRoom.Name);
            Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
            // #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
            if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            {
                Debug.Log("We load the RoleSelection with host: ");

                //PhotonNetwork.LoadLevel("RoleSelection");
                roomSelection.SetActive(false);
                roleSelection.SetActive(true);
            }
            //RefreshRoomList();

        }

        [PunRPC]
        public void RPC_AddRoomInfoSet(string roomName)
        {
            roomInfoSet.Add(roomName);
            print("Adding Room: " + roomName);
        }
        [PunRPC]
        public void RPC_RemoveRoomInfoSet(string roomName)
        {
            roomInfoSet.Remove(roomName);
            print("Removing Room: " + roomName);
        }

        //RPCs only get call in rooms
        //Only get call in lobby

        public void RefreshRoomList()
        {
            foreach (Transform child in gridLayout)
            {
                child.GetComponentInChildren<Button>().onClick.RemoveListener(() => JoinButton(child.GetComponentInChildren<TMP_Text>().text));
                Destroy(child.gameObject);
            }

            foreach(RoomInfo room in roomInfoList)
            {
                if ((bool)room.CustomProperties["show"])
                {
                    Transform newRoom = Instantiate(roomItemPrefab, gridLayout.transform).transform;

                    string player1 = (string)room.CustomProperties["p1"] != "" ? (string)room.CustomProperties["p1"] : " ----";
                    string player2 = (string)room.CustomProperties["p2"] != "" ? (string)room.CustomProperties["p2"] : " ----";
                    string players = player1 + " X " + player2;
                    newRoom.transform.Find("PlayerName").GetComponent<TMP_Text>().text = players;
                    newRoom.transform.Find("RoomName").GetComponent<TMP_Text>().text = room.Name;// + "(" + room.PlayerCount +")";
                    newRoom.GetComponentInChildren<Button>().onClick.AddListener(() => JoinButton(room.Name));
                }


            }

        }


        private List<RoomInfo> roomInfoList = new List<RoomInfo>();
        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            Debug.Log("Enter Callback OnRoomListUpdate");
            List<string> activeRooms = new List<string>();
            List<string> removingRooms = new List<string>();
            foreach (RoomInfo room in roomList)
            {
                if(room.RemovedFromList)
                {
                    print("Removing room: " + room.Name);
                    removingRooms.Add(room.Name);
                }
                else
                {
                    print("Active room: " + room.Name);
                    activeRooms.Add(room.Name);
                }
            }

            roomList.RemoveAll(room => room.RemovedFromList);

            List<Transform> toRemove = new List<Transform>();
            foreach (Transform child in gridLayout)
            {
                if(removingRooms.Contains(child.name) || activeRooms.Contains(child.name))
                {
                    toRemove.Add(child);

                }
            }

            foreach (Transform child in toRemove)
            {
                DestroyImmediate(child.gameObject);
            }

            print("__");
            foreach (RoomInfo room in roomList)
            {

                if(room.RemovedFromList)
                {
                    continue;
                }
                print("Cleaning" + room.Name + room.PlayerCount + (string)room.CustomProperties["p1"] + (string)room.CustomProperties["p2"]);
                //GameObject newRoom = Instantiate(roomItemPrefab, gridLayout.position, Quaternion.identity);
                Transform newRoom = null;
                if ((string)room.CustomProperties["p1"] != "" || (string)room.CustomProperties["p2"] != "")
                {
                    newRoom = Instantiate(roomItemPrefab, gridLayout.transform).transform;
                    newRoom.name = room.Name;
                }

                if (newRoom != null)
                {
                    if (room.PlayerCount == 2)
                    {
                        newRoom.GetComponentInChildren<Button>().interactable = false;
                    }
                    else
                    {
                        newRoom.GetComponentInChildren<Button>().interactable = true;

                    }

                    string player1 = (string)room.CustomProperties["p1"] != "" ? (string)room.CustomProperties["p1"] : " ----";
                    string player2 = (string)room.CustomProperties["p2"] != "" ? (string)room.CustomProperties["p2"] : " ----";
                    string players = player1 + " X " + player2;
                    newRoom.transform.Find("PlayerName").GetComponent<TMP_Text>().text = players;
                    Debug.Log($"Room: {room.Name}, Players: {players}");

                    newRoom.transform.Find("RoomName").GetComponent<TMP_Text>().text = room.Name;// + "(" + room.PlayerCount +")";
                    newRoom.GetComponentInChildren<Button>().onClick.AddListener(() => JoinButton(room.Name));
                }

            }

        }


        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            Debug.Log("Enter Callback OnRoomPropertiesUpdate");


            foreach (var key in propertiesThatChanged.Keys)
            {
                Debug.Log($"Room Properly changed:{key} ->{propertiesThatChanged[key]}, ROOM:{PhotonNetwork.CurrentRoom.Name}");

            }
        }

        

    }
}