using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 타이틀 카메라를 아주 천천히 흔들고, 마우스를 따라 조금 기울인다.
/// 배경이 정지 화면처럼 굳어 보이지 않게 하는 것이 목적이라 진폭은 일부러 작다.
///
/// 시작할 때의 위치/회전을 기준으로 삼으므로, 씬에서 카메라를 옮겨 두면
/// 그 자리를 중심으로 흔들린다.
/// </summary>
public class TitleCameraDrift : MonoBehaviour
{
    [Tooltip("저절로 떠도는 폭(m)")]
    [SerializeField] private float driftAmount = 0.35f;

    [Tooltip("떠도는 속도")]
    [SerializeField] private float driftSpeed = 0.12f;

    [Tooltip("마우스를 화면 끝까지 옮겼을 때 따라가는 폭(m)")]
    [SerializeField] private float mouseShift = 0.5f;

    [Tooltip("마우스를 화면 끝까지 옮겼을 때 기울어지는 각도")]
    [SerializeField] private float mouseTilt = 1.2f;

    [Tooltip("마우스 추적이 따라붙는 속도")]
    [SerializeField] private float followSharpness = 3f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector2 aim;

    private void Awake()
    {
        basePosition = transform.position;
        baseRotation = transform.rotation;
    }

    private void Update()
    {
        aim = Vector2.Lerp(aim, ReadMouse(), 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

        float t = Time.time * driftSpeed;
        Vector3 drift = new Vector3(
            (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(t, 5f) - 0.5f) * 2f * 0.4f,
            0f) * driftAmount;

        Vector3 offset = drift + new Vector3(aim.x * mouseShift, aim.y * mouseShift * 0.5f, 0f);

        transform.position = basePosition + baseRotation * offset;
        transform.rotation = baseRotation * Quaternion.Euler(-aim.y * mouseTilt, aim.x * mouseTilt, 0f);
    }

    /// <summary>화면 중앙을 0, 가장자리를 ±1로 본 마우스 위치.</summary>
    private static Vector2 ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Screen.width <= 0 || Screen.height <= 0)
            return Vector2.zero;

        Vector2 p = mouse.position.ReadValue();
        return new Vector2(
            Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f),
            Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
    }
}
