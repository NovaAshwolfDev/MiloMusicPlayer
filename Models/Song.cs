using System;

namespace MiloMusicPlayer.Models;

public class Song
{
    public string FilePath { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Album { get; set; } = "";
    public TimeSpan Duration { get; set; }
}