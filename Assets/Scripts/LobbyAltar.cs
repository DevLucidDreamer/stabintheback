using UnityEngine;

/// <summary>
/// 대기실 중앙의 참치 제단 연출.
/// 식탁 위 냉동참치를 누군가 집으면 "마검을 뽑았다!" 문구를 띄우고 제단 조명을 끈다.
/// 다시 내려놓으면 원래대로 돌아온다.
///
/// 소지 상태는 WeaponNetworkManager가 이미 모든 클라이언트에 동기화해 주므로,
/// 여기서는 그 결과(Weapon.OnHeldChanged)에만 반응하는 로컬 연출이다.
/// </summary>
public class LobbyAltar : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Weapon sword;       // 제단 위 냉동참치
    [SerializeField] private Light altarLight;   // 제단 스포트라이트
    [SerializeField] private TextMesh sign;      // 제단 팻말

    [Header("문구")]
    [SerializeField] private string idleSign = "이 냉동참치가 마검이다";
    [SerializeField] private string takenSign = "마검을 누가 가져갔다";
    [SerializeField] private float announceSeconds = 2.4f;

    private float announceUntil;
    private float announceReadyAt; // 접속 직후 상태 동기화로 뜨는 오인 문구 방지
    private bool held;

    /// <summary>빌더가 참조를 연결한다.</summary>
    public void Configure(Weapon swordWeapon, Light light, TextMesh signText)
    {
        sword = swordWeapon;
        altarLight = light;
        sign = signText;
    }

    private void OnEnable() => Weapon.OnHeldChanged += HandleHeldChanged;

    private void OnDisable() => Weapon.OnHeldChanged -= HandleHeldChanged;

    private void Start()
    {
        announceReadyAt = Time.time + 2f;
        Apply(false);
    }

    private void HandleHeldChanged(Weapon weapon, bool isHeld)
    {
        if (weapon == null || weapon != sword || isHeld == held)
            return;

        Apply(isHeld);

        if (isHeld && Time.time >= announceReadyAt)
            announceUntil = Time.time + announceSeconds;
    }

    private void Apply(bool isHeld)
    {
        held = isHeld;

        if (altarLight != null)
            altarLight.enabled = !isHeld;

        if (sign != null)
            sign.text = isHeld ? takenSign : idleSign;
    }

    private void OnGUI()
    {
        if (Time.time >= announceUntil)
            return;

        var big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 44,
            fontStyle = FontStyle.Bold,
        };
        big.normal.textColor = new Color(1f, 0.82f, 0.25f);

        var small = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
        };
        small.normal.textColor = new Color(0.95f, 0.95f, 0.95f);

        GUI.Label(new Rect(0f, Screen.height * 0.26f, Screen.width, 60f), "마검을 뽑았다!", big);
        GUI.Label(new Rect(0f, Screen.height * 0.26f + 62f, Screen.width, 30f), "등 뒤를 조심하세요.", small);
    }
}
