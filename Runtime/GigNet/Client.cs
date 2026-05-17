#if CLIENT

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;

internal class Client : Agent
{
    enum Connection
    {
        SSE, WS
    }
    Connection connection;

    //WS
    private WebSocketDemo.WebSocketClient wsClient;
    private HttpClient client;
    private HttpClient sendClient;

    static RPCRouter rpcRouter;
    string serverIP;
    int port;

    Thread socketThread;
    private volatile bool running = true;

    public static Action OnConnected;
    public static Action<long> OnReceivedID;
    public Action<int> OnDisconnected;

    ConcurrentQueue<Action> actionQueue = new();

    public override void Tick()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        // wsClient?.DispatchMessageQueue();
#endif
        while (actionQueue.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    public override void FixedTick()
    {

    }

    string sseLink;

    public Client(RPCRouter rPCRouter, string _serverIP, int _port, long userId, long roomId)
    {
        GigNet.Status = "Connecting to server...";

        sendClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        rpcRouter = rPCRouter;
        serverIP = _serverIP;
        port = _port;

        if (!string.IsNullOrEmpty(serverIP))
        {
            Uri uri = new Uri(serverIP);
            string host = uri.Host;

            if (string.IsNullOrEmpty(host))
            {
                host = "127.0.0.1";
            }

            serverIP = host;
        }

        // sseLink = $"http://127.0.0.1:{port}/msg?userId={userId}&roomId={roomId}";
        sseLink = $"https://{serverIP}/{NetworkManager.Instance.gameName}_server/msg?userId={userId}&roomId={roomId}";

        socketThread = new Thread(() => StartClientWSConnection(serverIP, port, userId, roomId));
        socketThread.Start();

        // _ = StartSSEConnection(serverIP, port, userId, roomId);
    }

    bool connecting;
    void StartClientWSConnection(string host, int port, long userId, long roomId)
    {
        try
        {
            if (connecting) return;
            connecting = true;

#if DEBUG_MODE
            wsClient = new SimpleWebSocket($"ws://127.0.0.1:{port}");
#elif RELEASE
            GigNet.Log?.Invoke("Connecting to " + $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws" + $" at port {port}");
            var link = $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws?userId={userId}&roomId={roomId}";
            // link = $"ws://127.0.0.1:{port}/ws?userId={userId}&roomId={roomId}";
            wsClient = new WebSocketDemo.WebSocketClient(link);
#endif

            wsClient.OnConnected += async () =>
            {
                OnConnected?.Invoke();
                connection = Connection.WS;
                GigNet.Log?.Invoke("✅ Connected to server!");
                connecting = false;
                GigNet.Status = "Connected to server!";
            };

            wsClient.OnDataReceived += async (buffer) =>
            {
                if (buffer.Length < 4) return; // invalid packet, ignore
                int payloadLength = BitConverter.ToInt32(buffer);
                if (payloadLength > 0)
                {
                    byte[] payload = new byte[payloadLength];
                    Buffer.BlockCopy(buffer, 4, payload, 0, payloadLength);
                    HandlePayload(payload);
                }
            };

            wsClient.OnError += async (err) =>
            {
                connecting = false;
                GigNet.LogError?.Invoke($"❌ Error: {err}");
                try
                {
                    wsClient?.DisconnectAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "abnormal");
                }
                catch { }
            };

            wsClient.OnDisconnected += async (code, message) =>
            {
                GigNet.Log?.Invoke($"🔌 Disconnected.{message}");
                wsClient = null;

                if (code != System.Net.WebSockets.WebSocketCloseStatus.NormalClosure)
                {
                    GigNet.Log?.Invoke("Reconnecting...");
                    StartClientWSConnection(host, port, userId, roomId);
                }
            };

            _ = wsClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            GigNet.LogError?.Invoke($"WS connection failed: {ex.Message}");
        }
    }

    async Task StartSSEConnection(string host, int port, long userId, long roomId)
    {
        int retryDelay = 3000;
        int maxRetries = 25;
        int attempts = 0;
        running = true;

        while (attempts < maxRetries && running)
        {
            try
            {
                // var link = $"http://127.0.0.1:{port}/join?userId={userId}&roomId={roomId}";
                var link = $"https://{host}/{NetworkManager.Instance.gameName}_server/join?userId={userId}&roomId={roomId}";

                GigNet.Status = "Connecting to server...";

                client = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };

                var request = new HttpRequestMessage(HttpMethod.Get, link);

                HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead
                );

                GigNet.Log("sent request");

                Stream stream = await response.Content.ReadAsStreamAsync();
                using StreamReader reader = new StreamReader(stream);

                GigNet.Log("gottenstream");
                OnConnected?.Invoke();
                connection = Connection.SSE;
                GigNet.Status = "Connected to server!";

                while (running)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var buffer = Convert.FromBase64String(line);
                    int payloadLength = BitConverter.ToInt32(buffer);
                    if (payloadLength > 0)
                    {
                        byte[] payload = new byte[payloadLength];
                        Buffer.BlockCopy(buffer, 4, payload, 0, payloadLength);
                        HandlePayload(payload);
                    }
                }

                GigNet.Log("rolled");
            }
            catch (Exception e)
            {
                GigNet.LogError(e.Message + ":" + e.StackTrace);

                if (e.Message.Contains("chunk") || e.Message.Contains("Expecting"))
                {
                    // transient Mono chunked parsing glitch, retry immediately
                    GigNet.Log("Chunk parse glitch, retrying...");
                    continue; // don't increment attempts, don't delay
                }
            }

            attempts++;
            await Task.Delay(retryDelay);
            GigNet.Status = $"Connection lost. Retrying... ({attempts}/{maxRetries})";
        }
    }

    //==================UDP======================//
    public override void SendUDPMessage(byte[] data)
    {
        OutGoingData += data.Length;
        // udp.Send(data, data.Length, serverEP);
    }

    public override async void SendTCPMessage(byte[] data)
    {
        try
        {
            await wsClient.SendAsync(data);
            OutGoingData += data.Length;
        }
        catch
        {
            try
            {
                wsClient?.DisconnectAsync();
                wsClient = null;
            }
            catch { }

            if (!running) return;

            try
            {
                var content = new ByteArrayContent(data);
                var response = await sendClient.PostAsync(sseLink, content);
                OutGoingData += data.Length;
            }
            catch
            {

            }
        }
    }

    //=================Payload====================//
    public void HandlePayload(byte[] payload)
    {
        IncomingData += (payload.Length + 4);
        int packID = BitConverter.ToInt32(payload);
        switch ((PackType)packID)
        {
            case PackType.NetEvent:
                {
                    byte[] eventArgs = new byte[payload.Length - 4];
                    Buffer.BlockCopy(payload, 4, eventArgs, 0, eventArgs.Length);
                    NetworkManager.Instance.QueueEvent(ActionType.NetEvent, eventArgs);
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
                    GigNet.ping = (int)((ReceiveTime - sendTime) / TimeSpan.TicksPerMillisecond);
                    receivedHeartbeat = NetworkManager.Time.time;
                    break;
                }
            case PackType.Rejected:
                {
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
        try
        {
            wsClient?.DisconnectAsync();
            wsClient = null;
            socketThread?.Join();
        }
        catch { }
    }
}
#endif