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
    // Navigation, initialization and visual deadlines share the original manager identity.
    // Only the current Master commits shared scene loads; recovery remains in its own module.
    public partial class GameManager
    {
        public void LoadLevel(int level)
        {
            if (sceneLoadInFlight || recoveryRefreshInProgress ||
                (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)) return;
            LevelCatalog.Definition target = levelCatalog != null ? levelCatalog.Find(level) : null;
            if (target == null || !levelCatalog.IsUnlocked(level, levelUnlocked) ||
                !Application.CanStreamedLevelBeLoaded(target.sceneName))
            {
                Debug.LogWarning($"Cannot load level {level}: missing scene/catalog entry or locked level.");
                return;
            }
            sceneLoadInFlight = true;
            currentLevel = level;
            PhotonNetwork.LoadLevel(target.sceneName);
        }

        public void BackToHomePage()
        {
            // Home leaves the session; returning to level selection preserves both actors and roles.
            returnHomeAfterLeave = true;
            explicitLeaveRequested = true;
            ClearPendingRoomRequest();
            if (PhotonNetwork.InRoom)
            {
                if (!PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient) CloseSessionPermanently();
                PhotonNetwork.LeaveRoom(false);
            }
            else PhotonNetwork.LoadLevel(LevelCatalog.LauncherScene);
        }

        public void BackToRoomSelectionPage()
        {
            if (sceneLoadInFlight || recoveryRefreshInProgress) return;
            if (PhotonNetwork.OfflineMode || PhotonNetwork.IsMasterClient)
                ReturnTogetherToLevelSelection();
            else if (PhotonNetwork.InRoom)
            {
                Hashtable properties = PhotonNetwork.CurrentRoom.CustomProperties;
                bool hasRequest = properties.TryGetValue(ReturnSelectionRequestKey, out object value);
                int previous = hasRequest ? (int)value : 0;
                // Photon CAS cannot create an absent property. Initialize the first request
                // without an expectation; subsequent clicks compare the existing counter.
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { ReturnSelectionRequestKey, previous + 1 } },
                    hasRequest ? new Hashtable { { ReturnSelectionRequestKey, previous } } : null);
            }
        }

        private void ReturnTogetherToLevelSelection()
        {
            if (sceneLoadInFlight || recoveryRefreshInProgress || !PhotonNetwork.InRoom ||
                (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)) return;
            string active = SceneManager.GetActiveScene().name;
            if (active == LevelCatalog.SelectionScene) return;
            sceneLoadInFlight = true;
            // Retire spawned players/strokes and their cached RPCs before retaining the room.
            // Room role slots and ready actors stay intact so both peers can select another level.
            PhotonNetwork.DestroyAll();
            if (!PhotonNetwork.OfflineMode)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
                {
                    { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseLevels },
                    { UnlockedLevelsKey, levelUnlocked }
                });
            PhotonNetwork.LoadLevel(LevelCatalog.SelectionScene);
        }

        public void OnReachDestination()
        {
            if (sceneLoadInFlight || recoveryRefreshInProgress || levelCatalog == null ||
                (!PhotonNetwork.OfflineMode && !PhotonNetwork.IsMasterClient)) return;
            levelUnlocked = levelCatalog.UnlockedAfter(currentLevel, levelUnlocked);
            if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { UnlockedLevelsKey, levelUnlocked } });
            LevelCatalog.Definition next = levelCatalog.Next(currentLevel);
            // Completing the last entry finishes the game, independently of unlock counts.
            if (next == null)
            {
                sceneLoadInFlight = true;
                PhotonNetwork.LoadLevel(LevelCatalog.FinishScene);
                return;
            }
            LoadLevel(next.id);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("Enter OnSceneLoaded " + scene.name);
            sceneLoadInFlight = false;
            sceneFlowFailed = false;
            if (departureCoroutine != null) StopCoroutine(departureCoroutine);
            departureCoroutine = null;
            if (gameplayReadyCoroutine != null) StopCoroutine(gameplayReadyCoroutine);
            gameplayReadyCoroutine = null;

            LevelCatalog.Definition definition = levelCatalog != null ? levelCatalog.FindScene(scene.name) : null;
            if (definition != null)
            {
                currentLevel = definition.id;

                var context = GameplaySceneContext.FindInScene(scene);
                if ((scene.name != "Level_0" && (context == null || !context.IsConfigured)) ||
                    LevelSetup.FindInScene(scene) == null)
                {
                    FailSceneFlow("Missing or invalid gameplay scene references: " + scene.name);
                    return;
                }

                Debug.Log($"Scene {scene.name} loaded. Spawning player...");
                if (devSpawn)
                {
                    var d = PhotonNetwork.Instantiate(drawerPrefab.name, new Vector3(0, 0, 0), Quaternion.identity);
                    d.name = drawerPrefab.name;
                    var r = PhotonNetwork.Instantiate(runnerPrefab.name, new Vector3(0, 5, 0), Quaternion.identity);
                    r.name = runnerPrefab.name;
                    LevelSetup.FindInScene(scene).Init(r.GetComponent<Runner>());
                }
                else SpawnPlayer();

                // Start callbacks, remote role arrivals, camera binding and Drawer UI must finish
                // before this actor acknowledges recovery. Never treat sceneLoaded as gameplay-ready.
                gameplayReadyCoroutine = StartCoroutine(WaitForGameplayReady(scene, recoveryRefreshEpoch));

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
                    ApplyRoleSelectionPhase();
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

        private IEnumerator WaitForGameplayReady(Scene scene, int epoch)
        {
            yield return null;
            LevelSetup setup = LevelSetup.FindInScene(scene);
            float deadline = Time.realtimeSinceStartup + sceneReadyTimeout;
            while (scene.IsValid() && SceneManager.GetActiveScene() == scene &&
                   (setup == null || !setup.IsGameplayReady))
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    gameplayReadyCoroutine = null;
                    FailSceneFlow("Gameplay initialization timed out in " + scene.name +
                        "; Runner ready=" + (Runner.Instance != null && Runner.Instance.SceneReady) +
                        ", Drawer ready=" + (Drawer.Instance != null && Drawer.Instance.SceneReady));
                    yield break;
                }
                yield return null;
            }
            gameplayReadyCoroutine = null;
            if (!scene.IsValid() || SceneManager.GetActiveScene() != scene) yield break;
            EventHandler.CallLevelStartEvent();
            if (epoch == recoveryRefreshEpoch) CompleteLocalRecoverySceneRefresh(scene.name);
        }

        public void BeginLevelDeparture(LevelTransition transition)
        {
            if (departureCoroutine != null || sceneFlowFailed || sceneLoadInFlight || InteractionsPausedForRecovery) return;
            // Both peers receive the existing RPC. Only the current Master may commit progression.
            // Schedule the flow first: exceptions or destruction in the visual cannot strand the level.
            departureCoroutine = StartCoroutine(CompleteLevelDeparture(SceneManager.GetActiveScene(),
                transition, transition != null ? Mathf.Clamp(transition.outTime, 0f, 30f) : 0f));
            if (transition != null) transition.PlayClosingVisual();
        }

        private IEnumerator CompleteLevelDeparture(Scene scene, LevelTransition transition, float duration)
        {
            // Unscaled timing also finishes a transition if a local menu pauses gameplay.
            yield return new WaitForSecondsRealtime(duration);
            // The timer and visual resume in unspecified frame order. Close exactly before committing the load.
            if (transition != null) transition.FinishClosingVisual();
            // Keep the peer's accepted departure alive until the scene changes. If it becomes Master
            // after its visual deadline, it can still commit the same transition without a second RPC.
            while (scene.IsValid() && scene == SceneManager.GetActiveScene() && !sceneFlowFailed &&
                !PhotonNetwork.OfflineMode && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
                yield return null;
            departureCoroutine = null;
            if (scene.IsValid() && scene == SceneManager.GetActiveScene() && !sceneFlowFailed)
                OnReachDestination();
        }

        private void FailSceneFlow(string reason)
        {
            if (sceneFlowFailed) return;
            sceneFlowFailed = true;
            Debug.LogError("[SceneFlow] " + reason + ". Returning to the room entry instead of resuming an unready level.", this);
            // Use the existing session-expiry/leave callbacks, not a new failure protocol.
            // Closing/leaving tells the other actor to exit; no gameplay Ready acknowledgement is fabricated.
            ResetLocalRecoveryRefreshState();
            ExpireSessionAndReturnToLobby();
            // LeaveRoom is asynchronous; keep the failing gameplay copy input-blocked until navigation completes.
            if (SceneManager.GetActiveScene().name != LevelCatalog.SelectionScene) SetRecoveryInputPause(true);
        }
    }
}
