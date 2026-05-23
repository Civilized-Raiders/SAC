using UnityEngine;


public static class LobbySession 
{
    public enum EntryMode
    {
        None,
        CreatePublic,
        CreatePrivate,
        Join,
    }

    public static EntryMode Mode { get; private set; } = EntryMode.None;
    public static string RoomCode { get; private set; } = string.Empty;

    public static void SetCreate(string roomCode, bool isPrivate)
    {
        Mode = isPrivate ? EntryMode.CreatePrivate : EntryMode.CreatePublic;
        RoomCode = roomCode ?? string.Empty;
    }

    public static void SetJoin(string roomCode)
    {
        Mode = EntryMode.Join;
        RoomCode = roomCode ?? string.Empty;
    }

    /// <summary>
    /// 이전 세션 값 잔존 방지
    /// </summary>
    public static void Clear()
    {
        Mode = EntryMode.None;
        RoomCode = string.Empty;
    }

}
