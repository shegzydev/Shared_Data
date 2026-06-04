#if CLIENT

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Buffers;
using System.Collections.Generic;

internal class Client : Agent
{
    enum Connection
    {
        NIL, SSE, WS
    }
    Connection connection;

    //WS
    private ClientWebSocket wsClient;
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

    public async void ShutDown()
    {
        actionQueue.Enqueue(() =>
        {
            GigNet.Status = "Disconnected...Retrying";

            GigNet.Log?.Invoke("Disconnected...Retrying");

            NetworkManager.TimeOut?.Invoke(true);
        });

        await wsClient.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Time out", CancellationToken.None);
    }

    async Task StartClientWSConnection(string host, int port, long userId, long roomId)
    {
        int retryDelay = 1000;
        int maxRetries = 25;
        int attempts = 0;
        running = true;

        GigNet.Log?.Invoke("Connecting to " + $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws" + $" at port {port}");
        // link = $"ws://127.0.0.1:{port}/ws?userId={userId}&roomId={roomId}";

        actionQueue.Enqueue(() =>
        {
            GigNet.Status = "Attempting Websocket Connection...";
            GigNet.Log?.Invoke("Attempting...(WS)");
        });

        while (attempts < maxRetries)
        {
            var frameBuffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                var link = $"wss://{host}/{NetworkManager.Instance.gameName}_server/ws?userId={userId}&roomId={roomId}";

                wsClient = new ClientWebSocket();

                using (var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    await wsClient.ConnectAsync(new Uri(link), connectCts.Token);
                }

                actionQueue.Enqueue(() =>
                {
                    OnConnected?.Invoke();
                    GigNet.Log?.Invoke("✅ Connected!");
                    GigNet.Status = "Connected to server!";
                });

                attempts = 0; // reset attempts on successful connection
                connection = Connection.WS;

                while (wsClient.State == WebSocketState.Open)
                {
                    GigNet.Log("Open");

                    var segments = new List<byte[]>();
                    int totalBytes = 0;

                    WebSocketReceiveResult result;

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        do
                        {
                            var segment = new ArraySegment<byte>(frameBuffer);

                            result = await wsClient.ReceiveAsync(segment, cts.Token);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                GigNet.Log("Closed");
                                await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged server close", CancellationToken.None);
                            }

                            if (result.MessageType != WebSocketMessageType.Binary)
                            {
                                GigNet.Log("Non Binary");
                                await wsClient.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Only binary messages are accepted", CancellationToken.None);
                            }

                            var chunk = new byte[result.Count];
                            Buffer.BlockCopy(frameBuffer, 0, chunk, 0, result.Count);
                            segments.Add(chunk);
                            totalBytes += result.Count;

                        } while (!result.EndOfMessage);
                    }

                    var buffer = AssembleMessage(segments, totalBytes);

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
            catch (Exception ex)
            {
                GigNet.LogError?.Invoke($"Exception: {ex.Message}");
                await wsClient.CloseAsync(WebSocketCloseStatus.InternalServerError, "Error", CancellationToken.None);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(frameBuffer);
            }

            attempts++;
            await Task.Delay(retryDelay);

            actionQueue.Enqueue(() =>
            {
                GigNet.Log?.Invoke($"Retrying Websocket({attempts}/{maxRetries})");

                GigNet.Status = $"Retrying Websocket({attempts}/{maxRetries})";

                NetworkManager.TimeOut?.Invoke(true);
            });
        }

        await Task.Run(() => StartSSEConnection(host, port, userId, roomId));
        return;
    }

    private static byte[] AssembleMessage(List<byte[]> segments, int totalBytes)
    {
        if (segments.Count == 1)
            return segments[0];

        var assembled = new byte[totalBytes];
        int offset = 0;
        foreach (var chunk in segments)
        {
            Buffer.BlockCopy(chunk, 0, assembled, offset, chunk.Length);
            offset += chunk.Length;
        }
        return assembled;
    }

    async Task StartSSEConnection(string host, int port, long userId, long roomId)
    {
        int retryDelay = 1000;
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
                attempts = 0; // reset attempts on successful connection

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
                GigNet.Log?.Invoke($"Reconnecting with SSE({attempts}/{maxRetries})");

                GigNet.Status = $"Reconnecting with SSE({attempts}/{maxRetries})";

                NetworkManager.TimeOut?.Invoke(true);
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
                    await wsClient?.SendAsync(data, WebSocketMessageType.Binary, true, CancellationToken.None);
                    OutGoingData += data.Length;
                }
                catch
                {
                    try
                    {
                        wsClient?.CloseAsync(WebSocketCloseStatus.InternalServerError, "SendError", CancellationToken.None);
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
            wsClient?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing Server", CancellationToken.None);
            wsClient = null;
            socketThread?.Join();
        }
        catch { }
    }
}
#endif