using UnityEngine;

public sealed class MagicEscapeRune : Interactable
{
    [SerializeField] private int index;
    [SerializeField] private string runeName = "룬";
    [SerializeField] private Renderer glow;
    private Color baseColor;

    public int Index => index;

    public void Configure(int value, string label, Renderer renderer)
    {
        index = value;
        runeName = label;
        glow = renderer;
        SetDisplayName(label + " 룬");
    }

    private void Awake()
    {
        if (glow != null) baseColor = glow.material.color;
    }

    public override string GetPrompt()
    {
        MagicEscapeGameManager game = MagicEscapeGameManager.Instance;
        return game != null && game.Phase == MagicEscapePhase.SplitCipher
            ? runeName + " 룬 누르기" : "룬이 잠들어 있다";
    }

    public override void Interact(PlayerInteraction player) => MagicEscapeGameManager.Instance?.RequestRune(index);

    private void Update()
    {
        if (glow == null) return;
        bool ready = MagicEscapeGameManager.Instance != null &&
                     MagicEscapeGameManager.Instance.Phase == MagicEscapePhase.SplitCipher;
        glow.material.color = ready
            ? baseColor * (1.15f + Mathf.Sin(Time.time * 3f) * 0.2f)
            : baseColor * 0.35f;
    }
}
