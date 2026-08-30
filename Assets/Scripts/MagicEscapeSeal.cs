using Mirror;
using UnityEngine;

public sealed class MagicEscapeSeal : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Renderer[] visuals;
    private Collider[] colliders;

    public int Index => index;

    public void Configure(int value, Renderer[] targets)
    {
        index = value;
        visuals = targets;
    }

    private void Awake() => colliders = GetComponentsInChildren<Collider>(true);
    private void OnEnable() => PlayerInteraction.OnSwingHit += OnSwingHit;
    private void OnDisable() => PlayerInteraction.OnSwingHit -= OnSwingHit;

    private void OnSwingHit(PlayerInteraction attacker, Collider hit)
    {
        if (hit == null || !hit.transform.IsChildOf(transform)) return;
        NetworkIdentity identity = attacker.GetComponent<NetworkIdentity>();
        if (identity != null && !identity.isLocalPlayer) return;
        if (attacker.HeldWeapon == null)
        {
            GameHud.Ensure().ShowToast("마검이 아니면 봉인을 깰 수 없다", 1.5f);
            return;
        }
        MagicEscapeGameManager.Instance?.RequestBreakSeal(index);
    }

    private void Update()
    {
        MagicEscapeGameManager game = MagicEscapeGameManager.Instance;
        if (game == null) return;
        bool broken = (game.BrokenSealMask & (1 << index)) != 0;
        if (visuals != null)
            foreach (Renderer renderer in visuals)
                if (renderer != null) renderer.enabled = !broken;
        if (colliders != null)
            foreach (Collider collider in colliders)
                if (collider != null) collider.enabled = !broken;
        if (!broken) transform.localRotation *= Quaternion.Euler(0f, Time.deltaTime * 35f, 0f);
    }
}
