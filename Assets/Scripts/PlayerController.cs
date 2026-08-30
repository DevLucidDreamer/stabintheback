using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 1인칭 캐릭터 컨트롤러.
/// WASD(또는 방향키)로 이동하고, 마우스로 시야를 조정한다.
/// 새 Input System의 디바이스를 직접 읽으므로 인스펙터에 별도 연결이 필요 없다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("기본 걷기 속도 (m/s)")]
    [SerializeField] private float moveSpeed = 6f;
    [Tooltip("Shift를 누르고 있을 때 달리기 속도 (m/s)")]
    [SerializeField] private float sprintSpeed = 10f;
    [Tooltip("점프 높이 (m)")]
    [SerializeField] private float jumpHeight = 1.2f;
    [Tooltip("중력 가속도 (m/s^2). 아래로 향하므로 음수")]
    [SerializeField] private float gravity = -20f;

    [Header("시야 (마우스)")]
    [Tooltip("마우스 감도")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [Tooltip("위/아래로 올려다보거나 내려다볼 수 있는 최대 각도")]
    [SerializeField] private float pitchLimit = 85f;
    [Tooltip("시야를 담당하는 카메라. 비워두면 자식 카메라를 자동으로 찾는다")]
    [SerializeField] private Transform cameraTransform;

    [Header("밀기")]
    [Tooltip("Rigidbody가 달린 물체(큐브 등)를 밀 때의 속도")]
    [SerializeField] private float pushPower = 3f;

    private CharacterController controller;
    private float pitch;            // 카메라 상하 회전 누적값(도)
    private float verticalVelocity; // 중력/점프에 의한 수직 속도
    private bool inputEnabled = true; // 이 캐릭터를 내가 조종하는가 (원격 아바타면 false)
    private bool inputPaused;         // UI가 열려 잠시 조작만 멈춘 상태 (중력은 계속 돈다)

    public float Pitch => pitch;
    public Transform CameraTransform => cameraTransform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 카메라가 지정되지 않았으면 자식에서 찾는다.
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }
    }

    private void OnEnable()
    {
        if (inputEnabled)
            LockCursor(true);
    }

    private void OnDisable()
    {
        if (inputEnabled)
            LockCursor(false);
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        if (!inputPaused)
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);

            LookAround();
        }

        // 창이 열려 있어도 Move는 계속 부른다.
        // CharacterController는 Move를 부르지 않으면 접지 판정도 멈춰 버려서,
        // 조작을 통째로 끄면 캐릭터가 바닥을 뚫고 가라앉는다.
        Move();
    }

    /// <summary>마우스 이동량으로 몸(좌우)과 카메라(상하)를 회전시킨다.</summary>
    private void LookAround()
    {
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        // 좌우: 몸 전체를 월드 Y축 기준으로 회전
        transform.Rotate(Vector3.up, delta.x, Space.World);

        // 상하: 카메라만 X축으로 회전하되 범위를 제한
        if (cameraTransform != null)
        {
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    /// <summary>WASD 입력으로 이동하고 중력/점프를 적용한다.</summary>
    private void Move()
    {
        Vector2 input = ReadMoveInput();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        bool sprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float speed = sprinting ? sprintSpeed : moveSpeed;

        if (controller.isGrounded)
        {
            // 접지 상태에서는 수직 속도를 살짝 눌러 바닥에 붙여 둔다.
            if (verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (!inputPaused && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>키보드에서 WASD / 방향키를 읽어 -1~1 방향 벡터로 반환한다.</summary>
    private Vector2 ReadMoveInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || inputPaused)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;

        return new Vector2(x, y).normalized;
    }

    /// <summary>
    /// CharacterController가 다른 콜라이더와 부딪힐 때 호출된다.
    /// 부딪힌 대상에 Rigidbody가 있으면 진행 방향으로 밀어낸다.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Rigidbody가 없거나 물리로 움직이지 않는(kinematic) 물체는 밀지 않는다.
        if (body == null || body.isKinematic)
            return;

        // 바닥처럼 아래쪽으로 부딪히는 경우는 무시한다.
        if (hit.moveDirection.y < -0.3f)
            return;

        // 수평 방향으로만 민다. (Y는 물체의 낙하/중력 속도를 유지)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        body.linearVelocity = new Vector3(pushDir.x * pushPower, body.linearVelocity.y, pushDir.z * pushPower);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (isActiveAndEnabled)
            LockCursor(enabled);
    }

    /// <summary>
    /// 메뉴/옵션 창이 열려 있는 동안 조작만 멈춘다.
    /// SetInputEnabled(false)와 달리 중력·접지는 계속 처리되므로 캐릭터가 가라앉지 않는다.
    /// </summary>
    public void SetInputPaused(bool paused)
    {
        inputPaused = paused;
        if (inputEnabled && isActiveAndEnabled)
            LockCursor(!paused);
    }

    public void SetRemotePitch(float remotePitch)
    {
        pitch = remotePitch;
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
