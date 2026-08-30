using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class MagicEscapePressurePlate : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Transform visual;
    [SerializeField] private float pressedDepth = 0.12f;
    private Collider area;
    private Transform localPlayer;
    private Vector3 visualStart;
    private bool inside;

    public int Index => index;
    public Collider Area => area != null ? area : GetComponent<Collider>();

    public void Configure(int value, Transform targetVisual)
    {
        index = value;
        visual = targetVisual;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
        if (visual != null) visualStart = visual.localPosition;
    }

    private void Update()
    {
        Transform player = ResolveLocalPlayer();
        CharacterController character = player != null ? player.GetComponent<CharacterController>() : null;
        bool nowInside = character != null
            ? Area.bounds.Intersects(character.bounds)
            : player != null && Area.bounds.Contains(player.position + Vector3.up * 0.2f);
        if (nowInside != inside)
        {
            inside = nowInside;
            MagicEscapeGameManager.Instance?.SetLocalPressure(index, inside);
        }

        if (visual != null && MagicEscapeGameManager.Instance != null)
        {
            int bit = 1 << index;
            bool pressed = (MagicEscapeGameManager.Instance.PressureMask & bit) != 0 ||
                           (MagicEscapeGameManager.Instance.PressureLatchedMask & bit) != 0;
            visual.localPosition = Vector3.Lerp(visual.localPosition,
                visualStart + Vector3.down * (pressed ? pressedDepth : 0f), Time.deltaTime * 10f);
        }
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
        if (inside) MagicEscapeGameManager.Instance?.SetLocalPressure(index, false);
        inside = false;
    }
}
