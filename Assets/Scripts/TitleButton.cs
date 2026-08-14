using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 버튼의 손맛. Button 기본 색 전환(transition) 대신 이걸 쓴다.
///
/// 마우스를 올리면 판이 오른쪽으로 살짝 밀려 나오고, 왼쪽 색 띠가 굵어지고,
/// 바탕과 글자 색이 밝아진다. 누르면 그림자 쪽으로 눌러 앉는다.
/// 저폴리 그래픽에 맞춰 곡선 없이 '판때기가 움직이는' 느낌으로 통일했다.
///
/// 실제로 눌리는 판정 영역(이 오브젝트)은 가만히 있고 자식 Body만 움직이므로,
/// 가장자리에서 커서가 들락날락하며 떠는 일이 없다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TitleButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("실제로 움직일 판. 비워 두면 이 오브젝트가 움직인다")]
    public RectTransform body;

    [Tooltip("바탕 판")]
    public Image fill;

    [Tooltip("왼쪽 색 띠. 마우스를 올리면 굵어진다")]
    public RectTransform accent;

    public TextMeshProUGUI label;

    [Header("색")]
    public Color idleFill = new Color(0.11f, 0.12f, 0.15f, 0.94f);
    public Color hoverFill = new Color(0.18f, 0.19f, 0.23f, 0.98f);
    public Color idleLabel = new Color(0.88f, 0.89f, 0.92f);
    public Color hoverLabel = Color.white;

    [Header("움직임")]
    public float hoverShift = 16f;
    public float pressShift = 5f;
    public float accentIdleWidth = 8f;
    public float accentHoverWidth = 22f;

    [Tooltip("클수록 빠르게 붙는다")]
    public float sharpness = 14f;

    private Button owner;
    private Vector2 basePosition;
    private bool hovered;
    private bool pressed;

    private void Awake()
    {
        if (body == null)
            body = (RectTransform)transform;

        owner = GetComponent<Button>();
        basePosition = body.anchoredPosition;
        Apply(1f);
    }

    private void OnDisable()
    {
        // 패널을 껐다 켜면 마우스를 올린 상태가 남아 있을 수 있다.
        hovered = false;
        pressed = false;
        if (body != null)
            Apply(1f);
    }

    private void Update()
    {
        // Button.transition을 None으로 쓰므로 '누를 수 없음' 표시도 여기서 직접 한다.
        if (owner != null && !owner.interactable)
        {
            hovered = false;
            pressed = false;
        }

        Apply(1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
    }

    private void Apply(float t)
    {
        bool usable = owner == null || owner.interactable;

        float targetShift = hovered ? hoverShift : 0f;
        if (pressed)
            targetShift -= pressShift;

        body.anchoredPosition = Vector2.Lerp(body.anchoredPosition, basePosition + new Vector2(targetShift, 0f), t);

        if (fill != null)
            fill.color = Color.Lerp(fill.color, Dim(hovered ? hoverFill : idleFill, usable), t);

        if (label != null)
            label.color = Color.Lerp(label.color, Dim(hovered ? hoverLabel : idleLabel, usable), t);

        if (accent != null)
        {
            float width = Mathf.Lerp(accent.sizeDelta.x, hovered ? accentHoverWidth : accentIdleWidth, t);
            accent.sizeDelta = new Vector2(width, accent.sizeDelta.y);
        }
    }

    /// <summary>못 누르는 동안은 색을 죽여 상태가 보이게 한다.</summary>
    private static Color Dim(Color color, bool usable)
        => usable ? color : new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, color.a);

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => pressed = true;

    public void OnPointerUp(PointerEventData eventData) => pressed = false;
}
