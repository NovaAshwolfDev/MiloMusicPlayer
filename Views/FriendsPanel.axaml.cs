using Avalonia.Controls;
using MiloMusicPlayer.ViewModels;

namespace MiloMusicPlayer.Views;

public partial class FriendsPanel : UserControl
{
    public FriendsPanel()
    {
        InitializeComponent();
        DataContext = new FriendsViewModel();
    }
}