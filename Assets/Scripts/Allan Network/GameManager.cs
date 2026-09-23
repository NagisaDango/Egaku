using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using static RolesManager;
using TMPro;
using ExitGames.Client.Photon;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using JetBrains.Annotations;



namespace Allan
{
    public class GameManager : MonoBehaviourPunCallbacks
    {
        public static GameManager Instance;
        public static bool initialized = false;
        
        [Header("Prefabs")]
        [Tooltip("The prefab to use for representing the player")]
        public GameObject runnerPrefab;
        public GameObject drawerPrefab;
        
        public GameObject roomSelection;
        public GameObject roleSelection;
        public GameObject levelSelection;
        
        public GameObject roomItemPrefab;
        
        [Header("Buttons")]
        public Button roomCreateOrJoinButton;
        public Button startGameButton;
        public Button devStartGameButton;
        public Button leaveGameButton;
        
        [Header("Other")]
        public Transform gridLayout;
        public TMP_InputField nameField;

        private HashSet<string> roomInfoSet = new();

        // A create request may arrive before Photon finishes joining the lobby.
        private string pendingRoomName;

        // These flags distinguish intentional navigation from a transport failure that should be recovered.
        private bool explicitLeaveRequested;
        private bool reconnecting;
        private bool returningToLobby;

        // Recovery coroutines use unscaled time because gameplay is paused while a player is disconnected.
        private Coroutine reconnectCoroutine;
        private Coroutine remoteRecoveryCoroutine;
        private Coroutine lobbyReconnectCoroutine;
        private int waitingForActorNumber = -1;

        // Runner and Drawer consult this flag before processing local gameplay input.
        public static bool InteractionsPausedForRecovery { get; private set; }
        public bool devSpawn = false;

        public int levelCounts = 3;
        public int levelUnlocked = 3;
        public int currentLevel = 0;

        public bool offline = false;
        private void Awake()
        {
            // Photon separates incompatible room protocols by Application.version.
            PhotonNetwork.GameVersion = Application.version;
            //if (Instance == null)
            //{
            //    Instance = this;
            //    GameManager.initialized = true;
            //    DontDestroyOnLoad(gameObject);
            //}
            //else if (Instance != this)
            //{
            //    Destroy(gameObject);
            //    return;
            //}

            print("Awake Before");

            if(Instance != null && Instance != this)
            {
                print("Awake Destroy");

                Destroy(this.gameObject);
                return;
            }


            Instance = this;
            SetRecoveryPause(false);
            gameObject.AddComponent<PhotonView>();
            PhotonNetwork.AllocateViewID(photonView);
            DontDestroyOnLoad(this.gameObject);

            //Transform canvas = GameObject.Find("Canvas").transform;
            //roomSelection = canvas.Find("RoomSelection").gameObject;
            //roleSelection = canvas.Find("RoleSelection").gameObject;
            //levelSelection = canvas.Find("LevelSelection").gameObject;

            //gridLayout = roomSelection.transform.Find("Scroll View/Viewport/Content");
            //nameField = roomSelection.transform.Find("RoomNameInputField").GetComponent<TMP_InputField>();
            roomSelection.SetActive(true);
            roleSelection.SetActive(false);
            levelSelection.SetActive(false);



            levelSelection.transform.Find("BackButton").GetComponent<Button>().onClick.AddListener(() => { Back2RoleSelection(); });


            roomCreateOrJoinButton.onClick.AddListener(() => { CreateJoinButton(); });
            startGameButton.onClick.AddListener(() => { LoadLevelSelection(); });
            //devStartGameButton.onClick.AddListener(() => { DevSpawnPlayers(); });

            //go.UpdateProperty(roomSelection, roleSelection, levelSelection, gridLayout, nameField);
            leaveGameButton.onClick.AddListener(() => { LeaveRoom(); });
        }
        void Start()
        {
            //if(Instance == null)
            //{
            //    Instance = this;
            //}
            //else
            //{
            //    Destroy(this);
            //}

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
            //DontDestroyOnLoad(this.gameObject);
            //Instance = this;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnSceneLoaded;
            //EventHandler.ReachDestinationEvent += OnReachDestination;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            //EventHandler.ReachDestinationEvent -= OnReachDestination;

        }




        public void LoadLevel(int level)
        {
            if (level < levelUnlocked)
            {
                currentLevel = level;
                PhotonNetwork.LoadLevel("Level_" + level);
            }
        }

        [PunRPC] public void RPC_LoadLevel(int level)
        {
            //if (true)//(PhotonNetwork.IsMasterClient)
            //{
            //    LoadLevel(level);

            //}

            if(PhotonNetwork.OfflineMode)
            {
                LoadLevel(level);
            }
            else if (PhotonNetwork.IsMasterClient)
            {
                LoadLevel(level);
            }

        }

        public void Back2RoleSelection()
        {
            if (PhotonNetwork.OfflineMode)
            {
                PhotonNetwork.LeaveRoom();
                //PhotonNetwork.LoadLevel("AllanLauncher");


                return;
            }


            roomSelection.SetActive(false);
            roleSelection.SetActive(true);
            levelSelection.SetActive(false);


        }



        public void UpdateProperty(GameObject roomSelection, GameObject roleSelection, GameObject levelSelection, Transform gridLayout, TMP_InputField nameField)
        {
            this.roomSelection = roomSelection;
            this.roleSelection = roleSelection;
            this.levelSelection = levelSelection;

            this.gridLayout = gridLayout;
            this.nameField = nameField;



        }

        public void UpdateProperty()
        {
            Transform canvas = GameObject.Find("Canvas").transform;
            roomSelection = canvas.Find("RoomSelection").gameObject;
            roleSelection = canvas.Find("RoleSelection").gameObject;
            levelSelection = canvas.Find("LevelSelection").gameObject;

            gridLayout = roomSelection.transform.Find("Scroll View/Viewport/Content");
            nameField = roomSelection.transform.Find("RoomNameInputField").GetComponent<TMP_InputField>();



            roomCreateOrJoinButton = roomSelection.transform.Find("Button").GetComponent<Button>();
            startGameButton = roleSelection.transform.Find("Start").GetComponent<Button>();
            leaveGameButton = roleSelection.transform.Find("LeaveRoomButton").GetComponent<Button>();
            devStartGameButton = roleSelection.transform.Find("DevButton").GetComponent<Button>();


            roomCreateOrJoinButton.onClick.AddListener(() => { CreateJoinButton(); });
            startGameButton.onClick.AddListener(() => { LoadLevelSelection(); });
            devStartGameButton.onClick.AddListener(() => { DevSpawnPlayers(); });
            leaveGameButton.onClick.AddListener(() => { LeaveRoom(); });

            levelSelection.transform.Find("BackButton").GetComponent<Button>().onClick.AddListener(() => { Back2RoleSelection(); });
        }


        public void BackToHomePage()
        {
            PhotonNetwork.LoadLevel("AllanLaunch");
        }

        public void BackToRoomSelectionPage()
        {
            PhotonNetwork.LoadLevel("RoleSelection");
            //LoadLevelSelection();
        }

        public void ExitGame()
        {
            LeaveRoom();
            Application.Quit();
        }

        public void OnReachDestination()
        {
            print("Enter OnReachDestination");


            if(PhotonNetwork.OfflineMode ||  PhotonNetwork.IsMasterClient)//PhotonNetwork.IsMasterClient)
            {
                if (currentLevel+1 == levelUnlocked)
                {
                    levelUnlocked++;

                    if(levelUnlocked >= levelCounts)
                    {
                        PhotonNetwork.LoadLevel("FinishGame");
                    }

                    currentLevel = levelUnlocked;
                    //LoadLevel(currentLevel);
                    photonView.RPC("RPC_LoadLevel", RpcTarget.All, currentLevel);
                }
                else
                {
                    //LoadLevel(currentLevel + 1);
                    photonView.RPC("RPC_LoadLevel", RpcTarget.All, currentLevel+1);

                }
            }

        }

        public void LoadLevelSelection()
        {
            print("Enter LoadLevelSelection");
            roomSelection.SetActive(false);
            roleSelection.SetActive(false);
            levelSelection.SetActive(true);
        }

        public void DevSpawnPlayers()
        {
            devSpawn = true;
            LoadLevelSelection();
            //LoadArena();
        }

        public void SpawnPlayer()
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
            {
                PlayerRole playerRole = (PlayerRole)(int)PhotonNetwork.LocalPlayer.CustomProperties["Role"];

                Vector3 spawnPosition = (playerRole == PlayerRole.Runner) ? new Vector3(0, 5, 0) : new Vector3(0, 0, 0);

                GameObject playerPrefab = (playerRole == PlayerRole.Runner) ? runnerPrefab : drawerPrefab;

                var r = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
                r.name = playerPrefab.name;
                if(playerRole == PlayerRole.Runner) 
                    GameObject.Find("LevelSetup").GetComponent<LevelSetup>().Init(r.GetComponent<Runner>());
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

            if (!PhotonNetwork.OfflineMode)
            {
                // Once play starts, hide and close the room. Only inactive actors reserved by PlayerTtl may rejoin.
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
                {
                    { PhotonSessionPolicy.SessionStateKey, PhotonSessionPolicy.SessionStarted },
                    { PhotonSessionPolicy.ShowRoomKey, false }
                });
            }

            //PhotonNetwork.IsMessageQueueRunning = false; // ✅ 暂停消息队列，防止 Photon 处理不完整的 ViewID
            LoadLevel(currentLevel);
            //SpawnPlayer();
        }

        private Room currentRoom;

        void JoinLobbyAfterDelay()
        {
            Debug.Log("Waiting for room cleanup...");
            PhotonNetwork.JoinLobby();
        }

        public void LeaveRoom()
        {
            //currentRoom = PhotonNetwork.CurrentRoom;

            //Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
            //photonView.RPC("RPC_RemoveRoomInfoSet", RpcTarget.All, PhotonNetwork.CurrentRoom.Name);
            //Debug.Log($"Cleaning {roomProperties["p1"]}， {roomProperties["p2"]}， {PhotonNetwork.NickName}");

            //if ((string)roomProperties["p1"] == PhotonNetwork.NickName)
            //{
            //    Debug.Log("Cleaning p1");
            //    roomProperties["p1"] = "";
            //}
            //if ((string)roomProperties["p2"] == PhotonNetwork.NickName)
            //{
            //    Debug.Log("Cleaning p2");
            //    roomProperties["p2"] = "";
            //}

            //if((string) roomProperties["p1"] == "" && (string)roomProperties["p2"] == "")
            //{
            //    roomProperties["show"] = false;
            //}
            //else
            //{
            //    roomProperties["show"] = true;
            //}


            //PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
            //RefreshRoomList();

            if (PhotonNetwork.OfflineMode)
            {
                PhotonNetwork.LoadLevel("RoleSelection");
                return;
            }
            // Explicit exits remove the actor immediately instead of reserving it for reconnection.
            explicitLeaveRequested = true;
            SetRecoveryPause(false);

            if (PhotonNetwork.IsMasterClient)
            {
                // An intentional departure ends this two-player session. Hide it immediately and
                // disable empty-room retention so the lobby cannot show a closed, unjoinable room.
                CloseSessionPermanently();
            }

            PhotonNetwork.LeaveRoom(false);
            //PhotonNetwork.LeaveLobby();
            //PhotonNetwork.JoinLobby();
            //PhotonNetwork.JoinLobby();
        }

        // Based on Loaded Scene, act differently
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("Enter OnSceneLoaded " + scene.name);

            if (scene.name.Contains("Level_"))
            {
                EventHandler.CallLevelStartEvent();

                currentLevel = int.Parse(scene.name.Split('_')[1]);

                Debug.Log($"Scene {scene.name} loaded. Spawning player...");
                if (devSpawn)
                {
                    var d = PhotonNetwork.Instantiate(drawerPrefab.name, new Vector3(0, 0, 0), Quaternion.identity);
                    d.name = drawerPrefab.name;
                    var r = PhotonNetwork.Instantiate(runnerPrefab.name, new Vector3(0, 5, 0), Quaternion.identity);
                    r.name = runnerPrefab.name;
                    GameObject.Find("LevelSetup").GetComponent<LevelSetup>().Init(r.GetComponent<Runner>());
                }
                else SpawnPlayer();

            }
            else if (scene.name == "RoleSelection")
            {


                Debug.Log("Scene RoleSelection loaded");
                UpdateProperty();

                if (PhotonNetwork.OfflineMode)
                {
                    //roomSelection.SetActive(false);
                    //roleSelection.SetActive(false);
                    //levelSelection.SetActive(true);
                    DevSpawnPlayers();
                    return;
                }


                if (PhotonNetwork.InRoom)
                {
                    roomSelection.SetActive(false);
                    roleSelection.SetActive(true);
                    levelSelection.SetActive(false);
                }
                else
                {
                    roomSelection.SetActive(true);
                    roleSelection.SetActive(false);
                    levelSelection.SetActive(false);
                }

            }
            else if (scene.name == "AllanLauncher")
            {
                GameManager.Instance = null;
                PhotonNetwork.OfflineMode = false;
                Destroy(gameObject);
            }
        }

        public void CreateJoinButton()
        {
            Debug.Log("Enter CreateJoinButton" + PhotonNetwork.InLobby);
            if (nameField.text.IsNullOrEmpty())
            {
                return;
            }

            // Preserve the user's request until the asynchronous JoinLobby callback arrives.
            pendingRoomName = nameField.text.Trim();

            if (!PhotonNetwork.InLobby)
            {
                Debug.Log("Not in lobby; queueing room creation until OnJoinedLobby.");
                if (PhotonNetwork.IsConnectedAndReady)
                    PhotonNetwork.JoinLobby();
                return;
            }

            TryCreatePendingRoom();
        }

        private void OnApplicationQuit()
        {
            explicitLeaveRequested = true;
        }

        private void TryCreatePendingRoom()
        {
            // This method is safe to call from both the button handler and OnJoinedLobby.
            if (string.IsNullOrWhiteSpace(pendingRoomName) || !PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InLobby)
                return;

            string roomName = pendingRoomName;
            pendingRoomName = null;
            PhotonNetwork.CreateRoom(roomName, PhotonSessionPolicy.CreateRoomOptions());
        }

        public void JoinButton(string name)
        {
            // Lobby entries use the exact Photon room name; this is already a direct room-ID join.
            PhotonNetwork.JoinRoom(name);
            //roomSelection.SetActive(false);
            //roleSelection.SetActive(true);

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
            // A single writer prevents p1/p2 from oscillating when both clients receive the same callbacks.
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

            // ActorNumber is stable across ReconnectAndRejoin and therefore gives deterministic display order.
            Player[] orderedPlayers = players.Values.OrderBy(player => player.ActorNumber).Take(2).ToArray();
            string playerOne = orderedPlayers.Length > 0 ? orderedPlayers[0].NickName : string.Empty;
            string playerTwo = orderedPlayers.Length > 1 ? orderedPlayers[1].NickName : string.Empty;

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.PlayerOneNameKey, playerOne },
                { PhotonSessionPolicy.PlayerTwoNameKey, playerTwo },
                { PhotonSessionPolicy.ShowRoomKey, orderedPlayers.Length > 0 }
            });
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

        /// <summary>Checks the authoritative room state before accepting a late rejoin.</summary>
        private static bool IsSessionExpired()
        {
            return PhotonNetwork.InRoom &&
                   PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonSessionPolicy.SessionStateKey, out object state) &&
                   Equals(state, PhotonSessionPolicy.SessionExpired);
        }

        /// <summary>Returns true while the host is still waiting for both players to start the match.</summary>
        private static bool IsSessionWaitingForPlayers()
        {
            return PhotonNetwork.InRoom &&
                   PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonSessionPolicy.SessionStateKey, out object state) &&
                   Equals(state, PhotonSessionPolicy.SessionActive);
        }

        /// <summary>Pauses physics and local input without stopping Photon message dispatch.</summary>
        private static void SetRecoveryPause(bool paused)
        {
            InteractionsPausedForRecovery = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        /// <summary>Restores normal gameplay state after the local actor successfully rejoins.</summary>
        private void CompleteRecovery()
        {
            reconnecting = false;
            returningToLobby = false;
            explicitLeaveRequested = false;
            SetRecoveryPause(false);

            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
        }

        /// <summary>Retries Photon ReconnectAndRejoin for the configured 30-second reservation window.</summary>
        private IEnumerator ReconnectAndRejoinWithinWindow()
        {
            reconnecting = true;
            SetRecoveryPause(true);
            float deadline = Time.realtimeSinceStartup + PhotonSessionPolicy.ReconnectWindowMilliseconds / 1000f;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (PhotonNetwork.InRoom)
                {
                    CompleteRecovery();
                    yield break;
                }

                if (PhotonNetwork.NetworkClientState == ClientState.Disconnected)
                {
                    // Reapply the protocol version before every fresh transport connection.
                    PhotonNetwork.GameVersion = Application.version;
                    PhotonNetwork.ReconnectAndRejoin();
                }

                yield return new WaitForSecondsRealtime(1f);
            }

            reconnectCoroutine = null;
            ExpireSessionAndReturnToLobby();
        }

        /// <summary>Pauses the connected peer while the other actor remains inactive in the room.</summary>
        private IEnumerator WaitForRemotePlayerRecovery(int actorNumber)
        {
            waitingForActorNumber = actorNumber;
            SetRecoveryPause(true);
            float deadline = Time.realtimeSinceStartup + PhotonSessionPolicy.ReconnectWindowMilliseconds / 1000f;

            while (PhotonNetwork.InRoom && Time.realtimeSinceStartup < deadline)
            {
                if (PhotonNetwork.CurrentRoom.Players.TryGetValue(actorNumber, out Player player) && !player.IsInactive)
                {
                    waitingForActorNumber = -1;
                    remoteRecoveryCoroutine = null;
                    SetRecoveryPause(false);
                    yield break;
                }

                if (IsSessionExpired()) break;
                yield return new WaitForSecondsRealtime(0.5f);
            }

            waitingForActorNumber = -1;
            remoteRecoveryCoroutine = null;
            ExpireSessionAndReturnToLobby();
        }

        /// <summary>Closes an unrecoverable session and routes this client back to the room-selection lobby.</summary>
        private void ExpireSessionAndReturnToLobby()
        {
            reconnecting = false;
            returningToLobby = true;
            SetRecoveryPause(false);

            if (PhotonNetwork.InRoom)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    // A timed-out or explicitly abandoned session no longer needs its recovery TTL.
                    CloseSessionPermanently();
                }

                PhotonNetwork.LeaveRoom(false);
                return;
            }

            if (SceneManager.GetActiveScene().name != "RoleSelection")
                SceneManager.LoadScene("RoleSelection");

            if (lobbyReconnectCoroutine == null)
                lobbyReconnectCoroutine = StartCoroutine(ConnectToLobbyWhenReady());
        }

        /// <summary>Waits for any in-progress disconnect before establishing a clean lobby connection.</summary>
        private IEnumerator ConnectToLobbyWhenReady()
        {
            while (!PhotonNetwork.IsConnectedAndReady)
            {
                if (PhotonNetwork.NetworkClientState == ClientState.Disconnected)
                {
                    PhotonNetwork.GameVersion = Application.version;
                    PhotonNetwork.ConnectUsingSettings();
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            lobbyReconnectCoroutine = null;
            if (!PhotonNetwork.InLobby && !PhotonNetwork.InRoom)
                PhotonNetwork.JoinLobby();
        }

        /// <summary>Closes, hides, and deletes a finished room as soon as its final actor leaves.</summary>
        private static void CloseSessionPermanently()
        {
            if (!PhotonNetwork.InRoom) return;

            // EmptyRoomTtl is retained during real connection loss, but explicit/expired sessions
            // set it to zero so an empty room is removed instead of lingering for sixty seconds.
            PhotonNetwork.CurrentRoom.IsOpen = false;
            PhotonNetwork.CurrentRoom.IsVisible = false;
            PhotonNetwork.CurrentRoom.EmptyRoomTtl = 0;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.SessionStateKey, PhotonSessionPolicy.SessionExpired },
                { PhotonSessionPolicy.ShowRoomKey, false }
            });
        }

        /// <summary>Releases role reservations only after an actor leaves permanently rather than becoming inactive.</summary>
        private static void ClearRoleSlotsForActor(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;

            foreach (string key in new[] { PhotonSessionPolicy.DrawerOwnerKey, PhotonSessionPolicy.RunnerOwnerKey })
            {
                if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object owner) || (int)owner != actorNumber)
                    continue;

                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { { key, 0 } },
                    new Hashtable { { key, actorNumber } });
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { "Role_" + actorNumber, (int)PlayerRole.None }
            });
        }

        #region Pun Callbacks
        public override void OnConnectedToMaster()
        {
            Debug.Log("Enter Callback OnConnectedToMaster");
            // ReconnectAndRejoin controls its own server transition, so normal lobby joining must stay out of its way.
            if (!reconnecting && !PhotonNetwork.InLobby)
                PhotonNetwork.JoinLobby();
        }
        public override void OnJoinedLobby()
        {
            Debug.Log("Enter Callback OnJoinedLobby");

            //Debug.Log("ding zhengasds");
            List<Transform> childs = new List<Transform>();
            foreach (Transform child in gridLayout)
            {
                childs.Add(child);
            }

            foreach (Transform child in childs)
            {
                DestroyImmediate(child.gameObject);
            }

            TryCreatePendingRoom();

            if (returningToLobby)
            {
                // A timeout may reconnect before the RoleSelection scene finishes loading.
                returningToLobby = false;
                explicitLeaveRequested = false;
                if (SceneManager.GetActiveScene().name != "RoleSelection")
                    SceneManager.LoadScene("RoleSelection");
            }
        }

        public override void OnJoinedRoom()
        {

            Debug.Log("Enter Callback OnJoinedRoom");
            Debug.Log($"Room successfully created! Now joining the room...");
            if (IsSessionExpired())
            {
                // Only the original active session may be resumed; expired rooms are cleanup-only.
                returningToLobby = true;
                PhotonNetwork.LeaveRoom(false);
                return;
            }

            CompleteRecovery();
            if (SceneManager.GetActiveScene().name == "RoleSelection" && roomSelection != null)
            {
                roomSelection.SetActive(false);
                roleSelection.SetActive(true);
                levelSelection.SetActive(false);
            }


            UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);


            print("Room X" + PhotonNetwork.CurrentRoom.Name);
            photonView.RPC("RPC_AddRoomInfoSet", RpcTarget.All, PhotonNetwork.CurrentRoom.Name);
            Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
            // #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
            if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            {
                Debug.Log("We load the RoleSelection with host: ");

                //PhotonNetwork.LoadLevel("RoleSelection");
                //roomSelection.SetActive(false);
                //roleSelection.SetActive(true);
                //levelSelection.SetActive(false);

            }
            //RefreshRoomList();

        }

        public override void OnLeftRoom()
        {
            Debug.Log("Enter Callback OnLeftRoom");
            SetRecoveryPause(false);

            if (PhotonNetwork.OfflineMode)
            {
                PhotonNetwork.LoadLevel("AllanLauncher");
                return;
            }


            if (SceneManager.GetActiveScene().name == "FinishGame")
            {
                PhotonNetwork.LeaveLobby();
                PhotonNetwork.LoadLevel("AllanLauncher");
            }
            else
            {
                PhotonNetwork.LoadLevel("RoleSelection");
            }

            explicitLeaveRequested = false;
            //PhotonNetwork.LoadLevel("RoleSelection");
            /*
            UpdateProperty();

            SceneManager.LoadScene(0);
            if (PhotonNetwork.InLobby)
            {
                Debug.Log("OnLeftRoom: go back to room selection page");
                roomSelection.SetActive(true);
                roleSelection.SetActive(false);
                levelSelection.SetActive(false);

            }
            */



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
        public override void OnPlayerEnteredRoom(Player other)
        {
            Debug.Log("Enter Callback OnPlayerEnteredRoom");

            Debug.LogFormat("OnPlayerEnteredRoom() {0}", other.NickName); // not seen if you're the player connecting

            if (PhotonNetwork.IsMasterClient)
            {
                Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

                UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);
            }

            if (waitingForActorNumber == other.ActorNumber)
            {
                if (remoteRecoveryCoroutine != null)
                    StopCoroutine(remoteRecoveryCoroutine);
                remoteRecoveryCoroutine = null;
                waitingForActorNumber = -1;
                SetRecoveryPause(false);
            }


        }

        public override void OnPlayerLeftRoom(Player other)
        {
            Debug.LogFormat("Enter Callback OnPlayerLeftRoom: {0} left room", other.NickName); // seen when other disconnects

            if (other.IsInactive)
            {
                // Inactive means an unexpected disconnect with a reserved actor slot, not an explicit leave.
                if (remoteRecoveryCoroutine != null)
                    StopCoroutine(remoteRecoveryCoroutine);
                remoteRecoveryCoroutine = StartCoroutine(WaitForRemotePlayerRecovery(other.ActorNumber));
                return;
            }

            ClearRoleSlotsForActor(other.ActorNumber);
            UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);

            if (IsSessionWaitingForPlayers())
            {
                // Before gameplay starts, a client's explicit exit only frees its seat. The host
                // remains in the open room and can wait for a replacement player to join.
                SetRecoveryPause(false);
                return;
            }

            // Started or host-closed sessions are atomic: an explicit departure returns both sides to the lobby.
            ExpireSessionAndReturnToLobby();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            Debug.Log($"Master Client switched to actor {newMasterClient.ActorNumber}.");
            // The new master becomes the sole writer for lobby-visible player names.
            if (PhotonNetwork.IsMasterClient)
                UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"Photon disconnected: {cause}");
            // User-requested disconnects must never trigger an automatic rejoin.
            if (explicitLeaveRequested || returningToLobby || PhotonNetwork.OfflineMode || cause == DisconnectCause.DisconnectByClientLogic)
                return;

            if (reconnectCoroutine == null)
                reconnectCoroutine = StartCoroutine(ReconnectAndRejoinWithinWindow());
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!reconnecting) return;

            Debug.LogWarning($"ReconnectAndRejoin failed ({returnCode}): {message}");
            // Return to Disconnected so the unscaled retry loop can make another clean attempt.
            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            Debug.Log("Enter Callback OnRoomListUpdate");

            //foreach (Transform child in gridLayout)
            //{
            //    DestroyImmediate(child.gameObject);
            //}


            List<string> activeRooms = new List<string>();
            List<string> removingRooms = new List<string>();
            foreach (RoomInfo room in roomList)
            {
                if (room.RemovedFromList)
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


            List<Transform> toRemove = new List<Transform>();
            foreach (Transform child in gridLayout)
            {
                if (removingRooms.Contains(child.name) || activeRooms.Contains(child.name))
                {
                    toRemove.Add(child);

                }
            }

            foreach (Transform child in toRemove)
            {
                DestroyImmediate(child.gameObject);
            }

            foreach (RoomInfo r in roomList)
            {
                print(r.Name + r.RemovedFromList);
            }

            roomList.RemoveAll(room => room.RemovedFromList);
            print("____");
            foreach (RoomInfo room in roomList)
            {

                // Delta updates can still contain a retained but closed room. Never recreate a UI
                // entry unless the server says that the room is open and its lobby flag is enabled.
                bool shouldShowRoom = room.IsOpen &&
                                      room.CustomProperties.TryGetValue(PhotonSessionPolicy.ShowRoomKey, out object showValue) &&
                                      showValue is bool showRoom &&
                                      showRoom;
                if (!shouldShowRoom)
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
        #endregion
    }
}
