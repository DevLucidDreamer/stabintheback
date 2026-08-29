using UnityEngine;

/// <summary>
/// Openable의 열림 토글을 가로채는 네트워크 컨트롤러(있을 때만).
/// 멀티플레이에서는 NetworkOpenable이 이 인터페이스를 구현해 서버 권한으로 상태를 확정한다.
/// </summary>
public interface IOpenableNetworkController
{
    void RequestToggle();
}

/// <summary>
/// 열고 닫을 수 있는 오브젝트 (서랍 = Slide, 냉장고 문 = Hinge).
/// Interact 할 때마다 열림/닫힘 상태를 토글하고 부드럽게 애니메이션한다.
/// 같은 오브젝트에 IOpenableNetworkController가 있으면 토글을 그쪽에 위임한다(멀티플레이).
/// </summary>
public class Openable : Interactable
{
    public enum Mode { Slide, Hinge }

    [Tooltip("Slide: 지정 방향으로 미끄러짐(서랍) / Hinge: 지정 각도로 회전(문)")]
    public Mode mode = Mode.Hinge;

    [Tooltip("실제로 움직일 부분. 비우면 자기 자신이 움직인다")]
    public Transform movingPart;

    [Tooltip("Slide 모드: 열렸을 때 이동량 (movingPart 자신의 로컬 축 기준)")]
    public Vector3 slideOffset = new Vector3(0f, 0f, 0.45f);

    [Tooltip("Hinge 모드: 열렸을 때 추가되는 로컬 회전(오일러 각)")]
    public Vector3 hingeOpenEuler = new Vector3(0f, 110f, 0f);

    [Tooltip("열리고 닫히는 데 걸리는 시간(초)")]
    public float duration = 0.25f;

    private bool isOpen;
    private float t; // 0 = 닫힘, 1 = 열림
    private Vector3 closedPos;
    private Quaternion closedRot;
    private IOpenableNetworkController netController; // 있으면 멀티플레이 경로

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (movingPart == null)
            movingPart = transform;
        closedPos = movingPart.localPosition;
        closedRot = movingPart.localRotation;
        netController = GetComponent<IOpenableNetworkController>();
    }

    private void Update()
    {
        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(t, target))
            return;

        t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
        float s = Mathf.SmoothStep(0f, 1f, t);

        if (mode == Mode.Slide)
            // 오프셋을 닫힌 상태의 로컬 회전으로 변환해, 서랍이 '자기가 향한 방향'으로 열리게 한다.
            movingPart.localPosition = closedPos + closedRot * slideOffset * s;
        else
            movingPart.localRotation = closedRot * Quaternion.Euler(hingeOpenEuler * s);
    }

    public override string GetPrompt() => DisplayName + (isOpen ? " 닫기" : " 열기");

    public override void Interact(PlayerInteraction player)
    {
        // 멀티플레이면 서버 권한 토글을 요청하고, 아니면 로컬에서 바로 토글한다.
        if (netController != null)
            netController.RequestToggle();
        else
            SetOpen(!isOpen);
    }

    /// <summary>열림 상태를 확정한다. 로컬 토글 또는 네트워크 동기화에서 호출된다. 애니메이션은 Update가 처리.</summary>
    public void SetOpen(bool open)
    {
        if (isOpen == open)
            return;

        isOpen = open;
        GameAudio.PlayAt(open ? "door_open" : "door_close", transform.position, 0.38f,
            Random.Range(0.96f, 1.04f), 1.2f, 12f);
    }
}
