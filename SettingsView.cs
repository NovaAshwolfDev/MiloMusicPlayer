using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using MiloMusicPlayer.Models;
using MiloMusicPlayer.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiloMusicPlayer.Views;

public partial class SettingsView : UserControl
{
    public event Action<AppSettings>? SettingsChanged;

    private static readonly Dictionary<string, string> _actionLabels = new()
    {
        ["PlayPause"]  = "Play / Pause",
        ["SkipNext"]   = "Skip Next",
        ["SkipPrev"]   = "Skip Previous",
        ["VolumeUp"]   = "Volume Up",
        ["VolumeDown"] = "Volume Down",
        ["Shuffle"]    = "Shuffle",
        ["Repeat"]     = "Repeat",
        ["Fullscreen"] = "Fullscreen",
    };

    private Button? _capturingButton = null;
    private string? _capturingAction = null;

    public SettingsView()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var s = SettingsManager.Current;
        VolumeSettingSlider.Value = s.Volume;
        VolumeValueLabel.Text = $"{(int)(s.Volume * 100)}%";
        RebuildFolderList();
        RebuildKeybindList();
    }

    private void RebuildFolderList()
    {
        FolderList.Children.Clear();
        foreach (var entry in SettingsManager.Current.MusicFolders)
            FolderList.Children.Add(BuildFolderRow(entry));
    }

    private Border BuildFolderRow(MusicFolderEntry entry)
    {
        var pathLabel = new TextBlock
        {
            Text = entry.Path,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#C7D9E8")),
            TextTrimming = TextTrimming.LeadingCharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var subfolderToggle = new CheckBox
        {
            Content = "Subfolders",
            IsChecked = entry.IncludeSubfolders,
            Foreground = new SolidColorBrush(Color.Parse("#7F96AA")),
            FontSize = 12,
        };
        subfolderToggle.IsCheckedChanged += (_, _) =>
        {
            entry.IncludeSubfolders = subfolderToggle.IsChecked ?? true;
            Save();
        };

        var removeBtn = new Button
        {
            Content = "Remove",
            Classes = { "danger" },
        };
        removeBtn.Click += (_, _) =>
        {
            SettingsManager.Current.MusicFolders.Remove(entry);
            Save();
            RebuildFolderList();
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
        };
        Grid.SetColumn(pathLabel, 0);
        Grid.SetColumn(subfolderToggle, 1);
        Grid.SetColumn(removeBtn, 2);

        var spaced = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
        };
        spaced.Children.Add(subfolderToggle);
        spaced.Children.Add(removeBtn);
        Grid.SetColumn(spaced, 1);

        row.Children.Add(pathLabel);
        row.Children.Add(spaced);

        return new Border
        {
            Classes = { "folder-row" },
            Child = row,
        };
    }

    private async void AddFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Folder",
            AllowMultiple = true,
        });

        foreach (var folder in folders)
        {
            var path = folder.Path.LocalPath;
            if (SettingsManager.Current.MusicFolders.Any(f => f.Path == path))
                continue;

            SettingsManager.Current.MusicFolders.Add(new MusicFolderEntry
            {
                Path = path,
                IncludeSubfolders = true,
            });
        }

        if (folders.Count > 0)
        {
            Save();
            RebuildFolderList();
        }
    }

    private void VolumeSettingSlider_Changed(object? sender,
        Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        VolumeValueLabel.Text = $"{(int)(e.NewValue * 100)}%";
        SettingsManager.Current.Volume = (float)e.NewValue;
        Save();
    }

    private void RebuildKeybindList()
    {
        KeybindList.Children.Clear();
        foreach (var kvp in SettingsManager.Current.Keybinds)
            KeybindList.Children.Add(BuildKeybindRow(kvp.Key, kvp.Value));
    }

    private Grid BuildKeybindRow(string action, string keyStr)
    {
        var label = new TextBlock
        {
            Text = _actionLabels.TryGetValue(action, out var friendly) ? friendly : action,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#C7D9E8")),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var btn = new Button
        {
            Content = keyStr,
            Classes = { "keybind-capture" },
            Tag = action,
        };
        btn.Click += KeybindButton_Click;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 0, 0, 0),
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(btn, 1);
        row.Children.Add(label);
        row.Children.Add(btn);
        return row;
    }

    private void KeybindButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        if (_capturingButton != null && _capturingButton != btn)
        {
            _capturingButton.Content = SettingsManager.Current.Keybinds[(string)_capturingButton.Tag!];
            _capturingButton.Classes.Remove("capturing");
        }

        _capturingButton = btn;
        _capturingAction = (string)btn.Tag!;
        btn.Content = "Press a key...";
        btn.Classes.Add("capturing");

        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;

        void OnKeyDown(object? s, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                btn.Content = SettingsManager.Current.Keybinds[_capturingAction!];
                btn.Classes.Remove("capturing");
                _capturingButton = null;
                _capturingAction = null;
                window.KeyDown -= OnKeyDown;
                args.Handled = true;
                return;
            }

            var keyString = args.Key.ToString();
            SettingsManager.Current.Keybinds[_capturingAction!] = keyString;
            btn.Content = keyString;
            btn.Classes.Remove("capturing");
            _capturingButton = null;
            _capturingAction = null;
            window.KeyDown -= OnKeyDown;
            Save();
            args.Handled = true;
        }

        window.KeyDown += OnKeyDown;
    }

    private void Save()
    {
        SettingsManager.Save();
        SettingsChanged?.Invoke(SettingsManager.Current);
    }
}