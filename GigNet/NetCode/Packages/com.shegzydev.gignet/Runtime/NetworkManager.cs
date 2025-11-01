using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
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
    RPC, IDAssignment, Instantiation, Heartbeat, Destroy, Audio, NetEvent, RoomAssign, RoomFilled, JoinRoom
}

internal enum ActionType
{
    RPC, Spawn, Despawn, Dispatch, NetEvent, JoinedRoom, RoomFilled
}

internal enum ServerIP
{
    localHost, AWS, Linux, LAN
}

internal class NetworkManager : MonoBehaviour
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
#endif
    [Space]
    public int port = 7778;
    [Space]
#if SERVER
    public int[] RoomSizes = { 2 };
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
            if (instance == null)
#pragma warning disable CS0618 // Type or member is obsolete
                instance = FindObjectOfType<NetworkManager>();
#pragma warning restore CS0618 // Type or member is obsolete
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

    void Awake()
    {
        if (instance && instance != this) DestroyImmediate(instance.gameObject);
        instance = this;

        ID = -1;

        rpcRouter = new RPCRouter();

#pragma warning disable CS0618 // Type or member is obsolete
        var netObjects = FindObjectsOfType<NetworkObject>();
#pragma warning restore CS0618 // Type or member is obsolete
        for (int i = 0; i < netObjects.Length; i++)
        {
            rpcRouter.RegisterObject(netObjects[i]);
            networkObjects[netObjects[i].OwnerID] = netObjects[i];
        }

        // DontDestroyOnLoad(gameObject);
    }

    public void RegObject(NetworkObject netObject)
    {
        if (rpcRouter == null) rpcRouter = new RPCRouter();
        rpcRouter.RegisterObject(netObject);
        networkObjects[netObject.OwnerID] = netObject;
    }

    void Start()
    {

    }

    public void TryConnect(long roomToConnect = -1, long idToBeAssigned = -1)
    {

#if SERVER
        Server.roomSizes = RoomSizes;
        agent = new Server(rpcRouter, port, enableAudio);
#elif CLIENT
        Debug.Log("I'm a client");
        serverIP = IPS[ServerEnum];
        Client.OnConnected = () => RequestID(roomToConnect, idToBeAssigned);
        Client.OnReceivedID = (id) =>
        {
            ID = id;
            Debug.Log($"I've been assigned with id {ID}");
            GigNet.OnConnect?.Invoke();

            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            heartbeatRoutine = StartCoroutine(Heartbeat(1));
        };
        agent = new Client(rpcRouter, serverIP, port, enableAudio, roomToConnect);
#endif
    }

    public void TryDisconnect(Action OnDisconnect)
    {
        agent?.CleanUp();
        OnDisconnect?.Invoke();
    }

    void Update()
    {
        HandleQueues();
        agent?.Tick();
    }

    void FixedUpdate()
    {
        agent?.FixedTick();
    }

    void RequestID(long roomToConnect, long idToBeAssigned)
    {
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

                        Debug.Log($"I've ben assigned to room {RoomID} with assigned id {IDInRoom}");

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

                        GigNet.OnRoomFilled?.Invoke(names);
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
        Destroy(networkObjects[ownerID].gameObject);
        networkObjects.Remove(ownerID);
    }

    Coroutine heartbeatRoutine;
    public IEnumerator Heartbeat(int timeOutInSeconds = 1)
    {
        int missedBeat = 0;
        while (true)
        {
            Agent.ResetHeartBeat();
            byte[] heartBeatPack = Util.MergeArrays(BitConverter.GetBytes(12), BitConverter.GetBytes((int)PackType.Heartbeat), BitConverter.GetBytes(DateTimeOffset.UtcNow.Ticks));
            agent?.SendTCPMessage(heartBeatPack);
            float time = Time.time;

            yield return new WaitUntil(() => Agent.receivedHeartbeat || Time.time - time > timeOutInSeconds);

            if (Time.time - time > timeOutInSeconds)
            {
                missedBeat++;
                if (missedBeat > 30) { Debug.Log("Assume Disconnection for Now"); }
            }
            else
            {
                missedBeat = 0;
            }
            yield return new WaitForSeconds(Mathf.Max(0, timeOutInSeconds - (Time.time - time)));
        }
    }

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
#endif

    void OnApplicationQuit()
    {
        Debug.Log("quit");
        agent?.CleanUp();
    }

    public static int ms = 0;
#if CLIENT
    void OnGUI()
    {
        if (showDebugLayer)
        {
            GUI.Label(new Rect(0, 0, 100, 50), $"{ms}ms : {Mathf.Round(1 / Time.deltaTime)}fps", new GUIStyle { fontSize = 20 });
            if (agent == null) return;
            GUI.Label(new Rect(0, 50, 100, 100), $"{agent.BandWidthUsage().In}MB : {agent.BandWidthUsage().Out}MB", new GUIStyle { fontSize = 20 });
        }
    }
#endif
}