using Mirror;
using UnityEngine;

/// <summary>Foot-space occupancy, sampled from replicated transforms. No trigger counters.</summary>
public sealed class TemplePressurePlate : MonoBehaviour
{
    public int index;
    public Vector2 halfSize = new Vector2(1.05f, 1.05f);
    public Transform visual;
    public bool acceptsStone;
    private Vector3 rest;
    private bool wasPressed;
    private bool initialized;

    private void Start() { if (visual != null) rest = visual.localPosition; }

    public bool ContainsFoot(Vector3 foot, float tolerance = 0f)
    {
        if (!isActiveAndEnabled || !ServerInteractionGuard.IsFinite(foot)) return false;
        Vector3 local = transform.InverseTransformPoint(foot);
        return Mathf.Abs(local.x) <= halfSize.x + tolerance &&
               Mathf.Abs(local.z) <= halfSize.y + tolerance && local.y >= -0.12f && local.y <= 0.22f;
    }

    public bool ContainsPlayer(NetworkConnectionToClient connection, bool wasOnPlate = false)
    {
        if (!ServerInteractionGuard.HasPlayer(connection)) return false;
        CharacterController cc = connection.identity.GetComponent<CharacterController>();
        if (cc == null || !cc.enabled || !cc.gameObject.activeInHierarchy) return false;
        Vector3 scale = cc.transform.lossyScale;
        Vector3 center = cc.transform.TransformPoint(cc.center);
        float height = Mathf.Max(cc.height * Mathf.Abs(scale.y),
            cc.radius * 2f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        return ContainsFoot(center - Vector3.up * (height * 0.5f), wasOnPlate ? 0.08f : 0f);
    }

    private void Update()
    {
        var game = StoneTempleManager.Instance;
        if (visual == null || game == null && ExpeditionManager.Instance == null) return;
        bool pressed = game != null ? game.IsPressed(index) : ExpeditionManager.Instance.IsPressed(index);
        if (initialized && pressed != wasPressed)
            GameAudio.PlayAt("lever", transform.position, 0.26f, pressed ? 0.8f : 1.05f);
        initialized = true;
        wasPressed = pressed;
        visual.localPosition = Vector3.Lerp(visual.localPosition,
            rest + Vector3.down * (pressed ? 0.07f : 0f), 1f - Mathf.Exp(-14f * Time.deltaTime));
    }
}
