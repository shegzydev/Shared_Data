#if CLIENT

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;
using System.Collections.Concurrent;
using System.Linq;
using SimpleJSON;

internal class Client : Agent
{
    enum Connection
    {
        TCP, WS
    }
    Connection connection;

    // UDP
    private UdpClient udp;
    private IPEndPoint serverEP;
    private Thread udpReceiveThread;

    //Audio
    private Thread audReceiveThread;
    private IPEndPoint audServerEP;
    private UdpClient aud;

    // TCP
    private TcpClient tcpClient;
    private NetworkStream tcpStream;
    private Thread tcpReceiveThread;

    //WS
    private WebSocket wsClient;

    static RPCRouter rpcRouter;
    string serverIP;
    int port;

    private volatile bool running = true;

    public static Action OnConnected;
    public static Action<long> OnReceivedID;
    public Action<int> OnDisconnected;

    ConcurrentQueue<Action> actionQueue = new();

    public override void Tick()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        wsClient?.DispatchMessageQueue();
#endif
        while (actionQueue.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    public override void FixedTick()
    {

    }

    public Client(RPCRouter rPCRouter, string _serverIP, int _port, bool enableAudio, long roomToConnect = -1)
    {
        session = -1;

        rpcRouter = rPCRouter;
        serverIP = _serverIP;
        port = _port;

        // StartClientUDPConnection();
#if UNITY_WEBGL
        StartClientWSConnection();
#else
        StartClientTCPConnection();
#endif
        // StartClientWSConnection();
        if (enableAudio) StartClientAudioConnection();
    }

    void StartClientUDPConnection()
    {
        try
        {
            udp = new UdpClient(0); // Local port for UDP
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            udpReceiveThread = new Thread(UDPReceiveLoop) { IsBackground = true };
            udpReceiveThread.Start();

            SendUDPMessage(BitConverter.GetBytes(-1));

            Debug.Log($"UDP Receiver started on {serverIP}:{port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"UDP connection failed: {ex.Message}");
        }
    }

    async void StartClientTCPConnection()
    {
        try
        {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;
            await tcpClient.ConnectAsync(serverIP, port); // Connect to TCP server
            tcpStream = tcpClient.GetStream();

            tcpReceiveThread = new Thread(TCPReceiveLoop);
            tcpReceiveThread.IsBackground = true;
            tcpReceiveThread.Start();

            OnConnected?.Invoke();

            connection = Connection.TCP;
            Debug.Log($"TCP Connected to {serverIP}:{port}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"TCP connection failed: {ex.Message}");
            //AttemptTCPReconnect();
        }
    }

    bool connecting;
    async void StartClientWSConnection()
    {
        try
        {
            if (connecting) return;
            connecting = true;

            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                Uri uri = new Uri(Application.absoluteURL);
                string host = uri.Host;

                Debug.Log("Host: " + host);

                if (string.IsNullOrEmpty(host))
                {
                    host = "127.0.0.1";
                }

                serverIP = host;
            }
            Debug.Log($"using host {serverIP}");

#if UNITY_EDITOR
            wsClient = new WebSocket($"ws://{serverIP}:{port + 6}");
#else
            wsClient = new WebSocket($"wss://{serverIP}/{NetworkManager.Instance.gameName}_server/");
#endif

            wsClient.OnOpen += () =>
            {
                OnConnected?.Invoke();
                connection = Connection.WS;
                Debug.Log("✅ Connected to server!");
                connecting = false;
            };

            wsClient.OnMessage += (buffer) =>
            {
                int payloadLength = BitConverter.ToInt32(buffer);
                if (payloadLength > 0)
                {
                    byte[] payload = new byte[payloadLength];
                    Buffer.BlockCopy(buffer, 4, payload, 0, payloadLength);
                    HandlePayload(payload);
                }
            };

            wsClient.OnError += (err) =>
            {
                connecting = false;
                Debug.LogError($"❌ Error: {err}");
                try
                {
                    wsClient?.Close();
                }
                catch { }
            };

            wsClient.OnClose += (e) =>
            {
                Debug.Log($"🔌 Disconnected.{e.ToString("g")}");
                wsClient = null;

                if (e != WebSocketCloseCode.Normal && Application.isPlaying)
                {
                    Debug.Log("Reconnecting...");
                    StartClientWSConnection();
                }
            };

            await wsClient.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WS connection failed: {ex.Message}");
        }
    }

    void StartClientAudioConnection()
    {
        aud = new UdpClient(0); // Local port for UDP
        audServerEP = new IPEndPoint(IPAddress.Parse(serverIP), port + 1);

        audReceiveThread = new Thread(AudioReceiveLoop) { IsBackground = true };
        audReceiveThread.Start();
#if !UNITY_WEBGL
        GigNetVoice.Instance.OnEncoded += SendAudio;
#endif
        Debug.Log($"UDP Receiver started on {serverIP}:{port + 1}");
    }

    //==================UDP======================//
    public override void SendUDPMessage(byte[] data)
    {
        OutGoingData += data.Length;
        udp.Send(data, data.Length, serverEP);
    }

    private void UDPReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref remoteEP);
                int length = BitConverter.ToInt32(data);

                IncomingData += data.Length;

                if (length > 0)
                {
                    int packID = BitConverter.ToInt32(data, 4);
                    switch ((PackType)packID)
                    {
                        case PackType.RPC:
                            {
                                byte[] payload = new byte[length];
                                Buffer.BlockCopy(data, 4, payload, 0, length);
                                NetworkManager.Instance.QueueEvent(ActionType.RPC, payload);
                                break;
                            }
                        case PackType.Heartbeat:
                            {
                                var sendTime = BitConverter.ToInt64(data, 8);
                                var ReceiveTime = DateTimeOffset.UtcNow.Ticks;
                                NetworkManager.ms = (int)((ReceiveTime - sendTime) / TimeSpan.TicksPerMillisecond);
                                receivedHeartbeat = true;
                                break;
                            }
                        case PackType.Audio:
                            {

                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"UDP receive error: {e.Message}");
                //break;
            }
        }
    }

    //==================AUD======================//
    private void SendAudio(byte[] data)
    {
        var payload = Util.MergeArrays(BitConverter.GetBytes(NetworkManager.Instance.ID), data);
        OutGoingData += payload.Length;
        aud.Send(payload, payload.Length, audServerEP);
    }

    private void AudioReceiveLoop()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = aud.Receive(ref remoteEP);

                IncomingData += data.Length;

                int sender = BitConverter.ToInt32(data, 0);
                var audiopacket = new byte[data.Length - 4];

                Buffer.BlockCopy(data, 4, audiopacket, 0, audiopacket.Length);
#if !UNITY_WEBGL
                if (sender > -1) GigNetVoice.Instance.OnReceiveEncoded(audiopacket, sender);
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"UDP receive error: {e.Message} : {e.InnerException}");
            }
        }
    }

    public override void SendTCPMessage(byte[] data)
    {
        if (connection == Connection.WS)
        {
            if (wsClient != null && wsClient.State == WebSocketState.Open)
            {
                try
                {
                    wsClient.Send(data);
                    OutGoingData += data.Length;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WS Send Error:{ex};{ex.Message}");
                    try
                    {
                        wsClient?.Close();
                        wsClient = null;
                    }
                    catch { }
                }
            }
        }
        else
        {
            if (tcpStream != null && tcpStream.CanWrite)
            {
                try
                {
                    tcpStream.Write(data);
                    OutGoingData += data.Length;
                }
                catch (IOException ex)
                {
                    Debug.LogError($"TCP send error:{ex};{ex.Message}");
                    AttemptTCPReconnect();
                }
            }
        }
    }

    //=================TCP======================//
    private void TCPReceiveLoop()
    {
        Debug.Log("Receiving TCP Data");

        byte[] buffer = new byte[4];
        while (running)
        {
            try
            {
                // int bytesRead = tcpStream.Read(buffer, 0, buffer.Length);
                int bytesRead = Readfully(tcpStream, buffer, 0, buffer.Length);
                if (bytesRead == 0) break; // Disconnected
                int payloadLength = BitConverter.ToInt32(buffer);

                if (payloadLength > 0)
                {
                    byte[] payload = new byte[payloadLength];
                    bytesRead = Readfully(tcpStream, payload, 0, payloadLength);
                    if (bytesRead == 0) break;
                    // tcpStream.Read(payload, 0, payloadLength);
                    HandlePayload(payload);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"TCP receive error: {e.Message}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"Inner: {e.InnerException.Message}");
                }
                break;
            }
        }
    }

    int Readfully(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        while (count > 0)
        {
            bytesRead = stream.Read(buffer, offset, count);
            if (bytesRead == 0)
                break;

            offset += bytesRead;
            count -= bytesRead;
        }
        return bytesRead;
    }

    private async void AttemptTCPReconnect()
    {
        try { tcpStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        try
        {
            Debug.Log("Attempting TCP reconnect...");
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(serverIP, port);
            tcpStream = tcpClient.GetStream();

            Debug.Log("TCP reconnected!");
            tcpReceiveThread = new Thread(TCPReceiveLoop) { IsBackground = true };
            tcpReceiveThread.Start();

            OnConnected?.Invoke();
        }
        catch
        {
            Debug.LogWarning("TCP reconnect failed. Retrying in 3 seconds...");
            await Task.Delay(3000);
            AttemptTCPReconnect();
        }
    }

    //=================Payload====================//
    public void HandlePayload(byte[] payload)
    {
        IncomingData += (payload.Length + 4);
        int packID = BitConverter.ToInt32(payload);
        switch ((PackType)packID)
        {
            case PackType.RPC:
                {
                    NetworkManager.Instance.QueueEvent(ActionType.RPC, payload);
                    break;
                }
            case PackType.NetEvent:
                {
                    byte[] eventArgs = new byte[payload.Length - 4];
                    Buffer.BlockCopy(payload, 4, eventArgs, 0, eventArgs.Length);
                    NetworkManager.Instance.QueueEvent(ActionType.NetEvent, eventArgs);
                    break;
                }
            case PackType.IDAssignment:
                {
                    long id = BitConverter.ToInt64(payload, 4);
                    session = BitConverter.ToInt64(payload, 12);
                    actionQueue.Enqueue(() =>
                    {
                        Debug.Log($"Received session {session} from server");
                        OnReceivedID?.Invoke(id);
                    });
                    break;
                }
            case PackType.Instantiation:
                {
                    NetworkManager.Instance.QueueEvent(ActionType.Spawn, payload);
                    break;
                }
            case PackType.Destroy:
                {
                    int ownerIdToDestroy = BitConverter.ToInt32(payload, 4);
                    NetworkManager.Instance.QueueEvent(ActionType.Despawn, ownerIdToDestroy);
                    break;
                }
            case PackType.RoomAssign:
                {
                    var data = new byte[payload.Length - 4];
                    Buffer.BlockCopy(payload, 4, data, 0, data.Length);
                    NetworkManager.Instance.QueueEvent(ActionType.JoinedRoom, data);
                    break;
                }
            case PackType.RoomFilled:
                {
                    NetworkManager.Instance.QueueEvent(ActionType.RoomFilled, payload.Skip(4).ToArray());
                    break;
                }
            case PackType.Heartbeat:
                {
                    var sendTime = BitConverter.ToInt64(payload, 4);
                    var ReceiveTime = DateTimeOffset.UtcNow.Ticks;
                    NetworkManager.ms = (int)((ReceiveTime - sendTime) / TimeSpan.TicksPerMillisecond);
                    receivedHeartbeat = true;
                    break;
                }
            default:
                {
                    break;
                }
        }
    }

    //=================Clean====================//
    public override void CleanUp()
    {
        running = false;

        try { udp?.Close(); } catch { }
        try { tcpStream?.Close(); } catch { }
        try { tcpClient?.Close(); } catch { }
        try
        {
            wsClient?.Close();
            wsClient = null;
        }
        catch
        {

        }

        tcpReceiveThread?.Join();
        udpReceiveThread?.Join();
    }
}
#endif