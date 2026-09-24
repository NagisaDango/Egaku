using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Centralizes the room protocol shared by the launcher, lobby, role selection, and reconnect flow.
/// Keep these keys and timing values stable within one Application.version.
/// </summary>
public static class PhotonSessionPolicy
{
    private const string PlayerIdCommandLinePrefix = "-egaku-player-id=";
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // ReconnectAndRejoin only needs the identity to survive reconnect attempts in this running process.
    // Do not store it in PlayerPrefs: two local standalone clients share those preferences and would then
    // authenticate as the same Photon user, preventing the second client from joining the first client's room.
    private static string processScopedUserId;

    // Six characters provide a shareable code while excluding easily confused glyphs such as O/0 and I/1.
    public const int RoomCodeLength = 6;
    public const int MaximumRoomCodeLength = 12;

    // An inactive actor keeps its actor number, role, and network objects for this long.
    public const int ReconnectWindowMilliseconds = 30_000;

    // The room must outlive the player TTL so both players can recover from a simultaneous outage.
    public const int EmptyRoomRetentionMilliseconds = 60_000;

    // Player names remain room state for diagnostics; private rooms do not publish them to a lobby list.
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

    // Recovery refresh state. The Master names the level to rebuild; each actor acknowledges
    // its restored scene before the request is cleared.
    public const string RecoveryRefreshEpochKey = "recovery_refresh_epoch";
    public const string RecoveryRefreshCounterKey = "recovery_refresh_counter";
    public const string RecoveryRefreshTargetKey = "recovery_refresh_target";
    public const string RecoveryRefreshRequestKey = "recovery_refresh_request";
    public const string RecoveryTargetAckKey = "recovery_target_ack";

    /// <summary>
    /// Creates the only supported online/offline room configuration for this Application.version.
    /// </summary>
    public static RoomOptions CreateRoomOptions()
    {
        return new RoomOptions
        {
            // Room codes are shared directly, so rooms must never be exposed through the public lobby list.
            IsVisible = false,
            IsOpen = true,
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
                { SessionStateKey, SessionActive },
                { RecoveryRefreshEpochKey, 0 },
                { RecoveryRefreshCounterKey, 0 },
                { RecoveryRefreshTargetKey, string.Empty },
                { RecoveryRefreshRequestKey, 0 }
            }
        };
    }

    /// <summary>Returns an uppercase alphanumeric room code safe for use as a Photon room name.</summary>
    public static string NormalizeRoomCode(string rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return string.Empty;

        char[] normalized = new char[Math.Min(rawCode.Length, MaximumRoomCodeLength)];
        int length = 0;
        foreach (char character in rawCode.ToUpperInvariant())
        {
            if (!char.IsLetterOrDigit(character)) continue;
            normalized[length++] = character;
            if (length == MaximumRoomCodeLength) break;
        }

        return new string(normalized, 0, length);
    }

    /// <summary>Generates a short private-room code that can be read aloud or copied between players.</summary>
    public static string GenerateRoomCode()
    {
        char[] code = new char[RoomCodeLength];
        for (int index = 0; index < code.Length; index++)
            code[index] = RoomCodeAlphabet[UnityEngine.Random.Range(0, RoomCodeAlphabet.Length)];

        return new string(code);
    }

    /// <summary>
    /// Ensures Photon receives the same process-scoped UserId on reconnect. This identifies a returning
    /// player to Photon; it is not account authentication and must not be treated as a security credential.
    /// </summary>
    public static string EnsureStableUserIdentity()
    {
        if (PhotonNetwork.AuthValues != null && !string.IsNullOrWhiteSpace(PhotonNetwork.AuthValues.UserId))
            return PhotonNetwork.AuthValues.UserId;

        string userId = GetCommandLinePlayerId();
        if (string.IsNullOrEmpty(userId))
        {
            if (string.IsNullOrEmpty(processScopedUserId))
                processScopedUserId = "egaku-session-" + Guid.NewGuid().ToString("N");

            userId = processScopedUserId;
        }

        PhotonNetwork.AuthValues = new AuthenticationValues(userId);
        return userId;
    }

    /// <summary>Allows two local test builds to use distinct identities despite sharing Unity PlayerPrefs.</summary>
    private static string GetCommandLinePlayerId()
    {
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (!argument.StartsWith(PlayerIdCommandLinePrefix, StringComparison.OrdinalIgnoreCase)) continue;

            string overrideId = NormalizeRoomCode(argument.Substring(PlayerIdCommandLinePrefix.Length));
            if (!string.IsNullOrEmpty(overrideId)) return "egaku-test-" + overrideId;
        }

        return string.Empty;
    }

    /// <summary>Maps a gameplay role to its authoritative room-property slot.</summary>
    public static string GetRoleOwnerKey(RolesManager.PlayerRole role)
    {
        return role == RolesManager.PlayerRole.Drawer ? DrawerOwnerKey : RunnerOwnerKey;
    }
}
