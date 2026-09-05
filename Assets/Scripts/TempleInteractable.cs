using UnityEngine;

public sealed class TempleInteractable : Interactable
{
    public int index;
    public bool stone;
    public bool returnToLobby;
    public Transform handle;
    private bool pulled;
    public override string GetPrompt()
    {
        if (returnToLobby) return "폐광으로 내려가기";
        if (stone) return "석재 들기";
        if (StoneTempleManager.Instance != null && (StoneTempleManager.Instance.leverMask & (1 << index)) != 0)
            return "통로 고정 완료";
        return StoneTempleManager.Instance != null && !StoneTempleManager.Instance.CanLatchLever(index)
            ? "잠긴 레버" : "레버 당기기";
    }
    private void Update()
    {
        if (handle == null || StoneTempleManager.Instance == null) return;
        bool next = (StoneTempleManager.Instance.leverMask & (1 << index)) != 0;
        if (next && !pulled) GameAudio.PlayAt("lever", transform.position, 0.45f, 0.85f);
        pulled = next;
        handle.localRotation = Quaternion.Slerp(handle.localRotation,
            Quaternion.Euler(pulled ? -50 : 25, 0, 0), 1f - Mathf.Exp(-10f * Time.deltaTime));
    }
    public override void Interact(PlayerInteraction player)
    {
        if (returnToLobby) StoneTempleManager.Instance?.RequestReturn();
        else if (stone) StoneTempleManager.Instance?.RequestStone(index);
        else StoneTempleManager.Instance?.RequestLever(index);
    }
}
