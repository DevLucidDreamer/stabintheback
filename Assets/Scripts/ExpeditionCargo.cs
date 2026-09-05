using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ExpeditionCargo : Interactable
{
    public int index;
    public int kind;
    public Vector3 home;
    public override string GetPrompt()
    {
        var game = ExpeditionManager.Instance;
        if (game != null && game.stage == 3) return game.phase > 0 ? "고정된 조각상" : "조각상 들기";
        return "석재 들기";
    }
    public override void Interact(PlayerInteraction player) => ExpeditionManager.Instance?.RequestCargo(index);
    private void LateUpdate()
    {
        var game = ExpeditionManager.Instance;
        if (game == null || index >= game.Cargo.Count) return;
        var state = game.Cargo[index];
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = state.holder == 0;
        if (state.holder != 0 && NetworkClient.spawned.TryGetValue(state.holder, out var holder))
        {
            transform.position = holder.transform.position + Vector3.up * 1.1f + holder.transform.forward * 1.25f;
            if (holder.isLocalPlayer && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame &&
                Cursor.lockState == CursorLockMode.Locked && !InGamePauseMenu.IsOpen) game.RequestCargo(index);
        }
        else transform.position = state.position;
    }
}
