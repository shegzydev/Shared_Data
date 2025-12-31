using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using SimpleJSON;

public interface GameAgent_Server
{
    public Action<long> OnRemoveRoom { get; set; }
    public void CreateRoom(long ID, int capacity, int bots, bool botWins);
    public void OnFilledRoom(long id, Room room);
    public void OnPlayerDisconnect(long RoomID, int IDInRoom);
    public void OnPlayerReconnect(long RoomID, int IDInRoom);

    public async Task SignalOnStart(long roomID, Action OnSuccess, bool isTour)
    {
        StreamReader reader = new StreamReader(Application.streamingAssetsPath + "/temp.txt");
        var data = reader.ReadToEnd();
        var json = JSONNode.Parse(data);

        string url = isTour ? json["tourstartURL"].Value : json["startURL"].Value;

        var body = JsonConvert.SerializeObject(new { lobbyId = roomID.ToString() });

        var hash = GigNet.HASH(body, json["GAME_SERVER_KEY"].Value);
        var header = new Dictionary<string, string> { { "skyboard-request-hash", hash } };

        await GigNet.Patch(url, body, header,
            (message, code) =>
                {
                    // Debug.Log($"{code}:{message}");
                    OnSuccess?.Invoke();
                },
            (message, code) =>
                {
                    // Debug.LogError($"{code}:{message}");
                }
        );
    }
    public async Task SignalOnEnd(long roomID, string winner, Action OnSuccess, bool isTour)
    {
        StreamReader reader = new StreamReader(Application.streamingAssetsPath + "/temp.txt");
        var data = reader.ReadToEnd();
        var json = JSONNode.Parse(data);

        string url = isTour ? json["tourendURL"].Value : json["endURL"].Value;

        var body = JsonConvert.SerializeObject(new { lobbyId = roomID.ToString(), winner = winner });

        var hash = GigNet.HASH(body, json["GAME_SERVER_KEY"].Value);
        var header = new Dictionary<string, string> { { "skyboard-request-hash", hash } };

        await GigNet.Patch(url, body, header,
            (message, code) =>
                {
                    // Debug.Log($"{code}:{message}");
                    OnSuccess?.Invoke();
                },
            (message, code) =>
                {
                    // Debug.LogError($"{code}:{message}");
                }
        );
    }
}
