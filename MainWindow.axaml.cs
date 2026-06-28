using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Input;
using Milo.Helpers;
using MiloMusicPlayer.Services;
using MiloMusicPlayer.Models;
using MiloMusicPlayer.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Transformation;

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
    private bool _settingsOpen = false;
    private bool _libraryReady = false;
    private bool _libraryInitialized = false;
    private bool _friendsOpen = false;
    private double _settingsButtonOriginalY;
    private double _settingsButtonTranslateY = 0;
    private TranslateTransform _settingsButtonTransform;
    private bool _folderView = false;
    private bool _shuffle = false;
    private bool _repeat = false;
    private List<LibraryFolder> _allFolders = new();
    private List<LibrarySong> _allLibrarySongs = new();
    private bool _animatingSettingsButton = false;

    private readonly AlbumArtServer _artServer = new();
    private readonly Services.DiscordRPC _rpc = new("980443378661621843");
    private ModLoader _modLoader;
    private ModSpriteManager? _spriteManager;

    public MainWindow()
    {
        SettingsManager.Load();
        _ = HydrateUserInfo();
        InitializeComponent();
        Icon = new WindowIcon("Assets/icon.ico");

        player.Volume = SettingsManager.Current.Volume;
        VolumeSlider.Value = SettingsManager.Current.Volume;
        _settingsButtonOriginalY = SettingsButton.Bounds.Top;

        songs = ScanAllFolders();

        var modsPath = Path.Combine(AppContext.BaseDirectory, "Mods");
        Directory.CreateDirectory(modsPath);

        _modLoader = new ModLoader(modsPath);
        _modLoader.Log = msg => Console.WriteLine(msg);
        _modLoader.GetVolume = () => player.Volume;
        _modLoader.SetVolume = v => { player.Volume = v; VolumeSlider.Value = v; };
        _modLoader.RequestPlay = () => ResumePlayback();
        _modLoader.RequestPause = () => PausePlayback();
        _modLoader.RequestNext = () => NextSong();
        _modLoader.RequestPrev = () => PrevSong();
        _spriteManager = new ModSpriteManager(ModSpriteHost);
        _modLoader.SpriteManager = _spriteManager;
        _modLoader.GetPlayerState = () => new PlayerState
        {
            IsPlaying = playing,
            Volume = player.Volume,
            Position = (float)player.Position.TotalSeconds
        };
        _modLoader.GetQueue = () => songs.Select(s => new TrackInfo
        {
            Title = s.Title,
            Artist = s.Artist,
            Album = s.Genre,
            Genre = s.Genre,
            Duration = (float)player.Duration.TotalSeconds,
            Path = s.FilePath
        }).ToList();
        _modLoader.PlayTrack = path =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var index = songs.FindIndex(s => s.FilePath == path);
                if (index >= 0)
                {
                    curSongIndex = index;
                    PlaySong(curSongIndex);
                }
            });
        };
        _modLoader.LoadAll();

        SettingsView.SettingsChanged += OnSettingsChanged;
        SettingsButton.LayoutUpdated += (_, _) =>
        {
            _settingsButtonOriginalY = SettingsButton.Bounds.Top;
        };
        _settingsButtonTransform = new TranslateTransform(0, 0);
        SettingsButton.RenderTransform = _settingsButtonTransform;
        if (songs.Count == 0)
            return;

        _ = InitLibrary();
        PlaySong(curSongIndex);
        PausePlayback();
        ShaderBackground.FftBands = player.GetFftBands();

        KeyDown += HandleGlobalKeyDown;

        this.SizeChanged += (_, _) =>
        {
            if (_libraryOpen)
                LibraryButton.Margin = new Thickness(Bounds.Width - 90, LibraryButton.Margin.Top, LibraryButton.Margin.Right, LibraryButton.Margin.Bottom);
            else
                LibraryButton.Margin = new Thickness(0, LibraryButton.Margin.Top, LibraryButton.Margin.Right, LibraryButton.Margin.Bottom);

            _settingsButtonOriginalY = SettingsButton.Bounds.Top;

            if (_settingsOpen)
            {
                var target = Bounds.Height - 90 - _settingsButtonOriginalY;
                _settingsButtonTranslateY = target - 20;
                SettingsButton.RenderTransform = TransformOperations.Parse($"translate(0px, {_settingsButtonTranslateY}px)");
            }
        };


        Closing += (_, _) => _modLoader.UnloadAll();

        ProgressSlider.PropertyChanged += ProgressSliderChanged;
        timer.Interval = TimeSpan.FromMilliseconds(34 / 2.0);
        timer.Tick += UpdatePlaybackTime;
        timer.Start();
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (FriendsPanel.DataContext is ViewModels.FriendsViewModel friendsVm)
            _ = friendsVm.Refresh();
    }
    private List<Song> ScanAllFolders()
    {
        var all = new List<Song>();
        foreach (var entry in SettingsManager.Current.MusicFolders)
        {
            if (!Directory.Exists(entry.Path)) continue;
            var found = LibraryScanner.ScanFolder(entry.Path);
            if (!entry.IncludeSubfolders)
                found = found.Where(s => Path.GetDirectoryName(s.FilePath) == entry.Path).ToList();
            all.AddRange(found);
        }
        return all;
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        player.Volume = settings.Volume;
        VolumeSlider.Value = settings.Volume;

        songs = ScanAllFolders();
        _ = InitLibrary();
    }

    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_settingsOpen)
            await FadeToPlayer();
        else
            await FadeToSettings();
    }

    private async Task FadeToSettings()
    {
        _settingsOpen = true;
        _libraryOpen = false;
        SettingsView.IsVisible = true;
        SettingsView.Refresh();

        var target = Bounds.Height - 90 - _settingsButtonOriginalY;
        _settingsButtonTranslateY = target - 20;

        SettingsButton.RenderTransform = TransformOperations.Parse($"translate(0px, {_settingsButtonTranslateY}px)");

        await AnimationHelper.AnimateMany(200,
            (PlayerView,    "Opacity", 1.0, 0.0),
            (LibraryView,   "Opacity", LibraryView.Opacity, 0.0),
            (ShaderButton,  "Opacity", 1.0, 0.0),
            (LibraryButton, "Opacity", 1.0, 0.0),
            (FriendsButton, "Opacity", 1.0, 0.0),
            (SettingsView,  "Opacity", 0.0, 1.0),
            (FriendsPanel,  "Opacity", FriendsPanel.Opacity, 0.0)
        );

        PlayerView.IsVisible  = false;
        LibraryView.IsVisible = false;
        FriendsPanel.IsVisible = false;
        _friendsOpen = false;
    }

    private void HandleGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        var binds = SettingsManager.Current.Keybinds;

        bool Is(string action) =>
            binds.TryGetValue(action, out var k) &&
            Enum.TryParse<Key>(k, out var parsed) &&
            e.Key == parsed;

        if (Is("PlayPause")) { PlayPause_Click(null, null!); e.Handled = true; }
        else if (Is("SkipNext")) { NextSong(); e.Handled = true; }
        else if (Is("SkipPrev")) { PrevSong(); e.Handled = true; }
        else if (Is("VolumeUp")) { VolumeSlider.Value = Math.Min(1.0, VolumeSlider.Value + 0.05); e.Handled = true; }
        else if (Is("VolumeDown")) { VolumeSlider.Value = Math.Max(0.0, VolumeSlider.Value - 0.05); e.Handled = true; }
        else if (Is("Shuffle")) { ShuffleButton_Click(null, null!); e.Handled = true; }
        else if (Is("Repeat")) { RepeatButton_Click(null, null!); e.Handled = true; }
        else if (Is("Fullscreen"))
        {
            WindowState = WindowState == WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
            e.Handled = true;
        }
        else
        {
            foreach (var kvp in binds)
            {
                if (kvp.Key.StartsWith("Mod.") &&
                    Enum.TryParse<Key>(kvp.Value, out var modKey) &&
                    e.Key == modKey)
                {
                    _modLoader.FireKeybind(kvp.Key);
                    e.Handled = true;
                    break;
                }
            }
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimiseButton_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximiseButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

        MaximiseIcon.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M4,8H8V4H20V16H16V20H4V8M16,8V14H18V6H10V8H16Z"
            : "M4,4H20V20H4V4M6,8V18H18V8H6Z");
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
        => Close();

    private async Task InitLibrary()
    {
        string musicFolder = SettingsManager.Current.MusicFolders.FirstOrDefault()?.Path
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

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

        var logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");
        using var log = new StreamWriter(logPath, append: false);

        foreach (var s in _allLibrarySongs)
        {
            var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
            log.WriteLine($"rel: {rel} | parts: {rel.Split(Path.DirectorySeparatorChar).Length}");
        }

        var topLevelGroups = _allLibrarySongs
            .GroupBy(s =>
            {
                var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
                return rel.Split(Path.DirectorySeparatorChar)[0];
            })
            .OrderBy(g => g.Key);

        _allFolders = topLevelGroups.Select(topGroup =>
        {
            var directSongs = topGroup.Where(s =>
            {
                var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
                return rel.Split(Path.DirectorySeparatorChar).Length == 1;
            }).ToList();

            var children = topGroup
                .Where(s =>
                {
                    var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
                    return rel.Split(Path.DirectorySeparatorChar).Length >= 2;
                })
                .GroupBy(s =>
                {
                    var rel = Path.GetRelativePath(musicFolder, Path.GetDirectoryName(s.FilePath) ?? musicFolder);
                    return rel.Split(Path.DirectorySeparatorChar)[1];
                })
                .OrderBy(g => g.Key)
                .Select(g => new LibraryFolder { Name = g.Key, Songs = g.ToList() })
                .ToList();

            return new LibraryFolder
            {
                Name = topGroup.Key,
                Songs = directSongs,
                Children = children
            };
        }).ToList();

        LibraryList.ItemsSource = _allLibrarySongs;
        FolderList.ItemsSource = _allFolders;
        _libraryInitialized = true;
    }

    private async void PlaySong(int index)
    {
        playing = true;
        PlayPauseIcon.Data = Geometry.Parse("M6,5H10V19H6M14,5H18V19H14");
        PlayPauseIcon.Margin = new Thickness(0, 0, 0, 0);
        var song = songs[index];
        var art = MetadataReader.GetAlbumArt(song.FilePath);
        byte[]? artBytes = art;

        player.Play(song.FilePath);

        await Task.Delay(200);

        Title = "Milo's Music Player - " + song.Title;
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

        await _artServer.SetArt(artBytes);
        _rpc.UpdatePresence(songs[index], TimeSpan.Zero, player.Duration, _artServer.Url);

        _modLoader.FireOnTrackChange(SongToTrackInfo(song));
        _modLoader.FireOnPlay(SongToTrackInfo(song));

        if (FriendsPanel.DataContext is ViewModels.FriendsViewModel friendsVm)
            _ = friendsVm.PostActivity("Playing", song.Title, song.Artist);
    }

    private void UpdatePlaybackTime(object? sender, EventArgs e)
    {
        SongPosition.Text = FormattedTime(player.Position, player.Duration);

        if (player.Duration.TotalSeconds > 0)
        {
            double target = player.Position.TotalSeconds / player.Duration.TotalSeconds * 100;

            updatingSlider = true;
            ProgressSlider.Value = Helpers.Lerp(ProgressSlider.Value, target, 0.3);
            updatingSlider = false;
        }

        _spriteManager?.UpdateSprites();

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

        if (_repeat)
        {
            PlaySong(curSongIndex);
            return;
        }

        if (_shuffle)
        {
            var rng = new Random();
            int next;
            do { next = rng.Next(songs.Count); }
            while (songs.Count > 1 && next == curSongIndex);
            curSongIndex = next;
        }
        else
        {
            curSongIndex++;
            if (curSongIndex >= songs.Count)
                curSongIndex = 0;
        }

        PlaySong(curSongIndex);
    }

    private void PrevSong()
    {
        if (songs.Count == 0) return;

        curSongIndex--;
        if (curSongIndex < 0)
            curSongIndex = songs.Count - 1;

        PlaySong(curSongIndex);
    }

    private void ResumePlayback()
    {
        player.Resume();
        PlayPauseIcon.Data = Geometry.Parse("M6,5H10V19H6M14,5H18V19H14");
        PlayPauseIcon.Margin = new Thickness(0, 0, 0, 0);
        playing = true;
        _rpc.UpdatePresence(songs[curSongIndex], player.Position, player.Duration, _artServer.Url);
    }

    private void PausePlayback()
    {
        player.Pause();
        PlayPauseIcon.Data = Geometry.Parse("M8,5V19L19,12L8,5Z");
        PlayPauseIcon.Margin = new Thickness(3, 0, 0, 0);
        playing = false;
        _rpc.ClearPresence();
        _modLoader.FireOnPause();
    }

    private void ShuffleButton_Click(object? sender, RoutedEventArgs e)
    {
        _shuffle = !_shuffle;
        ShuffleButton.Classes.Set("mediaActive", _shuffle);
        ShuffleButton.Classes.Set("media", !_shuffle);
    }

    private void RepeatButton_Click(object? sender, RoutedEventArgs e)
    {
        _repeat = !_repeat;
        RepeatButton.Classes.Set("mediaActive", _repeat);
        RepeatButton.Classes.Set("media", !_repeat);
    }
    private void VolumeSlider_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        player.Volume = (float)e.NewValue;
        SettingsManager.Current.Volume = (float)e.NewValue;
        SettingsManager.Save();
        _modLoader?.FireOnVolumeChange((float)e.NewValue);
    }

    private void SkipForward_Click(object? sender, RoutedEventArgs e) => NextSong();

    private void SkipBack_Click(object? sender, RoutedEventArgs e) => PrevSong();

    private void PlayPause_Click(object? sender, RoutedEventArgs e)
    {
        if (playing)
            PausePlayback();
        else
            ResumePlayback();
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
    private async void FriendsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SettingsManager.Current.AuthToken))
        {
            var login = new Views.LoginWindow();
            await login.ShowDialog(this);
            if (string.IsNullOrEmpty(login.Token))
                return;

            SettingsManager.Current.AuthToken = login.Token;
            if (!string.IsNullOrWhiteSpace(login.UserId))
                SettingsManager.Current.UserId = login.UserId;

            if (!string.IsNullOrWhiteSpace(login.DisplayName))
                SettingsManager.Current.DisplayName = login.DisplayName;

            SettingsManager.Save();

            FriendsPanel.DataContext = new ViewModels.FriendsViewModel();
        }

        if (_friendsOpen)
            await FadeFriendsOut();
        else
            await FadeFriendsIn();
    }
    private async Task FadeFriendsIn()
    {
        _friendsOpen = true;
        FriendsPanel.IsVisible = true;
        FriendsPanel.Margin = new Thickness(Bounds.Width, 0, 0, 0);
        await AnimationHelper.AnimateMany(200,
            (FriendsPanel, "X", Bounds.Width, 0),
            (FriendsPanel, "Opacity", 0.0, 1.0)
        );
    }

    private async Task FadeFriendsOut()
    {
        await AnimationHelper.AnimateMany(200,
            (FriendsPanel, "X", 0, Bounds.Width),
            (FriendsPanel, "Opacity", 1.0, 0.0)
        );
        FriendsPanel.IsVisible = false;
        _friendsOpen = false;
    }
    private async Task FadeToLibrary()
    {
        _libraryOpen = true;
        _settingsOpen = false;
        _libraryReady = false;
        _friendsOpen = false;
        LibraryView.IsVisible = true;

        double startLeft = LibraryButton.Margin.Left;
        double targetLeft = Bounds.Width - 90;

        await AnimationHelper.AnimateMany(200,
            (PlayerView, "Opacity", 1.0, 0.0),
            (LibraryView, "Opacity", 0.0, 1.0),
            (ShaderButton, "Opacity", 1.0, 0.0),
            (SettingsButton, "Opacity", 1.0, 0.0),
            (FriendsButton, "Opacity", 1.0, 0.0),
            (SettingsView, "Opacity", SettingsView.Opacity, 0.0),
            (FriendsPanel, "Opacity", 1.0, 0.0),
            (LibraryButton, "X", startLeft, targetLeft)
        );

        PlayerView.IsVisible = false;
        SettingsView.IsVisible = false;
        FriendsPanel.IsVisible = false;
        _libraryReady = true;
    }

    private async Task FadeToPlayer()
    {
        bool wasSettings = _settingsOpen;
        _libraryOpen  = false;
        _settingsOpen = false;
        PlayerView.IsVisible = true;

        double startLeft = LibraryButton.Margin.Left;

        if (wasSettings)
        {
            SettingsButton.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
            _settingsButtonTranslateY = 0;

            await AnimationHelper.AnimateMany(200,
                (LibraryView,   "Opacity", LibraryView.Opacity,  0.0),
                (SettingsView,  "Opacity", SettingsView.Opacity, 0.0),
                (FriendsPanel,  "Opacity", FriendsPanel.Opacity, 0.0),
                (PlayerView,    "Opacity", 0.0, 1.0),
                (LibraryButton, "Opacity", 0.0, 1.0),
                (ShaderButton,  "Opacity", 0.0, 1.0),
                (FriendsButton, "Opacity", 0.0, 1.0),
                (SettingsButton, "Opacity", 0.0, 1.0)
            );
            _friendsOpen = false; 
        }
        else
        {
            await AnimationHelper.AnimateMany(200,
                (LibraryView,   "Opacity", LibraryView.Opacity,  0.0),
                (SettingsView,  "Opacity", SettingsView.Opacity, 0.0),
                (FriendsPanel,  "Opacity", FriendsPanel.Opacity, 0.0),
                (PlayerView,    "Opacity", 0.0, 1.0),
                (LibraryButton, "Opacity", 0.0, 1.0),
                (ShaderButton,  "Opacity", 0.0, 1.0),
                (SettingsButton, "Opacity", 0.0, 1.0),
                (FriendsButton, "Opacity", 0.0, 1.0),
                (LibraryButton, "X", startLeft, 0)
            );
            _friendsOpen = false;
        }

        LibraryView.IsVisible  = false;
        SettingsView.IsVisible = false;
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
    private async Task HydrateUserInfo()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Current.AuthToken)) return;
        try
        {
            var auth = new AuthService();
            var (userId, displayName) = await auth.GetMeAsync(SettingsManager.Current.AuthToken);
            SettingsManager.Current.UserId = userId;
            SettingsManager.Current.DisplayName = displayName;
            SettingsManager.Save();
        }
        catch { }
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
        => $"{FormatTime(position)} / {FormatTime(duration)}";

    private TrackInfo SongToTrackInfo(Song song) => new TrackInfo
    {
        Title = song.Title,
        Artist = song.Artist,
        Album = song.Genre,
        Genre = song.Genre,
        Duration = (float)player.Duration.TotalSeconds,
        Path = song.FilePath
    };
}