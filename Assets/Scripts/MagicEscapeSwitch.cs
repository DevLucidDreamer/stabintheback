using UnityEngine;

public sealed class MagicEscapeSwitch : Interactable
{
    [SerializeField] private int index;
    [SerializeField] private Transform handle;
    [SerializeField] private Renderer indicator;
    private Color baseColor = Color.magenta;

    public int Index => index;

    public void Configure(int value, Transform switchHandle, Renderer lightRenderer)
    {
        index = value;
        handle = switchHandle;
        indicator = lightRenderer;
        SetDisplayName("숨은 봉인 스위치");
    }

    private void Awake()
    {
        if (indicator != null) baseColor = indicator.material.color;
    }

    public override string GetPrompt()
    {
        MagicEscapeGameManager game = MagicEscapeGameManager.Instance;
        if (game == null || game.Phase != MagicEscapePhase.HiddenSwitches) return "이미 잠금이 풀렸다";
        return (game.SwitchMask & (1 << index)) != 0 ? "작동 완료" : "숨은 스위치 누르기";
    }

    public override void Interact(PlayerInteraction player) => MagicEscapeGameManager.Instance?.RequestSwitch(index);

    private void Update()
    {
        MagicEscapeGameManager game = MagicEscapeGameManager.Instance;
        bool active = game != null && (game.SwitchMask & (1 << index)) != 0;
        if (handle != null)
            handle.localRotation = Quaternion.Slerp(handle.localRotation,
                Quaternion.Euler(active ? -50f : 25f, 0f, 0f), Time.deltaTime * 10f);
        if (indicator != null)
            indicator.material.color = active ? new Color(0.2f, 1f, 0.55f) : baseColor;
    }
}
