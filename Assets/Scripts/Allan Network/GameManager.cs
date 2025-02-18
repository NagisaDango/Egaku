using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;
using Photon.Pun.Demo.PunBasics;
using static RolesManager;

namespace Allan
{
    public class GameManager : MonoBehaviourPunCallbacks
    {

        #region Photon Callbacks

        /// <summary>
        /// Called when the local player left the room. We need to load the launcher scene.
        /// </summary>
        public override void OnLeftRoom()
        {
            SceneManager.LoadScene(0);
        }

        #endregion

        #region Public Field
        
        public static GameManager Instance; 
        [Tooltip("The prefab to use for representing the player")]
        public GameObject runnerPrefab;
        public GameObject drawerPrefab;
        
        #endregion

        #region Public Methods

        public void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }

        #endregion

        #region Private Methods

        private void Awake()
        {
            
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

        void SpawnPlayer()
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Role"))
            {
                PlayerRole playerRole = (PlayerRole)(int)PhotonNetwork.LocalPlayer.CustomProperties["Role"];

                Vector3 spawnPosition = (playerRole == PlayerRole.Runner) ? new Vector3(0, 5, 0) : new Vector3(0, 0, 0);

                GameObject playerPrefab = (playerRole == PlayerRole.Runner) ? runnerPrefab : drawerPrefab;

                PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
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
            PhotonNetwork.LoadLevel("Allan");
            //SpawnPlayer();
        }

        #endregion



        #region Photon Callbacks

        public override void OnPlayerEnteredRoom(Player other)
        {
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
            if (scene.name == "Allan")
            {
                Debug.Log("Scene Allan loaded. Spawning player...");
                SpawnPlayer();
            }
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

        #endregion
    }
}