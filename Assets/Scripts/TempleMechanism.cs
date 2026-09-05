using Mirror;
using UnityEngine;

/// <summary>Shared network-clock animation. Walking surfaces never translate under a capsule.</summary>
public sealed class TempleMechanism : MonoBehaviour
{
    public int index;
    public Transform visual;
    public Collider[] solid;
    public Vector3 inactiveOffset = Vector3.down * 4f;
    public float duration = 0.8f;
    public bool gate;
    private Vector3 rest;
    private bool initialized;
    private bool previous;

    private void Start() { rest = visual.localPosition; }
    private void Update()
    {
        StoneTempleManager game = StoneTempleManager.Instance;
        if (game == null || visual == null || game.Motions.Count <= index) return;
        TempleMotion motion = game.Motions[index];
        float progress = Mathf.Clamp01((float)(NetworkTime.time - motion.changedAt) / duration);
        float target = motion.active ? 1f : 0f;
        float fraction = Mathf.Lerp(motion.from, target, Mathf.SmoothStep(0f, 1f, progress));
        visual.localPosition = rest + inactiveOffset * (gate ? fraction : 1f - fraction);
        // Fixed collision geometry is only present after the rising animation finishes.
        // Doors become solid only when fully closed, so a closing door cannot crush a player.
        foreach (Collider col in solid)
            if (col != null) col.enabled = gate ? !motion.active && progress >= 1f : motion.active && progress >= 1f;
        if (initialized && previous != motion.active)
            GameAudio.PlayAt("gate_open", transform.position, 0.32f, 0.78f, 2f, 24f);
        previous = motion.active;
        initialized = true;
    }
}

public struct TempleMotion
{
    public bool active;
    public double changedAt;
    public float from;
}
