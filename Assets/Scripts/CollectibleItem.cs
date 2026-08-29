using UnityEngine;

/// <summary>
/// 캠핑장에 흩어져 있는 재료. 조준하고 좌클릭하면 팀 공용 재고에 들어가고 씬에서 사라진다.
///
/// 재료는 개인이 들고 다니지 않는다. 아무나 주우면 모두의 재고가 되고,
/// 화로와 그릴은 그 공용 재고에서 꺼내 쓴다 — 흩어져서 뒤지는 편이 항상 이득이 되게 하려는 것.
///
/// 멀티플레이 동기화는 고유 ID로 CampGameManager가 서버 권한 처리한다.
/// (아이템 자체에는 NetworkIdentity를 붙이지 않는다 — 쿨러/텐트 자식이라 중첩 불가)
/// </summary>
public class CollectibleItem : Interactable
{
    [Tooltip("이 재료가 무엇인지. 장작은 화로 연료, 고기·야채는 그릴에 굽는다")]
    [SerializeField] private Ingredient kind = Ingredient.Firewood;

    [Tooltip("멀티플레이 동기화용 고유 ID. 캠핑장 빌더가 자동 부여한다")]
    [SerializeField] private int itemId = -1;

    public Ingredient Kind => kind;
    public int ItemId => itemId;

    /// <summary>에디터 셋업에서 값을 넣을 때 사용.</summary>
    public void SetItemId(int id) => itemId = id;

    public void SetKind(Ingredient value) => kind = value;

    public override string GetPrompt() => DisplayName + " 챙기기";

    public override void Interact(PlayerInteraction player)
    {
        CampGameManager game = CampGameManager.Instance;
        if (game != null)
        {
            if (game.IsGathering)
                game.RequestCollect(itemId);
            return;
        }

        CampChecklistManager checklist = CampChecklistManager.Instance;
        if (checklist != null)
        {
            checklist.RequestCollect(itemId);
            return;
        }

        // 목표 매니저가 없는 대기실/실험 씬의 오프라인 폴백.
        gameObject.SetActive(false);
    }
}
