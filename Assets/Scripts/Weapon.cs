using UnityEngine;

/// <summary>
/// 주울 수 있는 무기 (국자, 냉동참치 등).
/// 클릭하면 플레이어 손(카메라 앞 소켓)에 들리고, 좌클릭으로 스윙한다.
/// 멀티플레이에서는 고유 ID로 WeaponNetworkManager가 소지 상태를 동기화한다.
/// (무기 자체엔 NetworkIdentity를 붙이지 않는다 — 냉장고 등 자식이라 중첩 불가)
/// </summary>
public class Weapon : Interactable
{
    /// <summary>
    /// 무기가 손에 들리거나(true) 월드에 내려놓아질 때(false) 호출.
    /// 소지 상태는 이미 모든 클라이언트에 동기화된 뒤 반영되므로, 연출용으로 구독하면 된다.
    /// </summary>
    public static event System.Action<Weapon, bool> OnHeldChanged;

    [Header("손에 들었을 때의 로컬 포즈")]
    [Tooltip("들었을 때 손 소켓 기준 위치 오프셋")]
    public Vector3 holdPosition = Vector3.zero;

    [Tooltip("들었을 때 손 소켓 기준 회전(오일러 각)")]
    public Vector3 holdEuler = Vector3.zero;

    [Header("휘두르기 성능")]
    [Tooltip("이 무기를 휘둘렀을 때 닿는 앞쪽 거리 (m). 당근 대검처럼 긴 무기는 멀리 닿는다")]
    public float swingReach = 1.4f;

    [Tooltip("타격 판정 반경 (m). 참치처럼 뭉툭한 무기는 넓게 맞는다")]
    public float swingRadius = 1.0f;

    [Header("내려놓기")]
    [Tooltip("물리 없이 내려놓을 때(멀티플레이) 바닥 위에 살짝 띄우는 간격 (m)")]
    public float groundClearance = 0.03f;

    [Tooltip("이 오브젝트의 원점(손잡이)에서 모델 맨 아래까지의 거리 (m). " +
             "원점이 손잡이라서, 이만큼 띄워야 바닥에 묻히지 않는다")]
    public float groundOffset = 0.05f;

    [Header("네트워크")]
    [Tooltip("멀티플레이 동기화용 고유 ID. Phase 4 셋업 메뉴가 부여한다")]
    [SerializeField] private int weaponId = -1;
    public string campaignKey;
    public bool startsAvailable = true;

    public int WeaponId => weaponId;
    public void SetWeaponId(int id) => weaponId = id;

    public override string GetPrompt()
    {
        var player = Mirror.NetworkClient.localPlayer;
        if (player != null && WeaponNetworkManager.Instance != null &&
            WeaponNetworkManager.Instance.Inventory(player.netId).Length >= WeaponNetworkManager.Capacity) return "소지 한도 3개";
        return DisplayName + " 줍기";
    }

    public override void Interact(PlayerInteraction player) => player.PickUp(this);

    /// <summary>손 소켓에 붙인다. 콜라이더/물리를 끄고 소지 포즈를 적용.</summary>
    public void AttachTo(Transform socket)
    {
        gameObject.SetActive(true);
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            Destroy(rb);
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        transform.SetParent(socket, false);
        transform.localPosition = holdPosition;
        transform.localEulerAngles = holdEuler;

        OnHeldChanged?.Invoke(this, true);
    }

    /// <summary>
    /// 월드에 내려놓는다.
    /// addPhysics면 Rigidbody를 붙여 자연스럽게 떨어뜨린다(싱글플레이용).
    /// 그렇지 않으면(멀티플레이) 물리를 동기화하지 않으므로, 드롭 위치 아래로
    /// 레이캐스트해 즉시 바닥 위에 얹어 공중에 뜨지 않게 한다.
    /// </summary>
    public void DetachTo(Vector3 worldPos, Quaternion worldRot, bool addPhysics, bool snapToGround = true)
    {
        gameObject.SetActive(true);
        transform.SetParent(null, true);

        // 물리 없이 놓는 경우 바닥에 스냅. (콜라이더는 아직 꺼져 있어 자기 자신에 걸리지 않는다)
        if (!addPhysics && snapToGround &&
            Physics.Raycast(worldPos + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            worldPos = hit.point + Vector3.up * (groundClearance + groundOffset);
        }

        transform.SetPositionAndRotation(worldPos, worldRot);
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = true;

        if (addPhysics && GetComponent<Rigidbody>() == null)
            gameObject.AddComponent<Rigidbody>().mass = 2f;

        OnHeldChanged?.Invoke(this, false);
    }
}
