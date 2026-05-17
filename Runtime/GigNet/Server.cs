#if SERVER

using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using SimpleJSON;
using System.IO;
using System.Text;
using WebSocketSharp.Server;
using WebSocketSharp;

internal class Server : Agent
{
    public static Server ServerInstance;

    APIServerBridge serverBridge;

    public int AssignableNetObjectID = 0;
    List<byte[]> messagePacks = new();

    ConcurrentQueue<long> disconnectionEventQueue = new();

    ConcurrentQueue<Action> actionQueue = new();

    Dictionary<long, Room> Rooms = new() { { 12345, new Room(1, 1, true, null) { roomId = 12345 } } };
    Queue<(long roomID, DateTime expirationTime)> expiryQueue = new();

    public static GameAgent_Server serv;

    public static int[] roomSizes;

    struct SendData
    {
        public PlayerData client;
        public byte[] dataPack;
        public DateTime createdTime;
    }

    ConcurrentQueue<SendData> SendQueue = new();
    CancellationTokenSource SendCancellationToken = new();

    Thread socketThread;

    public Server(RPCRouter rPCRouter, int port, bool enableAudio)
    {
        if (serv == null)
        {
            throw new Exception("GameAgent_Server value is null, call Gignet.HookServerAgent(agent) before connecting...");
        }

        ServerInstance = this;

        session = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        serv.OnRemoveRoom = (room) =>
        {
            CleanupRoom(room);
        };

        serverBridge = new APIServerBridge(port);
        serverBridge.OnRequestReceived += (body) =>
        {
            var json = JSON.Parse(body);
            GenerateRoom(json["lobbyId"].AsLong, json["realPlayers"], json["botCount"], json["botWins"], json["participants"], json.HasKey("tournamentId") ? new Dictionary<string, string> { { "tournamentId", json["tournamentId"].ToString() } } : null);
        };

        serverBridge.Start();

        socketThread = new Thread(() =>
        {
            SendLoop(SendCancellationToken.Token);
        });
        socketThread.Start();
    }

    public override void SendTCPMessage(byte[] data)
    {
        SendMessage(data);
    }

    public override void SendTCPMessageToRoom(long roomID, byte[] data)
    {
        if (!Rooms.ContainsKey(roomID)) return;

        for (int i = 0; i < Rooms[roomID].playerCount; i++)
        {
            SendMessageTo(Rooms[roomID][i], data);
        }
    }

    public void SendTCPMessageToPlayerInRoom(int roomID, int player, byte[] data)
    {
        SendMessageTo(Rooms[roomID][player], data);
    }

    void SendMessageTo(PlayerData client, byte[] data)
    {
        SendQueue.Enqueue(new SendData { client = client, dataPack = data, createdTime = DateTime.UtcNow });
    }

    void SendMessage(byte[] data)
    {
        // foreach (var client in lobby)
        // {
        //     SendMessageTo(client.Key, data);
        // }
    }

    void CleanupRoom(long room)
    {
        if (!Rooms.ContainsKey(room)) return;

        if (room == 12345) return;

        Rooms[room] = null;
        Rooms.Remove(room);
    }

    public override void CleanUp()
    {
        GigNet.Log?.Invoke("Cleaning up");
        SendCancellationToken.Cancel();
        serverBridge?.CleanUp();
        socketThread?.Join();
    }

    void SendLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            while (SendQueue.TryDequeue(out var data))
            {
                if (data.client == null || data.dataPack == null)
                {
                    continue;
                }

                bool sent = false;

                if (data.client.socket != null && data.client.socket.IsAlive)
                {
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        data.client.socket.Send(data.dataPack);
                        sw.Stop();
                        GigNet.LogError($"Send took {sw.ElapsedMilliseconds}ms for player {data.client.id}");
                        sent = true;
                    }
                    catch (Exception e)
                    {
                        // GigNet.LogError($"SendLoop WS send failed for player {data.client.id}: {e.Message}");
                    }
                }

                if (!sent && data.client.writer != null)
                {
                    try
                    {
                        data.client.writer.writer.WriteLine(Convert.ToBase64String(data.dataPack));
                        sent = true;
                    }
                    catch (Exception e)
                    {
                        // GigNet.LogError($"SendLoop writer send failed for player {data.client.id}: {e.Message}{e.StackTrace}");
                        try
                        {
                            data.client.writer.eventSlim?.Set();
                        }
                        catch { }
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    static readonly List<long> incompleteRooms = new List<long>();

    public void DisconnectPlayer(long Id, long roomId)
    {
        actionQueue.Enqueue(() =>
        {
            if (Rooms.TryGetValue(roomId, out var room))
            {
                serv.OnPlayerDisconnect(roomId, room.GetClientIDInRoom(Id));
            }
        });
    }

    public void LosePlayer(long Id, long roomId)
    {
        actionQueue.Enqueue(() =>
        {
            if (Rooms.TryGetValue(roomId, out var room))
            {
                serv.PlayerLost(roomId, room.GetClientIDInRoom(Id));
            }
        });
    }

    public void RestorePlayer(long Id, long roomId)
    {
        actionQueue.Enqueue(() =>
        {
            if (Rooms.TryGetValue(roomId, out var room))
            {
                serv.PlayerRestored(roomId, room.GetClientIDInRoom(Id));
            }
        });
    }

    public override void Tick()
    {
        foreach (var room in Rooms)
        {
            room.Value.TickRoom();
        }

        // while (reconnectionEventQueue.TryDequeue(out long ID))
        // {
        //     if (!lobby.ContainsKey(ID)) { Debug.Log("not reconnecting..."); continue; }

        //     long roomID = lobby[ID].room;
        //     var room = Rooms[roomID];
        //     serv.OnPlayerReconnect(roomID, room.GetClientIDInRoom(ID));
        // }

        while (actionQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }

        //Release Room wey players no complete
        incompleteRooms.Clear();
        var presentTime = DateTime.UtcNow;
        foreach (var kvp in Rooms)
        {
            var room = kvp.Value;
            if (!room.filled && room.playerCount > 0 &&
                presentTime - room.creationTime >= TimeSpan.FromMinutes(1))
            {
                incompleteRooms.Add(kvp.Key);
            }
        }

        foreach (var item in incompleteRooms)
        {
            var packType = BitConverter.GetBytes((int)PackType.ForceQuit);
            var data = Util.MergeArrays(BitConverter.GetBytes(packType.Length), packType);
            // SendTCPMessageToRoom(item, data);
            CleanupRoom(item);
        }

        //Release Expired Rooms
        var now = DateTimeOffset.UtcNow;
        while (expiryQueue.Count > 0 && expiryQueue.Peek().expirationTime <= now)
        {
            var (roomID, _) = expiryQueue.Dequeue();
            CleanupRoom(roomID);
            Debug.Log($"Room {roomID} expired from recently ended rooms.");
        }
    }

    void AddToRoom(long client, long id, WebSocket socket)
    {
        if (Rooms.TryGetValue(id, out var room) && room.Add(client, socket))
        {
            var data = Util.MergeArrays(BitConverter.GetBytes(16), BitConverter.GetBytes((int)PackType.RoomAssign), BitConverter.GetBytes(id), BitConverter.GetBytes(room.GetClientIDInRoom(client)));
            socket.Send(data);
            GigNet.Log($"Added player {client} to room {id} with id {room.GetClientIDInRoom(client)}");
        }
        else
        {
            var data = Util.MergeArrays(BitConverter.GetBytes(4), BitConverter.GetBytes((int)PackType.Rejected));
            socket.Send(data);
        }
    }

    bool AddToRoom(long client, long id, Writer writer)
    {
        if (Rooms.TryGetValue(id, out var room) && room.Add(client, writer))
        {
            var data = Util.MergeArrays(BitConverter.GetBytes(16), BitConverter.GetBytes((int)PackType.RoomAssign), BitConverter.GetBytes(id), BitConverter.GetBytes(room.GetClientIDInRoom(client)));

            try
            {
                writer.writer.WriteLine(Convert.ToBase64String(data));
            }
            catch (Exception e)
            {
                GigNet.LogError(e.Message + ":" + e.StackTrace);
            }

            GigNet.Log($"Added player {client} to room {id} with id {room.GetClientIDInRoom(client)}");

            return true;
        }
        else
        {
            var data = Util.MergeArrays(BitConverter.GetBytes(4), BitConverter.GetBytes((int)PackType.Rejected));

            try
            {
                writer.writer.WriteLine(Convert.ToBase64String(data));
            }
            catch (Exception e)
            {
                GigNet.LogError(e.Message + ":" + e.StackTrace);
            }

            return false;
        }
    }

    public void OnRoomComplete(long id, int[] inactivePlayers)
    {
        var room = Rooms[id];
        var names = room.GetNames();
        var filledData = Util.MergeArrays(BitConverter.GetBytes(4 + names.Length), BitConverter.GetBytes((int)PackType.RoomFilled), names);

        for (int i = 0; i < room.playerCount; i++)
        {
            SendMessageTo(room[i], filledData);
            Debug.Log($"notified player {room[i].id} of room filled");
        }

        actionQueue.Enqueue(() =>
        {
            serv.CreateRoom(id, room.capacity, room.botCount, room.botWins, inactivePlayers);
        });
    }

    void GenerateRoom(long roomId, int playerCount, int botCount, bool botWins, JSONNode participants, Dictionary<string, string> extras)
    {
        if (Rooms.ContainsKey(roomId)) throw new ArgumentException($"Room {roomId} already exists on the game server");

        var playerData = new Dictionary<long, PlayerData>();

        for (int i = 0; i < participants.Count; i++)
        {
            var player = new PlayerData
            {
                id = participants[i]["id"].AsLong,
                name = participants[i]["name"],
                actualID = participants[i]["actualId"],
                avatar = participants[i]["avatar"],
                room = roomId
            };

            playerData.Add(participants[i]["id"].AsLong, player);
        }

        var createdRoom = new Room(playerCount, botCount, botWins, playerData, extras)
        {
            roomId = roomId,
        };

        Rooms.Add(roomId, createdRoom);
        Debug.Log($"Created room {roomId} with {playerCount} players and {botCount} bots");

        var expirationTime = DateTimeOffset.UtcNow.AddHours(12);
        expiryQueue.Enqueue((roomId, expirationTime.DateTime));
    }

    public Dictionary<int, string> GetIDMaps(long roomID)
    {
        return Rooms[roomID].GetIDs();
    }

    public bool GetRoomParameter(long roomID, string key, out string value)
    {
        return Rooms[roomID].GetExtraData(key, out value);
    }

    public void StackMessage(byte[] payload)
    {
        lock (messagePacks) messagePacks.Add(payload);
    }

    void HandlePayload(long id, long roomId, byte[] payload)
    {
        int packID = BitConverter.ToInt32(payload);

        switch ((PackType)packID)
        {
            case PackType.NetEvent:
                {
                    byte[] eventArgs = new byte[payload.Length - 4];

                    Buffer.BlockCopy(payload, 4, eventArgs, 0, eventArgs.Length);

                    var room = roomId;
                    var data = Util.MergeArrays(BitConverter.GetBytes(room), eventArgs);

                    NetworkManager.Instance.QueueEvent(ActionType.NetEvent, data);

                    break;
                }
            case PackType.Heartbeat:
                {
                    var player = Rooms[roomId].GetPlayer(id);

                    player.ResetTimer();

                    var clTime = BitConverter.ToInt64(payload, 4) / TimeSpan.TicksPerMillisecond;
                    var currentTime = DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

                    Debug.Log($"Received heartbeat from player {id} in room {roomId}. Client ticks: {clTime}, Server ticks: {currentTime}, Difference: {currentTime - clTime} ms");

                    var data = Util.MergeArrays(BitConverter.GetBytes(payload.Length), payload);
                    SendMessageTo(player, data);

                    break;
                }
            default:
                {
                    break;
                }
        }
    }

    class APIServerBridge
    {
        private readonly int _port;

        public delegate void RequestReceivedHandler(string path, string method, string body);
        public event Action<string> OnRequestReceived;

        public int Port => _port;

        private HttpServer server;
        private HttpRouter router;

        public APIServerBridge(int port)
        {
            server = new HttpServer(port);
            server.AddWebSocketService<WebService>("/ws");

            router = new HttpRouter(server);

            _port = port;

            AppDomain.CurrentDomain.ProcessExit += delegate
            {
                CleanUp();
            };
            AppDomain.CurrentDomain.DomainUnload += delegate
            {
                CleanUp();
            };

            router.Post("/createRoom", async ctx =>
            {
                string body = HttpRouter.ReadBody(ctx);
                OnRequestReceived?.Invoke(body);
                await HttpRouter.Json(ctx, "{\"ok\":true}");
            });

            router.Post("/leave", async ctx =>
            {
                long id = long.Parse(ctx.Request.QueryString["userId"]);
                ServerInstance.disconnectionEventQueue.Enqueue(id);
                await HttpRouter.Text(ctx, "Successfully left room");
            });

            router.Post("/msg", async ctx =>
            {
                long room = long.Parse(ctx.Request.QueryString["roomId"]);

                if (!ServerInstance.Rooms.ContainsKey(room))
                {
                    await HttpRouter.Text(ctx, "room no longer exists");
                    return;
                }

                long id = long.Parse(ctx.Request.QueryString["userId"]);

                using var ms = new MemoryStream();
                ctx.Request.InputStream.CopyTo(ms);
                ms.Position = 4; // skip first 4 bytes
                byte[] trimmed = new byte[ms.Length - 4];
                ms.Read(trimmed, 0, trimmed.Length);

                ServerInstance.HandlePayload(id, room, trimmed);

                await HttpRouter.Text(ctx, "received message");
            });

            router.Get("/join", e =>
            {
                long id = long.Parse(e.Request.QueryString["userId"]);
                long room = long.Parse(e.Request.QueryString["roomId"]);

                try
                {
                    e.Response.ContentType = "text/event-stream";
                    e.Response.AppendHeader("Cache-Control", "no-cache");
                    e.Response.SendChunked = true;
                    e.Response.StatusCode = 200;
                    e.Response.OutputStream.Flush();

                    var stream = e.Response.OutputStream;

                    var writer = new StreamWriter(stream) { AutoFlush = true };
                    var done = new ManualResetEventSlim(false);

                    if (ServerInstance.AddToRoom(id, room, new Writer { writer = writer, eventSlim = done }))
                    {
                        GigNet.Log("Joined");
                        done.Wait();
                    }
                }
                catch (Exception ex)
                {
                    GigNet.LogError(ex.Message + ":" + ex.StackTrace);
                }
                finally
                {
                    // GigNet.Log($"Closed connection on {id}");
                    try { e.Response.Close(); } catch { }
                }

                return Task.CompletedTask;
            });
        }

        public void Start()
        {
            try
            {
                server.Start();
                Debug.Log($"HTTP listener started on port {_port}");
            }
            catch (Exception ex)
            {
                Debug.Log($"Failed to start listener on port {_port}: {ex.Message}");
            }
        }

        public void CleanUp()
        {
            try
            {
                Debug.Log($"🛑 Stopping HTTP listener on port {_port}...");
                server.Stop();
                Debug.Log($"✅ Listener on port {_port} stopped and cleaned up.");
            }
            catch (Exception ex)
            {
                Debug.Log($"⚠️ Cleanup error (port {_port}): {ex.Message}");
            }
        }

        class WebService : WebSocketBehavior
        {
            long id, room;

            protected override void OnMessage(MessageEventArgs e)
            {
                base.OnMessage(e);
                if (e.IsBinary)
                {
                    try
                    {
                        byte[] copy = new byte[e.RawData.Length - 4];
                        Buffer.BlockCopy(e.RawData, 4, copy, 0, copy.Length);
                        ServerInstance.HandlePayload(id, room, copy);
                    }
                    catch (Exception ex)
                    {
                        GigNet.LogError(ex.Message);
                    }
                }
            }

            protected override void OnOpen()
            {
                base.OnOpen();

                id = long.Parse(Context.QueryString["userId"]);
                room = long.Parse(Context.QueryString["roomId"]);

                ServerInstance.AddToRoom(id, room, Context.WebSocket);
            }

            protected override void OnError(WebSocketSharp.ErrorEventArgs e)
            {
                base.OnError(e);
            }

            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);
            }
        }

        public class HttpRouter
        {
            private readonly HttpServer _server;
            private readonly Dictionary<string, Func<HttpRequestEventArgs, Task>> _getRoutes = new();
            private readonly Dictionary<string, Func<HttpRequestEventArgs, Task>> _postRoutes = new();

            public HttpRouter(HttpServer server)
            {
                _server = server;
                _server.OnGet += async (sender, e) => await Dispatch(_getRoutes, e);
                _server.OnPost += async (sender, e) => await Dispatch(_postRoutes, e);
            }

            public void Get(string path, Func<HttpRequestEventArgs, Task> handler) => _getRoutes[path] = handler;
            public void Post(string path, Func<HttpRequestEventArgs, Task> handler) => _postRoutes[path] = handler;

            private async Task Dispatch(Dictionary<string, Func<HttpRequestEventArgs, Task>> routes, HttpRequestEventArgs e)
            {
                var path = e.Request.Url.AbsolutePath;

                if (routes.TryGetValue(path, out var handler))
                {
                    try { await handler(e); }
                    catch (Exception ex) { await Error(e, 500, ex.Message); }
                }
                else
                {
                    await Error(e, 404, "Not found");
                }
            }

            public static string ReadBody(HttpRequestEventArgs e)
            {
                using var reader = new StreamReader(e.Request.InputStream, e.Request.ContentEncoding);
                return reader.ReadToEnd();
            }

            public static Task Json(HttpRequestEventArgs e, string json)
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                e.Response.ContentType = "application/json";
                e.Response.ContentLength64 = bytes.Length;
                e.Response.OutputStream.Write(bytes, 0, bytes.Length);
                e.Response.Close();
                return Task.CompletedTask;
            }

            public static Task Text(HttpRequestEventArgs e, string text)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                e.Response.ContentType = "text/plain";
                e.Response.ContentLength64 = bytes.Length;
                e.Response.OutputStream.Write(bytes, 0, bytes.Length);
                e.Response.Close();
                return Task.CompletedTask;
            }

            public static Task Error(HttpRequestEventArgs e, int code, string message)
            {
                e.Response.StatusCode = code;
                return Text(e, message);
            }
        }
    }
}
#endif