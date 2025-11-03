#if SERVER

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections.Concurrent;
using Fleck;
using SimpleJSON;
using System.IO;
using System.Text;
using System.Linq;

internal class Server : Agent
{
    public class ClientData
    {
        long assignedId; long id; object conn;

        public ClientData(long _assignedid, long _id = -1, object _conn = null)
        {
            id = _id;
            conn = _conn;
        }

        public object GetConn => conn;
        public long GetId => id;
        public long GetAssignedId => assignedId;
    }

    public static Server ServerInstance;

    TCP tcp;
    UDP udp;
    APIServerBridge serverBridge;
    AudUDP audioUdp;
    WS ws;

    RPCRouter rpcRouter;
    public int AssignableNetObjectID = 0;

    List<byte[]> messagePacks = new();

    ConcurrentQueue<(long id, long target)> roomQueue = new();
    ConcurrentQueue<long> disconnectionEventQueue = new();
    ConcurrentQueue<long> reconnectionEventQueue = new();

    ConcurrentDictionary<long, long> clientRoomAllocation = new();
    Dictionary<long, Room> Rooms = new();

    static Dictionary<long, object> lobby = new();
    static Dictionary<long, float> lobbyTimers = new();

    long currRoomIndex;
    public static GameAgent_Server serv;

    public static int[] roomSizes;
    static ThreadLocal<System.Random> threadRandom = new ThreadLocal<System.Random>(() => new System.Random());

    public Server(RPCRouter rPCRouter, int port, bool enableAudio)
    {
        if (serv == null)
        {
            throw new Exception("GameAgent_Server value is null, call Gignet.HookServerAgent(agent) before connecting...");
        }

        session = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Debug.Log($"Starting Server with Session {session}");

        serv.OnRemoveRoom = (room) =>
        {
            for (int i = 0; i < Rooms[room].playerCount; i++)
            {
                var client = Rooms[room][i];
                clientRoomAllocation.TryRemove(client, out var _);
            }
            Rooms[room] = null;
            Rooms.Remove(room);
        };

        ServerInstance = this;
        rpcRouter = rPCRouter;

        //tcp = new TCP(port);
        // udp = new UDP(port);
        ws = new WS(port);

        serverBridge = new APIServerBridge(port);
        serverBridge.OnRequestReceived += (path, method, body) =>
        {
            var json = JSON.Parse(body);
            GenerateRoom(json["lobbyId"].AsLong, json["realPlayers"], json["botCount"], json["botWins"], json["participants"]);
        };
        serverBridge.Start();

        if (enableAudio) audioUdp = new AudUDP(port + 1);
    }

    public override void SendUDPMessage(byte[] data)
    {
        udp.SendUDPUpdate(data);
    }

    public override void SendTCPMessage(byte[] data)
    {
        SendMessage(data);
    }

    public override void SendTCPMessageToRoom(long roomID, byte[] data)
    {
        for (int i = 0; i < Rooms[roomID].playerCount; i++)
        {
            long clientID = Rooms[roomID][i];
            SendMessageTo(clientID, data);
        }
    }

    public void SendTCPMessageToPlayerInRoom(int roomID, int player, byte[] data)
    {
        long clientID = Rooms[roomID][player];
        SendMessageTo(clientID, data);
    }

    void SendMessageTo(long clientID, byte[] data)
    {
        lock (lobby)
        {
            if (!lobby.TryGetValue(clientID, out var client)) return;

            if (client is IWebSocketConnection wsClient)
            {
                if (wsClient.IsAvailable)
                {
                    try
                    {
                        wsClient.Send(data);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Removing disconnected WS client {clientID}: {ex.Message}");
                        try { wsClient.Close(); } catch { }
                        lobby.Remove(clientID);
                        lobbyTimers.Remove(clientID);
                    }
                }
            }
            else if (client is TcpClient tcpClient)
            {
                if (tcpClient.Connected)
                {
                    try
                    {
                        tcpClient.GetStream().Write(data);
                    }
                    catch (Exception)
                    {
                        Debug.Log("Removing disconnected client.");
                        tcpClient.Close();
                        lobby.Remove(clientID);
                        lobbyTimers.Remove(clientID);
                    }
                }
            }
        }
    }

    void SendMessage(byte[] data)
    {
        foreach (var client in lobby)
        {
            SendMessageTo(client.Key, data);
        }
    }

    public override void CleanUp()
    {
        tcp?.CleanUp();
        udp?.CleanUp();
        ws?.CleanUp();
        audioUdp?.CleanUp();
        serverBridge?.CleanUp();
    }

    public override void Tick()
    {
        var timers = lobbyTimers.ToList();
        foreach (var entry in timers)
        {
            if (entry.Value < 0) continue;

            if (entry.Value >= 5f)
            {
                Debug.Log($"Client {entry.Key} timed out");
                try
                {
                    if (lobby[entry.Key] is IWebSocketConnection wsClient) { wsClient.Close(1006); }
                    else if (lobby[entry.Key] is TcpClient tcpClient) { tcpClient.Close(); }
                }
                catch { }
                lobbyTimers[entry.Key] = -10;
                continue;
            }

            float time = entry.Value + Time.deltaTime;
            lobbyTimers[entry.Key] = time;
        }

        while (roomQueue.TryDequeue(out var result))
        {
            Debug.Log($"player {result.id} is being added to room");
            if (result.target == -1)
                while (!AddToRoom(result.id, currRoomIndex, () => currRoomIndex++))
                {
                    currRoomIndex++;
                }
            else
            {
                AddToRoom(result.id, result.target, () => { });
            }
        }

        while (disconnectionEventQueue.TryDequeue(out long ID))
        {
            if (!lobby.ContainsKey(ID)) continue;

            try
            {
                if (lobby[ID] is IWebSocketConnection wsClient) wsClient.Close();
                else if (lobby[ID] is TcpClient tcpClient) tcpClient.Close();
            }
            catch { }

            lobby.Remove(ID);
            lobbyTimers.Remove(ID);

            if (clientRoomAllocation.ContainsKey(ID))
            {
                long roomID = clientRoomAllocation[ID];
                if (!Rooms.ContainsKey(roomID)) continue;
                var room = Rooms[roomID];
                serv.OnPlayerDisconnect(roomID, room.GetClientIDInRoom(ID));
            }
        }

        while (reconnectionEventQueue.TryDequeue(out long ID))
        {
            if (!lobby.ContainsKey(ID)) { Debug.Log("not reconnecting..."); continue; }

            long roomID = clientRoomAllocation[ID];
            var room = Rooms[roomID];
            serv.OnPlayerReconnect(roomID, room.GetClientIDInRoom(ID));
        }
    }

    bool AddToRoom(long client, long id, Action OnRoomFull)
    {
        if (!Rooms.ContainsKey(id))
        {
            int rand = threadRandom.Value.Next(roomSizes.Length);
            int cap = roomSizes[rand];
            Rooms.Add(id, new Room(cap, 1, true, null));
        }

        if (!Rooms[id].Add(client, out var addToRoom)) return false;

        if (!clientRoomAllocation.TryAdd(client, id))
        {
            Debug.Log($"Could nod add client {client} to room {id}");
        }

        var data = Util.MergeArrays(BitConverter.GetBytes(16), BitConverter.GetBytes((int)PackType.RoomAssign), BitConverter.GetBytes(id), BitConverter.GetBytes(addToRoom.assignedIDInRoom));
        SendMessageTo(client, data);

        Debug.Log($"Added player {client} to room {id} with id {addToRoom.assignedIDInRoom}");

        if (addToRoom.filled)
        {
            var names = Rooms[id].GetNames();
            var filledData = Util.MergeArrays(BitConverter.GetBytes(4 + names.Length), BitConverter.GetBytes((int)PackType.RoomFilled), names);

            for (int i = 0; i < Rooms[id].playerCount; i++)
            {
                SendMessageTo(Rooms[id][i], filledData);
                Debug.Log($"notified player {Rooms[id][i]} of room filled");
            }

            serv.CreateRoom(id, Rooms[id].capacity, Rooms[id].botCount, Rooms[id].botWins);

            serv.OnFilledRoom(id);

            OnRoomFull?.Invoke();
        }

        return true;
    }

    void GenerateRoom(long id, int playerCount, int botCount, bool botWins, JSONNode participants)
    {
        if (Rooms.ContainsKey(id)) throw new ArgumentException($"Room {id} already exists on the game server");

        var names = new Dictionary<long, PlayerData>();
        for (int i = 0; i < participants.Count; i++)
        {
            names.Add(participants[i]["id"].AsLong, new PlayerData { id = participants[i]["id"].AsLong, name = participants[i]["name"], actualID = participants[i]["actualId"], avatar = participants[i]["avatar"] });
        }

        var createdRoom = new Room(playerCount, botCount, botWins, names);
        Rooms.Add(id, createdRoom);
        Debug.Log($"Created room {id} with {playerCount} players and {botCount} bots");
    }

    public Dictionary<int, string> GetIDMaps(long roomID)
    {
        return Rooms[roomID].GetIDs();
    }

    public void StackMessage(byte[] payload)
    {
        lock (messagePacks) messagePacks.Add(payload);
    }

    public Dictionary<long, long> IDMapping = new();
    long nextID;
    void HandlePayload(object client, ref long id, byte[] payload)
    {
        int packID = BitConverter.ToInt32(payload);
        switch ((PackType)packID)
        {
            case PackType.RPC:
                {
                    rpcRouter.HandlePacket(payload);
                    break;
                }
            case PackType.NetEvent:
                {
                    if (id == -1)
                    {
                        Debug.Log($"trying to event with negative {id}");
                        break;
                    }

                    byte[] eventArgs = new byte[payload.Length - 4];

                    Buffer.BlockCopy(payload, 4, eventArgs, 0, eventArgs.Length);

                    var room = clientRoomAllocation[id];
                    var data = Util.MergeArrays(BitConverter.GetBytes(room), eventArgs);

                    NetworkManager.Instance.QueueEvent(ActionType.NetEvent, data);
                    break;
                }
            case PackType.IDAssignment:
                {
                    long currClientID = BitConverter.ToInt64(payload, 4);
                    long roomToJoin = BitConverter.ToInt64(payload, 12);
                    long clientsession = BitConverter.ToInt64(payload, 20);

                    if (clientsession != -1)//Reconnecting User
                    {
                        if (clientsession != session)
                        {
                            if (client is IWebSocketConnection wsClient) wsClient.Close();
                            else if (client is TcpClient tcpClient) tcpClient.Close();
                            Debug.Log("rejecting connection");
                            break;
                        }

                        lock (lobby)
                        {
                            lock (reconnectionEventQueue)
                            {
                                // long newID = Interlocked.Increment(ref nextID);
                                long newID = currClientID;
                                id = newID;

                                if (clientRoomAllocation.ContainsKey(newID))
                                {
                                    long room = clientRoomAllocation[newID];
                                    if (Rooms.ContainsKey(room))
                                    {
                                        int idInroom = Rooms[room].GetClientIDInRoom(newID);
                                        Rooms[room][idInroom] = newID;

                                        if (lobby.ContainsKey(newID))
                                        {
                                            lobby.Remove(newID);
                                            lobbyTimers.Remove(newID);
                                        }

                                        lobby.Add(newID, client);
                                        lobbyTimers.Add(newID, 0);

                                        reconnectionEventQueue.Enqueue(newID);
                                        Debug.Log($"Player {currClientID} now with new ID {newID} is back in room {room}");

                                        byte[] packIDBytes = BitConverter.GetBytes(packID);
                                        byte[] playerIdBytes = BitConverter.GetBytes(newID);
                                        byte[] sessionBytes = BitConverter.GetBytes(session);
                                        byte[] lenBytes = BitConverter.GetBytes(Util.LengthOfArrays(packIDBytes, playerIdBytes, sessionBytes));

                                        var data = Util.MergeArrays(lenBytes, packIDBytes, playerIdBytes, sessionBytes);
                                        SendMessageTo(newID, data);

                                        Debug.Log($"Client {currClientID} Reconnected as {newID}!");
                                    }
                                    else
                                    {
                                        if (client is IWebSocketConnection wsClient) wsClient.Close(1000);
                                        else if (client is TcpClient tcpClient) tcpClient.Close();
                                    }
                                }
                                else
                                {
                                    if (client is IWebSocketConnection wsClient) wsClient.Close(1000);
                                    else if (client is TcpClient tcpClient) tcpClient.Close();
                                    // roomQueue.Enqueue((newID, roomToJoin));
                                }
                            }
                        }
                    }
                    else//New User
                    {
                        lock (lobby)
                        {
                            if (currClientID > -1)
                            {
                                id = currClientID;
                            }
                            else
                            {
                                do
                                {
                                    id = Interlocked.Increment(ref nextID);
                                } while (lobby.ContainsKey(id));
                            }
                            Debug.Log($"Client just requested for ID: {id} and wants to join room: {roomToJoin}");

                            byte[] packIDBytes = BitConverter.GetBytes(packID);
                            byte[] playerIdBytes = BitConverter.GetBytes(id);
                            byte[] sessionBytes = BitConverter.GetBytes(session);
                            byte[] lenBytes = BitConverter.GetBytes(Util.LengthOfArrays(packIDBytes, playerIdBytes, sessionBytes));

                            if (lobby.ContainsKey(id))
                            {
                                Debug.Log($"Lobby already contains player {id}, removing...");
                                lobby.Remove(id);
                                lobbyTimers.Remove(id);
                            }
                            lobby.Add(id, client);
                            lobbyTimers.Add(id, 0);

                            if (clientRoomAllocation.ContainsKey(id))
                            {
                                Debug.Log($"Room allocation already contains player {id} in room {clientRoomAllocation[id]}, removing...");
                                clientRoomAllocation.Remove(id, out long room);

                                if (Rooms.ContainsKey(room))
                                {
                                    Rooms.Remove(room);
                                }
                            }

                            roomQueue.Enqueue((id, roomToJoin));

                            var data = Util.MergeArrays(lenBytes, packIDBytes, playerIdBytes, sessionBytes);
                            SendMessageTo(id, data);

                            foreach (var pack in messagePacks)//Pooled Dispatches
                            {
                                SendMessageTo(id, pack);
                            }
                        }
                    }
                    break;
                }
            case PackType.JoinRoom:
                {
                    //QueueRoom(id, rm => room = rm);//general room
                    //AddToRoom(0, id);//customroom
                    break;
                }
            case PackType.Instantiation:
                {
                    var packIdBytes = BitConverter.GetBytes(packID);
                    var netObjID = BitConverter.GetBytes(AssignableNetObjectID++);
                    var lenBytes = BitConverter.GetBytes(Util.LengthOfArrays(netObjID, payload));

                    byte[] finalPayload = new byte[payload.Length - 4];
                    Buffer.BlockCopy(payload, 4, finalPayload, 0, payload.Length - 4);

                    var data = Util.MergeArrays(lenBytes, packIdBytes, netObjID, finalPayload);
                    SendMessage(data);

                    lock (messagePacks) messagePacks.Add(data);
                    var localData = Util.MergeArrays(packIdBytes, netObjID, finalPayload);
                    NetworkManager.Instance.QueueEvent(ActionType.Spawn, localData);
                    break;
                }
            case PackType.Heartbeat:
                {
                    if (id == -1)
                    {
                        Debug.Log($"trying to heartbeat with negative {id}");
                        break;
                    }

                    lock (lobbyTimers)
                    {
                        lobbyTimers[id] = 0;
                    }

                    var data = Util.MergeArrays(BitConverter.GetBytes(payload.Length), payload);
                    SendMessageTo(id, data);
                    break;
                }
            default:
                {
                    break;
                }
        }
    }

    class WS
    {
        WebSocketServer server;
        public WS(int port)
        {
            FleckLog.Level = LogLevel.Info;
            FleckLog.LogAction = (logLevel, message, exception) => { };

            string protocol = "ws://";
            server = new WebSocketServer($"{protocol}0.0.0.0:{port + 6}");
            server.ListenerSocket.NoDelay = true;

            server.Start(socket =>
            {
                long id = -1;

                socket.OnOpen = () =>
                {
                    Debug.Log("Client connected!");
                };

                socket.OnClose = () =>
                {
                    Debug.Log("Client disconnected!");
                    if (!ServerInstance.disconnectionEventQueue.Contains(id))
                    {
                        ServerInstance.disconnectionEventQueue.Enqueue(id);
                    }
                    socket = null;
                };

                socket.OnError = (exc) =>
                {
                    socket.OnClose = null;
                    socket.OnError = null;
                    socket.OnBinary = null;

                    try { socket.Close(1006); } catch { }
                };

                socket.OnMessage = message =>
                {
                    Debug.Log("received message");
                };

                socket.OnBinary = buffer =>
                {
                    int payloadLength = BitConverter.ToInt32(buffer);
                    if (payloadLength > 0)
                    {
                        byte[] payload = new byte[payloadLength];
                        Buffer.BlockCopy(buffer, 4, payload, 0, payloadLength);
                        ServerInstance.HandlePayload(socket, ref id, payload);
                    }
                };
            });

            Debug.Log($"✅ WebSocket Server started at {protocol}<your-public-ip>:{port + 6}");
        }
        public void CleanUp()
        {
            server.Dispose();
            Debug.Log("Server stopped.");
        }
    }

    class TCP
    {
        TcpListener tcpListener;
        Thread thread;
        volatile bool running = true;

        public TCP(int port = 7778)
        {
            running = true;

            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Server.NoDelay = true;
            tcpListener.Start();

            thread = new Thread(AdmitClients);
            thread.Start();

            Debug.Log($"TCP server started on port {port}");
        }

        async void AdmitClients()
        {
            while (running)
            {
                TcpClient client = await tcpListener.AcceptTcpClientAsync();

                Debug.Log("Client connected.");

                _ = Task.Run(() => HandleTCPClientAsync(client));
            }
        }

        async Task HandleTCPClientAsync(TcpClient client)
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            byte[] buffer = new byte[4];
            long id = -1;

            try
            {
                while (running)
                {
                    // int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    int bytesRead = await Readfully(stream, buffer, 0, buffer.Length);
                    if (bytesRead == 0) { Debug.Log("Read zero bytes"); break; }

                    int payloadLength = BitConverter.ToInt32(buffer);
                    if (payloadLength > 0)
                    {
                        byte[] payload = new byte[payloadLength];
                        // stream.Read(payload, 0, payloadLength);
                        bytesRead = await Readfully(stream, payload, 0, payloadLength);
                        if (bytesRead == 0) { Debug.Log("Read zero bytes"); break; }

                        ServerInstance.HandlePayload(client, ref id, payload);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Client error: {e.Message}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"Client error: {e.InnerException.Message}");
                }
            }
            finally
            {
                client.Close();
                if (id > -1)
                {
                    lock (lobby)
                    {
                        if (lobby.ContainsKey(id))
                        {
                            lobby.Remove(id);
                            lobbyTimers.Remove(id);
                        }
                    }
                }
                Debug.Log("Client disconnected.");
            }
        }

        async Task<int> Readfully(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (count > 0)
            {
                bytesRead = await stream.ReadAsync(buffer, offset, count);
                if (bytesRead == 0)
                    break;

                offset += bytesRead;
                count -= bytesRead;
            }
            return bytesRead;
        }

        public void CleanUp()
        {
            running = false;
            thread.Join();
            tcpListener.Stop();
        }
    }

    class UDP
    {
        List<IPEndPoint> udpClients = new List<IPEndPoint>();
        UdpClient server;
        IPEndPoint remoteEP;
        Thread thread;
        bool running = true;

        public UDP(int port = 7778)
        {
            running = true;

            server = new UdpClient(port);
            remoteEP = new IPEndPoint(IPAddress.Any, 0);

            thread = new Thread(AdmitClients);
            thread.Start();

            Debug.Log($"UDP server started on port {port}");
        }

        async void AdmitClients()
        {
            while (running)
            {
                try
                {
                    UdpReceiveResult result = await server.ReceiveAsync();

                    lock (udpClients)
                    {
                        if (!udpClients.Contains(result.RemoteEndPoint))
                            udpClients.Add(result.RemoteEndPoint);
                    }

                    byte[] data = result.Buffer;
                    int length = BitConverter.ToInt32(data);

                    if (length > 0)
                    {
                        int packID = BitConverter.ToInt32(data, 4);
                        switch ((PackType)packID)
                        {
                            case PackType.RPC:
                                {
                                    byte[] payload = new byte[length];
                                    Buffer.BlockCopy(data, 4, payload, 0, length);
                                    ServerInstance.rpcRouter.HandlePacket(payload);
                                    break;
                                }
                            case PackType.Heartbeat:
                                {
                                    server.Send(data, data.Length, result.RemoteEndPoint);
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
                    Debug.LogError($"Receive error: {e.Message}");
                }
            }
        }

        public void SendUDPUpdate(byte[] data)
        {
            lock (udpClients)
            {
                foreach (var c in udpClients)
                {
                    server.Send(data, data.Length, c);
                }
            }
        }

        public void CleanUp()
        {
            running = false;
            thread.Join();
            try { server?.Close(); } catch { }
        }
    }

    class AudUDP
    {
        List<IPEndPoint> udpClients = new List<IPEndPoint>();
        UdpClient server;
        IPEndPoint remoteEP;
        Thread thread;
        bool running = true;

        public AudUDP(int port = 7778)
        {
            running = true;

            server = new UdpClient(port);
            remoteEP = new IPEndPoint(IPAddress.Any, 0);

            thread = new Thread(AdmitClients);
            thread.Start();

            Debug.Log($"UDP server started on port {port}");
        }

        async void AdmitClients()
        {
            while (running)
            {
                try
                {
                    UdpReceiveResult result = await server.ReceiveAsync();

                    lock (udpClients)
                    {
                        if (!udpClients.Contains(result.RemoteEndPoint))
                            udpClients.Add(result.RemoteEndPoint);
                    }

                    byte[] data = result.Buffer;
                    // SendAudio(data, result.RemoteEndPoint);
                    SendAudio(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Receive error: {e.Message}");
                }
            }
        }

        void SendAudio(byte[] data, IPEndPoint exclude = null)
        {
            lock (udpClients)
            {
                foreach (var c in udpClients)
                {
                    if ((exclude != null && c == exclude) || c == null) continue;
                    server.Send(data, data.Length, c);
                }
            }
        }

        public void CleanUp()
        {
            running = false;
            thread.Join();
            try { server?.Close(); } catch { }
        }
    }

    class APIServerBridge
    {
        private readonly HttpListener _listener;
        private readonly int _port;
        private CancellationTokenSource _cts;

        public delegate void RequestReceivedHandler(string path, string method, string body);
        public event RequestReceivedHandler OnRequestReceived;

        public bool IsRunning => _listener.IsListening;
        public int Port => _port;

        public APIServerBridge(int port)
        {
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:{_port}/"); // allow all hostnames
        }

        /// <summary>
        /// Starts the HTTP listener asynchronously on the specified port.
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();

            try
            {
                _listener.Start();
                Debug.Log($"✅ HTTP listener started on port {_port}");
                _ = ListenLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Debug.Log($"❌ Failed to start listener on port {_port}: {ex.Message}");
            }
        }

        /// <summary>
        /// Non-blocking accept loop for handling requests.
        /// </summary>
        private async Task ListenLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context); // fire and forget
                }
            }
            catch (HttpListenerException)
            {
                // Expected when stopping — safe to ignore
            }
            catch (Exception ex)
            {
                Debug.Log($"⚠️ Listener error (port {_port}): {ex.Message}");
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "POST")
                {
                    string body;
                    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        body = await reader.ReadToEndAsync();

                    // Fire event so your game logic can process the request
                    OnRequestReceived?.Invoke(context.Request.Url.AbsolutePath, context.Request.HttpMethod, body);

                    // Default response
                    var responseJson = "{\"ok\":true}";
                    var buffer = Encoding.UTF8.GetBytes(responseJson);
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.StatusCode = 200;
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.Close();
                }
                else
                {
                    context.Response.StatusCode = 405;
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"⚠️ Request handling error on port {_port}: {ex.Message}");
                context.Response.StatusCode = 500; // Let JS know it failed
                byte[] buffer = Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"" + ex.Message + "\"}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                try
                {
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                catch { /* ignore if stream already dead */ }
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        /// <summary>
        /// Gracefully stops the listener and releases resources.
        /// </summary>
        public void CleanUp()
        {
            try
            {
                if (!IsRunning) return;
                Debug.Log($"🛑 Stopping HTTP listener on port {_port}...");
                _cts?.Cancel();
                _listener.Stop();
                _listener.Close();
                Debug.Log($"✅ Listener on port {_port} stopped and cleaned up.");
            }
            catch (Exception ex)
            {
                Debug.Log($"⚠️ Cleanup error (port {_port}): {ex.Message}");
            }
        }
    }
}
#endif