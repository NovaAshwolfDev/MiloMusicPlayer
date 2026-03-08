// Models/LibrarySong.cs
using Avalonia.Media.Imaging;

namespace MiloMusicPlayer.Models;

public class LibrarySong
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string FilePath { get; set; } = "";
    public Bitmap? AlbumArt { get; set; }
}