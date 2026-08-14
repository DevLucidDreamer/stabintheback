using TMPro;
using UnityEngine;

/// <summary>
/// 바베큐 그릴. 화로에 불이 붙은 뒤부터 쓸 수 있다.
///
/// 좌클릭 한 번이 상황에 맞는 일 하나를 한다 —
/// 다 익은 게 있으면 꺼내고, 탄 게 있으면 치우고, 없으면 재료를 올린다.
/// 무엇을 하게 될지는 조준했을 때 뜨는 안내 문구가 미리 알려 준다.
///
/// 판정과 타이머는 전부 <see cref="CampGameManager"/>가 서버 권한으로 들고 있다.
/// 여기서는 칸 위에 올라간 고기·야채를 그려 주고, 익은 정도를 색과 글자로 보여 준다.
/// </summary>
public class BarbecueGrill : Interactable
{
    [Header("연결 (빌더가 채운다)")]
    [Tooltip("재료가 올라갈 자리. 개수가 곧 그릴 칸 수다")]
    [SerializeField] private Transform[] slotAnchors = new Transform[0];

    [Tooltip("칸 위 글자에 쓸 폰트. 비우면 TMP 기본 폰트를 쓴다")]
    [SerializeField] private TMP_FontAsset font;

    [Tooltip("불이 붙었을 때만 보이는 숯불")]
    [SerializeField] private GameObject[] coals = new GameObject[0];

    [Header("모양")]
    [SerializeField] private float itemScale = 0.16f;
    [SerializeField] private float labelHeight = 0.3f;

    private static readonly Color MeatRaw = new Color(0.86f, 0.36f, 0.38f);
    private static readonly Color MeatCooked = new Color(0.44f, 0.24f, 0.14f);
    private static readonly Color VegRaw = new Color(0.46f, 0.76f, 0.30f);
    private static readonly Color VegCooked = new Color(0.50f, 0.53f, 0.20f);
    private static readonly Color Burnt = new Color(0.10f, 0.09f, 0.09f);

    private static readonly Color LabelCooking = new Color(1f, 0.88f, 0.55f);
    private static readonly Color LabelDone = new Color(0.55f, 1f, 0.5f);
    private static readonly Color LabelBurnt = new Color(1f, 0.36f, 0.3f);

    private GameObject[] items;      // 칸마다 하나씩. 비었으면 꺼 둔다
    private Material[] itemMaterials;
    private TextMeshPro[] labels;
    private Camera viewCamera;
    private bool coalsOn;

    /// <summary>빌더가 만든 부품을 연결한다.</summary>
    public void Configure(Transform[] anchors, GameObject[] coalParts, TMP_FontAsset labelFont)
    {
        slotAnchors = anchors ?? new Transform[0];
        coals = coalParts ?? new GameObject[0];
        font = labelFont;
    }

    public override string GetPrompt()
    {
        CampGameManager game = CampGameManager.Instance;
        return game != null ? game.GrillPrompt() : "바베큐 그릴";
    }

    public override void Interact(PlayerInteraction player)
    {
        CampGameManager game = CampGameManager.Instance;
        if (game != null && game.GrillUsable())
            game.RequestGrillInteract();
    }

    private void Awake() => BuildSlotVisuals();

    private void Update()
    {
        CampGameManager game = CampGameManager.Instance;
        if (game == null || items == null)
            return;

        SetCoals(game.FireLit);

        int count = Mathf.Min(items.Length, game.SlotCount);
        for (int i = 0; i < count; i++)
            RefreshSlot(game, i);
    }

    // ---- 연출 -------------------------------------------------------------

    private void RefreshSlot(CampGameManager game, int index)
    {
        GrillSlotState state = game.SlotState(index);

        if (state == GrillSlotState.Empty || !game.TryGetSlot(index, out Ingredient kind))
        {
            if (items[index].activeSelf)
                items[index].SetActive(false);
            return;
        }

        if (!items[index].activeSelf)
            items[index].SetActive(true);

        // 색: 날것 → 알맞게 → 탄 것. 익는 동안은 두 색 사이를 서서히 건넌다.
        bool meat = kind == Ingredient.Meat;
        Color raw = meat ? MeatRaw : VegRaw;
        Color cooked = meat ? MeatCooked : VegCooked;

        Color color;
        if (state == GrillSlotState.Burnt)
        {
            color = Burnt;
        }
        else
        {
            color = Color.Lerp(raw, cooked, game.SlotProgress01(index));
            if (state == GrillSlotState.Cooked)
            {
                // 다 익으면 탈 때까지 남은 시간을 색으로 경고한다.
                color = Color.Lerp(Burnt, cooked, game.SlotBurnLeft01(index));
            }
        }

        PipelineShaders.SetBaseColor(itemMaterials[index], color);

        // 구워지는 동안 지글지글 떠는 느낌.
        if (state != GrillSlotState.Burnt)
        {
            float bob = Mathf.Sin(Time.time * 12f + index * 1.7f) * 0.006f;
            Vector3 p = items[index].transform.localPosition;
            items[index].transform.localPosition = new Vector3(p.x, bob, p.z);
        }

        UpdateLabel(game, index, state, kind);
    }

    private void UpdateLabel(CampGameManager game, int index, GrillSlotState state, Ingredient kind)
    {
        TextMeshPro label = labels[index];
        if (label == null)
            return;

        switch (state)
        {
            case GrillSlotState.Raw:
                label.text = $"{Mathf.RoundToInt(game.SlotProgress01(index) * 100f)}%";
                label.color = LabelCooking;
                break;

            case GrillSlotState.Cooked:
                label.text = "다 익음!";
                // 탈 때가 가까워지면 깜빡여서 재촉한다.
                float left = game.SlotBurnLeft01(index);
                bool blink = left < 0.35f && Mathf.Repeat(Time.time, 0.5f) < 0.25f;
                label.color = blink ? LabelBurnt : LabelDone;
                break;

            case GrillSlotState.Burnt:
                label.text = "탔다";
                label.color = LabelBurnt;
                break;

            default:
                label.text = string.Empty;
                break;
        }

        // 글자는 항상 보는 사람 쪽을 향한다. Camera.main은 매 프레임 부르기엔 무거워 캐시한다.
        if (viewCamera == null)
            viewCamera = Camera.main;
        if (viewCamera != null)
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - viewCamera.transform.position);
    }

    private void SetCoals(bool on)
    {
        if (coalsOn == on || coals == null)
            return;
        coalsOn = on;

        foreach (GameObject coal in coals)
            if (coal != null)
                coal.SetActive(on);
    }

    /// <summary>
    /// 칸마다 고기/야채로 쓸 덩어리와 글자를 하나씩 만들어 둔다.
    /// 올라간 재료에 따라 색만 바꾸므로, 매번 생성·파괴하지 않는다.
    /// </summary>
    private void BuildSlotVisuals()
    {
        items = new GameObject[slotAnchors.Length];
        itemMaterials = new Material[slotAnchors.Length];
        labels = new TextMeshPro[slotAnchors.Length];

        for (int i = 0; i < slotAnchors.Length; i++)
        {
            Transform anchor = slotAnchors[i];
            if (anchor == null)
                continue;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Food";
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(itemScale, itemScale * 0.45f, itemScale * 1.35f);

            // 레이캐스트는 그릴 본체가 받아야 안내 문구가 흔들리지 않는다.
            Destroy(go.GetComponent<Collider>());

            Material mat = PipelineShaders.CreateLit(MeatRaw);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            items[i] = go;
            itemMaterials[i] = mat;
            labels[i] = BuildLabel(anchor);

            go.SetActive(false);
        }
    }

    private TextMeshPro BuildLabel(Transform anchor)
    {
        var go = new GameObject("SlotLabel", typeof(RectTransform));
        go.transform.SetParent(anchor, false);
        go.transform.localPosition = new Vector3(0f, labelHeight, 0f);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0.6f, 0.2f);

        var label = go.AddComponent<TextMeshPro>();
        if (font != null)
            label.font = font;
        label.fontSize = 1.1f;
        label.alignment = TextAlignmentOptions.Center;
        label.text = string.Empty;
        return label;
    }
}
