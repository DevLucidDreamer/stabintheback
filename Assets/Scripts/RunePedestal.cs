using UnityEngine;

public class RunePedestal : Interactable
{
    [SerializeField] private int runeIndex;
    [SerializeField] private string runeName = "룬";
    [SerializeField] private Renderer glowRenderer;
    private Color baseColor;

    public int RuneIndex => runeIndex;

    public void Configure(int index, string label, Renderer glow)
    {
        runeIndex = index;
        runeName = label;
        glowRenderer = glow;
        SetDisplayName(label + " 룬");
    }

    private void Awake()
    {
        if (glowRenderer != null) baseColor = glowRenderer.material.color;
    }

    public override string GetPrompt()
    {
        FortressGameManager game = FortressGameManager.Instance;
        return game != null && game.Phase == FortressPhase.RuneCipher
            ? runeName + " 룬 누르기"
            : "아직 반응하지 않는다";
    }

    public override void Interact(PlayerInteraction player)
    {
        FortressGameManager.Instance?.RequestRune(runeIndex);
    }

    private void Update()
    {
        if (glowRenderer == null || FortressGameManager.Instance == null) return;
        bool active = FortressGameManager.Instance.Phase == FortressPhase.RuneCipher;
        glowRenderer.material.color = Color.Lerp(baseColor * 0.4f, baseColor * (1.15f + Mathf.Sin(Time.time * 3f) * 0.2f), active ? 1f : 0f);
    }
}
