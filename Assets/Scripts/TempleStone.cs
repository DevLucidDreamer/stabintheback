using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TempleStone : MonoBehaviour
{
    public int index;
    public Vector3 home;
    private Collider solid;
    private void Awake() { solid = GetComponent<Collider>(); }
    private void LateUpdate()
    {
        var game = StoneTempleManager.Instance;
        if (game == null || game.Stones.Count <= index) return;
        TempleStoneState state = game.Stones[index];
        solid.enabled = state.holder == 0;
        if (state.holder != 0 && NetworkClient.spawned.TryGetValue(state.holder, out var holder))
        {
            transform.position = holder.transform.position + Vector3.up * 1.15f + holder.transform.forward * 1.25f;
            if (holder.isLocalPlayer && Cursor.lockState == CursorLockMode.Locked && !InGamePauseMenu.IsOpen &&
                Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
                game.RequestStone(index);
        }
        else transform.position = state.position;
    }
}

public struct TempleStoneState
{
    public uint holder;
    public Vector3 position;
}
