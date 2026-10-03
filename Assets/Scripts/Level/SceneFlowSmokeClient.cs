#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using Allan;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Opt-in two-Player scene test. No object is created during normal launches.</summary>
public sealed class SceneFlowSmokeClient : MonoBehaviourPunCallbacks
{
    private const string StageKey = "smokeSceneStage", AckKey = "smokeSceneAck";
    private string room;
    private bool create;
    private RolesManager.PlayerRole role;
    private int observedStage = -1, acknowledgedStage = -1, advancedStage = -1;
    private GameObject previousRunner;
    private bool seededRoom;
    private bool requestedReturn;
    private float deadline;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        string argument = args.FirstOrDefault(a => a.StartsWith("-egaku-scene-smoke="));
        if (argument == null) return;
        // Explicit flags and Development-only compilation keep test room writes out of real sessions.
        var client = new GameObject("SceneFlowSmokeClient").AddComponent<SceneFlowSmokeClient>();
        client.room = argument.Substring("-egaku-scene-smoke=".Length);
        client.create = args.Contains("-egaku-smoke-host");
        client.role = args.Contains("-egaku-smoke-runner") ? RolesManager.PlayerRole.Runner : RolesManager.PlayerRole.Drawer;
        DontDestroyOnLoad(client.gameObject);
    }

    private void Start()
    {
        deadline = Time.realtimeSinceStartup + 90f;
        PhotonNetwork.NickName = "Smoke" + role;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        { { "Role", (int)role }, { "Eyes", 1 }, { "Mouth", 1 }, { "Color", Vector3.one } });
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        if (create) PhotonNetwork.CreateRoom(room, PhotonSessionPolicy.CreateRoomOptions());
        else PhotonNetwork.JoinRoom(room);
    }
    public override void OnJoinRoomFailed(short code, string message) => Fail("join failed " + code);
    public override void OnCreateRoomFailed(short code, string message) => Fail("create failed " + code);
    public override void OnJoinedRoom()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "Role", (int)role } });
        // Skip role-selection UI tests as requested; seed the existing role contract in this test room.
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel(LevelCatalog.SelectionScene);
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup > deadline) { Fail("timeout at stage " + observedStage); return; }
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount != 2 || GameManager.Instance == null) return;
        var properties = PhotonNetwork.CurrentRoom.CustomProperties;
        if (!properties.ContainsKey(StageKey))
        {
            if (!PhotonNetwork.IsMasterClient || seededRoom) return;
            var players = PhotonNetwork.PlayerList;
            // The real selection UI clears unclaimed player roles on entry. Test-only names
            // identify requested roles until authoritative room slots have been seeded.
            var runner = players.FirstOrDefault(p => p.NickName == "SmokeRunner");
            var drawer = players.FirstOrDefault(p => p.NickName == "SmokeDrawer");
            if (runner == null || drawer == null) return;
            seededRoom = true;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { StageKey, 0 }, { PhotonSessionPolicy.RunnerOwnerKey, runner.ActorNumber },
                { PhotonSessionPolicy.DrawerOwnerKey, drawer.ActorNumber },
                { PhotonSessionPolicy.RunnerReadyActorKey, runner.ActorNumber },
                { PhotonSessionPolicy.DrawerReadyActorKey, drawer.ActorNumber },
                { PhotonSessionPolicy.RoleSelectionPhaseKey, PhotonSessionPolicy.RoleSelectionPhaseLevels },
                { PhotonSessionPolicy.SessionStateKey, PhotonSessionPolicy.SessionStarted }
            });
            return;
        }
        int stage = (int)properties[StageKey];
        if (stage != observedStage)
        {
            observedStage = stage;
            requestedReturn = false;
            deadline = Time.realtimeSinceStartup + 45f;
            Debug.Log($"[SceneFlowSmoke] stage={stage} actor={PhotonNetwork.LocalPlayer.ActorNumber} role={role} master={PhotonNetwork.MasterClient.ActorNumber}");
        }
        if (stage == 11)
        {
            Debug.Log("[SceneFlowSmoke] PASS: loads, two refreshes, peer return, last level, finish and Master handoff.");
            Application.Quit(0);
            enabled = false;
            return;
        }
        if ((stage == 3 || stage == 7) && !PhotonNetwork.IsMasterClient && !requestedReturn)
        {
            requestedReturn = true;
            GameManager.Instance.BackToRoomSelectionPage();
        }
        string expected = stage == 0 || stage == 3 || stage == 7 || stage == 8 ? LevelCatalog.SelectionScene :
            stage == 4 ? "Level_19" : stage == 5 ? "Level_20" : stage == 6 ? LevelCatalog.FinishScene : "Level_3";
        if (SceneManager.GetActiveScene().name != expected) return;
        var setup = LevelSetup.FindInScene(SceneManager.GetActiveScene());
        if (setup != null && (!setup.IsGameplayReady || GameManager.InteractionsPausedForRecovery)) return;
        if (stage == 2 || stage == 10)
        {
            int required = stage == 2 ? 1 : 2;
            if (!properties.ContainsKey(PhotonSessionPolicy.RecoveryRefreshCounterKey) ||
                (int)properties[PhotonSessionPolicy.RecoveryRefreshCounterKey] < required ||
                (int)properties[PhotonSessionPolicy.RecoveryRefreshEpochKey] != 0 ||
                ReferenceEquals(Runner.Instance.gameObject, previousRunner)) return;
        }
        if (stage == 8 && PhotonNetwork.MasterClient.ActorNumber == 1) return;
        if (acknowledgedStage != stage)
        {
            if (stage == 1 || stage == 9) previousRunner = Runner.Instance.gameObject;
            acknowledgedStage = stage;
            if (setup != null)
            {
                var body = Runner.Instance.GetComponent<Rigidbody2D>();
                Debug.Log($"[SceneFlowSmoke] ready stage={stage} actor={PhotonNetwork.LocalPlayer.ActorNumber} runnerOwner={Runner.Instance.photonView.OwnerActorNr} master={PhotonNetwork.MasterClient.ActorNumber} simulated={body.simulated} bodyType={body.bodyType}");
            }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { AckKey, stage } });
        }
        // Each step advances only after both independent processes see the expected state.
        if (advancedStage == stage || !PhotonNetwork.IsMasterClient || !PhotonNetwork.PlayerList.All(p => Equals(p.CustomProperties[AckKey], stage))) return;
        advancedStage = stage;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { StageKey, stage + 1 } });
        switch (stage + 1)
        {
            case 1: case 9: GameManager.Instance.LoadLevel(3); break;
            case 2: GameManager.Instance.RequestLevelRefresh(); break;
            case 10:
                GameManager.Instance.RequestLevelRefresh();
                // Change Master while the second epoch is pending, exercising adoption of its cleanup/load barrier.
                PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]);
                break;
            case 4: GameManager.Instance.LoadLevel(19); break;
            case 5: case 6: EventHandler.CallReachDestinationEvent(); break;
            case 8: PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]); break;
        }
    }

    private void Fail(string reason)
    {
        Debug.LogError("[SceneFlowSmoke] FAIL: " + reason);
        Application.Quit(1);
        enabled = false;
    }
}
#endif
