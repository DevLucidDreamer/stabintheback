/// <summary>
/// 타이틀 화면 → 게임 씬으로 넘길 실행 의도(호스트/참가)를 담는 정적 값.
/// 씬 로드 후 NetworkAutoLaunch가 읽어서 Host/Client를 시작한다.
/// </summary>
public static class GameLaunch
{
    public enum LaunchMode { None, Host, Client }

    public static LaunchMode Mode = LaunchMode.None;
    public static string Address = "127.0.0.1";
}
