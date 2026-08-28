using Mirror;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class NetworkPlayerSetup : NetworkBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private ItemChecklist itemChecklist;
    [SerializeField] private GameObject remoteAvatarRoot;
    [SerializeField] private Transform remoteHeadPitch;
    [SerializeField] private float pitchSendThreshold = 0.5f;

    [SyncVar(hook = nameof(OnVoiceChannelChanged))]
    private string voiceChannelName;

    private static string serverVoiceChannelName;

    [SyncVar(hook = nameof(OnPitchChanged))]
    private float syncedPitch;

    private Camera[] cameras;
    private AudioListener[] audioListeners;
    private float lastSentPitch;
    private VivoxProximityVoice proximityVoice;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVoiceChannelStatics()
    {
        serverVoiceChannelName = null;
    }

    private void Awake()
    {
        CacheComponents();
        EnsureRemoteAvatar();
    }

    private void Start()
    {
        if (!isClient && !isServer)
            ApplyLocalState(true);
    }

    public override void OnStartClient()
    {
        ApplyLocalState(isLocalPlayer);
        ApplyRemotePitch(syncedPitch);
    }

    public override void OnStartServer()
    {
        // 첫 플레이어가 생성될 때마다 서버 세션 전용 채널을 새로 만든다.
        // 채널 이름을 Mirror로 전달하므로 사설 IP가 같은 다른 방과도 겹치지 않는다.
        if (NetworkServer.connections.Count <= 1 || string.IsNullOrEmpty(serverVoiceChannelName))
            serverVoiceChannelName = "sitb-" + System.Guid.NewGuid().ToString("N");

        voiceChannelName = serverVoiceChannelName;
    }

    public override void OnStartLocalPlayer()
    {
        ApplyLocalState(true);
        TryStartVoice();
    }

    public override void OnStopClient()
    {
        if (proximityVoice != null)
            proximityVoice.StopVoice();
        ApplyLocalState(true);
    }

    private void OnVoiceChannelChanged(string oldChannel, string newChannel)
    {
        if (isLocalPlayer)
            TryStartVoice();
    }

    private void TryStartVoice()
    {
        if (!isLocalPlayer || string.IsNullOrEmpty(voiceChannelName))
            return;

        if (proximityVoice == null)
            proximityVoice = GetComponent<VivoxProximityVoice>();
        if (proximityVoice == null)
            proximityVoice = gameObject.AddComponent<VivoxProximityVoice>();

        Transform listenerTransform = transform;
        foreach (Camera cam in cameras)
        {
            if (cam != null && cam.enabled)
            {
                listenerTransform = cam.transform;
                break;
            }
        }

        proximityVoice.StartVoice(voiceChannelName, listenerTransform);
    }

    private void Update()
    {
        if (!isLocalPlayer || playerController == null)
            return;

        float pitch = playerController.Pitch;
        if (Mathf.Abs(pitch - lastSentPitch) < pitchSendThreshold)
            return;

        lastSentPitch = pitch;
        if (isServer)
            syncedPitch = pitch;
        else
            CmdSetPitch(pitch);
    }

    [Command]
    private void CmdSetPitch(float pitch)
    {
        syncedPitch = pitch;
    }

    private void OnPitchChanged(float oldPitch, float newPitch)
    {
        if (!isLocalPlayer)
            ApplyRemotePitch(newPitch);
    }

    private void ApplyRemotePitch(float pitch)
    {
        if (playerController != null)
            playerController.SetRemotePitch(pitch);

        if (remoteHeadPitch != null)
            remoteHeadPitch.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void ApplyLocalState(bool local)
    {
        CacheComponents();
        EnsureRemoteAvatar();

        if (playerController != null)
            playerController.SetInputEnabled(local);
        if (playerInteraction != null)
            playerInteraction.SetInputEnabled(local);
        if (itemChecklist != null)
            itemChecklist.SetInputEnabled(local);

        foreach (Camera cam in cameras)
        {
            if (cam != null)
                cam.enabled = local;
        }

        foreach (AudioListener listener in audioListeners)
        {
            if (listener != null)
                listener.enabled = local;
        }

        if (remoteAvatarRoot != null)
            remoteAvatarRoot.SetActive(!local);
    }

    private void CacheComponents()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (playerInteraction == null)
            playerInteraction = GetComponent<PlayerInteraction>();
        if (itemChecklist == null)
            itemChecklist = GetComponent<ItemChecklist>();

        cameras = GetComponentsInChildren<Camera>(true);
        audioListeners = GetComponentsInChildren<AudioListener>(true);
    }

    private void EnsureRemoteAvatar()
    {
        if (remoteAvatarRoot != null)
            return;

        Transform existing = transform.Find("RemoteAvatar");
        if (existing != null)
        {
            remoteAvatarRoot = existing.gameObject;
            remoteHeadPitch = existing.Find("HeadPitch");
            return;
        }

        remoteAvatarRoot = new GameObject("RemoteAvatar");
        remoteAvatarRoot.transform.SetParent(transform, false);
        remoteAvatarRoot.transform.localPosition = Vector3.zero;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(remoteAvatarRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.65f, 0.85f, 0.65f);
        Destroy(body.GetComponent<Collider>());

        GameObject headPivot = new GameObject("HeadPitch");
        headPivot.transform.SetParent(remoteAvatarRoot.transform, false);
        headPivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        remoteHeadPitch = headPivot.transform;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(headPivot.transform, false);
        head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        Destroy(head.GetComponent<Collider>());

        GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        facing.name = "Facing";
        facing.transform.SetParent(headPivot.transform, false);
        facing.transform.localPosition = new Vector3(0f, 0f, 0.33f);
        facing.transform.localScale = new Vector3(0.16f, 0.12f, 0.28f);
        Destroy(facing.GetComponent<Collider>());
    }
}
