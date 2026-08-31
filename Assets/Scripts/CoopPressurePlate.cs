using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoopPressurePlate : MonoBehaviour
{
    [SerializeField] private int plateIndex;
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressedDepth = 0.12f;

    private Collider area;
    private Vector3 visualStart;

    public int PlateIndex => plateIndex;
    public Collider Area => area != null ? area : GetComponent<Collider>();

    public void Configure(int index, Transform visual)
    {
        plateIndex = index;
        plateVisual = visual;
    }

    private void Awake()
    {
        area = GetComponent<Collider>();
        area.isTrigger = true;
        if (plateVisual != null) visualStart = plateVisual.localPosition;
    }

    private void Update()
    {
        // Occupancy comes from the server's continuous position checks, not a
        // one-shot client enter request that can arrive before its movement.
        if (plateVisual != null && FortressGameManager.Instance != null)
        {
            bool pressed = (FortressGameManager.Instance.PressureMask & (1 << plateIndex)) != 0 ||
                           (FortressGameManager.Instance.PressureLatchedMask & (1 << plateIndex)) != 0;
            Vector3 target = visualStart + Vector3.down * (pressed ? pressedDepth : 0f);
            plateVisual.localPosition = Vector3.Lerp(plateVisual.localPosition, target, Time.deltaTime * 10f);
        }
    }

}
