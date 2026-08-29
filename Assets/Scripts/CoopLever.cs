using UnityEngine;

public class CoopLever : Interactable
{
    [SerializeField] private int leverIndex;
    [SerializeField] private Transform handle;
    private bool audioStateReady;
    private bool wasPulled;

    public int LeverIndex => leverIndex;

    public void Configure(int index, Transform leverHandle)
    {
        leverIndex = index;
        handle = leverHandle;
        SetDisplayName(index == 0 ? "서쪽 공명 레버" : "동쪽 공명 레버");
    }

    public override string GetPrompt()
    {
        FortressGameManager game = FortressGameManager.Instance;
        if (game == null || game.Phase != FortressPhase.TwinLevers) return "봉인되어 있다";
        return (game.LeverMask & (1 << leverIndex)) != 0 ? "공명 중" : "레버 당기기";
    }

    public override void Interact(PlayerInteraction player) => FortressGameManager.Instance?.RequestLever(leverIndex);

    private void Update()
    {
        if (handle == null || FortressGameManager.Instance == null) return;
        bool pulled = (FortressGameManager.Instance.LeverMask & (1 << leverIndex)) != 0;
        if (!audioStateReady)
        {
            wasPulled = pulled;
            audioStateReady = true;
        }
        else if (pulled != wasPulled)
        {
            wasPulled = pulled;
            GameAudio.PlayAt("lever", transform.position + Vector3.up, 0.5f,
                pulled ? 0.94f : 1.04f, 1.5f, 16f);
        }

        handle.localRotation = Quaternion.Slerp(handle.localRotation,
            Quaternion.Euler(pulled ? -55f : 35f, 0f, 0f), Time.deltaTime * 9f);
    }
}
