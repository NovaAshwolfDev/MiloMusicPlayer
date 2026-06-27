using System.Collections.Generic;

namespace MiloMusicPlayer.Models;

public class Friend
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public bool IsOnline { get; set; }
    public string CurrentTrackTitle { get; set; }
    public string CurrentTrackArtist { get; set; }
    public string? SessionId { get; set; }
}

public class UserSearchResult
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class ActivityEntry
{
    public string UserId { get; set; }
    public string DisplayName { get; set; }
    public string Type { get; set; } 
    public string TrackTitle { get; set; }
    public string TrackArtist { get; set; }
    public long Timestamp { get; set; }
}

public class SessionData
{
    public string SessionId { get; set; }
    public string HostUserId { get; set; }
    public string HostDisplayName { get; set; }
    public List<string> ParticipantIds { get; set; } = new();
    public string CurrentTrackPath { get; set; }
    public double CurrentPositionSeconds { get; set; }
    public bool IsPlaying { get; set; }
}

public class ChatMessage
{
    public string UserId { get; set; }
    public string DisplayName { get; set; }
    public string Message { get; set; }
    public long Timestamp { get; set; }
}

public class WsMessage
{
    public string Type { get; set; }
    public object Data { get; set; }
}
