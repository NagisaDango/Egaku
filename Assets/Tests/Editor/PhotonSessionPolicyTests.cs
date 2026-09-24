using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Egaku.Tests.Editor
{
    /// <summary>Guards the private-room protocol without coupling this test assembly to Assembly-CSharp.</summary>
    public sealed class PhotonSessionPolicyTests
    {
        private static Type PolicyType =>
            Type.GetType("PhotonSessionPolicy, Assembly-CSharp", throwOnError: true);

        [Test]
        public void RoomCodesAreNormalizedForExactJoin()
        {
            // Reflection keeps the editor-only test assembly independent of the game's predefined assembly.
            MethodInfo normalize = PolicyType.GetMethod("NormalizeRoomCode", BindingFlags.Public | BindingFlags.Static);
            string result = (string)normalize.Invoke(null, new object[] { " ab-c 12! " });

            Assert.That(result, Is.EqualTo("ABC12"));
        }

        [Test]
        public void GeneratedRoomCodesUseTheConfiguredLength()
        {
            MethodInfo generate = PolicyType.GetMethod("GenerateRoomCode", BindingFlags.Public | BindingFlags.Static);
            int expectedLength = (int)PolicyType.GetField("RoomCodeLength").GetRawConstantValue();
            string result = (string)generate.Invoke(null, null);

            Assert.That(result, Has.Length.EqualTo(expectedLength));
            Assert.That(result, Does.Match("^[A-Z2-9]+$"));
        }

        [Test]
        public void OnlineRoomsArePrivateAndRecoverable()
        {
            // Read RoomOptions through reflection so this test remains decoupled from Photon assemblies.
            MethodInfo createOptions = PolicyType.GetMethod("CreateRoomOptions", BindingFlags.Public | BindingFlags.Static);
            object options = createOptions.Invoke(null, null);
            Type optionsType = options.GetType();

            Assert.That(optionsType.GetProperty("IsVisible").GetValue(options), Is.False);
            Assert.That(optionsType.GetProperty("IsOpen").GetValue(options), Is.True);
            Assert.That((byte)optionsType.GetField("MaxPlayers").GetValue(options), Is.EqualTo(2));
            Assert.That((int)optionsType.GetField("PlayerTtl").GetValue(options), Is.EqualTo(30_000));
            Assert.That((int)optionsType.GetField("EmptyRoomTtl").GetValue(options), Is.EqualTo(60_000));
        }

        [Test]
        public void ProtocolVersionMatchesPrivateRoomRelease()
        {
            // Network-incompatible room rules must always be isolated by the Unity application version.
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo("0.3"));
        }
    }
}
