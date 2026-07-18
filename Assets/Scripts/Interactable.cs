using UnityEngine;

/// <summary>
/// 플레이어가 마우스로 상호작용할 수 있는 오브젝트의 공통 베이스.
/// 자식 콜라이더 어디에 레이캐스트가 맞아도 GetComponentInParent로 찾아진다.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("화면 안내 문구에 표시될 이름 (예: 서랍, 냉장고, 국자)")]
    [SerializeField] private string displayName = "오브젝트";

    public string DisplayName => displayName;

    public void SetDisplayName(string name) => displayName = name;

    /// <summary>조준했을 때 표시할 안내 문구.</summary>
    public abstract string GetPrompt();

    /// <summary>플레이어가 상호작용했을 때 실행.</summary>
    public abstract void Interact(PlayerInteraction player);
}
