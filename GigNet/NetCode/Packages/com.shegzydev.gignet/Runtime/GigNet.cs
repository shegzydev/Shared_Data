using System;
using System.Collections.Generic;

public class GigNet
{
    /// <summary>
    /// Returns values; int = roomID; byte = eventID; byte[] = payload
    /// </summary>
    public static Action<long, byte, byte[]> OnEvent;

    public static Action OnConnect;
    public static Action OnJoinedLobby;
    public static Action OnJoinedRoom;

    public static string Alias;

    /// <summary>
    /// returns room capacity
    /// </summary>
    public static Action<Dictionary<int, (string name, string avatar)>> OnRoomFilled;

    public static int IDInRoom => NetworkManager.Instance.IDInRoom;


#if CLIENT
    public static void Connect(long roomToConnect = -1, long idToBeAssigned = -1)
    {
        NetworkManager.Instance.TryConnect(roomToConnect, idToBeAssigned);
    }
#elif SERVER
    public static void HookServerAgent(GameAgent_Server agent)
    {
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

    public static void Get(string url, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        SimpleWebRequest.Get(url, headers, onSuccess, onFailure, shouldRetry);
    }

    public static void Post(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        SimpleWebRequest.Post(url, jsonBody, headers, onSuccess, onFailure, shouldRetry);
    }

    public static void Patch(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false)
    {
        SimpleWebRequest.Patch(url, jsonBody, headers, onSuccess, onFailure, shouldRetry);
    }

    public static string HASH(string data, string key)
    {
        return SimpleWebRequest.ComputeHmacSHA512(data, key);
    }

    public static byte[] PackBytes(params byte[][] bytes)
    {
        return Util.MergeArrays(bytes);
    }
}

