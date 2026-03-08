using MiloMusicPlayer.Models;
using TagLib;

namespace MiloMusicPlayer.Services;

public static class MetadataReader
{
    public static Song LoadSong(string path)
    {
        var file = File.Create(path);

        return new Song
        {
            FilePath = path,
            Title = file.Tag.Title ?? System.IO.Path.GetFileNameWithoutExtension(path),
            Artist = file.Tag.FirstPerformer ?? "Unknown Artist",
            Genre = file.Tag.FirstGenre ?? "Unknown",
            Duration = file.Properties.Duration
        };
    }
    public static byte[]? GetAlbumArt(string path)
    {
        var file = TagLib.File.Create(path);

        if (file.Tag.Pictures.Length > 0)
            return file.Tag.Pictures[0].Data.Data;

        return null;
    }
}