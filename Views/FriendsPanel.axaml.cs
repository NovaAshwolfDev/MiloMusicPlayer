using Avalonia.Controls;
using MiloMusicPlayer.ViewModels;
using System.ComponentModel;

namespace MiloMusicPlayer.Views;

public partial class FriendsPanel : UserControl
{
    private FriendsViewModel? _viewModel;

    public FriendsPanel()
    {
        InitializeComponent();
        DataContext = new FriendsViewModel();

        _viewModel = DataContext as FriendsViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateButtonStyles();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FriendsViewModel.IsFriendsVisible) ||
            e.PropertyName == nameof(FriendsViewModel.IsFeedVisible) ||
            e.PropertyName == nameof(FriendsViewModel.IsSessionVisible))
        {
            UpdateButtonStyles();
        }
    }

    private void UpdateButtonStyles()
    {
        if (_viewModel == null) return;

        void ApplyClasses(Button btn, bool isActive)
        {
            btn.Classes.Remove("media");
            btn.Classes.Remove("mediaActive");
            btn.Classes.Add("media");
            if (isActive)
                btn.Classes.Add("mediaActive");
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ApplyClasses(FriendsButton, _viewModel.IsFriendsVisible);
            ApplyClasses(ActivityButton, _viewModel.IsFeedVisible);
            ApplyClasses(SessionButton, _viewModel.IsSessionVisible);
        });
    }
}