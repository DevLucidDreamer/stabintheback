using Mirror;
using UnityEngine;

/// <summary>
/// 탈출 지점. 맵 밖으로 나가는 길목에 놓인 트리거.
/// 한 판의 목표를 끝낸 상태에서 플레이어가 들어오면 다음 씬으로 전환한다.
/// 아직이면 그 플레이어에게만 안내를 띄운다. 씬 전환은 서버 권한으로만 수행.
///
/// 캠핑장은 바베큐를 다 구우면 <see cref="CampGameManager"/>가 알아서 대기실로 돌려보내므로
/// 이 컴포넌트를 두지 않는다. 목표가 다른 스테이지를 붙일 때 쓰는 범용 부품이다.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(Collider))]
public class EscapeZone : NetworkBehaviour
{
    [Tooltip("전환할 다음 씬 이름(Build Settings에 포함되어야 함). 비우면 loop 옵션에 따라 시작 씬으로")]
    [SerializeField] private string nextSceneName = "";

    [Tooltip("nextSceneName이 비었을 때 시작 씬으로 되돌아갈지(마지막 스테이지 루프)")]
    [SerializeField] private bool loopToStartIfEmpty = true;

    [Tooltip("루프 시 돌아갈 시작 씬")]
    [SerializeField] private string startSceneName = "Lobby";

    private bool serverTransitioning;

    private void OnTriggerEnter(Collider other)
    {
        // 로컬 플레이어가 들어왔을 때만 판단(로컬 이동은 CharacterController.Move라 트리거가 확실히 발생).
        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();
        if (id == null || !id.isLocalPlayer)
            return;

        if (GoalReached())
            CmdTryEscape();
        else
            GameHud.Ensure().ShowToast("아직 할 일이 남았다!", 2.5f, new Color(1f, 0.85f, 0.3f));
    }

    /// <summary>현재 스테이지의 팀 공용 목표가 끝났는가.</summary>
    private static bool GoalReached()
    {
        CampChecklistManager checklist = CampChecklistManager.Instance;
        if (checklist != null)
            return checklist.IsComplete();

        CampGameManager game = CampGameManager.Instance;
        return game == null || game.Phase == CampPhase.Feast;
    }

    [Command(requiresAuthority = false)]
    private void CmdTryEscape(NetworkConnectionToClient sender = null)
    {
        Collider area = GetComponent<Collider>();
        if (serverTransitioning || !GoalReached() || !ServerInteractionGuard.IsInside(sender, area))
            return;

        string target = nextSceneName;
        if (string.IsNullOrEmpty(target) && loopToStartIfEmpty)
            target = startSceneName;
        if (string.IsNullOrEmpty(target))
            return;
        if (NetworkManager.singleton == null)
            return;

        serverTransitioning = true;
        NetworkManager.singleton.ServerChangeScene(target);
    }
}
