
using System;
using System.Collections.Generic;

public interface GameAgent_Server
{
    public Action<long> OnRemoveRoom { get; set; }
    public void CreateRoom(long ID, int capacity, int bots, bool botWins);
    public void OnFilledRoom(long id, Room room);
    public void OnPlayerDisconnect(long RoomID, int IDInRoom);
    public void OnPlayerReconnect(long RoomID, int IDInRoom);
}
