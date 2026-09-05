using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>Keep the connection identity across scene-local player replacement.</summary>
public static class WeaponJourney
{
    public sealed class Record
    {
        public NetworkConnectionToClient connection;
        public string key;
        public bool equipped;
        public int order;
    }
    public static readonly List<Record> Records = new List<Record>();
    public static bool Transitioning;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear() { Records.Clear(); Transitioning = false; }
    public static void ChangeScene(string scene)
    {
        if (!NetworkServer.active || NetworkManager.singleton == null || Transitioning) return;
        WeaponNetworkManager.Instance?.CaptureJourney();
        NetworkManager.singleton.ServerChangeScene(scene);
    }
}
