using System.Text;
using UnityEngine;

/// <summary>
/// 캠핑장 진행 상황을 HUD에 그려 주는 표시 전용 컴포넌트.
/// <see cref="CampGameManager"/>와 같은 오브젝트에 붙는다.
///
/// 판정은 전부 매니저가 하고, 여기서는 그 상태를 읽어 글자로 옮기기만 한다.
/// 페이즈가 바뀌는 순간에는 화면 가운데에 큰 알림을 한 번 띄운다.
/// </summary>
public class CampHud : MonoBehaviour
{
    private static readonly Color Sunset = new Color(1f, 0.66f, 0.3f);
    private static readonly Color Success = new Color(0.62f, 1f, 0.58f);

    private CampGameManager game;
    private readonly StringBuilder sb = new StringBuilder(160);

    private void Awake() => game = GetComponent<CampGameManager>();

    private void OnEnable()
    {
        CampGameManager.OnChanged += Refresh;
        CampGameManager.OnPhaseEntered += Announce;
    }

    private void OnDisable()
    {
        CampGameManager.OnChanged -= Refresh;
        CampGameManager.OnPhaseEntered -= Announce;

        // 씬을 떠날 때 캠핑장 문구가 대기실에 남지 않게 지운다.
        GameHud hud = GameHud.Current;
        if (hud != null)
        {
            hud.SetGoal(string.Empty);
            hud.SetTopLeft(string.Empty);
        }
    }

    private void Start() => Refresh();

    /// <summary>노을이 지는 동안에는 남은 시간이 매 프레임 바뀌므로 계속 갱신한다.</summary>
    private void Update()
    {
        if (game != null && game.Phase == CampPhase.Dusk)
            Refresh();
    }

    private void Refresh()
    {
        if (game == null)
            return;

        GameHud hud = GameHud.Ensure();
        hud.SetGoal(game.GoalLine());
        hud.SetTopLeft(BuildPanel());
    }

    private void Announce(CampPhase phase)
    {
        GameHud hud = GameHud.Ensure();

        switch (phase)
        {
            case CampPhase.Dusk:
                hud.ShowBanner("재료를 다 모았다!\n해가 넘어간다", 5f, Sunset);
                break;

            case CampPhase.Cooking:
                hud.ShowBanner("화로에 장작을 넣어 불을 피워라", 4.5f, Sunset);
                break;

            case CampPhase.Feast:
                hud.ShowBanner("바베큐 완성!\n잘 먹겠습니다", 7f, Success);
                break;
        }
    }

    /// <summary>왼쪽 위 진행 패널. 페이즈에 따라 보여 줄 것이 달라진다.</summary>
    private string BuildPanel()
    {
        sb.Clear();

        if (game.Phase == CampPhase.Gathering || game.Phase == CampPhase.Dusk)
        {
            sb.AppendLine("<b>< 캠핑 재료 ></b>").AppendLine();
            Line("장작", game.FirewoodHave, game.FirewoodNeeded);
            Line("고기", game.MeatHave, game.MeatNeeded);
            Line("야채", game.VegetableHave, game.VegetableNeeded);

            if (game.Phase == CampPhase.Dusk)
            {
                sb.AppendLine();
                sb.Append("해가 넘어가는 중...");
            }

            return sb.ToString();
        }

        sb.AppendLine("<b>< 바베큐 ></b>").AppendLine();

        if (!game.FireLit)
        {
            Line("장작 투입", game.FirewoodLoaded, game.FirewoodNeeded);
            sb.AppendLine();
            sb.Append("화로에 장작을 전부 넣어야\n불이 붙는다");
            return sb.ToString();
        }

        Line("구운 것", game.CookedCount, game.CookTarget);
        sb.AppendLine();
        sb.Append($"남은 재료   고기 {game.MeatAvailable} · 야채 {game.VegetableAvailable}");

        if (game.BurntCount > 0)
            sb.Append($"\n<color=#FF6B5C>태운 것 {game.BurntCount}</color>");

        return sb.ToString();
    }

    private void Line(string label, int have, int need)
    {
        bool done = have >= need;
        sb.Append(done ? "■ " : "□ ")
          .Append(label)
          .Append("   ")
          .Append(have).Append(" / ").Append(need)
          .Append('\n');
    }
}
