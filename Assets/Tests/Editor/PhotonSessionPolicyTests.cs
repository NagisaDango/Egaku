using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

            object customProperties = optionsType.GetField("CustomRoomProperties").GetValue(options);
            var properties = (System.Collections.IDictionary)customProperties;
            Assert.That(properties["recovery_refresh_epoch"], Is.EqualTo(0));
            Assert.That(properties["recovery_refresh_target"], Is.EqualTo(string.Empty));
            Assert.That(properties["recovery_refresh_request"], Is.EqualTo(0));
        }

        [Test]
        public void LocalStandaloneClientsDoNotReuseLegacyPlayerPrefsIdentity()
        {
            const string legacyPreferenceKey = "Egaku.Photon.StableUserId";
            const string legacyUserId = "egaku-shared-local-client";
            Type photonNetworkType = Type.GetType("Photon.Pun.PhotonNetwork, PhotonUnityNetworking", throwOnError: true);
            PropertyInfo authValuesProperty = photonNetworkType.GetProperty("AuthValues", BindingFlags.Public | BindingFlags.Static);
            FieldInfo processUserIdField = PolicyType.GetField("processScopedUserId", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo ensureIdentity = PolicyType.GetMethod("EnsureStableUserIdentity", BindingFlags.Public | BindingFlags.Static);
            object originalAuthValues = authValuesProperty.GetValue(null);
            string originalProcessUserId = (string)processUserIdField.GetValue(null);

            try
            {
                // Two standalone clients on one PC share PlayerPrefs, so a saved ID must not be reused.
                PlayerPrefs.SetString(legacyPreferenceKey, legacyUserId);
                authValuesProperty.SetValue(null, null);
                processUserIdField.SetValue(null, null);

                string result = (string)ensureIdentity.Invoke(null, null);

                Assert.That(result, Does.StartWith("egaku-session-"));
                Assert.That(result, Is.Not.EqualTo(legacyUserId));
            }
            finally
            {
                authValuesProperty.SetValue(null, originalAuthValues);
                processUserIdField.SetValue(null, originalProcessUserId);
                PlayerPrefs.DeleteKey(legacyPreferenceKey);
            }
        }

        [Test]
        public void NetworkProtocolVersionMatchesRelease()
        {
            // Network-incompatible room rules must always be isolated by the Unity application version.
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo("0.9"));
        }

        [Test]
        public void PackagedLevelSetupCsvIsAvailableThroughResources()
        {
            TextAsset csv = Resources.Load<TextAsset>("LevelSetup");

            Assert.That(csv, Is.Not.Null);
            Assert.That(csv.text, Does.Contain("level_id,wood,cloud,steel,electric"));
            Assert.That(csv.text, Does.Contain("1,300,-1,-1,-1"));
        }

        [Test]
        public void PenUnlockWaitsForDrawerUiDuringRecoveryReload()
        {
            Type levelSetupType = Type.GetType("LevelSetup, Assembly-CSharp", throwOnError: true);
            GameObject levelSetupObject = new GameObject("Recovery LevelSetup Test");

            try
            {
                Component levelSetup = levelSetupObject.AddComponent(levelSetupType);
                MethodInfo receiveUnlock = levelSetupType.GetMethod(
                    "RPC_EnablePen", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo pendingUnlocks = levelSetupType.GetField(
                    "pendingPenUnlocks", BindingFlags.NonPublic | BindingFlags.Instance);

                receiveUnlock.Invoke(levelSetup, new object[] { "Wood" });
                object queuedUnlocks = pendingUnlocks.GetValue(levelSetup);
                bool containsWood = (bool)queuedUnlocks.GetType().GetMethod("Contains")
                    .Invoke(queuedUnlocks, new object[] { "Wood" });

                Assert.That(containsWood, Is.True,
                    "A pickup received before Drawer UI initialization must be replayed after Init.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(levelSetupObject);
            }
        }
    }
}
