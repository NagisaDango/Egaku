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
    // Same component, Photon callbacks and serialized identity. Recovery logic is isolated here
    // so changes to the epoch handshake can be reviewed independently of UI and progression.
    public partial class GameManager
    {
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

        /// <summary>Acknowledges local gameplay readiness; shared epoch completion releases input.</summary>
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
            // Keep both clients paused until the Master's room-property completion arrives.
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
            // Stay paused until the epoch-zero echo releases the barrier on both peers.
            recoveryTargetLoadIssued = false;
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

        /// <summary>
        /// Refresh epochs start at one in each room. Forget the previous room's local
        /// completion before joining another room, or its first refresh looks finished.
        /// </summary>
        private void ResetLocalRecoveryRefreshState()
        {
            StopRecoveryRefreshTimeout();
            if (recoveryLocalReloadCoroutine != null)
            {
                StopCoroutine(recoveryLocalReloadCoroutine);
                recoveryLocalReloadCoroutine = null;
            }

            recoveryRefreshInProgress = false;
            recoveryTargetLoadIssued = false;
            recoveryLocalReloadIssued = false;
            recoveryRefreshEpoch = 0;
            recoveryLocalLoadCompletedEpoch = 0;
            recoveryRefreshTargetScene = null;
        }

        private IEnumerator ExpireStalledRecoveryRefresh()
        {
            // A failed scene handshake must never leave both players permanently paused.
            yield return new WaitForSecondsRealtime(PhotonSessionPolicy.ReconnectWindowMilliseconds / 1000f);
            recoveryRefreshTimeoutCoroutine = null;
            if (!recoveryRefreshInProgress || !PhotonNetwork.InRoom) yield break;

            // A timeout is failure, never a synthetic successful Ready barrier. Retire the session
            // through the existing leave flow so two incomplete copies cannot resume divergent physics.
            FailSceneFlow($"Recovery scene refresh {recoveryRefreshEpoch} timed out");
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
                { "Role_" + actorNumber, (int)PlayerRole.None },
                { PhotonSessionPolicy.RunnerReadyActorKey, 0 },
                { PhotonSessionPolicy.DrawerReadyActorKey, 0 },
                { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseRoles }
            });
        }

    }
}
