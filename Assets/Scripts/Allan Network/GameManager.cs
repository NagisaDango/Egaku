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
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Allan
{
    public partial class GameManager : MonoBehaviourPunCallbacks
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
        // Device choices are local presentation/input state. They are never Photon
        // room properties: online peers may use different devices independently.
        public bool keyboardRunnerInLocal = true;
        // Retained for existing launcher serialization; online gameplay now detects
        // the active device locally instead of requiring a pre-game choice.
        public bool onlineUseGamepad;
        // The selection scene owns presentation and releases its button listeners when destroyed.
        private SelectionSceneBindings selectionBindings;
        
        public GameObject roomItemPrefab;
        
        [Header("Buttons")]
        public Button roomCreateOrJoinButton;
        // Legacy RoleSelect buttons are retained in history for reference. OnlineSelection
        // now owns role confirmation, readiness, leaving, and progression.
        // public Button startGameButton;
        // public Button devStartGameButton;
        // public Button leaveGameButton;
        
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

        // Offline Photon owns both role objects on one client; input routing still
        // assigns one physical device to each local player.
        public static bool IsLocalMultiplayer => PhotonNetwork.OfflineMode && Instance != null && Instance.devSpawn;

        public int levelCounts = 3;
        public int levelUnlocked = 3;
        public int currentLevel = 0;
        // Retain serialized unlock counts: today's test scene starts fully unlocked.
        private LevelCatalog levelCatalog;
        private bool sceneLoadInFlight;
        private bool returnHomeAfterLeave;
        private Coroutine gameplayReadyCoroutine;
        // Departure is scene-scoped, but the persistent flow owner survives a missing/disabled mask.
        private Coroutine departureCoroutine;
        private bool sceneFlowFailed;
        [SerializeField, Min(1f)] private float sceneReadyTimeout = 30f;
        // Commands use room properties because this persistent object's PhotonView does not
        // survive scene changes reliably. Only the Master executes shared navigation.
        private const string ReturnSelectionRequestKey = "sceneReturnSelectionRequest";
        private const string UnlockedLevelsKey = "sceneUnlockedLevels";

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
            // Local device assignment is a machine preference, never a Photon role property.
            keyboardRunnerInLocal = EgakuSettings.KeyboardRunnerInLocal;
            SetRecoveryPause(false);
            DontDestroyOnLoad(this.gameObject);

            levelCatalog = LevelCatalog.Load();
            if (levelCatalog == null) Debug.LogError("LevelCatalog is missing; scene navigation is unavailable.", this);
            else levelCounts = levelCatalog.levels.Count;
            UpdateProperty();

        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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
            // A duplicate scene manager is destroyed at end of frame; it must not bind the
            // fresh scene UI or register callbacks during that interval.
            if (Instance != this) return;
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

            if (PhotonNetwork.InRoom)
            {
                // Returning from level selection is a shared lobby transition. Clearing
                // both ready actors lets either player revise roles without split-screen state.
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
                {
                    { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseRoles },
                    { PhotonSessionPolicy.RunnerReadyActorKey, 0 },
                    { PhotonSessionPolicy.DrawerReadyActorKey, 0 }
                });
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
            // Each selection scene owns its references. Rebinding is idempotent, including
            // repeated callbacks on the same scene; persistent serialized fields remain compatible.
            Scene selectionScene = SceneManager.GetSceneByName(LevelCatalog.SelectionScene);
            if (!selectionScene.IsValid() || !selectionScene.isLoaded) return;
            SelectionSceneBindings bindings = selectionScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SelectionSceneBindings>(true)).FirstOrDefault();
            if (bindings == null)
            {
                Debug.LogError("RoleSelection is missing SelectionSceneBindings.", this);
                return;
            }
            roomSelection = bindings.roomSelection;
            roleSelection = bindings.roleSelection;
            levelSelection = bindings.levelSelection;
            gridLayout = bindings.gridLayout;
            nameField = bindings.nameField;
            roomCreateOrJoinButton = bindings.createJoinButton;
            selectionBindings = bindings;
            bindings.Bind(this);
        }

        /// <summary>Configures the existing room panel for direct private-code matchmaking.</summary>

        /// <summary>Shows the private code in the waiting room so the host can share it with one client.</summary>

        public void ExitGame()
        {
            LeaveRoom();
            Application.Quit();
        }

        public bool CanStartSelectedMode()
        {
            // The offline button now starts local two-player play. Do not launch a
            // level with a role that has no physical controller assigned.
            if (PhotonNetwork.OfflineMode && devSpawn &&
                !InputDeviceRouter.BothClaimed)
            {
                Debug.LogWarning("Both local roles must claim different devices before starting.");
                return false;
            }
            return true;
        }

        public void DevSpawnPlayers()
        {
            // Kept under its serialized legacy name; the offline launcher now uses
            // this two-role spawn path for keyboard/mouse plus gamepad co-op.
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
                    LevelSetup.FindInScene(SceneManager.GetActiveScene()).Init(r.GetComponent<Runner>());
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
                BackToRoomSelectionPage();
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

        // Preserve serialized/public entry points while presentation stays with the scene.
        public void LoadLevelSelection() { if (selectionBindings != null) selectionBindings.LoadLevelSelection(); }
        private void ApplyRoleSelectionPhase() { if (selectionBindings != null) selectionBindings.ApplyRoleSelectionPhase(); }
        private void ShowCurrentRoomCode() { if (selectionBindings != null) selectionBindings.ShowCurrentRoomCode(); }

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
            ResetLocalRecoveryRefreshState();
            if (!completedReconnect)
            {
                // Photon player properties can survive a room change. A new room must
                // not inherit a scene-load acknowledgement for the same epoch number.
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
                {
                    { PhotonSessionPolicy.RecoveryTargetAckKey, 0 }
                });
            }
            CompleteRecovery();
            bool resumedExistingRefresh = ResumeExistingRecoverySceneRefresh();
            if (SceneManager.GetActiveScene().name == "RoleSelection" && roomSelection != null)
            {
                ApplyRoleSelectionPhase();
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
            ResetLocalRecoveryRefreshState();
            SetRecoveryPause(false);

            if (returnHomeAfterLeave || PhotonNetwork.OfflineMode)
            {
                returnHomeAfterLeave = false;
                PhotonNetwork.LoadLevel(LevelCatalog.LauncherScene);
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
            {
                UpdateRoomPlayerList(PhotonNetwork.CurrentRoom.Players);
                // A former non-Master may already have acknowledged its local initialization.
                recoveryTargetLoadIssued = recoveryRefreshEpoch > 0 &&
                    recoveryLocalLoadCompletedEpoch == recoveryRefreshEpoch;
                // The new Master resumes the same epoch instead of waiting for another property change.
                TryAdvanceRecoverySceneRefreshToLoad();
                TryCompleteSharedRecoverySceneRefresh();
            }
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

            if (propertiesThatChanged.TryGetValue(UnlockedLevelsKey, out object unlocked) && unlocked is int count)
                levelUnlocked = Mathf.Clamp(count, 0, levelCounts);
            if (PhotonNetwork.IsMasterClient && propertiesThatChanged.ContainsKey(ReturnSelectionRequestKey))
                ReturnTogetherToLevelSelection();
            if (propertiesThatChanged.TryGetValue(PhotonSessionPolicy.RecoveryRefreshEpochKey, out object completed) &&
                completed is int completedEpoch && completedEpoch == 0 && recoveryRefreshInProgress)
            {
                // Epoch zero is published only after both gameplay-ready acknowledgements.
                ResetLocalRecoveryRefreshState();
                SetRecoveryInputPause(false);
            }

            if (propertiesThatChanged.ContainsKey(PhotonSessionPolicy.RoleSelectionPhaseKey) &&
                SceneManager.GetActiveScene().name == "RoleSelection")
                ApplyRoleSelectionPhase();

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
