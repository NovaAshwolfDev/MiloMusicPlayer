using MiloMusicPlayer.Models;
using System.Collections.Generic;
using System.IO;

namespace MiloMusicPlayer.Services;

public static class LibraryScanner
{
    public static List<Song> ScanFolder(string folder)
    {
        var songs = new List<Song>();

        var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLower();

            if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".ogg" || ext == ".m4a")
            {
                try
                {
                    var song = MetadataReader.LoadSong(file);
                    songs.Add(song);
                }
                catch
                {
                }
            }
        }

        return songs;
    }
}