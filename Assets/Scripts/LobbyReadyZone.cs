using Mirror;
using UnityEngine;

/// <summary>
/// 대기실 출발 발판. 로컬 플레이어가 이 영역 안에 서 있는 동안 '준비 완료'가 된다.
/// 트리거 이벤트가 아니라 주기적인 영역 검사를 쓴다 — 리스폰 텔레포트처럼
/// 콜라이더가 순간이동하는 경우에도 상태가 어긋나지 않게 하기 위함.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LobbyReadyZone : MonoBehaviour
{
    [Tooltip("영역 검사 주기(초)")]
    [SerializeField] private float checkInterval = 0.2f;

    private Collider area;
    private Transform localPlayer;
    private float timer;
    private bool inside;

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true; // 발판을 통과해 지나갈 수 있어야 한다
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;
        timer = Mathf.Max(0.05f, checkInterval);

        Transform player = ResolveLocalPlayer();
        bool nowInside = player != null && area.bounds.Contains(player.position + Vector3.up * 0.5f);
        if (nowInside == inside)
            return;

        inside = nowInside;
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.SetLocalReady(inside);
    }

    private Transform ResolveLocalPlayer()
    {
        if (localPlayer != null)
            return localPlayer;

        if (NetworkClient.active && NetworkClient.localPlayer != null)
            localPlayer = NetworkClient.localPlayer.transform;
        else
        {
            // 오프라인으로 대기실 씬을 직접 열어 확인하는 경우의 폴백.
            var controller = Object.FindFirstObjectByType<PlayerController>();
            if (controller != null)
                localPlayer = controller.transform;
        }

        return localPlayer;
    }
}
