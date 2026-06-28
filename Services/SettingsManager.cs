using System;
using System.IO;
using System.Text.Json;
using MiloMusicPlayer.Models;

namespace MiloMusicPlayer.Services;

public class SettingsManager
{
    private static readonly string _path = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                Current = new AppSettings();
                Current.MusicFolders.Add(new MusicFolderEntry
                {
                    Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                    IncludeSubfolders = true,
                });
                
                Save();
                return;
            }

            var json = File.ReadAllText(_path);
            Current = JsonSerializer.Deserialize<AppSettings>(json, _options) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Current = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, _options);
            File.WriteAllText(_path, json);
        }
        catch (Exception ex)
        {
        }
        
    }

    public static void RegisterKeybind(string action, string key)
    {
        if (!Current.Keybinds.ContainsKey(action))
        {
            Current.Keybinds[action] = key;
            Save();
        }
    }
}
