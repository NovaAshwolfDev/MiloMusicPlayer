using System.Collections.Generic;
using System.Linq;

namespace MiloMusicPlayer.Models;

public class LibraryFolder
{
    public string Name { get; set; } = "";
    public List<LibrarySong> Songs { get; set; } = new();
    public List<LibraryFolder> Children { get; set; } = new();

    public IEnumerable<object> Items =>
        Children.Cast<object>().Concat(Songs.Cast<object>());
}