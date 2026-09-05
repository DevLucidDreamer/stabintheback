using UnityEngine;

public enum ExpeditionAction { Build, Socket, Altar, Exit }
public sealed class ExpeditionNode : Interactable
{
    public int index;
    public int slot;
    public ExpeditionAction action;
    public override string GetPrompt()
    {
        var game = ExpeditionManager.Instance;
        if (action == ExpeditionAction.Build) return game != null && game.delivered == ExpeditionManager.BridgeUnits ? "다리 완성" : "석재 쌓기";
        if (action == ExpeditionAction.Socket) return game != null && game.phase > 0 ? "자리 고정 완료" : "조각상 놓기";
        if (action == ExpeditionAction.Altar) return game != null && game.completed ? "의식 완료" :
            game != null && game.phase >= 2 ? "제단에 손 얹기" : "봉인된 제단";
        return game != null && game.stage == 2 ? "지하 제단으로 내려가기" : "대기실로 돌아가기";
    }
    public override void Interact(PlayerInteraction player) => ExpeditionManager.Instance?.RequestUse(index);
}
