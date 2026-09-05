using UnityEngine;

/// <summary>State-derived visuals and fixed collision; joins and reconnects need no replay.</summary>
public sealed class ExpeditionVisuals : MonoBehaviour
{
    public GameObject[] bridgeStones;
    public Collider[] bridgeFloors;
    public Transform buildPost;
    public Transform[] gates;
    public Collider[] gateColliders;
    public Renderer[] handLights;
    public Light ritualLight;
    public GameObject ritualBeam;
    private int oldPhase = -1;
    private void Update()
    {
        var game = ExpeditionManager.Instance;
        if (game == null) return;
        if (game.stage == 2)
        {
            for (int i = 0; i < bridgeStones.Length; i++) bridgeStones[i].SetActive(i < game.delivered);
            for (int i = 0; i < bridgeFloors.Length; i++) bridgeFloors[i].enabled = i < game.BridgeSections;
            if (buildPost != null) buildPost.position = game.BuildPosition;
        }
        for (int i = 0; i < gates.Length; i++)
        {
            bool open = game.stage == 2 ? game.completed : game.phase > i;
            gates[i].localPosition = Vector3.Lerp(gates[i].localPosition, Vector3.up * (open ? 7f : 0f), 1f - Mathf.Exp(-3f * Time.deltaTime));
            gateColliders[i].enabled = !open && gates[i].localPosition.y < 0.03f;
        }
        for (int i = 0; i < handLights.Length; i++)
            handLights[i].gameObject.SetActive(game.Hands.Count > i && game.Hands[i] != 0);
        if (ritualLight != null) ritualLight.intensity = game.completed ? 12f : 1f + game.ritualProgress * 7f;
        if (ritualBeam != null) ritualBeam.SetActive(game.completed);
        if (oldPhase >= 0 && game.phase > oldPhase) GameAudio.PlayAt("gate_open", transform.position, 0.6f, 0.75f, 3f, 70f);
        if (game.stage == 3 && game.completed && oldPhase != 3) GameHud.Ensure().ShowBanner("의식 완료", 4f);
        oldPhase = game.phase;
    }
}
