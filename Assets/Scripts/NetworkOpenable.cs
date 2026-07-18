using Mirror;
using UnityEngine;

/// <summary>
/// Openable의 열림 상태를 서버 권한으로 동기화한다 (Phase 2).
/// - 씬 오브젝트(서랍/문/냉장고)에 NetworkIdentity와 함께 붙는다.
/// - 아무 클라이언트나 상호작용하면 Command로 서버에 토글을 요청한다(소유권 불필요).
/// - 서버가 SyncVar를 바꾸면 훅을 통해 모든 클라이언트의 Openable이 같은 상태로 애니메이션한다.
/// - 늦게 접속한 클라이언트도 OnStartClient에서 현재 상태를 반영받는다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Openable))]
public class NetworkOpenable : NetworkBehaviour, IOpenableNetworkController
{
    private Openable openable;

    [SyncVar(hook = nameof(OnOpenChanged))]
    private bool isOpen;

    private void Awake()
    {
        openable = GetComponent<Openable>();
    }

    public override void OnStartClient()
    {
        // 접속 시점의 현재 상태로 맞춘다(스폰 시 SyncVar 초기값은 이미 반영된 뒤 호출됨).
        openable.SetOpen(isOpen);
    }

    /// <summary>Openable.Interact가 호출한다.</summary>
    public void RequestToggle()
    {
        // 네트워크가 가동되지 않은(스폰되지 않은) 상태면 로컬 토글로 폴백한다.
        if (!isServer && !isClient)
        {
            openable.SetOpen(!openable.IsOpen);
            return;
        }

        if (isServer)
            SetServer(!isOpen);
        else
            CmdToggle();
    }

    // 씬 오브젝트는 클라이언트 소유권이 없으므로 requiresAuthority = false.
    [Command(requiresAuthority = false)]
    private void CmdToggle()
    {
        SetServer(!isOpen);
    }

    [Server]
    private void SetServer(bool open)
    {
        isOpen = open;          // SyncVar 변경 → 클라이언트 훅 호출
        openable.SetOpen(open); // 서버에서도 즉시 반영
    }

    private void OnOpenChanged(bool _, bool now)
    {
        openable.SetOpen(now);
    }
}
