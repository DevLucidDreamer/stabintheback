/// <summary>
/// 타이틀 화면 → 대기실로 넘길 실행 의도(호스트/참가)를 담는 정적 값.
/// 씬 로드 후 NetworkAutoLaunch가 읽어서 Host/Client를 시작하고,
/// LobbyManager가 Code를 대기실 화면에 표시한다.
/// </summary>
public static class GameLaunch
{
    public enum LaunchMode { None, Host, Client }

    public static LaunchMode Mode = LaunchMode.None;
    public static string Address = "127.0.0.1";

    /// <summary>대기실에서 보여줄 방 코드(호스트는 생성, 참가자는 입력한 값).</summary>
    public static string Code = string.Empty;
}
