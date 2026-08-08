using UnityEngine;

/// <summary>
/// 모닥불 주변을 떠다니는 반딧불. 저폴리 배경에 공기감을 얹어 주는 장식이다.
///
/// 씬 파일이 불필요하게 커지지 않도록 오브젝트는 실행 시점에 만든다
/// (에디터에서 씬을 열어 보면 보이지 않고, 플레이하면 나타난다).
/// </summary>
public class TitleFireflies : MonoBehaviour
{
    [SerializeField] private int count = 26;
    [SerializeField] private float radius = 8f;
    [SerializeField] private float minHeight = 0.4f;
    [SerializeField] private float maxHeight = 3.2f;
    [SerializeField] private float size = 0.07f;
    [SerializeField] private float speed = 0.35f;

    [Tooltip("반딧불에 쓸 머티리얼(발광). 빌더가 넣어 준다")]
    [SerializeField] private Material glow;

    private Transform[] flies;
    private Vector3[] origins;
    private Vector3[] seeds;

    private void Start()
    {
        flies = new Transform[count];
        origins = new Vector3[count];
        seeds = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Random.value) * radius; // 균일하게 흩뿌리려면 제곱근
            origins[i] = new Vector3(
                Mathf.Cos(angle) * r,
                Random.Range(minHeight, maxHeight),
                Mathf.Sin(angle) * r);
            seeds[i] = new Vector3(Random.value * 60f, Random.value * 60f, Random.value * 60f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Firefly";
            Destroy(go.GetComponent<Collider>());
            if (glow != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = glow;

            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * size * Random.Range(0.7f, 1.3f);
            go.transform.localPosition = origins[i];
            flies[i] = go.transform;
        }
    }

    private void Update()
    {
        if (flies == null)
            return;

        float t = Time.time * speed;
        for (int i = 0; i < flies.Length; i++)
        {
            Transform fly = flies[i];
            if (fly == null)
                continue;

            Vector3 s = seeds[i];
            fly.localPosition = origins[i] + new Vector3(
                (Mathf.PerlinNoise(t + s.x, 0f) - 0.5f) * 2.4f,
                (Mathf.PerlinNoise(t + s.y, 7f) - 0.5f) * 1.2f,
                (Mathf.PerlinNoise(t + s.z, 13f) - 0.5f) * 2.4f);
        }
    }
}
