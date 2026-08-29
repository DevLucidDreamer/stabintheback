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
    private const int MaxReconnectAttempts = 3;
    private const float StableConnectionSeconds = 30f;

    [Header("3D Voice")]
    [Min(1)] [SerializeField] private int audibleDistance = 25;
    [Min(0)] [SerializeField] private int conversationalDistance = 1;
    [Min(0f)] [SerializeField] private float fadeIntensity = 1f;
    [SerializeField] private AudioFadeModel fadeModel = AudioFadeModel.InverseByDistance;
    [SerializeField] private bool allowStereoPanning = true;
    [Min(0.05f)] [SerializeField] private float positionUpdateInterval = 0.1f;

    [Header("Microphone")]
    [Tooltip("키를 누르는 동안에만 송신합니다. 배포 기본값은 Push-to-Talk입니다.")]
    [SerializeField] private bool pushToTalk = true;
    [SerializeField] private Key pushToTalkKey = Key.V;
    [SerializeField] private Key muteToggleKey = Key.M;

    private static readonly SemaphoreSlim ServiceGate = new SemaphoreSlim(1, 1);

    private Transform listener;
    private string activeChannel;
    private string requestedChannel;
    private bool joined;
    private bool stopping;
    private bool lastTransmitState;
    private float nextPositionUpdate;
    private int operationVersion;
    private bool userMuted;
    private IVivoxService observedService;
    private bool connectInProgress;
    private bool connectionRecovering;
    private bool reconnectScheduled;
    private int reconnectAttempt;
    private float nextReconnectAt;
    private float connectedAt;

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
        reconnectAttempt = 0;
        reconnectScheduled = false;

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
        if (connectInProgress)
        {
            reconnectScheduled = true;
            nextReconnectAt = Time.unscaledTime;
            return;
        }

        connectInProgress = true;
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

            ObserveService(service);

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

            if (joined && !string.IsNullOrEmpty(activeChannel) && activeChannel != channelName &&
                service.ActiveChannels.ContainsKey(activeChannel))
                await service.LeaveChannelAsync(activeChannel);

            int audible = Mathf.Max(1, audibleDistance);
            int conversational = Mathf.Clamp(conversationalDistance, 0, audible);
            var properties = new Channel3DProperties(
                audible,
                conversational,
                Mathf.Max(0f, fadeIntensity),
                fadeModel);

            if (!service.ActiveChannels.ContainsKey(channelName))
                await service.JoinPositionalChannelAsync(channelName, ChatCapability.AudioOnly, properties);

            if (!IsCurrent(version, channelName))
            {
                await service.LeaveChannelAsync(channelName);
                return;
            }

            activeChannel = channelName;
            joined = true;
            connectionRecovering = false;
            reconnectScheduled = false;
            connectedAt = Time.unscaledTime;
            ApplyPushToTalk(force: true);
            Update3DPosition();
            Debug.Log($"[Vivox] 3D 음성 채널 연결 완료: {channelName}");
            GameHud.Ensure().ShowToast(pushToTalk
                ? "음성 채팅 준비됨 · V를 누르는 동안 송신"
                : "음성 채팅 준비됨 · M으로 음소거", 4f, new Color(0.65f, 0.9f, 1f));
        }
        catch (Exception exception)
        {
            if (IsCurrent(version, channelName))
            {
                Debug.LogWarning($"[Vivox] 음성 채팅 연결 실패: {exception.Message}");
                ScheduleReconnect(channelName);
            }
        }
        finally
        {
            ServiceGate.Release();
            connectInProgress = false;
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

            if (!string.IsNullOrEmpty(channelToLeave) && service.IsLoggedIn &&
                service.ActiveChannels.ContainsKey(channelToLeave))
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
        if (joined && reconnectAttempt > 0 && Time.unscaledTime - connectedAt >= StableConnectionSeconds)
            reconnectAttempt = 0;

        if (reconnectScheduled && !connectInProgress && !stopping &&
            Time.unscaledTime >= nextReconnectAt && !string.IsNullOrEmpty(requestedChannel))
        {
            reconnectScheduled = false;
            _ = ConnectAsync(requestedChannel, operationVersion);
        }

        if (!joined)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[muteToggleKey].wasPressedThisFrame)
        {
            userMuted = !userMuted;
            GameHud.Ensure().ShowToast(userMuted ? "마이크 음소거" : "마이크 사용 가능", 1.8f,
                userMuted ? new Color(1f, 0.65f, 0.55f) : new Color(0.6f, 1f, 0.7f));
        }

        ApplyPushToTalk();

        if (Time.unscaledTime >= nextPositionUpdate)
        {
            nextPositionUpdate = Time.unscaledTime + positionUpdateInterval;
            Update3DPosition();
        }
    }

    private void ApplyPushToTalk(bool force = false)
    {
        bool transmit = !userMuted;
        if (pushToTalk)
        {
            Keyboard keyboard = Keyboard.current;
            transmit = !userMuted && keyboard != null && keyboard[pushToTalkKey].isPressed;
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
        if (!joined || service == null || connectionRecovering || string.IsNullOrEmpty(activeChannel))
            return;

        if (!service.IsLoggedIn || !service.ActiveChannels.ContainsKey(activeChannel))
        {
            HandleChannelUnavailable(activeChannel);
            return;
        }

        Transform ears = listener != null ? listener : transform;
        try
        {
            service.Set3DPosition(
                ears.position,
                ears.position,
                ears.forward,
                ears.up,
                activeChannel,
                allowStereoPanning);
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning($"[Vivox] 음성 위치 갱신 중 채널 연결이 끊겼습니다: {exception.Message}");
            HandleChannelUnavailable(activeChannel);
        }
    }

    private void ObserveService(IVivoxService service)
    {
        if (ReferenceEquals(observedService, service))
            return;

        StopObservingService();
        observedService = service;
        observedService.ChannelLeft += OnChannelLeft;
        observedService.LoggedOut += OnLoggedOut;
        observedService.ConnectionRecovering += OnConnectionRecovering;
        observedService.ConnectionRecovered += OnConnectionRecovered;
        observedService.ConnectionFailedToRecover += OnConnectionFailedToRecover;
    }

    private void StopObservingService()
    {
        if (observedService == null)
            return;

        observedService.ChannelLeft -= OnChannelLeft;
        observedService.LoggedOut -= OnLoggedOut;
        observedService.ConnectionRecovering -= OnConnectionRecovering;
        observedService.ConnectionRecovered -= OnConnectionRecovered;
        observedService.ConnectionFailedToRecover -= OnConnectionFailedToRecover;
        observedService = null;
    }

    private void OnChannelLeft(string channelName)
    {
        if (channelName != activeChannel)
            return;

        HandleChannelUnavailable(channelName);
    }

    private void OnLoggedOut()
    {
        if (stopping)
            return;

        HandleChannelUnavailable(activeChannel);
    }

    private void OnConnectionRecovering()
    {
        connectionRecovering = true;
        Debug.LogWarning("[Vivox] 네트워크 연결 복구를 기다리는 중입니다.");
    }

    private void OnConnectionRecovered()
    {
        connectionRecovering = false;
        if (observedService != null && !string.IsNullOrEmpty(activeChannel) &&
            observedService.ActiveChannels.ContainsKey(activeChannel))
        {
            joined = true;
            connectedAt = Time.unscaledTime;
            Debug.Log("[Vivox] 네트워크 연결이 복구되었습니다.");
            return;
        }

        HandleChannelUnavailable(activeChannel);
    }

    private void OnConnectionFailedToRecover()
    {
        connectionRecovering = false;
        HandleChannelUnavailable(activeChannel);
    }

    private void HandleChannelUnavailable(string channelName)
    {
        if (!joined && reconnectScheduled)
            return;

        if (!string.IsNullOrEmpty(channelName))
            Debug.LogWarning($"[Vivox] 음성 채널 연결 종료: {channelName}");

        joined = false;
        activeChannel = null;
        lastTransmitState = false;

        if (!stopping && !connectInProgress && !string.IsNullOrEmpty(requestedChannel))
            ScheduleReconnect(requestedChannel);
    }

    private void ScheduleReconnect(string channelName)
    {
        if (stopping || reconnectScheduled || string.IsNullOrEmpty(channelName))
            return;

        reconnectAttempt++;
        if (reconnectAttempt > MaxReconnectAttempts)
        {
            Debug.LogError("[Vivox] 음성 채팅 자동 복구를 중단했습니다. 네트워크 또는 계정 상태를 확인하세요.");
            GameHud.Ensure().ShowToast("음성 채팅을 사용할 수 없습니다", 4f, new Color(1f, 0.55f, 0.45f));
            return;
        }

        float delay = Mathf.Pow(2f, reconnectAttempt);
        nextReconnectAt = Time.unscaledTime + delay;
        reconnectScheduled = true;
        Debug.LogWarning($"[Vivox] {delay:0}초 후 음성 채팅 복구를 시도합니다. ({reconnectAttempt}/{MaxReconnectAttempts})");
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
        StopObservingService();
        if (joined || !string.IsNullOrEmpty(requestedChannel))
            StopVoice();
    }
}
