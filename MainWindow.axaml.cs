using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Media;
using Milo.Helpers;
using MiloMusicPlayer.Services;
using MiloMusicPlayer.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace MiloMusicPlayer;

public partial class MainWindow : Window
{
    private AudioPlayer player = new AudioPlayer();
    private DispatcherTimer timer = new DispatcherTimer();
    private List<Song> songs = new();
    public int curSongIndex = 0;
    private bool playing = false;
    private bool updatingSlider = false;
    private bool shadersOn = false;
    
    private readonly AlbumArtServer _artServer = new();
    private readonly Services.DiscordRPC _rpc = new("980443378661621843");

    public MainWindow()
    {
        InitializeComponent();

        songs = LibraryScanner.ScanFolder(@"C:\Users\Milo\Music\Spotify");

        if (songs.Count == 0)
            return;

        PlaySong(curSongIndex);
        
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F11)
            {
                WindowState = WindowState == WindowState.FullScreen
                    ? WindowState.Normal
                    : WindowState.FullScreen;
            }
        };

        ProgressSlider.PropertyChanged += ProgressSliderChanged;
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += UpdatePlaybackTime;
        timer.Start();
    }

    private async void PlaySong(int index)
    {
        playing = true;
        PlayPauseIcon.Data = Geometry.Parse("M6,5H10V19H6M14,5H18V19H14");
        var song = songs[index];
        var art = MetadataReader.GetAlbumArt(song.FilePath);
        byte[]? artBytes = art; 

        player.Play(song.FilePath);

        Title = "Milo's Music Player" + " - " + song.Title;
        SongTitleText.Text = song.Title;
        ArtistText.Text = song.Artist;
        GenreText.Text = song.Genre;
        SongPosition.Text = $"0:00 / {player.Duration:mm\\:ss}";

        if (art != null)
        {
            using var ms = new MemoryStream(art);
            var bitmap = new Bitmap(ms);

            SongImage.Source = bitmap;

            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            double maxWidth = 500;
            double maxHeight = 300;

            double scale = Math.Min(maxWidth / width, maxHeight / height);

            SongImageBorder.Width = width * scale;
            SongImageBorder.Height = height * scale;
        }
        else
        {
            SongImage.Source = new Bitmap(
                AssetLoader.Open(new Uri("avares://MiloMusicPlayer/Assets/NoThumbnail.png"))
            );
            SongImageBorder.Width = 300;
            SongImageBorder.Height = 300;
        }

        // Update art server and Discord RPC
        await _artServer.SetArt(artBytes);
        Console.WriteLine("Art URL: " + _artServer.Url);
        _rpc.UpdatePresence(songs[index], TimeSpan.Zero, player.Duration, _artServer.Url);
    }

    private void UpdatePlaybackTime(object? sender, EventArgs e)
    {
        SongPosition.Text = $"{player.Position:hh\\:mm\\:ss} / {player.Duration:hh\\:mm\\:ss}";

        if (player.Duration.TotalSeconds > 0)
        {
            double target = player.Position.TotalSeconds / player.Duration.TotalSeconds * 100;

            updatingSlider = true;
            ProgressSlider.Value = Helpers.Lerp(ProgressSlider.Value, target, 0.25);
            updatingSlider = false;
        }

        if (player.Duration > TimeSpan.Zero &&
            player.Duration - player.Position < TimeSpan.FromMilliseconds(200))
        {
            NextSong();
        }
    }

    private void ProgressSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (updatingSlider) return;

        if (e.Property == Slider.ValueProperty && player.Duration.TotalSeconds > 0)
        {
            double percent = ProgressSlider.Value / 100;
            player.Position = TimeSpan.FromSeconds(player.Duration.TotalSeconds * percent);
        }
    }

    private void NextSong()
    {
        if (songs.Count == 0) return;

        curSongIndex++;

        if (curSongIndex >= songs.Count)
            curSongIndex = 0;

        PlaySong(curSongIndex);
    }

    private void SkipForward_Click(object? sender, RoutedEventArgs e)
    {
        if (songs.Count == 0) return;

        curSongIndex++;

        if (curSongIndex >= songs.Count)
            curSongIndex = 0;

        PlaySong(curSongIndex);
    }

    private void SkipBack_Click(object? sender, RoutedEventArgs e)
    {
        if (songs.Count == 0) return;

        curSongIndex--;

        if (curSongIndex < 0)
            curSongIndex = songs.Count - 1;

        PlaySong(curSongIndex);
    }

    private void PlayPause_Click(object? sender, RoutedEventArgs e)
    {
        if (playing)
        {
            player.Pause();
            PlayPauseIcon.Data = Geometry.Parse("M8,5V19L19,12L8,5Z");
            playing = false;
            _rpc.ClearPresence();
        }
        else
        {
            player.Resume();
            PlayPauseIcon.Data = Geometry.Parse("M6,5H10V19H6M14,5H18V19H14");
            playing = true;
            _rpc.UpdatePresence(songs[curSongIndex], player.Position, player.Duration, _artServer.Url);
        }
    }

    private void LibraryButton_Click(object? sender, RoutedEventArgs e)
    {
        // temp stub so build works
    }

    private void ShaderButton_Click(object? sender, RoutedEventArgs e)
    {
        shadersOn = !shadersOn;
        ShaderBackground.IsVisible = shadersOn;
    }
}