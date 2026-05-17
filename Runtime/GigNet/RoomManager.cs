using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SimpleJSON;
using UnityEngine;
using WebSocketSharp;

public class Writer
{
    public StreamWriter writer;
    public ManualResetEventSlim eventSlim;
}

public class PlayerData
{
    public long id, room;
    public string name;
    public string actualID;
    public string avatar;
    public WebSocket socket;
    public Writer writer;

    public readonly SemaphoreSlim clientLock = new(1, 1);
    public bool ready = false;

    public void InitSocket(WebSocket _socket)
    {
        socket = _socket;
        Init();
    }

    public void InitWriter(Writer _writer)
    {
        writer = _writer;
        Init();
    }

    void Init()
    {
#if SERVER
        if (ready) Server.ServerInstance.RestorePlayer(room, id);
#endif
        ready = true;
        active = true;
        pingTimer = 0;
        lostConnectionTriggered = false;
    }

    float pingTimer = 0;
    bool active;
    bool lostConnectionTriggered = false;

    public void UpdatePingTimer()
    {
        if (!active) return;

        pingTimer += Time.deltaTime;

        if (!lostConnectionTriggered && pingTimer >= 5f)
        {
            lostConnectionTriggered = true;
            writer?.eventSlim?.Set();
#if SERVER
            Server.ServerInstance.LosePlayer(id, room);
#endif
        }

        if (pingTimer >= 15f)
        {
            active = false;
            ready = false;
#if SERVER
            Server.ServerInstance.DisconnectPlayer(id, room);
#endif
        }
    }

    public void ResetTimer()
    {
        pingTimer = 0;
    }
}

public class Room
{
    public long roomId;
    public int capacity;
    public int botCount { get; }
    public bool botWins { get; }

    long[] clientIDs;

    Dictionary<long, PlayerData> dataDict = new() { { 12345, new PlayerData { id = 12345, name = "Olu", room = 12345 } }, { 67890, new PlayerData { id = 67890, name = "Ola", room = 12345 } } };
    Dictionary<long, int> playerIds = new();
    Dictionary<string, string> extraData = new();

    public DateTime creationTime;

    public bool isClosed;
    float startTime = 0;

    public Room(int _capacity, int _bots, bool _botWin, Dictionary<long, PlayerData> _names, Dictionary<string, string> extras = null)
    {
        if (_names != null) dataDict = _names;

        botWins = _botWin;
        botCount = _bots;
        capacity = _capacity;

        extraData = extras;
        creationTime = DateTime.UtcNow;

        startTime = 0;

        clientIDs = new long[dataDict.Count];
        playerIds = new Dictionary<long, int>(dataDict.Count);
        int index = 0;
        foreach (var kvp in dataDict)
        {
            clientIDs[index] = kvp.Key;
            playerIds[kvp.Key] = index;
            index++;
        }
    }

    public bool filled => dataDict.Count(x => x.Value.ready) >= capacity;

    public int playerCount => dataDict.Count;
    int activeCount => dataDict.Count(x => x.Value.ready);

    public void TickRoom()
    {
        foreach (var player in dataDict)
        {
            player.Value.UpdatePingTimer();
        }

        if (!isClosed)
        {
            startTime += Time.deltaTime;
            if (startTime >= 30)
            {
                // isClosed = true;
                // int[] inactivePlayers = dataDict.Where(x => !x.Value.ready).Select(x => playerIds[x.Key]).ToArray();
                // if (playerCount > 0) OnStart?.Invoke(inactivePlayers);
            }
        }
    }

    public bool Add(long clientid, WebSocket socket)
    {
        if (isClosed && !dataDict[clientid].ready) return false;

        dataDict[clientid].InitSocket(socket);

        if (filled)
        {
            isClosed = true;
            int[] inactivePlayers = dataDict.Where(x => !x.Value.ready).Select(x => playerIds[x.Key]).ToArray();
#if SERVER
            Server.ServerInstance.OnRoomComplete(roomId, inactivePlayers);
#endif
        }

        return true;
    }

    public bool Add(long clientid, Writer writer)
    {
        if (isClosed && !dataDict[clientid].ready) return false;

        dataDict[clientid].InitWriter(writer);

        if (filled)
        {
            isClosed = true;
            int[] inactivePlayers = dataDict.Where(x => !x.Value.ready).Select(x => playerIds[x.Key]).ToArray();
#if SERVER
            Server.ServerInstance.OnRoomComplete(roomId, inactivePlayers);
#endif
        }

        return true;
    }

    public PlayerData this[int index]
    {
        get => dataDict[clientIDs[index]];
    }

    public PlayerData GetPlayer(long id)
    {
        return dataDict[id];
    }

    public bool GetExtraData(string key, out string value)
    {
        value = "";
        if (extraData != null && extraData.TryGetValue(key, out value)) return true;
        return false;
    }

    public int GetClientIDInRoom(long clientID)
    {
        return playerIds[clientID];
    }

    public byte[] GetNames()
    {
        if (dataDict == null)
        {
            return new byte[4] { 0, 0, 0, 0 };
        }

        JSONObject nameData = new JSONObject();

        dataDict.OrderBy(x => playerIds[x.Key]).ToList()
        .ForEach(x =>
        {
            var tmp = new JSONObject();
            var player = x.Value;
            tmp.Add("name", player.name);
            tmp.Add("avatar", player.avatar);
            nameData.Add(playerIds[x.Key].ToString(), tmp);
        });

        /*for (int i = 0; i < dataDict.Count; i++)
        {
            var tmp = new JSONObject();

            var player = this[i];
            tmp.Add("name", player.name);
            tmp.Add("avatar", player.avatar);

            nameData.Add(playerIds[player.id].ToString(), tmp);
        }*/

        string nameString = nameData.ToString();
        var namebytes = System.Text.Encoding.UTF8.GetBytes(nameString);
        var lenBytes = BitConverter.GetBytes(namebytes.Length);
        return GigNet.PackBytes(lenBytes, namebytes);
    }

    public Dictionary<int, string> GetIDs()
    {
        var IDMap = new Dictionary<int, string>();
        if (dataDict == null || dataDict.Count == 0) return IDMap;

        for (int i = 0; i < clientIDs.Length; i++)
        {
            IDMap.Add(i, dataDict[clientIDs[i]].actualID);
        }

        if (botCount > 0)
        {
            foreach (var entry in dataDict)
            {
                if (IDMap.ContainsValue(entry.Value.actualID)) continue;
                IDMap.Add(IDMap.Count, entry.Value.actualID);
            }
        }

        return IDMap;
    }
}

