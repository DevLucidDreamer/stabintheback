#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinishingChecks
{
    private static int assertions;
    private static void Assert(bool test, string reason)
    { assertions++; if (!test) throw new Exception("[FinishingChecks] " + reason); }
    public static void Run()
    {
        var stamina = new SprintStamina();
        for (int i = 0; i < 460; i++) stamina.Tick(.01f, true, true);
        Assert(stamina.Exhausted && stamina.Value == 0, "sprint exhausts after 4.5 seconds");
        bool neverResumed = true;
        for (int i = 0; i < 800; i++) neverResumed &= !stamina.Tick(.01f, true, true);
        Assert(neverResumed, "holding shift cannot restart an exhausted sprint");
        Assert(stamina.Value > .99f, "rest replenishes stamina even while exhausted");
        stamina.Tick(.01f, false, true); Assert(stamina.Tick(.01f, true, true), "release then press resumes sprint");
        stamina.Reset(); for (int i = 0; i < 100; i++) stamina.Tick(.01f, true, false);
        Assert(stamina.Value == 1, "standing still does not drain stamina");
        stamina.Tick(1, true, true); float value = stamina.Value;
        stamina.Tick(1, false, true); Assert(Mathf.Approximately(value, stamina.Value), "recovery delay prevents instant recharge");

        EditorSceneManager.OpenScene(ExpeditionBuilder.BridgeScene, OpenSceneMode.Single);
        var manager = Object.FindFirstObjectByType<WeaponNetworkManager>();
        var dummy = new GameObject("FourthWeapon"); var fourth = dummy.AddComponent<Weapon>(); fourth.SetWeaponId(999);
        typeof(WeaponNetworkManager).GetMethod("BuildRegistry", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
        manager.States.IsWritable = () => true; manager.States.IsRecording = () => false;
        int[] ids = { manager.FindKey("chalice"), manager.FindKey("mine_lantern"), manager.FindKey("secret_tuna"), 999 };
        Assert(ids.All(i => i >= 0) && ids.Distinct().Count() == 4, "unique campaign weapons and capacity fixture");
        var player = new GameObject("InventoryFixture").AddComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetProperty("netId").SetValue(player, (uint)500);
        var connection = new NetworkConnectionToClient(55);
        typeof(NetworkConnection).GetProperty("identity").SetValue(connection, player); connection.isReady = true;
        NetworkServer.connections.Add(55, connection);
        try
        {
            foreach (int id in ids) manager.States[id] = new WeaponState { available = true, position = Vector3.zero };
            for (int i = 0; i < 3; i++) Assert(manager.ServerPickup(ids[i], connection), "pickup without discarding older weapons");
            Assert(!manager.ServerPickup(999, connection) && manager.Inventory(500).Length == 3, "fourth weapon is rejected without loss");
            Assert(manager.Equipped(500) == ids[2], "new pickup is equipped");
            Assert(!manager.ServerCycle(1, connection), "switch cooldown is enforced");
            var cooldown = (Dictionary<uint, double>)typeof(WeaponNetworkManager).GetField("nextSwingAt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
            cooldown.Clear(); Assert(manager.ServerCycle(1, connection) && manager.Equipped(500) == ids[0], "wheel wraps forward");
            cooldown.Clear(); Assert(manager.ServerCycle(-1, connection) && manager.Equipped(500) == ids[2], "wheel wraps backward");
            Assert(manager.States.Count(s => s.Value.equipped) == 1, "only one weapon equipped");
            manager.CaptureJourney(); Assert(WeaponJourney.Records.Count == 3, "three weapons captured for travel");
            typeof(NetworkIdentity).GetProperty("netId").SetValue(player, (uint)501);
            foreach (int id in ids.Take(3)) manager.States[id] = new WeaponState();
            var restore = (IEnumerator)typeof(WeaponNetworkManager).GetMethod("RestoreJourney", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            while (restore.MoveNext()) { if (WeaponJourney.Records.Count > 0) throw new Exception("Travel did not resolve ready player"); }
            Assert(manager.Inventory(501).Length == 3 && manager.Inventory(500).Length == 0, "new scene player inherits original connection inventory");
            Assert(manager.Equipped(501) == ids[2] && !WeaponJourney.Transitioning, "equipped weapon survives travel");
            var chalice = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/ChaliceBottle.prefab");
            var size = chalice.GetComponent<BoxCollider>().size;
            Assert(Mathf.Max(size.x, size.y, size.z) <= .63f, "chalice reduced to 62cm");
            Assert(Object.FindObjectsByType<Weapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(w => w.campaignKey == "secret_tuna" && w.startsAvailable) == 1,
                "one discoverable secret tuna location");
        }
        finally { NetworkServer.connections.Clear(); WeaponJourney.Clear(); Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(dummy); }
        Debug.Log("[FinishingChecks] PASS " + assertions);
    }
}
#endif
