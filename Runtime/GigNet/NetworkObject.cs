using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;

public interface NetworkObject
{
    public string name { get; set; }
    public int ObjectId { get; set; }
    public int OwnerID {  get; set; }
    public Dictionary<string, MethodInfo> _rpcMethods { get; set; }
    public bool isOwner {  get; set; }

    public void Init(string name)
    {
        name = name.ToLower();
    }

    internal void Register()
    {
        if (_rpcMethods == null) _rpcMethods = new Dictionary<string, MethodInfo>();
        NetworkManager.Instance?.RegObject(this);
        RegisterAllRpcMethods();
    }

    private void RegisterAllRpcMethods()
    {
        if (string.IsNullOrEmpty(name)) throw new Exception("Network Object Has No Name");

        var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var m in methods)
        {
            if (m.GetCustomAttribute<RpcAttribute>() != null)
            {
                _rpcMethods[m.Name] = m;
                GigNet.Log?.Invoke($"[RPC] Registered {m.Name} on object {ObjectId}");
            }
        }
    }

    internal void HandleRpc(string methodName, byte[] payload)
    {
        if (string.IsNullOrEmpty(name)) throw new Exception("Network Object Has No Name");

        if (_rpcMethods.TryGetValue(methodName, out var method))
        {
            var parameters = method.GetParameters();
            object[] args = new object[0];

            if (parameters.Length == 0)
            {
                args = Array.Empty<object>();
            }
            else
            {
                args = new object[parameters.Length];
                MemoryStream stream = new MemoryStream(payload);
                BinaryReader binaryReader = new BinaryReader(stream);
                for (int i = 0; i < parameters.Length; i++)
                {
                    var type = parameters[i].ParameterType;
                    if (type == typeof(int)) args[i] = binaryReader.ReadInt32();
                    else if (type == typeof(long)) args[i] = binaryReader.ReadInt64();
                    else if (type == typeof(float)) args[i] = binaryReader.ReadSingle();
                    else if (type == typeof(bool)) args[i] = binaryReader.ReadBoolean();
                    else if (type == typeof(string)) args[i] = binaryReader.ReadString();
                    else if (type == typeof(Vector3)) args[i] = new Vector3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                    else if (type == typeof(Quaternion)) args[i] = new Quaternion(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
                    else throw new Exception($"Unsupported param type {type}");
                }
            }
            method.Invoke(this, args);
        }
        else
        {
            Console.WriteLine($"Unknown RPC {methodName} on object {ObjectId}");
        }
    }

    protected void SendRPC(TransferProtocol transferProtocol, TargetGroup targetGroup, string methodName, params object[] args)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter binaryWriter = new BinaryWriter(stream);

        binaryWriter.Write((int)PackType.RPC);
        binaryWriter.Write(ObjectId);

        var nameBytes = Encoding.UTF8.GetBytes(methodName);
        binaryWriter.Write((ushort)nameBytes.Length);
        binaryWriter.Write(nameBytes);

        foreach (var arg in args)
        {
            switch (arg)
            {
                case int i:
                    {
                        binaryWriter.Write(i);
                        break;
                    }
                case long l:
                    {
                        binaryWriter.Write(l);
                        break;
                    }
                case float f:
                    {
                        binaryWriter.Write(f);
                        break;
                    }
                case bool b:
                    {
                        binaryWriter.Write(b);
                        break;
                    }
                case string s:
                    {
                        binaryWriter.Write(s);
                        break;
                    }
                case Vector3 v:
                    {
                        binaryWriter.Write(v.x);
                        binaryWriter.Write(v.y);
                        binaryWriter.Write(v.z);
                        break;
                    }
                case Quaternion q:
                    {
                        binaryWriter.Write(q.x);
                        binaryWriter.Write(q.y);
                        binaryWriter.Write(q.z);
                        binaryWriter.Write(q.w);
                        break;
                    }
                default:
                    {
                        throw new Exception($"Unsupported type {arg.GetType()}");
                    }
            }
        }

        byte[] payload = stream.ToArray();
        byte[] payloadLength = BitConverter.GetBytes(payload.Length);

        var data = Util.MergeArrays(payloadLength, payload);

        NetworkManager.Instance.SendCookedRPC(transferProtocol, data);

        stream.Close();
    }

    internal void SetID(int netID, int ownerID)
    {
        ObjectId = netID;
        OwnerID = ownerID;
        isOwner = ownerID == NetworkManager.Instance.ID;
    }

    protected void Destroy()
    {
        if (string.IsNullOrEmpty(name)) throw new Exception("Network Object Has No Name");

        if (!NetworkManager.Instance.isServer) return;
        SendRPC(TransferProtocol.TCP, TargetGroup.All, nameof(NetDestroy));
        OnDestroy?.Invoke(name);
    }

    void NetDestroy()
    {
        OnDestroy?.Invoke(name);
    }

    public Action<string> OnDestroy { get; set; }
}
