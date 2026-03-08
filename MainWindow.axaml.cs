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
using System.Linq;
using System.Threading.Tasks;

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
    private bool _libraryOpen = false;
    private bool _libraryReady = false;
    private bool _libraryInitialized = false;
    private bool _folderView = false;
    private List<LibraryFolder> _allFolders = new();
    private List<LibrarySong> _allLibrarySongs = new();

    private readonly AlbumArtServer _artServer = new();
    private readonly Services.DiscordRPC _rpc = new("980443378661621843");
    public MainWindow()
    {
        string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        InitializeComponent();
        Icon = new WindowIcon("Assets/icon.ico");
        songs = LibraryScanner.ScanFolder(musicFolder);

        if (songs.Count == 0)
            return;

        _ = InitLibrary();
        PlaySong(curSongIndex);
        ShaderBackground.FftBands = player.GetFftBands();

        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.F11)
            {
                WindowState = WindowState == WindowState.FullScreen
                    ? WindowState.Normal
                    : WindowState.FullScreen;
            }
        };

        this.SizeChanged += (_, _) =>
        {
            if (_libraryOpen)
                LibraryButton.Margin = new Thickness(Bounds.Width - 90, LibraryButton.Margin.Top, LibraryButton.Margin.Right, LibraryButton.Margin.Bottom);
            else
                LibraryButton.Margin = new Thickness(0, LibraryButton.Margin.Top, LibraryButton.Margin.Right, LibraryButton.Margin.Bottom);
        };

        ProgressSlider.PropertyChanged += ProgressSliderChanged;
        timer.Interval = TimeSpan.FromMilliseconds(500);
        timer.Tick += UpdatePlaybackTime;
        timer.Start();
    }

    private async Task InitLibrary()
    {
        string musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        _allLibrarySongs = await Task.Run(() => songs.Select(s =>
        {
            var art = MetadataReader.GetAlbumArt(s.FilePath);
            Bitmap? bitmap = null;
            if (art != null)
            {
                using var ms = new MemoryStream(art);
                var full = new Bitmap(ms);
                bitmap = full.CreateScaledBitmap(new PixelSize(48, 48), BitmapInterpolationMode.LowQuality);
                full.Dispose();
            }
            return new LibrarySong
            {
                Title = s.Title,
                Artist = s.Artist,
                Album = s.Genre,
                FilePath = s.FilePath,
                AlbumArt = bitmap
            };
        }).ToList());

        _allFolders = _allLibrarySongs
            .GroupBy(s =>
            {
                var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
                return rel.Split(Path.DirectorySeparatorChar)[0];
            })
            .OrderBy(g => g.Key)
            .Select(g => new LibraryFolder { Name = g.Key, Songs = g.ToList() })
            .ToList();

        LibraryList.ItemsSource = _allLibrarySongs;
        FolderList.ItemsSource = _allFolders;
        _libraryInitialized = true;
    }

    private async void PlaySong(int index)
    {
        playing = true;
        PlayPauseIcon.Data = Geometry.Parse("M6,5H10V19H6M14,5H18V19H14");
        var song = songs[index];
        var art = MetadataReader.GetAlbumArt(song.FilePath);
        byte[]? artBytes = art;

        player.Play(song.FilePath);

        await Task.Delay(200);

        Title = "Milo's Music Player" + " - " + song.Title;
        SongTitleText.Text = song.Title;
        ArtistText.Text = song.Artist;
        GenreText.Text = song.Genre;
        SongPosition.Text = $"0:00 / {player.Duration:hh\\:mm\\:ss}";

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
        SongPosition.Text = FormattedTime(player.Position, player.Duration);

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

    private async void LibraryButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_libraryOpen)
            await FadeToPlayer();
        else
            await FadeToLibrary();
    }

    private void ListViewButton_Click(object? sender, RoutedEventArgs e)
    {
        _folderView = false;
        LibraryList.IsVisible = true;
        FolderList.IsVisible = false;
        ListViewButton.Classes.Set("mediaActive", true);
        ListViewButton.Classes.Set("media", false);
        FolderViewButton.Classes.Set("media", true);
        FolderViewButton.Classes.Set("mediaActive", false);
    }

    private void FolderViewButton_Click(object? sender, RoutedEventArgs e)
    {
        _folderView = true;
        LibraryList.IsVisible = false;
        FolderList.IsVisible = true;
        FolderViewButton.Classes.Set("mediaActive", true);
        FolderViewButton.Classes.Set("media", false);
        ListViewButton.Classes.Set("media", true);
        ListViewButton.Classes.Set("mediaActive", false);
    }

    private async void FolderList_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (FolderList.SelectedItem is not LibrarySong selected) return;

        var index = songs.FindIndex(s => s.FilePath == selected.FilePath);
        if (index < 0) return;

        curSongIndex = index;
        await FadeToPlayer();
        PlaySong(curSongIndex);

        FolderList.SelectedItem = null;
    }
    private async Task FadeToLibrary()
    {
        _libraryOpen = true;
        _libraryReady = false;
        LibraryView.IsVisible = true;

        // animate right margin: 16 (visible at right edge) to -62 (off screen right) for player
        // LibraryButton slides from left (16) to right edge (Bounds.Width - 90)
        double targetRight = 16;
        double startLeft = LibraryButton.Margin.Left;
        double startBottom = LibraryButton.Margin.Bottom;
        double targetLeft = Bounds.Width - 90;

        await AnimationHelper.AnimateMany(200,
            (PlayerView, "Opacity", 1.0, 0.0),
            (LibraryView, "Opacity", 0.0, 1.0),
            (ShaderButton, "Opacity", 1.0, 0.0),
            (LibraryButton, "X", startLeft, targetLeft)
        );

        PlayerView.IsVisible = false;
        _libraryReady = true;
    }

    private async Task FadeToPlayer()
    {
        _libraryOpen = false;
        PlayerView.IsVisible = true;

        double startLeft = LibraryButton.Margin.Left;

        await AnimationHelper.AnimateMany(200,
            (LibraryView, "Opacity", 1.0, 0.0),
            (PlayerView, "Opacity", 0.0, 1.0),
            (ShaderButton, "Opacity", 0.0, 1.0),
            (LibraryButton, "X", startLeft, 0)
        );

        LibraryView.IsVisible = false;
    }
    private string FormatTime(TimeSpan time)
    {
        if (time.Hours > 0)
            return $"{time.Hours}:{time.Minutes:D2}:{time.Seconds:D2}";

        if (time.Minutes > 0)
            return $"{time.Minutes}:{time.Seconds:D2}";

        return $"0:{time.Seconds:D2}";
    }

    private string FormattedTime(TimeSpan position, TimeSpan duration)
    {
        return $"{FormatTime(position)} / {FormatTime(duration)}";
    }    

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.ToLower() ?? "";
        LibraryList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _allLibrarySongs
            : _allLibrarySongs.Where(s =>
                s.Title.ToLower().Contains(query) ||
                s.Artist.ToLower().Contains(query) ||
                s.Album.ToLower().Contains(query)
            ).ToList();
    }

    private async void LibraryList_Tapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (LibraryList.SelectedItem is not LibrarySong selected) return;

        var index = songs.FindIndex(s => s.FilePath == selected.FilePath);
        if (index < 0) return;

        curSongIndex = index;
        await FadeToPlayer();
        PlaySong(curSongIndex);

        LibraryList.SelectedItem = null;
    }

    private void ShaderButton_Click(object? sender, RoutedEventArgs e)
    {
        shadersOn = !shadersOn;
        ShaderBackground.IsVisible = shadersOn;
    }
}