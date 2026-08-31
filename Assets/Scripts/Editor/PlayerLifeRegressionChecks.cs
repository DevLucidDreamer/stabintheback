#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated Unity batch: -executeMethod PlayerLifeRegressionChecks.RunBatch</summary>
public static class PlayerLifeRegressionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
        try
        {
            assertions = 0;
            CheckLifeCycle();
            CheckPresentation();
            Debug.Log($"[PlayerLifeRegression] PASS: {assertions} assertions.");
            PressurePlateRegressionChecks.RunBatch();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckLifeCycle()
    {
        Transport previousTransport = Transport.active;
        var transport = new GameObject("LifeRegressionTransport");
        var player = new GameObject("LifeRegressionPlayer");
        try
        {
            Transport.active = transport.AddComponent<TelepathyTransport>();
            NetworkServer.listen = false;
            NetworkServer.Listen(4);
            NetworkIdentity identity = player.AddComponent<NetworkIdentity>();
            var health = player.AddComponent<PlayerHealth>();
            Invoke(health, "Awake");
            Invoke(identity, "InitializeNetworkBehaviours");
            NetworkServer.Spawn(player);
            var connection = new NetworkConnectionToClient(1);
            typeof(NetworkConnection).GetProperty("identity").SetValue(connection, identity);
            NetworkServer.connections.Add(1, connection);
            player.transform.position = new Vector3(6f, 0f, 2f);
            Vector3 deathPosition = player.transform.position;

            health.ServerKill(99, Vector3.right);
            PlayerHealth.LifeState dead = State(health);
            Check(health.IsDead, "hit enters death state");
            Near(2f, (float)(dead.endsAt - NetworkTime.time), "two-second death delay");
            Check(!player.GetComponent<CharacterController>().enabled, "dead controller disabled");
            Check(!ServerInteractionGuard.HasPlayer(connection), "dead player cannot interact or occupy plates");
            Check(!health.IsSpawnProtected, "respawn protection does not run during death view");
            health.ServerKill(99, Vector3.forward);
            Check(State(health).deathSequence == dead.deathSequence && State(health).endsAt == dead.endsAt,
                "repeated hit cannot restart death timer");
            Invoke(health, "Update");
            Check(health.IsDead, "no immediate respawn");

            dead.endsAt = NetworkTime.time - 0.01d;
            SetState(health, dead);
            player.transform.position = Vector3.one * 50f;
            Invoke(health, "Update");
            Check(!health.IsDead && health.IsSpawnProtected, "respawn enters protection");
            Near(1.5f, health.ProtectionRemaining, "full protection begins at respawn");
            Check(player.transform.position == deathPosition, "no-start-position fallback returns to death location");
            Check(player.GetComponent<CharacterController>().enabled, "respawn controller restored");
            Check(ServerInteractionGuard.HasPlayer(connection), "protected living player may interact");
            health.ServerKill(99);
            Check(!health.IsDead, "protected hit rejected");
            var alive = State(health);
            alive.endsAt = NetworkTime.time - 0.01d;
            SetState(health, alive);
            health.ServerKill(99);
            Check(health.IsDead && State(health).deathSequence == 2, "hit works after protection expires");

            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.Write(State(health));
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    var received = reader.Read<PlayerHealth.LifeState>();
                    Check(received.dead && received.deathSequence == 2 && received.position == State(health).position,
                        "Mirror serializes life state and death pose together");
                }
            }
        }
        finally
        {
            NetworkServer.connections.Clear();
            if (player != null)
            {
                if (player.TryGetComponent(out NetworkIdentity identity) && identity.isServer) NetworkServer.UnSpawn(player);
                Object.DestroyImmediate(player);
            }
            NetworkServer.Shutdown();
            Object.DestroyImmediate(transport);
            Transport.active = previousTransport;
        }
    }

    private static void CheckPresentation()
    {
        var player = new GameObject("PresentationRegression");
        var corpse = new GameObject("CorpseRegression");
        Material original = null;
        try
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camera.transform.localRotation = Quaternion.Euler(17f, 0f, 0f);
            Vector3 savedPosition = camera.transform.localPosition;
            Quaternion savedRotation = camera.transform.localRotation;
            var controller = player.AddComponent<PlayerController>();
            Invoke(controller, "Awake");
            var visuals = player.AddComponent<PlayerRespawnVisuals>();
            corpse.AddComponent<Rigidbody>().isKinematic = true;
            Check(visuals.BeginDeathView(corpse, Vector3.zero, Quaternion.identity), "death camera starts");
            Check(Vector3.Distance(camera.transform.position, Vector3.up) > 2f, "camera moves to third-person distance");
            Check(!visuals.BeginDeathView(corpse, Vector3.zero, Quaternion.identity), "repeat update preserves original camera pose");
            Check(visuals.EndDeathView(), "death camera stops");
            Check(camera.transform.localPosition == savedPosition && Quaternion.Angle(camera.transform.localRotation, savedRotation) < 0.01f,
                "first-person camera position and pitch restored exactly");

            var avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            avatar.name = "RemoteAvatar";
            avatar.transform.SetParent(player.transform, false);
            Renderer renderer = avatar.GetComponent<Renderer>();
            original = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            original.SetColor("_BaseColor", new Color(0.2f, 0.7f, 0.9f, 1f));
            renderer.sharedMaterial = original;
            Invoke(visuals, "UpdateProtection", true, 1.49d);
            Check(renderer.sharedMaterial != original, "protected avatar uses its own material");
            Near(0.5f, renderer.sharedMaterial.GetColor("_BaseColor").a, "half-transparent alpha");
            Near(1f, renderer.sharedMaterial.GetFloat("_Surface"), "transparent render mode");
            Near(1f, original.GetColor("_BaseColor").a, "shared player color stays untouched");
            Invoke(visuals, "UpdateProtection", true, 1.34d);
            Check(renderer.sharedMaterial == original, "next blink is fully opaque");
            Invoke(visuals, "UpdateProtection", true, 1.19d);
            Check(renderer.sharedMaterial != original, "blink repeats half-transparent");
            Invoke(visuals, "UpdateProtection", false, 0d);
            Check(renderer.sharedMaterial == original, "protection end restores original material");
            Check(((System.Collections.ICollection)typeof(PlayerRespawnVisuals).GetField("materialSets", Private).GetValue(visuals)).Count == 0,
                "temporary material cache released");

            var body = corpse.AddComponent<RagdollCorpse>();
            Invoke(body, "Awake");
            body.KeepVisibleFor(2.2f);
            Near(2.2f, (float)typeof(RagdollCorpse).GetField("vanishStartTime", Private).GetValue(body) - Time.time,
                "corpse survives entire death camera");

            // Check the actual imported character materials, not just a test shader.
            Material[] modelMaterials = AssetDatabase.LoadAllAssetsAtPath("Assets/Player/goshi(red).fbx").OfType<Material>().ToArray();
            Check(modelMaterials.Length > 0, "real avatar materials loaded");
            foreach (Material source in modelMaterials)
            {
                Material faded = (Material)typeof(PlayerRespawnVisuals).GetMethod("CreateTransparent", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { source });
                Check(faded.renderQueue == 3000 && faded.HasProperty("_SrcBlend"), "real avatar supports alpha blending: " + source.shader.name);
                Object.DestroyImmediate(faded);
            }
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(corpse);
            if (original != null) Object.DestroyImmediate(original);
        }
    }

    private static PlayerHealth.LifeState State(PlayerHealth health)
        => (PlayerHealth.LifeState)typeof(PlayerHealth).GetField("life", Private).GetValue(health);
    private static void SetState(PlayerHealth health, PlayerHealth.LifeState value)
        => typeof(PlayerHealth).GetField("life", Private).SetValue(health, value);
    private static object Invoke(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Near(float expected, float actual, string message)
        => Check(Mathf.Abs(expected - actual) < 0.05f, $"{message}: expected {expected}, got {actual}");
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("[PlayerLifeRegression] " + message);
        assertions++;
    }
}
#endif
