using UnityEngine;

/// <summary>
/// 대기실 허수아비. 무기 스윙에 맞으면 맞은 방향으로 넘어졌다가 잠시 뒤 일어난다.
/// 타격감을 미리 체험시키는 용도라 각 클라이언트에서 로컬로만 연출한다
/// (스윙 자체는 모든 클라이언트에 동기화되므로 대체로 같은 타이밍에 넘어진다).
/// </summary>
public class PracticeDummy : MonoBehaviour
{
    [Tooltip("넘어져 있는 시간(초)")]
    [SerializeField] private float downSeconds = 1.1f;

    [Tooltip("넘어질 때 기울어지는 각도")]
    [SerializeField] private float fallAngle = 78f;

    [Tooltip("넘어지고 일어나는 속도(도/초)")]
    [SerializeField] private float turnSpeed = 480f;

    private Quaternion uprightRot;
    private Quaternion fallenRot;
    private float getUpAt;

    private void Awake()
    {
        uprightRot = transform.rotation;
        fallenRot = uprightRot;
    }

    private void OnEnable() => PlayerInteraction.OnSwingHit += HandleSwingHit;

    private void OnDisable() => PlayerInteraction.OnSwingHit -= HandleSwingHit;

    private void HandleSwingHit(PlayerInteraction attacker, Collider hit)
    {
        if (attacker == null || hit == null)
            return;
        if (hit.GetComponentInParent<PracticeDummy>() != this)
            return;
        if (Time.time < getUpAt)
            return; // 이미 넘어져 있음

        // 때린 사람 반대 방향으로 넘어진다.
        Vector3 away = transform.position - attacker.transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
            away = attacker.transform.forward;

        Vector3 axis = Vector3.Cross(Vector3.up, away.normalized);
        fallenRot = Quaternion.AngleAxis(fallAngle, axis) * uprightRot;
        getUpAt = Time.time + downSeconds;
    }

    private void Update()
    {
        Quaternion target = Time.time < getUpAt ? fallenRot : uprightRot;
        if (transform.rotation != target)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
    }
}
