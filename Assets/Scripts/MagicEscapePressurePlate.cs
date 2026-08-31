using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class MagicEscapePressurePlate : MonoBehaviour
{
    [SerializeField] private int index;
    [SerializeField] private Transform visual;
    [SerializeField] private float pressedDepth = 0.12f;
    private Collider area;
    private Vector3 visualStart;

    public int Index => index;
    public Collider Area => area != null ? area : GetComponent<Collider>();

    public void Configure(int value, Transform targetVisual)
    {
        index = value;
        visual = targetVisual;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
        if (visual != null) visualStart = visual.localPosition;
    }

    private void Update()
    {
        // Players already standing here when the phase starts are detected by
        // the server too; this component only renders the synchronized state.
        if (visual != null && MagicEscapeGameManager.Instance != null)
        {
            int bit = 1 << index;
            bool pressed = (MagicEscapeGameManager.Instance.PressureMask & bit) != 0 ||
                           (MagicEscapeGameManager.Instance.PressureLatchedMask & bit) != 0;
            visual.localPosition = Vector3.Lerp(visual.localPosition,
                visualStart + Vector3.down * (pressed ? pressedDepth : 0f), Time.deltaTime * 10f);
        }
    }

}
