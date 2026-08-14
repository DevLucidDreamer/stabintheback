using Mirror;
using UnityEngine;

/// <summary>
/// 대기실 식탁 위 냉동참치를 <b>내가</b> 집었을 때 나에게만 짧게 알려 준다.
///
/// 이 게임은 서로 속이는 배신 구도라, 누가 무기를 가져갔는지가 남에게 새면 안 된다.
/// 그래서 제단 조명을 끄거나 팻말 문구를 바꾸는 식의 "모두가 보는 신호"는 두지 않는다
/// (Weapon.OnHeldChanged는 모든 클라이언트에서 울리므로, 그대로 반응하면 전부에게 알려진다).
/// 집은 사람 본인에게만 보이도록 로컬 플레이어인지 확인한 뒤에만 문구를 띄운다.
///
/// 무기는 하나뿐인 물건이 아니다. 여기 놓인 참치는 대기실에 마련된 것 중 하나일 뿐이다.
/// </summary>
public class LobbyAltar : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Weapon sword;       // 식탁 위 냉동참치

    [Header("문구")]
    [SerializeField] private string pickedUpText = "냉동참치를 손에 넣었다!";
    [SerializeField] private float announceSeconds = 2.4f;

    private float announceReadyAt; // 접속 직후 상태 동기화로 뜨는 오인 문구 방지

    /// <summary>빌더가 참조를 연결한다.</summary>
    public void Configure(Weapon swordWeapon) => sword = swordWeapon;

    private void OnEnable() => Weapon.OnHeldChanged += HandleHeldChanged;

    private void OnDisable() => Weapon.OnHeldChanged -= HandleHeldChanged;

    private void Start() => announceReadyAt = Time.time + 2f;

    private void HandleHeldChanged(Weapon weapon, bool isHeld)
    {
        if (!isHeld || weapon == null || weapon != sword)
            return;

        if (Time.time < announceReadyAt || !PickedUpByMe(weapon))
            return;

        GameHud.Ensure().ShowToast(pickedUpText, announceSeconds, new Color(1f, 0.82f, 0.25f));
    }

    /// <summary>
    /// 집은 사람이 나인지. 무기는 집은 사람의 손 소켓 밑으로 들어가므로,
    /// 로컬 플레이어 계층 안에 있으면 내가 집은 것이다.
    /// </summary>
    private static bool PickedUpByMe(Weapon weapon)
    {
        NetworkIdentity me = NetworkClient.localPlayer;
        if (me == null)
            return false;

        return weapon.transform.IsChildOf(me.transform);
    }
}
