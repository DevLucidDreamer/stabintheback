using System.Text;
using UnityEngine;

public class FortressHud : MonoBehaviour
{
    private FortressGameManager game;
    private readonly StringBuilder text = new StringBuilder(180);

    private void Awake() => game = GetComponent<FortressGameManager>();
    private void OnEnable() => FortressGameManager.OnChanged += Refresh;
    private void OnDisable()
    {
        FortressGameManager.OnChanged -= Refresh;
        GameHud hud = GameHud.Current;
        if (hud != null) { hud.SetGoal(string.Empty); hud.SetTopLeft(string.Empty); }
    }
    private void Start() => Refresh();
    private void Update()
    {
        if (game != null && (game.Phase == FortressPhase.Counterweights ||
                             game.Phase == FortressPhase.TwinLevers || game.Phase == FortressPhase.RallyEscape))
            Refresh();
    }

    private void Refresh()
    {
        if (game == null) return;
        GameHud hud = GameHud.Ensure();
        hud.SetGoal(game.GoalLine());
        text.Clear().AppendLine("<b>< 저주받은 성채 ></b>").AppendLine();
        switch (game.Phase)
        {
            case FortressPhase.Counterweights:
                text.Append("압력판  ").Append(BitCount(game.PressureMask | game.PressureLatchedMask)).Append(" / 2");
                if (game.PressureProgress01 > 0f) text.Append("\n고정 중  ").Append(Mathf.RoundToInt(game.PressureProgress01 * 100f)).Append('%');
                break;
            case FortressPhase.RuneCipher:
                text.Append("맞춘 룬  ").Append(game.RuneProgress).Append(" / ").Append(game.RuneLength)
                    .Append("\n\n서쪽과 동쪽 벽화의\n반쪽 단서를 합쳐라");
                break;
            case FortressPhase.TwinLevers:
                text.Append("활성 레버  ").Append(BitCount(game.LeverMask)).Append(" / 2");
                if (game.LeverMask != 0) text.Append("\n남은 시간  ").Append(game.LeverSecondsLeft.ToString("0.0")).Append("초");
                break;
            case FortressPhase.SealBreaking:
                text.Append("파괴한 봉인핵  ").Append(game.BrokenSealCount).Append(" / ").Append(game.SealCount)
                    .Append("\n\n무기를 들고 좌클릭으로\n보랏빛 핵을 타격");
                break;
            case FortressPhase.RallyEscape:
                text.Append("탈출진 집결  ").Append(game.RallyCount).Append(" / ").Append(Mathf.Max(1, game.ConnectedPlayers));
                if (game.RallyProgress01 > 0f) text.Append("\n동기화  ").Append(Mathf.RoundToInt(game.RallyProgress01 * 100f)).Append('%');
                break;
            default:
                text.Append("모두 생환했다");
                break;
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
