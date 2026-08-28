using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 호스트가 대기실에 있는 동안 "여기 방 열려 있다"고 LAN에 알린다.
/// NetworkBootstrap에 붙어 씬을 넘어가도 살아남는다.
///
/// 대기실을 떠나 게임 씬으로 들어가면 알림을 끈다 — 이미 시작한 판에
/// '빠른 참가'로 사람이 떨어지면 곤란하기 때문이다.
/// </summary>
public class LanRoomAdvertiser : MonoBehaviour
{
    [Tooltip("이 씬에 있는 동안만 방을 알린다(대기실)")]
    [SerializeField] private string lobbyScene = "Lobby";

    private void Update()
    {
        bool open = NetworkServer.active &&
                    SceneManager.GetActiveScene().name == lobbyScene &&
                    !string.IsNullOrEmpty(GameLaunch.Code);

        if (!open)
        {
            if (LanRoomBeacon.IsAdvertising)
                LanRoomBeacon.StopAdvertising();
            return;
        }

        if (!LanRoomBeacon.IsAdvertising)
            LanRoomBeacon.StartAdvertising();

        LanRoomBeacon.SetRoomInfo(GameLaunch.Code, NetworkServer.connections.Count, NetworkServer.maxConnections);
        LanRoomBeacon.PumpAdvertising();
    }

    private void OnDisable() => LanRoomBeacon.StopAdvertising();

    private void OnApplicationQuit() => LanRoomBeacon.StopAdvertising();

}
