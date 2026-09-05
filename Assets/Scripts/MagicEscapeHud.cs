using System.Text;
using UnityEngine;

[RequireComponent(typeof(MagicEscapeGameManager))]
public sealed class MagicEscapeHud : MonoBehaviour
{
    private MagicEscapeGameManager game;
    private readonly StringBuilder text = new StringBuilder(180);
    private void Awake() => game = GetComponent<MagicEscapeGameManager>();
    private void OnEnable() => MagicEscapeGameManager.OnChanged += Refresh;
    private void OnDisable()
    {
        MagicEscapeGameManager.OnChanged -= Refresh;
        if (GameHud.Current != null)
        {
            GameHud.Current.SetGoal(string.Empty);
            GameHud.Current.SetTopLeft(string.Empty);
        }
    }
    private void Start() => Refresh();
    private void Update()
    {
        if (game != null && (game.Phase == MagicEscapePhase.Counterweights ||
                             game.Phase == MagicEscapePhase.TwinLevers ||
                             game.Phase == MagicEscapePhase.RallyEscape))
            Refresh();
    }

    private void Refresh()
    {
        if (game == null) return;
        GameHud hud = GameHud.Ensure();
        hud.SetGoal(game.GoalLine());
        text.Clear().Append("<b>< 지하 격리 연구동 B-13 ></b>\n\n");
        switch (game.Phase)
        {
            case MagicEscapePhase.HiddenSwitches:
                text.Append("전력 단자  ").Append(BitCount(game.SwitchMask)).Append(" / ").Append(game.HiddenSwitchCount); break;
            case MagicEscapePhase.SplitCipher:
                text.Append("접근 코드  ").Append(game.RuneProgress).Append(" / ").Append(game.RuneLength); break;
            case MagicEscapePhase.Counterweights:
                text.Append("중량판  ").Append(BitCount(game.PressureMask | game.PressureLatchedMask)).Append(" / 2");
                if (game.PressureProgress01 > 0f) text.Append("\n고정 중  ").Append(Mathf.RoundToInt(game.PressureProgress01 * 100f)).Append('%');
                break;
            case MagicEscapePhase.TwinLevers:
                text.Append("활성 차단기  ").Append(BitCount(game.LeverMask)).Append(" / 2");
                if (game.LeverSecondsLeft > 0f) text.Append("\n남은 시간  ").Append(game.LeverSecondsLeft.ToString("0.0")).Append("초");
                break;
            case MagicEscapePhase.SealBreaking:
                text.Append("파괴한 격리핵  ").Append(game.BrokenSealCount).Append(" / ").Append(game.SealCount); break;
            case MagicEscapePhase.RallyEscape:
                text.Append("승강기 탑승  ").Append(game.RallyCount).Append(" / ").Append(game.ConnectedPlayers); break;
            default: text.Append("지상 이송 중"); break;
        }
        hud.SetTopLeft(text.ToString());
    }

    private static int BitCount(int value)
    {
        int count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }
}
