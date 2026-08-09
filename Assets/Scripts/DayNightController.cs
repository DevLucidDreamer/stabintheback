using UnityEngine;

/// <summary>
/// 낮 → 저녁노을 전환 연출.
///
/// CampGameManager가 알려 주는 진행도(<see cref="CampGameManager.DuskProgress01"/>)만 보고
/// 해의 각도·색·세기, 환경광, 안개, 하늘을 통째로 보간한다.
/// 시간은 서버의 NetworkTime을 따라가므로 모든 플레이어가 같은 하늘을 본다.
///
/// 되돌아가지 않는다 — 재료를 다 모으면 해가 넘어가고, 그대로 노을에 머문다.
/// "밤이 되기 전에 모아야 한다"가 아니라 "다 모으면 저녁이 온다"가 이 게임의 규칙이다.
///
/// 씬에 하나만 두면 되고, 해(Directional Light)는 자동으로 찾는다.
///
/// 플레이 중에만 동작한다. 에디터에서도 돌게 하면 RenderSettings와 조명 세기가
/// 씬 파일에 눌러앉아, 다음에 씬을 열었을 때 이미 해가 진 상태로 시작한다.
/// 씬을 열었을 때의 '낮' 모습은 맵 빌더가 저장해 둔 값이 담당한다.
/// </summary>
public class DayNightController : MonoBehaviour
{
    [Header("해")]
    [Tooltip("비워 두면 씬의 Directional Light를 자동으로 찾는다")]
    [SerializeField] private Light sun;

    [SerializeField] private Vector3 daySunEuler = new Vector3(48f, -35f, 0f);
    [SerializeField] private Vector3 duskSunEuler = new Vector3(3f, -88f, 0f);

    [SerializeField] private Color daySunColor = new Color(1f, 0.95f, 0.84f);
    [SerializeField] private Color duskSunColor = new Color(1f, 0.46f, 0.19f);

    [SerializeField] private float daySunIntensity = 1.35f;
    [SerializeField] private float duskSunIntensity = 0.7f;

    [Header("환경광")]
    [SerializeField] private Color daySky = new Color(0.55f, 0.66f, 0.82f);
    [SerializeField] private Color dayEquator = new Color(0.44f, 0.47f, 0.44f);
    [SerializeField] private Color dayGround = new Color(0.24f, 0.24f, 0.20f);

    [SerializeField] private Color duskSky = new Color(0.34f, 0.24f, 0.36f);
    [SerializeField] private Color duskEquator = new Color(0.46f, 0.26f, 0.20f);
    [SerializeField] private Color duskGround = new Color(0.12f, 0.09f, 0.11f);

    [Header("안개")]
    [SerializeField] private Color dayFog = new Color(0.68f, 0.75f, 0.82f);
    [SerializeField] private Color duskFog = new Color(0.55f, 0.31f, 0.24f);
    [SerializeField] private float dayFogStart = 22f;
    [SerializeField] private float dayFogEnd = 85f;
    [SerializeField] private float duskFogStart = 10f;
    [SerializeField] private float duskFogEnd = 58f;

    [Header("하늘")]
    [Tooltip("Unity 기본 프로시저럴 하늘의 색을 함께 물들인다")]
    [SerializeField] private bool tintSkybox = true;
    [SerializeField] private Color daySkyTint = new Color(0.5f, 0.6f, 0.72f);
    [SerializeField] private Color duskSkyTint = new Color(0.78f, 0.36f, 0.22f);
    [SerializeField] private Color dayGroundTint = new Color(0.37f, 0.35f, 0.31f);
    [SerializeField] private Color duskGroundTint = new Color(0.15f, 0.11f, 0.12f);
    [SerializeField] private float dayExposure = 1.15f;
    [SerializeField] private float duskExposure = 0.75f;
    [SerializeField] private float dayThickness = 0.75f;
    [SerializeField] private float duskThickness = 2.2f;

    [Header("모닥불")]
    [Tooltip("어두워질수록 불빛이 세지도록 씬의 점광원을 함께 조절한다")]
    [SerializeField] private bool boostFireLights = true;
    [SerializeField] private float fireBoost = 1.9f;

    /// <summary>씬 애셋을 건드리지 않으려고 런타임에 복제해 쓰는 하늘 머티리얼.</summary>
    private Material skyboxInstance;

    private Light[] fireLights;
    private float[] fireBaseIntensity;
    private float applied = -1f;

    private void OnEnable()
    {
        EnsureSun();
        CacheFireLights();
        applied = -1f; // 처음 한 번은 무조건 반영
        Apply(CurrentProgress());
    }

    private void OnDisable()
    {
        // 에디터에서 놀다가 껐을 때 복제해 둔 하늘을 정리한다.
        if (skyboxInstance != null)
        {
            if (Application.isPlaying)
                Destroy(skyboxInstance);
            else
                DestroyImmediate(skyboxInstance);
            skyboxInstance = null;
        }
    }

    private void LateUpdate() => Apply(CurrentProgress());

    private float CurrentProgress()
    {
        CampGameManager game = CampGameManager.Instance;
        return game != null ? game.DuskProgress01 : 0f;
    }

    /// <summary>진행도 t(0 = 낮, 1 = 저녁노을)에 맞춰 씬 전체의 빛을 맞춘다.</summary>
    public void Apply(float t)
    {
        t = Mathf.Clamp01(t);

        // 눈에 보이는 변화가 없으면 건너뛴다. 매 프레임 RenderSettings를 쓰면 낭비다.
        if (Mathf.Abs(t - applied) < 0.0005f)
            return;
        applied = t;

        // 해는 후반부에 급히 떨어지는 편이 노을답다.
        float sunT = Mathf.SmoothStep(0f, 1f, t);

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(Vector3.Lerp(daySunEuler, duskSunEuler, sunT));
            sun.color = Color.Lerp(daySunColor, duskSunColor, sunT);
            sun.intensity = Mathf.Lerp(daySunIntensity, duskSunIntensity, sunT);
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(daySky, duskSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, duskEquator, t);
        RenderSettings.ambientGroundColor = Color.Lerp(dayGround, duskGround, t);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(dayFog, duskFog, t);
        RenderSettings.fogStartDistance = Mathf.Lerp(dayFogStart, duskFogStart, t);
        RenderSettings.fogEndDistance = Mathf.Lerp(dayFogEnd, duskFogEnd, t);

        if (tintSkybox)
            ApplySkybox(t);

        if (boostFireLights)
            ApplyFireLights(t);
    }

    /// <summary>
    /// 프로시저럴 하늘을 물들인다.
    ///
    /// RenderSettings.skybox는 프로젝트 애셋이라 그대로 만지면 에디터에서 값이 눌러앉는다.
    /// 복제본을 만들어 그것만 건드린다.
    /// </summary>
    private void ApplySkybox(float t)
    {
        Material sky = RenderSettings.skybox;
        if (sky == null)
            return;

        if (skyboxInstance == null || RenderSettings.skybox != skyboxInstance)
        {
            if (!sky.HasProperty("_SkyTint"))
                return; // 프로시저럴 하늘이 아니면 손대지 않는다

            skyboxInstance = new Material(sky) { name = sky.name + " (Runtime)" };
            RenderSettings.skybox = skyboxInstance;
        }

        skyboxInstance.SetColor("_SkyTint", Color.Lerp(daySkyTint, duskSkyTint, t));
        if (skyboxInstance.HasProperty("_GroundColor"))
            skyboxInstance.SetColor("_GroundColor", Color.Lerp(dayGroundTint, duskGroundTint, t));
        if (skyboxInstance.HasProperty("_Exposure"))
            skyboxInstance.SetFloat("_Exposure", Mathf.Lerp(dayExposure, duskExposure, t));
        if (skyboxInstance.HasProperty("_AtmosphereThickness"))
            skyboxInstance.SetFloat("_AtmosphereThickness", Mathf.Lerp(dayThickness, duskThickness, t));
    }

    private void ApplyFireLights(float t)
    {
        if (fireLights == null)
            return;

        float scale = Mathf.Lerp(1f, fireBoost, t);
        for (int i = 0; i < fireLights.Length; i++)
        {
            if (fireLights[i] != null)
                fireLights[i].intensity = fireBaseIntensity[i] * scale;
        }
    }

    private void EnsureSun()
    {
        if (sun != null)
            return;

        if (RenderSettings.sun != null)
        {
            sun = RenderSettings.sun;
            return;
        }

        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                sun = light;
                return;
            }
        }
    }

    /// <summary>
    /// 모닥불·랜턴 같은 점광원을 모아 둔다. 어두워질수록 같이 세진다.
    ///
    /// 꺼져 있는 광원은 건드리지 않는다 — 화로의 불빛은 <see cref="Firepit"/>이
    /// 점화 시점에 켜고 매 프레임 세기를 흔들기 때문에, 여기서도 만지면 서로 덮어쓴다.
    /// </summary>
    private void CacheFireLights()
    {
        var found = new System.Collections.Generic.List<Light>();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Point && light.enabled)
                found.Add(light);
        }

        fireLights = found.ToArray();
        fireBaseIntensity = new float[fireLights.Length];
        for (int i = 0; i < fireLights.Length; i++)
            fireBaseIntensity[i] = fireLights[i].intensity;
    }

    /// <summary>불이 새로 생겼을 때(화로 점화 등) 다시 훑는다.</summary>
    public void RefreshFireLights()
    {
        CacheFireLights();
        applied = -1f;
    }
}
