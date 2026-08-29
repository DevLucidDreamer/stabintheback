using UnityEngine;

/// <summary>
/// 캠핑장 화로. 저녁이 되면 여기에 장작을 넣어 불을 피운다.
///
/// 상태는 전부 <see cref="CampGameManager"/>가 들고 있고, 이 스크립트는
/// 상호작용 창구와 눈에 보이는 연출만 맡는다 —
/// Interactable은 MonoBehaviour라 NetworkBehaviour를 겸할 수 없기 때문이다.
///
/// 장작을 넣을수록 쌓인 장작이 하나씩 드러나고, 다 채우면 불이 붙는다.
/// 불이 붙어야 옆의 바베큐 그릴을 쓸 수 있다.
/// </summary>
public class Firepit : Interactable
{
    [Header("연출 (빌더가 연결한다)")]
    [Tooltip("장작을 넣을 때마다 하나씩 켜지는 장작 모델들")]
    [SerializeField] private GameObject[] logs = new GameObject[0];

    [Tooltip("불이 붙으면 켜지는 불꽃 모델들")]
    [SerializeField] private GameObject[] flames = new GameObject[0];

    [Tooltip("불이 붙으면 켜지는 불빛")]
    [SerializeField] private Light fireLight;

    [Header("불꽃 흔들림")]
    [SerializeField] private float flickerSpeed = 7f;
    [SerializeField] private float flickerAmount = 0.22f;

    private float litLightIntensity = 3.4f;
    private bool lit;
    private int shownLogs = -1;
    private AudioSource fireAudio;

    /// <summary>빌더가 만든 부품을 연결한다.</summary>
    public void Configure(GameObject[] logParts, GameObject[] flameParts, Light light)
    {
        logs = logParts ?? new GameObject[0];
        flames = flameParts ?? new GameObject[0];
        fireLight = light;
    }

    public override string GetPrompt()
    {
        CampGameManager game = CampGameManager.Instance;
        return game != null ? game.FirepitPrompt() : "화로";
    }

    public override void Interact(PlayerInteraction player)
    {
        CampGameManager game = CampGameManager.Instance;
        if (game != null && game.FirepitUsable())
            game.RequestLoadFirewood();
    }

    private void Awake()
    {
        if (fireLight != null)
            litLightIntensity = fireLight.intensity;

        fireAudio = GameAudio.CreateLoop(transform, "fire_loop", 0.28f, 1.5f, 12f);

        SetLit(false);
        ShowLogs(0);
    }

    private void OnEnable() => CampGameManager.OnChanged += Refresh;

    private void OnDisable() => CampGameManager.OnChanged -= Refresh;

    private void Start() => Refresh();

    private void Refresh()
    {
        CampGameManager game = CampGameManager.Instance;
        if (game == null)
            return;

        ShowLogs(game.FirewoodLoaded);
        SetLit(game.FireLit);
    }

    private void Update()
    {
        if (!lit || fireLight == null)
            return;

        // 불빛이 살짝 일렁여야 불처럼 보인다. 값이 두 겹으로 흔들리게 섞는다.
        float n = Mathf.Sin(Time.time * flickerSpeed) * 0.6f
                  + Mathf.Sin(Time.time * flickerSpeed * 2.3f) * 0.4f;
        fireLight.intensity = litLightIntensity * (1f + n * flickerAmount);
    }

    /// <summary>넣은 장작 개수만큼만 장작 모델을 보여준다.</summary>
    private void ShowLogs(int count)
    {
        if (shownLogs == count)
            return;
        shownLogs = count;

        for (int i = 0; i < logs.Length; i++)
            if (logs[i] != null)
                logs[i].SetActive(i < count);
    }

    private void SetLit(bool value)
    {
        if (lit == value && shownLogs >= 0)
            return;
        lit = value;

        foreach (GameObject flame in flames)
            if (flame != null)
                flame.SetActive(value);

        if (fireLight != null)
        {
            fireLight.enabled = value;
            fireLight.intensity = litLightIntensity;
        }

        if (fireAudio != null)
        {
            if (value && !fireAudio.isPlaying)
                fireAudio.Play();
            else if (!value && fireAudio.isPlaying)
                fireAudio.Stop();
        }
    }
}
