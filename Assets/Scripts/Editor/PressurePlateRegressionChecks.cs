#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Runs the real pressure managers and Unity colliders without Relay/Vivox or
/// client enter/exit messages. Batch: -executeMethod PressurePlateRegressionChecks.RunBatch
/// </summary>
public static class PressurePlateRegressionChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;

    public static void RunBatch()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
                throw new InvalidOperationException("Run in an idle, isolated validation project.");

            assertions = 0;
            CheckStage(false);
            CheckStage(true);
            Debug.Log($"[PressureRegression] PASS: {assertions} assertions, Fortress + MagicEscape.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckStage(bool magic)
    {
        using (var fixture = new Fixture(magic))
        {
            fixture.Tick();
            Equal(0, fixture.Mask, "empty plates");

            // Replicated transform arrives after the old enter command would
            // have been rejected. No physics step or client resend follows.
            fixture.First.transform.position = fixture.Left.transform.position;
            fixture.Tick();
            Equal(1, fixture.Mask, "late movement detected without enter RPC");
            fixture.First.transform.position = Vector3.forward * 100f;
            fixture.Tick();
            Equal(0, fixture.Mask, "leaving/respawning clears occupancy without exit RPC");

            fixture.First.transform.position = fixture.Left.transform.position;
            fixture.Second.transform.position = fixture.Right.transform.position;
            fixture.Tick();
            Equal(3, fixture.Mask, "two distinct players detected");
            Equal(fixture.PressurePhase, fixture.Phase, "standing briefly does not skip hold time");

            fixture.Left.enabled = false;
            fixture.Tick();
            Equal(2, fixture.Mask, "disabled plate cannot stay occupied");
            fixture.Left.enabled = true;
            fixture.First.gameObject.SetActive(false);
            fixture.Tick();
            Equal(2, fixture.Mask, "inactive player cannot hold plate");
            fixture.First.gameObject.SetActive(true);
            fixture.First.enabled = false;
            fixture.Tick();
            Equal(2, fixture.Mask, "disabled character cannot hold plate");
            fixture.First.enabled = true;

            NetworkServer.connections.Remove(fixture.SecondConnection.connectionId);
            fixture.Tick();
            Equal(1, fixture.Mask, "disconnected player removed while object still exists");
            NetworkServer.connections.Add(fixture.SecondConnection.connectionId, fixture.SecondConnection);

            // Both boxes overlap one player: mask alone must not satisfy co-op.
            fixture.First.transform.position = Vector3.zero;
            fixture.Second.transform.position = Vector3.forward * 100f;
            Vector3 originalSize = fixture.Left.size;
            fixture.Left.size = fixture.Right.size = new Vector3(30f, 1.8f, 3.6f);
            Physics.SyncTransforms();
            fixture.Set("pressureProgress", 3.5f);
            fixture.Tick();
            Equal(3, fixture.Mask, "one player may overlap both test regions");
            Equal(fixture.PressurePhase, fixture.Phase, "one player cannot satisfy two-player gate");
            fixture.Left.size = fixture.Right.size = originalSize;
            Physics.SyncTransforms();

            fixture.First.transform.position = fixture.Left.transform.position;
            fixture.Second.transform.position = fixture.Right.transform.position;
            fixture.Set("pressureProgress", 3.5f);
            fixture.Tick();
            Equal(fixture.PressurePhase + 1, fixture.Phase, "completed hold advances gate");

            // Entering the phase with stationary players must work, including
            // Stage4 where the rune puzzle finishes after they reach the plates.
            fixture.Set("phase", magic ? (int)MagicEscapePhase.SplitCipher : (int)FortressPhase.RuneCipher);
            fixture.Set("pressureMask", 0);
            fixture.Set("pressureProgress", 0f);
            foreach (HashSet<uint> occupants in fixture.Get<Dictionary<int, HashSet<uint>>>("plateOccupants").Values)
                occupants.Clear();
            fixture.Tick();
            Equal(0, fixture.Mask, "no pressure input during another phase");
            fixture.Set("phase", fixture.PressurePhase);
            fixture.Tick();
            Equal(3, fixture.Mask, "already-standing players detected at phase entry");

            // Solo host keeps the existing sequential assist.
            NetworkServer.connections.Remove(fixture.SecondConnection.connectionId);
            fixture.Set("allowSoloAssist", true);
            fixture.Set("pressureProgress", 0f);
            fixture.Set("pressureLatchedMask", 0);
            fixture.Tick();
            Equal(1, fixture.Get<int>("pressureLatchedMask"), "solo first plate latches");
            fixture.First.transform.position = fixture.Right.transform.position;
            fixture.Tick();
            Equal(fixture.PressurePhase + 1, fixture.Phase, "solo sequential plates advance");
        }
    }

    private static void Equal(int expected, int actual, string label)
    {
        if (expected != actual)
            throw new Exception($"[PressureRegression] {label}: expected {expected}, got {actual}");
        assertions++;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly Transport previousTransport = Transport.active;
        private readonly bool previousAutoSync = Physics.autoSyncTransforms;
        private readonly NetworkBehaviour manager;
        public readonly CharacterController First;
        public readonly CharacterController Second;
        public readonly NetworkConnectionToClient SecondConnection;
        public readonly BoxCollider Left;
        public readonly BoxCollider Right;
        public readonly int PressurePhase;
        public int Mask => Get<int>("pressureMask");
        public int Phase => Get<int>("phase");

        public Fixture(bool magic)
        {
            Physics.autoSyncTransforms = false;
            Transport.active = Create("RegressionTransport").AddComponent<TelepathyTransport>();
            NetworkServer.listen = false; // No socket, external service, or user session.
            NetworkServer.Listen(8);
            Left = Plate(magic, 0, new Vector3(-5f, 0.12f, 0f));
            Right = Plate(magic, 1, new Vector3(5f, 0.12f, 0f));
            First = Player(1, out _);
            Second = Player(2, out SecondConnection);
            Physics.SyncTransforms();

            GameObject root = Create("PressureManager");
            manager = magic ? (NetworkBehaviour)root.AddComponent<MagicEscapeGameManager>() : root.AddComponent<FortressGameManager>();
            Invoke(manager, "Awake");
            InitializeIdentity(root);
            NetworkServer.Spawn(root);
            PressurePhase = magic ? (int)MagicEscapePhase.Counterweights : (int)FortressPhase.Counterweights;
            Set("phase", PressurePhase);
            Set("allowSoloAssist", false);
        }

        private GameObject Create(string name)
        {
            GameObject go = new GameObject(name);
            objects.Add(go);
            return go;
        }

        private BoxCollider Plate(bool magic, int index, Vector3 position)
        {
            GameObject go = Create("PressurePlate_" + index);
            go.transform.position = position;
            BoxCollider area = go.AddComponent<BoxCollider>();
            area.center = new Vector3(0f, 0.8f, 0f);
            area.size = new Vector3(3.6f, 1.8f, 3.6f);
            area.isTrigger = true;
            if (magic) go.AddComponent<MagicEscapePressurePlate>().Configure(index, null);
            else go.AddComponent<CoopPressurePlate>().Configure(index, null);
            return area;
        }

        private CharacterController Player(int id, out NetworkConnectionToClient connection)
        {
            GameObject go = Create("Player_" + id);
            go.transform.position = Vector3.forward * 100f;
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            CharacterController character = go.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = Vector3.up * 0.9f;
            InitializeIdentity(go);
            NetworkServer.Spawn(go);
            connection = new NetworkConnectionToClient(id);
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, identity);
            NetworkServer.connections.Add(id, connection);
            return character;
        }

        private static void InitializeIdentity(GameObject go)
            => Invoke(go.GetComponent<NetworkIdentity>(), "InitializeNetworkBehaviours");

        public void Tick() => Invoke(manager, "Update");
        public void Set(string name, object value) => manager.GetType().GetField(name, PrivateInstance).SetValue(manager, value);
        public T Get<T>(string name) => (T)manager.GetType().GetField(name, PrivateInstance).GetValue(manager);

        public void Dispose()
        {
            NetworkServer.connections.Clear();
            foreach (GameObject go in objects)
            {
                if (go == null) continue;
                if (go.GetComponent<Transport>() != null) continue;
                if (go.TryGetComponent(out NetworkIdentity identity) && identity.isServer)
                    NetworkServer.UnSpawn(go);
                Object.DestroyImmediate(go);
            }
            NetworkServer.Shutdown();
            Object.DestroyImmediate(Transport.active.gameObject);
            Transport.active = previousTransport;
            Physics.autoSyncTransforms = previousAutoSync;
        }
    }

    private static void Invoke(object target, string name)
        => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
}
#endif
