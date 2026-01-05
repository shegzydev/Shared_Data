using System;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;

public class RoomManager
{

}

public struct PlayerData
{
    public long id;
    public string name;
    public string actualID;
    public string avatar;
}

public class Room
{
    public int capacity;
    public int botCount { get; }
    public bool botWins { get; }

    long[] clientIDs;

    Dictionary<long, PlayerData> dataDict = new() { { 12345, new PlayerData { id = 12345, name = "Olu" } }, { 67890, new PlayerData { id = 67890, name = "Ola" } } };
    Dictionary<long, int> playerIds = new();
    Dictionary<string, string> extraData = new();

    public DateTime creationTime;

    public Room(int _capacity, int _bots, bool _botWin, Dictionary<long, PlayerData> _names, Dictionary<string, string> extras = null)
    {
        if (_names != null) { dataDict = _names; }
        botWins = _botWin;
        botCount = _bots;
        capacity = _capacity;
        clientIDs = new long[capacity];
        extraData = extras;
        creationTime = DateTime.UtcNow;
    }

    public bool filled => playerIds.Count == capacity;
    public int playerCount => playerIds.Count;

    public void Add(long clientid)
    {
        if (playerIds.ContainsKey(clientid)) return;
        int index = playerIds.Count;
        clientIDs[index] = clientid;
        playerIds[clientid] = index;
    }

    public long this[int index]
    {
        get => clientIDs[index];
        set
        {
            clientIDs[index] = value;
        }
    }

    public bool Has(long idToCheck)
    {
        var has = dataDict.Where(x => x.Value.id == idToCheck).ToArray(); ;
        return has.Length > 0;
    }

    public bool GetExtraData(string key, out string value)
    {
        value = "";
        if (extraData != null && extraData.TryGetValue(key, out value)) return true;
        return false;
    }

    public int GetClientIDInRoom(long clientID)
    {
        try
        {
            return playerIds[clientID];
        }
        catch (NullReferenceException)
        {
            return -1;
        }
    }

    public byte[] GetNames()
    {
        if (dataDict == null)
        {
            return new byte[4] { 0, 0, 0, 0 };
        }

        JSONObject nameData = new JSONObject();

        HashSet<string> assigned = new();

        for (int i = 0; i < clientIDs.Length; i++)
        {
            var tmp = new JSONObject();
            tmp.Add("name", dataDict[clientIDs[i]].name);
            tmp.Add("avatar", dataDict[clientIDs[i]].avatar);
            nameData.Add(i.ToString(), tmp);
            assigned.Add(dataDict[clientIDs[i]].name);
        }

        if (assigned.Count < dataDict.Count)
        {
            foreach (var entry in dataDict)
            {
                if (assigned.Contains(entry.Value.name)) continue;
                var tmp = new JSONObject();
                tmp.Add("name", entry.Value.name);
                tmp.Add("avatar", entry.Value.avatar);
                nameData.Add(nameData.Count.ToString(), tmp);
            }
        }

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