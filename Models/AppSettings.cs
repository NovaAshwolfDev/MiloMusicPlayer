using System.Collections.Generic;

namespace MiloMusicPlayer.Models;

public class MusicFolderEntry
{
    public string Path { get; set; } = "";
    public bool IncludeSubfolders { get; set; } = true;
}

public class AppSettings
{
    public List<MusicFolderEntry> MusicFolders { get; set; } = new();
 
    public float Volume { get; set; } = 1.0f;
    public string? OutputDevice { get; set; } = null;
 
    public Dictionary<string, string> Keybinds { get; set; } = new()
    {
        ["PlayPause"]    = "Space",
        ["SkipNext"]     = "Right",
        ["SkipPrev"]     = "Left",
        ["VolumeUp"]     = "Up",
        ["VolumeDown"]   = "Down",
        ["Shuffle"]      = "S",
        ["Repeat"]       = "R",
        ["Fullscreen"]   = "F11",
    };
}
