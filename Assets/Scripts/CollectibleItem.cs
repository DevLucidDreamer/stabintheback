using UnityEngine;

/// <summary>
/// 챙길 수 있는 캠핑 준비물. 클릭하면 체크리스트에 반영되고 씬에서 사라진다.
/// 멀티플레이에서는 각 아이템에 부여된 고유 ID로 CampChecklistManager가 서버 권한 처리한다.
/// (아이템 자체에는 NetworkIdentity를 붙이지 않는다 — 서랍/냉장고 자식이라 중첩 불가)
/// </summary>
public class CollectibleItem : Interactable
{
    [Tooltip("멀티플레이 동기화용 고유 ID. Phase 3 셋업 메뉴가 자동 부여한다")]
    [SerializeField] private int itemId = -1;

    public int ItemId => itemId;

    /// <summary>에디터 셋업에서 ID를 부여할 때 사용.</summary>
    public void SetItemId(int id) => itemId = id;

    public override string GetPrompt() => DisplayName + " 챙기기";

    public override void Interact(PlayerInteraction player)
    {
        var manager = CampChecklistManager.Instance;
        if (manager != null)
        {
            // 멀티플레이: 서버에 획득 요청(숨김/체크는 서버 브로드캐스트가 처리). 로컬 피드백만 즉시.
            player.GetComponent<ItemChecklist>()?.Peek();
            manager.RequestCollect(itemId);
            return;
        }

        // 싱글플레이: 로컬 체크리스트에 반영하고 비활성화.
        var checklist = player.GetComponent<ItemChecklist>();
        if (checklist == null)
            checklist = Object.FindFirstObjectByType<ItemChecklist>();
        if (checklist != null)
            checklist.Collect(DisplayName);

        gameObject.SetActive(false);
    }
}
