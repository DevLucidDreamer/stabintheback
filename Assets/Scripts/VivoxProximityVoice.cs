using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 로컬 플레이어를 Vivox 3D 채널에 연결하고 머리 위치를 송신한다.
/// NetworkPlayerSetup이 서버에서 동기화한 채널 이름으로 StartVoice를 호출한다.
/// </summary>
public sealed class VivoxProximityVoice : MonoBehaviour
{
    [Header("3D Voice")]
    [Min(1)] [SerializeField] private int audibleDistance = 25;
    [Min(0)] [SerializeField] private int conversationalDistance = 1;
    [Min(0f)] [SerializeField] private float fadeIntensity = 1f;
    [SerializeField] private AudioFadeModel fadeModel = AudioFadeModel.InverseByDistance;
    [SerializeField] private bool allowStereoPanning = true;
    [Min(0.05f)] [SerializeField] private float positionUpdateInterval = 0.1f;

    [Header("Microphone")]
    [Tooltip("켜면 키를 누르는 동안에만 송신합니다. 기본값은 오픈 마이크입니다.")]
    [SerializeField] private bool pushToTalk;
    [SerializeField] private Key pushToTalkKey = Key.V;

    private static readonly SemaphoreSlim ServiceGate = new SemaphoreSlim(1, 1);

    private Transform listener;
    private string activeChannel;
    private string requestedChannel;
    private bool joined;
    private bool stopping;
    private bool lastTransmitState;
    private float nextPositionUpdate;
    private int operationVersion;

    public bool IsConnected => joined;
    public bool IsMicrophoneMuted => VivoxService.Instance != null && VivoxService.Instance.IsInputDeviceMuted;
    public string ActiveChannel => activeChannel;

    /// <summary>로컬 플레이어를 지정된 근접 음성 채널에 연결한다.</summary>
    public void StartVoice(string channelName, Transform listenerTransform = null)
    {
#if UNITY_SERVER
        return;
#else
        if (string.IsNullOrWhiteSpace(channelName))
            return;

        listener = listenerTransform != null ? listenerTransform : transform;
        requestedChannel = SanitizeChannelName(channelName);
        stopping = false;

        if (joined && activeChannel == requestedChannel)
            return;

        int version = ++operationVersion;
        _ = ConnectAsync(requestedChannel, version);
#endif
    }

    /// <summary>현재 채널에서 나가고 Vivox 로그인을 정리한다.</summary>
    public void StopVoice()
    {
#if !UNITY_SERVER
        stopping = true;
        requestedChannel = null;
        ++operationVersion;
        _ = DisconnectAsync();
#endif
    }

    public void SetMicrophoneMuted(bool muted)
    {
        if (!joined || VivoxService.Instance == null)
            return;

        if (muted)
            VivoxService.Instance.MuteInputDevice();
        else
            VivoxService.Instance.UnmuteInputDevice();
    }

    public void ToggleMicrophoneMute() => SetMicrophoneMuted(!IsMicrophoneMuted);

    private async Task ConnectAsync(string channelName, int version)
    {
        await ServiceGate.WaitAsync();
        try
        {
            if (!IsCurrent(version, channelName))
                return;

            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IVivoxService service = VivoxService.Instance;
            if (service == null)
                throw new InvalidOperationException("Vivox 서비스가 UGS에 등록되지 않았습니다.");

            if (service.InitializationState != VivoxInitializationState.Initialized)
                await service.InitializeAsync();

            if (!service.IsLoggedIn)
            {
                string playerId = AuthenticationService.Instance.PlayerId;
                string displayName = "Player-" + playerId.Substring(Math.Max(0, playerId.Length - 6));
                await service.LoginAsync(new LoginOptions { DisplayName = displayName });
            }

            if (!IsCurrent(version, channelName))
                return;

            if (joined && !string.IsNullOrEmpty(activeChannel) && activeChannel != channelName)
                await service.LeaveChannelAsync(activeChannel);

            int audible = Mathf.Max(1, audibleDistance);
            int conversational = Mathf.Clamp(conversationalDistance, 0, audible);
            var properties = new Channel3DProperties(
                audible,
                conversational,
                Mathf.Max(0f, fadeIntensity),
                fadeModel);

            await service.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, properties);

            if (!IsCurrent(version, channelName))
            {
                await service.LeaveChannelAsync(channelName);
                return;
            }

            activeChannel = channelName;
            joined = true;
            ApplyPushToTalk(force: true);
            Update3DPosition();
            Debug.Log($"[Vivox] 3D 음성 채널 연결 완료: {channelName}");
        }
        catch (Exception exception)
        {
            if (IsCurrent(version, channelName))
                Debug.LogError($"[Vivox] 음성 채팅 연결 실패: {exception.Message}\n{exception}");
        }
        finally
        {
            ServiceGate.Release();
        }
    }

    private async Task DisconnectAsync()
    {
        await ServiceGate.WaitAsync();
        try
        {
            IVivoxService service = VivoxService.Instance;
            string channelToLeave = activeChannel;
            activeChannel = null;
            joined = false;

            if (service == null)
                return;

            if (!string.IsNullOrEmpty(channelToLeave) && service.IsLoggedIn)
                await service.LeaveChannelAsync(channelToLeave);

            if (service.IsLoggedIn)
                await service.LogoutAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Vivox] 음성 채팅 종료 중 오류: {exception.Message}");
        }
        finally
        {
            ServiceGate.Release();
        }
    }

    private void Update()
    {
        if (!joined)
            return;

        ApplyPushToTalk();

        if (Time.unscaledTime >= nextPositionUpdate)
        {
            nextPositionUpdate = Time.unscaledTime + positionUpdateInterval;
            Update3DPosition();
        }
    }

    private void ApplyPushToTalk(bool force = false)
    {
        bool transmit = true;
        if (pushToTalk)
        {
            Keyboard keyboard = Keyboard.current;
            transmit = keyboard != null && keyboard[pushToTalkKey].isPressed;
        }

        if (force || transmit != lastTransmitState)
        {
            lastTransmitState = transmit;
            SetMicrophoneMuted(!transmit);
        }
    }

    private void Update3DPosition()
    {
        IVivoxService service = VivoxService.Instance;
        if (!joined || service == null || string.IsNullOrEmpty(activeChannel))
            return;

        Transform ears = listener != null ? listener : transform;
        service.Set3DPosition(
            ears.position,
            ears.position,
            ears.forward,
            ears.up,
            activeChannel,
            allowStereoPanning);
    }

    private bool IsCurrent(int version, string channelName)
        => this != null && !stopping && operationVersion == version && requestedChannel == channelName;

    private static string SanitizeChannelName(string channelName)
    {
        char[] chars = channelName.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_')
                chars[i] = '-';
        return new string(chars);
    }

    private void OnDestroy()
    {
        if (joined || !string.IsNullOrEmpty(requestedChannel))
            StopVoice();
    }
}
