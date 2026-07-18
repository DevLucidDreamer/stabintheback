using Mirror;
using UnityEngine;

/// <summary>
/// NetworkBootstrap에 붙여, 타이틀 화면에서 넘어온 실행 의도(GameLaunch.Mode)에 따라
/// 자동으로 Host 또는 Client를 시작한다.
/// 의도가 None이면(게임 씬을 직접 열어 플레이) 아무것도 하지 않아 HUD로 수동 제어 가능.
/// </summary>
public class NetworkAutoLaunch : MonoBehaviour
{
    private void Start()
    {
        NetworkManager nm = NetworkManager.singleton;
        if (nm == null)
            return;

        switch (GameLaunch.Mode)
        {
            case GameLaunch.LaunchMode.Host:
                GameLaunch.Mode = GameLaunch.LaunchMode.None;
                nm.StartHost();
                break;

            case GameLaunch.LaunchMode.Client:
                GameLaunch.Mode = GameLaunch.LaunchMode.None;
                nm.networkAddress = string.IsNullOrWhiteSpace(GameLaunch.Address) ? "localhost" : GameLaunch.Address.Trim();
                nm.StartClient();
                break;
        }
    }
}
