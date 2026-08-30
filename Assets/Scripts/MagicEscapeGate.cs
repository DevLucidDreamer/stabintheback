using UnityEngine;

public sealed class MagicEscapeGate : MonoBehaviour
{
    [SerializeField] private MagicEscapePhase opensAtPhase;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 5.5f, 0f);
    [SerializeField] private float speed = 2.5f;
    private Vector3 closedPosition;
    private bool stateReady;
    private bool wasOpen;

    public void Configure(MagicEscapePhase phase, Vector3 offset)
    {
        opensAtPhase = phase;
        openOffset = offset;
    }

    private void Awake() => closedPosition = transform.localPosition;

    private void Update()
    {
        bool open = MagicEscapeGameManager.Instance != null && MagicEscapeGameManager.Instance.Phase >= opensAtPhase;
        if (!stateReady) { wasOpen = open; stateReady = true; }
        else if (open != wasOpen)
        {
            wasOpen = open;
            if (open) GameAudio.PlayAt("gate_open", transform.position, 0.65f, 0.86f, 2f, 24f);
        }
        transform.localPosition = Vector3.MoveTowards(transform.localPosition,
            closedPosition + (open ? openOffset : Vector3.zero), speed * Time.deltaTime);
    }
}
