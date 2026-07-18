using Mirror;
using UnityEngine;

/// <summary>
/// 탈출 지점 (Phase 6). 메인 문 밖에 놓인 트리거.
/// 준비물 체크리스트를 모두 완료한 상태에서 플레이어가 들어오면 다음 스테이지로 전환한다.
/// 미완료면 그 플레이어에게 안내만 표시한다. 씬 전환은 서버 권한으로만 수행.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class EscapeZone : NetworkBehaviour
{
    [Tooltip("전환할 다음 씬 이름(Build Settings에 포함되어야 함). 비우면 loop 옵션에 따라 시작 씬으로")]
    [SerializeField] private string nextSceneName = "";

    [Tooltip("nextSceneName이 비었을 때 시작 씬으로 되돌아갈지(마지막 스테이지 루프)")]
    [SerializeField] private bool loopToStartIfEmpty = true;

    [Tooltip("루프 시 돌아갈 시작 씬")]
    [SerializeField] private string startSceneName = "NetworkDemo";

    private bool serverTransitioning;
    private float localMsgUntil;

    private void OnTriggerEnter(Collider other)
    {
        // 로컬 플레이어가 들어왔을 때만 판단(로컬 이동은 CharacterController.Move라 트리거가 확실히 발생).
        NetworkIdentity id = other.GetComponentInParent<NetworkIdentity>();
        if (id == null || !id.isLocalPlayer)
            return;

        var checklist = CampChecklistManager.Instance;
        if (checklist != null && checklist.IsComplete())
            CmdTryEscape();
        else
            localMsgUntil = Time.time + 2.5f; // 아직 준비물이 남음
    }

    [Command(requiresAuthority = false)]
    private void CmdTryEscape()
    {
        if (serverTransitioning)
            return;

        var checklist = CampChecklistManager.Instance;
        if (checklist == null || !checklist.IsComplete())
            return;

        string target = nextSceneName;
        if (string.IsNullOrEmpty(target) && loopToStartIfEmpty)
            target = startSceneName;
        if (string.IsNullOrEmpty(target))
            return;

        serverTransitioning = true;
        NetworkManager.singleton.ServerChangeScene(target);
    }

    private void OnGUI()
    {
        if (Time.time >= localMsgUntil)
            return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
        };
        style.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 32f), "준비물을 모두 챙겨야 나갈 수 있다!", style);
    }
}
