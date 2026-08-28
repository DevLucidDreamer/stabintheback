using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// 같은 공유기(LAN) 안에서 열려 있는 방을 서로 찾게 해 주는 UDP 신호등.
/// '빠른 참가'는 이걸로 방을 찾아 코드 입력 없이 바로 들어간다.
///
/// 매치메이킹 서버가 없으므로 방식은 단순하다.
///   호스트 : 47776 포트에 앉아 "SITB?1" 이 오면 방 정보를 그대로 돌려준다.
///   참가자 : 브로드캐스트로 "SITB?1" 을 뿌리고 몇 초간 답장을 모은다.
/// 답장에는 Unity Relay 참가 코드를 싣는다. 발견은 LAN에서 하지만 게임 트래픽은
/// Relay를 통해 흐르므로 게임용 포트 포워딩은 필요 없다.
///
/// 소켓을 백그라운드 스레드로 돌리지 않고 Pump/코루틴에서 폴링만 한다.
/// 프레임당 한 번이면 충분히 빠르고, Unity API를 다른 스레드에서 만질 일이 없어진다.
///
/// 한계: 브로드캐스트는 공유기를 넘지 못한다. 인터넷 너머 친구는 Relay 방 코드를 입력해야 한다.
/// 방화벽이 UDP 47776을 막고 있으면 서로 못 찾는다.
/// </summary>
public static class LanRoomBeacon
{
    /// <summary>LAN 방 검색 전용 포트. Relay 게임 트래픽과는 별개다.</summary>
    public const int Port = 47776;

    private const string Request = "SITB?1";
    private const string ResponseTag = "SITB!1";

    /// <summary>발견한 방 하나.</summary>
    public struct Room
    {
        public string Address;
        public string Code;
        public int Players;
        public int Capacity;

        public bool IsFull => Capacity > 0 && Players >= Capacity;
    }

    // ---------------------------------------------------------------- 호스트 (방 알리기)

    private static UdpClient host;
    private static string roomCode = string.Empty;
    private static int roomPlayers;
    private static int roomCapacity;

    public static bool IsAdvertising => host != null;

    /// <summary>답장에 실어 보낼 방 정보. 매 프레임 갱신해도 부담 없다.</summary>
    public static void SetRoomInfo(string code, int players, int capacity)
    {
        roomCode = string.IsNullOrEmpty(code) ? string.Empty : code;
        roomPlayers = Mathf.Max(0, players);
        roomCapacity = Mathf.Max(0, capacity);
    }

    public static void StartAdvertising()
    {
        if (host != null)
            return;

        try
        {
            var socket = new UdpClient();
            // 한 PC에서 두 개를 띄워 테스트할 때 두 번째가 포트를 못 잡는 일을 막는다.
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
            socket.EnableBroadcast = true;
            host = socket;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanRoomBeacon] 방 알림을 켜지 못했습니다(UDP {Port}). " +
                             $"이 방은 '빠른 참가'로 찾을 수 없고, 방 코드로만 들어올 수 있습니다.\n{e.Message}");
            host = null;
        }
    }

    public static void StopAdvertising()
    {
        if (host == null)
            return;

        try { host.Close(); }
        catch (Exception) { /* 닫는 중 오류는 무시 */ }
        host = null;
    }

    /// <summary>들어온 문의에 답장한다. 호스트 쪽에서 매 프레임 부른다.</summary>
    public static void PumpAdvertising()
    {
        if (host == null)
            return;

        try
        {
            while (host.Available > 0)
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = host.Receive(ref from);
                if (data == null || data.Length == 0)
                    continue;

                if (Encoding.UTF8.GetString(data) != Request)
                    continue; // 다른 프로그램의 브로드캐스트

                byte[] reply = Encoding.UTF8.GetBytes(
                    $"{ResponseTag}|{roomCode}|{roomPlayers}|{roomCapacity}");
                host.Send(reply, reply.Length, from);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanRoomBeacon] 방 알림이 끊겼습니다.\n{e.Message}");
            StopAdvertising();
        }
    }

    // ---------------------------------------------------------------- 참가자 (방 찾기)

    /// <summary>
    /// seconds 동안 브로드캐스트를 뿌리며 답장을 모은다. 코루틴으로 돌린다.
    /// 끝나면 results 에 발견한 방이 들어 있다(중복 주소는 하나로 합친다).
    /// </summary>
    public static IEnumerator Discover(float seconds, List<Room> results)
    {
        if (results == null)
            yield break;

        results.Clear();

        UdpClient client = OpenClient();
        if (client == null)
            yield break;

        byte[] request = Encoding.UTF8.GetBytes(Request);
        var broadcast = new IPEndPoint(IPAddress.Broadcast, Port);
        var seen = new HashSet<string>();

        try
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(0.5f, seconds);
            float nextPing = 0f;

            while (Time.realtimeSinceStartup < deadline)
            {
                // 패킷이 유실될 수 있으니 몇 번 더 물어본다.
                if (Time.realtimeSinceStartup >= nextPing)
                {
                    nextPing = Time.realtimeSinceStartup + 0.6f;
                    TrySend(client, request, broadcast);
                }

                Collect(client, results, seen);
                yield return null;
            }

            // 마지막 답장까지 훑고 끝낸다.
            Collect(client, results, seen);
        }
        finally
        {
            try { client.Close(); }
            catch (Exception) { /* 닫는 중 오류는 무시 */ }
        }
    }

    private static UdpClient OpenClient()
    {
        try
        {
            return new UdpClient(0) { EnableBroadcast = true };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LanRoomBeacon] 방을 찾을 소켓을 열지 못했습니다.\n{e.Message}");
            return null;
        }
    }

    private static void TrySend(UdpClient client, byte[] data, IPEndPoint to)
    {
        try
        {
            client.Send(data, data.Length, to);
        }
        catch (Exception)
        {
            // 브로드캐스트를 막는 어댑터가 섞여 있을 수 있다. 남은 시간 동안 계속 시도한다.
        }
    }

    private static void Collect(UdpClient client, List<Room> results, HashSet<string> seen)
    {
        try
        {
            while (client.Available > 0)
            {
                var from = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref from);
                if (data == null || data.Length == 0)
                    continue;

                if (!TryParse(Encoding.UTF8.GetString(data), out Room room))
                    continue;

                room.Address = from.Address.ToString();
                if (seen.Add(room.Address))
                    results.Add(room);
            }
        }
        catch (Exception)
        {
            // 읽다가 실패해도 남은 시간 동안 계속 모은다.
        }
    }

    private static bool TryParse(string text, out Room room)
    {
        room = default;

        string[] parts = text.Split('|');
        if (parts.Length < 4 || parts[0] != ResponseTag)
            return false;

        room.Code = parts[1];
        int.TryParse(parts[2], out room.Players);
        int.TryParse(parts[3], out room.Capacity);
        return true;
    }
}
