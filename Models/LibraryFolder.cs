// Models/LibraryFolder.cs
using System.Collections.Generic;

namespace MiloMusicPlayer.Models;

public class LibraryFolder
{
    public string Name { get; set; } = "";
    public List<LibrarySong> Songs { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
}