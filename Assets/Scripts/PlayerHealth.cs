using Mirror;
using UnityEngine;

/// <summary>
/// 무기 원킬 + 즉시 리스폰 (Phase 5).
/// - 체력 개념 없이 무기에 한 대 맞으면 죽는다(서버 권한).
/// - 죽으면 즉시 스폰 지점으로 리스폰하고, 들고 있던 무기는 죽은 자리에 떨어진다(권력 이동).
/// - 죽은 자리에는 래그돌 시체가 남아 맞은 방향으로 날아간다(PlayerRagdoll).
/// - 리스폰 직후 짧은 무적으로 스폰킬을 막는다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : NetworkBehaviour
{
    [Tooltip("리스폰 직후 무적 시간(초). 스폰킬 방지")]
    [SerializeField] private float spawnProtection = 1.5f;

    private CharacterController controller;
    private PlayerRagdoll ragdoll;
    private float invulnUntil;      // 서버 기준 무적 종료 시각

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        ragdoll = GetComponent<PlayerRagdoll>();
    }

    /// <summary>서버에서 이 플레이어를 처치한다.</summary>
    /// <param name="blowDirection">맞은 방향(수평). 시체가 이쪽으로 날아간다.</param>
    [Server]
    public void ServerKill(uint killerNetId, Vector3 blowDirection = default)
    {
        if (Time.time < invulnUntil)
            return; // 무적/중복 처리 방지
        invulnUntil = Time.time + spawnProtection;

        // 들고 있던 무기를 떨어뜨려 권력을 이동시킨다.
        var weapons = WeaponNetworkManager.Instance;
        if (weapons != null)
            weapons.ServerDropWeaponOf(netId, transform.position + Vector3.up * 0.5f);

        // 죽은 자리/자세를 그대로 넘겨, 리스폰 이동과 관계없이 시체가 제자리에 남게 한다.
        if (blowDirection.sqrMagnitude < 0.0001f)
            blowDirection = transform.forward;
        RpcDie(transform.position, transform.rotation, blowDirection.normalized);

        // 리스폰 위치 선택 (Phase 1의 NetworkStartPosition 사용).
        Vector3 pos = transform.position;
        NetworkManager nm = NetworkManager.singleton;
        if (nm != null)
        {
            Transform sp = nm.GetStartPosition();
            if (sp != null)
                pos = sp.position;
        }

        TargetRespawn(connectionToClient, pos);
    }

    /// <summary>죽는 연출. 시체 물리는 동기화하지 않고 각자 로컬에서 굴린다.</summary>
    [ClientRpc]
    private void RpcDie(Vector3 position, Quaternion rotation, Vector3 blowDirection)
    {
        if (ragdoll != null)
            ragdoll.SpawnCorpse(position, rotation, blowDirection);
    }

    // 위치 이동은 클라이언트 권한 NetworkTransform이라, 소유 클라이언트에서 옮겨야 동기화된다.
    [TargetRpc]
    private void TargetRespawn(NetworkConnectionToClient conn, Vector3 pos)
    {
        if (controller != null)
        {
            controller.enabled = false;      // 직접 이동이 막히지 않게 잠깐 끈다
            transform.position = pos;
            controller.enabled = true;
        }
        else
        {
            transform.position = pos;
        }

        if (isLocalPlayer)
            GameHud.Ensure().ShowToast("당했다!  리스폰", 1.8f, new Color(1f, 0.3f, 0.28f));
    }
}
