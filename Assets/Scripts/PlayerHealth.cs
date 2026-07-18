using Mirror;
using UnityEngine;

/// <summary>
/// 마검 원킬 + 즉시 리스폰 (Phase 5).
/// - 체력 개념 없이 마검에 한 대 맞으면 죽는다(서버 권한).
/// - 죽으면 즉시 스폰 지점으로 리스폰하고, 들고 있던 마검은 죽은 자리에 떨어진다(권력 이동).
/// - 리스폰 직후 짧은 무적으로 스폰킬을 막는다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : NetworkBehaviour
{
    [Tooltip("리스폰 직후 무적 시간(초). 스폰킬 방지")]
    [SerializeField] private float spawnProtection = 1.5f;

    private CharacterController controller;
    private float invulnUntil;      // 서버 기준 무적 종료 시각
    private float respawnMsgUntil;  // 로컬 UI 표시 종료 시각

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    /// <summary>서버에서 이 플레이어를 처치한다.</summary>
    [Server]
    public void ServerKill(uint killerNetId)
    {
        if (Time.time < invulnUntil)
            return; // 무적/중복 처리 방지
        invulnUntil = Time.time + spawnProtection;

        // 들고 있던 마검을 떨어뜨려 권력을 이동시킨다.
        var weapons = WeaponNetworkManager.Instance;
        if (weapons != null)
            weapons.ServerDropWeaponOf(netId, transform.position + Vector3.up * 0.5f);

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

        respawnMsgUntil = Time.time + 1.5f;
    }

    private void OnGUI()
    {
        if (!isLocalPlayer || Time.time >= respawnMsgUntil)
            return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 30,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = new Color(1f, 0.25f, 0.25f);
        GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 44f), "당했다!  리스폰", style);
    }
}
