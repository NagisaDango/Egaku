using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;


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

        /// <summary>
        /// This client's version number. Users are separated from each other by gameVersion (which allows you to make breaking changes).
        /// </summary>
        string gameVersion = "1";

        /// <summary>
        /// Keep track of the current process. Since connection is asynchronous and is based on several callbacks from Photon,
        /// we need to keep track of this to properly adjust the behavior when we receive call back by Photon.
        /// Typically this is used for the OnConnectedToMaster() callback.
        /// </summary>
        bool isConnecting;

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
            progressLabel.SetActive(true);
            controlPanel.SetActive(false);
            facePanel.SetActive(false);
            AudioManager.PlayOne(AudioManager.CLICKSFX, false);
            // we check if we are connected or not, we join if we are , else we initiate the connection to the server.
            if (PhotonNetwork.IsConnected)
            {
                // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
                //PhotonNetwork.JoinRandomRoom();
                Debug.Log("Launcher: Join Lobby After Cliking Connect Button");
                PhotonNetwork.JoinLobby();
            }
            else
            {
                Debug.Log("Launcher: Connecting");
                // #Critical, we must first and foremost connect to Photon Online Server.
                // keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
                isConnecting = PhotonNetwork.ConnectUsingSettings();
                //PhotonNetwork.GameVersion = gameVersion;
            }

        }


        public void OfflineConnect()
        {
            progressLabel.SetActive(true);
            controlPanel.SetActive(false);
            facePanel.SetActive(false);
            AudioManager.PlayOne(AudioManager.CLICKSFX, false);
            // we check if we are connected or not, we join if we are , else we initiate the connection to the server.

            PhotonNetwork.Disconnect();
            PhotonNetwork.OfflineMode = true;
            //RoomOptions roomOptions = new RoomOptions();
            //PhotonNetwork.CreateRoom("OfflineRoom", roomOptions);
            //PhotonNetwork.JoinRoom("OfflineRoom");

            //if (PhotonNetwork.IsConnected)
            //{
            //    // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
            //    //PhotonNetwork.JoinRandomRoom();
            //    Debug.Log("Launcher: Join Lobby After Cliking Connect Button");
            //    PhotonNetwork.JoinLobby();
            //}
            //else
            //{
            //    Debug.Log("Launcher: Connecting");
            //    // #Critical, we must first and foremost connect to Photon Online Server.
            //    // keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
            //    isConnecting = PhotonNetwork.ConnectUsingSettings();
            //    //PhotonNetwork.GameVersion = gameVersion;
            //}
        }

        #endregion


        #region MonoBehaviourPunCallbacks Callbacks

        public override void OnConnectedToMaster()
        {
            Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN");

            // #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
            // we don't want to do anything if we are not attempting to join a room.
            // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
            // we don't want to do anything.
            if (PhotonNetwork.OfflineMode)
            {
                RoomOptions roomOptions = new RoomOptions();
                PhotonNetwork.CreateRoom("OfflineRoom", roomOptions);
                PhotonNetwork.LoadLevel("RoleSelection");
                return;
            }


            if (isConnecting)
            {
                // #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
                //PhotonNetwork.JoinRandomRoom();
                PhotonNetwork.JoinLobby();
                //isConnecting = false;
            }
            else
            {
                // #Critical, we must first and foremost connect to Photon Online Server.
                // keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
                isConnecting = PhotonNetwork.ConnectUsingSettings();
                //PhotonNetwork.GameVersion = gameVersion;
            }

        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            progressLabel.SetActive(false);
            controlPanel.SetActive(true);
            isConnecting = false;
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
            PhotonNetwork.LoadLevel("RoleSelection");
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