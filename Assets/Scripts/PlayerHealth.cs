using Mirror;
using UnityEngine;

/// <summary>
/// 서버가 사망 대기 → 리스폰 → 보호 종료를 결정한다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float deathViewDuration = 2f;
    [Tooltip("리스폰한 순간부터 적용되는 무적 시간(초)")]
    [SerializeField, Min(0f)] private float spawnProtection = 1.5f;

    // One atomic update keeps pose, respawn position and protection time together.
    public struct LifeState
    {
        public bool dead;
        public uint deathSequence;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 blowDirection;
        public double endsAt;
    }

    [SyncVar(hook = nameof(OnLifeChanged))] private LifeState life;
    private CharacterController controller;
    private PlayerRagdoll ragdoll;
    private PlayerRespawnVisuals visuals;
    private uint shownDeathSequence;
    private GameObject corpse;

    public bool IsDead => life.dead;
    public bool IsSpawnProtected => !IsDead && NetworkTime.time < life.endsAt;
    public float ProtectionRemaining => IsSpawnProtected ? (float)(life.endsAt - NetworkTime.time) : 0f;
    public double ProtectionEndsAt => life.endsAt;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        ragdoll = GetComponent<PlayerRagdoll>();
        visuals = GetComponent<PlayerRespawnVisuals>();
        if (visuals == null) visuals = gameObject.AddComponent<PlayerRespawnVisuals>();
    }

    public override void OnStartClient() => RefreshPresentation();
    public override void OnStartLocalPlayer() => RefreshPresentation();

    [ServerCallback]
    private void Update()
    {
        if (life.dead && NetworkTime.time >= life.endsAt)
            ServerRespawn();
    }

    [Server]
    public void ServerKill(uint killerNetId, Vector3 blowDirection = default)
    {
        if (IsDead || IsSpawnProtected) return;
        if (!ServerInteractionGuard.IsFinite(blowDirection) || blowDirection.sqrMagnitude < 0.0001f)
            blowDirection = transform.forward;

        life = new LifeState
        {
            dead = true,
            deathSequence = life.deathSequence + 1,
            position = transform.position,
            rotation = transform.rotation,
            blowDirection = blowDirection.normalized,
            endsAt = NetworkTime.time + deathViewDuration,
        };
        controller.enabled = false;
        WeaponNetworkManager.Instance?.ServerDropWeaponOf(netId, transform.position + Vector3.up * 0.5f);
    }

    [Server]
    private void ServerRespawn()
    {
        Transform spawn = NetworkManager.singleton != null ? NetworkManager.singleton.GetStartPosition() : null;
        Vector3 position = spawn != null ? spawn.position : life.position;
        Quaternion rotation = spawn != null ? spawn.rotation : life.rotation;
        Teleport(position, rotation);
        life = new LifeState
        {
            deathSequence = life.deathSequence,
            position = position,
            rotation = rotation,
            endsAt = NetworkTime.time + spawnProtection,
        };
    }

    private void OnLifeChanged(LifeState previous, LifeState current)
    {
        if (previous.dead && !current.dead)
            Teleport(current.position, current.rotation);
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (!isClient) return;
        controller.enabled = !IsDead;
        if (IsDead)
        {
            if (shownDeathSequence != life.deathSequence)
            {
                shownDeathSequence = life.deathSequence;
                if (ragdoll != null)
                    corpse = ragdoll.SpawnCorpse(life.position, life.rotation, life.blowDirection,
                        Mathf.Max(0.1f, (float)(life.endsAt - NetworkTime.time)) + 0.2f);
            }
            if (isLocalPlayer && visuals.BeginDeathView(corpse, life.position, life.rotation))
                GameHud.Ensure().ShowToast("당했다! 잠시 후 리스폰", 2f, new Color(1f, 0.4f, 0.35f));
        }
        else
        {
            bool wasViewingDeath = visuals.EndDeathView();
            if (wasViewingDeath && isLocalPlayer)
                GameHud.Ensure().ShowToast($"리스폰 보호 중 · {ProtectionRemaining:0.0}초", ProtectionRemaining,
                    new Color(0.55f, 0.9f, 1f));
        }
        GetComponent<NetworkPlayerSetup>()?.RefreshLifeState();
    }

    private void Teleport(Vector3 position, Quaternion rotation)
    {
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        // Clear old interpolation snapshots on server, owner and observers.
        GetComponent<NetworkTransformBase>()?.ResetState();
        GetComponent<PlayerController>()?.ResetMotionAfterRespawn();
        controller.enabled = true;
    }
}
