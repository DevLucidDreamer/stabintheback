using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoopRallyZone : MonoBehaviour
{
    private Collider area;
    private Transform localPlayer;
    private bool inside;

    public Collider Area => area != null ? area : GetComponent<Collider>();

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
    }

    private void Update()
    {
        Transform player = ResolveLocalPlayer();
        bool nowInside = player != null && area.bounds.Contains(player.position + Vector3.up * 0.5f);
        if (nowInside == inside) return;
        inside = nowInside;
        FortressGameManager.Instance?.SetLocalRally(inside);
    }

    private Transform ResolveLocalPlayer()
    {
        if (localPlayer != null) return localPlayer;
        if (NetworkClient.active && NetworkClient.localPlayer != null) localPlayer = NetworkClient.localPlayer.transform;
        else
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) localPlayer = player.transform;
        }
        return localPlayer;
    }

    private void OnDisable()
    {
        if (inside) FortressGameManager.Instance?.SetLocalRally(false);
        inside = false;
    }
}
