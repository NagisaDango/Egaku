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

        // A room-code request may arrive before Photon finishes returning to the Master Server.
        private enum PrivateRoomRequest
        {
            None,
            Create,
            Join
        }

        private string pendingRoomCode;
        private PrivateRoomRequest pendingRoomRequest;
        private bool roomRequestInFlight;

        // These flags distinguish intentional navigation from a transport failure that should be recovered.
        private bool explicitLeaveRequested;
        private bool reconnecting;
        private bool returningToLobby;

        // Recovery coroutines use unscaled time because gameplay is paused while a player is disconnected.
        private Coroutine reconnectCoroutine;
        private Coroutine remoteRecoveryCoroutine;
        private Coroutine lobbyReconnectCoroutine;
        private int waitingForActorNumber = -1;

        // The Master requests a rebuild in room properties; every actor reloads locally and acknowledges
        // the restored level so same-scene refreshes work without PUN's scene synchronization shortcut.
        private bool recoveryRefreshInProgress;
        private bool recoveryTargetLoadIssued;
        private bool recoveryLocalReloadIssued;
        private int recoveryRefreshEpoch;
        private int recoveryLocalLoadCompletedEpoch;
        private string recoveryRefreshTargetScene;
        private Coroutine recoveryRefreshTimeoutCoroutine;
        private Coroutine recoveryLocalReloadCoroutine;

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
            // A stable Photon UserId prevents a different actor from claiming a reserved reconnect slot.
            PhotonSessionPolicy.EnsureStableUserIdentity();
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
            ConfigurePrivateRoomCodeUi();
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
            ConfigurePrivateRoomCodeUi();
        }

        /// <summary>Configures the existing room panel for direct private-code matchmaking.</summary>
        private void ConfigurePrivateRoomCodeUi()
        {
            if (nameField != null)
            {
                nameField.characterLimit = PhotonSessionPolicy.MaximumRoomCodeLength;
                nameField.contentType = TMP_InputField.ContentType.Alphanumeric;
                if (nameField.placeholder is TMP_Text placeholderLabel)
                    placeholderLabel.text = "Room code (blank = create)";
            }

            if (roomCreateOrJoinButton != null)
            {
                TMP_Text buttonLabel = roomCreateOrJoinButton.GetComponentInChildren<TMP_Text>();
                if (buttonLabel != null) buttonLabel.text = "Create / Join Code";
            }

            if (gridLayout != null && gridLayout.parent != null && gridLayout.parent.parent != null)
            {
                // Private rooms are never listed, so hide the legacy public-room scroll view.
                gridLayout.parent.parent.gameObject.SetActive(false);
            }
        }

        /// <summary>Shows the private code in the waiting room so the host can share it with one client.</summary>
        private void ShowCurrentRoomCode()
        {
            if (roleSelection == null || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;

            Transform existingLabel = roleSelection.transform.Find("PrivateRoomCodeLabel");
            TMP_Text codeLabel;
            if (existingLabel == null)
            {
                // The label is runtime-only so legacy scenes remain untouched and every scene reload gets fresh state.
                GameObject labelObject = new GameObject(
                    "PrivateRoomCodeLabel",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(roleSelection.transform, false);

                RectTransform labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0.5f, 1f);
                labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -24f);
                labelRect.sizeDelta = new Vector2(600f, 60f);

                codeLabel = labelObject.GetComponent<TextMeshProUGUI>();
                codeLabel.alignment = TextAlignmentOptions.Center;
                codeLabel.fontSize = 30f;
                codeLabel.fontStyle = FontStyles.Bold;
                codeLabel.color = Color.black;
                codeLabel.raycastTarget = false;
            }
            else
            {
                codeLabel = existingLabel.GetComponent<TMP_Text>();
            }

            codeLabel.text = $"Room Code: {PhotonNetwork.CurrentRoom.Name}";
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
                    LoadLevel(currentLevel);
                }
                else
                {
                    //LoadLevel(currentLevel + 1);
                    LoadLevel(currentLevel + 1);

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
            if (TryGetLocalAssignedRole(out PlayerRole playerRole))
            {
                Vector3 spawnPosition = (playerRole == PlayerRole.Runner) ? new Vector3(0, 5, 0) : new Vector3(0, 0, 0);

                GameObject playerPrefab = (playerRole == PlayerRole.Runner) ? runnerPrefab : drawerPrefab;

                var r = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
                r.name = playerPrefab.name;
                if(playerRole == PlayerRole.Runner) 
                    GameObject.Find("LevelSetup").GetComponent<LevelSetup>().Init(r.GetComponent<Runner>());
            }
            else
            {
                Debug.LogError($"Cannot spawn local player: actor {PhotonNetwork.LocalPlayer.ActorNumber} has no valid role in player or room properties.");
            }
        }

        /// <summary>Recovers the local role from the room's authoritative actor slots if its player property is missing.</summary>
        private static bool TryGetLocalAssignedRole(out PlayerRole role)
        {
            role = PlayerRole.None;
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null || PhotonNetwork.CurrentRoom == null)
                return false;

            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            if (TryReadPlayerRole(PhotonNetwork.LocalPlayer.CustomProperties, "Role", out role))
                return true;

            if (TryReadPlayerRole(PhotonNetwork.CurrentRoom.CustomProperties, "Role_" + actorNumber, out role) ||
                ReadRoomRoleOwner(PhotonSessionPolicy.DrawerOwnerKey, actorNumber, PlayerRole.Drawer, out role) ||
                ReadRoomRoleOwner(PhotonSessionPolicy.RunnerOwnerKey, actorNumber, PlayerRole.Runner, out role))
            {
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Role", (int)role } });
                Debug.LogWarning($"Restored missing player role for actor {actorNumber} from room role ownership.");
                return true;
            }

            return false;
        }

        private static bool TryReadPlayerRole(Hashtable properties, string key, out PlayerRole role)
        {
            role = PlayerRole.None;
            if (properties == null || !properties.TryGetValue(key, out object value) || !(value is int roleValue))
                return false;

            if (roleValue != (int)PlayerRole.Drawer && roleValue != (int)PlayerRole.Runner)
                return false;

            role = (PlayerRole)roleValue;
            return true;
        }

        private static bool ReadRoomRoleOwner(string key, int actorNumber, PlayerRole candidate, out PlayerRole role)
        {
            role = PlayerRole.None;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object owner) ||
                !(owner is int ownerActor) || ownerActor != actorNumber)
                return false;

            role = candidate;
            return true;
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
            // Multiple UnityEvent bindings must not send LeaveRoom again while Photon is already leaving.
            if (!PhotonNetwork.InRoom || explicitLeaveRequested) return;

            // Explicit exits remove the actor immediately instead of reserving it for reconnection.
            explicitLeaveRequested = true;
            ClearPendingRoomRequest();
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

                CompleteLocalRecoverySceneRefresh(scene.name);

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
                    ShowCurrentRoomCode();
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
            // Serialized and runtime UnityEvent bindings may both invoke this handler in the legacy scene.
            // Only one Photon operation may be queued or in flight at a time.
            if (PhotonNetwork.InRoom || roomRequestInFlight || pendingRoomRequest != PrivateRoomRequest.None)
                return;

            // A blank field creates a readable code; entering a code only joins that exact private room.
            string roomCode = PhotonSessionPolicy.NormalizeRoomCode(nameField != null ? nameField.text : string.Empty);
            pendingRoomRequest = string.IsNullOrEmpty(roomCode) ? PrivateRoomRequest.Create : PrivateRoomRequest.Join;
            if (pendingRoomRequest == PrivateRoomRequest.Create) roomCode = PhotonSessionPolicy.GenerateRoomCode();

            if (nameField != null) nameField.text = roomCode;
            pendingRoomCode = roomCode;
            TryStartPendingRoomRequest();
        }

        private void OnApplicationQuit()
        {
            explicitLeaveRequested = true;
        }

        private void TryStartPendingRoomRequest()
        {
            // This method is safe from the button and connection callbacks; Photon accepts it on the Master Server.
            if (roomRequestInFlight || string.IsNullOrWhiteSpace(pendingRoomCode) ||
                !PhotonNetwork.IsConnectedAndReady || PhotonNetwork.InRoom)
                return;

            string roomCode = pendingRoomCode;
            PrivateRoomRequest request = pendingRoomRequest;
            bool requestStarted = false;
            if (request == PrivateRoomRequest.Create)
                requestStarted = PhotonNetwork.CreateRoom(roomCode, PhotonSessionPolicy.CreateRoomOptions(), TypedLobby.Default);
            else if (request == PrivateRoomRequest.Join)
                requestStarted = PhotonNetwork.JoinRoom(roomCode);

            if (!requestStarted) return;

            roomRequestInFlight = true;
            pendingRoomCode = null;
            pendingRoomRequest = PrivateRoomRequest.None;
        }

        private void ClearPendingRoomRequest()
        {
            pendingRoomCode = null;
            pendingRoomRequest = PrivateRoomRequest.None;
            roomRequestInFlight = false;
        }

        public void JoinButton(string name)
        {
            // Retained for serialized legacy buttons; all joins still resolve through the exact room code.
            if (PhotonNetwork.InRoom || roomRequestInFlight || pendingRoomRequest != PrivateRoomRequest.None)
                return;

            pendingRoomCode = PhotonSessionPolicy.NormalizeRoomCode(name);
            pendingRoomRequest = PrivateRoomRequest.Join;
            TryStartPendingRoomRequest();
            //roomSelection.SetActive(false);
            //roleSelection.SetActive(true);

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

        /// <summary>Returns true after role selection has committed the room to gameplay.</summary>
        private static bool IsSessionStarted()
        {
            return PhotonNetwork.InRoom &&
                   PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PhotonSessionPolicy.SessionStateKey, out object state) &&
                   Equals(state, PhotonSessionPolicy.SessionStarted);
        }

        /// <summary>Pauses physics and local input without stopping Photon message dispatch.</summary>
        private static void SetRecoveryPause(bool paused)
        {
            InteractionsPausedForRecovery = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        /// <summary>Blocks gameplay input during a scene refresh without freezing Photon callbacks or Unity initialization.</summary>
        private static void SetRecoveryInputPause(bool paused)
        {
            InteractionsPausedForRecovery = paused;
        }

        /// <summary>Reads the active recovery refresh request shared by the Master Client.</summary>
        private static bool TryGetRecoveryRefreshRequest(out int epoch, out string targetScene)
        {
            epoch = 0;
            targetScene = string.Empty;
            if (!PhotonNetwork.InRoom || IsSessionExpired()) return false;

            Hashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!properties.TryGetValue(PhotonSessionPolicy.RecoveryRefreshEpochKey, out object epochValue) ||
                !properties.TryGetValue(PhotonSessionPolicy.RecoveryRefreshTargetKey, out object targetValue))
                return false;

            epoch = (int)epochValue;
            targetScene = targetValue as string;
            return epoch > 0 && !string.IsNullOrEmpty(targetScene);
        }

        /// <summary>Returns true only after both reserved actors are online and ready for a synchronized reset.</summary>
        private static bool AreBothRoomPlayersActive()
        {
            return PhotonNetwork.InRoom &&
                   PhotonNetwork.CurrentRoom.Players.Count == 2 &&
                   PhotonNetwork.CurrentRoom.Players.Values.All(player => !player.IsInactive);
        }

        /// <summary>Begins a full two-client level rebuild after an inactive actor successfully rejoins.</summary>
        private void TryBeginRecoverySceneRefresh()
        {
            if (recoveryRefreshInProgress || !PhotonNetwork.IsMasterClient || IsSessionExpired() ||
                !AreBothRoomPlayersActive())
                return;

            string activeScene = SceneManager.GetActiveScene().name;
            if (!activeScene.StartsWith("Level_")) return;

            int previousEpoch = 0;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                    PhotonSessionPolicy.RecoveryRefreshCounterKey, out object previousEpochValue))
                previousEpoch = (int)previousEpochValue;

            recoveryRefreshInProgress = true;
            recoveryTargetLoadIssued = false;
            recoveryLocalReloadIssued = false;
            recoveryLocalLoadCompletedEpoch = 0;
            recoveryRefreshEpoch = previousEpoch + 1;
            recoveryRefreshTargetScene = activeScene;
            SetRecoveryInputPause(true);
            StartRecoveryRefreshTimeout();

            // Clear runtime instantiations and Photon room caches before sending the reload request.
            PhotonNetwork.DestroyAll();

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.RecoveryRefreshEpochKey, recoveryRefreshEpoch },
                { PhotonSessionPolicy.RecoveryRefreshCounterKey, recoveryRefreshEpoch },
                { PhotonSessionPolicy.RecoveryRefreshTargetKey, recoveryRefreshTargetScene },
                { PhotonSessionPolicy.RecoveryRefreshReadyKey, 0 },
                { GetRecoveryCleanupAckKey(PhotonNetwork.LocalPlayer.ActorNumber), recoveryRefreshEpoch }
            });

            // Wait for every actor to receive DestroyAll before allowing either client to respawn.
            TryAdvanceRecoverySceneRefreshToLoad();
        }

        /// <summary>Routes the in-game reset button through the Master-owned full scene rebuild.</summary>
        public void RequestLevelRefresh()
        {
            if (PhotonNetwork.OfflineMode)
            {
                LoadLevel(currentLevel);
                return;
            }

            if (!PhotonNetwork.InRoom || recoveryRefreshInProgress) return;

            if (PhotonNetwork.IsMasterClient)
                TryBeginRecoverySceneRefresh();
            else
            {
                // GameManager is a persistent scene object without a reliable PhotonView after
                // scene changes. A room property delivers this request to the current Master.
                Hashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
                int previousRequest = properties.TryGetValue(
                    PhotonSessionPolicy.RecoveryRefreshRequestKey, out object value) ? (int)value : 0;
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { { PhotonSessionPolicy.RecoveryRefreshRequestKey, previousRequest + 1 } },
                    new Hashtable { { PhotonSessionPolicy.RecoveryRefreshRequestKey, previousRequest } });
            }
        }

        private IEnumerator BeginRecoverySceneRefreshNextFrame()
        {
            yield return null;
            TryBeginRecoverySceneRefresh();
        }

        /// <summary>Adopts a refresh request that already existed when this actor joined the room.</summary>
        private bool ResumeExistingRecoverySceneRefresh()
        {
            if (!TryGetRecoveryRefreshRequest(out int epoch, out string targetScene)) return false;

            recoveryRefreshInProgress = true;
            recoveryTargetLoadIssued = false;
            recoveryLocalReloadIssued = false;
            recoveryRefreshEpoch = epoch;
            recoveryLocalLoadCompletedEpoch = 0;
            recoveryRefreshTargetScene = targetScene;
            SetRecoveryInputPause(true);
            StartRecoveryRefreshTimeout();

            // A returning actor may have missed the room-wide cleanup event while disconnected.
            // Clean only its local Photon instances before acknowledging the cleanup barrier.
            PhotonNetwork.DestroyAll(true);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { GetRecoveryCleanupAckKey(PhotonNetwork.LocalPlayer.ActorNumber), epoch }
            });
            TryAdvanceRecoverySceneRefreshToLoad();
            return true;
        }

        private static string GetRecoveryCleanupAckKey(int actorNumber)
        {
            return PhotonSessionPolicy.RecoveryCleanupAckPrefix + actorNumber;
        }

        /// <summary>Only the Master releases the reload after both actors report processing DestroyAll.</summary>
        private void TryAdvanceRecoverySceneRefreshToLoad()
        {
            if (!PhotonNetwork.IsMasterClient || !recoveryRefreshInProgress || !AreBothRoomPlayersActive())
                return;

            Hashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
            bool bothCleaned = PhotonNetwork.CurrentRoom.Players.Values.All(player =>
                properties.TryGetValue(GetRecoveryCleanupAckKey(player.ActorNumber), out object value) &&
                value is int acknowledgedEpoch && acknowledgedEpoch == recoveryRefreshEpoch);
            if (!bothCleaned)
                return;

            if (properties.TryGetValue(PhotonSessionPolicy.RecoveryRefreshReadyKey, out object readyValue) &&
                readyValue is int readyEpoch && readyEpoch == recoveryRefreshEpoch)
            {
                ScheduleLocalRecoverySceneReload(recoveryRefreshEpoch, recoveryRefreshTargetScene);
                return;
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.RecoveryRefreshReadyKey, recoveryRefreshEpoch }
            });
        }

        private void ScheduleLocalRecoverySceneReload(int epoch, string targetScene)
        {
            if (!recoveryRefreshInProgress || recoveryLocalReloadIssued || epoch != recoveryRefreshEpoch ||
                string.IsNullOrEmpty(targetScene) || !PhotonNetwork.InRoom)
                return;

            recoveryLocalReloadIssued = true;
            if (recoveryLocalReloadCoroutine != null)
                StopCoroutine(recoveryLocalReloadCoroutine);
            recoveryLocalReloadCoroutine = StartCoroutine(ReloadRecoverySceneNextFrame(epoch, targetScene));
        }

        private IEnumerator ReloadRecoverySceneNextFrame(int epoch, string targetScene)
        {
            yield return null;
            recoveryLocalReloadCoroutine = null;
            if (!PhotonNetwork.InRoom || !recoveryRefreshInProgress || epoch != recoveryRefreshEpoch ||
                targetScene != recoveryRefreshTargetScene)
                yield break;

            Debug.Log($"Reloading {targetScene} locally for recovery refresh {epoch}.");
            SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
        }

        /// <summary>Releases recovery input pause after this client rebuilt the requested gameplay scene.</summary>
        private void CompleteLocalRecoverySceneRefresh(string loadedScene)
        {
            if (!recoveryRefreshInProgress || recoveryRefreshEpoch <= 0 ||
                loadedScene != recoveryRefreshTargetScene)
                return;

            // The local Master may receive its own room-property callback after this sceneLoaded
            // callback. Remember local completion so that late callback cannot re-pause controls.
            recoveryLocalLoadCompletedEpoch = recoveryRefreshEpoch;

            // Each player confirms its own rebuilt level before the Master retires the request.
            // The Master keeps the request active until both scene loads have completed.
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.RecoveryTargetAckKey, recoveryRefreshEpoch }
            });
            if (PhotonNetwork.IsMasterClient)
                recoveryTargetLoadIssued = true;
            if (!PhotonNetwork.IsMasterClient)
            {
                recoveryRefreshInProgress = false;
                StopRecoveryRefreshTimeout();
            }
            SetRecoveryInputPause(false);
            TryCompleteSharedRecoverySceneRefresh();
        }

        private void TryCompleteSharedRecoverySceneRefresh()
        {
            if (!PhotonNetwork.IsMasterClient || !recoveryRefreshInProgress ||
                !AreBothRoomPlayersActive() || !recoveryTargetLoadIssued ||
                SceneManager.GetActiveScene().name != recoveryRefreshTargetScene)
                return;

            bool bothLoaded = PhotonNetwork.CurrentRoom.Players.Values.All(player =>
                player.CustomProperties.TryGetValue(PhotonSessionPolicy.RecoveryTargetAckKey, out object value) &&
                (int)value == recoveryRefreshEpoch);
            if (!bothLoaded) return;

            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { PhotonSessionPolicy.RecoveryRefreshEpochKey, 0 },
                { PhotonSessionPolicy.RecoveryRefreshTargetKey, string.Empty },
                { PhotonSessionPolicy.RecoveryRefreshReadyKey, 0 }
            });
            recoveryRefreshInProgress = false;
            recoveryTargetLoadIssued = false;
            recoveryLocalReloadIssued = false;
            StopRecoveryRefreshTimeout();
        }

        private void StartRecoveryRefreshTimeout()
        {
            if (recoveryRefreshTimeoutCoroutine != null)
                StopCoroutine(recoveryRefreshTimeoutCoroutine);
            recoveryRefreshTimeoutCoroutine = StartCoroutine(ExpireStalledRecoveryRefresh());
        }

        private void StopRecoveryRefreshTimeout()
        {
            if (recoveryRefreshTimeoutCoroutine == null) return;
            StopCoroutine(recoveryRefreshTimeoutCoroutine);
            recoveryRefreshTimeoutCoroutine = null;
        }

        private IEnumerator ExpireStalledRecoveryRefresh()
        {
            // A failed scene handshake must never leave both players permanently paused.
            yield return new WaitForSecondsRealtime(PhotonSessionPolicy.ReconnectWindowMilliseconds / 1000f);
            recoveryRefreshTimeoutCoroutine = null;
            if (!recoveryRefreshInProgress || !PhotonNetwork.InRoom) yield break;

            Debug.LogError($"Recovery scene refresh {recoveryRefreshEpoch} timed out; restoring local control without leaving the room.");
            SetRecoveryInputPause(false);
            recoveryRefreshInProgress = false;
            recoveryTargetLoadIssued = false;
            recoveryLocalReloadIssued = false;
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
                {
                    { PhotonSessionPolicy.RecoveryRefreshEpochKey, 0 },
                    { PhotonSessionPolicy.RecoveryRefreshTargetKey, string.Empty },
                    { PhotonSessionPolicy.RecoveryRefreshReadyKey, 0 }
                });
            }
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
                lobbyReconnectCoroutine = StartCoroutine(ConnectToRoomSelectionWhenReady());
        }

        /// <summary>Waits for any in-progress disconnect before returning to private-code matchmaking.</summary>
        private IEnumerator ConnectToRoomSelectionWhenReady()
        {
            while (!PhotonNetwork.IsConnectedAndReady)
            {
                if (PhotonNetwork.NetworkClientState == ClientState.Disconnected)
                {
                    PhotonNetwork.GameVersion = Application.version;
                    PhotonSessionPolicy.EnsureStableUserIdentity();
                    PhotonNetwork.ConnectUsingSettings();
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            lobbyReconnectCoroutine = null;
            returningToLobby = false;
            explicitLeaveRequested = false;
            if (SceneManager.GetActiveScene().name != "RoleSelection")
                SceneManager.LoadScene("RoleSelection");

            TryStartPendingRoomRequest();
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
            // ReconnectAndRejoin owns its connection path; normal room-code requests resume on the Master Server.
            if (reconnecting) return;

            if (returningToLobby)
            {
                returningToLobby = false;
                explicitLeaveRequested = false;
                if (SceneManager.GetActiveScene().name != "RoleSelection")
                    SceneManager.LoadScene("RoleSelection");
            }

            TryStartPendingRoomRequest();
        }
        public override void OnJoinedLobby()
        {
            Debug.Log("Enter Callback OnJoinedLobby (legacy compatibility)");

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

            // A client upgraded while already in a lobby can still use the same direct room-code request.
            TryStartPendingRoomRequest();

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

            ClearPendingRoomRequest();
            bool completedReconnect = reconnecting;
            CompleteRecovery();
            bool resumedExistingRefresh = ResumeExistingRecoverySceneRefresh();
            if (SceneManager.GetActiveScene().name == "RoleSelection" && roomSelection != null)
            {
                roomSelection.SetActive(false);
                roleSelection.SetActive(true);
                levelSelection.SetActive(false);
                ShowCurrentRoomCode();
            }


            UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);


            print("Room X" + PhotonNetwork.CurrentRoom.Name);
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
            if (completedReconnect && !resumedExistingRefresh)
                TryBeginRecoverySceneRefresh();
            //RefreshRoomList();

        }

        public override void OnLeftRoom()
        {
            Debug.Log("Enter Callback OnLeftRoom");
            ClearPendingRoomRequest();
            StopRecoveryRefreshTimeout();
            recoveryRefreshInProgress = false;
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

            // Only the current Master Client starts the refresh. A one-frame delay lets PUN apply the
            // reactivated actor state before the active-player gate is evaluated.
            if (PhotonNetwork.IsMasterClient &&
                SceneManager.GetActiveScene().name.StartsWith("Level_"))
                StartCoroutine(BeginRecoverySceneRefreshNextFrame());


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
            if (!reconnecting)
            {
                // Direct-code joins never create a room on typo; keep the entered code visible for correction.
                ClearPendingRoomRequest();
                Debug.LogWarning($"Room code join failed ({returnCode}): {message}");
                return;
            }

            Debug.LogWarning($"ReconnectAndRejoin failed ({returnCode}): {message}");
            // Return to Disconnected so the unscaled retry loop can make another clean attempt.
            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            // A generated-code collision is extremely unlikely; the user can clear the field to generate another.
            ClearPendingRoomRequest();
            Debug.LogWarning($"Private room creation failed ({returnCode}): {message}");
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            // Private-code protocol rooms are invisible. Ignore any legacy lobby delta instead of
            // rebuilding stale join buttons that can outlive a closed or retained Photon room.
            Debug.Log($"Ignored {roomList.Count} legacy lobby room updates in private-code mode.");
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (!PhotonNetwork.IsMasterClient || !recoveryRefreshInProgress) return;

            if (changedProps.ContainsKey(PhotonSessionPolicy.RecoveryTargetAckKey))
                TryCompleteSharedRecoverySceneRefresh();
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            Debug.Log("Enter Callback OnRoomPropertiesUpdate");
            foreach (var key in propertiesThatChanged.Keys)
            {
                Debug.Log($"Room Properly changed:{key} ->{propertiesThatChanged[key]}, ROOM:{PhotonNetwork.CurrentRoom.Name}");
            }

            if (PhotonNetwork.IsMasterClient &&
                propertiesThatChanged.ContainsKey(PhotonSessionPolicy.RecoveryRefreshRequestKey))
                TryBeginRecoverySceneRefresh();

            bool recoveryPropertyChanged = propertiesThatChanged.ContainsKey(PhotonSessionPolicy.RecoveryRefreshEpochKey) ||
                                           propertiesThatChanged.ContainsKey(PhotonSessionPolicy.RecoveryRefreshTargetKey) ||
                                           propertiesThatChanged.ContainsKey(PhotonSessionPolicy.RecoveryRefreshReadyKey) ||
                                           propertiesThatChanged.Keys.Cast<object>().Any(key =>
                                               key is string keyString && keyString.StartsWith(
                                                   PhotonSessionPolicy.RecoveryCleanupAckPrefix, System.StringComparison.Ordinal));
            if (recoveryPropertyChanged && TryGetRecoveryRefreshRequest(out int epoch, out string targetScene))
            {
                if (epoch != recoveryRefreshEpoch)
                {
                    recoveryTargetLoadIssued = false;
                    recoveryLocalReloadIssued = false;
                    recoveryLocalLoadCompletedEpoch = 0;
                }

                recoveryRefreshInProgress = true;
                recoveryRefreshEpoch = epoch;
                recoveryRefreshTargetScene = targetScene;

                // Photon delivers the room-property update to its writer asynchronously. If this
                // actor already loaded and acknowledged this exact epoch, this is only a late echo.
                if (recoveryLocalLoadCompletedEpoch == epoch)
                {
                    TryCompleteSharedRecoverySceneRefresh();
                    return;
                }

                SetRecoveryInputPause(true);
                StartRecoveryRefreshTimeout();

                string localAckKey = GetRecoveryCleanupAckKey(PhotonNetwork.LocalPlayer.ActorNumber);
                Hashtable roomProperties = PhotonNetwork.CurrentRoom.CustomProperties;
                if (!roomProperties.TryGetValue(localAckKey, out object localAck) ||
                    !(localAck is int ackEpoch) || ackEpoch != epoch)
                {
                    // The Master's reliable cleanup event precedes the room update on this connection.
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { localAckKey, epoch } });
                }

                if (PhotonNetwork.IsMasterClient)
                    TryAdvanceRecoverySceneRefreshToLoad();

                if (roomProperties.TryGetValue(PhotonSessionPolicy.RecoveryRefreshReadyKey, out object ready) &&
                    ready is int readyEpoch && readyEpoch == epoch)
                    ScheduleLocalRecoverySceneReload(epoch, targetScene);
            }
        }
        #endregion
    }
}
