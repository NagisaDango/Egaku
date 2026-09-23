using ExitGames.Client.Photon;
using Photon.Realtime;

/// <summary>
/// Centralizes the room protocol shared by the launcher, lobby, role selection, and reconnect flow.
/// Keep these keys and timing values stable within one Application.version.
/// </summary>
public static class PhotonSessionPolicy
{
    // An inactive actor keeps its actor number, role, and network objects for this long.
    public const int ReconnectWindowMilliseconds = 30_000;

    // The room must outlive the player TTL so both players can recover from a simultaneous outage.
    public const int EmptyRoomRetentionMilliseconds = 60_000;

    // Lobby-visible properties used to render the two player names.
    public const string PlayerOneNameKey = "p1";
    public const string PlayerTwoNameKey = "p2";
    public const string ShowRoomKey = "show";

    // Actor-number ownership slots. A value of zero means the role is available.
    public const string DrawerOwnerKey = "role_drawer_actor";
    public const string RunnerOwnerKey = "role_runner_actor";

    // Session state prevents an expired or already-started match from accepting unrelated players.
    public const string SessionStateKey = "session_state";

    public const string SessionActive = "active";
    public const string SessionStarted = "started";
    public const string SessionExpired = "expired";

    /// <summary>
    /// Creates the only supported online/offline room configuration for protocol version 0.2.
    /// </summary>
    public static RoomOptions CreateRoomOptions()
    {
        return new RoomOptions
        {
            MaxPlayers = 2,
            CleanupCacheOnLeave = true,
            PlayerTtl = ReconnectWindowMilliseconds,
            EmptyRoomTtl = EmptyRoomRetentionMilliseconds,
            CustomRoomProperties = new Hashtable
            {
                { PlayerOneNameKey, string.Empty },
                { PlayerTwoNameKey, string.Empty },
                { ShowRoomKey, false },
                { DrawerOwnerKey, 0 },
                { RunnerOwnerKey, 0 },
                { SessionStateKey, SessionActive }
            },
            CustomRoomPropertiesForLobby = new[]
            {
                PlayerOneNameKey,
                PlayerTwoNameKey,
                ShowRoomKey
            }
        };
    }

    /// <summary>Maps a gameplay role to its authoritative room-property slot.</summary>
    public static string GetRoleOwnerKey(RolesManager.PlayerRole role)
    {
        return role == RolesManager.PlayerRole.Drawer ? DrawerOwnerKey : RunnerOwnerKey;
    }
}
