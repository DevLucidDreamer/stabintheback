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
        bool nowInside = IsPlayerOnPlate(player);
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

    private bool IsPlayerOnPlate(Transform player)
    {
        if (player == null || Area == null || !Area.enabled)
            return false;

        // CharacterController의 실제 발/몸통 범위와 판정 영역이 겹치는지 본다.
        // transform 원점만 검사하면 경사·단차·네트워크 보간 중 발판을 놓칠 수 있다.
        CharacterController character = player.GetComponent<CharacterController>();
        if (character != null)
            return Area.bounds.Intersects(character.bounds);

        return Area.bounds.Contains(player.position + Vector3.up * 0.2f);
    }

    private Transform ResolveLocalPlayer()
    {
        // 접속 직후 localPlayer가 아직 없을 때 첫 번째(원격) PlayerController를
        // 캐시하면 이 클라이언트의 발판 입력이 영구히 다른 플레이어를 추적한다.
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
        if (inside) FortressGameManager.Instance?.SetLocalPressure(plateIndex, false);
        inside = false;
    }
}
