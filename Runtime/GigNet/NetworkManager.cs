using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleJSON;

public enum TransferProtocol
{
    TCP, UDP
}

public enum TargetGroup
{
    Server, Others, All
}

internal enum PackType
{
    RPC, IDAssignment, Instantiation, Heartbeat, Destroy, Audio, NetEvent, RoomAssign, RoomFilled, JoinRoom, ForceQuit
}

internal enum ActionType
{
    RPC, Spawn, Despawn, Dispatch, NetEvent, JoinedRoom, RoomFilled
}

internal enum ServerIP
{
    localHost, AWS, Linux, LAN
}

internal class NetworkManager
{
    Dictionary<ServerIP, string> IPS = new() {
        { ServerIP.localHost, "127.0.0.1" },
        { ServerIP.AWS, "16.171.195.142" },
        { ServerIP.LAN, "192.168.0.100" },
        { ServerIP.Linux, "192.168.179.129" } };
#if CLIENT
    public ServerIP ServerEnum;
    string serverIP = "192.168.1.100";
    public string gameName = "word";
    Stopwatch stopwatch;
#endif
    public int port = 7778;
#if SERVER
    public int[] RoomSizes = { 1 };
#endif
    public bool isServer = false;
    public bool enableAudio;

#if CLIENT
    public bool showDebugLayer = true;
#endif

    public static NetworkManager Instance
    {
        get
        {
            if (instance == null) throw new InvalidOperationException("Network Manager Instance Does Not Exist");
            return instance;
        }
    }

    static NetworkManager instance;
    static RPCRouter rpcRouter;
    Agent agent;

    /// <summary>
    /// The Id of the player among its roommates
    /// </summary>
    public long ID { get; private set; }
    /// <summary>
    /// The ID of the current room that the player is on
    /// </summary>
    public long RoomID { get; private set; }
    /// <summary>
    /// The id of the player in the room
    /// </summary>
    public int IDInRoom { get; private set; }

    Dictionary<int, NetworkObject> networkObjects = new();
    ConcurrentQueue<(ActionType eventType, object payload)> actionQueue = new();

    public static void Init(string gameName, int port, bool isServer = true)
    {
        instance = new NetworkManager(gameName, port, isServer);
    }

    NetworkManager(string gameName, int port, bool isServer = true)
    {
#if CLIENT
        this.gameName = gameName;
#endif
        this.port = port;
        this.isServer = isServer;
    }

    public void Init()
    {
        ID = -1;
        rpcRouter = new RPCRouter();
    }

    public void InitNetObjects(NetworkObject[] netObjects)
    {
        for (int i = 0; i < netObjects.Length; i++)
        {
            rpcRouter.RegisterObject(netObjects[i]);
            networkObjects[netObjects[i].OwnerID] = netObjects[i];
        }
    }

    public void RegObject(NetworkObject netObject)
    {
        if (rpcRouter == null) rpcRouter = new RPCRouter();
        rpcRouter.RegisterObject(netObject);
        networkObjects[netObject.OwnerID] = netObject;
    }

    IEnumerator heartbeatRoutine;
    public void TryConnect(string url = "", long roomToConnect = -1, long idToBeAssigned = -1)
    {

#if SERVER
        Server.roomSizes = RoomSizes;
        agent = new Server(rpcRouter, port, enableAudio);
#elif CLIENT
        GigNet.Log?.Invoke("I'm a client");
        serverIP = IPS[ServerEnum];
        Client.OnConnected = () => RequestID(roomToConnect, idToBeAssigned);
        Client.OnReceivedID = (id) =>
        {
            ID = id;
            GigNet.Log?.Invoke($"I've been assigned with id {ID}");
            GigNet.OnConnect?.Invoke();

            if (heartbeatRoutine != null) CoroutineRunnner.StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = CoroutineRunnner.StartCoroutine(Heartbeat(1));
        };
        agent = new Client(rpcRouter, url, port, enableAudio, roomToConnect);

        stopwatch = Stopwatch.StartNew();
#endif
    }

    public void TryDisconnect(Action OnDisconnect)
    {
        agent?.CleanUp();
        OnDisconnect?.Invoke();
    }

    public void Update()
    {
        HandleQueues();
        agent?.Tick();
#if CLIENT
        if (stopwatch.IsRunning &&
         stopwatch.Elapsed >= TimeSpan.FromSeconds(90))
        {
            GigNet.OnForceQuit?.Invoke();
            stopwatch.Stop();
        }
#endif
    }

    public void FixedUpdate()
    {
        agent?.FixedTick();
    }

    void RequestID(long roomToConnect, long idToBeAssigned)
    {
        GigNet.Log?.Invoke("Requesting ID");

        byte[] packID = BitConverter.GetBytes((int)PackType.IDAssignment);
        byte[] playerID = BitConverter.GetBytes(idToBeAssigned);
        byte[] roomBytes = BitConverter.GetBytes(roomToConnect);
        byte[] session = BitConverter.GetBytes(agent.session);
        byte[] len = BitConverter.GetBytes(Util.LengthOfArrays(packID, playerID, roomBytes, session));

        var data = Util.MergeArrays(len, packID, playerID, roomBytes, session);
        QueueEvent(ActionType.Dispatch, data);
    }

    void HandleQueues()
    {
        while (actionQueue.TryDequeue(out (ActionType eventType, object payload) data))
        {
            switch (data.eventType)
            {
                case ActionType.RPC:
                    {
                        rpcRouter.HandlePacket((byte[])data.payload);
                        break;
                    }
                case ActionType.Spawn:
                    {
                        NetworkSpawner.HandleSpawn((byte[])data.payload);
                        break;
                    }
                case ActionType.Despawn:
                    {
                        DestroyOwnerObjects((int)data.payload);
                        break;
                    }
                case ActionType.Dispatch:
                    {
                        agent?.SendTCPMessage((byte[])data.payload);
                        break;
                    }
                case ActionType.NetEvent:
                    {
                        var eventPayload = (byte[])data.payload;

                        long room = BitConverter.ToInt64(eventPayload, 0);
                        byte id = eventPayload[8];

                        var sub = sizeof(long) + sizeof(byte);

                        var args = new byte[eventPayload.Length - sub];
                        Buffer.BlockCopy(eventPayload, sub, args, 0, args.Length);

                        if (isServer || (!isServer && room == RoomID))
                        {
                            GigNet.OnEvent?.Invoke(room, id, args);
                        }

                        break;
                    }
                case ActionType.JoinedRoom:
                    {
                        var eventPayload = (byte[])data.payload;

                        RoomID = BitConverter.ToInt64(eventPayload, 0);
                        IDInRoom = BitConverter.ToInt32(eventPayload, 8);

                        GigNet.Log?.Invoke($"I've ben assigned to room {RoomID} with assigned id {IDInRoom}");

                        GigNet.OnJoinedRoom?.Invoke();

                        break;
                    }
                case ActionType.RoomFilled:
                    {
                        var bytes = (byte[])data.payload;
                        var names = new Dictionary<int, (string name, string avatar)>();

                        int len;
                        if ((len = BitConverter.ToInt32(bytes)) > 0)
                        {
                            bytes = bytes.Skip(4).ToArray();
                            string jsonString = Encoding.UTF8.GetString(bytes, 0, len);

                            JSONNode json = JSONNode.Parse(jsonString);
                            for (int i = 0; i < json.Count; i++)
                            {
                                names.Add(i, (json[i]["name"], json[i]["avatar"]));
                            }
                        }
#if CLIENT
                        GigNet.OnRoomFilled?.Invoke(names);
                        if (stopwatch.IsRunning)
                        {
                            stopwatch.Stop();
                        }
#endif
                        break;
                    }
                default: { break; }
            }
        }
    }

    public void QueueEvent(ActionType eventType, object payload)
    {
        actionQueue.Enqueue((eventType, payload));
    }

    public void NetInstantiate(string objectName, Vector3 position, Quaternion rotation)
    {
        byte[] packID = BitConverter.GetBytes((int)PackType.Instantiation);
#if SERVER
        byte[] netObjectID = BitConverter.GetBytes(Server.ServerInstance.AssignableNetObjectID++);
#elif CLIENT
        byte[] netObjectID = new byte[0];
#endif
        byte[] spawnerIDBytes = BitConverter.GetBytes(ID);
        byte[] nameBytes = Encoding.UTF8.GetBytes(objectName);
        byte[] nameLenBytes = BitConverter.GetBytes(nameBytes.Length);
        byte[] positionBytesX = BitConverter.GetBytes(position.x);
        byte[] positionBytesY = BitConverter.GetBytes(position.y);
        byte[] positionBytesZ = BitConverter.GetBytes(position.z);
        byte[] rotationBytesX = BitConverter.GetBytes(rotation.x);
        byte[] rotationBytesY = BitConverter.GetBytes(rotation.y);
        byte[] rotationBytesZ = BitConverter.GetBytes(rotation.z);
        byte[] rotationBytesW = BitConverter.GetBytes(rotation.w);

        int length = Util.LengthOfArrays(packID, netObjectID, spawnerIDBytes, nameLenBytes, nameBytes, positionBytesX, positionBytesY, positionBytesZ, rotationBytesX, rotationBytesY, rotationBytesZ, rotationBytesW);
        byte[] lengthBytes = BitConverter.GetBytes(length);

        byte[] payLoad = Util.MergeArrays(lengthBytes, packID, netObjectID, spawnerIDBytes, nameLenBytes, nameBytes, positionBytesX, positionBytesY, positionBytesZ, rotationBytesX, rotationBytesY, rotationBytesZ, rotationBytesW);

        QueueEvent(ActionType.Dispatch, payLoad);
        if (isServer) QueueEvent(ActionType.Spawn, payLoad);
    }

    public void DestroyNetObjectByOwner(int ownerID)
    {
#if SERVER
        byte[] packIDBytes = BitConverter.GetBytes((int)PackType.Destroy);
        byte[] ownerIDBytes = BitConverter.GetBytes(ownerID);
        byte[] lengthBytes = BitConverter.GetBytes(Util.LengthOfArrays(packIDBytes, ownerIDBytes));
        byte[] payload = Util.MergeArrays(lengthBytes, packIDBytes, ownerIDBytes);

        ((Server)agent).StackMessage(payload);

        QueueEvent(ActionType.Dispatch, payload);
        QueueEvent(ActionType.Despawn, ownerID);
#endif
    }

    public void DestroyOwnerObjects(int ownerID)
    {
        //Destroy(networkObjects[ownerID].gameObject);
        networkObjects.Remove(ownerID);
    }

#if CLIENT
    public IEnumerator Heartbeat(int timeOutInSeconds = 1)
    {
        int missedBeat = 0;
        while (true)
        {
            Agent.ResetHeartBeat();
            byte[] heartBeatPack = Util.MergeArrays(BitConverter.GetBytes(12), BitConverter.GetBytes((int)PackType.Heartbeat), BitConverter.GetBytes(DateTimeOffset.UtcNow.Ticks));
            agent?.SendTCPMessage(heartBeatPack);
            double time = Time.time;

            yield return new CoroutineRunnner.WaitUntil(() => Agent.receivedHeartbeat || Time.time - time > timeOutInSeconds);

            if (Time.time - time > timeOutInSeconds)
            {
                missedBeat++;
                if (missedBeat > 5)
                {
                    GigNet.OnTimeOut?.Invoke(true);
                    if (missedBeat == 16)
                    {
                        GigNet.OnForceQuit?.Invoke();
                        break;
                    }
                }
            }
            else
            {
                missedBeat = 0;
                GigNet.OnTimeOut?.Invoke(false);
            }
            yield return new CoroutineRunnner.WaitForSeconds(Math.Max(0, timeOutInSeconds - (float)(Time.time - time)));
        }
    }
#endif

    public void SendCookedRPC(TransferProtocol transferProtocol, byte[] payload)
    {
        switch (transferProtocol)
        {
            case TransferProtocol.UDP:
                {
                    agent?.SendUDPMessage(payload);
                    break;
                }
            case TransferProtocol.TCP:
                {
                    agent?.SendTCPMessage(payload);
                    break;
                }
            default:
                break;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ID"></param>
    /// <param name="args"></param>
    /// <param name="room">optional on client, compulsory on server</param>
    public void RaiseNetworkEvent(byte ID, byte[] args, long room = -1)
    {
        var payload = Util.MergeArrays(BitConverter.GetBytes((int)PackType.NetEvent), isServer ? BitConverter.GetBytes(room) : new byte[0], new byte[1] { ID }, args);
        var length = payload.Length;
        var finalPayload = Util.MergeArrays(BitConverter.GetBytes(length), payload);

        if (!isServer)
            agent?.SendTCPMessage(finalPayload);
        else
        {
            agent?.SendTCPMessageToRoom(room, finalPayload);
        }
    }

#if SERVER
    public void HookServerGameAgent(GameAgent_Server servAgent)
    {
        if (!isServer) return;
        Server.serv = servAgent;
    }

    public Dictionary<int, string> GetIDMaps(long roomID)
    {
        return ((Server)agent).GetIDMaps(roomID);
    }

    public bool GetRoomParameter(long roomID, string key, out string value)
    {
        return ((Server)agent).GetRoomParameter(roomID, key, out value);
    }
#endif

    void OnApplicationQuit()
    {
        GigNet.Log?.Invoke("quit");
        agent?.CleanUp();
    }

    public static int ms = 0;
#if CLIENT
    public Action<(string ms_fps, string bandwidth)> OnGUILog;
#endif
    class Time
    {
        public static double time => DateTimeOffset.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
    }
}

public class CoroutineRunnner
{
    static readonly HashSet<IEnumerator> _activeCoroutines = new();
    public static IEnumerator StartCoroutine(IEnumerator routine)
    {
        _activeCoroutines.Add(routine);
        _ = RunRoutine(routine);
        return routine;
    }

    public static void StopCoroutine(IEnumerator routine)
    {
        _activeCoroutines?.Remove(routine);
    }
    static async Task RunRoutine(IEnumerator routine)
    {
        while (_activeCoroutines.Contains(routine))
        {
            if (!routine.MoveNext()) break;

            object yield = routine.Current;

            if (yield is WaitForSeconds wait)
            {
                await Task.Delay(wait.Milliseconds);
            }
            else if (yield is WaitUntil waitUntil)
            {
                while (!waitUntil.IsDone())
                {
                    await Task.Delay(10);
                }
            }
            else if (yield is null)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    public class WaitForSeconds
    {
        public int Milliseconds { get; }

        public WaitForSeconds(float seconds)
        {
            Milliseconds = (int)(seconds * 1000);
        }

        public WaitForSeconds(TimeSpan timeSpan)
        {
            Milliseconds = timeSpan.Seconds * 1000;
        }
    }
    public class WaitUntil
    {
        private readonly Func<bool> _predicate;
        public WaitUntil(Func<bool> predicate)
        {
            _predicate = predicate;
        }

        public bool IsDone() => _predicate();
    }
}
