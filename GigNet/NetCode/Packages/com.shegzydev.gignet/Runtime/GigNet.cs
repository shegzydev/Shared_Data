using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal struct Vector3
{
    public float x, y, z;
    public Vector3(float _x = 0, float _y = 0, float _z = 0)
    {
        x = _x; y = _y; z = _z;
    }
}

internal struct Quaternion
{
    public float x, y, z, w;
    public Quaternion(float _x = 0, float _y = 0, float _z = 0, float _w = 0)
    {
        x = _x; y = _y; z = _z; w = _w;
    }
}

public class GigNet
{
    public static Action<string> Log;
    public static Action<string> LogError;
    public static Action<string> LogWarning;
    /// <summary>
    /// Returns values; int = roomID; byte = eventID; byte[] = payload
    /// </summary>
    public static Action<long, byte, byte[]> OnEvent;

    public static Action OnConnect;
    public static Action OnJoinedLobby;
    public static Action OnJoinedRoom;

    public static string Alias;

    public static int IDInRoom => NetworkManager.Instance.IDInRoom;


#if CLIENT
    public static int ping;

    public static Action<bool> OnTimeOut;
    public static Action<Dictionary<int, (string name, string avatar)>> OnRoomFilled;
    public static void Init(string gameName,int port)
    {
        NetworkManager.Init(gameName,port,false);
    }
    public static void Connect(string url ="",long roomToConnect = -1, long idToBeAssigned = -1)
    {
        NetworkManager.Instance.TryConnect(url,roomToConnect, idToBeAssigned);
    }
#elif SERVER
    public static void Init(int port, GameAgent_Server agent)
    {
        NetworkManager.Init("", port);
        NetworkManager.Instance.HookServerGameAgent(agent);
    }
    public static void Connect()
    {
        NetworkManager.Instance.TryConnect();
    }
#endif

    public static void Disconnect(Action OnDisconnect)
    {
        NetworkManager.Instance.TryDisconnect(OnDisconnect);
    }

    public static void JoinRoom(int ID)
    {

    }

#if SERVER
    public static void RemoveRoom(long ID)
    {

    }

    public static void CreateRoom(int ID)
    {

    }

    public static Dictionary<int, string> GetRoomIDMaps(long roomID)
    {
        return NetworkManager.Instance.GetIDMaps(roomID);
    }
#endif

    public static void JoinRoom()
    {

    }

#if CLIENT
    public static void RaiseEvent(byte id, byte[] args)
    {
        NetworkManager.Instance.RaiseNetworkEvent(id, args);
    }
#elif SERVER
    public static void RaiseEvent(byte id, byte[] args, long room)
    {
        NetworkManager.Instance.RaiseNetworkEvent(id, args, room);
    }
#endif

    public static void Tick()
    {
        NetworkManager.Instance.Update();
    }

    public static void FixedTick()
    {
        NetworkManager.Instance.FixedUpdate();
    }

#if SERVER
    public static async void Get(string url, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        await SimpleWebRequest.Get(url, headers, onSuccess, onFailure);
    }

    public static async Task Post(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        await SimpleWebRequest.Post(url, jsonBody, headers, onSuccess, onFailure);
    }

    public static async Task Patch(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        await SimpleWebRequest.Patch(url, jsonBody, headers, onSuccess, onFailure);
    }

    public static string HASH(string data, string key)
    {
        return SimpleWebRequest.ComputeHmacSHA512(data, key);
    }
#endif

    public static byte[] PackBytes(params byte[][] bytes)
    {
        return Util.MergeArrays(bytes);
    }
}
