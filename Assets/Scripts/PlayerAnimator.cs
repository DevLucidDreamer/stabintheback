using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 모델(goshi)의 이동 애니메이션을 구동한다.
///
/// 이 게임은 1인칭이라 로컬 플레이어는 자기 몸이 보이지 않고,
/// 남에게 보이는 것은 RemoteAvatar(=이 goshi 모델)뿐이다.
/// 따라서 애니메이터는 캐릭터가 실제로 이동한 거리(=플레이어 루트 Transform의 속도)를
/// 측정해서 구동한다. 이렇게 하면 로컬/원격을 가리지 않고,
/// 네트워크로 위치만 동기화되는 원격 아바타에서도 자연스럽게 동작한다.
///
/// AnimatorController 파라미터:
///  - IsMoving (bool)  : 수평 속도가 임계값 이상이면 true → Start→Run, 멈추면 Stop→Idle
///  - Jump    (trigger): 위로 튀어오르는 순간 → Jump
///  - Speed   (float)  : 수평 속도 (블렌드용, 선택)
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Tooltip("이동을 측정할 대상. 비워두면 이 오브젝트의 최상위 루트(=플레이어 본체)를 사용한다")]
    [SerializeField] private Transform motionSource;

    [Tooltip("이 속도(m/s) 이상이면 달리는 것으로 판단")]
    [SerializeField] private float moveThreshold = 0.6f;

    [Tooltip("이 상향 속도(m/s) 이상으로 튀어오르면 점프로 판단")]
    [SerializeField] private float jumpVelocityThreshold = 2.5f;

    [Tooltip("점프 트리거가 연달아 발동되지 않도록 하는 최소 간격(초)")]
    [SerializeField] private float jumpCooldown = 0.35f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Animator animator;
    private Vector3 lastPos;
    private float smoothedVy;
    private float lastJumpTime = -999f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (motionSource == null)
            motionSource = transform.root;
    }

    private void OnEnable()
    {
        // 활성화(원격에서 보이기 시작) 시점에 기준 위치를 초기화해 튀는 것을 방지.
        if (motionSource != null)
            lastPos = motionSource.position;
        smoothedVy = 0f;
    }

    private void Update()
    {
        if (animator == null || motionSource == null)
            return;

        // AnimatorController가 안 붙어 있으면 파라미터를 건드릴 때마다
        // "Animator is not playing an AnimatorController" 에러가 매 프레임 쏟아진다.
        // 한 번만 알려 주고 스스로 꺼진다 (셋업을 돌리면 정상으로 돌아온다).
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning(
                "[PlayerAnimator] AnimatorController가 없어 애니메이션을 끕니다.\n" +
                "Tools > Player > Setup Player 를 실행해 캐릭터를 다시 셋업하세요.", this);
            enabled = false;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        Vector3 pos = motionSource.position;
        Vector3 delta = pos - lastPos;
        lastPos = pos;

        // 수평 속도 → 달리기/정지 판단
        Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
        float speed = horizontal.magnitude / dt;

        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(IsMovingHash, speed > moveThreshold);

        // 수직 속도 → 점프 감지 (약간 평활화해 네트워크 보간 노이즈를 줄임)
        float vy = delta.y / dt;
        smoothedVy = Mathf.Lerp(smoothedVy, vy, 0.5f);

        if (smoothedVy > jumpVelocityThreshold && Time.time - lastJumpTime > jumpCooldown)
        {
            lastJumpTime = Time.time;
            animator.SetTrigger(JumpHash);
        }
    }
}
