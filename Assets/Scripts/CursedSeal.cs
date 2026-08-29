using Mirror;
using UnityEngine;

public class CursedSeal : MonoBehaviour
{
    [SerializeField] private int sealIndex;
    [SerializeField] private Renderer[] renderers;
    private Collider[] colliders;

    public int SealIndex => sealIndex;

    public void Configure(int index, Renderer[] visuals)
    {
        sealIndex = index;
        renderers = visuals;
    }

    private void Awake() => colliders = GetComponentsInChildren<Collider>(true);
    private void OnEnable() => PlayerInteraction.OnSwingHit += OnSwingHit;
    private void OnDisable() => PlayerInteraction.OnSwingHit -= OnSwingHit;

    private void OnSwingHit(PlayerInteraction attacker, Collider hit)
    {
        if (hit == null || !hit.transform.IsChildOf(transform)) return;
        NetworkIdentity id = attacker.GetComponent<NetworkIdentity>();
        if (id != null && !id.isLocalPlayer) return;
        if (attacker.HeldWeapon == null)
        {
            GameHud.Ensure().ShowToast("맨손으로는 봉인을 깰 수 없다", 1.5f);
            return;
        }
        FortressGameManager.Instance?.RequestBreakSeal(sealIndex);
    }

    private void Update()
    {
        FortressGameManager game = FortressGameManager.Instance;
        if (game == null) return;
        bool broken = (game.BrokenSealMask & (1 << sealIndex)) != 0;
        foreach (Renderer r in renderers)
            if (r != null) r.enabled = !broken;
        if (colliders != null)
            foreach (Collider c in colliders)
                if (c != null) c.enabled = !broken;
        if (!broken)
            transform.localRotation *= Quaternion.Euler(0f, Time.deltaTime * 35f, 0f);
    }
}
