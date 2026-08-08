using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 죽는 연출 담당. 죽은 자리에 goshi 모델로 만든 래그돌 시체를 떨어뜨린다.
///
/// 플레이어 본체는 (기존 설계대로) 즉시 리스폰하므로, 시체는 본체와 완전히 분리된
/// 연출용 오브젝트다. 물리는 동기화하지 않고 각 클라이언트가 로컬로 굴린다 —
/// 죽는 위치/방향만 서버가 알려주면 충분하고, 트래픽도 들지 않는다.
///
/// 래그돌 프리팹은 Tools > Player > Build Ragdoll Prefab 로 만든다.
/// </summary>
public class PlayerRagdoll : MonoBehaviour
{
    [Tooltip("Tools > Player > Build Ragdoll Prefab 이 만든 시체 프리팹")]
    [SerializeField] private GameObject ragdollPrefab;

    [Tooltip("죽을 때 시체가 날아가는 세기(m/s)")]
    [SerializeField] private float launchForce = 3.5f;

    [Tooltip("죽을 때 시체가 굴러가는 세기(rad/s). 크게 주면 사지가 뽑힌 것처럼 보인다")]
    [SerializeField] private float launchSpin = 3f;

    [Tooltip("포즈를 베껴올 원본 아바타. 비우면 RemoteAvatar를 자동으로 찾는다")]
    [SerializeField] private Transform poseSource;

    public GameObject RagdollPrefab { get => ragdollPrefab; set => ragdollPrefab = value; }

    private void Awake()
    {
        if (poseSource == null)
        {
            Transform avatar = transform.Find("RemoteAvatar");
            if (avatar != null)
                poseSource = avatar;
        }
    }

    /// <summary>죽은 자리에 시체를 만든다. 모든 클라이언트가 각자 호출한다.</summary>
    public void SpawnCorpse(Vector3 position, Quaternion rotation, Vector3 blowDirection)
    {
        if (ragdollPrefab == null)
            return;

        GameObject corpse = Instantiate(ragdollPrefab, position, rotation);
        CopyPose(corpse.transform);

        // 시체도 본인 색으로. 시체 프리팹은 같은 goshi 모델이라 렌더러 구성이 일치한다.
        var color = GetComponent<PlayerColor>();
        if (color != null)
            color.ApplyTo(corpse.transform);

        var script = corpse.GetComponent<RagdollCorpse>();
        if (script == null)
            script = corpse.AddComponent<RagdollCorpse>();
        script.Launch(blowDirection, launchForce, launchSpin);
    }

    /// <summary>
    /// 죽는 순간의 애니메이션 포즈를 시체에 그대로 옮긴다.
    /// 같은 goshi 모델에서 만든 프리팹이라 뼈 이름이 일치한다.
    /// (1인칭 본인은 아바타가 꺼져 있어 포즈를 못 가져오므로, 그때는 기본 자세로 쓰러진다)
    /// </summary>
    private void CopyPose(Transform corpseRoot)
    {
        if (poseSource == null || !poseSource.gameObject.activeInHierarchy)
            return;

        var source = new Dictionary<string, Transform>();
        foreach (Transform t in poseSource.GetComponentsInChildren<Transform>(true))
            source[t.name] = t;

        foreach (Transform t in corpseRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == corpseRoot)
                continue;
            if (source.TryGetValue(t.name, out Transform src))
                t.localRotation = src.localRotation;
        }
    }
}
