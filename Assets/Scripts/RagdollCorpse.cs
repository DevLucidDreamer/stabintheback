using UnityEngine;

/// <summary>
/// 죽은 플레이어 자리에 잠깐 남는 시체. 순수 연출이라 각 클라이언트가 로컬로 만들고 지운다
/// (네트워크로 물리를 동기화하지 않으므로 사람마다 튀는 모양이 조금씩 다르다).
///
/// 뼈대에 붙은 Rigidbody들이 물리로 흐물흐물 무너지고, 곧바로 쪼그라들며 사라진다.
/// 죽자마자 리스폰하는 게임이라 시체가 오래 남으면 시야만 가리므로 총 1초 안에 정리한다.
/// </summary>
public class RagdollCorpse : MonoBehaviour
{
    [Tooltip("시체가 물리로 굴러다니는 시간(초)")]
    [SerializeField] private float ragdollTime = 0.75f;

    [Tooltip("쪼그라들며 사라지는 시간(초). 위 시간 + 이 시간 뒤에 완전히 없어진다")]
    [SerializeField] private float vanishDuration = 0.25f;

    private Rigidbody[] bodies;
    private Collider[] colliders;
    private Vector3 startScale;
    private float vanishStartTime;
    private bool frozen;

    private void Awake()
    {
        bodies = GetComponentsInChildren<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        startScale = transform.localScale;
        vanishStartTime = Time.time + ragdollTime;
        Destroy(gameObject, ragdollTime + vanishDuration);

        IgnorePlayers();
    }

    /// <summary>
    /// 시체는 순수 연출이라 플레이어를 밀거나 막으면 안 된다.
    /// 레이어로 거르면 시체가 바닥까지 통과해 버리므로, 플레이어 캡슐하고만 충돌을 끈다.
    /// (시체는 1초만 살다 사라지니 그 사이에 들어오는 플레이어는 신경 쓰지 않아도 된다)
    /// </summary>
    private void IgnorePlayers()
    {
        var players = FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        foreach (CharacterController player in players)
        {
            foreach (Collider col in colliders)
            {
                if (col != null && player != null)
                    Physics.IgnoreCollision(col, player);
            }
        }
    }

    /// <summary>죽은 순간의 기세를 물려받아 시체를 날린다.</summary>
    public void Launch(Vector3 direction, float force, float spin)
    {
        if (bodies == null || bodies.Length == 0)
            bodies = GetComponentsInChildren<Rigidbody>();

        Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        // 맞은 방향으로 살짝 띄우며 민다.
        Vector3 velocity = (dir + Vector3.up * 0.3f) * force;
        // 맞은 방향과 직각인 축으로 굴린다 → 앞으로 고꾸라져 구르는 모양.
        Vector3 tumble = Vector3.Cross(Vector3.up, dir).normalized * spin;

        foreach (Rigidbody rb in bodies)
        {
            if (rb == null || rb.isKinematic)
                continue;

            // 뼈마다 다른 힘을 주면 그 차이만큼 조인트가 벌어져 몸이 늘어나 보인다.
            // 그래서 몸 전체에 같은 속도를 주고, 흐느적거림은 중력과 조인트에 맡긴다.
            rb.linearVelocity = velocity;
            rb.angularVelocity = tumble + Random.insideUnitSphere * (spin * 0.15f);
        }
    }

    private void Update()
    {
        float elapsed = Time.time - vanishStartTime;
        if (elapsed < 0f)
            return;

        // 물리를 멈춘 뒤 쪼그라뜨린다. 공중에 떠 있어도 어색하지 않게 사라진다
        // (바닥으로 가라앉히는 방식은 1초 안에 끝내기엔 시체가 아직 날아가는 중이라 이상했다).
        if (!frozen)
        {
            frozen = true;
            foreach (Rigidbody rb in bodies)
            {
                if (rb != null && !rb.isKinematic)
                    rb.isKinematic = true;
            }
            foreach (Collider col in colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
        }

        float t = vanishDuration > 0f ? Mathf.Clamp01(elapsed / vanishDuration) : 1f;
        transform.localScale = startScale * (1f - t);
    }
}
