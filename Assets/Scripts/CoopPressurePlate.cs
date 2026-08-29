using Mirror;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoopPressurePlate : MonoBehaviour
{
    [SerializeField] private int plateIndex;
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressedDepth = 0.12f;

    private Collider area;
    private Transform localPlayer;
    private Vector3 visualStart;
    private bool inside;

    public int PlateIndex => plateIndex;
    public Collider Area => area != null ? area : GetComponent<Collider>();

    public void Configure(int index, Transform visual)
    {
        plateIndex = index;
        plateVisual = visual;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
        if (plateVisual != null) visualStart = plateVisual.localPosition;
    }

    private void Update()
    {
        Transform player = ResolveLocalPlayer();
        bool nowInside = player != null && area.bounds.Contains(player.position + Vector3.up * 0.2f);
        if (nowInside != inside)
        {
            inside = nowInside;
            FortressGameManager.Instance?.SetLocalPressure(plateIndex, inside);
        }

        if (plateVisual != null && FortressGameManager.Instance != null)
        {
            bool pressed = (FortressGameManager.Instance.PressureMask & (1 << plateIndex)) != 0 ||
                           (FortressGameManager.Instance.PressureLatchedMask & (1 << plateIndex)) != 0;
            Vector3 target = visualStart + Vector3.down * (pressed ? pressedDepth : 0f);
            plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, Time.deltaTime * 10f);
        }
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
        if (inside) FortressGameManager.Instance?.SetLocalPressure(plateIndex, false);
        inside = false;
    }
}
