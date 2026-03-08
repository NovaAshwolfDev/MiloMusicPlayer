// Services/DiscordRpcService.cs
using DiscordRPC;
using MiloMusicPlayer.Models;
using System;

namespace MiloMusicPlayer.Services;

public class DiscordRPC
{
    private readonly DiscordRpcClient _client;

    public DiscordRPC(string appId)
    {
        _client = new DiscordRpcClient(appId);
        _client.Initialize();
    }

    public void UpdatePresence(Song song, TimeSpan position, TimeSpan duration, string artUrl)
    {
        var end = DateTimeOffset.UtcNow + (duration - position);
        _client.SetPresence(new RichPresence
        {
            Details = song.Title,
            State = song.Artist,
            Timestamps = new Timestamps { End = end.UtcDateTime },
            Assets = new Assets
            {
                LargeImageKey = artUrl,        // uses the localhost URL now
                LargeImageText = song.Title,
                SmallImageKey = "note",
                SmallImageText = "Milo Music Player"
            }
        });
    }

    public void ClearPresence() => _client.ClearPresence();

    public void Dispose() => _client.Dispose();
}