using System;
using System.Collections.Generic;
using Mirror;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UtpConnection = Unity.Networking.Transport.NetworkConnection;

/// <summary>
/// Mirror transport backed by Unity Transport and a pre-created Unity Relay allocation.
/// NetworkAutoLaunch creates or joins the allocation before starting Mirror.
/// </summary>
[DisallowMultipleComponent]
public sealed class RelayMirrorTransport : Transport
{
    [Header("Packet sizes")]
    [SerializeField, Min(1024)] private int reliableMaxPacketSize = 16 * 1024;
    [SerializeField, Min(256)] private int unreliableMaxPacketSize = 1200;

    [Header("Timeouts (milliseconds)")]
    [SerializeField, Min(100)] private int connectTimeoutMs = 1000;
    [SerializeField, Min(1)] private int maxConnectAttempts = 15;
    [SerializeField, Min(1000)] private int disconnectTimeoutMs = 30000;
    [SerializeField, Min(100)] private int heartbeatTimeoutMs = 500;

    private NetworkDriver driver;
    private NetworkPipeline reliablePipeline;
    private NetworkPipeline unreliablePipeline;
    private UtpConnection clientConnection;
    private readonly Dictionary<int, UtpConnection> serverConnections = new Dictionary<int, UtpConnection>();
    private readonly List<int> connectionIds = new List<int>();

    private RelayServerData relayServerData;
    private bool relayConfigured;
    private bool clientMode;
    private bool clientConnected;
    private bool serverActive;
    private int nextConnectionId = 1;

    public override bool IsEncrypted => true;
    public override string EncryptionCipher => "DTLS";

    public void ConfigureHost(RelayServerData data)
    {
        Shutdown();
        relayServerData = data;
        relayConfigured = true;
        clientMode = false;
    }

    public void ConfigureClient(RelayServerData data)
    {
        Shutdown();
        relayServerData = data;
        relayConfigured = true;
        clientMode = true;
    }

    public override bool Available() => Application.platform != RuntimePlatform.WebGLPlayer;

    public override bool ClientConnected() => clientMode && clientConnected;

    public override void ClientConnect(string address)
    {
        if (!relayConfigured || !clientMode)
        {
            FailClient("Relay 참가 정보가 준비되지 않았습니다.");
            return;
        }

        try
        {
            CreateDriver();
            clientConnection = driver.Connect();
            if (!clientConnection.IsCreated)
                FailClient("Unity Transport가 Relay 연결을 만들지 못했습니다.");
        }
        catch (Exception ex)
        {
            OnClientTransportException?.Invoke(ex);
            FailClient(ex.Message);
        }
    }

    public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        if (!clientConnected || !clientConnection.IsCreated)
            return;

        if (Send(clientConnection, segment, channelId, out string error))
            OnClientDataSent?.Invoke(segment, channelId);
        else
            OnClientError?.Invoke(TransportError.InvalidSend, error);
    }

    public override void ClientDisconnect()
    {
        if (driver.IsCreated && clientConnection.IsCreated)
            driver.Disconnect(clientConnection);
    }

    public override Uri ServerUri() => new Uri("relay://unity");

    public override bool ServerActive() => serverActive;

    public override void ServerStart()
    {
        if (!relayConfigured || clientMode)
        {
            Debug.LogError("[Relay] Relay 호스트 할당이 준비되지 않아 서버를 시작할 수 없습니다.");
            return;
        }

        try
        {
            CreateDriver();
            if (driver.Bind(NetworkEndpoint.AnyIpv4) < 0)
                throw new InvalidOperationException("Unity Transport Relay bind 실패");
            if (driver.Listen() < 0)
                throw new InvalidOperationException("Unity Transport Relay listen 실패");

            nextConnectionId = 1;
            serverActive = true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            ShutdownDriver();
            throw;
        }
    }

    public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        if (!serverConnections.TryGetValue(connectionId, out UtpConnection connection))
            return;

        if (Send(connection, segment, channelId, out string error))
            OnServerDataSent?.Invoke(connectionId, segment, channelId);
        else
            OnServerError?.Invoke(connectionId, TransportError.InvalidSend, error);
    }

    public override void ServerDisconnect(int connectionId)
    {
        if (driver.IsCreated && serverConnections.TryGetValue(connectionId, out UtpConnection connection))
            driver.Disconnect(connection);
    }

    public override string ServerGetClientAddress(int connectionId) => "Unity Relay";

    public override void ServerStop()
    {
        if (!serverActive)
            return;

        connectionIds.Clear();
        connectionIds.AddRange(serverConnections.Keys);
        foreach (int connectionId in connectionIds)
        {
            if (serverConnections.TryGetValue(connectionId, out UtpConnection connection) && connection.IsCreated)
                driver.Disconnect(connection);
            OnServerDisconnected?.Invoke(connectionId);
        }

        serverConnections.Clear();
        serverActive = false;
        ShutdownDriver();
    }

    public override int GetMaxPacketSize(int channelId = Channels.Reliable)
        => channelId == Channels.Unreliable ? unreliableMaxPacketSize : reliableMaxPacketSize;

    public override int GetBatchThreshold(int channelId = Channels.Reliable)
        => channelId == Channels.Unreliable ? unreliableMaxPacketSize : 1200;

    public override void ClientEarlyUpdate()
    {
        if (!clientMode || !driver.IsCreated || !clientConnection.IsCreated)
            return;

        driver.ScheduleUpdate().Complete();

        NetworkEvent.Type eventType;
        while ((eventType = driver.PopEventForConnection(clientConnection, out DataStreamReader reader, out NetworkPipeline pipeline))
               != NetworkEvent.Type.Empty)
        {
            switch (eventType)
            {
                case NetworkEvent.Type.Connect:
                    clientConnected = true;
                    OnClientConnected?.Invoke();
                    break;
                case NetworkEvent.Type.Data:
                    OnClientDataReceived?.Invoke(Read(reader), ChannelFor(pipeline));
                    break;
                case NetworkEvent.Type.Disconnect:
                    bool wasActive = clientConnected || clientConnection.IsCreated;
                    clientConnected = false;
                    clientConnection = default;
                    if (wasActive)
                        OnClientDisconnected?.Invoke();
                    break;
            }
        }
    }

    public override void ServerEarlyUpdate()
    {
        if (!serverActive || !driver.IsCreated)
            return;

        driver.ScheduleUpdate().Complete();

        UtpConnection accepted;
        while ((accepted = driver.Accept()) != default)
        {
            int connectionId = nextConnectionId++;
            serverConnections.Add(connectionId, accepted);
            OnServerConnectedWithAddress?.Invoke(connectionId, "Unity Relay");
        }

        connectionIds.Clear();
        connectionIds.AddRange(serverConnections.Keys);
        foreach (int connectionId in connectionIds)
        {
            if (!serverConnections.TryGetValue(connectionId, out UtpConnection connection))
                continue;

            NetworkEvent.Type eventType;
            while ((eventType = driver.PopEventForConnection(connection, out DataStreamReader reader, out NetworkPipeline pipeline))
                   != NetworkEvent.Type.Empty)
            {
                if (eventType == NetworkEvent.Type.Data)
                {
                    OnServerDataReceived?.Invoke(connectionId, Read(reader), ChannelFor(pipeline));
                }
                else if (eventType == NetworkEvent.Type.Disconnect)
                {
                    serverConnections.Remove(connectionId);
                    OnServerDisconnected?.Invoke(connectionId);
                    break;
                }
            }
        }
    }

    public override void ClientLateUpdate()
    {
        if (clientMode && driver.IsCreated)
            driver.ScheduleFlushSend().Complete();
    }

    public override void ServerLateUpdate()
    {
        if (serverActive && driver.IsCreated)
            driver.ScheduleFlushSend().Complete();
    }

    public override void Shutdown()
    {
        clientConnected = false;
        clientConnection = default;
        serverActive = false;
        serverConnections.Clear();
        connectionIds.Clear();
        ShutdownDriver();
        relayConfigured = false;
    }

    private void CreateDriver()
    {
        ShutdownDriver();

        var settings = new NetworkSettings();
        settings.WithNetworkConfigParameters(
            connectTimeoutMS: connectTimeoutMs,
            maxConnectAttempts: maxConnectAttempts,
            disconnectTimeoutMS: disconnectTimeoutMs,
            heartbeatTimeoutMS: heartbeatTimeoutMs,
            reconnectionTimeoutMS: 2000,
            receiveQueueCapacity: 1024,
            sendQueueCapacity: 1024);
        settings.WithFragmentationStageParameters(payloadCapacity: reliableMaxPacketSize);
        settings.WithRelayParameters(serverData: ref relayServerData);

        driver = NetworkDriver.Create(settings);
        reliablePipeline = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        unreliablePipeline = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
    }

    private bool Send(UtpConnection connection, ArraySegment<byte> segment, int channelId, out string error)
    {
        NetworkPipeline pipeline = channelId == Channels.Unreliable ? unreliablePipeline : reliablePipeline;
        int maxSize = GetMaxPacketSize(channelId);
        if (segment.Array == null || segment.Count > maxSize)
        {
            error = $"Relay packet size {segment.Count} exceeds channel limit {maxSize}.";
            return false;
        }

        int result = driver.BeginSend(pipeline, connection, out DataStreamWriter writer, segment.Count);
        if (result != 0)
        {
            error = $"Unity Transport BeginSend failed ({result}).";
            return false;
        }

        writer.WriteBytes(new Span<byte>(segment.Array, segment.Offset, segment.Count));
        result = driver.EndSend(writer);
        if (result < 0)
        {
            error = $"Unity Transport EndSend failed ({result}).";
            return false;
        }

        error = null;
        return true;
    }

    private static ArraySegment<byte> Read(DataStreamReader reader)
    {
        byte[] bytes = new byte[reader.Length];
        reader.ReadBytes(new Span<byte>(bytes));
        return new ArraySegment<byte>(bytes);
    }

    private int ChannelFor(NetworkPipeline pipeline)
        => pipeline == unreliablePipeline ? Channels.Unreliable : Channels.Reliable;

    private void FailClient(string message)
    {
        Debug.LogError("[Relay] " + message);
        OnClientError?.Invoke(TransportError.Unexpected, message);
        OnClientDisconnected?.Invoke();
    }

    private void ShutdownDriver()
    {
        if (driver.IsCreated)
            driver.Dispose();
        driver = default;
    }
}
