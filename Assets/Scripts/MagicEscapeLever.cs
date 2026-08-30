using UnityEngine;

public sealed class MagicEscapeLever : Interactable
{
    [SerializeField] private int index;
    [SerializeField] private Transform handle;
    private bool stateReady;
    private bool wasPulled;

    public int Index => index;

    public void Configure(int value, Transform targetHandle)
    {
        index = value;
        handle = targetHandle;
        SetDisplayName(value == 0 ? "서쪽 비밀 레버" : "동쪽 비밀 레버");
    }

    public override string GetPrompt()
    {
        MagicEscapeGameManager game = MagicEscapeGameManager.Instance;
        if (game == null || game.Phase != MagicEscapePhase.TwinLevers) return "레버가 잠겨 있다";
        return (game.LeverMask & (1 << index)) != 0 ? "공명 중" : "레버 당기기";
    }

    public override void Interact(PlayerInteraction player) => MagicEscapeGameManager.Instance?.RequestLever(index);

    private void Update()
    {
        if (handle == null || MagicEscapeGameManager.Instance == null) return;
        bool pulled = (MagicEscapeGameManager.Instance.LeverMask & (1 << index)) != 0;
        if (!stateReady) { wasPulled = pulled; stateReady = true; }
        else if (pulled != wasPulled)
        {
            wasPulled = pulled;
            GameAudio.PlayAt("lever", transform.position + Vector3.up, 0.5f, pulled ? 0.94f : 1.04f);
        }
        handle.localRotation = Quaternion.Slerp(handle.localRotation,
            Quaternion.Euler(pulled ? -55f : 35f, 0f, 0f), Time.deltaTime * 9f);
    }
}
