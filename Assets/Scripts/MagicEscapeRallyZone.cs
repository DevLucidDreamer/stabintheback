using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class MagicEscapeRallyZone : MonoBehaviour
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
        CharacterController character = player != null ? player.GetComponent<CharacterController>() : null;
        bool nowInside = character != null
            ? Area.bounds.Intersects(character.bounds)
            : player != null && Area.bounds.Contains(player.position + Vector3.up * 0.5f);
        if (nowInside == inside) return;
        inside = nowInside;
        MagicEscapeGameManager.Instance?.SetLocalRally(inside);
    }

    private Transform ResolveLocalPlayer()
    {
        if (NetworkClient.active)
        {
            localPlayer = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.transform : null;
            return localPlayer;
        }
        if (localPlayer == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) localPlayer = player.transform;
        }
        return localPlayer;
    }

    private void OnDisable()
    {
        if (inside) MagicEscapeGameManager.Instance?.SetLocalRally(false);
        inside = false;
    }
}
