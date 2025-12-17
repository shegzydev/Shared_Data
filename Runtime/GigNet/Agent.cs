using System;
using System.Threading.Tasks;

internal class Agent
{
    public Agent() { }
    public Action OnReceiveHeartBeat;
    protected float IncomingData = 0;
    protected float OutGoingData = 0;
    public long session { get; protected set; }
    public static bool receivedHeartbeat { get; protected set; }
    public static void ResetHeartBeat() { receivedHeartbeat = false; }
    public virtual void Tick() { }
    public virtual void FixedTick() { }
    public virtual void SendTCPMessage(byte[] data) { }
    public virtual void SendTCPMessageToRoom(long id, byte[] data) { }
    public virtual void SendUDPMessage(byte[] data) { }
    public virtual void CleanUp() { }
    public (float In, float Out) BandWidthUsage()
    {
        var data = (IncomingData / 1000000f, OutGoingData / 1000000f);
        return data;
    }
}