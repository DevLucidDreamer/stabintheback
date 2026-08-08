using UnityEngine;

/// <summary>
/// 타이틀 화면 배경에서 캠핑장을 뛰어다니는 캐릭터.
///
/// 게임용 PlayerAnimator는 "실제로 움직인 거리"를 재서 애니메이션을 굴리지만,
/// 여기서는 이 스크립트가 직접 목적지를 정하고 달리므로 파라미터도 직접 넣는다.
/// (전용 컨트롤러 Assets/Player/Title/*.controller 의 IsMoving / Speed)
///
/// 모닥불 자리(roamCenter)는 안쪽 반지름으로 비워 두어 불 위를 밟고 지나가지 않는다.
/// </summary>
public class TitleActor : MonoBehaviour
{
    [Tooltip("돌아다닐 중심(보통 모닥불). 월드 좌표")]
    [SerializeField] private Vector3 roamCenter = Vector3.zero;

    [Tooltip("중심에서 이만큼 안쪽으로는 들어가지 않는다")]
    [SerializeField] private float innerRadius = 2.8f;

    [Tooltip("중심에서 이만큼 바깥으로는 나가지 않는다")]
    [SerializeField] private float outerRadius = 7.5f;

    [SerializeField] private float runSpeed = 3.2f;

    [Tooltip("초당 회전 각도")]
    [SerializeField] private float turnSpeed = 360f;

    [Tooltip("목적지에 도착한 것으로 볼 거리")]
    [SerializeField] private float arriveDistance = 0.7f;

    [Tooltip("목적지에 닿았을 때 잠깐 쉴 확률")]
    [SerializeField, Range(0f, 1f)] private float restChance = 0.35f;

    [Tooltip("쉬는 시간 범위(초)")]
    [SerializeField] private Vector2 restSeconds = new Vector2(0.6f, 2.2f);

    [Tooltip("모델이 +Z를 보고 있지 않다면 여기서 돌린다(도)")]
    [SerializeField] private float facingOffset = 0f;

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Animator animator;
    private Vector3 heading = Vector3.forward;
    private Vector3 target;
    private float restLeft;

    /// <summary>빌더가 배치하면서 활동 범위를 잡아 준다.</summary>
    public void Configure(Vector3 center, float inner, float outer, float speed)
    {
        roamCenter = center;
        innerRadius = inner;
        outerRadius = outer;
        runSpeed = speed;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        heading = transform.forward;
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.001f)
            heading = Vector3.forward;
        heading.Normalize();

        PickTarget();
        // 넷이 동시에 같은 동작을 하지 않도록 시작을 흩뜨린다.
        restLeft = Random.Range(0f, 1.2f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        if (restLeft > 0f)
        {
            restLeft -= dt;
            Drive(0f);
            if (restLeft <= 0f)
                PickTarget();
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;

        if (to.magnitude <= arriveDistance)
        {
            if (Random.value < restChance)
                restLeft = Random.Range(restSeconds.x, restSeconds.y);
            else
                PickTarget();
            return;
        }

        Vector3 want = to.normalized;

        // 불 쪽으로 너무 파고들면 바깥으로 밀어낸다.
        Vector3 fromCenter = transform.position - roamCenter;
        fromCenter.y = 0f;
        float distance = fromCenter.magnitude;
        if (distance > 0.01f && distance < innerRadius)
        {
            float push = Mathf.InverseLerp(innerRadius, innerRadius * 0.4f, distance);
            want = Vector3.Slerp(want, fromCenter / distance, push).normalized;
        }

        heading = Vector3.RotateTowards(heading, want, turnSpeed * Mathf.Deg2Rad * dt, 0f).normalized;
        transform.rotation = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, facingOffset, 0f);

        // 방향이 크게 어긋난 동안은 속도를 줄여, 옆으로 미끄러지듯 도는 걸 막는다.
        float align = Mathf.Clamp01(Vector3.Dot(heading, want) * 1.5f);
        float speed = runSpeed * Mathf.Max(0.25f, align);

        transform.position += heading * (speed * dt);
        Drive(speed);
    }

    private void PickTarget()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float radius = Random.Range(innerRadius + 0.5f, outerRadius);
        target = roamCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        target.y = transform.position.y;
    }

    private void Drive(float speed)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(IsMovingHash, speed > 0.15f);
    }
}
