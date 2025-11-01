using System;
using System.Collections.Generic;
using SimpleJSON;

public class RoomManager
{

}

internal struct PlayerData
{
    public long id;
    public string name;
    public string actualID;
    public string avatar;
}

internal class Room
{
    public int capacity;
    public int botCount { get; }
    public bool botWins { get; }
    long[] clientIDs;
    int curr;

    Dictionary<long, PlayerData> dataDict { get; }

    public int playerCount => curr;

    public Room(int _capacity, int _bots, bool _botWin, Dictionary<long, PlayerData> _names)
    {
        dataDict = _names;
        botWins = _botWin;
        botCount = _bots;
        capacity = _capacity;
        clientIDs = new long[capacity];
    }

    public bool filled => curr == capacity;

    /// <summary>
    /// returns true if room is filled
    /// </summary>
    /// <param name="clientid"></param>
    /// <returns></returns>
    public bool Add(long clientid, out (bool filled, int assignedIDInRoom) fillData)
    {
        fillData = (false, -1);

        if (filled) return false;

        clientIDs[curr++] = clientid;
        fillData = (filled, curr - 1);

        return true;
    }

    public long this[int index]
    {
        get => clientIDs[index];
        set
        {
            clientIDs[index] = value;
        }
    }

    public int GetClientIDInRoom(long clientID)
    {
        int id = -1;
        for (int i = 0; i < playerCount; i++)
        {
            if (clientIDs[i] == clientID)
            {
                id = i;
            }
        }

        return id;
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