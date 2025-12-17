using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class RpcAttribute : Attribute
{

}

internal class RPCRouter
{
    private Dictionary<int, NetworkObject> _objects = new();

    public void RegisterObject(NetworkObject obj)
    {
        _objects[obj.ObjectId] = obj;
    }

    public void HandlePacket(byte[] _packet)
    {
        try
        {
            byte[] packet = new byte[_packet.Length - 4];
            Buffer.BlockCopy(_packet, 4, packet, 0, packet.Length);

            int objectId = BitConverter.ToInt32(packet, 0);
            ushort nameLen = BitConverter.ToUInt16(packet, 4);
            string methodName = Encoding.UTF8.GetString(packet, 6, nameLen);

            byte[] payload = new byte[packet.Length - 6 - nameLen];
            Buffer.BlockCopy(packet, 6 + nameLen, payload, 0, payload.Length);

            if (_objects.TryGetValue(objectId, out var obj))
            {
                obj.HandleRpc(methodName, payload);
            }
            else
            {
                GigNet.Log?.Invoke($"Unknown object {objectId}");
            }
        }
        catch (TargetInvocationException e)
        {
            GigNet.LogError?.Invoke($"Captured Exception {e.InnerException.Message}:{e.InnerException.InnerException.Message}:{e.InnerException.InnerException.InnerException.Message}");
        }
    }
}
