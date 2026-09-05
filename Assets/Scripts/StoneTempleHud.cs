using UnityEngine;

// Puzzle solutions are communicated by the architecture and moving mechanisms.
public sealed class StoneTempleHud : MonoBehaviour
{
    private void Start() => ClearHints();
    private void OnDisable() => ClearHints();
    private static void ClearHints()
    {
        if (GameHud.Current == null) return;
        GameHud.Current.SetGoal(string.Empty);
        GameHud.Current.SetTopLeft(string.Empty);
    }
}
