using UnityEngine;

/// <summary>
/// 모닥불 조명이 살아 있게 밝기와 위치를 미세하게 흔든다.
/// 불꽃 메시(있으면)도 같이 숨쉬듯 크기를 바꾼다.
/// 랜덤 대신 Perlin 노이즈를 써서 깜빡임이 튀지 않고 부드럽게 이어진다.
/// </summary>
[RequireComponent(typeof(Light))]
public class TitleFireFlicker : MonoBehaviour
{
    [SerializeField] private float minIntensity = 2.6f;
    [SerializeField] private float maxIntensity = 4.2f;

    [Tooltip("흔들리는 속도")]
    [SerializeField] private float speed = 2.2f;

    [Tooltip("좌우로 흔들리는 폭(m)")]
    [SerializeField] private float sway = 0.07f;

    [Tooltip("같이 흔들 불꽃 메시(선택)")]
    [SerializeField] private Transform flame;

    private Light fire;
    private Vector3 basePosition;
    private Vector3 flameBaseScale;
    private float seed;

    private void Awake()
    {
        fire = GetComponent<Light>();
        basePosition = transform.localPosition;
        if (flame != null)
            flameBaseScale = flame.localScale;
        seed = Random.value * 100f;
    }

    private void Update()
    {
        float t = Time.time * speed + seed;
        float noise = Mathf.PerlinNoise(t, 0f);

        fire.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        transform.localPosition = basePosition + new Vector3(
            (Mathf.PerlinNoise(t, 11f) - 0.5f) * sway,
            (Mathf.PerlinNoise(t, 23f) - 0.5f) * sway,
            (Mathf.PerlinNoise(t, 37f) - 0.5f) * sway);

        if (flame != null)
            flame.localScale = flameBaseScale * Mathf.Lerp(0.9f, 1.12f, noise);
    }
}
