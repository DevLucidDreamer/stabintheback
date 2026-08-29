using Mirror;
using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// NetworkBootstrap에 붙여, 타이틀 화면에서 넘어온 실행 의도(GameLaunch.Mode)에 따라
/// Unity Services에 익명 로그인한 뒤 Relay 방을 만들거나 참가하고,
/// 준비된 RelayMirrorTransport로 Mirror Host/Client를 시작한다.
/// </summary>
public class NetworkAutoLaunch : MonoBehaviour
{
    private async void Start()
    {
        NetworkManager nm = NetworkManager.singleton;
        if (nm == null)
            return;

        GameLaunch.LaunchMode mode = GameLaunch.Mode;
        if (mode == GameLaunch.LaunchMode.None)
            return;

        GameLaunch.Mode = GameLaunch.LaunchMode.None;

        RelayMirrorTransport transport = nm.GetComponent<RelayMirrorTransport>();
        if (transport == null)
        {
            ShowFailure("NetworkBootstrap에 RelayMirrorTransport가 없습니다.");
            await ReturnToTitleAfterFailureAsync();
            return;
        }

        try
        {
            GameHud.Ensure().ShowBanner("Unity Relay에 연결하는 중...", 3f, Color.white);
            await EnsureSignedInAsync();

            if (mode == GameLaunch.LaunchMode.Host)
            {
                // Relay의 maxConnections는 호스트를 제외한 참가자 수다.
                int joiningPlayers = Mathf.Max(1, nm.maxConnections - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(joiningPlayers);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                GameLaunch.Address = "relay";
                GameLaunch.Code = joinCode.Trim().ToUpperInvariant();
                transport.ConfigureHost(allocation.ToRelayServerData("dtls"));
                nm.StartHost();
                Debug.Log("[Relay] 방 생성 완료.");
            }
            else
            {
                string joinCode = (GameLaunch.Code ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(joinCode))
                    throw new InvalidOperationException("Relay 참가 코드가 비어 있습니다.");

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                GameLaunch.Address = "relay";
                GameLaunch.Code = joinCode;
                transport.ConfigureClient(allocation.ToRelayServerData("dtls"));
                nm.networkAddress = "relay";
                nm.StartClient();
                Debug.Log("[Relay] 방 참가 요청 완료.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShowFailure(FriendlyMessage(ex));
            await ReturnToTitleAfterFailureAsync();
        }
    }

    private static async System.Threading.Tasks.Task EnsureSignedInAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private static string FriendlyMessage(Exception ex)
    {
        string message = ex.Message ?? string.Empty;
        if (message.IndexOf("join", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("allocation", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0)
            return "방 코드가 만료되었거나 존재하지 않습니다.";

        return "Unity Relay 연결에 실패했습니다. 인터넷 연결과 Unity Dashboard의 Relay 활성화를 확인하세요.";
    }

    private static void ShowFailure(string message)
    {
        GameHud.Ensure().ShowBanner("방 연결 실패\n" + message, 10f, new Color(1f, 0.4f, 0.35f));
    }

    private static async System.Threading.Tasks.Task ReturnToTitleAfterFailureAsync()
    {
        await System.Threading.Tasks.Task.Delay(4000);
        if (!NetworkClient.active && !NetworkServer.active && SceneManager.GetActiveScene().name != "MainTitle")
            SceneManager.LoadScene("MainTitle");
    }
}
