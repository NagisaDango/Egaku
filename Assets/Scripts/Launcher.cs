using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using WebSocketSharp;


namespace Phantom
{
    public class Launcher : MonoBehaviourPunCallbacks
    {
        #region Private Serializable Fields

        /// <summary>
        /// The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created.
        /// </summary>
        [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
        [SerializeField]
        private byte maxPlayersPerRoom = 4;

        #endregion

        #region Private Fields

        // Tracks exactly one user intent across Photon's asynchronous connection callbacks.
        private enum LaunchRequest
        {
            None,
            OnlineLobby,
            OfflineGame
        }

        private LaunchRequest launchRequest;

        #endregion

        #region Public Fields

        [Tooltip("The Ui Panel to let the user enter name, connect and play")]
        [SerializeField]
        private GameObject controlPanel;
        [Tooltip("The UI Label to inform the user that the connection is in progress")]
        [SerializeField]
        private GameObject progressLabel;
        [SerializeField] private GameObject facePanel;
        
        #endregion
        
        #region MonoBehaviour CallBacks

        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity during early initialization phase.
        /// </summary>
        void Awake()
        {
            // #Critical
            // this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
            PhotonNetwork.AutomaticallySyncScene = true;
            // Application.version is the network protocol boundary for incompatible releases.
            PhotonNetwork.GameVersion = Application.version;
        }

        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity during initialization phase.
        /// </summary>
        void Start()
        {
            PhotonNetwork.SendRate = 30;
            PhotonNetwork.SerializationRate = 15;
            //Connect();
            progressLabel.SetActive(false);
            controlPanel.SetActive(true);
        }

        #endregion


        #region Public Methods
        
        /// <summary>
        /// Start the connection process.
        /// - If already connected, we attempt joining a random room
        /// - if not yet connected, Connect this application instance to Photon Cloud Network
        /// </summary>
        public void Connect()
        {
            if (PhotonNetwork.NickName.IsNullOrEmpty()) return;

            progressLabel.SetActive(true);
            controlPanel.SetActive(false);
            facePanel.SetActive(false);
            AudioManager.PlayOne(AudioManager.CLICKSFX, false);
            // Store the request before connecting so OnConnectedToMaster never guesses user intent.
            launchRequest = LaunchRequest.OnlineLobby;
            PhotonNetwork.GameVersion = Application.version;

            if (PhotonNetwork.IsConnectedAndReady)
            {
                ContinueToOnlineLobby();
            }
            else
            {
                Debug.Log("Launcher: Connecting");
                PhotonNetwork.ConnectUsingSettings();
            }

        }


        public void OfflineConnect()
        {
            if(PhotonNetwork.NickName.IsNullOrEmpty()) return;

            progressLabel.SetActive(true);
            controlPanel.SetActive(false);
            facePanel.SetActive(false);
            AudioManager.PlayOne(AudioManager.CLICKSFX, false);
            // OfflineMode can only be enabled safely after any live Photon connection finishes disconnecting.
            launchRequest = LaunchRequest.OfflineGame;

            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            else
            {
                EnterOfflineGame();
            }
        }

        private void ContinueToOnlineLobby()
        {
            // Each state advances once; this method never starts a second connection.
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LoadLevel("RoleSelection");
            }
            else if (PhotonNetwork.InLobby)
            {
                PhotonNetwork.LoadLevel("RoleSelection");
            }
            else
            {
                PhotonNetwork.JoinLobby();
            }
        }

        private void EnterOfflineGame()
        {
            // Offline room creation is synchronous, but uses the same protocol defaults as online rooms.
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("OfflineRoom", PhotonSessionPolicy.CreateRoomOptions());
            PhotonNetwork.LoadLevel("RoleSelection");
            launchRequest = LaunchRequest.None;
        }

        #endregion


        #region MonoBehaviourPunCallbacks Callbacks

        public override void OnConnectedToMaster()
        {
            Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN");
            if (launchRequest == LaunchRequest.OnlineLobby)
                ContinueToOnlineLobby();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            if (launchRequest == LaunchRequest.OfflineGame)
            {
                // This callback is the handoff point requested by OfflineConnect.
                EnterOfflineGame();
                return;
            }

            progressLabel.SetActive(false);
            controlPanel.SetActive(true);
            facePanel.SetActive(true);
            launchRequest = LaunchRequest.None;
            Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log("PUN Basics Tutorial/Launcher:OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

            // #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
            PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = maxPlayersPerRoom });
        }

        public override void OnJoinedLobby()
        {
            if (launchRequest != LaunchRequest.OnlineLobby) return;

            PhotonNetwork.LoadLevel("RoleSelection");
            launchRequest = LaunchRequest.None;
        }

        public override void OnJoinedRoom()
        {
            //Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.");
            //// #Critical: We only load if we are the first player, else we rely on `PhotonNetwork.AutomaticallySyncScene` to sync our instance scene.
            //if (PhotonNetwork.CurrentRoom.PlayerCount == 1)
            //{
            //    Debug.Log("We load the RoleSelection");

            //    // #Critical
            //    // Load the Room Level.
            //    PhotonNetwork.LoadLevel("RoleSelection");
            //}
        }

        #endregion


    }
}
