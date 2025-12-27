using System;
using System.Collections.Generic;
using System.Numerics;

#if BLAZOR
using System.Text.Json.Serialization;
#endif
using System.Threading.Tasks;

internal struct Vector3
{
#if BLAZOR
    [JsonInclude]
    public float x;

    [JsonInclude]
    public float y;

    [JsonInclude]
    public float z;
#else
    public float x, y, z;
#endif
    public Vector3(float _x = 0, float _y = 0, float _z = 0)
    {
        x = _x; y = _y; z = _z;
    }

    public static Vector3 Cross(Vector3 a, Vector3 b)
    {
        return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
    }

    public static float Dot(Vector3 a, Vector3 b)
    {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    public static Vector3 operator *(Vector3 v, float f)
    {
        return new Vector3(v.x * f, v.y * f, v.z * f);
    }

    public static Vector3 operator *(float f, Vector3 v)
    {
        return new Vector3(v.x * f, v.y * f, v.z * f);
    }

    public static Vector3 operator +(Vector3 vA, Vector3 vB)
    {
        return new Vector3(vA.x + vB.x, vA.y + vB.y, vA.z + vB.z);
    }

    public static Vector3 operator -(Vector3 vA, Vector3 vB)
    {
        return new Vector3(vA.x - vB.x, vA.y - vB.y, vA.z - vB.z);
    }

    public static Vector3 Reflect(Vector3 direction, Vector3 normal)
    {
        return direction - 2 * Dot(direction, normal) * normal;
    }

    public float magnitude()
    {
        return (float)Math.Sqrt(x * x + y * y + z * z);
    }

    public override string ToString()
    {
        return $"({x}, {y}, {z})";
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

    public static int IDInRoom
    {
        get
        {
            try
            {
                return NetworkManager.Instance.IDInRoom;
            }
            catch
            {
                return 0;
            }
        }
    }


#if CLIENT
    public static int ping;

    public static Action<bool> OnTimeOut;
    public static Action<Dictionary<int, (string name, string avatar)>> OnRoomFilled;
    public static void Init(string gameName, int port)
    {
        NetworkManager.Init(gameName, port, false);
    }
    public static void Connect(string url = "", long roomToConnect = -1, long idToBeAssigned = -1)
    {
        NetworkManager.Instance.TryConnect(url, roomToConnect, idToBeAssigned);
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

    public static bool GetRoomParameter(long roomID, string key, out string value)
    {
        return NetworkManager.Instance.GetRoomParameter(roomID, key, out value);
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

    static HashSet<long> retryErrors = new() { 429, 500, 502, 503, 504, 429, 408, 0, 409 };

    //#if SERVER
#if SERVER || !BLAZOR
    public static async Task Get(string url, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, string authToken = "", bool shouldRetry = true, int retries = 5)
    {
        await SimpleWebRequest.Get(url, headers, onSuccess, async (msg, code) =>
        {
            if (retryErrors.Contains(code) && retries > 0)
            {
                await Task.Delay(1000);
                await Get(url, headers, onSuccess, onFailure, authToken: authToken, retries: retries - 1);
            }
            else
            {
                onFailure.Invoke(msg, code);
            }
        }, authToken);
    }

    public static async Task Post(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, string authToken = "", bool shouldRetry = true, int retries = 5)
    {
        await SimpleWebRequest.Post(url, jsonBody, headers, onSuccess, async (msg, code) =>
        {
            if (retryErrors.Contains(code) && retries > 0)
            {
                await Task.Delay(1000);
                await Post(url, jsonBody, headers, onSuccess, onFailure, authToken: authToken, retries: retries - 1);
            }
            else
            {
                onFailure.Invoke(msg, code);
            }
        }, authToken);
    }

    public static async Task Patch(string url, string jsonBody, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, string authToken = "", bool shouldRetry = true, int retries = 5)
    {
        await SimpleWebRequest.Patch(url, jsonBody, headers, onSuccess, async (msg, code) =>
        {
            if (retryErrors.Contains(code) && retries > 0)
            {
                await Task.Delay(1000);
                await Patch(url, jsonBody, headers, onSuccess, onFailure, authToken: authToken, retries: retries - 1);
            }
            else
            {
                onFailure.Invoke(msg, code);
            }
        }, authToken);
    }

    public static async Task Delete(string url, Dictionary<string, string> headers, Action<string, long> onSuccess, Action<string, long> onFailure, string authToken = "", bool shouldRetry = true, int retries = 5)
    {
        await SimpleWebRequest.Delete(url, headers, onSuccess, async (msg, code) =>
        {
            if (retryErrors.Contains(code) && retries > 0)
            {
                await Task.Delay(1000);
                await Delete(url, headers, onSuccess, onFailure, authToken: authToken, retries: retries - 1);
            }
            else
            {
                onFailure.Invoke(msg, code);
            }
        }, authToken);
    }

    public static void UploadImage(string url, string formFieldName, Dictionary<string, string> headers, Action onUploadStart, Action<string, long> onSuccess, Action<string, long> onFailure, string authToken = "")
    {
        WebImageUploader.PickAndPatchImagePng(url, formFieldName, headers, () => onUploadStart(), (msg, code) => onSuccess(msg, code), (msg, code) => onFailure(msg, code), authToken);
    }

    public static Action OnAuthFailed;

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
