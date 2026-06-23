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
using System.Text;
using SimpleJSON;
using UnityEngine.UIElements;
using System.Text.Json;
using UnityEngine;

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

    CancellationTokenSource cts;

    public Client(RPCRouter rPCRouter, string _serverIP, int _port, long userId, long roomId, CancellationToken token)
    {
        GigNet.Status = "Connecting to server...";

        cts = CancellationTokenSource.CreateLinkedTokenSource(token);

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
        sseLink = $"https://{serverIP}/{GigNet.gameName}_server/msg?userId={userId}&roomId={roomId}";

        Task.Run(() => StartClientWSConnection(serverIP, port, userId, roomId));
    }

    public static readonly HttpClient errorClient = new();

    async Task StartClientWSConnection(string host, int port, long userId, long roomId)
    {
        int retryDelay = 1000;
        int maxRetries = 25;
        int attempts = 0;
        running = true;

        GigNet.Log?.Invoke("Connecting to " + $"wss://{host}/{GigNet.gameName}_server/ws" + $" at port {port}");
        // link = $"ws://127.0.0.1:{port}/ws?userId={userId}&roomId={roomId}";

        actionQueue.Enqueue(() =>
        {
            GigNet.Status = "Attempting Websocket Connection...";
            GigNet.Log?.Invoke("Attempting...(WS)");
        });

        while (attempts < maxRetries && !cts.IsCancellationRequested)
        {
            var frameBuffer = ArrayPool<byte>.Shared.Rent(4096);

            try
            {
                var link = $"wss://{host}/{GigNet.gameName}_server/ws?userId={userId}&roomId={roomId}";

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

                _ = Ping();

                while (wsClient.State == WebSocketState.Open)
                {
                    GigNet.Log("Open");

                    WebSocketReceiveResult result;
                    int offset = 0;

                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        do
                        {
                            result = await wsClient.ReceiveAsync(
                                new ArraySegment<byte>(frameBuffer, offset, frameBuffer.Length - offset),
                                cts.Token);

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                GigNet.Log("Closed");
                                throw new Exception("Closed");
                            }

                            if (result.MessageType != WebSocketMessageType.Binary)
                            {
                                GigNet.Log("Non Binary");
                                throw new Exception("Wrong Data Type");
                            }

                            offset += result.Count;

                            if (offset >= frameBuffer.Length)
                                throw new Exception("");

                        } while (!result.EndOfMessage);
                    }
                    int payloadOffset = 4;
                    int payloadLength = offset - payloadOffset;

                    if (payloadLength <= 0)
                        continue;

                    HandlePayload(frameBuffer.AsSpan(payloadOffset, payloadLength).ToArray());
                }
            }
            catch (Exception ex)
            {
                GigNet.LogError?.Invoke($"Exception: {ex} \n {ex.Message} \n {ex.InnerException} \n {ex.StackTrace}");
                _ = LogError(ex);
                GigNet.errors += $"{ex.Message}\n\n";
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

                GigNet.OnTimeOut?.Invoke(true);
            });
        }

        await Task.Run(() => StartSSEConnection(host, port, userId, roomId));
        return;
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

        while (attempts < maxRetries && !cts.IsCancellationRequested)
        {
            try
            {
                // var link = $"http://127.0.0.1:{port}/join?userId={userId}&roomId={roomId}";
                var link = $"https://{host}/{GigNet.gameName}_server/join?userId={userId}&roomId={roomId}";

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
                _ = Ping();

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
                GigNet.errors += $"{e.Message}\n\n";
                _ = LogError(e);

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

    async Task Ping()
    {
        while (!cts.IsCancellationRequested)
        {
            byte[] heartBeatPack = Util.MergeArrays(BitConverter.GetBytes(12), BitConverter.GetBytes((int)PackType.Heartbeat), BitConverter.GetBytes(DateTimeOffset.UtcNow.Ticks));
            SendTCPMessage(heartBeatPack);
            await Task.Delay(2000);
        }
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
                catch (Exception e)
                {
                    try
                    {
                        actionQueue.Enqueue(() =>
                        {
                            GigNet.Log($"Error pushing Data {e.Message}");
                        });
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

    async Task LogError(Exception e, int retries = 5)
    {
        if (retries <= 0) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                username = GigNet.user,
                message = e.Message,
                errorCode = (e as WebSocketException)?.WebSocketErrorCode.ToString(),
                innerException = e.InnerException?.Message,
                innerInner = e.InnerException?.InnerException?.Message,
                timestamp = DateTime.UtcNow,
            });

            var response = await errorClient.PostAsync(
                $"https://gameserver.skyboardgames.com/{GigNet.gameName}_server/log-error",
                new StringContent(payload, Encoding.UTF8, "application/json")
            );

            var msg = await response.Content.ReadAsStringAsync();

            GigNet.Log("Sent error log" + msg);
        }
        catch (Exception ex)
        {
            GigNet.LogError("Error_Sending_Logs " + ex.Message);
            await LogError(e, retries - 1);
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
                    var eventArgs = new ReadOnlySpan<byte>(payload).Slice(4);
                    // Buffer.BlockCopy(payload, 4, eventArgs, 0, eventArgs.Length);
                    // NetworkManager.Instance.QueueEvent(ActionType.NetEvent, eventArgs);

                    var eventPayload = eventArgs;

                    long room = BitConverter.ToInt64(eventPayload);
                    byte id = eventPayload[8];

                    var sub = sizeof(long) + sizeof(byte);

                    var args = eventPayload.Slice(sub).ToArray();
                    // Buffer.BlockCopy(eventPayload, sub,s args, 0, args.Length);

                    if (room == GigNet.RoomID)
                    {
                        actionQueue.Enqueue(() =>
                        {
                            GigNet.OnEvent?.Invoke(room, id, args);
                        });
                    }

                    break;
                }
            case PackType.RoomAssign:
                {
                    var data = new ReadOnlySpan<byte>(payload);

                    // Buffer.BlockCopy(payload, 4, data, 0, data.Length);
                    // NetworkManager.Instance.QueueEvent(ActionType.JoinedRoom, data);

                    var eventPayload = data.Slice(4);

                    GigNet.RoomID = BitConverter.ToInt64(eventPayload);
                    GigNet.IDInRoom = BitConverter.ToInt32(eventPayload.Slice(8));

                    actionQueue.Enqueue(() =>
                    {
                        GigNet.Log?.Invoke($"I've been assigned to room {GigNet.RoomID} with assigned id {GigNet.IDInRoom}");
                        GigNet.OnJoinedRoom?.Invoke();
                        GigNet.Status = $"Joined room {GigNet.RoomID}... Waiting for other players";
                    });

                    break;
                }
            case PackType.RoomFilled:
                {
                    ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(payload);

                    // NetworkManager.Instance.QueueEvent(ActionType.RoomFilled, payload.Skip(4).ToArray());

                    var bytes = span.Slice(4);
                    var names = new List<(string name, string avatar)>();

                    int len;
                    if ((len = BitConverter.ToInt32(bytes)) > 0)
                    {
                        // bytes = bytes.Skip(4).ToArray();
                        string jsonString = Encoding.UTF8.GetString(bytes.Slice(4));

                        JSONNode json = JSONNode.Parse(jsonString);
                        for (int i = 0; i < json.Count; i++)
                        {
                            names.Add((json[i]["name"], json[i]["avatar"]));
                        }
                    }

                    actionQueue.Enqueue(() =>
                    {
                        GigNet.Log?.Invoke("Room filled");
                        GigNet.OnRoomFilled?.Invoke(names);
                        GigNet.Status = "All players have joined! Starting game...";
                    });

                    break;
                }
            case PackType.Heartbeat:
                {
                    var sendTime = BitConverter.ToInt64(payload, 4);
                    var ReceiveTime = DateTimeOffset.UtcNow.Ticks;

                    // NetworkManager.ms = (int)((ReceiveTime - sendTime) / TimeSpan.TicksPerMillisecond);

                    GigNet.ping = (int)((ReceiveTime - sendTime) / TimeSpan.TicksPerMillisecond);
                    receivedHeartbeat = Time.time;

                    break;
                }
            case PackType.Rejected:
                {
                    actionQueue.Enqueue(() =>
                    {
                        GigNet.Log("Rejected!");
                        GigNet.Status = "Rejected!";
                        GigNet.OnTimeOut(true);
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
    public override async Task CleanUp()
    {
        running = false;
        try
        {
            actionQueue.Enqueue(() =>
            {
                GigNet.Log("Cleaning Up");
            });

            await wsClient?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing Server", CancellationToken.None);
            wsClient?.Dispose();

            socketThread?.Join();
        }
        catch { }
    }
}

internal class Time
{
    public static double time => DateTimeOffset.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
}
#endif