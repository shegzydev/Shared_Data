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
        NIL, SSE, WS
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
    ConcurrentQueue<byte[]> sendQueue = new();

    public override void Tick()
    {
        while (actionQueue.TryDequeue(out Action action))
        {
            action.Invoke();
        }
        _ = Send();
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

        sseLink = $"http://127.0.0.1:{port}/msg?userId={userId}&roomId={roomId}";
        sseLink = $"https://{serverIP}/{NetworkManager.Instance.gameName}_server/msg?userId={userId}&roomId={roomId}";

        Task.Run(() => StartClientWSConnection(serverIP, port, userId, roomId));
    }

    private bool isRetrying = false; // separate from connecting
    private CancellationTokenSource retryCts;
    int maxWSRetries = 15;

    public async void ShutDown()
    {
        actionQueue.Enqueue(() =>
        {
            GigNet.Status = "Client Timeout...Attempting Retry";
            GigNet.OnTimeOut?.Invoke(true);
        });
        await wsClient?.DisconnectAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "abnormal");
    }

    async Task StartClientWSConnection(string host, int port, long userId, long roomId)
    {
        if (isRetrying) return; // ✅ Block concurrent retry chains
        isRetrying = true;

        // Cancel any previous retry chain
        retryCts?.Cancel();
        retryCts = new CancellationTokenSource();
        var token = retryCts.Token;

        int retriesLeft = maxWSRetries; // ✅ Local copy, not shared state

        while (!token.IsCancellationRequested)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>();

                GigNet.Log?.Invoke("Connecting to " + $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws" + $" at port {port}");
                var link = $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws?userId={userId}&roomId={roomId}";
                // link = $"ws://127.0.0.1:{port}/ws?userId={userId}&roomId={roomId}";

                wsClient = new WebSocketDemo.WebSocketClient(link);

                wsClient.OnConnected += async () =>
                {
                    actionQueue.Enqueue(() =>
                    {
                        OnConnected?.Invoke();
                        GigNet.Log?.Invoke("✅ Connected!");
                        GigNet.Status = "Connected to server!";
                    });
                    connection = Connection.WS;
                    isRetrying = false; // ✅ Fully connected, release lock
                    tcs.TrySetResult(true);
                    retriesLeft = maxWSRetries;
                };

                wsClient.OnDataReceived += async (buffer) =>
                {
                    if (buffer.Length < 4) return;
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
                    GigNet.LogError?.Invoke($"❌ Error: {err}");
                    tcs.TrySetResult(false);
                    try { await wsClient?.DisconnectAsync(System.Net.WebSockets.WebSocketCloseStatus.InternalServerError, "abnormal"); } catch { }
                };

                wsClient.OnDisconnected += async (code, message) =>
                {
                    GigNet.Log?.Invoke($"🔌 Disconnected. {message}");
                    wsClient = null;
                    tcs.TrySetResult(false); // ✅ Signal the loop to handle retry

                    await StartClientWSConnection(host, port, userId, roomId);
                };

                await wsClient.ConnectAsync();
                bool success = await tcs.Task; // ✅ Wait for connect or disconnect

                if (success) return; // Connected — done

                // --- Disconnected or error reached here ---
                if (retriesLeft > 0)
                {
                    retriesLeft--;
                    actionQueue.Enqueue(() =>
                    {
                        GigNet.Log?.Invoke($"Connection Lost... retrying ({retriesLeft} left)");
                        GigNet.Status = $"Connection Lost... retrying ({retriesLeft} left)";
                        GigNet.OnTimeOut?.Invoke(true);
                    });
                    await Task.Delay(500);
                    // loop continues ✅
                }
                else
                {
                    // ✅ All retries exhausted — fallback to SSE
                    actionQueue.Enqueue(() =>
                    {
                        GigNet.Log?.Invoke("Connection Lost... Trying fallback protocol");
                        GigNet.Status = "Connection Lost... Trying fallback protocol";
                        GigNet.OnTimeOut?.Invoke(true);
                    });

                    isRetrying = false;

                    await Task.Run(() => StartSSEConnection(host, port, userId, roomId));

                    return;
                }
            }
            catch (Exception ex)
            {
                GigNet.LogError?.Invoke($"Exception: {ex.Message}");

                if (retriesLeft > 0)
                {
                    retriesLeft--;
                    await Task.Delay(500);
                    // loop continues ✅
                }
                else
                {
                    isRetrying = false;
                    await Task.Run(() => StartSSEConnection(host, port, userId, roomId));
                    return;
                }
            }
        }

        isRetrying = false;
    }

    async Task StartSSEConnection(string host, int port, long userId, long roomId)
    {
        int retryDelay = 3000;
        int maxRetries = 25;
        int attempts = 0;
        running = true;

        actionQueue.Enqueue(() =>
        {
            GigNet.Status = "Attempting...(SSE)";
            GigNet.Log?.Invoke("Attempting...(SSE)");
        });

        while (attempts < maxRetries)
        {
            try
            {
                // var link = $"http://127.0.0.1:{port}/join?userId={userId}&roomId={roomId}";
                var link = $"https://{host}/{NetworkManager.Instance.gameName}_server/join?userId={userId}&roomId={roomId}";

                actionQueue.Enqueue(() =>
                {
                    GigNet.Status = "Connecting to server...(SSE)";
                });

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

                actionQueue.Enqueue(() =>
                {
                    OnConnected?.Invoke();
                    GigNet.Log("Connected to server...(SSE)");
                    GigNet.Status = "Connected to server...(SSE)";
                });

                attempts = 0;
                connection = Connection.SSE;

                while (running)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var buffer = Convert.FromBase64String(line);

                    if (buffer.Length < 4) continue;
                    int payloadLength = BitConverter.ToInt32(buffer);
                    if (payloadLength > 0)
                    {
                        byte[] payload = new byte[payloadLength];
                        Buffer.BlockCopy(buffer, 4, payload, 0, payloadLength);
                        HandlePayload(payload);
                    }
                }
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

            actionQueue.Enqueue(() =>
            {
                GigNet.Log?.Invoke($"Connection lost. Retrying SSE...({attempts}/{maxRetries})");

                GigNet.Status = $"Connection lost. Retrying SSE...({attempts}/{maxRetries})";

                GigNet.OnTimeOut?.Invoke(true);
            });
        }
        connection = Connection.NIL;
        actionQueue.Enqueue(() =>
        {
            GigNet.OnForceQuit?.Invoke();
        });
    }

    //==================UDP======================//
    public override void SendUDPMessage(byte[] data)
    {
        OutGoingData += data.Length;
        // udp.Send(data, data.Length, serverEP);
    }

    public override void SendTCPMessage(byte[] data)
    {
        sendQueue.Enqueue(data);
    }

    async Task Send()
    {
        while (sendQueue.TryDequeue(out var data))
        {
            if (connection == Connection.WS)
            {
                try
                {
                    await wsClient?.SendAsync(data);
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
                }
            }
            else if (connection == Connection.SSE)
            {
                try
                {
                    var content = new ByteArrayContent(data);
                    var response = await sendClient.PostAsync(sseLink, content);
                    OutGoingData += data.Length;
                }
                catch (Exception e)
                {
                    GigNet.LogError?.Invoke("Error sending packet: " + e.Message);
                }
            }
        }
    }

    //=================Payload====================//
    public void HandlePayload(byte[] payload)
    {
        IncomingData += (payload.Length + 4);
        int packID = BitConverter.ToInt32(payload);

        // GigNet.Log?.Invoke(((PackType)packID).ToString());

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
                    actionQueue.Enqueue(() =>
                    {
                        GigNet.OnForceQuit?.Invoke();
                    });
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