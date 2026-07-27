using System.Net;
using System.Net.Sockets;

/// <summary>
/// 방 코드 ↔ 접속 주소(IPv4) 변환.
///
/// 코드는 A~Z 7글자다. 별도의 매치메이킹 서버 없이 IPv4 주소 32비트를 그대로 26진수로
/// 담기 때문에(26^7 = 80억 > 2^32), 호스트가 부른 코드를 그대로 받아 적으면 접속된다.
/// 나중에 Steam 로비로 바꿀 땐 이 클래스만 로비 ID 기반으로 교체하면 된다.
/// </summary>
public static class RoomCode
{
    /// <summary>코드 길이. IPv4 32비트를 담으려면 26진수 7자리가 필요하다.</summary>
    public const int Length = 7;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>IPv4 주소를 방 코드로 바꾼다. 실패하면 빈 문자열.</summary>
    public static string FromAddress(string address)
    {
        if (!IPAddress.TryParse(address, out IPAddress ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return string.Empty;

        byte[] bytes = ip.GetAddressBytes();
        uint value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

        var chars = new char[Length];
        for (int i = Length - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value % 26)];
            value /= 26;
        }
        return new string(chars);
    }

    /// <summary>방 코드를 IPv4 주소로 되돌린다.</summary>
    public static bool TryToAddress(string code, out string address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        string clean = code.Trim().ToUpperInvariant();
        if (clean.Length != Length)
            return false;

        ulong value = 0;
        foreach (char c in clean)
        {
            int digit = Alphabet.IndexOf(c);
            if (digit < 0)
                return false;
            value = value * 26 + (ulong)digit;
        }

        if (value > uint.MaxValue)
            return false;

        address = $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";
        return true;
    }

    /// <summary>입력이 코드가 아니라 IP 주소로 보이는지(직접 주소 입력도 허용하기 위함).</summary>
    public static bool LooksLikeAddress(string text)
        => !string.IsNullOrWhiteSpace(text) && text.Contains(".");

    /// <summary>
    /// 이 PC의 IPv4 주소. 실제로 패킷을 보내지 않고 나가는 인터페이스만 골라 확인한다.
    /// 공유기 안의 사설 주소이므로 같은 네트워크(LAN)에서 바로 접속 가능하고,
    /// 인터넷 너머로 초대하려면 포트포워딩이 필요하다.
    /// </summary>
    public static string LocalAddress()
    {
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                    return endPoint.Address.ToString();
            }
        }
        catch
        {
            // 네트워크가 없으면 아래 방법으로 넘어간다.
        }

        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
        }
        catch
        {
            // 마지막 폴백으로 넘어간다.
        }

        return "127.0.0.1";
    }
}
