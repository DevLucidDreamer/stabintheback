using UnityEngine;

public class FortressGate : MonoBehaviour
{
    [SerializeField] private FortressPhase opensAtPhase;
    [SerializeField] private Vector3 openOffset = new Vector3(0f, 5.5f, 0f);
    [SerializeField] private float moveSpeed = 2.5f;
    private Vector3 closedPosition;
    private bool audioStateReady;
    private bool wasOpen;

    public void Configure(FortressPhase phase, Vector3 offset)
    {
        opensAtPhase = phase;
        openOffset = offset;
    }

    private void Awake() => closedPosition = transform.localPosition;

    private void Update()
    {
        FortressGameManager game = FortressGameManager.Instance;
        bool open = game != null && game.Phase >= opensAtPhase;
        if (!audioStateReady)
        {
            wasOpen = open;
            audioStateReady = true;
        }
        else if (open != wasOpen)
        {
            wasOpen = open;
            if (open)
                GameAudio.PlayAt("gate_open", transform.position, 0.65f, 0.86f, 2f, 24f);
        }

        transform.localPosition = Vector3.MoveTowards(transform.localPosition,
            closedPosition + (open ? openOffset : Vector3.zero), moveSpeed * Time.deltaTime);
    }
}
